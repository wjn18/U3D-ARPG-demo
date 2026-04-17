using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(BossAnimatorController))]
[RequireComponent(typeof(BossRuntime))]
public class BOSSAI : MonoBehaviour, IDamageable
{
    public enum BossState
    {
        Idle,
        Approach,
        Retreat,
        Attacking,
        Dead
    }

    [Header("Config")]
    public BossConfig config;

    [Header("Refs")]
    public Transform player;
    public NavMeshAgent agent;
    public BossAnimatorController animatorController;
    public BossMeleeDamageWindow meleeDamageWindow;
    public BossRangedSkillCaster rangedSkillCaster;
    public BossStaggerSystem staggerSystem;
    public BossWeaponTrailController weaponTrailVFX;
    public CombatAudioController combatAudioController;
    public BossRuntime bossRuntime;

    [FormerlySerializedAs("maxHP"), SerializeField, HideInInspector] private float legacyMaxHP = 1000f;
    [FormerlySerializedAs("currentHP"), SerializeField, HideInInspector] private float legacyCurrentHP = 1000f;

    [Header("Detection")]
    public float engageDistance = 25f;

    [Header("Locomotion")]
    public float approachStopDistance = 3.5f;
    public float repathInterval = 0.15f;
    public float combatDistanceTolerance = 0.35f;

    [Header("Target Switching")]
    public float damageWindowDuration = 2f;
    public float playerSwitchDamageThreshold = 30f;
    public float guardSwitchDamageThreshold = 25f;
    public float targetSwitchCooldown = 10f;

    [FormerlySerializedAs("attacks"), SerializeField, HideInInspector] private BossAttackDefinition[] legacyAttacks = BossConfig.CreateDefaultAttackSet();

    [Header("Debug")]
    public bool drawGizmos = true;

    public float maxHP => bossRuntime != null ? bossRuntime.maxHP : legacyMaxHP;
    public float currentHP => bossRuntime != null ? bossRuntime.currentHP : legacyCurrentHP;
    public BossAttackDefinition[] attacks => GetAttackSet();
    public BossState currentState
    {
        get => bossRuntime != null ? bossRuntime.currentState : BossState.Idle;
        private set
        {
            if (bossRuntime != null)
                bossRuntime.SetState(value);
        }
    }
    public int currentAttackIndex => bossRuntime != null ? bossRuntime.currentAttackIndex : -1;
    public BossAttackPhase currentAttackPhase => bossRuntime != null ? bossRuntime.currentAttackPhase : BossAttackPhase.None;
    public Transform CurrentTarget => bossRuntime != null && bossRuntime.currentTarget != null ? bossRuntime.currentTarget : player;

    private readonly Dictionary<int, float> nextReadyTimeByIndex = new Dictionary<int, float>();
    private struct DamageRecord
    {
        public float time;
        public float amount;

        public DamageRecord(float time, float amount)
        {
            this.time = time;
            this.amount = amount;
        }
    }

    private readonly List<DamageRecord> recentPlayerDamage = new List<DamageRecord>();
    private readonly Dictionary<GuardRuntime, List<DamageRecord>> recentGuardDamageBySource = new Dictionary<GuardRuntime, List<DamageRecord>>();

    private BossAttackDefinition currentAttack;
    private float currentAttackElapsed;
    private float lastRepathTime;
    private int currentAttackWindowIndex;
    private bool attackHitConfirmedThisWindow;
    private bool rangedAttackResultPending;
    private float nextAllowedTargetSwitchTime;

    void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animatorController == null)
            animatorController = GetComponent<BossAnimatorController>();

        if (staggerSystem == null)
            staggerSystem = GetComponent<BossStaggerSystem>();

        if (weaponTrailVFX == null)
            weaponTrailVFX = GetComponentInChildren<BossWeaponTrailController>(true);

        if (combatAudioController == null)
            combatAudioController = GetComponentInChildren<CombatAudioController>(true);

        if (bossRuntime == null)
            bossRuntime = GetComponent<BossRuntime>();
        if (bossRuntime == null)
            bossRuntime = gameObject.AddComponent<BossRuntime>();

        if (config == null && bossRuntime != null)
            config = bossRuntime.config;

        if (bossRuntime != null)
        {
            if (bossRuntime.config == null)
                bossRuntime.config = config;

            float initialHP = legacyCurrentHP > 0f ? legacyCurrentHP : legacyMaxHP;
            bossRuntime.Initialize(config, player, initialHP, -1f, legacyMaxHP);
        }

        ApplyConfig();

        agent.updateRotation = false;

        if (legacyAttacks == null || legacyAttacks.Length == 0)
            legacyAttacks = BossConfig.CreateDefaultAttackSet();

        ApplyAttackPayloadDefaults();
        BuildCooldownTable();
    }

    void ApplyConfig()
    {
        if (config == null)
            return;

        engageDistance = config.engageDistance;
        approachStopDistance = config.approachStopDistance;
        repathInterval = config.repathInterval;
        combatDistanceTolerance = config.combatDistanceTolerance;

        if (bossRuntime != null)
            bossRuntime.config = config;

        if (staggerSystem != null)
            staggerSystem.config = config;

        ApplyBossAudioConfig();
    }

    BossAttackDefinition[] GetAttackSet()
    {
        if (config != null && config.attacks != null && config.attacks.Length > 0)
            return config.attacks;

        if (legacyAttacks == null || legacyAttacks.Length == 0)
            legacyAttacks = BossConfig.CreateDefaultAttackSet();

        return legacyAttacks;
    }

    void ApplyBossAudioConfig()
    {
        if (combatAudioController == null || config == null || config.audio == null)
            return;

        if (HasAnyClip(config.audio.hurtVoiceClips))
            combatAudioController.hurtClips = config.audio.hurtVoiceClips;

        if (HasAnyClip(config.audio.dieVoiceClips))
            combatAudioController.dieVoiceClips = config.audio.dieVoiceClips;
    }

    void ApplyAttackPayloadDefaults()
    {
        if (attacks == null)
            return;

        BossAttackData[] defaults = BossConfig.CreateDefaultAttackSet();

        for (int i = 0; i < attacks.Length; i++)
        {
            BossAttackDefinition attack = attacks[i];
            if (attack == null)
                continue;

            BossAttackDefinition defaultAttack = FindAttack(defaults, attack.attackIndex);
            if (defaultAttack == null)
                continue;

            if (attack.damage <= 0f)
                attack.damage = defaultAttack.damage;

            if (attack.actualAttackRange <= 0f)
                attack.actualAttackRange = defaultAttack.ActualRange;

            if (attack.projectileSpeed <= 0f)
                attack.projectileSpeed = defaultAttack.projectileSpeed;

            if (attack.projectileLifeTime <= 0f)
                attack.projectileLifeTime = defaultAttack.projectileLifeTime;

            if (attack.meleeWindows == null || attack.meleeWindows.Length == 0)
                attack.meleeWindows = defaultAttack.meleeWindows;
        }
    }

    BossAttackDefinition FindAttack(BossAttackDefinition[] source, int attackIndex)
    {
        if (source == null)
            return null;

        for (int i = 0; i < source.Length; i++)
        {
            BossAttackDefinition attack = source[i];
            if (attack != null && attack.attackIndex == attackIndex)
                return attack;
        }

        return null;
    }

    bool HasAnyClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return false;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                return true;
        }

        return false;
    }

    void Start()
    {
        SetCurrentTarget(player);

        if (meleeDamageWindow != null)
            meleeDamageWindow.ownerAI = this;

        if (rangedSkillCaster != null)
            rangedSkillCaster.ownerAI = this;
    }

    void Update()
    {
        if (player == null || currentState == BossState.Dead)
            return;

        EnsureValidCurrentTarget();

        if (staggerSystem != null && staggerSystem.ShouldLockBossAction())
        {
            ForceInterruptAction();
            return;
        }

        if (currentHP <= 0f)
        {
            Die();
            return;
        }

        if (currentState == BossState.Attacking)
        {
            UpdateAttackState();
            return;
        }

        float distance = DistanceToCurrentTarget();
        if (distance > engageDistance)
        {
            StopMove();
            currentState = BossState.Idle;
            return;
        }

        BossAttackDefinition attack = SelectAttack(distance);
        if (attack != null)
        {
            StartAttack(attack);
            return;
        }

        UpdatePositioning(distance);
    }

    void SetCurrentTarget(Transform target)
    {
        if (target == null)
            target = player;

        if (bossRuntime != null)
            bossRuntime.currentTarget = target;

        if (animatorController != null)
            animatorController.SetTarget(target);

        if (rangedSkillCaster != null)
            rangedSkillCaster.target = target;
    }

    void EnsureValidCurrentTarget()
    {
        Transform target = CurrentTarget;
        if (target != null && target.gameObject.activeInHierarchy && !IsTargetDead(target))
            return;

        SetCurrentTarget(player);
    }

    void BuildCooldownTable()
    {
        nextReadyTimeByIndex.Clear();

        if (attacks == null)
            return;

        for (int i = 0; i < attacks.Length; i++)
        {
            BossAttackDefinition attack = attacks[i];
            if (attack == null)
                continue;

            if (!nextReadyTimeByIndex.ContainsKey(attack.attackIndex))
                nextReadyTimeByIndex.Add(attack.attackIndex, 0f);
        }
    }

    float DistanceToCurrentTarget()
    {
        Transform target = CurrentTarget;
        if (target == null)
            return float.MaxValue;

        Vector3 a = transform.position;
        Vector3 b = target.position;
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    BossAttackDefinition SelectAttack(float distanceToPlayer)
    {
        if (attacks == null || attacks.Length == 0)
            return null;

        List<BossAttackDefinition> candidates = new List<BossAttackDefinition>();
        int bestScore = int.MinValue;

        for (int i = 0; i < attacks.Length; i++)
        {
            BossAttackDefinition attack = attacks[i];
            if (attack == null)
                continue;

            if (!IsAttackReady(attack))
                continue;

            if (!attack.IsInRange(distanceToPlayer))
                continue;

            int score = EvaluateAttackScore(attack, distanceToPlayer);
            if (score > bestScore)
            {
                bestScore = score;
                candidates.Clear();
                candidates.Add(attack);
            }
            else if (score == bestScore)
            {
                candidates.Add(attack);
            }
        }

        if (candidates.Count == 0)
            return null;

        return candidates[Random.Range(0, candidates.Count)];
    }

    int EvaluateAttackScore(BossAttackDefinition attack, float distanceToPlayer)
    {
        int score = attack.priority * 1000;
        score -= Mathf.RoundToInt(Mathf.Abs(attack.PreferredRange - distanceToPlayer) * 100f);

        switch (attack.category)
        {
            case BossAttackCategory.MeleeSkill:
                score += 100;
                break;
            case BossAttackCategory.Ranged:
                score += 50;
                break;
        }

        return score;
    }

    bool IsAttackReady(BossAttackDefinition attack)
    {
        if (attack == null)
            return false;

        if (!nextReadyTimeByIndex.TryGetValue(attack.attackIndex, out float readyTime))
            return true;

        return Time.time >= readyTime;
    }

    void SetAttackCooldown(BossAttackDefinition attack)
    {
        if (attack == null)
            return;

        nextReadyTimeByIndex[attack.attackIndex] = Time.time + Mathf.Max(0f, attack.cooldown);
    }

    void UpdatePositioning(float distanceToPlayer)
    {
        currentState = BossState.Approach;

        Transform target = CurrentTarget;
        if (target == null)
        {
            StopMove();
            return;
        }

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            StopMove();
            return;
        }

        float desiredDistance = GetDesiredCombatDistance(distanceToPlayer);
        if (Mathf.Abs(distanceToPlayer - desiredDistance) <= combatDistanceTolerance)
        {
            StopMove();
            return;
        }

        Vector3 desiredPosition = target.position - direction.normalized * desiredDistance;
        if (Time.time - lastRepathTime <= repathInterval)
            return;

        lastRepathTime = Time.time;
        agent.isStopped = false;
        agent.updatePosition = true;
        agent.SetDestination(desiredPosition);
    }

    float GetDesiredCombatDistance(float distanceToPlayer)
    {
        BossAttackDefinition spacingAttack = SelectSpacingAttack(distanceToPlayer);
        if (spacingAttack != null)
            return Mathf.Max(0f, spacingAttack.PreferredRange);

        return Mathf.Max(0f, approachStopDistance);
    }

    BossAttackDefinition SelectSpacingAttack(float distanceToPlayer)
    {
        if (attacks == null || attacks.Length == 0)
            return null;

        BossAttackDefinition bestAttack = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < attacks.Length; i++)
        {
            BossAttackDefinition attack = attacks[i];
            if (attack == null)
                continue;

            int score = attack.priority * 1000;

            if (IsAttackReady(attack))
                score += 200;

            if (attack.IsInRange(distanceToPlayer))
                score += 100;

            score -= Mathf.RoundToInt(Mathf.Abs(attack.PreferredRange - distanceToPlayer) * 100f);

            if (score > bestScore)
            {
                bestScore = score;
                bestAttack = attack;
            }
        }

        return bestAttack;
    }

    void StartAttack(BossAttackDefinition attack)
    {
        if (attack == null)
            return;

        currentAttack = attack;
        currentAttackElapsed = 0f;
        SetAttackRuntime(attack.attackIndex, BossAttackPhase.Startup);
        currentAttackWindowIndex = 0;
        attackHitConfirmedThisWindow = false;
        rangedAttackResultPending = false;
        currentState = BossState.Attacking;

        SetAttackCooldown(attack);
        StopMove();
        if (staggerSystem != null)
            staggerSystem.ClearPendingHitReaction();

        SetWeaponTrailForAttack(attack);
        SetWeaponTrailActive(false);

        if (meleeDamageWindow != null)
            meleeDamageWindow.ForceCloseWindow();

        if (animatorController != null)
        {
            animatorController.SetTarget(CurrentTarget);
            animatorController.RequestAttack(attack);
            animatorController.SyncAttackPhase(BossAttackPhase.Startup);
        }
    }

    void UpdateAttackState()
    {
        if (animatorController == null)
            return;

        if (currentAttack == null)
        {
            FinishAttack();
            return;
        }

        currentAttackElapsed += Time.deltaTime;
        BossAttackPhase evaluatedPhase = currentAttack.EvaluatePhase(currentAttackElapsed);
        if (evaluatedPhase != currentAttackPhase)
            SetAttackPhase(evaluatedPhase);

        StopMove();

        if (!animatorController.IsBusyWithAttackMotion && !animatorController.IsInAttackState())
            FinishAttack();
    }

    void SetAttackPhase(BossAttackPhase phase)
    {
        SetAttackRuntime(currentAttackIndex, phase);

        if (animatorController != null)
            animatorController.SyncAttackPhase(phase);
    }

    void SetAttackRuntime(int attackIndex, BossAttackPhase phase)
    {
        if (bossRuntime != null)
            bossRuntime.SetAttackRuntime(attackIndex, phase);
    }

    void ClearAttackRuntime()
    {
        if (bossRuntime != null)
            bossRuntime.ClearAttackRuntime();
    }

    void FinishAttack()
    {
        currentAttack = null;
        currentAttackElapsed = 0f;
        ClearAttackRuntime();
        currentAttackWindowIndex = 0;
        attackHitConfirmedThisWindow = false;
        rangedAttackResultPending = false;

        if (meleeDamageWindow != null)
            meleeDamageWindow.ForceCloseWindow();

        SetWeaponTrailActive(false);
        currentState = BossState.Idle;
    }

    void StopMove()
    {
        if (agent == null || !agent.enabled)
            return;

        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
    }

    public void ForceInterruptAction()
    {
        if (meleeDamageWindow != null)
            meleeDamageWindow.ForceCloseWindow();

        StopMove();

        if (animatorController != null)
            animatorController.AbortAttackMotion();

        if (staggerSystem != null)
            staggerSystem.ClearPendingHitReaction();

        SetWeaponTrailActive(false);
        currentAttack = null;
        currentAttackElapsed = 0f;
        ClearAttackRuntime();
        currentAttackWindowIndex = 0;
        attackHitConfirmedThisWindow = false;
        rangedAttackResultPending = false;

        if (currentState != BossState.Dead)
            currentState = BossState.Idle;
    }

    public bool CanInterruptWithHitReaction()
    {
        if (currentState != BossState.Attacking || currentAttack == null)
            return true;

        return currentAttack.IsInterruptible(currentAttackPhase);
    }

    void Die()
    {
        currentState = BossState.Dead;
        attackHitConfirmedThisWindow = false;
        rangedAttackResultPending = false;

        if (combatAudioController != null)
        {
            if (config != null && config.audio != null && HasAnyClip(config.audio.dieVoiceClips))
                combatAudioController.PlayDieVoice(config.audio.dieVoiceClips);
            else
                combatAudioController.PlayDieVoice();
        }

        if (meleeDamageWindow != null)
            meleeDamageWindow.ForceCloseWindow();

        SetWeaponTrailActive(false);
        StopMove();

        if (animatorController != null)
            animatorController.PlayDeath();

        if (staggerSystem != null)
            staggerSystem.ClearPendingHitReaction();

        enabled = false;
    }

    public void TakeDamage(float amount, GameObject attacker = null)
    {
        if (currentState == BossState.Dead)
            return;

        float safeAmount = Mathf.Max(0f, amount);
        if (safeAmount <= 0f)
            return;

        TrackRecentDamage(attacker, safeAmount);

        if (combatAudioController != null)
        {
            if (config != null && config.audio != null && HasAnyClip(config.audio.hurtVoiceClips))
                combatAudioController.PlayHurt(config.audio.hurtVoiceClips);
            else
                combatAudioController.PlayHurt();
        }

        if (bossRuntime != null)
            bossRuntime.TakeDamage(safeAmount);
        else
            legacyCurrentHP = Mathf.Max(0f, legacyCurrentHP - safeAmount);

        if (currentHP <= 0f)
            Die();
    }

    void TrackRecentDamage(GameObject attacker, float amount)
    {
        if (attacker == null)
            return;

        GuardRuntime guardSource = attacker.GetComponentInParent<GuardRuntime>();
        if (guardSource == null)
            guardSource = attacker.GetComponentInChildren<GuardRuntime>(true);

        if (guardSource != null && !guardSource.isDead)
        {
            if (!recentGuardDamageBySource.TryGetValue(guardSource, out List<DamageRecord> guardRecords))
            {
                guardRecords = new List<DamageRecord>();
                recentGuardDamageBySource.Add(guardSource, guardRecords);
            }

            AddDamageRecord(guardRecords, amount);
            TrySwitchTargetFromDamage(guardSource.transform, SumRecentDamage(guardRecords), guardSwitchDamageThreshold);
            return;
        }

        if (!IsPlayerDamageSource(attacker))
            return;

        AddDamageRecord(recentPlayerDamage, amount);
        TrySwitchTargetFromDamage(player, SumRecentDamage(recentPlayerDamage), playerSwitchDamageThreshold);
    }

    bool IsPlayerDamageSource(GameObject attacker)
    {
        if (attacker == null)
            return false;

        if (attacker.GetComponentInParent<PlayerController>() != null)
            return true;

        if (attacker.GetComponentInChildren<PlayerController>(true) != null)
            return true;

        if (attacker.GetComponentInParent<PlayerStatsRuntime>() != null)
            return true;

        if (attacker.GetComponentInChildren<PlayerStatsRuntime>(true) != null)
            return true;

        return attacker.CompareTag("Player") || attacker.transform.root.CompareTag("Player");
    }

    void AddDamageRecord(List<DamageRecord> records, float amount)
    {
        if (records == null)
            return;

        records.Add(new DamageRecord(Time.time, amount));
        TrimDamageRecords(records);
    }

    float SumRecentDamage(List<DamageRecord> records)
    {
        if (records == null)
            return 0f;

        TrimDamageRecords(records);

        float total = 0f;
        for (int i = 0; i < records.Count; i++)
            total += records[i].amount;

        return total;
    }

    void TrimDamageRecords(List<DamageRecord> records)
    {
        if (records == null)
            return;

        float oldestAllowedTime = Time.time - Mathf.Max(0.01f, damageWindowDuration);
        for (int i = records.Count - 1; i >= 0; i--)
        {
            if (records[i].time < oldestAllowedTime)
                records.RemoveAt(i);
        }
    }

    void TrySwitchTargetFromDamage(Transform target, float recentDamageTotal, float threshold)
    {
        if (target == null)
            return;

        if (CurrentTarget == target)
            return;

        if (recentDamageTotal <= threshold)
            return;

        if (Time.time < nextAllowedTargetSwitchTime)
            return;

        SetCurrentTarget(target);
        nextAllowedTargetSwitchTime = Time.time + Mathf.Max(0f, targetSwitchCooldown);
    }

    bool IsTargetDead(Transform target)
    {
        if (target == null)
            return true;

        GuardRuntime guard = target.GetComponentInParent<GuardRuntime>();
        if (guard != null)
            return guard.isDead;

        PlayerController playerController = target.GetComponentInParent<PlayerController>();
        if (playerController != null)
            return playerController.IsDead();

        return false;
    }

    public bool IsDead()
    {
        return currentState == BossState.Dead || currentHP <= 0f;
    }

    void SetWeaponTrailForAttack(BossAttackDefinition attack)
    {
        if (weaponTrailVFX == null || attack == null)
            return;

        weaponTrailVFX.SetTrailSet(GetTrailSetForAttack(attack.attackIndex), attack.slashTrails);
    }

    public BossAttackDefinition GetAttackDefinition(int attackIndex)
    {
        if (attacks == null)
            return null;

        for (int i = 0; i < attacks.Length; i++)
        {
            BossAttackDefinition attack = attacks[i];
            if (attack != null && attack.attackIndex == attackIndex)
                return attack;
        }

        return null;
    }

    BossMeleeWindowData GetAttackWindowData(int attackIndex, int windowIndex)
    {
        BossAttackDefinition attack = GetAttackDefinition(attackIndex);
        return attack != null ? attack.GetMeleeWindow(windowIndex) : null;
    }

    public float GetAttackDamage(int attackIndex, int windowIndex, float fallback)
    {
        BossMeleeWindowData windowData = GetAttackWindowData(attackIndex, windowIndex);
        if (windowData != null)
            return Mathf.Max(0f, windowData.damage);

        BossAttackDefinition attack = GetAttackDefinition(attackIndex);
        if (attack != null)
            return Mathf.Max(0f, attack.damage);

        return Mathf.Max(0f, fallback);
    }

    public float GetAttackRadius(int attackIndex, int windowIndex, float fallback)
    {
        BossMeleeWindowData windowData = GetAttackWindowData(attackIndex, windowIndex);
        if (windowData != null)
            return Mathf.Max(0f, windowData.radius);

        return Mathf.Max(0f, fallback);
    }

    public float GetAttackFacingAngle(int attackIndex, int windowIndex, float fallback)
    {
        BossMeleeWindowData windowData = GetAttackWindowData(attackIndex, windowIndex);
        if (windowData != null)
            return Mathf.Clamp(windowData.facingAngle, 0f, 180f);

        return Mathf.Clamp(fallback, 0f, 180f);
    }

    public float GetProjectileDamage(int attackIndex, float fallback)
    {
        BossAttackDefinition attack = GetAttackDefinition(attackIndex);
        if (attack != null)
            return Mathf.Max(0f, attack.damage);

        return Mathf.Max(0f, fallback);
    }

    public float GetProjectileSpeed(int attackIndex, float fallback)
    {
        BossAttackDefinition attack = GetAttackDefinition(attackIndex);
        if (attack != null && attack.projectileSpeed > 0f)
            return attack.projectileSpeed;

        return Mathf.Max(0f, fallback);
    }

    public float GetProjectileLifeTime(int attackIndex, float fallback)
    {
        BossAttackDefinition attack = GetAttackDefinition(attackIndex);
        if (attack != null && attack.projectileLifeTime > 0f)
            return attack.projectileLifeTime;

        return Mathf.Max(0.01f, fallback);
    }

    public GameObject GetAttackHitEffectPrefab(int attackIndex)
    {
        BossAttackDefinition attack = GetAttackDefinition(attackIndex);
        return attack != null ? attack.hitEffectPrefab : null;
    }

    public float GetAttackHitEffectLifetime(int attackIndex, float fallback)
    {
        BossAttackDefinition attack = GetAttackDefinition(attackIndex);
        if (attack != null && attack.hitEffectLifetime > 0f)
            return attack.hitEffectLifetime;

        return Mathf.Max(0.01f, fallback);
    }

    public float GetAttackHitEffectNormalOffset(int attackIndex, float fallback)
    {
        BossAttackDefinition attack = GetAttackDefinition(attackIndex);
        if (attack != null)
            return Mathf.Max(0f, attack.hitEffectNormalOffset);

        return Mathf.Max(0f, fallback);
    }

    public void PlayCurrentAttackHitVFX(Vector3 position, Vector3 normal)
    {
        if (currentAttack == null || currentAttack.hitEffectPrefab == null)
            return;

        SpawnHitEffect(
            currentAttack.hitEffectPrefab,
            position,
            normal,
            currentAttack.hitEffectLifetime,
            currentAttack.hitEffectNormalOffset
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

    void SetWeaponTrailActive(bool active)
    {
        if (weaponTrailVFX == null)
            return;

        if (active)
            weaponTrailVFX.TrailOn();
        else
            weaponTrailVFX.TrailOff();
    }

    BossWeaponTrailController.TrailSet GetTrailSetForAttack(int attackIndex)
    {
        switch (attackIndex)
        {
            case 3:
                return BossWeaponTrailController.TrailSet.MeleeSkill1;
            case 4:
                return BossWeaponTrailController.TrailSet.MeleeSkill2;
            case 5:
                return BossWeaponTrailController.TrailSet.MeleeSkill3;
            case 6:
                return BossWeaponTrailController.TrailSet.Ranged;
            default:
                return BossWeaponTrailController.TrailSet.Normal;
        }
    }

    public void AnimEvent_OpenMeleeWindow()
    {
        AnimEvent_OpenMeleeWindowSection(0);
    }

    public void AnimEvent_OpenMeleeWindowSection(int windowIndex)
    {
        if (staggerSystem != null && staggerSystem.ShouldLockBossAction())
            return;

        if (meleeDamageWindow == null || currentAttack == null || !currentAttack.opensMeleeWindow)
            return;

        currentAttackWindowIndex = Mathf.Max(0, windowIndex);
        attackHitConfirmedThisWindow = false;
        meleeDamageWindow.OpenWindow(currentAttackIndex, currentAttackWindowIndex);
        SetAttackPhase(BossAttackPhase.Active);
    }

    public void AnimEvent_CloseMeleeWindow()
    {
        if (meleeDamageWindow == null)
            return;

        if (!attackHitConfirmedThisWindow && combatAudioController != null)
        {
            if (currentAttack != null && HasAnyClip(currentAttack.attackMissClips))
                combatAudioController.PlayAttackMiss(currentAttack.attackMissClips);
            else
                combatAudioController.PlayAttackMiss();
        }

        meleeDamageWindow.CloseWindow();
        currentAttackWindowIndex = 0;
        attackHitConfirmedThisWindow = false;

        if (currentState == BossState.Attacking)
            SetAttackPhase(BossAttackPhase.Recovery);
    }

    public void AnimEvent_FireProjectile()
    {
        if (staggerSystem != null && staggerSystem.ShouldLockBossAction())
            return;

        if (rangedSkillCaster == null || currentAttack == null || !currentAttack.firesProjectile)
            return;

        SetAttackPhase(BossAttackPhase.Active);

        Vector3 attackDirection = animatorController != null
            ? animatorController.GetCurrentAttackDirection()
            : transform.forward;

        attackHitConfirmedThisWindow = false;
        rangedAttackResultPending = rangedSkillCaster.Fire(currentAttackIndex, attackDirection);
    }

    public void AnimEvent_LockAttackDirection()
    {
        if (animatorController == null)
            return;

        animatorController.LockCurrentAttackDirection();
    }

    public void AnimEvent_PlayAttackSFX()
    {
        if (combatAudioController == null)
            return;

        if (currentAttack != null && (HasAnyClip(currentAttack.attackVoiceClips) || HasAnyClip(currentAttack.attackSwingClips)))
            combatAudioController.PlayAttackStart(currentAttack.attackVoiceClips, currentAttack.attackSwingClips);
        else
            combatAudioController.PlayAttackStart();
    }

    public void NotifyAttackHitConfirmed()
    {
        if (currentState == BossState.Dead)
            return;

        if (attackHitConfirmedThisWindow)
            return;

        attackHitConfirmedThisWindow = true;
        rangedAttackResultPending = false;

        if (combatAudioController != null)
        {
            if (currentAttack != null && HasAnyClip(currentAttack.attackHitClips))
                combatAudioController.PlayAttackHit(currentAttack.attackHitClips);
            else
                combatAudioController.PlayAttackHit();
        }
    }

    public void NotifyRangedAttackMiss()
    {
        if (currentState == BossState.Dead)
            return;

        if (!rangedAttackResultPending)
            return;

        rangedAttackResultPending = false;

        if (!attackHitConfirmedThisWindow && combatAudioController != null)
        {
            if (currentAttack != null && HasAnyClip(currentAttack.attackMissClips))
                combatAudioController.PlayAttackMiss(currentAttack.attackMissClips);
            else
                combatAudioController.PlayAttackMiss();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, engageDistance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, approachStopDistance);
    }

}
