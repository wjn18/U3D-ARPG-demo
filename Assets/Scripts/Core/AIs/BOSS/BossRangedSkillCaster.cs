using UnityEngine;

public class BossRangedSkillCaster : MonoBehaviour
{
    [System.Serializable]
    public class ProjectileConfig
    {
        public int attackIndex = 6;
        public BossProjectile projectilePrefab;
        public Transform firePoint;
        public float damage = 25f;
        public float speed = 12f;
        public float lifeTime = 5f;
    }

    [Header("Refs")]
    public BOSSAI ownerAI;
    public Transform target;

    [Header("Configs")]
    public ProjectileConfig[] projectileConfigs;

    public bool Fire(int attackIndex, Vector3 attackDirection)
    {
        ProjectileConfig cfg = GetConfig(attackIndex);
        if (cfg == null || cfg.projectilePrefab == null || cfg.firePoint == null)
            return false;

        attackDirection.y = 0f;

        Vector3 direction = attackDirection.sqrMagnitude > 0.0001f
            ? attackDirection.normalized
            : cfg.firePoint.forward;

        BossProjectile projectile = Instantiate(
            cfg.projectilePrefab,
            cfg.firePoint.position,
            Quaternion.LookRotation(direction, Vector3.up)
        );

        projectile.Initialize(
            ownerAI != null ? ownerAI.gameObject : gameObject,
            ownerAI,
            direction,
            ownerAI != null ? ownerAI.GetProjectileDamage(attackIndex, cfg.damage) : cfg.damage,
            ownerAI != null ? ownerAI.GetProjectileSpeed(attackIndex, cfg.speed) : cfg.speed,
            ownerAI != null ? ownerAI.GetProjectileLifeTime(attackIndex, cfg.lifeTime) : cfg.lifeTime,
            ownerAI != null ? ownerAI.GetAttackHitEffectPrefab(attackIndex) : null,
            ownerAI != null ? ownerAI.GetAttackHitEffectLifetime(attackIndex, 0.75f) : 0.75f,
            ownerAI != null ? ownerAI.GetAttackHitEffectNormalOffset(attackIndex, 0.02f) : 0.02f
        );

        return true;
    }

    ProjectileConfig GetConfig(int attackIndex)
    {
        if (projectileConfigs == null)
            return null;

        for (int i = 0; i < projectileConfigs.Length; i++)
        {
            if (projectileConfigs[i] != null && projectileConfigs[i].attackIndex == attackIndex)
                return projectileConfigs[i];
        }

        return null;
    }
}
