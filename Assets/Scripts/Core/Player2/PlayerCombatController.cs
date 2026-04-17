using System.Collections.Generic;
using UnityEngine;

public class PlayerCombatController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController legacyPlayerController;

    [Header("Input")]
    [SerializeField] private KeyCode lightAttackKey = KeyCode.Mouse0;
    [SerializeField] private KeyCode heavyAttackKey = KeyCode.Mouse1;

    [Header("Buffer")]
    [SerializeField] private float bufferWindowSeconds = 1.2f;
    [SerializeField] private int maxStoredInputs = 10;
    [SerializeField] private CombatInputBuffer inputBuffer = new CombatInputBuffer();

    [Header("Combos")]
    [SerializeField] private bool seedDefaultCombosOnReset = true;
    [SerializeField] private List<ComboDefinition> comboDefinitions = new List<ComboDefinition>();
    [SerializeField] private ComboExecutor comboExecutor = new ComboExecutor();

    [Header("Debug")]
    [SerializeField] private bool debugInputLog = false;
    [SerializeField] private bool debugStateLog = false;

    [Header("Runtime")]
    [SerializeField] private CombatExecutionState currentState = CombatExecutionState.Locomotion;
    [SerializeField] private bool chainWindowOpen = false;

    private bool executionLocked = false;

    public CombatExecutionState CurrentState => currentState;
    public bool IsChainWindowOpen => chainWindowOpen;
    public IReadOnlyList<CombatBufferedInput> BufferedInputs => inputBuffer.History;
    public IReadOnlyList<CombatBufferedInput> PendingInputs => inputBuffer.PendingInputs;

    void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        legacyPlayerController = GetComponent<PlayerController>();

        if (seedDefaultCombosOnReset && (comboDefinitions == null || comboDefinitions.Count == 0))
            comboDefinitions = CreateDefaultCombos();
    }

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (legacyPlayerController == null)
            legacyPlayerController = GetComponent<PlayerController>();

        inputBuffer.Configure(bufferWindowSeconds, maxStoredInputs);
        comboExecutor.ResetRuntimeState();
        SyncExecutionLockForState();
    }

    void OnValidate()
    {
        if (inputBuffer == null)
            inputBuffer = new CombatInputBuffer();

        inputBuffer.Configure(bufferWindowSeconds, maxStoredInputs);

        if (comboDefinitions == null)
            comboDefinitions = new List<ComboDefinition>();
    }

    void Update()
    {
        float currentTime = Time.time;
        inputBuffer.Configure(bufferWindowSeconds, maxStoredInputs);
        inputBuffer.Prune(currentTime);

        CaptureCombatInput(currentTime);
        TryExecuteBufferedInput();
    }

    public void SetCombatState(CombatExecutionState newState)
    {
        CombatExecutionState previousState = currentState;
        currentState = newState;

        if (newState != CombatExecutionState.Attack)
            chainWindowOpen = false;

        bool sequenceFinished =
            previousState == CombatExecutionState.Attack &&
            (newState == CombatExecutionState.Locomotion || newState == CombatExecutionState.LockOnLocomotion);

        if (sequenceFinished)
        {
            inputBuffer.ClearAll();
            comboExecutor.ResetRuntimeState();
        }

        if (newState == CombatExecutionState.Dead)
        {
            inputBuffer.ClearAll();
            comboExecutor.ResetRuntimeState();
        }

        SyncExecutionLockForState();

        if (debugStateLog)
            Debug.Log("[PlayerCombatController] State -> " + currentState);

        if (CanExecuteNow())
            TryExecuteBufferedInput();
    }

    public void OpenChainWindow()
    {
        currentState = CombatExecutionState.Attack;
        chainWindowOpen = true;
        executionLocked = false;

        if (debugStateLog)
            Debug.Log("[PlayerCombatController] Chain window OPEN.");

        TryExecuteBufferedInput();
    }

    public void CloseChainWindow()
    {
        chainWindowOpen = false;

        if (currentState == CombatExecutionState.Attack)
            executionLocked = true;

        if (debugStateLog)
            Debug.Log("[PlayerCombatController] Chain window CLOSED.");
    }

    public void ClearBufferedInputs()
    {
        inputBuffer.ClearAll();
        comboExecutor.ResetRuntimeState();
        SyncExecutionLockForState();
    }

    public void AE_OpenChainWindow()
    {
        OpenChainWindow();
    }

    public void AE_CloseChainWindow()
    {
        CloseChainWindow();
    }

    public void AE_StateLocomotion()
    {
        SetCombatState(CombatExecutionState.Locomotion);
    }

    public void AE_StateLockOnLocomotion()
    {
        SetCombatState(CombatExecutionState.LockOnLocomotion);
    }

    public void AE_StateAttack()
    {
        SetCombatState(CombatExecutionState.Attack);
    }

    public void AE_StateHit()
    {
        SetCombatState(CombatExecutionState.Hit);
    }

    public void AE_StateRoll()
    {
        SetCombatState(CombatExecutionState.Roll);
    }

    public void AE_StateKnockback()
    {
        SetCombatState(CombatExecutionState.Knockback);
    }

    public void AE_StateDead()
    {
        SetCombatState(CombatExecutionState.Dead);
    }

    public void AE_ClearBuffer()
    {
        ClearBufferedInputs();
    }

    public string GetBufferedInputDebugString()
    {
        IReadOnlyList<CombatBufferedInput> history = inputBuffer.History;
        if (history.Count == 0)
            return "(empty)";

        System.Text.StringBuilder builder = new System.Text.StringBuilder(history.Count * 2);
        for (int i = 0; i < history.Count; i++)
            builder.Append(history[i].inputType);

        return builder.ToString();
    }

    void CaptureCombatInput(float currentTime)
    {
        if (Input.GetKeyDown(lightAttackKey))
            RecordInput(CombatInputType.X, currentTime);

        if (Input.GetKeyDown(heavyAttackKey))
            RecordInput(CombatInputType.Y, currentTime);
    }

    void RecordInput(CombatInputType inputType, float currentTime)
    {
        inputBuffer.RecordInput(inputType, currentTime);

        if (debugInputLog)
            Debug.Log("[PlayerCombatController] Buffered input: " + inputType + " | " + GetBufferedInputDebugString());
    }

    void TryExecuteBufferedInput()
    {
        if (!CanExecuteNow())
            return;

        if (executionLocked)
            return;

        bool allowFallbackAttacks = !UsesLegacyNormalAttackFlow();
        if (!comboExecutor.TryExecute(animator, inputBuffer, comboDefinitions, allowFallbackAttacks, out string executedAction))
            return;

        executionLocked = true;

        if (debugStateLog)
            Debug.Log("[PlayerCombatController] Executed action: " + executedAction);
    }

    bool UsesLegacyNormalAttackFlow()
    {
        return legacyPlayerController != null && legacyPlayerController.enabled;
    }

    bool CanExecuteNow()
    {
        switch (currentState)
        {
            case CombatExecutionState.Locomotion:
            case CombatExecutionState.LockOnLocomotion:
                return true;

            case CombatExecutionState.Attack:
                return chainWindowOpen;

            default:
                return false;
        }
    }

    void SyncExecutionLockForState()
    {
        switch (currentState)
        {
            case CombatExecutionState.Locomotion:
            case CombatExecutionState.LockOnLocomotion:
                executionLocked = false;
                break;

            case CombatExecutionState.Attack:
                executionLocked = !chainWindowOpen;
                break;

            default:
                executionLocked = true;
                break;
        }
    }

    static List<ComboDefinition> CreateDefaultCombos()
    {
        return new List<ComboDefinition>
        {
            new ComboDefinition("XXXX", "ComboXXXX", 0.35f, 400, CombatInputType.X, CombatInputType.X, CombatInputType.X, CombatInputType.X),
            new ComboDefinition("YYYY", "ComboYYYY", 0.35f, 390, CombatInputType.Y, CombatInputType.Y, CombatInputType.Y, CombatInputType.Y),
            new ComboDefinition("XYXY", "ComboXYXY", 0.35f, 380, CombatInputType.X, CombatInputType.Y, CombatInputType.X, CombatInputType.Y),
            new ComboDefinition("XXYY", "ComboXXYY", 0.35f, 370, CombatInputType.X, CombatInputType.X, CombatInputType.Y, CombatInputType.Y)
        };
    }
}
