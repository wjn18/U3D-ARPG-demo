using System.Collections.Generic;
using UnityEngine;

public class BossMeleeDamageWindow : MonoBehaviour
{
    [System.Serializable]
    public class MeleeDamageConfig
    {
        public int attackIndex;
        public int windowIndex;
        public Transform hitOriginOverride;
        public float damage = 20f;
        public float radius = 2f;
        public float facingAngle = 80f;
    }

    [Header("Refs")]
    public BOSSAI ownerAI;
    public Transform hitOrigin;

    [Header("Target")]
    public LayerMask targetLayers = ~0;

    [Header("Configs")]
    public MeleeDamageConfig[] meleeConfigs;

    [Header("Fallback")]
    public float fallbackRadius = 2f;
    public float fallbackDamage = 20f;
    public float fallbackFacingAngle = 80f;

    [Header("Debug")]
    public bool drawGizmos = true;
    public bool drawEvenWhenClosed = true;

    bool windowOpen = false;
    int activeAttackIndex = -1;
    int activeWindowIndex = 0;

    readonly HashSet<GameObject> hitTargetsThisWindow = new HashSet<GameObject>();
    Collider[] cachedTargetColliders;
    Transform cachedTarget;

    void Reset()
    {
        hitOrigin = transform;
    }

    void Update()
    {
        if (!windowOpen)
            return;

        Transform target = GetCurrentTarget();
        if (ownerAI == null || target == null)
            return;

        Transform origin = GetActiveHitOrigin();
        Vector3 center = origin.position;

        float radius = GetActiveRadius();
        float damage = GetActiveDamage();
        float facingAngle = GetActiveFacingAngle();

        if (!IsFacingTarget(center, facingAngle))
            return;

        CacheTargetCollidersIfNeeded();

        if (cachedTargetColliders == null || cachedTargetColliders.Length == 0)
            return;

        bool hitTarget = IsAnyTargetColliderInsideSphere(center, radius);
        if (!hitTarget)
            return;

        GameObject targetRoot = target.root.gameObject;
        if (hitTargetsThisWindow.Contains(targetRoot))
            return;

        Vector3 hitPoint = GetTargetAimPoint();
        Vector3 hitNormal = hitPoint - center;
        if (hitNormal.sqrMagnitude < 0.0001f)
            hitNormal = ownerAI.transform.forward;

        ApplyDamageToTarget(damage);
        hitTargetsThisWindow.Add(targetRoot);

        if (ownerAI != null)
            ownerAI.PlayCurrentAttackHitVFX(hitPoint, hitNormal);

        if (ownerAI != null)
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
        CacheTargetColliders(forceRefresh: true);
    }

    public void CloseWindow()
    {
        windowOpen = false;
        activeAttackIndex = -1;
        activeWindowIndex = 0;
        hitTargetsThisWindow.Clear();
        cachedTarget = null;
        cachedTargetColliders = null;
    }

    public void ForceCloseWindow()
    {
        CloseWindow();
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
            foreach (Collider c in selfAndChildren)
            {
                if (c == null) continue;
                if (!unique.Add(c)) continue;
                if (!IsColliderLayerAllowed(c.gameObject.layer)) continue;
                merged.Add(c);
            }
        }

        if (parents != null)
        {
            foreach (Collider c in parents)
            {
                if (c == null) continue;
                if (!unique.Add(c)) continue;
                if (!IsColliderLayerAllowed(c.gameObject.layer)) continue;
                merged.Add(c);
            }
        }

        cachedTargetColliders = merged.ToArray();
    }

    bool IsColliderLayerAllowed(int layer)
    {
        return (targetLayers.value & (1 << layer)) != 0;
    }

    bool IsAnyTargetColliderInsideSphere(Vector3 center, float radius)
    {
        float radiusSqr = radius * radius;

        foreach (Collider col in cachedTargetColliders)
        {
            if (col == null || !col.enabled)
                continue;

            Vector3 closest = col.ClosestPoint(center);
            float sqr = (closest - center).sqrMagnitude;
            if (sqr <= radiusSqr)
                return true;
        }

        return false;
    }

    bool IsFacingTarget(Vector3 fromPoint, float maxAngle)
    {
        if (ownerAI == null || GetCurrentTarget() == null)
            return false;

        Vector3 toTarget = GetTargetAimPoint() - fromPoint;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.001f)
            return true;

        Vector3 forward = ownerAI.transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            return true;

        float angle = Vector3.Angle(forward.normalized, toTarget.normalized);
        return angle <= maxAngle;
    }

    Vector3 GetTargetAimPoint()
    {
        Transform target = GetCurrentTarget();
        if (ownerAI == null || target == null)
            return transform.position;

        if (cachedTargetColliders == null || cachedTargetColliders.Length == 0)
            return target.position;

        Vector3 from = transform.position;
        float bestSqr = float.MaxValue;
        Vector3 bestPoint = target.position;

        foreach (Collider col in cachedTargetColliders)
        {
            if (col == null || !col.enabled)
                continue;

            Vector3 p = col.ClosestPoint(from);
            float sqr = (p - from).sqrMagnitude;

            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                bestPoint = p;
            }
        }

        return bestPoint;
    }

    MeleeDamageConfig GetConfig(int attackIndex, int windowIndex)
    {
        if (meleeConfigs == null)
            return null;

        MeleeDamageConfig attackFallback = null;

        for (int i = 0; i < meleeConfigs.Length; i++)
        {
            MeleeDamageConfig cfg = meleeConfigs[i];
            if (cfg == null || cfg.attackIndex != attackIndex)
                continue;

            if (cfg.windowIndex == windowIndex)
                return cfg;

            if (cfg.windowIndex == 0 && attackFallback == null)
                attackFallback = cfg;
        }

        return attackFallback;
    }

    float GetActiveRadius()
    {
        MeleeDamageConfig cfg = GetConfig(activeAttackIndex, activeWindowIndex);
        if (ownerAI != null)
            return ownerAI.GetAttackRadius(activeAttackIndex, activeWindowIndex, cfg != null ? cfg.radius : fallbackRadius);

        if (cfg != null)
            return Mathf.Max(0f, cfg.radius);

        return Mathf.Max(0f, fallbackRadius);
    }

    float GetActiveDamage()
    {
        MeleeDamageConfig cfg = GetConfig(activeAttackIndex, activeWindowIndex);
        if (ownerAI != null)
            return ownerAI.GetAttackDamage(activeAttackIndex, activeWindowIndex, cfg != null ? cfg.damage : fallbackDamage);

        if (cfg != null)
            return Mathf.Max(0f, cfg.damage);

        return Mathf.Max(0f, fallbackDamage);
    }

    float GetActiveFacingAngle()
    {
        MeleeDamageConfig cfg = GetConfig(activeAttackIndex, activeWindowIndex);
        if (ownerAI != null)
            return ownerAI.GetAttackFacingAngle(activeAttackIndex, activeWindowIndex, cfg != null ? cfg.facingAngle : fallbackFacingAngle);

        if (cfg != null)
            return Mathf.Clamp(cfg.facingAngle, 0f, 180f);

        return Mathf.Clamp(fallbackFacingAngle, 0f, 180f);
    }

    Transform GetActiveHitOrigin()
    {
        MeleeDamageConfig cfg = GetConfig(activeAttackIndex, activeWindowIndex);
        if (cfg != null && cfg.hitOriginOverride != null)
            return cfg.hitOriginOverride;

        if (hitOrigin != null)
            return hitOrigin;

        return transform;
    }

    void ApplyDamageToTarget(float damage)
    {
        Transform target = GetCurrentTarget();
        if (ownerAI == null || target == null)
            return;

        MonoBehaviour[] components = target.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour c in components)
        {
            if (c is IDamageable damageable)
            {
                damageable.TakeDamage(damage, ownerAI.gameObject);
                return;
            }
        }

        MonoBehaviour[] childComponents = target.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour c in childComponents)
        {
            if (c is IDamageable damageable)
            {
                damageable.TakeDamage(damage, ownerAI.gameObject);
                return;
            }
        }

        MonoBehaviour[] parentComponents = target.GetComponentsInParent<MonoBehaviour>(true);
        foreach (MonoBehaviour c in parentComponents)
        {
            if (c is IDamageable damageable)
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

        if (!drawEvenWhenClosed && !windowOpen)
            return;

        Transform origin = GetActiveHitOrigin();
        Vector3 center = origin.position;

        float radius = windowOpen ? GetActiveRadius() : Mathf.Max(0f, fallbackRadius);

        Gizmos.color = windowOpen ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(center, radius);
    }
}
