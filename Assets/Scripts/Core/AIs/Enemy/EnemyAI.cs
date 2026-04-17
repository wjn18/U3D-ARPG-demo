using System;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public enum EnemyState
    {
        Idle,
        Chase,
        Attack,
        Cooldown
    }

    [Header("Config")]
    public EnemyConfig config;

    [Header("Target")]
    public Transform player;
    public Transform currentTarget;

    [Header("Detect")]
    public float detectRange = 25f;
    public float loseTargetRange = 30f;

    [Header("Priority Check")]
    public float priorityRange = 6f;

    [Header("Move")]
    public float stopDistance = 2.2f;
    public float rotateSpeed = 10f;

    [Header("Attack")]
    public float attackRange = 2.0f;
    public float attackCooldown = 1.2f;
    public float attackFacingAngle = 20f;
    public float damageFacingAngle = 35f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip swordSound;

    [Header("Debug")]
    public EnemyState currentState = EnemyState.Idle;

    private NavMeshAgent agent;
    private EnemyAnimationController enemyAnimationController;
    private Collider[] targetColliders;
    private float lastAttackTime = -999f;
    private bool damageAppliedThisAttack = false;
    private float stopTimer = 0f;
    private bool isDead = false;

    public float CurrentSpeed { get; private set; }
    public bool IsChasing { get; private set; }

    public event Action OnAttack;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        enemyAnimationController = GetComponent<EnemyAnimationController>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
            audioSource.clip = swordSound;

        if (agent != null)
            agent.stoppingDistance = stopDistance;

        if (player == null)
        {
            GameObject obj = GameObject.FindGameObjectWithTag("Player");
            if (obj != null)
                player = obj.transform;
        }

        currentTarget = player;
        CacheTargetColliders();
        currentState = EnemyState.Idle;
    }

    void Update()
    {
        if (isDead)
        {
            CurrentSpeed = 0f;
            return;
        }

        if (IsAnimationLocked())
        {
            ForceStopForAnimationLock();
            return;
        }

        if (stopTimer > 0f)
        {
            stopTimer -= Time.deltaTime;

            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;

                if (stopTimer <= 0f)
                    agent.isStopped = false;
            }

            return;
        }

        if (player == null || agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            CurrentSpeed = 0f;
            return;
        }

        RefreshCurrentTarget();

        if (currentTarget == null)
        {
            CurrentSpeed = 0f;
            return;
        }

        if (targetColliders == null || targetColliders.Length == 0)
            CacheTargetColliders();

        float distanceToTarget = DistanceToTargetSurfaceXZ();

        switch (currentState)
        {
            case EnemyState.Idle:
                UpdateIdle(distanceToTarget);
                break;
            case EnemyState.Chase:
                UpdateChase(distanceToTarget);
                break;
            case EnemyState.Attack:
                UpdateAttack(distanceToTarget);
                break;
            case EnemyState.Cooldown:
                UpdateCooldown(distanceToTarget);
                break;
        }

        CurrentSpeed = agent.velocity.magnitude;
    }

    bool IsAnimationLocked()
    {
        if (enemyAnimationController == null)
            return false;

        return enemyAnimationController.IsInHeavyHitReact ||
               enemyAnimationController.IsInFlyingBackDeath ||
               enemyAnimationController.IsInStandingUp;
    }

    void ForceStopForAnimationLock()
    {
        CurrentSpeed = 0f;
        IsChasing = false;
        currentState = EnemyState.Idle;
        damageAppliedThisAttack = false;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }

    public void SetDead(bool value)
    {
        isDead = value;

        if (!isDead) return;

        CurrentSpeed = 0f;
        IsChasing = false;
        currentState = EnemyState.Idle;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }

    void RefreshCurrentTarget()
    {
        if (player == null) return;

        float distToPlayer = DistanceXZ(transform.position, player.position);
        if (distToPlayer <= priorityRange)
        {
            if (currentTarget != player)
            {
                currentTarget = player;
                CacheTargetColliders();
            }
            return;
        }

        Transform nearestGuard = FindNearestGuardInRange(priorityRange);
        if (nearestGuard != null)
        {
            if (currentTarget != nearestGuard)
            {
                currentTarget = nearestGuard;
                CacheTargetColliders();
                stopTimer = 1.0f;
            }
            return;
        }

        if (currentTarget != player)
        {
            currentTarget = player;
            CacheTargetColliders();
            stopTimer = 1.0f;
        }
    }

    Transform FindNearestGuardInRange(float range)
    {
        GameObject[] guards = GameObject.FindGameObjectsWithTag("Guard");

        Transform best = null;
        float bestDist = float.MaxValue;

        foreach (GameObject go in guards)
        {
            if (!go.activeInHierarchy) continue;

            float dist = DistanceXZ(transform.position, go.transform.position);
            if (dist <= range && dist < bestDist)
            {
                bestDist = dist;
                best = go.transform;
            }
        }

        return best;
    }

    void UpdateIdle(float distance)
    {
        IsChasing = false;
        agent.ResetPath();

        if (distance <= detectRange)
            currentState = EnemyState.Chase;
    }

    void UpdateChase(float distance)
    {
        if (distance > loseTargetRange)
        {
            IsChasing = false;
            agent.ResetPath();
            currentState = EnemyState.Idle;
            return;
        }

        if (distance <= attackRange)
        {
            IsChasing = false;
            agent.ResetPath();
            currentState = EnemyState.Attack;
            return;
        }

        IsChasing = true;
        agent.isStopped = false;
        agent.stoppingDistance = stopDistance;
        agent.SetDestination(GetTargetAimPoint());
    }

    void UpdateAttack(float distance)
    {
        IsChasing = false;
        agent.ResetPath();
        FaceTarget();

        if (distance > attackRange)
        {
            currentState = EnemyState.Chase;
            return;
        }

        if (!IsFacingTarget(attackFacingAngle))
        {
            agent.isStopped = true;
            return;
        }

        TryAttack();
    }

    void UpdateCooldown(float distance)
    {
        IsChasing = false;
        agent.ResetPath();
        FaceTarget();

        if (Time.time >= lastAttackTime + attackCooldown)
        {
            if (distance <= attackRange)
                currentState = EnemyState.Attack;
            else if (distance <= loseTargetRange)
                currentState = EnemyState.Chase;
            else
                currentState = EnemyState.Idle;
        }
    }

    void TryAttack()
    {
        if (Time.time < lastAttackTime + attackCooldown)
            return;

        if (!IsFacingTarget(attackFacingAngle))
            return;

        lastAttackTime = Time.time;
        damageAppliedThisAttack = false;

        OnAttack?.Invoke();
        currentState = EnemyState.Cooldown;
    }

    bool IsFacingTarget(float maxAngle)
    {
        if (currentTarget == null) return false;

        Vector3 dir = GetTargetAimPoint() - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            return true;

        dir.Normalize();
        float angle = Vector3.Angle(transform.forward, dir);
        return angle <= maxAngle;
    }

    void FaceTarget()
    {
        if (currentTarget == null) return;

        Vector3 dir = GetTargetAimPoint() - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                Time.deltaTime * rotateSpeed
            );
        }
    }

    Vector3 GetTargetAimPoint()
    {
        if (currentTarget == null)
            return transform.position;

        if (targetColliders == null || targetColliders.Length == 0)
            return currentTarget.position;

        Vector3 from = transform.position;
        float bestSqr = float.MaxValue;
        Vector3 bestPoint = currentTarget.position;

        foreach (Collider col in targetColliders)
        {
            if (col == null || !col.enabled) continue;

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

    float DistanceToTargetSurfaceXZ()
    {
        if (currentTarget == null)
            return float.MaxValue;

        if (targetColliders == null || targetColliders.Length == 0)
        {
            Vector3 a = transform.position;
            Vector3 b = currentTarget.position;
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        Vector3 enemyPos = transform.position;
        enemyPos.y = 0f;

        float minDistance = float.MaxValue;

        foreach (Collider col in targetColliders)
        {
            if (col == null || !col.enabled) continue;

            Vector3 closest = col.ClosestPoint(transform.position);
            closest.y = 0f;

            float dist = Vector3.Distance(enemyPos, closest);
            if (dist < minDistance)
                minDistance = dist;
        }

        return minDistance;
    }

    void CacheTargetColliders()
    {
        if (currentTarget == null)
        {
            targetColliders = null;
            return;
        }

        Collider[] selfAndChildren = currentTarget.GetComponentsInChildren<Collider>(true);
        Collider[] parents = currentTarget.GetComponentsInParent<Collider>(true);

        int total = (selfAndChildren?.Length ?? 0) + (parents?.Length ?? 0);
        if (total == 0)
        {
            targetColliders = Array.Empty<Collider>();
            return;
        }

        Collider[] merged = new Collider[total];
        int index = 0;

        if (selfAndChildren != null)
        {
            foreach (var c in selfAndChildren)
            {
                if (c != null) merged[index++] = c;
            }
        }

        if (parents != null)
        {
            foreach (var c in parents)
            {
                if (c != null) merged[index++] = c;
            }
        }

        if (index < merged.Length)
            Array.Resize(ref merged, index);

        targetColliders = merged;
    }

    public void ApplyAttackDamageNow()
    {
        if (isDead) return;
        if (damageAppliedThisAttack) return;
        if (currentTarget == null) return;
        if (IsAnimationLocked()) return;

        float distance = DistanceToTargetSurfaceXZ();
        if (distance > attackRange + 0.5f)
            return;

        damageAppliedThisAttack = true;

        if (audioSource != null && swordSound != null)
            audioSource.PlayOneShot(swordSound);

        float damage = config != null ? config.attackDamage : 0f;
        ApplyDamageToCurrentTarget(damage);
    }

    void ApplyDamageToCurrentTarget(float damage)
    {
        if (currentTarget == null) return;

        MonoBehaviour[] components = currentTarget.GetComponents<MonoBehaviour>();
        foreach (var c in components)
        {
            if (c is IDamageable damageable)
            {
                damageable.TakeDamage(damage, gameObject);
                return;
            }
        }

        MonoBehaviour[] childComponents = currentTarget.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var c in childComponents)
        {
            if (c is IDamageable damageable)
            {
                damageable.TakeDamage(damage, gameObject);
                return;
            }
        }

        MonoBehaviour[] parentComponents = currentTarget.GetComponentsInParent<MonoBehaviour>(true);
        foreach (var c in parentComponents)
        {
            if (c is IDamageable damageable)
            {
                damageable.TakeDamage(damage, gameObject);
                return;
            }
        }
    }

    float DistanceXZ(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, loseTargetRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, priorityRange);
    }
}
