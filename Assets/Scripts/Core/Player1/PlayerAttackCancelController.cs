using UnityEngine;

public class PlayerAttackCancelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Input")]
    [SerializeField] private string horizontalAxis = "Horizontal";
    [SerializeField] private string verticalAxis = "Vertical";
    [SerializeField] private float moveInputThreshold = 0.15f;

    [Header("Animator Params")]
    [SerializeField] private string isAttackingParam = "IsAttacking";
    [SerializeField] private string interruptAttackParam = "InterruptAttack";
    [SerializeField] private string queueNextAttackParam = "QueueNextAttack";

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    private bool inAttackState = false;
    private bool moveCancelWindowOpen = false;
    private bool interruptedThisAttack = false;

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (animator == null) return;

        if (!inAttackState) return;
        if (!moveCancelWindowOpen) return;
        if (interruptedThisAttack) return;

        Vector2 moveInput = new Vector2(
            Input.GetAxisRaw(horizontalAxis),
            Input.GetAxisRaw(verticalAxis)
        );

        bool hasMoveInput = moveInput.sqrMagnitude > (moveInputThreshold * moveInputThreshold);

        if (hasMoveInput)
        {
            InterruptCurrentAttack();
        }
    }

    private void InterruptCurrentAttack()
    {
        interruptedThisAttack = true;
        moveCancelWindowOpen = false;

        // 清掉可能残留的连段输入，避免退出攻击后又被带回攻击链
        animator.SetBool(queueNextAttackParam, false);

        // 触发从攻击状态退出
        animator.SetTrigger(interruptAttackParam);

        if (debugLog)
            Debug.Log("[AttackCancel] Interrupt attack by movement input.");
    }

    // ===== 这些函数由动画事件调用 =====

    /// <summary>
    /// 在攻击开始帧或攻击状态进入时调用
    /// </summary>
    public void NotifyAttackStarted()
    {
        inAttackState = true;
        moveCancelWindowOpen = false;
        interruptedThisAttack = false;

        animator.SetBool(isAttackingParam, true);

        if (debugLog)
            Debug.Log("[AttackCancel] Attack started.");
    }

    /// <summary>
    /// 在 AttackHit 之后的动画事件调用，打开移动取消窗口
    /// </summary>
    public void EnableMoveCancel()
    {
        if (!inAttackState) return;

        moveCancelWindowOpen = true;

        if (debugLog)
            Debug.Log("[AttackCancel] Move cancel window OPEN.");
    }

    /// <summary>
    /// 在攻击动画末尾调用，关闭取消窗口
    /// </summary>
    public void DisableMoveCancel()
    {
        moveCancelWindowOpen = false;

        if (debugLog)
            Debug.Log("[AttackCancel] Move cancel window CLOSED.");
    }

    /// <summary>
    /// 在攻击动画结束时调用
    /// </summary>
    public void NotifyAttackEnded()
    {
        inAttackState = false;
        moveCancelWindowOpen = false;
        interruptedThisAttack = false;

        animator.SetBool(isAttackingParam, false);
        animator.SetBool(queueNextAttackParam, false);

        if (debugLog)
            Debug.Log("[AttackCancel] Attack ended.");
    }

    // ===== 可选：给其他脚本读取 =====
    public bool IsInAttackState => inAttackState;
    public bool CanMoveCancel => moveCancelWindowOpen;
}