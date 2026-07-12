using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCombatController : MonoBehaviour, ICombatPrioritySource
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayerLockOn playerLockOn;
    [SerializeField] private PlayerStatsRuntime stats;
    [SerializeField] private CombatAudioController combatAudioController;
    [SerializeField] private PlayerHitVFXController hitVfxController;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Transform attackPoint;

    [Header("Config")]
    [SerializeField] private bool useIntegratedController = true;
    [SerializeField] private PlayerCombatConfig combatConfig;
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField] private float targetApproachDistance = 3f;

    [Header("Movement - Free")]
    [SerializeField] private float freeMoveSpeed = 6f;
    [SerializeField] private float sprintMoveSpeed = 11f;
    [SerializeField] private float freeRotationSpeed = 10f;
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;

    [Header("Movement - Lock On")]
    [SerializeField] private float lockMoveSpeed = 6f;
    [SerializeField] private float lockRotationSpeed = 12f;
    [SerializeField] private float backwardSpeedMultiplier = 0.85f;

    [Header("Avoid")]
    [SerializeField] private KeyCode avoidKey = KeyCode.Space;

    [Header("Parry")]
    [SerializeField] private KeyCode parryKey = KeyCode.F;

    [Header("Input")]
    [SerializeField] private KeyCode lightAttackKey = KeyCode.Mouse0;
    [SerializeField] private KeyCode heavyAttackKey = KeyCode.Mouse1;
    [SerializeField] private float heavyAttackReleaseWindow = 0.4f;

    [Header("Animator Params")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string moveXParam = "MoveX";
    [SerializeField] private string moveYParam = "MoveY";
    [SerializeField] private string isLockedOnParam = "IsLockedOn";
    [SerializeField] private string isDeadParam = "IsDead";
    [SerializeField] private string isSprintingParam = "IsSprinting";
    [SerializeField] private string rollDirectionParam = "RollDirection";
    [SerializeField] private string hitSmallTrigger = "HitSmallTrigger";
    [SerializeField] private string hitBigTrigger = "HitBigTrigger";
    [SerializeField] private string parryTriggerParam = "ParryTrigger";
    [SerializeField] private string deadChooseParam = "DeadChoose";

    [Header("Buffer")]
    [SerializeField] private float bufferWindowSeconds = 1.2f;
    [SerializeField] private int maxStoredInputs = 10;
    [SerializeField] private CombatInputBuffer inputBuffer = new CombatInputBuffer();

    [Header("Special Combos")]
    [SerializeField] private List<ComboDefinition> comboDefinitions = new List<ComboDefinition>();

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedStickForce = -2f;

    [Header("Debug")]
    [SerializeField] private bool debugInputLog = false;
    [SerializeField] private bool debugStateLog = false;

    [Header("Runtime")]
    [SerializeField] private CombatExecutionState currentState = CombatExecutionState.Locomotion;
    [SerializeField] private bool chainWindowOpen = false;
    [SerializeField] private bool avoidWindowOpen = false;
    [SerializeField] private bool attackWindowActive = false;
    [SerializeField] private bool parryWindowActive = false;
    [SerializeField] private bool sprintMode = false;

    private bool isDead = false;
    private bool executionLocked = false;
    private bool heavyButtonTracking = false;
    private bool chargedAttackTriggeredThisPress = false;
    private float heavyButtonPressedTime = -999f;
    private float verticalVelocity = 0f;
    private float currentSpeed = 0f;
    private float currentMoveX = 0f;
    private float currentMoveY = 0f;
    private float moveXVelocity = 0f;
    private float moveYVelocity = 0f;
    private float lastExecutedSpecialEndTimestamp = float.NegativeInfinity;

    private CombatAttackData currentAttackData;
    private CombatParryData currentParryData;
    private CombatAvoidData currentAvoidData;
    private CombatHitData currentHitData;
    private bool chargedAttackActive = false;
    private CombatInputType? activeChainInputType = null;
    private int activeLightAttackIndex = -1;
    private int activeHeavyAttackIndex = -1;
    private Vector3 lastMoveInput = Vector3.forward;
    private Vector3 actionMoveDirection = Vector3.zero;
    private float actionMoveRemainingDistance = 0f;
    private float actionMoveSpeed = 0f;
    private RollDirection currentAvoidDirection = RollDirection.Front;
    private readonly HashSet<IDamageable> hitTargetsThisAction = new HashSet<IDamageable>();
    private readonly HashSet<BOSSAI> parriedBossesThisAction = new HashSet<BOSSAI>();
    private GameObject activeLocomotionVfxInstance;
    private GameObject activeActionVfxInstance;
    private CombatLocomotionKind? activeLocomotionKind = null;
    private AnimationClip activeAttackAnimationClip;
    private int activeAttackStateHash = 0;
    private AnimationClip activeHitAnimationClip;
    private CombatAttackData prepaidAttackData;
    private bool currentAttackSpApplied = false;
    private bool attackStartFeedbackPlayed = false;
    private Vector3 pendingHitMoveDirection = Vector3.zero;
    private bool pendingHitMovement = false;
    private float parryStateMinEndTime = 0f;

    public bool HasCombatConfigAsset => combatConfig != null;
    public PlayerCombatConfig CombatConfig => combatConfig;
    public bool UsesIntegratedControl => useIntegratedController && combatConfig != null;
    public bool IsDead => isDead;

    void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        characterController = GetComponent<CharacterController>();
        playerLockOn = GetComponent<PlayerLockOn>();
        stats = GetComponent<PlayerStatsRuntime>();
        combatAudioController = GetComponentInChildren<CombatAudioController>(true);
        hitVfxController = FindHitVfxController();
        audioSource = GetComponentInChildren<AudioSource>(true);
        attackPoint = transform.Find("AttackPoint");
    }

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (playerLockOn == null)
            playerLockOn = GetComponent<PlayerLockOn>();

        if (stats == null)
            stats = GetComponent<PlayerStatsRuntime>();

        if (combatAudioController == null)
            combatAudioController = GetComponentInChildren<CombatAudioController>(true);

        if (hitVfxController == null)
            hitVfxController = FindHitVfxController();

        if (audioSource == null)
            audioSource = GetComponentInChildren<AudioSource>(true);

        inputBuffer.Configure(bufferWindowSeconds, maxStoredInputs);

        if (UsesIntegratedControl)
            DisableLegacyControllers();
    }

    void OnValidate()
    {
        if (inputBuffer == null)
            inputBuffer = new CombatInputBuffer();

        inputBuffer.Configure(bufferWindowSeconds, maxStoredInputs);

        if (hitVfxController == null)
            hitVfxController = FindHitVfxController();
    }

    void Update()
    {
        if (!UsesIntegratedControl || animator == null || characterController == null)
            return;

        SyncStateFromAnimator();

        if (isDead)
        {
            HandleMovement(Vector2.zero);
            UpdateAnimatorParams(Vector2.zero);
            return;
        }

        float currentTime = Time.time;
        inputBuffer.Configure(bufferWindowSeconds, maxStoredInputs);
        inputBuffer.Prune(currentTime);

        HandleSprintToggleInput();
        HandleParryInput();
        CaptureAttackInput(currentTime);
        HandleAvoidInput();
        TryExecuteBufferedInput();

        Vector2 moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        StopSprintWhenMovementStops(moveInput);
        HandleMovement(moveInput);

        if (attackWindowActive && currentAttackData != null)
            ProcessCurrentAttackHitbox();

        if (parryWindowActive)
            ProcessCurrentParryHitbox();

        UpdateSprintSPDrain(moveInput);
        UpdateAnimatorParams(moveInput);
        UpdateLocomotionFeedback();
        UpdateSPRecovery();
    }

    public void SetCombatState(CombatExecutionState newState)
    {
        currentState = newState;

        if (newState != CombatExecutionState.Attack)
            chainWindowOpen = false;

        if (newState != CombatExecutionState.Parry)
            parryWindowActive = false;

        if (newState == CombatExecutionState.Locomotion || newState == CombatExecutionState.LockOnLocomotion)
            executionLocked = false;

        if (debugStateLog)
            Debug.Log("[PlayerCombatController] State -> " + currentState);
    }

    public void OpenChainWindow()
    {
        currentState = CombatExecutionState.Attack;
        chainWindowOpen = true;
        executionLocked = false;
    }

    public void CloseChainWindow()
    {
        chainWindowOpen = false;
        executionLocked = true;
    }

    public void AE_OpenChainWindow() => OpenChainWindow();
    public void AE_CloseChainWindow() => CloseChainWindow();
    public void AE_StateLocomotion() => SetCombatState(IsLockedOn() ? CombatExecutionState.LockOnLocomotion : CombatExecutionState.Locomotion);
    public void AE_StateLockOnLocomotion() => SetCombatState(CombatExecutionState.LockOnLocomotion);
    public void AE_StateAttack() => SetCombatState(CombatExecutionState.Attack);
    public void AE_StateHit() => SetCombatState(CombatExecutionState.Hit);
    public void AE_StateRoll() => SetCombatState(CombatExecutionState.Roll);
    public void AE_StateKnockback() => SetCombatState(CombatExecutionState.Knockback);
    public void AE_StateDead() => SetCombatState(CombatExecutionState.Dead);
    PlayerHitVFXController FindHitVfxController()
    {
        return GetComponentInChildren<PlayerHitVFXController>(true);
    }

    public void AE_BeginAttackWindow()
    {
        attackWindowActive = true;
        hitTargetsThisAction.Clear();
        StopActionMovement();
    }

    public void AE_EndAttackWindow()
    {
        attackWindowActive = false;
    }

    public void AE_BeginParryWindow()
    {
        if (currentState != CombatExecutionState.Parry)
            return;

        parryWindowActive = true;
        parriedBossesThisAction.Clear();
        StopActionMovement();
    }

    public void AE_EndParryWindow()
    {
        parryWindowActive = false;
    }

    public void AE_AvoidWindowOn()
    {
        avoidWindowOpen = true;
    }

    public void AE_AvoidWindowOff()
    {
        avoidWindowOpen = false;
    }

    public void AE_ActionVfxOn()
    {
        if (currentAttackData != null)
            StartActionVfx(currentAttackData.attackVFX, currentAttackData.vfxSocketId);
        else if (currentAvoidData != null)
            StartActionVfx(currentAvoidData.vfx, currentAvoidData.vfxSocketId);
    }

    public void AE_ActionVfxOff()
    {
        StopActionVfx();
    }

    public void AE_TriggerChargedAttackHit()
    {
        if (currentAttackData == null || !chargedAttackActive || combatConfig == null || combatConfig.chargedAttack == null)
            return;

        string socketId = combatConfig.chargedAttack.hitboxSocketId;
        if (string.IsNullOrWhiteSpace(socketId))
            return;

        BoxCollider hitbox = FindNamedBoxCollider(socketId);
        if (hitbox == null)
            return;

        Vector3 center = hitbox.transform.TransformPoint(hitbox.center);
        Vector3 halfExtents = Vector3.Scale(hitbox.size, hitbox.transform.lossyScale) * 0.5f;
        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            hitbox.transform.rotation,
            targetLayers,
            QueryTriggerInteraction.Ignore
        );

        ApplyHits(hits, currentAttackData);
    }

    // Compatibility hooks so existing animation events / SMBs can stay in place
    // while Player1 and Player2 move over to the integrated controller.
    public void NotifyAttackStarted()
    {
        if (!UsesIntegratedControl || isDead)
            return;

        currentState = CombatExecutionState.Attack;
    }

    public void NotifyAttackEnded()
    {
        if (!UsesIntegratedControl || isDead)
            return;

        if (currentState == CombatExecutionState.Attack && !IsInTaggedAnimation("Attack"))
            ForceReturnToLocomotion();
    }

    public void AE_PlayAttackSFX()
    {
        PlayAttackStartFeedback();
    }

    public void AE_DoAttackHit()
    {
        if (!UsesIntegratedControl || currentAttackData == null)
            return;

        hitTargetsThisAction.Clear();
        ProcessCurrentAttackHitbox();
    }

    public void AE_EndRoll()
    {
        if (!UsesIntegratedControl)
            return;

        if (currentState == CombatExecutionState.Roll)
            ForceReturnToLocomotion();
    }

    public void AE_EnableMoveCancel()
    {
        if (!UsesIntegratedControl || currentState != CombatExecutionState.Attack)
            return;

        avoidWindowOpen = true;
    }

    public void AE_DisableMoveCancel()
    {
        avoidWindowOpen = false;
    }

    public void AvoidWindowsOn() => AE_AvoidWindowOn();
    public void AvoidWindowsOff() => AE_AvoidWindowOff();

    public void AE_EndAttack()
    {
        if (!UsesIntegratedControl)
            return;

        if (currentState == CombatExecutionState.Attack)
            ForceReturnToLocomotion();
    }

    public void AE_EndSprintAttack() => AE_EndAttack();
    public void AE_SetAttackMoveSpeed(float speed) { }
    public void AE_ClearAttackMoveSpeed() { }
    public void AE_SetSprintAttackMoveSpeed() { }
    public void AE_SetRollMoveSpeed(float speed) { }
    public void AE_ClearRollMoveSpeed() { }
    public void AE_EnteredAttackStep2() { }
    public void AE_EnteredAttackStep3() { }
    public void AE_EnteredAttackStep4() { }

    public int GetCombatPriority()
    {
        if (currentState == CombatExecutionState.Attack && currentAttackData != null)
            return Mathf.Max(1, currentAttackData.priority);

        if (currentState == CombatExecutionState.Parry && currentParryData != null)
            return Mathf.Max(1, currentParryData.priority);

        if (currentState == CombatExecutionState.Roll && combatConfig != null)
            return Mathf.Max(1, combatConfig.avoidPriority);

        if (combatConfig != null)
            return Mathf.Max(1, combatConfig.locomotionPriority);

        return 1;
    }

    public void ProcessIncomingHit(ref float damage, GameObject attacker)
    {
        if (!UsesIntegratedControl || isDead)
            return;

        int incomingPriority = ResolveIncomingPriority(attacker);
        int delta = incomingPriority - GetCombatPriority();
        if (delta <= 0)
            return;

        Vector3 hitMoveDirection = ResolveHitMoveDirection(attacker);
        CancelCurrentAction();
        pendingHitMoveDirection = hitMoveDirection;
        pendingHitMovement = true;
        activeHitAnimationClip = null;
        currentHitData = null;

        string triggerName = delta == 1 ? hitSmallTrigger : hitBigTrigger;
        TrySetTrigger(triggerName);
        currentState = delta == 1 ? CombatExecutionState.Hit : CombatExecutionState.Knockback;
    }

    public void HandleDeath()
    {
        if (!UsesIntegratedControl || isDead)
            return;

        isDead = true;
        sprintMode = false;
        CancelCurrentAction();
        currentState = CombatExecutionState.Dead;
        inputBuffer.ClearAll();
        StopLocomotionFeedback();
        StopActionVfx();

        if (HasIntParameter(deadChooseParam))
            animator.SetInteger(deadChooseParam, UnityEngine.Random.Range(0, 4));

        if (HasBoolParameter(isDeadParam))
            animator.SetBool(isDeadParam, true);
    }

    void DisableLegacyControllers()
    {
        DisableLegacyControllerByTypeName("PlayerController");
        DisableLegacyControllerByTypeName("PlayerAttackCancelController");
    }

    void DisableLegacyControllerByTypeName(string typeName)
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == this)
                continue;

            if (behaviour.GetType().Name != typeName)
                continue;

            behaviour.enabled = false;
        }
    }

    void CaptureAttackInput(float currentTime)
    {
        if (WasBindingPressed(lightAttackKey))
            RecordInput(CombatInputType.X, currentTime);

        if (WasBindingPressed(heavyAttackKey))
        {
            heavyButtonTracking = true;
            chargedAttackTriggeredThisPress = false;
            heavyButtonPressedTime = currentTime;
        }

        if (heavyButtonTracking &&
            !chargedAttackTriggeredThisPress &&
            combatConfig != null &&
            combatConfig.chargedAttack != null &&
            combatConfig.chargedAttack.attack != null &&
            currentTime - heavyButtonPressedTime >= Mathf.Max(0f, combatConfig.chargedAttack.holdTime))
        {
            if (TryStartChargedAttack())
                chargedAttackTriggeredThisPress = true;
        }

        if (WasBindingReleased(heavyAttackKey))
        {
            bool allowHeavyRelease =
                !chargedAttackTriggeredThisPress &&
                currentTime - heavyButtonPressedTime <= Mathf.Max(0f, heavyAttackReleaseWindow);

            if (allowHeavyRelease)
                RecordInput(CombatInputType.Y, currentTime);

            heavyButtonTracking = false;
            chargedAttackTriggeredThisPress = false;
            heavyButtonPressedTime = -999f;
        }
    }

    bool WasBindingPressed(KeyCode keyCode)
    {
        int mouseButton = GetMouseButtonIndex(keyCode);
        if (mouseButton >= 0)
            return Input.GetMouseButtonDown(mouseButton);

        return Input.GetKeyDown(keyCode);
    }

    bool WasBindingReleased(KeyCode keyCode)
    {
        int mouseButton = GetMouseButtonIndex(keyCode);
        if (mouseButton >= 0)
            return Input.GetMouseButtonUp(mouseButton);

        return Input.GetKeyUp(keyCode);
    }

    static int GetMouseButtonIndex(KeyCode keyCode)
    {
        switch (keyCode)
        {
            case KeyCode.Mouse0:
                return 0;
            case KeyCode.Mouse1:
                return 1;
            case KeyCode.Mouse2:
                return 2;
            default:
                return -1;
        }
    }

    void RecordInput(CombatInputType inputType, float currentTime)
    {
        inputBuffer.RecordInput(inputType, currentTime);

        if (debugInputLog)
            Debug.Log("[PlayerCombatController] Buffered input: " + inputType);
    }

    void TryExecuteBufferedInput()
    {
        if (!CanExecuteNow() || executionLocked)
            return;

        if (TryExecuteSpecialCombo())
            return;

        if (!inputBuffer.TryPeekPending(out CombatBufferedInput bufferedInput))
            return;

        bool started = false;
        if (bufferedInput.inputType == CombatInputType.X)
        {
            if (CanStartSprintAttack())
                started = TryStartAttack(combatConfig.sprintAttack, null, -1, false);
            else
                started = TryStartChainAttack(combatConfig.lightAttacks, CombatInputType.X, ref activeLightAttackIndex);
        }
        else
        {
            started = TryStartChainAttack(combatConfig.heavyAttacks, CombatInputType.Y, ref activeHeavyAttackIndex);
        }

        if (started)
            inputBuffer.ConsumePending(bufferedInput);
    }

    bool TryExecuteSpecialCombo()
    {
        if (comboDefinitions == null || comboDefinitions.Count == 0)
            return false;

        if (!ComboMatcher.TryFindBestMatch(
                inputBuffer.History,
                comboDefinitions,
                lastExecutedSpecialEndTimestamp,
                out ComboMatchResult match))
        {
            return false;
        }

        if (!HasEnoughSPForSpecialCombo(match.Definition))
            return false;

        if (!TrySetTrigger(match.Definition.AnimatorTrigger))
            return false;

        lastExecutedSpecialEndTimestamp = match.EndTimestamp;
        inputBuffer.ClearAll();
        CancelCurrentAction();
        currentState = CombatExecutionState.Attack;
        executionLocked = true;
        return true;
    }

    bool HasEnoughSPForSpecialCombo(ComboDefinition comboDefinition)
    {
        if (comboDefinition == null || stats == null)
            return true;

        float requiredSp = ResolveSpecialComboSpCost(comboDefinition);
        if (requiredSp <= 0f)
            return true;

        bool hasEnough = stats.HasEnoughSP(requiredSp);
        if (!hasEnough && debugInputLog)
        {
            Debug.Log(
                "[PlayerCombatController] Not enough SP for combo " +
                comboDefinition.ComboName +
                ". Required=" + requiredSp +
                ", Current=" + stats.sp);
        }

        return hasEnough;
    }

    float ResolveSpecialComboSpCost(ComboDefinition comboDefinition)
    {
        if (comboDefinition == null || combatConfig == null || comboDefinition.InputSequence == null)
            return 0f;

        float totalSpCost = 0f;
        int xCount = 0;
        int yCount = 0;

        for (int i = 0; i < comboDefinition.InputSequence.Count; i++)
        {
            CombatInputType inputType = comboDefinition.InputSequence[i];
            int occurrenceIndex;
            if (inputType == CombatInputType.X)
            {
                xCount++;
                occurrenceIndex = xCount;
            }
            else
            {
                yCount++;
                occurrenceIndex = yCount;
            }

            CombatAttackData attackData = ResolveSpecialComboAttackData(comboDefinition.ComboName, inputType, occurrenceIndex);
            if (attackData == null)
            {
                Debug.LogWarning(
                    "[PlayerCombatController] Missing combo attack config for " +
                    comboDefinition.ComboName +
                    " step " + inputType + occurrenceIndex,
                    this);
                return 0f;
            }

            totalSpCost += Mathf.Max(0f, attackData.spCost);
        }

        return totalSpCost;
    }

    CombatAttackData ResolveSpecialComboAttackData(string comboName, CombatInputType inputType, int occurrenceIndex)
    {
        CombatAttackData comboSpecificAttack = FindComboSpecificAttackData(comboName, inputType, occurrenceIndex);
        if (comboSpecificAttack != null)
            return comboSpecificAttack;

        List<CombatAttackData> attackList = inputType == CombatInputType.X
            ? combatConfig.lightAttacks
            : combatConfig.heavyAttacks;

        int listIndex = occurrenceIndex - 1;
        if (attackList == null || listIndex < 0 || listIndex >= attackList.Count)
            return null;

        return attackList[listIndex];
    }

    CombatAttackData FindComboSpecificAttackData(string comboName, CombatInputType inputType, int occurrenceIndex)
    {
        string normalizedComboName = NormalizeComboLookupToken(comboName);
        if (string.IsNullOrEmpty(normalizedComboName) || combatConfig == null)
            return null;

        string normalizedStepToken = NormalizeComboLookupToken(inputType.ToString() + occurrenceIndex);

        CombatAttackData attackData = FindComboSpecificAttackData(combatConfig.lightAttacks, normalizedComboName, normalizedStepToken);
        if (attackData != null)
            return attackData;

        return FindComboSpecificAttackData(combatConfig.heavyAttacks, normalizedComboName, normalizedStepToken);
    }

    static CombatAttackData FindComboSpecificAttackData(List<CombatAttackData> attackList, string normalizedComboName, string normalizedStepToken)
    {
        if (attackList == null)
            return null;

        for (int i = 0; i < attackList.Count; i++)
        {
            CombatAttackData attackData = attackList[i];
            if (attackData == null)
                continue;

            string normalizedAttackName = NormalizeComboLookupToken(attackData.attackName);
            string normalizedStateName = NormalizeComboLookupToken(attackData.animatorStateName);

            bool comboMatch = normalizedAttackName.Contains(normalizedComboName) || normalizedStateName.Contains(normalizedComboName);
            if (!comboMatch)
                continue;

            bool stepMatch = normalizedAttackName.Contains(normalizedStepToken) || normalizedStateName.Contains(normalizedStepToken);
            if (stepMatch)
                return attackData;
        }

        return null;
    }

    static string NormalizeComboLookupToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] buffer = new char[value.Length];
        int count = 0;

        for (int i = 0; i < value.Length; i++)
        {
            char c = char.ToLowerInvariant(value[i]);
            if (!char.IsLetterOrDigit(c))
                continue;

            buffer[count++] = c;
        }

        return count > 0 ? new string(buffer, 0, count) : string.Empty;
    }

    bool TryStartChainAttack(List<CombatAttackData> attackList, CombatInputType inputType, ref int activeIndex)
    {
        if (attackList == null || attackList.Count == 0)
            return false;

        int nextIndex = 0;
        if (currentState == CombatExecutionState.Attack &&
            chainWindowOpen &&
            activeChainInputType.HasValue &&
            activeChainInputType.Value == inputType)
        {
            nextIndex = activeIndex + 1;
        }

        if (nextIndex < 0 || nextIndex >= attackList.Count)
            return false;

        return TryStartAttack(attackList[nextIndex], inputType, nextIndex, false);
    }

    bool TryStartChargedAttack()
    {
        if (combatConfig == null || combatConfig.chargedAttack == null || combatConfig.chargedAttack.attack == null)
            return false;

        return TryStartAttack(combatConfig.chargedAttack.attack, CombatInputType.Y, -1, true);
    }

    bool TryStartAttack(CombatAttackData attackData, CombatInputType? chainInputType, int attackIndex, bool charged)
    {
        if (attackData == null || string.IsNullOrWhiteSpace(attackData.animatorStateName))
            return false;

        if (!CanStartAttack())
            return false;

        if (!TrySpendSP(attackData.spCost))
            return false;

        ResetActionRuntime();

        currentAttackData = attackData;
        currentAvoidData = null;
        chargedAttackActive = charged;
        prepaidAttackData = attackData;
        currentAttackSpApplied = true;
        activeChainInputType = charged ? null : chainInputType;
        executionLocked = true;
        chainWindowOpen = false;
        currentState = CombatExecutionState.Attack;

        if (chainInputType == CombatInputType.X)
        {
            activeLightAttackIndex = attackIndex;
            activeHeavyAttackIndex = -1;
        }
        else if (chainInputType == CombatInputType.Y)
        {
            activeHeavyAttackIndex = attackIndex;
            activeLightAttackIndex = -1;
        }
        else
        {
            activeLightAttackIndex = -1;
            activeHeavyAttackIndex = -1;
        }

        FaceLockTarget();
        BeginAttackMovement(attackData);
        StopLocomotionFeedback();
        animator.CrossFadeInFixedTime(attackData.animatorStateName, Mathf.Max(0f, attackData.transitionDuration));
        return true;
    }

    bool TryStartAvoid()
    {
        bool inLocomotion = currentState == CombatExecutionState.Locomotion || currentState == CombatExecutionState.LockOnLocomotion;
        bool canCancelAttack = currentState == CombatExecutionState.Attack && avoidWindowOpen;

        if (!inLocomotion && !canCancelAttack)
            return false;

        RollDirection direction = DetermineAvoidDirection();
        CombatAvoidData avoidData = combatConfig != null ? combatConfig.GetAvoidData(direction) : null;
        if (avoidData == null || string.IsNullOrWhiteSpace(avoidData.animatorStateName))
            return false;

        float avoidSpCost = stats != null ? Mathf.Max(0f, stats.avoidSpCost) : 15f;
        if (!TrySpendSP(avoidSpCost))
            return false;

        ResetActionRuntime();
        currentAvoidData = avoidData;
        currentAvoidDirection = direction;
        executionLocked = true;
        chainWindowOpen = false;
        avoidWindowOpen = false;
        currentState = CombatExecutionState.Roll;
        activeChainInputType = null;
        activeLightAttackIndex = -1;
        activeHeavyAttackIndex = -1;

        if (HasIntParameter(rollDirectionParam))
            animator.SetInteger(rollDirectionParam, (int)direction);

        PlayOneShot(avoidData.dodgeSound);
        StopLocomotionFeedback();
        animator.CrossFadeInFixedTime(avoidData.animatorStateName, Mathf.Max(0f, avoidData.transitionDuration));
        BeginActionMovement(GetWorldDirection(direction), avoidData.moveDistance, avoidData.animation != null ? avoidData.animation.length : 0.1f);
        return true;
    }

    bool TryStartParry()
    {
        if (!CanStartParry())
            return false;

        CombatParryData parryData = combatConfig != null ? combatConfig.parry : null;
        if (parryData == null || string.IsNullOrWhiteSpace(parryData.animatorStateName))
        {
            Debug.LogWarning("[PlayerCombatController] Parry config is missing or has no animator state.", this);
            return false;
        }

        if (!HasTriggerParameter(parryTriggerParam))
        {
            Debug.LogWarning($"[PlayerCombatController] Animator trigger '{parryTriggerParam}' is missing.", this);
            return false;
        }

        if (!TrySpendSP(parryData.spCost))
            return false;

        ResetActionRuntime();
        currentParryData = parryData;
        currentState = CombatExecutionState.Parry;
        executionLocked = true;
        sprintMode = false;
        parryWindowActive = false;
        parryStateMinEndTime = Time.time + Mathf.Max(0.05f, parryData.transitionDuration);
        parriedBossesThisAction.Clear();

        FaceLockTarget();
        PlayOneShot(parryData.releaseVoice);
        StopLocomotionFeedback();
        TrySetTrigger(parryTriggerParam);
        return true;
    }

    void HandleAvoidInput()
    {
        if (!Input.GetKeyDown(avoidKey))
            return;

        TryStartAvoid();
    }

    void HandleParryInput()
    {
        if (!Input.GetKeyDown(parryKey))
            return;

        TryStartParry();
    }

    void HandleSprintToggleInput()
    {
        if (!Input.GetKeyDown(sprintKey))
            return;

        if (currentState != CombatExecutionState.Locomotion && currentState != CombatExecutionState.LockOnLocomotion)
            return;

        sprintMode = !sprintMode;
    }

    void StopSprintWhenMovementStops(Vector2 moveInput)
    {
        bool inLocomotion = currentState == CombatExecutionState.Locomotion ||
                            currentState == CombatExecutionState.LockOnLocomotion;

        if (sprintMode && inLocomotion && moveInput.sqrMagnitude <= 0.01f)
            sprintMode = false;
    }

    bool CanStartSprintAttack()
    {
        if (!sprintMode || combatConfig == null || combatConfig.sprintAttack == null)
            return false;

        Vector2 moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        return moveInput.sqrMagnitude > 0.0001f;
    }

    bool CanStartAttack()
    {
        if (currentState == CombatExecutionState.Locomotion || currentState == CombatExecutionState.LockOnLocomotion)
            return true;

        return currentState == CombatExecutionState.Attack && chainWindowOpen;
    }

    bool CanStartParry()
    {
        return currentState == CombatExecutionState.Locomotion || currentState == CombatExecutionState.LockOnLocomotion;
    }

    bool CanExecuteNow()
    {
        if (currentState == CombatExecutionState.Locomotion || currentState == CombatExecutionState.LockOnLocomotion)
            return true;

        return currentState == CombatExecutionState.Attack && chainWindowOpen;
    }

    void HandleMovement(Vector2 moveInput)
    {
        bool lockedOn = IsLockedOn();
        Vector3 moveVelocity = Vector3.zero;
        bool hitMotionActive =
            currentState == CombatExecutionState.Hit ||
            currentState == CombatExecutionState.Knockback ||
            IsInTaggedAnimation("Hit");
        bool canUseLocomotionInput =
            !hitMotionActive &&
            (currentState == CombatExecutionState.Locomotion || currentState == CombatExecutionState.LockOnLocomotion);

        if (canUseLocomotionInput)
        {
            if (lockedOn)
            {
                Transform target = playerLockOn.GetTargetTransform();
                if (target != null)
                    FaceTarget(target, lockRotationSpeed);
            }

            Vector3 worldMove = GetNormalizedWorldInput(moveInput.x, moveInput.y);
            if (worldMove.sqrMagnitude > 0.0001f)
                lastMoveInput = worldMove;

            if (!lockedOn && worldMove.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(worldMove, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, freeRotationSpeed * Time.deltaTime);
            }

            float baseSpeed = lockedOn ? lockMoveSpeed : freeMoveSpeed;
            if (sprintMode)
                baseSpeed = lockedOn ? sprintMoveSpeed : sprintMoveSpeed;

            if (lockedOn)
            {
                float animMoveX = Vector3.Dot(worldMove, transform.right);
                float animMoveY = Vector3.Dot(worldMove, transform.forward);
                float speedMul = animMoveY < -0.01f ? backwardSpeedMultiplier : 1f;
                moveVelocity = worldMove * (baseSpeed * speedMul);
                currentMoveX = Mathf.SmoothDamp(currentMoveX, animMoveX, ref moveXVelocity, 0.08f);
                currentMoveY = Mathf.SmoothDamp(currentMoveY, animMoveY, ref moveYVelocity, 0.08f);
            }
            else
            {
                moveVelocity = worldMove * baseSpeed;
                currentMoveX = Mathf.SmoothDamp(currentMoveX, 0f, ref moveXVelocity, 0.08f);
                currentMoveY = Mathf.SmoothDamp(currentMoveY, 0f, ref moveYVelocity, 0.08f);
            }
        }
        else
        {
            currentMoveX = Mathf.SmoothDamp(currentMoveX, 0f, ref moveXVelocity, 0.08f);
            currentMoveY = Mathf.SmoothDamp(currentMoveY, 0f, ref moveYVelocity, 0.08f);

            if (actionMoveRemainingDistance > 0f && actionMoveSpeed > 0f)
            {
                float frameDistance = Mathf.Min(actionMoveRemainingDistance, actionMoveSpeed * Time.deltaTime);
                moveVelocity = actionMoveDirection * (frameDistance / Mathf.Max(Time.deltaTime, 0.0001f));
                actionMoveRemainingDistance -= frameDistance;
            }
        }

        ApplyGravity(ref moveVelocity);
        currentSpeed = new Vector3(moveVelocity.x, 0f, moveVelocity.z).magnitude;
        characterController.Move(moveVelocity * Time.deltaTime);
    }

    void UpdateAnimatorParams(Vector2 moveInput)
    {
        if (HasBoolParameter(isLockedOnParam))
            animator.SetBool(isLockedOnParam, IsLockedOn());

        if (HasBoolParameter(isDeadParam))
            animator.SetBool(isDeadParam, isDead);

        if (HasBoolParameter(isSprintingParam))
            animator.SetBool(isSprintingParam, sprintMode);

        if (HasFloatParameter(speedParam))
            animator.SetFloat(speedParam, currentSpeed, 0.1f, Time.deltaTime);

        if (HasFloatParameter(moveXParam))
            animator.SetFloat(moveXParam, currentMoveX, 0.08f, Time.deltaTime);

        if (HasFloatParameter(moveYParam))
            animator.SetFloat(moveYParam, currentMoveY, 0.08f, Time.deltaTime);
    }

    void UpdateLocomotionFeedback()
    {
        if (currentState != CombatExecutionState.Locomotion && currentState != CombatExecutionState.LockOnLocomotion)
        {
            StopLocomotionFeedback();
            return;
        }

        CombatLocomotionKind desiredKind = currentState == CombatExecutionState.LockOnLocomotion
            ? CombatLocomotionKind.LockOn
            : CombatLocomotionKind.Free;

        if (activeLocomotionKind.HasValue && activeLocomotionKind.Value == desiredKind)
            return;

        StopLocomotionFeedback();

        if (combatConfig == null)
            return;

        CombatLocomotionData locomotionData = combatConfig.GetLocomotionData(desiredKind);
        activeLocomotionKind = desiredKind;

        if (locomotionData == null)
            return;

        PlayOneShot(locomotionData.movementSound);
        if (locomotionData.loopingVFX != null)
            activeLocomotionVfxInstance = InstantiateAtSocket(locomotionData.loopingVFX, locomotionData.vfxSocketId, true);
    }

    void StopLocomotionFeedback()
    {
        activeLocomotionKind = null;
        if (activeLocomotionVfxInstance != null)
            Destroy(activeLocomotionVfxInstance);
        activeLocomotionVfxInstance = null;
    }

    void StartActionVfx(GameObject prefab, string socketId)
    {
        StopActionVfx();
        if (prefab == null)
            return;

        activeActionVfxInstance = InstantiateAtSocket(prefab, socketId, true);
    }

    void StopActionVfx()
    {
        if (activeActionVfxInstance != null)
            Destroy(activeActionVfxInstance);
        activeActionVfxInstance = null;
    }

    GameObject InstantiateAtSocket(GameObject prefab, string socketId, bool parentToSocket)
    {
        Transform socket = ResolveSocket(socketId);
        if (socket == null)
        {
            return Instantiate(prefab, transform.position, transform.rotation);
        }

        if (parentToSocket)
            return Instantiate(prefab, socket.position, socket.rotation, socket);

        return Instantiate(prefab, socket.position, socket.rotation);
    }

    Transform ResolveSocket(string socketId)
    {
        if (attackPoint != null && string.IsNullOrWhiteSpace(socketId))
            return attackPoint;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == socketId)
                return children[i];
        }

        return attackPoint != null ? attackPoint : transform;
    }

    BoxCollider FindNamedBoxCollider(string socketId)
    {
        BoxCollider[] colliders = GetComponentsInChildren<BoxCollider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].name == socketId)
                return colliders[i];
        }

        return null;
    }

    void ResetActionRuntime()
    {
        attackWindowActive = false;
        parryWindowActive = false;
        avoidWindowOpen = false;
        currentAttackData = null;
        currentParryData = null;
        currentAvoidData = null;
        currentHitData = null;
        chargedAttackActive = false;
        activeAttackAnimationClip = null;
        activeHitAnimationClip = null;
        prepaidAttackData = null;
        currentAttackSpApplied = false;
        attackStartFeedbackPlayed = false;
        pendingHitMoveDirection = Vector3.zero;
        pendingHitMovement = false;
        parryStateMinEndTime = 0f;
        hitTargetsThisAction.Clear();
        parriedBossesThisAction.Clear();
        StopActionMovement();
        StopActionVfx();
    }

    void CancelCurrentAction()
    {
        ResetActionRuntime();
        executionLocked = true;
        chainWindowOpen = false;
        activeChainInputType = null;
        activeLightAttackIndex = -1;
        activeHeavyAttackIndex = -1;
    }

    void StopActionMovement()
    {
        actionMoveDirection = Vector3.zero;
        actionMoveRemainingDistance = 0f;
        actionMoveSpeed = 0f;
    }

    void BeginAttackMovement(CombatAttackData attackData)
    {
        float duration = ResolveEventTime(attackData.animation, "AE_BeginAttackWindow");
        if (duration <= 0f)
            duration = attackData.animation != null ? attackData.animation.length * 0.25f : 0.1f;

        Vector3 direction = ResolveAttackMoveDirection();
        float distance = ResolveAttackMoveDistance(direction, attackData.maxMoveDistance);
        BeginActionMovement(direction, distance, duration);
    }

    void BeginActionMovement(Vector3 direction, float distance, float duration)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f || distance <= 0f || duration <= 0f)
        {
            StopActionMovement();
            return;
        }

        actionMoveDirection = direction.normalized;
        actionMoveRemainingDistance = distance;
        actionMoveSpeed = distance / duration;
    }

    Vector3 ResolveAttackMoveDirection()
    {
        Transform target = playerLockOn != null ? playerLockOn.GetTargetTransform() : null;
        if (target != null)
        {
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
                return toTarget.normalized;
        }

        return transform.forward;
    }

    float ResolveAttackMoveDistance(Vector3 direction, float maxMoveDistance)
    {
        float clampedMoveDistance = Mathf.Max(0f, maxMoveDistance);
        Transform target = playerLockOn != null ? playerLockOn.GetTargetTransform() : null;
        if (target == null)
            return clampedMoveDistance;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float distanceToTarget = toTarget.magnitude;
        if (distanceToTarget <= targetApproachDistance)
            return 0f;

        return Mathf.Min(clampedMoveDistance, distanceToTarget - targetApproachDistance);
    }

    static Vector3 ResolveHitPoint(Collider col, Vector3 fromPoint)
    {
        if (col == null)
            return fromPoint;

        Vector3 hitPoint = col.ClosestPoint(fromPoint);
        if ((hitPoint - fromPoint).sqrMagnitude <= 0.000001f)
            return col.bounds.center;

        return hitPoint;
    }

    static float ResolveEventTime(AnimationClip clip, string eventName)
    {
        if (clip == null)
            return 0f;

        AnimationEvent[] events = clip.events;
        for (int i = 0; i < events.Length; i++)
        {
            if (events[i] != null && events[i].functionName == eventName)
                return Mathf.Max(0f, events[i].time);
        }

        return 0f;
    }

    void PlayHitVFX(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (hitVfxController == null)
            hitVfxController = FindHitVfxController();

        if (hitVfxController == null)
        {
            Debug.LogWarning("[PlayerCombatController] PlayerHitVFXController is missing from the player hierarchy.", this);
            return;
        }

        hitVfxController.PlayHitVFX(hitPoint, hitNormal);
    }

    void ProcessCurrentAttackHitbox()
    {
        if (attackPoint == null || currentAttackData == null)
            return;

        Collider[] hits = Physics.OverlapSphere(
            attackPoint.position,
            Mathf.Max(0f, currentAttackData.hitRadius),
            targetLayers,
            QueryTriggerInteraction.Ignore
        );

        ApplyHits(hits, currentAttackData);
    }

    void ProcessCurrentParryHitbox()
    {
        if (attackPoint == null || currentParryData == null)
            return;

        Collider[] hits = Physics.OverlapSphere(
            attackPoint.position,
            Mathf.Max(0f, currentParryData.hitRadius),
            targetLayers,
            QueryTriggerInteraction.Ignore
        );

        ApplyParryHits(hits);
    }

    void ApplyParryHits(Collider[] hits)
    {
        if (hits == null)
            return;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (col == null || IsOwnCollider(col))
                continue;

            BOSSAI bossAI = col.GetComponentInParent<BOSSAI>();
            if (bossAI == null)
                continue;

            if (parriedBossesThisAction.Contains(bossAI))
                continue;

            if (bossAI.ReceiveParry(gameObject))
            {
                parriedBossesThisAction.Add(bossAI);

                if (stats != null && currentParryData.apGainOnSuccess > 0f)
                    stats.GainAP(currentParryData.apGainOnSuccess);

                PlayOneShot(currentParryData.hitSound);
                StartActionVfx(currentParryData.hitVFX, currentParryData.vfxSocketId);
            }
        }
    }

    void ApplyHits(Collider[] hits, CombatAttackData attackData)
    {
        if (hits == null || attackData == null)
            return;

        bool playedHitAudio = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i];
            if (col == null || IsOwnCollider(col))
                continue;

            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (damageable == null || IsOwnDamageable(damageable))
                continue;

            if (hitTargetsThisAction.Contains(damageable))
                continue;

            hitTargetsThisAction.Add(damageable);
            damageable.TakeDamage(Mathf.Max(0f, attackData.damage), gameObject);

            if (hitVfxController != null && attackPoint != null)
            {
                Vector3 hitPoint = ResolveHitPoint(col, attackPoint.position);
                Vector3 hitNormal = hitPoint - attackPoint.position;
                if (hitNormal.sqrMagnitude <= 0.0001f)
                    hitNormal = col.transform.position - transform.position;

                PlayHitVFX(hitPoint, hitNormal);
            }

            if (stats != null && attackData.apGainPerHit > 0f)
                stats.GainAP(attackData.apGainPerHit);

            if (!playedHitAudio)
            {
                PlayOneShot(attackData.attackHit);
                playedHitAudio = true;
            }
        }
    }

    void SyncStateFromAnimator()
    {
        if (currentState == CombatExecutionState.Attack)
            SyncAttackDataFromAnimator();
        else
        {
            activeAttackAnimationClip = null;
            activeAttackStateHash = 0;
        }

        if (currentState == CombatExecutionState.Hit || currentState == CombatExecutionState.Knockback)
            SyncHitDataFromAnimator();
        else
            activeHitAnimationClip = null;

        if (currentState == CombatExecutionState.Attack && !IsInTaggedAnimation("Attack"))
        {
            ForceReturnToLocomotion();
        }

        if (currentState == CombatExecutionState.Roll && !IsInTaggedAnimation("Roll"))
        {
            ForceReturnToLocomotion();
        }

        if (currentState == CombatExecutionState.Parry && Time.time >= parryStateMinEndTime && !IsInParryAnimation())
        {
            ForceReturnToLocomotion();
        }

        if ((currentState == CombatExecutionState.Hit || currentState == CombatExecutionState.Knockback) && !IsInTaggedAnimation("Hit"))
        {
            ForceReturnToLocomotion();
        }
    }

    void SyncAttackDataFromAnimator()
    {
        AnimationClip activeClip = GetActiveAnimatorClip();
        int activeStateHash = GetCurrentAnimatorStateHash();
        if (activeClip == null || activeStateHash == 0)
            return;

        bool stateChanged = activeStateHash != activeAttackStateHash;
        bool clipChanged = activeClip != activeAttackAnimationClip;
        if (stateChanged || clipChanged)
        {
            activeAttackAnimationClip = activeClip;
            activeAttackStateHash = activeStateHash;

            CombatAttackData resolvedAttackData = ResolveAttackDataForClip(activeClip);
            if (resolvedAttackData == null)
                return;

            bool isSameAttackData = currentAttackData == resolvedAttackData;
            bool isPrepaidAttack = prepaidAttackData == resolvedAttackData;

            if (!isSameAttackData || stateChanged)
                currentAttackSpApplied = false;

            if (stateChanged)
                attackStartFeedbackPlayed = false;

            currentAttackData = resolvedAttackData;
            currentAvoidData = null;
            chargedAttackActive = combatConfig != null &&
                                  combatConfig.chargedAttack != null &&
                                  combatConfig.chargedAttack.attack == resolvedAttackData;

            if (!currentAttackSpApplied)
            {
                if (!isPrepaidAttack)
                    TrySpendSP(resolvedAttackData.spCost);

                currentAttackSpApplied = true;
            }

            prepaidAttackData = null;

            FaceLockTarget();
            BeginAttackMovement(resolvedAttackData);
        }

    }

    int GetCurrentAnimatorStateHash()
    {
        if (animator == null)
            return 0;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (!current.IsTag("Attack"))
            return 0;

        return current.fullPathHash != 0 ? current.fullPathHash : current.shortNameHash;
    }

    void PlayAttackStartFeedback()
    {
        if (attackStartFeedbackPlayed || currentAttackData == null)
            return;

        PlayOneShot(currentAttackData.attackVoice);
        PlayOneShot(currentAttackData.attackSwing);
        attackStartFeedbackPlayed = true;
    }

    void SyncHitDataFromAnimator()
    {
        AnimationClip activeClip = GetActiveAnimatorClip();
        if (activeClip == null || activeClip == activeHitAnimationClip)
            return;

        activeHitAnimationClip = activeClip;
        currentHitData = ResolveHitDataForCurrentAnimator(activeClip);
        if (currentHitData == null)
        {
            pendingHitMovement = false;
            StopActionMovement();
            Debug.Log("[PlayerCombatController] Missing hit config for animation clip/state: " + activeClip.name);
            return;
        }

        if (!pendingHitMovement)
            return;

        float duration = currentHitData.transitionDuration > 0f
            ? currentHitData.transitionDuration
            : (currentHitData.animation != null ? currentHitData.animation.length : 0.1f);

        BeginActionMovement(pendingHitMoveDirection, Mathf.Max(0f, currentHitData.moveDistance), duration);
        pendingHitMovement = false;
    }

    AnimationClip GetActiveAnimatorClip()
    {
        if (animator == null)
            return null;

        AnimatorClipInfo[] currentClips = animator.GetCurrentAnimatorClipInfo(0);
        for (int i = 0; i < currentClips.Length; i++)
        {
            if (currentClips[i].clip != null)
                return currentClips[i].clip;
        }

        AnimatorClipInfo[] nextClips = animator.GetNextAnimatorClipInfo(0);
        for (int i = 0; i < nextClips.Length; i++)
        {
            if (nextClips[i].clip != null)
                return nextClips[i].clip;
        }

        return null;
    }

    CombatAttackData ResolveAttackDataForClip(AnimationClip clip)
    {
        if (clip == null || combatConfig == null)
            return null;

        CombatAttackData attackData = FindAttackDataByClip(combatConfig.lightAttacks, clip);
        if (attackData != null)
            return attackData;

        attackData = FindAttackDataByClip(combatConfig.heavyAttacks, clip);
        if (attackData != null)
            return attackData;

        if (combatConfig.sprintAttack != null && combatConfig.sprintAttack.animation == clip)
            return combatConfig.sprintAttack;

        if (combatConfig.chargedAttack != null &&
            combatConfig.chargedAttack.attack != null &&
            combatConfig.chargedAttack.attack.animation == clip)
        {
            return combatConfig.chargedAttack.attack;
        }

        attackData = FindAttackDataByName(combatConfig.lightAttacks, clip.name);
        if (attackData != null)
            return attackData;

        attackData = FindAttackDataByName(combatConfig.heavyAttacks, clip.name);
        if (attackData != null)
            return attackData;

        if (combatConfig.sprintAttack != null &&
            string.Equals(combatConfig.sprintAttack.animatorStateName, clip.name, StringComparison.OrdinalIgnoreCase))
        {
            return combatConfig.sprintAttack;
        }

        if (combatConfig.chargedAttack != null &&
            combatConfig.chargedAttack.attack != null &&
            string.Equals(combatConfig.chargedAttack.attack.animatorStateName, clip.name, StringComparison.OrdinalIgnoreCase))
        {
            return combatConfig.chargedAttack.attack;
        }

        return null;
    }

    CombatHitData ResolveHitDataForCurrentAnimator(AnimationClip clip)
    {
        if (combatConfig == null)
            return null;

        bool preferBigHit = currentState == CombatExecutionState.Knockback;
        CombatHitData hitData = FindHitData(combatConfig.bigHits, clip, preferStateMatch: preferBigHit);
        if (hitData != null && preferBigHit)
            return hitData;

        hitData = FindHitData(combatConfig.smallHits, clip, preferStateMatch: !preferBigHit);
        if (hitData != null && !preferBigHit)
            return hitData;

        if (preferBigHit)
            return FindHitData(combatConfig.smallHits, clip, preferStateMatch: false);

        return FindHitData(combatConfig.bigHits, clip, preferStateMatch: false);
    }

    static CombatAttackData FindAttackDataByClip(List<CombatAttackData> attackList, AnimationClip clip)
    {
        if (attackList == null || clip == null)
            return null;

        for (int i = 0; i < attackList.Count; i++)
        {
            CombatAttackData attackData = attackList[i];
            if (attackData != null && attackData.animation == clip)
                return attackData;
        }

        return null;
    }

    CombatHitData FindHitData(List<CombatHitData> hitList, AnimationClip clip, bool preferStateMatch)
    {
        if (hitList == null || hitList.Count == 0)
            return null;

        if (preferStateMatch)
        {
            for (int i = 0; i < hitList.Count; i++)
            {
                CombatHitData hitData = hitList[i];
                if (hitData == null || string.IsNullOrWhiteSpace(hitData.animatorStateName))
                    continue;

                if (IsAnimatorStateMatch(hitData.animatorStateName))
                    return hitData;
            }
        }

        if (clip != null)
        {
            for (int i = 0; i < hitList.Count; i++)
            {
                CombatHitData hitData = hitList[i];
                if (hitData != null && hitData.animation == clip)
                    return hitData;
            }

            for (int i = 0; i < hitList.Count; i++)
            {
                CombatHitData hitData = hitList[i];
                if (hitData == null || string.IsNullOrWhiteSpace(hitData.animatorStateName))
                    continue;

                if (string.Equals(hitData.animatorStateName, clip.name, StringComparison.OrdinalIgnoreCase))
                    return hitData;
            }
        }

        if (!preferStateMatch)
        {
            for (int i = 0; i < hitList.Count; i++)
            {
                CombatHitData hitData = hitList[i];
                if (hitData == null || string.IsNullOrWhiteSpace(hitData.animatorStateName))
                    continue;

                if (IsAnimatorStateMatch(hitData.animatorStateName))
                    return hitData;
            }
        }

        return null;
    }

    static CombatAttackData FindAttackDataByName(List<CombatAttackData> attackList, string clipName)
    {
        if (attackList == null || string.IsNullOrWhiteSpace(clipName))
            return null;

        for (int i = 0; i < attackList.Count; i++)
        {
            CombatAttackData attackData = attackList[i];
            if (attackData == null || string.IsNullOrWhiteSpace(attackData.animatorStateName))
                continue;

            if (string.Equals(attackData.animatorStateName, clipName, StringComparison.OrdinalIgnoreCase))
                return attackData;
        }

        return null;
    }

    void ForceReturnToLocomotion()
    {
        ResetActionRuntime();
        executionLocked = false;
        chainWindowOpen = false;
        activeChainInputType = null;
        activeLightAttackIndex = -1;
        activeHeavyAttackIndex = -1;
        currentState = IsLockedOn() ? CombatExecutionState.LockOnLocomotion : CombatExecutionState.Locomotion;
    }

    bool IsInTaggedAnimation(string tagName)
    {
        if (animator == null)
            return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            return current.IsTag(tagName) || next.IsTag(tagName);
        }

        return current.IsTag(tagName);
    }

    bool IsInParryAnimation()
    {
        AnimationClip activeClip = GetActiveAnimatorClip();
        return IsInTaggedAnimation("Parry")
            || (currentParryData != null && IsAnimatorStateMatch(currentParryData.animatorStateName))
            || (currentParryData != null && currentParryData.animation != null && activeClip == currentParryData.animation);
    }

    bool IsAnimatorStateMatch(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return false;

        int stateHash = Animator.StringToHash(stateName);
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.shortNameHash == stateHash || current.fullPathHash == stateHash || current.IsName(stateName))
            return true;

        if (!animator.IsInTransition(0))
            return false;

        AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
        return next.shortNameHash == stateHash || next.fullPathHash == stateHash || next.IsName(stateName);
    }

    void FaceLockTarget()
    {
        Transform target = playerLockOn != null ? playerLockOn.GetTargetTransform() : null;
        if (target == null)
            return;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
    }

    bool IsLockedOn()
    {
        return playerLockOn != null && playerLockOn.HasTarget();
    }

    Vector3 GetNormalizedWorldInput(float inputX, float inputY)
    {
        Vector3 input = new Vector3(inputX, 0f, inputY);
        if (input.sqrMagnitude > 1f)
            input.Normalize();

        return input;
    }

    void FaceTarget(Transform target, float rotSpeed)
    {
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotSpeed * Time.deltaTime);
    }

    void ApplyGravity(ref Vector3 move)
    {
        if (characterController.isGrounded)
            verticalVelocity = groundedStickForce;
        else
            verticalVelocity += gravity * Time.deltaTime;

        move.y = verticalVelocity;
    }

    RollDirection DetermineAvoidDirection()
    {
        if (!IsLockedOn())
            return RollDirection.Front;

        Vector2 moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector3 velocity = characterController != null ? characterController.velocity : Vector3.zero;

        if (moveInput.sqrMagnitude > 0.04f)
            return ResolveWorldRollDirection(moveInput.x, moveInput.y);

        if (new Vector2(velocity.x, velocity.z).sqrMagnitude > 0.04f)
            return ResolveWorldRollDirection(velocity.x, velocity.z);

        return RollDirection.Back;
    }

    Vector3 GetWorldDirection(RollDirection direction)
    {
        if (IsLockedOn())
        {
            switch (direction)
            {
                case RollDirection.Back:
                    return Vector3.back;
                case RollDirection.Left:
                    return Vector3.left;
                case RollDirection.Right:
                    return Vector3.right;
                default:
                    return Vector3.forward;
            }
        }

        switch (direction)
        {
            case RollDirection.Back:
                return -transform.forward;
            case RollDirection.Left:
                return -transform.right;
            case RollDirection.Right:
                return transform.right;
            default:
                return transform.forward;
        }
    }

    static RollDirection ResolveWorldRollDirection(float x, float y)
    {
        if (y < -0.2f)
            return RollDirection.Back;

        if (Mathf.Abs(y) >= Mathf.Abs(x))
            return y >= 0f ? RollDirection.Front : RollDirection.Back;

        return x >= 0f ? RollDirection.Right : RollDirection.Left;
    }

    Vector3 ResolveHitMoveDirection(GameObject attacker)
    {
        if (attacker == null)
            return -transform.forward;

        Transform attackerRoot = attacker.transform.root != null ? attacker.transform.root : attacker.transform;
        Vector3 direction = transform.position - attackerRoot.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return -transform.forward;

        return direction.normalized;
    }

    bool TrySpendSP(float amount)
    {
        if (stats == null)
            return true;

        return stats.SpendSP(Mathf.Max(0f, amount));
    }

    void UpdateSPRecovery()
    {
        if (stats == null || isDead)
            return;

        bool allowRecovery = currentState == CombatExecutionState.Locomotion ||
                             currentState == CombatExecutionState.LockOnLocomotion;

        stats.UpdateSPRecovery(allowRecovery, Time.deltaTime);
    }

    void UpdateSprintSPDrain(Vector2 moveInput)
    {
        if (stats == null || isDead)
            return;

        bool inLocomotion = currentState == CombatExecutionState.Locomotion ||
                            currentState == CombatExecutionState.LockOnLocomotion;
        bool isMoving = moveInput.sqrMagnitude > 0.01f && currentSpeed > 0.01f;
        bool shouldDrain = sprintMode && inLocomotion && isMoving;

        if (stats.UpdateSprintSPDrain(shouldDrain, Time.deltaTime))
            return;

        sprintMode = false;
    }

    int ResolveIncomingPriority(GameObject attacker)
    {
        if (attacker == null)
            return combatConfig != null ? Mathf.Max(1, combatConfig.locomotionPriority) : 1;

        MonoBehaviour[] components = attacker.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] is ICombatPrioritySource prioritySource)
                return Mathf.Max(1, prioritySource.GetCombatPriority());
        }

        return combatConfig != null ? Mathf.Max(1, combatConfig.locomotionPriority) : 1;
    }

    void PlayOneShot(AudioClip clip)
    {
        if (clip == null)
            return;

        AudioSource targetSource = combatAudioController != null && combatAudioController.audioSource != null
            ? combatAudioController.audioSource
            : audioSource;

        if (targetSource == null)
            return;

        targetSource.PlayOneShot(clip);
    }

    bool TrySetTrigger(string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName) || !HasTriggerParameter(triggerName))
            return false;

        animator.ResetTrigger(triggerName);
        animator.SetTrigger(triggerName);
        return true;
    }

    bool HasTriggerParameter(string paramName)
    {
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == paramName && parameters[i].type == AnimatorControllerParameterType.Trigger)
                return true;
        }

        return false;
    }

    bool HasBoolParameter(string paramName)
    {
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == paramName && parameters[i].type == AnimatorControllerParameterType.Bool)
                return true;
        }

        return false;
    }

    bool HasIntParameter(string paramName)
    {
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == paramName && parameters[i].type == AnimatorControllerParameterType.Int)
                return true;
        }

        return false;
    }

    bool HasFloatParameter(string paramName)
    {
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == paramName && parameters[i].type == AnimatorControllerParameterType.Float)
                return true;
        }

        return false;
    }

    bool IsOwnCollider(Collider col)
    {
        return col != null && col.transform.root == transform.root;
    }

    bool IsOwnDamageable(IDamageable damageable)
    {
        Component component = damageable as Component;
        return component != null && component.transform.root == transform.root;
    }
}
