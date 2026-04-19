using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class GuardAI : MonoBehaviour
{
    public enum GuardState
    {
        Idle,
        Follow,
        Chase,
        Attack,
        Cooldown
    }

    [Header("Config")]
    public GuardConfig config;

    [Header("Refs")]
    public Transform player;
    public AudioSource audioSource;
    public GuardRuntime guardRuntime;

    [Header("Follow")]
    public float followDistance = 12f;          // 和玩家保持的理想距离
    public float followSlack = 1f;             // 允许的浮动范围，避免频繁抖动
    public float repathInterval = 0.25f;         // 跟随重算路径间隔
    public float followPointRefreshDistance = 1.0f; // 玩家移动这么多后，再更新站位点
    public float orbitJitter = 0.6f;             // 站位点的小随机偏移
    public float teleportBackDistance = 8f;      // 离玩家太远时，优先快速回位
    

    [Header("Detect")]
    public float detectRange = 15f;
    public float loseTargetRange = 20f;
    public string enemyTag = "Enemy";

    [Header("Move")]
    public float chaseStopDistance = 1.8f;       // 追敌时停止距离
    public float followStopDistance = 0.15f;     // 到达跟随点时的停止距离
    public float rotateSpeed = 10f;
    public float slowSpeed = 2f;  //靠近敌人后的移动速度
    public float slowDownDistance = 3.5f; // 开始减速的距离

    [Header("Attack")]
    public float attackRange = 2.2f;
    public float attackCooldown = 1.0f;
    public float healCooldown = 20.0f;
    public float attackHitTolerance = 0.5f;
    public float damage = 10f;
   

    [Header("Avoidance")]
    public bool setAvoidancePriorityAutomatically = true;
    [Range(0, 99)] public int avoidancePriority = 40;

    [Header("Debug")]
    public GuardState currentState = GuardState.Idle;
    public Transform currentTarget;
    public Transform attackerTarget;
    public bool drawGizmos = true;

    private NavMeshAgent agent;
    private float lastAttackTime = -999f;
    private float repathTimer = 0f;
    private bool damageAppliedThisAttack = false;
    private float nextMoveSoundTime = 0f;

    private EnemyRuntime lastAttackerEnemy;
    private EnemyRuntime lockedEnemyRuntime;
    private BOSSAI lastAttackerBoss;
    private BOSSAI lockedBossTarget;
    private Transform pendingAttackTarget;
    private GuardActionData currentAction;
    private PlayerStatsRuntime playerStats;

    private Vector3 currentFollowPoint;
    private Vector3 lastPlayerPosForFollow;
    private bool hasFollowPoint = false;

    public float CurrentSpeed { get; private set; }

    private float normalSpeed;
    public bool IsChasing { get; private set; }

    public event Action OnAttack;
    public event Action<GuardActionData> OnAction;

    // 敌人 -> 当前锁定它的 Guard
    private static Dictionary<EnemyRuntime, GuardAI> targetReservations =
        new Dictionary<EnemyRuntime, GuardAI>();

    private readonly Dictionary<GuardActionType, float> nextReadyTimeByAction =
        new Dictionary<GuardActionType, float>();

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (guardRuntime == null)
            guardRuntime = GetComponent<GuardRuntime>();

        if (config == null && guardRuntime != null)
            config = guardRuntime.config;

        normalSpeed = agent.speed;

        if (player == null)
        {
            GameObject obj = GameObject.FindGameObjectWithTag("Player");
            if (obj != null)
                player = obj.transform;
        }

        CachePlayerStats();

        agent.autoBraking = true;
        agent.stoppingDistance = followStopDistance;

        if (setAvoidancePriorityAutomatically)
        {
            agent.avoidancePriority = avoidancePriority;
        }

        currentState = GuardState.Idle;

        if (player != null)
        {
            lastPlayerPosForFollow = player.position;
        }
    }

    void Update()
    {
        if (player == null || agent == null || !agent.isOnNavMesh)
        {
            CurrentSpeed = 0f;
            return;
        }

        CleanupInvalidTargets();

        Transform chosenTarget = ChooseTarget();
        BOSSAI chosenBoss = GetBossFromTarget(chosenTarget);
        EnemyRuntime chosenEnemy = chosenBoss == null ? GetEnemyFromTarget(chosenTarget) : null;

        UpdateReservation(chosenEnemy);
        lockedBossTarget = chosenBoss;

        currentTarget = lockedBossTarget != null ? lockedBossTarget.transform : lockedEnemyRuntime != null ? lockedEnemyRuntime.transform : null;

        float distanceToTarget = DistanceToCurrentTargetXZ();
        float distanceToPlayer = DistanceToPlayerXZ();

        if (currentState != GuardState.Attack && currentState != GuardState.Cooldown && SelectReadyHealAction() != null)
        {
            StopAgentCompletely();
            currentState = GuardState.Attack;
        }

        switch (currentState)
        {
            case GuardState.Idle:
                UpdateIdle(distanceToPlayer);
                break;

            case GuardState.Follow:
                UpdateFollow(distanceToPlayer);
                break;

            case GuardState.Chase:
                UpdateChase(distanceToTarget, distanceToPlayer);
                break;

            case GuardState.Attack:
                UpdateAttack(distanceToTarget, distanceToPlayer);
                break;

            case GuardState.Cooldown:
                UpdateCooldown(distanceToTarget, distanceToPlayer);
                break;
        }

        CurrentSpeed = agent.velocity.magnitude;
        UpdateMoveAudio();
    }

    void UpdateIdle(float distanceToPlayer)
    {
        IsChasing = false;
        StopAgentCompletely();

        if (currentTarget != null)
        {
            currentState = GuardState.Chase;
            return;
        }

        currentState = GuardState.Follow;
    }

    void UpdateFollow(float distanceToPlayer)
    {
        IsChasing = false;
        agent.speed = normalSpeed;

        if (currentTarget != null)
        {
            currentState = GuardState.Chase;
            return;
        }

        agent.stoppingDistance = followStopDistance;

        float minKeepDistance = Mathf.Max(6.0f, followDistance - followSlack);
        float maxKeepDistance = followDistance + followSlack;

        // 在可接受范围内，就停下，不反复挤玩家
        if (distanceToPlayer >= minKeepDistance && distanceToPlayer <= maxKeepDistance)
        {
            StopAgentButKeepRotation();
            FaceSameDirectionAsPlayer();
            return;
        }

        // 太近了，立刻退到合适站位
        if (distanceToPlayer < minKeepDistance)
        {
            Vector3 retreatPoint = GetRetreatPointFromPlayer(minKeepDistance + 0.35f);
            MoveToFollowPoint(retreatPoint);
            return;
        }

        // 太远则重新计算护卫站位点
        repathTimer -= Time.deltaTime;
        bool playerMovedEnough = (player.position - lastPlayerPosForFollow).sqrMagnitude >=
                                 followPointRefreshDistance * followPointRefreshDistance;

        bool shouldRebuildFollowPoint =
            !hasFollowPoint ||
            repathTimer <= 0f ||
            playerMovedEnough ||
            distanceToPlayer > teleportBackDistance;

        if (shouldRebuildFollowPoint)
        {
            repathTimer = repathInterval;
            lastPlayerPosForFollow = player.position;

            Vector3 followPoint = BuildSmartFollowPoint();
            currentFollowPoint = followPoint;
            hasFollowPoint = true;
        }

        MoveToFollowPoint(currentFollowPoint);
    }

    void UpdateChase(float distanceToTarget, float distanceToPlayer)
    {
        if (currentTarget == null)
        {
            hasFollowPoint = false;
            currentState = GuardState.Follow;
            return;
        }

        if (distanceToTarget > loseTargetRange)
        {
            ReleaseCurrentReservation();
            hasFollowPoint = false;
            currentState = GuardState.Follow;
            return;
        }

        if (CanStartOffensiveAction(distanceToTarget))
        {
            IsChasing = false;
            StopAgentCompletely();
            currentState = GuardState.Attack;
            return;
        }

        if (distanceToTarget <= slowDownDistance)
        {
            agent.speed = slowSpeed;   // 靠近后慢下来
        }

        IsChasing = true;
        agent.isStopped = false;
        agent.stoppingDistance = chaseStopDistance;
        agent.SetDestination(currentTarget.position);
    }

    void UpdateAttack(float distanceToTarget, float distanceToPlayer)
    {
        if (currentTarget == null)
        {
            if (SelectReadyHealAction() != null)
            {
                IsChasing = false;
                StopAgentCompletely();
                TryAttack();
                return;
            }

            hasFollowPoint = false;
            currentState = GuardState.Follow;
            return;
        }

        IsChasing = false;
        StopAgentCompletely();
        FaceTarget(currentTarget.position);

        if (distanceToTarget > GetMaxOffensiveStartRange())
        {
            currentState = GuardState.Chase;
            return;
        }

        TryAttack();
    }

    void UpdateCooldown(float distanceToTarget, float distanceToPlayer)
    {
        if (SelectReadyHealAction() != null)
        {
            currentState = GuardState.Attack;
            return;
        }

        if (currentTarget == null)
        {
            hasFollowPoint = false;
            currentState = GuardState.Follow;
            return;
        }

        IsChasing = false;
        StopAgentCompletely();
        FaceTarget(currentTarget.position);

        if (SelectReadyOffensiveAction(distanceToTarget) != null)
        {
            currentState = GuardState.Attack;
            return;
        }

        if (distanceToTarget > GetMaxOffensiveStartRange() && distanceToTarget <= loseTargetRange)
        {
            currentState = GuardState.Chase;
            return;
        }

        if (distanceToTarget > loseTargetRange)
        {
            hasFollowPoint = false;
            currentState = GuardState.Follow;
        }
    }

    void TryAttack()
    {
        GuardActionData action = SelectReadyHealAction();
        if (action == null)
        {
            if (currentTarget == null) return;

            float distanceToTarget = DistanceToCurrentTargetXZ();
            action = SelectReadyOffensiveAction(distanceToTarget);
        }

        if (action == null)
        {
            currentState = GuardState.Cooldown;
            return;
        }

        lastAttackTime = Time.time;
        damageAppliedThisAttack = false;
        currentAction = action;
        pendingAttackTarget = IsHealAction(action) ? null : currentTarget;

        SetActionCooldown(action);
        PlayActionVoice(action);
        PlayActionHappenVFX(action);

        OnAction?.Invoke(action);
        OnAttack?.Invoke();
        currentState = GuardState.Cooldown;
    }

    public void ApplyAttackDamageNow()
    {
        if (IsHealAction(currentAction))
        {
            ApplyHealNow();
            return;
        }

        if (damageAppliedThisAttack) return;
        if (pendingAttackTarget == null) return;

        EnemyRuntime er = GetEnemyFromTarget(pendingAttackTarget);
        if (er != null && er.IsDead()) return;

        BOSSAI boss = GetBossFromTarget(pendingAttackTarget);
        if (boss != null && boss.IsDead()) return;

        float distance = GetDistanceToTargetXZ(pendingAttackTarget);
        if (distance > GetActionActualRange(currentAction) + attackHitTolerance)
            return;

        IDamageable damageable = GetDamageableFromTarget(pendingAttackTarget);
        if (damageable == null) return;

        Vector3 hitPoint = GetClosestTargetPoint(pendingAttackTarget, transform.position);
        Vector3 hitNormal = hitPoint - transform.position;
        if (hitNormal.sqrMagnitude < 0.0001f)
            hitNormal = transform.forward;

        damageAppliedThisAttack = true;
        damageable.TakeDamage(GetActionValue(currentAction), gameObject);
        PlayActionHitVoice(currentAction);
        PlayActionHitVFX(currentAction, hitPoint, hitNormal);
    }

    public void ApplyHealNow()
    {
        if (damageAppliedThisAttack) return;

        PlayerStatsRuntime targetStats = GetPlayerStats();
        if (targetStats == null || IsPlayerDead(targetStats))
            return;

        GuardActionData healAction = IsHealAction(currentAction)
            ? currentAction
            : GetConfiguredAction(GuardActionType.Heal);

        damageAppliedThisAttack = true;
        targetStats.Heal(GetActionValue(healAction));
        PlayActionHitVoice(healAction);
    }

    GuardActionData SelectReadyHealAction()
    {
        GuardActionData healAction = GetConfiguredAction(GuardActionType.Heal);
        if (healAction == null || !healAction.enabled)
            return null;

        if (!IsActionReady(healAction))
            return null;

        if (!CanHealPlayer())
            return null;

        return healAction;
    }

    bool CanHealPlayer()
    {
        PlayerStatsRuntime targetStats = GetPlayerStats();
        if (targetStats == null || IsPlayerDead(targetStats))
            return false;

        return targetStats.hp < targetStats.maxHP;
    }

    PlayerStatsRuntime GetPlayerStats()
    {
        if (playerStats != null)
            return playerStats;

        CachePlayerStats();
        return playerStats;
    }

    void CachePlayerStats()
    {
        if (player == null)
        {
            GameObject obj = GameObject.FindGameObjectWithTag("Player");
            if (obj != null)
                player = obj.transform;
        }

        if (player == null)
            return;

        playerStats = player.GetComponent<PlayerStatsRuntime>();
        if (playerStats == null)
            playerStats = player.GetComponentInParent<PlayerStatsRuntime>();
        if (playerStats == null)
            playerStats = player.GetComponentInChildren<PlayerStatsRuntime>();
    }

    bool IsPlayerDead(PlayerStatsRuntime targetStats)
    {
        if (targetStats == null)
            return true;

        PlayerCombatController combatController = targetStats.GetComponent<PlayerCombatController>();
        if (combatController != null && combatController.enabled)
            return combatController.IsDead;

        return targetStats.IsDeadState();
    }

    GuardActionData SelectReadyOffensiveAction(float distanceToTarget)
    {
        GuardActionData bestAction = null;
        float bestScore = float.MinValue;

        GuardActionData attack1 = GetConfiguredAction(GuardActionType.Attack1);
        EvaluateOffensiveAction(attack1, distanceToTarget, ref bestAction, ref bestScore);

        GuardActionData attack2 = GetConfiguredAction(GuardActionType.Attack2);
        EvaluateOffensiveAction(attack2, distanceToTarget, ref bestAction, ref bestScore);

        return bestAction;
    }

    void EvaluateOffensiveAction(GuardActionData action, float distanceToTarget, ref GuardActionData bestAction, ref float bestScore)
    {
        if (action == null || !action.enabled || IsHealAction(action))
            return;

        if (!IsActionReady(action))
            return;

        if (distanceToTarget > GetActionStartRange(action))
            return;

        float score = -Mathf.Abs(GetActionPreferredRange(action) - distanceToTarget);
        if (score > bestScore)
        {
            bestScore = score;
            bestAction = action;
        }
    }

    bool CanStartOffensiveAction(float distanceToTarget)
    {
        return distanceToTarget <= GetMaxOffensiveStartRange();
    }

    float GetMaxOffensiveStartRange()
    {
        float maxRange = Mathf.Max(0f, attackRange);

        GuardActionData attack1 = GetConfiguredAction(GuardActionType.Attack1);
        if (attack1 != null && attack1.enabled)
            maxRange = Mathf.Max(maxRange, GetActionStartRange(attack1));

        GuardActionData attack2 = GetConfiguredAction(GuardActionType.Attack2);
        if (attack2 != null && attack2.enabled)
            maxRange = Mathf.Max(maxRange, GetActionStartRange(attack2));

        return maxRange;
    }

    GuardActionData GetConfiguredAction(GuardActionType actionType)
    {
        if (config != null && config.actions != null)
        {
            for (int i = 0; i < config.actions.Length; i++)
            {
                GuardActionData action = config.actions[i];
                if (action != null && action.actionType == actionType)
                    return action;
            }
        }

        return CreateLegacyAction(actionType);
    }

    GuardActionData CreateLegacyAction(GuardActionType actionType)
    {
        switch (actionType)
        {
            case GuardActionType.Attack1:
                return new GuardActionData
                {
                    actionName = "Attack 1",
                    actionType = GuardActionType.Attack1,
                    value = damage,
                    cooldownTime = attackCooldown,
                    preferredRange = attackRange,
                    actualRange = attackRange,
                    animatorChooseValue = 0
                };

            case GuardActionType.Attack2:
                return new GuardActionData
                {
                    actionName = "Attack 2",
                    actionType = GuardActionType.Attack2,
                    value = damage,
                    cooldownTime = attackCooldown,
                    preferredRange = attackRange,
                    actualRange = attackRange,
                    animatorChooseValue = 2
                };

            case GuardActionType.Heal:
                return new GuardActionData
                {
                    enabled = false,
                    actionName = "Heal",
                    actionType = GuardActionType.Heal,
                    value = 0f,
                    cooldownTime = healCooldown,
                    preferredRange = 0f,
                    actualRange = 0f,
                    animatorChooseValue = 1
                };

            default:
                return null;
        }
    }

    bool IsActionReady(GuardActionData action)
    {
        if (action == null)
            return false;

        float readyTime = 0f;
        nextReadyTimeByAction.TryGetValue(action.actionType, out readyTime);
        return Time.time >= readyTime;
    }

    void SetActionCooldown(GuardActionData action)
    {
        if (action == null)
            return;

        nextReadyTimeByAction[action.actionType] = Time.time + Mathf.Max(0f, action.cooldownTime);
    }

    bool IsHealAction(GuardActionData action)
    {
        return action != null && action.actionType == GuardActionType.Heal;
    }

    float GetActionValue(GuardActionData action)
    {
        if (action != null && action.value > 0f)
            return Mathf.Max(0f, action.value);

        return Mathf.Max(0f, damage);
    }

    float GetActionPreferredRange(GuardActionData action)
    {
        if (action == null)
            return Mathf.Max(0f, attackRange);

        if (action.preferredRange <= 0f && !IsHealAction(action))
            return Mathf.Max(0f, attackRange);

        return Mathf.Max(0f, action.preferredRange);
    }

    float GetActionActualRange(GuardActionData action)
    {
        if (action == null)
            return Mathf.Max(0f, attackRange);

        if (action.actualRange <= 0f && !IsHealAction(action))
            return GetActionPreferredRange(action);

        return Mathf.Max(0f, action.actualRange);
    }

    float GetActionStartRange(GuardActionData action)
    {
        if (action == null)
            return Mathf.Max(0f, attackRange);

        return Mathf.Max(GetActionPreferredRange(action), GetActionActualRange(action)) + Mathf.Max(0f, attackHitTolerance);
    }

    Vector3 BuildSmartFollowPoint()
    {
        Vector3 toGuard = transform.position - player.position;
        toGuard.y = 0f;

        Vector3 baseDir;

        if (toGuard.sqrMagnitude > 0.01f)
        {
            baseDir = toGuard.normalized;
        }
        else
        {
            baseDir = -player.forward;
            baseDir.y = 0f;
            if (baseDir.sqrMagnitude < 0.01f)
                baseDir = Vector3.back;
            baseDir.Normalize();
        }

        // 尝试多个候选点，选一个既在玩家周围，又不会太贴脸的位置
        const int maxTry = 5;
        Vector3 bestPoint = player.position + baseDir * followDistance;
        float bestScore = float.MaxValue;
        bool found = false;

        for (int i = 0; i < maxTry; i++)
        {
            float angleOffset = UnityEngine.Random.Range(-55f, 55f);
            Vector3 dir = Quaternion.Euler(0f, angleOffset, 0f) * baseDir;

            float radius = UnityEngine.Random.Range(followDistance - 0.2f, followDistance + 0.4f);
            Vector3 rawPoint = player.position + dir * radius;

            Vector2 jitter2D = UnityEngine.Random.insideUnitCircle * orbitJitter;
            rawPoint += new Vector3(jitter2D.x, 0f, jitter2D.y);

            Vector3 fromPlayer = rawPoint - player.position;
            fromPlayer.y = 0f;

            if (fromPlayer.magnitude < followDistance - followSlack)
            {
                fromPlayer = fromPlayer.normalized * (followDistance - followSlack);
                rawPoint = player.position + fromPlayer;
            }

            NavMeshHit hit;
            if (!NavMesh.SamplePosition(rawPoint, out hit, 2f, NavMesh.AllAreas))
                continue;

            float distToIdeal = Mathf.Abs(Vector3.Distance(hit.position, player.position) - followDistance);
            float moveCost = Vector3.Distance(transform.position, hit.position);
            float score = distToIdeal * 2f + moveCost * 0.35f;

            if (score < bestScore)
            {
                bestScore = score;
                bestPoint = hit.position;
                found = true;
            }
        }

        if (!found)
        {
            NavMeshHit hit;
            Vector3 fallback = player.position + baseDir * followDistance;
            if (NavMesh.SamplePosition(fallback, out hit, 2f, NavMesh.AllAreas))
                bestPoint = hit.position;
            else
            {
                Vector3 fallbackNearPlayer = player.position + baseDir * Mathf.Min(followDistance, 3f);

                if (NavMesh.SamplePosition(fallbackNearPlayer, out hit, 4f, NavMesh.AllAreas))
                    bestPoint = hit.position;
                
            }
        }

        return bestPoint;
    }

    Vector3 GetRetreatPointFromPlayer(float retreatDistance)
    {
        Vector3 dir = transform.position - player.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.01f)
        {
            dir = -player.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector3.back;
        }

        dir.Normalize();

        Vector3 rawPoint = player.position + dir * retreatDistance;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(rawPoint, out hit, 2f, NavMesh.AllAreas))
            return hit.position;

        return transform.position;
    }

    void MoveToFollowPoint(Vector3 point)
    {
        float distToPoint = Vector3.Distance(transform.position, point);

        if (distToPoint <= followStopDistance + 0.05f)
        {
            StopAgentButKeepRotation();
            FaceSameDirectionAsPlayer();
            return;
        }

        agent.isStopped = false;
        agent.stoppingDistance = followStopDistance;
        agent.SetDestination(point);
    }

    void StopAgentCompletely()
    {
        if (agent == null) return;

        agent.isStopped = true;
        if (agent.hasPath)
            agent.ResetPath();
    }

    void StopAgentButKeepRotation()
    {
        if (agent == null) return;

        agent.isStopped = true;
        if (agent.hasPath)
            agent.ResetPath();
    }

    void FaceTarget(Vector3 targetPos)
    {
        Vector3 dir = targetPos - transform.position;
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

    void FaceSameDirectionAsPlayer()
    {
        if (player == null) return;

        Vector3 dir = player.forward;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                Time.deltaTime * rotateSpeed * 0.6f
            );
        }
    }

    Transform ChooseTarget()
    {
        BOSSAI bossTarget = ChooseBossTarget();
        if (bossTarget != null)
            return bossTarget.transform;

        EnemyRuntime enemyTarget = ChooseTargetEnemy();
        return enemyTarget != null ? enemyTarget.transform : null;
    }

    BOSSAI ChooseBossTarget()
    {
        if (IsBossValid(lastAttackerBoss))
            return lastAttackerBoss;

        return FindNearestBossInRange();
    }

    BOSSAI FindNearestBossInRange()
    {
        BOSSAI[] bosses = FindObjectsOfType<BOSSAI>();
        BOSSAI best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < bosses.Length; i++)
        {
            BOSSAI boss = bosses[i];
            if (!IsBossValid(boss))
                continue;

            float distToSelf = GetDistanceToTargetXZ(boss.transform);
            if (distToSelf > detectRange)
                continue;

            float distToPlayer = player != null
                ? Vector3.Distance(player.position, boss.transform.position)
                : distToSelf;

            if (distToPlayer < bestDist)
            {
                bestDist = distToPlayer;
                best = boss;
            }
        }

        return best;
    }

    EnemyRuntime ChooseTargetEnemy()
    {
        // 1. 最高优先级：反击打我的敌人
        if (lastAttackerEnemy != null && IsEnemyValid(lastAttackerEnemy))
            return lastAttackerEnemy;

        // 2. 攻击正在攻击 player 的敌人
        EnemyRuntime attackingPlayer = FindEnemyAttackingPlayer();
        if (attackingPlayer != null)
            return attackingPlayer;

        // 3. 打自己范围内离 player 最近的敌人
        EnemyRuntime nearestToPlayer = FindNearestEnemyToPlayerInRange();
        if (nearestToPlayer != null)
            return nearestToPlayer;

        return null;
    }

    EnemyRuntime FindEnemyAttackingPlayer()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);

        EnemyRuntime best = null;
        float bestDist = float.MaxValue;

        foreach (GameObject go in enemies)
        {
            if (!go.activeInHierarchy) continue;

            EnemyRuntime enemyRuntime = go.GetComponent<EnemyRuntime>();
            EnemyAI enemyAI = go.GetComponent<EnemyAI>();

            if (enemyRuntime == null || enemyAI == null) continue;
            if (enemyRuntime.IsDead()) continue;
            if (enemyAI.currentTarget != player) continue;
            if (IsReservedByOther(enemyRuntime)) continue;

            float d = Vector3.Distance(transform.position, go.transform.position);
            if (d <= detectRange && d < bestDist)
            {
                bestDist = d;
                best = enemyRuntime;
            }
        }

        return best;
    }

    EnemyRuntime FindNearestEnemyToPlayerInRange()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);

        EnemyRuntime best = null;
        float bestDistToPlayer = float.MaxValue;

        foreach (GameObject go in enemies)
        {
            if (!go.activeInHierarchy) continue;

            EnemyRuntime enemyRuntime = go.GetComponent<EnemyRuntime>();
            if (enemyRuntime == null) continue;
            if (enemyRuntime.IsDead()) continue;
            if (IsReservedByOther(enemyRuntime)) continue;

            float distToSelf = Vector3.Distance(transform.position, go.transform.position);
            if (distToSelf > detectRange) continue;

            float distToPlayer = Vector3.Distance(player.position, go.transform.position);
            if (distToPlayer < bestDistToPlayer)
            {
                bestDistToPlayer = distToPlayer;
                best = enemyRuntime;
            }
        }

        return best;
    }

    void CleanupInvalidTargets()
    {
        if (!IsBossValid(lastAttackerBoss))
            lastAttackerBoss = null;

        if (!IsBossValid(lockedBossTarget))
            lockedBossTarget = null;

        if (lastAttackerEnemy != null)
        {
            if (!lastAttackerEnemy.gameObject.activeInHierarchy || lastAttackerEnemy.IsDead())
            {
                lastAttackerEnemy = null;
            }
            else
            {
                attackerTarget = lastAttackerEnemy.transform;
            }
        }
        else
        {
            attackerTarget = null;
        }

        if (lastAttackerBoss != null)
            attackerTarget = lastAttackerBoss.transform;

        if (lockedEnemyRuntime != null)
        {
            if (!lockedEnemyRuntime.gameObject.activeInHierarchy || lockedEnemyRuntime.IsDead())
            {
                ReleaseCurrentReservation();
            }
        }

        if (pendingAttackTarget != null)
        {
            EnemyRuntime er = GetEnemyFromTarget(pendingAttackTarget);
            BOSSAI boss = GetBossFromTarget(pendingAttackTarget);
            if (!pendingAttackTarget.gameObject.activeInHierarchy || (er != null && er.IsDead()) || (boss != null && boss.IsDead()))
            {
                pendingAttackTarget = null;
                damageAppliedThisAttack = false;
            }
        }
    }

    bool IsEnemyValid(EnemyRuntime enemy)
    {
        if (enemy == null) return false;
        if (!enemy.gameObject.activeInHierarchy) return false;
        if (enemy.IsDead()) return false;
        return true;
    }

    bool IsBossValid(BOSSAI boss)
    {
        if (boss == null) return false;
        if (!boss.gameObject.activeInHierarchy) return false;
        if (boss.IsDead()) return false;
        return true;
    }

    void UpdateReservation(EnemyRuntime newEnemy)
    {
        if (newEnemy == lockedEnemyRuntime)
            return;

        ReleaseCurrentReservation();

        if (newEnemy == null)
            return;

        bool isCounterAttackTarget = (newEnemy == lastAttackerEnemy);

        if (isCounterAttackTarget)
        {
            targetReservations[newEnemy] = this;
            lockedEnemyRuntime = newEnemy;
            return;
        }

        if (!targetReservations.TryGetValue(newEnemy, out GuardAI owner) || owner == null || owner == this)
        {
            targetReservations[newEnemy] = this;
            lockedEnemyRuntime = newEnemy;
        }
    }

    void ReleaseCurrentReservation()
    {
        if (lockedEnemyRuntime != null)
        {
            if (targetReservations.TryGetValue(lockedEnemyRuntime, out GuardAI owner))
            {
                if (owner == this)
                {
                    targetReservations.Remove(lockedEnemyRuntime);
                }
            }
        }

        lockedEnemyRuntime = null;
        lockedBossTarget = null;
        currentTarget = null;
    }

    bool IsReservedByOther(EnemyRuntime enemy)
    {
        if (enemy == null) return true;

        if (targetReservations.TryGetValue(enemy, out GuardAI owner))
        {
            return owner != null && owner != this;
        }

        return false;
    }

    float DistanceToCurrentTargetXZ()
    {
        return GetDistanceToTargetXZ(currentTarget);
    }

    float GetDistanceToTargetXZ(Transform target)
    {
        if (target == null)
            return float.MaxValue;

        Vector3 a = transform.position;
        Vector3 b = GetClosestTargetPoint(target, transform.position);
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    Vector3 GetClosestTargetPoint(Transform target, Vector3 fromPoint)
    {
        if (target == null)
            return fromPoint;

        Collider[] childColliders = target.GetComponentsInChildren<Collider>(true);
        Collider[] parentColliders = target.GetComponentsInParent<Collider>(true);

        Vector3 bestPoint = target.position;
        float bestSqr = float.MaxValue;

        CheckClosestPoint(childColliders, fromPoint, ref bestPoint, ref bestSqr);
        CheckClosestPoint(parentColliders, fromPoint, ref bestPoint, ref bestSqr);

        return bestPoint;
    }

    void CheckClosestPoint(Collider[] colliders, Vector3 fromPoint, ref Vector3 bestPoint, ref float bestSqr)
    {
        if (colliders == null)
            return;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || !col.enabled)
                continue;

            Vector3 point = col.ClosestPoint(fromPoint);
            float sqr = (point - fromPoint).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                bestPoint = point;
            }
        }
    }

    BOSSAI GetBossFromTarget(Transform target)
    {
        if (target == null)
            return null;

        BOSSAI boss = target.GetComponent<BOSSAI>();
        if (boss != null)
            return boss;

        boss = target.GetComponentInParent<BOSSAI>();
        if (boss != null)
            return boss;

        return target.GetComponentInChildren<BOSSAI>(true);
    }

    EnemyRuntime GetEnemyFromTarget(Transform target)
    {
        if (target == null)
            return null;

        EnemyRuntime enemy = target.GetComponent<EnemyRuntime>();
        if (enemy != null)
            return enemy;

        enemy = target.GetComponentInParent<EnemyRuntime>();
        if (enemy != null)
            return enemy;

        return target.GetComponentInChildren<EnemyRuntime>(true);
    }

    IDamageable GetDamageableFromTarget(Transform target)
    {
        if (target == null)
            return null;

        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
            return damageable;

        damageable = target.GetComponentInParent<IDamageable>();
        if (damageable != null)
            return damageable;

        return target.GetComponentInChildren<IDamageable>(true);
    }

    float DistanceToPlayerXZ()
    {
        if (player == null)
            return float.MaxValue;

        Vector3 a = transform.position;
        Vector3 b = player.position;
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    public void NotifyBeingAttacked(EnemyRuntime attacker)
    {
        if (attacker == null) return;
        if (attacker.IsDead()) return;

        lastAttackerEnemy = attacker;
        lastAttackerBoss = null;
        attackerTarget = attacker.transform;

        if (currentState == GuardState.Follow || currentState == GuardState.Idle)
            currentState = GuardState.Chase;
    }

    public void NotifyBeingAttacked(BOSSAI attacker)
    {
        if (!IsBossValid(attacker)) return;

        lastAttackerBoss = attacker;
        lastAttackerEnemy = null;
        attackerTarget = attacker.transform;

        if (currentState == GuardState.Follow || currentState == GuardState.Idle)
            currentState = GuardState.Chase;
    }

    public void PlayHurtVoice()
    {
        PlayRandomClip(config != null && config.audio != null ? config.audio.hurtVoiceClips : null);
    }

    public void PlayDieVoice()
    {
        PlayRandomClip(config != null && config.audio != null ? config.audio.dieVoiceClips : null);
    }

    void PlayActionVoice(GuardActionData action)
    {
        if (action == null)
            return;

        PlayRandomClip(action.actionVoiceClips);
    }

    void PlayActionHitVoice(GuardActionData action)
    {
        if (action == null)
            return;

        PlayRandomClip(action.actionHitVoiceClips);
    }

    void PlayActionHappenVFX(GuardActionData action)
    {
        if (action == null || action.attackHappenVFXPrefab == null)
            return;

        Transform socket = ResolveActionVFXSocket(action.attackHappenVFXDisplaySocket);

        GameObject instance = Instantiate(
            action.attackHappenVFXPrefab,
            socket.position,
            socket.rotation,
            socket
        );

        instance.SetActive(true);
        RestartActionVFX(instance);
        Destroy(instance, Mathf.Max(0.01f, action.attackHappenVFXLifetime));
    }

    void RestartActionVFX(GameObject effectObject)
    {
        if (effectObject == null)
            return;

        ParticleSystem[] particleSystems = effectObject.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Play(true);
        }
    }

    Transform ResolveActionVFXSocket(GameObject configuredSocket)
    {
        if (configuredSocket == null)
            return transform;

        Transform configuredTransform = configuredSocket.transform;
        if (configuredTransform == transform || configuredTransform.IsChildOf(transform))
            return configuredTransform;

        Transform localSocket = FindChildByName(transform, configuredSocket.name);
        return localSocket != null ? localSocket : transform;
    }

    Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child;

            Transform nested = FindChildByName(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    void PlayActionHitVFX(GuardActionData action, Vector3 position, Vector3 normal)
    {
        if (action == null || action.hitEffectPrefab == null)
            return;

        SpawnHitEffect(
            action.hitEffectPrefab,
            position,
            normal,
            action.hitEffectLifetime,
            action.hitEffectNormalOffset
        );
    }

    void SpawnHitEffect(GameObject prefab, Vector3 position, Vector3 normal, float lifetime, float normalOffset)
    {
        if (prefab == null)
            return;

        Vector3 safeNormal = normal.sqrMagnitude > 0.0001f
            ? normal.normalized
            : Vector3.up;

        Vector3 spawnPosition = position + safeNormal * Mathf.Max(0f, normalOffset);
        Quaternion rotation = Quaternion.LookRotation(safeNormal, Vector3.up);
        GameObject instance = Instantiate(prefab, spawnPosition, rotation);
        Destroy(instance, Mathf.Max(0.01f, lifetime));
    }

    void UpdateMoveAudio()
    {
        if (config == null || config.audio == null)
            return;

        if (CurrentSpeed < Mathf.Max(0f, config.audio.moveSoundMinSpeed))
            return;

        if (Time.time < nextMoveSoundTime)
            return;

        nextMoveSoundTime = Time.time + Mathf.Max(0.05f, config.audio.moveSoundInterval);
        PlayRandomClip(config.audio.moveClips);
    }

    void PlayRandomClip(AudioClip[] clips)
    {
        if (audioSource == null || clips == null || clips.Length == 0)
            return;

        AudioClip clip = GetRandomClip(clips);
        if (clip == null)
            return;

        audioSource.PlayOneShot(clip);
    }

    AudioClip GetRandomClip(AudioClip[] clips)
    {
        int startIndex = UnityEngine.Random.Range(0, clips.Length);
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[(startIndex + i) % clips.Length];
            if (clip != null)
                return clip;
        }

        return null;
    }

    void OnDisable()
    {
        ReleaseCurrentReservation();
    }

    void OnDestroy()
    {
        ReleaseCurrentReservation();
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, GetMaxOffensiveStartRange());

        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, loseTargetRange);

        if (player != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(player.position, followDistance);

            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(player.position, Mathf.Max(0.1f, followDistance - followSlack));

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(player.position, followDistance + followSlack);

            if (hasFollowPoint)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawSphere(currentFollowPoint, 0.15f);
                Gizmos.DrawLine(transform.position, currentFollowPoint);
            }
        }

        if (currentTarget != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }
}
