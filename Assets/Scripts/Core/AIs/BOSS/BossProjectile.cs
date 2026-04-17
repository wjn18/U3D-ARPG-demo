using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BossProjectile : MonoBehaviour
{
    public GameObject owner;
    public float damage = 30f;
    public float speed = 30f;
    public float lifeTime = 5f;

    private Vector3 moveDirection;
    private BOSSAI ownerAI;
    private bool resolvedImpact;
    private GameObject hitEffectPrefab;
    private float hitEffectLifetime = 0.75f;
    private float hitEffectNormalOffset = 0.02f;

    public void Initialize(
        GameObject projectileOwner,
        BOSSAI projectileOwnerAI,
        Vector3 direction,
        float projectileDamage,
        float projectileSpeed,
        float projectileLifeTime,
        GameObject projectileHitEffectPrefab = null,
        float projectileHitEffectLifetime = 0.75f,
        float projectileHitEffectNormalOffset = 0.02f)
    {
        owner = projectileOwner;
        ownerAI = projectileOwnerAI;
        moveDirection = direction.normalized;
        damage = projectileDamage;
        speed = projectileSpeed;
        lifeTime = projectileLifeTime;
        hitEffectPrefab = projectileHitEffectPrefab;
        hitEffectLifetime = projectileHitEffectLifetime;
        hitEffectNormalOffset = projectileHitEffectNormalOffset;
        resolvedImpact = false;

        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        transform.position += moveDirection * speed * Time.deltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        TryHit(other.gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        TryHit(collision.gameObject);
    }

    void TryHit(GameObject other)
    {
        if (other == null)
            return;

        if (IsOwnerObject(other))
            return;

        IDamageable damageable = ResolveDamageable(other);
        Component damageableComponent = damageable as Component;
        if (damageable != null && damageableComponent != null)
        {
            Collider hitCollider = other.GetComponent<Collider>();
            if (hitCollider == null)
                hitCollider = other.GetComponentInParent<Collider>();

            Vector3 hitPoint = hitCollider != null
                ? hitCollider.ClosestPoint(transform.position)
                : other.transform.position;

            if (hitCollider != null && (hitPoint - transform.position).sqrMagnitude < 0.0001f)
                hitPoint = hitCollider.bounds.center;

            Vector3 hitNormal = hitPoint - transform.position;
            if (hitNormal.sqrMagnitude < 0.0001f)
                hitNormal = -moveDirection;

            damageable.TakeDamage(damage, owner);
            resolvedImpact = true;

            PlayHitEffect(hitPoint, hitNormal);

            PlayerStatsRuntime playerStats = damageableComponent.GetComponentInParent<PlayerStatsRuntime>();
            if (playerStats != null && ScreenShakeController.Instance != null)
                ScreenShakeController.Instance.PlayBossRangedHitShake();

            if (ownerAI != null)
                ownerAI.NotifyAttackHitConfirmed();

            Destroy(gameObject);
            return;
        }

        if (!other.isStatic)
        {
            resolvedImpact = true;

            if (ownerAI != null)
                ownerAI.NotifyRangedAttackMiss();

            Destroy(gameObject);
        }
    }

    bool IsOwnerObject(GameObject other)
    {
        if (owner == null || other == null)
            return false;

        if (other == owner || other.transform.root.gameObject == owner)
            return true;

        BOSSAI hitBoss = other.GetComponentInParent<BOSSAI>();
        return ownerAI != null && hitBoss == ownerAI;
    }

    IDamageable ResolveDamageable(GameObject other)
    {
        if (other == null)
            return null;

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
            return damageable;

        damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
            return damageable;

        damageable = other.GetComponentInChildren<IDamageable>(true);
        if (damageable != null)
            return damageable;

        Transform root = other.transform.root;
        if (root != null)
            return root.GetComponentInChildren<IDamageable>(true);

        return null;
    }

    void PlayHitEffect(Vector3 position, Vector3 normal)
    {
        if (hitEffectPrefab == null)
            return;

        Vector3 safeNormal = normal.sqrMagnitude > 0.0001f
            ? normal.normalized
            : Vector3.up;

        Vector3 spawnPosition = position + safeNormal * Mathf.Max(0f, hitEffectNormalOffset);
        Quaternion rotation = Quaternion.LookRotation(safeNormal, Vector3.up);
        GameObject instance = Instantiate(hitEffectPrefab, spawnPosition, rotation);
        Destroy(instance, Mathf.Max(0.01f, hitEffectLifetime));
    }

    void OnDestroy()
    {
        if (resolvedImpact)
            return;

        if (ownerAI != null)
            ownerAI.NotifyRangedAttackMiss();
    }
}
