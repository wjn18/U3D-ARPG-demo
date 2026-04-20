using System.Collections.Generic;
using UnityEngine;

public class BossMeleeDamageWindow : MonoBehaviour
{
    [Header("Refs")]
    public BOSSAI ownerAI;

    [Header("Target")]
    public LayerMask targetLayers = ~0;

    [Header("Debug")]
    public bool drawGizmos = true;
    public bool drawEvenWhenClosed = true;

    bool windowOpen = false;
    int activeAttackIndex = -1;
    int activeWindowIndex = 0;

    readonly HashSet<GameObject> hitTargetsThisWindow = new HashSet<GameObject>();
    readonly List<Collider> activeHitColliders = new List<Collider>();

    Collider[] cachedTargetColliders;
    Transform cachedTarget;

    void Update()
    {
        if (!windowOpen)
            return;

        Transform target = GetCurrentTarget();
        if (ownerAI == null || target == null)
            return;

        CacheTargetCollidersIfNeeded();
        if (cachedTargetColliders == null || cachedTargetColliders.Length == 0)
            return;

        if (!TryFindHitOverlap(out Vector3 hitPoint, out Vector3 hitNormal))
            return;

        GameObject targetRoot = target.root.gameObject;
        if (hitTargetsThisWindow.Contains(targetRoot))
            return;

        ApplyDamageToTarget(GetActiveDamage());
        hitTargetsThisWindow.Add(targetRoot);

        ownerAI.PlayCurrentAttackHitVFX(hitPoint, hitNormal);
        ownerAI.NotifyAttackHitConfirmed();
    }

    public void OpenWindow(int attackIndex)
    {
        OpenWindow(attackIndex, 0);
    }

    public void OpenWindow(int attackIndex, int windowIndex)
    {
        activeAttackIndex = attackIndex;
        activeWindowIndex = Mathf.Max(0, windowIndex);
        windowOpen = true;
        hitTargetsThisWindow.Clear();
        RefreshActiveHitColliders();
        CacheTargetColliders(forceRefresh: true);
    }

    public void CloseWindow()
    {
        windowOpen = false;
        activeAttackIndex = -1;
        activeWindowIndex = 0;
        activeHitColliders.Clear();
        hitTargetsThisWindow.Clear();
        cachedTarget = null;
        cachedTargetColliders = null;
    }

    public void ForceCloseWindow()
    {
        CloseWindow();
    }

    void RefreshActiveHitColliders()
    {
        activeHitColliders.Clear();

        if (ownerAI == null)
            return;

        BossMeleeWindowData windowData = ownerAI.GetAttackWindowData(activeAttackIndex, activeWindowIndex);
        if (windowData == null)
        {
            Debug.LogWarning($"Boss melee window data missing for attack {activeAttackIndex}, window {activeWindowIndex}.", this);
            return;
        }

        if (windowData.hitboxIds == null || windowData.hitboxIds.Length == 0)
        {
            Debug.LogWarning($"Boss melee hitbox IDs missing for attack {activeAttackIndex}, window {activeWindowIndex}.", this);
            return;
        }

        HashSet<Collider> uniqueColliders = new HashSet<Collider>();

        for (int i = 0; i < windowData.hitboxIds.Length; i++)
        {
            string hitboxId = windowData.hitboxIds[i];
            if (string.IsNullOrWhiteSpace(hitboxId))
                continue;

            Transform hitboxTransform = FindHitboxTransform(hitboxId);
            if (hitboxTransform == null)
            {
                Debug.LogWarning($"Boss melee hitbox '{hitboxId}' was not found under boss '{ownerAI.name}'.", this);
                continue;
            }

            Collider[] colliders = hitboxTransform.GetComponents<Collider>();
            if (colliders == null || colliders.Length == 0)
            {
                Debug.LogWarning($"Boss melee hitbox '{hitboxId}' must have a Collider on the exact hitbox object.", hitboxTransform);
                continue;
            }

            for (int j = 0; j < colliders.Length; j++)
            {
                Collider collider = colliders[j];
                if (collider == null || !uniqueColliders.Add(collider))
                    continue;

                activeHitColliders.Add(collider);
            }
        }

        if (activeHitColliders.Count == 0)
            Debug.LogWarning($"Boss melee hitboxes resolved no valid colliders for attack {activeAttackIndex}, window {activeWindowIndex}.", this);
    }

    Transform FindHitboxTransform(string hitboxId)
    {
        if (ownerAI == null || string.IsNullOrWhiteSpace(hitboxId))
            return null;

        Transform bossRoot = ownerAI.transform;
        Transform directMatch = bossRoot.Find(hitboxId);
        if (directMatch != null)
            return directMatch;

        Transform[] allChildren = bossRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allChildren.Length; i++)
        {
            Transform child = allChildren[i];
            if (child != null && child.name == hitboxId)
                return child;
        }

        return null;
    }

    void CacheTargetCollidersIfNeeded()
    {
        if (cachedTargetColliders == null || cachedTargetColliders.Length == 0 || cachedTarget != GetCurrentTarget())
            CacheTargetColliders(forceRefresh: true);
    }

    void CacheTargetColliders(bool forceRefresh)
    {
        if (!forceRefresh && cachedTargetColliders != null && cachedTargetColliders.Length > 0)
            return;

        Transform target = GetCurrentTarget();
        if (ownerAI == null || target == null)
        {
            cachedTarget = null;
            cachedTargetColliders = System.Array.Empty<Collider>();
            return;
        }

        cachedTarget = target;

        Collider[] selfAndChildren = target.GetComponentsInChildren<Collider>(true);
        Collider[] parents = target.GetComponentsInParent<Collider>(true);

        int total = (selfAndChildren?.Length ?? 0) + (parents?.Length ?? 0);
        if (total == 0)
        {
            cachedTargetColliders = System.Array.Empty<Collider>();
            return;
        }

        List<Collider> merged = new List<Collider>(total);
        HashSet<Collider> unique = new HashSet<Collider>();

        if (selfAndChildren != null)
        {
            foreach (Collider collider in selfAndChildren)
            {
                if (collider == null) continue;
                if (!unique.Add(collider)) continue;
                if (!IsColliderLayerAllowed(collider.gameObject.layer)) continue;
                merged.Add(collider);
            }
        }

        if (parents != null)
        {
            foreach (Collider collider in parents)
            {
                if (collider == null) continue;
                if (!unique.Add(collider)) continue;
                if (!IsColliderLayerAllowed(collider.gameObject.layer)) continue;
                merged.Add(collider);
            }
        }

        cachedTargetColliders = merged.ToArray();
    }

    bool IsColliderLayerAllowed(int layer)
    {
        return (targetLayers.value & (1 << layer)) != 0;
    }

    bool TryFindHitOverlap(out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = transform.position;
        hitNormal = ownerAI != null ? ownerAI.transform.forward : Vector3.forward;

        for (int i = 0; i < activeHitColliders.Count; i++)
        {
            Collider attackCollider = activeHitColliders[i];
            if (!IsUsableCollider(attackCollider))
                continue;

            for (int j = 0; j < cachedTargetColliders.Length; j++)
            {
                Collider targetCollider = cachedTargetColliders[j];
                if (!IsUsableCollider(targetCollider))
                    continue;

                if (!CollidersOverlap(attackCollider, targetCollider, out Vector3 penetrationDirection, out float penetrationDistance))
                    continue;

                Vector3 attackCenter = attackCollider.bounds.center;
                hitPoint = targetCollider.ClosestPoint(attackCenter);
                if ((hitPoint - attackCenter).sqrMagnitude < 0.0001f)
                    hitPoint = targetCollider.bounds.center;

                hitNormal = hitPoint - attackCenter;
                if (hitNormal.sqrMagnitude < 0.0001f)
                    hitNormal = penetrationDistance > 0f ? -penetrationDirection : ownerAI.transform.forward;

                return true;
            }
        }

        return false;
    }

    bool IsUsableCollider(Collider collider)
    {
        return collider != null && collider.enabled && collider.gameObject.activeInHierarchy;
    }

    bool CollidersOverlap(Collider attackCollider, Collider targetCollider, out Vector3 direction, out float distance)
    {
        direction = Vector3.zero;
        distance = 0f;

        return Physics.ComputePenetration(
            attackCollider,
            attackCollider.transform.position,
            attackCollider.transform.rotation,
            targetCollider,
            targetCollider.transform.position,
            targetCollider.transform.rotation,
            out direction,
            out distance);
    }

    float GetActiveDamage()
    {
        return ownerAI != null
            ? ownerAI.GetAttackDamage(activeAttackIndex, activeWindowIndex, 0f)
            : 0f;
    }

    void ApplyDamageToTarget(float damage)
    {
        Transform target = GetCurrentTarget();
        if (ownerAI == null || target == null)
            return;

        MonoBehaviour[] components = target.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour component in components)
        {
            if (component is IDamageable damageable)
            {
                damageable.TakeDamage(damage, ownerAI.gameObject);
                return;
            }
        }

        MonoBehaviour[] childComponents = target.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour component in childComponents)
        {
            if (component is IDamageable damageable)
            {
                damageable.TakeDamage(damage, ownerAI.gameObject);
                return;
            }
        }

        MonoBehaviour[] parentComponents = target.GetComponentsInParent<MonoBehaviour>(true);
        foreach (MonoBehaviour component in parentComponents)
        {
            if (component is IDamageable damageable)
            {
                damageable.TakeDamage(damage, ownerAI.gameObject);
                return;
            }
        }
    }

    Transform GetCurrentTarget()
    {
        return ownerAI != null ? ownerAI.CurrentTarget : null;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        if (!windowOpen && !drawEvenWhenClosed)
            return;

        Gizmos.color = windowOpen ? Color.red : Color.yellow;

        for (int i = 0; i < activeHitColliders.Count; i++)
        {
            Collider collider = activeHitColliders[i];
            if (!IsUsableCollider(collider))
                continue;

            DrawColliderGizmo(collider);
        }
    }

    void DrawColliderGizmo(Collider collider)
    {
        switch (collider)
        {
            case BoxCollider box:
                Matrix4x4 oldBoxMatrix = Gizmos.matrix;
                Gizmos.matrix = box.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = oldBoxMatrix;
                break;

            case SphereCollider sphere:
                Matrix4x4 oldSphereMatrix = Gizmos.matrix;
                Gizmos.matrix = sphere.transform.localToWorldMatrix;
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                Gizmos.matrix = oldSphereMatrix;
                break;

            case CapsuleCollider capsule:
                Bounds capsuleBounds = capsule.bounds;
                Gizmos.DrawWireCube(capsuleBounds.center, capsuleBounds.size);
                break;

            default:
                Bounds bounds = collider.bounds;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
                break;
        }
    }
}
