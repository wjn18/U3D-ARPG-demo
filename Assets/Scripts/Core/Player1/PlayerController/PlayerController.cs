using System.Collections.Generic;
using UnityEngine;

public enum RollDirection
{
    Front = 0,
    Back = 1,
    Left = 2,
    Right = 3
}

public partial class PlayerController : MonoBehaviour
{
    const string LegacyChargedAttackTriggerParamName = "ChaegedAttackTrigger";
    const string DefaultChargedAttackTriggerParamName = "ChargedAttackTrigger";

    [Header("References")]
    public Animator animator;
    public CharacterController characterController;
    public PlayerLockOn playerLockOn;
    public PlayerStatsRuntime stats;
    public PlayerWeaponTrailController weaponTrailVFX;
    public CombatAudioController combatAudioController;
    public PlayerHitVFXController hitVFXController;
    public RollAfterImageController rollAfterImageVFX;

    [Header("Movement - Free")]
    public float freeMoveSpeed = 6f;
    public float sprintMoveSpeed = 11f;
    public float freeRotationSpeed = 10f;
    public KeyCode sprintKey = KeyCode.LeftShift;

    [Header("Movement - Lock On")]
    public float lockMoveSpeed = 6f;
    public float lockRotationSpeed = 12f;
    public float backwardSpeedMultiplier = 0.85f;

    [Header("Sprint SP Drain")]
    public float sprintSPDrainPerSecond = 10f;

    [Header("Combat Move")]
    [Range(0f, 1f)]
    public float combatMoveMultiplier = 0.2f;

    [Header("Action Locked Movement")]
    public bool disableWASDMovementDuringCombatAnimations = true;
    public bool useStoredRollMotion = true;

    [Header("Attack Facing Correction")]
    public bool snapToLockTargetBeforeAttack = true;
    [Range(0f, 180f)]
    public float lockAttackFacingMaxAngle = 100f;

    [Header("Sprint Attack Move")]
    public float sprintAttackDefaultMoveSpeed = 11f;

    [Header("Roll")]
    public KeyCode rollKey = KeyCode.Space;
    public float rollMoveSpeed = 6f;
    public bool allowRollMovementAssist = false;
    public Vector3 lastMoveInput = Vector3.forward;
    public float rollSPCost = 15f;
    public float rollDirectionInputThreshold = 0.2f;
    public float rollDirectionVelocityThreshold = 0.2f;

    [Header("Roll Move Override")]
    public float rollDefaultMoveSpeed = 6f;

    [Header("Lock-On Roll")]
    public float lockOnRollMoveSpeed = 10f;

    [Header("SP Recovery Control")]
    public bool blockStopsSPRecovery = true;
    public bool sprintStopsSPRecovery = true;

    [Header("Block")]
    public KeyCode blockKey = KeyCode.Mouse2;
    [Range(0f, 1f)]
    public float blockDamageMultiplier = 0.35f;
    public float blockSPCostPerHit = 35f;
    [Range(0f, 1f)]
    public float blockReenterSPPercent = 0.5f;
    public bool autoFaceLockTargetWhileBlocking = true;
    public float blockLockRotationSpeed = 14f;

    [Header("Hit Reaction Animator Params")]
    public string hitTriggerParam = "HitTrigger";
    public string hitSmallTriggerParam = "HitSmallTrigger";
    public string hitBigTriggerParam = "HitBigTrigger";
    public string blockedHitTriggerParam = "BlockedHitTrigger";

    [Header("Gravity")]
    public float gravity = -20f;
    public float groundedStickForce = -2f;

    [Header("Attack Combo")]
    public float comboResetTime = 1.0f;
    public Transform attackPoint;
    public float attackRadius = 2.7f;
    public LayerMask targetLayers;
    public float fallbackAttackDamage = 10f;

    [Header("Player Hit Stop")]
    public int playerHitStopFrames = 3;

    [Header("Attack Data")]
    public PlayerAttackData attack1Data = new PlayerAttackData
    {
        actionName = "Attack 1",
        damage = 20f,
        spCost = 10f,
        apGainPerHit = 0.5f
    };

    public PlayerAttackData attack2Data = new PlayerAttackData
    {
        actionName = "Attack 2",
        damage = 20f,
        spCost = 10f,
        apGainPerHit = 0.5f
    };

    public PlayerAttackData attack3Data = new PlayerAttackData
    {
        actionName = "Attack 3",
        damage = 20f,
        spCost = 10f,
        apGainPerHit = 0.5f
    };

    public PlayerAttackData attack4Data = new PlayerAttackData
    {
        actionName = "Attack 4",
        damage = 25f,
        spCost = 10f,
        apGainPerHit = 0.5f
    };

    public PlayerAttackData heavyAttackData = new PlayerAttackData
    {
        actionName = "Heavy Attack",
        damage = 35f,
        spCost = 15f,
        apGainPerHit = 10f
    };

    public PlayerAttackData sprintAttackData = new PlayerAttackData
    {
        actionName = "Sprint Attack",
        damage = 30f,
        spCost = 20f,
        apGainPerHit = 5f
    };

    [Header("Heavy Attack")]
    public float heavyAttackHoldTime = 0.5f;
    public PlayerAttackData chargedAttackData = new PlayerAttackData
    {
        actionName = "Charged Attack",
        damage = 50f,
        spCost = 25f,
        apGainPerHit = 15f
    };

    [Header("Berserk")]
    public KeyCode berserkKey = KeyCode.V;
    public bool berserkConsumesFullAP = true;
    public float berserkDuration = 60f;
    public float berserkDamageMultiplier = 1.5f;
    public float berserkAPGainMultiplier = 2f;
    public float berserkAttackRadiusMultiplier = 1.5f;
    public float berserkSPRecoveryMultiplier = 2f;
    public float berserkHealPerAttack = 1f;

    [Header("Berserk VFX")]
    public GameObject[] berserkEnterVFX;
    public GameObject[] berserkActiveVFX;
    public float berserkEnterVFXFallbackLifetime = 2f;

    [Header("Power Up Animation")]
    public string powerUpTriggerParam = "PowerUpTrigger";
    public string powerUpStateName = "SandS Power Up";
    public string powerUpStateTag = "PowerUp";

    [Header("Attack Cancel")]
    public string interruptAttackParam = "InterruptAttack";
    public float moveCancelInputThreshold = 0.15f;

    [Header("Animator Params")]
    public string speedParam = "Speed";
    public string moveXParam = "MoveX";
    public string moveYParam = "MoveY";
    public string isLockedOnParam = "IsLockedOn";
    public string isBlockingParam = "IsBlocking";
    public string isDeadParam = "IsDead";
    public string isSprintingParam = "IsSprinting";
    public string attackTriggerParam = "AttackTrigger";
    public string heavyAttackTriggerParam = "HeavyAttackTrigger";
    public string chargedAttackTriggerParam = DefaultChargedAttackTriggerParamName;
    public string sprintAttackTriggerParam = "SprintAttackTrigger";
    public string queueNextAttackParam = "QueueNextAttack";
    public string rollTriggerParam = "RollTrigger";
    public string rollDirectionParam = "RollDirection";
    public string deadChooseParam = "DeadChoose";

    [Header("State")]
    public bool isDead = false;

    bool isAttacking = false;
    bool isBlocking = false;
    bool queueNextAttack = false;
    bool isRolling = false;
    bool isInHitReaction = false;

    bool sprintMode = false;
    bool sprintAttackActive = false;
    bool heavyAttackActive = false;
    bool chargedAttackActive = false;

    bool isBerserkActive = false;
    bool isPoweringUp = false;
    float berserkEndTime = -999f;

    bool canMoveCancelAttack = false;
    bool moveWasHeldWhenCancelWindowOpened = false;
    bool previousMoveHeld = false;

    bool attackWindowActive = false;
    bool attackHitConfirmedThisWindow = false;
    bool avoidWindowOpen = false;
    bool blockLockedByLowSP = false;
    bool hitStopActive = false;
    float hitStopPreviousTimeScale = 1f;
    float hitStopPreviousFixedDeltaTime = 0.02f;

    bool useAttackMoveSpeedOverride = false;
    float attackMoveSpeedOverride = 0f;

    bool useRollMoveSpeedOverride = false;
    float rollMoveSpeedOverride = 0f;
    bool lockOnRollUsesScriptMotion = false;
    bool restoreAnimatorRootMotionAfterLockOnRoll = false;
    bool cachedAnimatorApplyRootMotion = false;

    bool rightMouseTracking = false;
    bool chargedAttackTriggeredThisPress = false;
    float rightMousePressedTime = -999f;

    PlayerAttackData currentAttackData = null;

    int currentAttackStep = 0;
    float lastAttackFinishedTime = -999f;

    float verticalVelocity = 0f;
    float currentSpeed = 0f;

    float currentMoveX = 0f;
    float currentMoveY = 0f;
    float moveXVelocity = 0f;
    float moveYVelocity = 0f;

    public float moveParamSmoothTime = 0.08f;

    Vector3 cachedAttackMotionDirection = Vector3.forward;
    Vector3 cachedRollMotionDirection = Vector3.forward;

    readonly HashSet<IDamageable> hitTargetsThisSwing = new HashSet<IDamageable>();

    void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        characterController = GetComponent<CharacterController>();
        playerLockOn = GetComponent<PlayerLockOn>();
        stats = GetComponent<PlayerStatsRuntime>();
        weaponTrailVFX = GetComponentInChildren<PlayerWeaponTrailController>(true);
        combatAudioController = GetComponentInChildren<CombatAudioController>(true);
        hitVFXController = GetComponentInChildren<PlayerHitVFXController>(true);
        rollAfterImageVFX = GetComponentInChildren<RollAfterImageController>(true);
    }

    void Awake()
    {
        MigrateLegacyAnimatorParamNames();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (playerLockOn == null)
            playerLockOn = GetComponent<PlayerLockOn>();

        if (stats == null)
            stats = GetComponent<PlayerStatsRuntime>();

        if (weaponTrailVFX == null)
            weaponTrailVFX = GetComponentInChildren<PlayerWeaponTrailController>(true);

        if (combatAudioController == null)
            combatAudioController = GetComponentInChildren<CombatAudioController>(true);

        if (hitVFXController == null)
            hitVFXController = GetComponentInChildren<PlayerHitVFXController>(true);

        if (rollAfterImageVFX == null)
            rollAfterImageVFX = GetComponentInChildren<RollAfterImageController>(true);

        InitializeBerserkVFX();
    }

    void OnValidate()
    {
        MigrateLegacyAnimatorParamNames();
    }

    void Update()
    {
        if (animator == null || characterController == null)
            return;

        SyncRollingStateFromAnimator();
        SyncAttackStateFromAnimator();
        SyncHitReactionStateFromAnimator();
        SyncPowerUpStateFromAnimator();
        UpdateBlockReentryLock();
        UpdateBerserkTimer();

        if (isDead)
        {
            ResetLeftMouseAttackTracking();
            ApplyGravityOnly();
            UpdateAnimatorParams(IsLockedOn());
            previousMoveHeld = IsMoveHeld();
            return;
        }

        HandleSprintToggleInput();
        HandleRollInput();
        HandleBerserkInput();
        HandleCombatInput();
        HandleAttackMoveCancel();
        ProcessActiveAttackHitbox();
        HandleMovement();
        HandleSprintSPDrain();
        HandleSPRecovery();
        UpdateAnimatorParams(IsLockedOn());

        previousMoveHeld = IsMoveHeld();
    }

    public bool IsAttacking() => isAttacking;
    public bool IsBlocking() => isBlocking;
    public bool IsDead() => isDead;
    public bool IsRolling() => isRolling;
    public bool IsSprinting() => sprintMode;
    public bool IsSprintAttacking() => sprintAttackActive;
    public bool IsHeavyAttacking() => heavyAttackActive;
    public bool IsBerserkActive() => isBerserkActive;
    public bool IsPoweringUp() => isPoweringUp;

    public void Die()
    {
        if (isDead) return;

        isDead = true;
        isBerserkActive = false;
        isPoweringUp = false;
        PlayPlayerDieVoice();
        ResetLeftMouseAttackTracking();
        ClearActionStateForHit(keepBlocking: false, keepSprint: false);
        EndRollState();
        currentSpeed = 0f;

        animator.SetBool(isBlockingParam, false);
        animator.SetBool(queueNextAttackParam, false);
        if (HasAnimatorParameter(deadChooseParam))
            animator.SetInteger(deadChooseParam, Random.Range(0, 4));
        animator.SetBool(isDeadParam, true);
        animator.SetBool(isSprintingParam, false);

        SetWeaponTrailActive(false);
        HandleBerserkEnded();
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;

        float gizmoRadius = attackRadius;
        if (Application.isPlaying && isBerserkActive)
            gizmoRadius *= berserkAttackRadiusMultiplier;

        Gizmos.color = attackWindowActive ? Color.yellow : Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, gizmoRadius);
    }

    void OnDisable()
    {
        RestorePlayerHitStopTimeScale();
    }

    void MigrateLegacyAnimatorParamNames()
    {
        if (chargedAttackTriggerParam == LegacyChargedAttackTriggerParamName)
            chargedAttackTriggerParam = DefaultChargedAttackTriggerParamName;
    }
}
