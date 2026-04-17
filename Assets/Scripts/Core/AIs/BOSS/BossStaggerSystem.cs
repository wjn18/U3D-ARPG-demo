using UnityEngine;
using UnityEngine.Serialization;

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
    public string kneelTriggerParam = "KneelTrigger";
    public string locomotionStateName = "Locomotion";
    public float hitReactionInterruptBlendDuration = 0.05f;

    [Header("RV")]
    [FormerlySerializedAs("maxRV"), SerializeField, HideInInspector] private float legacyMaxRV = 200f;
    [FormerlySerializedAs("currentRV"), SerializeField, HideInInspector] private float legacyCurrentRV = 200f;
    [Range(0f, 1f)]
    public float alwaysOpenThresholdPercent = 0.3f;

    [Header("Recovery")]
    public float recoverDelay = 3f;
    public float recoverPerSecond = 10f;

    [Header("Stagger Window Cycle")]
    public float initialStaggerWindowDuration = 2f;
    public float superArmorDuration = 6f;

    [Header("Kneel Idle Hold")]
    public float kneelIdleHoldDuration = 2f;

    [Header("Stand Bool Release")]
    public float standBoolReleaseDelay = 1f;

    [Header("Execution")]
    public float executeDistance = 4f;
    public float executeDamage = 80f;

    public float maxRV
    {
        get => bossRuntime != null ? bossRuntime.maxRV : legacyMaxRV;
        private set
        {
            if (bossRuntime != null)
                bossRuntime.maxRV = value;
            else
                legacyMaxRV = value;
        }
    }

    public float currentRV
    {
        get => bossRuntime != null ? bossRuntime.currentRV : legacyCurrentRV;
        private set
        {
            if (bossRuntime != null)
                bossRuntime.SetRV(value);
            else
                legacyCurrentRV = Mathf.Clamp(value, 0f, legacyMaxRV);
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

        currentRV = Mathf.Clamp(legacyCurrentRV, 0f, maxRV);
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

        maxRV = Mathf.Max(1f, config.maxRV);
        alwaysOpenThresholdPercent = config.alwaysOpenThresholdPercent;
        recoverDelay = config.recoverDelay;
        recoverPerSecond = config.recoverPerSecond;
        initialStaggerWindowDuration = config.initialStaggerWindowDuration;
        superArmorDuration = config.superArmorDuration;
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
        TakeStaggerDamage(amount, PlayerHitType.Normal);
    }

    public void TakeStaggerDamage(float amount, PlayerHitType hitType)
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

        if (!CanPlayHitReaction(hitType))
        {
            ClearPendingHitReaction();
            return;
        }

        if (ShouldInterruptAttackForHitReaction(hitType))
            InterruptAttackForHitReaction();

        ClearPendingHitReaction();
        animatorController.Animator.SetTrigger(hitTriggerParam);
    }

    bool CanPlayHitReaction(PlayerHitType hitType)
    {
        if (bossAI == null || animatorController == null)
            return false;

        if (ShouldIgnoreHitReactionCompletely())
            return false;

        if (currentRV <= 30f)
            return true;

        if (hitType == PlayerHitType.Heavy)
            return true;

        if (IsInAttackState())
            return false;

        return true;
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

    bool ShouldInterruptAttackForHitReaction(PlayerHitType hitType)
    {
        if (!IsInAttackState())
            return false;

        if (currentRV <= 30f)
            return true;

        return hitType == PlayerHitType.Heavy;
    }

    void InterruptAttackForHitReaction()
    {
        if (bossAI != null)
            bossAI.ForceInterruptAction();

        if (animatorController == null || animatorController.Animator == null || string.IsNullOrEmpty(locomotionStateName))
            return;

        animatorController.Animator.CrossFade(locomotionStateName, Mathf.Max(0f, hitReactionInterruptBlendDuration), 0);
    }


}
