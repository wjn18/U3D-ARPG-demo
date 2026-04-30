using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(BossAnimatorController))]
[RequireComponent(typeof(BossRuntime))]
public class BOSSAI : MonoBehaviour, IDamageable, ICombatPrioritySource
{
    const float StartupMoveStopDistance = 0.5f;
    const float AvoidTriggerCooldown = 10f;

    public enum BossState
    {
        Idle,
        Approach,
        Retreat,
        Attacking,
        Dead,
        Stagger
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

    [Header("Detection")]
    [HideInInspector]
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

    [Header("Debug")]
    public bool drawGizmos = true;

    public float maxHP => bossRuntime != null ? bossRuntime.maxHP : 0f;
    public float currentHP => bossRuntime != null ? bossRuntime.currentHP : 0f;
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
    private GameObject activeWarningArea;
    private bool avoidTriggeredWhileAllAttacksCoolingDown;
    private float nextAvoidAllowedTime;
    private bool wasInAvoidState;
    private bool avoidMoveActive;
    private float avoidMoveEndTime;
    private float avoidMoveRemainingDistance;
    private bool startupMoveActive;
    private float startupMoveEndTime;
    private float startupMoveRemainingDistance;
    private bool parryWindowOpen;
    private float staggerStateMinEndTime;

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

            bossRuntime.Initialize(config, player);
        }

        ApplyConfig();

        agent.updateRotation = false;

        ApplyAttackPayloadDefaults();
        BuildCooldownTable();
        HideAllConfiguredWarningAreas();
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

        return BossConfig.CreateDefaultAttackSet();
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

            if (attack.maxRange <= 0f)
                attack.maxRange = defaultAttack.maxRange;

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

        UpdateAvoidState();
        if (animatorController != null && animatorController.IsInAvoidState())
        {
            StopMove();
            return;
        }

        if (currentState == BossState.Stagger)
        {
            UpdateStaggerState();
            return;
        }

        if (currentState == BossState.Attacking)
        {
            avoidTriggeredWhileAllAttacksCoolingDown = false;
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
            avoidTriggeredWhileAllAttacksCoolingDown = false;
            StartAttack(attack);
            return;
        }

        if (AreAllAttacksCoolingDown())
        {
            TriggerAvoidIfNeeded();
            return;
        }

        avoidTriggeredWhileAllAttacksCoolingDown = false;

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
        int score = attack.attackPriority * 1000;
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

    bool AreAllAttacksCoolingDown()
    {
        if (attacks == null || attacks.Length == 0)
            return false;

        bool hasValidAttack = false;

        for (int i = 0; i < attacks.Length; i++)
        {
            BossAttackDefinition attack = attacks[i];
            if (attack == null)
                continue;

            hasValidAttack = true;

            if (IsAttackReady(attack))
                return false;
        }

        return hasValidAttack;
    }

    void TriggerAvoidIfNeeded()
    {
        if (avoidTriggeredWhileAllAttacksCoolingDown)
            return;

        if (Time.time < nextAvoidAllowedTime)
            return;

        avoidTriggeredWhileAllAttacksCoolingDown = true;
        StopMove();

        if (animatorController != null && animatorController.PlayAvoid())
            nextAvoidAllowedTime = Time.time + AvoidTriggerCooldown;
    }

    void UpdateAvoidState()
    {
        bool isInAvoidState = animatorController != null && animatorController.IsInAvoidState();

        if (isInAvoidState && !wasInAvoidState)
            BeginAvoidState();
        else if (!isInAvoidState && wasInAvoidState)
            EndAvoidState();

        wasInAvoidState = isInAvoidState;

        if (isInAvoidState)
            UpdateAvoidMove();
    }

    void BeginAvoidState()
    {
        BossAvoidConfig avoidConfig = config != null ? config.avoid : null;
        if (avoidConfig == null)
            return;

        float moveDistance = Mathf.Max(0f, avoidConfig.moveDistance);
        float avoidDuration = 0.01f;

        if (animatorController != null && animatorController.Animator != null)
        {
            Animator animator = animatorController.Animator;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
                if (nextState.IsName(animatorController.avoidStateName))
                    state = nextState;
            }

            avoidDuration = Mathf.Max(0.01f, state.length);
        }

        avoidMoveRemainingDistance = moveDistance;
        avoidMoveEndTime = Time.time + avoidDuration;
        avoidMoveActive = moveDistance > 0.001f;

        if (combatAudioController != null && HasAnyClip(avoidConfig.sfxClips))
            combatAudioController.PlayOneShot(avoidConfig.sfxClips);

        PlayAvoidVFX(avoidConfig);
    }

    void EndAvoidState()
    {
        StopAvoidMove();
    }

    void StopAvoidMove()
    {
        avoidMoveActive = false;
        avoidMoveEndTime = 0f;
        avoidMoveRemainingDistance = 0f;

        if (agent != null && agent.enabled)
            agent.nextPosition = transform.position;
    }

    void UpdateAvoidMove()
    {
        if (!avoidMoveActive)
            return;

        float remainingTime = avoidMoveEndTime - Time.time;
        if (remainingTime <= 0.001f || avoidMoveRemainingDistance <= 0.001f)
        {
            StopAvoidMove();
            return;
        }

        Vector3 backward = -transform.forward;
        backward.y = 0f;
        if (backward.sqrMagnitude <= 0.0001f)
        {
            StopAvoidMove();
            return;
        }

        backward.Normalize();

        float speed = avoidMoveRemainingDistance / remainingTime;
        float frameMoveDistance = Mathf.Min(avoidMoveRemainingDistance, speed * Time.deltaTime);
        if (frameMoveDistance <= 0.0001f)
            return;

        transform.position += backward * frameMoveDistance;
        avoidMoveRemainingDistance = Mathf.Max(0f, avoidMoveRemainingDistance - frameMoveDistance);

        if (agent != null && agent.enabled)
            agent.nextPosition = transform.position;
    }

    void PlayAvoidVFX(BossAvoidConfig avoidConfig)
    {
        if (avoidConfig == null || avoidConfig.vfxPrefab == null)
            return;

        Transform socket = FindBossChildTransform(avoidConfig.vfxSocketId);
        if (socket == null && !string.IsNullOrWhiteSpace(avoidConfig.vfxSocketId))
            Debug.LogWarning($"Boss avoid VFX socket '{avoidConfig.vfxSocketId}' was not found.", this);

        Vector3 position = socket != null ? socket.position : transform.position;
        Quaternion rotation = socket != null ? socket.rotation : transform.rotation;

        GameObject instance = Instantiate(avoidConfig.vfxPrefab, position, rotation);
        Destroy(instance, Mathf.Max(0.01f, avoidConfig.vfxLifetime));
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

            int score = attack.attackPriority * 1000;

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

        HideActiveWarningArea();
        currentAttack = attack;
        currentAttackElapsed = 0f;
        SetAttackRuntime(attack.attackIndex, BossAttackPhase.Startup);
        currentAttackWindowIndex = 0;
        attackHitConfirmedThisWindow = false;
        rangedAttackResultPending = false;
        parryWindowOpen = false;
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

        UpdateStartupMove();
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
        StopStartupMoveToTarget();
        StopAvoidMove();
        HideActiveWarningArea();
        currentAttack = null;
        currentAttackElapsed = 0f;
        ClearAttackRuntime();
        currentAttackWindowIndex = 0;
        attackHitConfirmedThisWindow = false;
        rangedAttackResultPending = false;
        parryWindowOpen = false;

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

        StopStartupMoveToTarget();
        StopAvoidMove();
        HideActiveWarningArea();
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
        parryWindowOpen = false;

        if (currentState != BossState.Dead)
            currentState = BossState.Idle;
    }

    public bool CanInterruptWithHitReaction()
    {
        if (currentState != BossState.Attacking || currentAttack == null)
            return true;

        return currentAttack.IsInterruptible(currentAttackPhase);
    }

    public void EnterStaggerState(float minimumDuration = 0.2f)
    {
        if (currentState == BossState.Dead)
            return;

        StopMove();
        currentState = BossState.Stagger;
        staggerStateMinEndTime = Time.time + Mathf.Max(0f, minimumDuration);
    }

    void UpdateStaggerState()
    {
        StopMove();

        if (Time.time < staggerStateMinEndTime)
            return;

        if (animatorController != null)
        {
            if (animatorController.IsInStaggerReactionState())
                return;

            if (animatorController.Animator != null && animatorController.Animator.IsInTransition(0))
                return;
        }

        currentState = BossState.Idle;
    }

    public void AnimEvent_EndStagger()
    {
        if (currentState == BossState.Stagger)
            currentState = BossState.Idle;
    }

    void Die()
    {
        currentState = BossState.Dead;
        attackHitConfirmedThisWindow = false;
        rangedAttackResultPending = false;
        parryWindowOpen = false;
        StopStartupMoveToTarget();
        StopAvoidMove();
        HideActiveWarningArea();

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

        if (TryReceiveParry(attacker))
            return;

        float safeAmount = Mathf.Max(0f, amount);
        if (safeAmount <= 0f)
            return;

        TrackRecentDamage(attacker, safeAmount);

        if (staggerSystem != null)
            staggerSystem.TakeStaggerDamage(safeAmount, ResolveHitType(attacker), attacker);

        if (combatAudioController != null)
        {
            if (config != null && config.audio != null && HasAnyClip(config.audio.hurtVoiceClips))
                combatAudioController.PlayHurt(config.audio.hurtVoiceClips);
            else
                combatAudioController.PlayHurt();
        }

        if (bossRuntime == null)
            return;

        bossRuntime.TakeDamage(safeAmount);

        if (currentHP <= 0f)
            Die();
    }

    public bool ReceiveParry(GameObject attacker = null)
    {
        return TryReceiveParry(attacker);
    }

    bool TryReceiveParry(GameObject attacker)
    {
        if (!parryWindowOpen)
            return false;

        if (!IsPlayerDamageSource(attacker))
            return false;

        parryWindowOpen = false;

        if (staggerSystem == null)
            return false;

        return staggerSystem.ReceiveParry(attacker);
    }

    BossStaggerSystem.PlayerHitType ResolveHitType(GameObject attacker)
    {
        if (attacker != null)
        {
            MonoBehaviour[] components = attacker.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is ICombatPrioritySource prioritySource)
                {
                    int incomingPriority = Mathf.Max(1, prioritySource.GetCombatPriority());
                    return incomingPriority >= 2
                        ? BossStaggerSystem.PlayerHitType.Heavy
                        : BossStaggerSystem.PlayerHitType.Normal;
                }
            }
        }

        return BossStaggerSystem.PlayerHitType.Normal;
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

        if (attacker.GetComponentInParent<PlayerCombatController>() != null)
            return true;

        if (attacker.GetComponentInChildren<PlayerCombatController>(true) != null)
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

        PlayerCombatController combatController = target.GetComponentInParent<PlayerCombatController>();
        if (combatController != null && combatController.enabled)
            return combatController.IsDead;

        PlayerStatsRuntime playerStats = target.GetComponentInParent<PlayerStatsRuntime>();
        if (playerStats != null)
            return playerStats.IsDeadState();

        return false;
    }

    public bool IsDead()
    {
        return currentState == BossState.Dead || currentHP <= 0f;
    }

    public int GetCombatPriority()
    {
        if (currentState == BossState.Attacking && currentAttack != null)
            return Mathf.Max(1, currentAttack.priority);

        return 1;
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

    public BossMeleeWindowData GetAttackWindowData(int attackIndex, int windowIndex)
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

    GameObject GetAttackWarningArea(int attackIndex)
    {
        BossAttackDefinition attack = GetAttackDefinition(attackIndex);
        if (attack == null || string.IsNullOrWhiteSpace(attack.warningAreaId))
            return null;

        return FindBossChildObject(attack.warningAreaId);
    }

    GameObject FindBossChildObject(string objectId)
    {
        Transform childTransform = FindBossChildTransform(objectId);
        return childTransform != null ? childTransform.gameObject : null;
    }

    Transform FindBossChildTransform(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId))
            return null;

        Transform directMatch = transform.Find(objectId);
        if (directMatch != null)
            return directMatch;

        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allChildren.Length; i++)
        {
            Transform child = allChildren[i];
            if (child != null && child.name == objectId)
                return child;
        }

        return null;
    }

    void HideAllConfiguredWarningAreas()
    {
        if (attacks == null)
            return;

        HashSet<GameObject> uniqueAreas = new HashSet<GameObject>();
        for (int i = 0; i < attacks.Length; i++)
        {
            BossAttackDefinition attack = attacks[i];
            if (attack == null || string.IsNullOrWhiteSpace(attack.warningAreaId))
                continue;

            GameObject warningArea = FindBossChildObject(attack.warningAreaId);
            if (warningArea == null || !uniqueAreas.Add(warningArea))
                continue;

            warningArea.SetActive(false);
        }

        activeWarningArea = null;
    }

    void HideActiveWarningArea()
    {
        if (activeWarningArea != null)
            activeWarningArea.SetActive(false);

        activeWarningArea = null;
    }

    void StartStartupMoveToTarget()
    {
        StopStartupMoveToTarget();

        if (currentAttack == null)
            return;

        if (CurrentTarget == null)
            return;

        float maxMoveDistance = Mathf.Max(0f, currentAttack.startupMoveMaxDistance);
        if (maxMoveDistance <= 0f)
        {
            Debug.LogWarning($"Boss startup move max distance is not configured for attack {currentAttack.attackIndex}.", this);
            return;
        }

        float remainingStartupTime = Mathf.Max(0f, currentAttack.startupDuration - currentAttackElapsed);
        if (remainingStartupTime <= 0.01f)
            return;

        float currentDistance = DistanceToCurrentTarget();
        float requiredMoveDistance = Mathf.Max(0f, currentDistance - StartupMoveStopDistance);
        startupMoveRemainingDistance = Mathf.Min(requiredMoveDistance, maxMoveDistance);

        if (startupMoveRemainingDistance <= 0.01f)
            return;

        startupMoveEndTime = Time.time + remainingStartupTime;
        startupMoveActive = true;
    }

    void StopStartupMoveToTarget()
    {
        startupMoveActive = false;
        startupMoveEndTime = 0f;
        startupMoveRemainingDistance = 0f;

        if (agent != null && agent.enabled)
            agent.nextPosition = transform.position;
    }

    void UpdateStartupMove()
    {
        if (!startupMoveActive || currentAttack == null)
            return;

        if (currentAttackPhase != BossAttackPhase.Startup)
        {
            StopStartupMoveToTarget();
            return;
        }

        float remainingTime = startupMoveEndTime - Time.time;
        if (remainingTime <= 0.001f)
        {
            StopStartupMoveToTarget();
            return;
        }

        Transform target = CurrentTarget;
        if (target == null)
        {
            StopStartupMoveToTarget();
            return;
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            StopStartupMoveToTarget();
            return;
        }

        float neededDistance = Mathf.Max(0f, toTarget.magnitude - StartupMoveStopDistance);
        if (neededDistance <= 0.01f || startupMoveRemainingDistance <= 0.01f)
        {
            StopStartupMoveToTarget();
            return;
        }

        float plannedSpeed = startupMoveRemainingDistance / remainingTime;
        float frameMoveDistance = Mathf.Min(
            startupMoveRemainingDistance,
            neededDistance,
            plannedSpeed * Time.deltaTime
        );

        if (frameMoveDistance <= 0.0001f)
            return;

        Vector3 moveDirection = toTarget.normalized;
        transform.position += moveDirection * frameMoveDistance;
        startupMoveRemainingDistance = Mathf.Max(0f, startupMoveRemainingDistance - frameMoveDistance);

        float rotateSpeed = currentAttack.turnSpeed > 0f ? currentAttack.turnSpeed : 360f;
        Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);

        if (agent != null && agent.enabled)
            agent.nextPosition = transform.position;
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
                return BossWeaponTrailController.TrailSet.MeleeSkill4;
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

    public void AnimEvent_OpenParryWindow()
    {
        if (staggerSystem != null && staggerSystem.ShouldLockBossAction())
            return;

        if (currentState != BossState.Attacking)
            return;

        parryWindowOpen = true;
    }

    public void AnimEvent_CloseParryWindow()
    {
        parryWindowOpen = false;
    }

    public void AnimEvent_ParryWindowOn()
    {
        AnimEvent_OpenParryWindow();
    }

    public void AnimEvent_ParryWindowOff()
    {
        AnimEvent_CloseParryWindow();
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

    public void AnimEvent_StartMoveToTarget()
    {
        if (staggerSystem != null && staggerSystem.ShouldLockBossAction())
            return;

        if (currentState != BossState.Attacking)
            return;

        StartStartupMoveToTarget();
    }

    public void AnimEvent_StopMoveToTarget()
    {
        StopStartupMoveToTarget();
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

    public void AnimEvent_ShowWarningArea()
    {
        if (currentAttack == null)
            return;

        GameObject warningArea = GetAttackWarningArea(currentAttack.attackIndex);
        if (warningArea == null)
        {
            Debug.LogWarning($"Boss warning area missing for attack {currentAttack.attackIndex}.", this);
            return;
        }

        if (activeWarningArea != null && activeWarningArea != warningArea)
            activeWarningArea.SetActive(false);

        activeWarningArea = warningArea;
        activeWarningArea.SetActive(true);
    }

    public void AnimEvent_HideWarningArea()
    {
        HideActiveWarningArea();
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
