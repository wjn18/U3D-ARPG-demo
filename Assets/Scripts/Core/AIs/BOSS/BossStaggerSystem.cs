using UnityEngine;

public class BossStaggerSystem : MonoBehaviour
{
    public enum PlayerHitType
    {
        Normal,
        Sprint,
        Heavy
    }

    enum KneelSequencePhase
    {
        None,
        WaitingKneelState,
        WaitingKneelIdleState,
        HoldingKneelIdle,
        WaitingStandState,
        PlayingStand
    }

    [Header("Refs")]
    public BOSSAI bossAI;
    public BossAnimatorController animatorController;
    public BossRuntime bossRuntime;
    public BossConfig config;

    [Header("Animator Params")]
    public string hitTriggerParam = "HitTrigger";
    public string hitSmallTriggerParam = "HitSmallTrigger";
    public string hitBigTriggerParam = "HitBigTrigger";
    public string kneelTriggerParam = "KneelTrigger";
    public string parryTriggerParam = "ParryTrigger";
    public string locomotionStateName = "Locomotion";
    public float hitReactionInterruptBlendDuration = 0.05f;

    [Header("RV")]
    [Range(0f, 1f)]
    public float alwaysOpenThresholdPercent = 0.3f;

    [Header("Recovery")]
    public float recoverDelay = 3f;
    public float recoverPerSecond = 10f;

    [Header("Stagger Window Cycle")]
    public float initialStaggerWindowDuration = 2f;
    public float superArmorDuration = 6f;
    public float rvReductionAfterParried = 50f;

    [Header("Kneel Idle Hold")]
    public float kneelIdleHoldDuration = 2f;

    [Header("Stand Bool Release")]
    public float standBoolReleaseDelay = 1f;

    [Header("Execution")]
    public float executeDistance = 4f;
    public float executeDamage = 80f;

    public float maxRV
    {
        get => bossRuntime != null ? bossRuntime.maxRV : 0f;
        private set
        {
            if (bossRuntime != null)
                bossRuntime.maxRV = value;
        }
    }

    public float currentRV
    {
        get => bossRuntime != null ? bossRuntime.currentRV : 0f;
        private set
        {
            if (bossRuntime != null)
                bossRuntime.SetRV(value);
        }
    }

    float lastHitTime = -999f;

    bool staggerWindowOpen = true;
    float staggerCycleTimer = 0f;

    KneelSequencePhase kneelPhase = KneelSequencePhase.None;
    float kneelIdleHoldTimer = 0f;

    bool waitingToReleaseKneelBool = false;
    float releaseKneelBoolTimer = 0f;

    void Awake()
    {
        if (bossAI == null)
            bossAI = GetComponent<BOSSAI>();

        if (animatorController == null)
            animatorController = GetComponent<BossAnimatorController>();

        if (bossRuntime == null)
            bossRuntime = GetComponent<BossRuntime>();
        if (bossRuntime == null)
            bossRuntime = gameObject.AddComponent<BossRuntime>();

        if (config == null)
        {
            if (bossRuntime != null && bossRuntime.config != null)
                config = bossRuntime.config;
            else if (bossAI != null)
                config = bossAI.config;
        }

        ApplyConfig();

        currentRV = Mathf.Clamp(currentRV > 0f ? currentRV : maxRV, 0f, maxRV);
        staggerWindowOpen = true;
        staggerCycleTimer = 0f;
    }

    void Start()
    {
        if (config == null && bossAI != null)
            config = bossAI.config;

        ApplyConfig();
        currentRV = Mathf.Clamp(currentRV, 0f, maxRV);
    }

    void ApplyConfig()
    {
        if (config == null)
            return;

        alwaysOpenThresholdPercent = config.alwaysOpenThresholdPercent;
        recoverDelay = config.recoverDelay;
        recoverPerSecond = config.recoverPerSecond;
        initialStaggerWindowDuration = config.initialStaggerWindowDuration;
        superArmorDuration = config.superArmorDuration;
        rvReductionAfterParried = config.rvReductionAfterParried;
        kneelIdleHoldDuration = config.kneelIdleHoldDuration;
        standBoolReleaseDelay = config.standBoolReleaseDelay;
        executeDistance = config.executeDistance;
        executeDamage = config.executeDamage;
    }

    void Update()
    {
        UpdateRecovery();
        UpdateStaggerWindowCycle();
        UpdateKneelSequence();
        UpdateKneelBoolReleaseTimer();
    }

    void UpdateRecovery()
    {
        if (IsInKneelSequence())
            return;

        if (currentRV >= maxRV)
            return;

        if (Time.time - lastHitTime < recoverDelay)
            return;

        currentRV += recoverPerSecond * Time.deltaTime;
        currentRV = Mathf.Clamp(currentRV, 0f, maxRV);
    }

    void UpdateStaggerWindowCycle()
    {
        if (IsInKneelSequence())
            return;

        if (currentRV <= maxRV * alwaysOpenThresholdPercent)
        {
            staggerWindowOpen = true;
            staggerCycleTimer = 0f;
            return;
        }

        staggerCycleTimer += Time.deltaTime;

        if (staggerWindowOpen)
        {
            if (staggerCycleTimer >= initialStaggerWindowDuration)
            {
                staggerWindowOpen = false;
                staggerCycleTimer = 0f;
            }
        }
        else
        {
            if (staggerCycleTimer >= superArmorDuration)
            {
                staggerWindowOpen = true;
                staggerCycleTimer = 0f;
            }
        }
    }

    void UpdateKneelSequence()
    {
        if (animatorController == null || animatorController.Animator == null)
            return;

        switch (kneelPhase)
        {
            case KneelSequencePhase.None:
                return;

            case KneelSequencePhase.WaitingKneelState:
                if (animatorController.IsInKneelState())
                    kneelPhase = KneelSequencePhase.WaitingKneelIdleState;
                return;

            case KneelSequencePhase.WaitingKneelIdleState:
                if (animatorController.IsInKneelIdleState())
                {
                    kneelPhase = KneelSequencePhase.HoldingKneelIdle;
                    kneelIdleHoldTimer = 0f;
                }
                return;

            case KneelSequencePhase.HoldingKneelIdle:
                kneelIdleHoldTimer += Time.deltaTime;
                if (kneelIdleHoldTimer >= kneelIdleHoldDuration)
                {
                    TriggerStand();
                }
                return;

            case KneelSequencePhase.WaitingStandState:
                if (animatorController.IsInStandState())
                {
                    kneelPhase = KneelSequencePhase.PlayingStand;
                    waitingToReleaseKneelBool = true;
                    releaseKneelBoolTimer = 0f;
                }
                return;

            case KneelSequencePhase.PlayingStand:
                if (!animatorController.IsInStandState())
                {
                    FinishKneelSequence();
                }
                return;
        }
    }

    void UpdateKneelBoolReleaseTimer()
    {
        if (!waitingToReleaseKneelBool)
            return;

        releaseKneelBoolTimer += Time.deltaTime;
        if (releaseKneelBoolTimer >= standBoolReleaseDelay)
        {
            waitingToReleaseKneelBool = false;
            releaseKneelBoolTimer = 0f;
            
        }
    }

    public void TakeStaggerDamage(float amount)
    {
        TakeStaggerDamage(amount, PlayerHitType.Normal, null);
    }

    public void TakeStaggerDamage(float amount, PlayerHitType hitType)
    {
        TakeStaggerDamage(amount, hitType, null);
    }

    public void TakeStaggerDamage(float amount, PlayerHitType hitType, GameObject attacker)
    {
        if (bossAI == null || animatorController == null || animatorController.Animator == null)
            return;

        if (bossAI.currentState == BOSSAI.BossState.Dead)
            return;

        if (ShouldIgnoreHitReactionCompletely())
        {
            ClearPendingHitReaction();
            return;
        }

        lastHitTime = Time.time;
        currentRV = Mathf.Clamp(currentRV - Mathf.Max(0f, amount), 0f, maxRV);

        if (currentRV <= 0f)
        {
            EnterKneel();
            return;
        }

        string hitReactionTrigger = ResolveHitReactionTrigger(hitType, attacker);
        if (string.IsNullOrWhiteSpace(hitReactionTrigger))
        {
            ClearPendingHitReaction();
            return;
        }

        if (ShouldInterruptAttackForHitReaction(hitType, attacker))
            InterruptAttackForHitReaction();

        ClearPendingHitReaction();
        if (bossAI != null)
            bossAI.EnterStaggerState();

        SetTriggerIfPresent(hitReactionTrigger);
    }

    public bool ReceiveParry(GameObject attacker)
    {
        if (bossAI == null || animatorController == null || animatorController.Animator == null)
            return false;

        if (bossAI.currentState == BOSSAI.BossState.Dead)
            return false;

        if (ShouldIgnoreHitReactionCompletely())
            return false;

        lastHitTime = Time.time;
        currentRV = Mathf.Clamp(currentRV - Mathf.Max(0f, rvReductionAfterParried), 0f, maxRV);

        if (currentRV <= 0f)
        {
            EnterKneel();
            return true;
        }

        InterruptAttackForParryReaction();
        ClearPendingHitReaction();

        if (bossAI != null)
            bossAI.EnterStaggerState();

        SetTriggerIfPresent(parryTriggerParam);
        return true;
    }

    string ResolveHitReactionTrigger(PlayerHitType hitType, GameObject attacker)
    {
        if (bossAI == null || animatorController == null)
            return null;

        if (ShouldIgnoreHitReactionCompletely())
            return null;

        if (currentRV <= 30f)
            return hitTriggerParam;

        if (!IsInAttackState())
            return hitTriggerParam;

        int delta = ResolveIncomingPriority(attacker, hitType) - GetCurrentCombatPriority();
        if (delta <= 0)
            return null;

        return delta >= 2 ? hitBigTriggerParam : hitSmallTriggerParam;
    }

    void EnterKneel()
    {
        if (animatorController == null || animatorController.Animator == null)
            return;

        kneelPhase = KneelSequencePhase.WaitingKneelState;
        kneelIdleHoldTimer = 0f;
        if (bossRuntime != null)
            bossRuntime.isKneeling = true;

        waitingToReleaseKneelBool = false;
        releaseKneelBoolTimer = 0f;


        if (bossAI != null)
        {
            bossAI.ForceInterruptAction();
        }

        ClearPendingHitReaction();
        animatorController.Animator.SetTrigger(kneelTriggerParam);
    }

    void TriggerStand()
    {
        if (animatorController == null || animatorController.Animator == null)
            return;

        kneelPhase = KneelSequencePhase.WaitingStandState;
    }

    void FinishKneelSequence()
    {
        kneelPhase = KneelSequencePhase.None;
        kneelIdleHoldTimer = 0f;
        if (bossRuntime != null)
            bossRuntime.isKneeling = false;

        waitingToReleaseKneelBool = false;
        releaseKneelBoolTimer = 0f;

        ClearPendingHitReaction();
        currentRV = maxRV;
        lastHitTime = Time.time;
    }

    public bool TryExecute(Transform executor)
    {
        if (!IsInKneelSequence() || bossAI == null || animatorController == null || animatorController.Animator == null)
            return false;

        if (executor == null)
            return false;

        float dist = Vector3.Distance(executor.position, transform.position);
        if (dist > executeDistance)
            return false;

        bossAI.TakeDamage(executeDamage, executor.gameObject);

        animatorController.Animator.ResetTrigger(kneelTriggerParam);
        ClearPendingHitReaction();

        FinishKneelSequence();

        return true;
    }

    public bool ShouldLockBossAction()
    {
        if (bossAI != null && bossAI.currentState == BOSSAI.BossState.Dead)
            return true;

        if (IsInKneelSequence())
            return true;

        if (animatorController != null && animatorController.IsInKneelLikeState())
            return true;

        return false;
    }

    public bool IsKneelingOrStanding()
    {
        if (IsInKneelSequence())
            return true;

        if (animatorController != null && animatorController.IsInKneelLikeState())
            return true;

        return false;
    }

    bool IsInKneelSequence()
    {
        return kneelPhase != KneelSequencePhase.None;
    }

    public float GetRVPercent()
    {
        if (maxRV <= 0f)
            return 0f;

        return Mathf.Clamp01(currentRV / maxRV);
    }

    public void ClearPendingHitReaction()
    {
        if (animatorController == null || animatorController.Animator == null)
            return;

        animatorController.Animator.ResetTrigger(hitTriggerParam);
        animatorController.Animator.ResetTrigger(hitSmallTriggerParam);
        animatorController.Animator.ResetTrigger(hitBigTriggerParam);
        ResetTriggerIfPresent(parryTriggerParam);
    }

    bool ShouldIgnoreHitReactionCompletely()
    {
        if (bossAI == null || animatorController == null)
            return true;

        if (bossAI.currentState == BOSSAI.BossState.Dead)
            return true;

        if (IsInKneelSequence())
            return true;

        if (currentRV <= 0f && animatorController.IsInKneelLikeState())
            return true;

        if (animatorController.IsInKneelLikeState())
            return true;

        return false;
    }

    bool IsInAttackState()
    {
        if (bossAI != null && bossAI.currentState == BOSSAI.BossState.Attacking)
            return true;

        if (animatorController == null)
            return false;

        return animatorController.IsBusyWithAttackMotion || animatorController.IsInAttackState();
    }

    bool ShouldInterruptAttackForHitReaction(PlayerHitType hitType, GameObject attacker)
    {
        if (!IsInAttackState())
            return false;

        if (currentRV <= 30f)
            return true;

        int delta = ResolveIncomingPriority(attacker, hitType) - GetCurrentCombatPriority();
        return delta > 0;
    }

    void InterruptAttackForHitReaction()
    {
        if (bossAI != null)
            bossAI.ForceInterruptAction();

        if (animatorController == null || animatorController.Animator == null || string.IsNullOrEmpty(locomotionStateName))
            return;

        animatorController.Animator.CrossFade(locomotionStateName, Mathf.Max(0f, hitReactionInterruptBlendDuration), 0);
    }

    void InterruptAttackForParryReaction()
    {
        if (bossAI != null)
            bossAI.ForceInterruptAction();

        if (animatorController == null || animatorController.Animator == null || string.IsNullOrEmpty(locomotionStateName))
            return;

        animatorController.Animator.CrossFade(locomotionStateName, Mathf.Max(0f, hitReactionInterruptBlendDuration), 0);
    }

    void SetTriggerIfPresent(string triggerName)
    {
        if (animatorController == null || animatorController.Animator == null)
            return;

        if (string.IsNullOrWhiteSpace(triggerName))
            return;

        if (!HasTriggerParameter(triggerName))
        {
            Debug.LogWarning($"Boss animator trigger '{triggerName}' was not found.", this);
            return;
        }

        animatorController.Animator.SetTrigger(triggerName);
    }

    void ResetTriggerIfPresent(string triggerName)
    {
        if (animatorController == null || animatorController.Animator == null)
            return;

        if (string.IsNullOrWhiteSpace(triggerName))
            return;

        if (!HasTriggerParameter(triggerName))
            return;

        animatorController.Animator.ResetTrigger(triggerName);
    }

    bool HasTriggerParameter(string triggerName)
    {
        AnimatorControllerParameter[] parameters = animatorController.Animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.name == triggerName && parameter.type == AnimatorControllerParameterType.Trigger)
                return true;
        }

        return false;
    }

    int GetCurrentCombatPriority()
    {
        if (bossAI != null)
            return bossAI.GetCombatPriority();

        return 1;
    }

    int ResolveIncomingPriority(GameObject attacker, PlayerHitType hitType)
    {
        if (attacker != null)
        {
            MonoBehaviour[] components = attacker.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is ICombatPrioritySource prioritySource)
                    return Mathf.Max(1, prioritySource.GetCombatPriority());
            }
        }

        switch (hitType)
        {
            case PlayerHitType.Heavy:
                return 2;
            case PlayerHitType.Sprint:
                return 2;
            default:
                return 1;
        }
    }


}
