using UnityEngine;

public class PlayerLockOnMovement : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public CharacterController characterController;
    public PlayerLockOn playerLockOn;

    [Header("Movement")]
    public float moveSpeed = 3.5f;
    public float backwardSpeedMultiplier = 0.85f;
    public float rotationSpeed = 12f;
    public float inputSmoothTime = 0.08f;

    [Header("Animator Params")]
    public string isLockedOnParam = "IsLockedOn";
    public string moveXParam = "MoveX";
    public string moveYParam = "MoveY";

    [Header("Debug")]
    public bool useLockOnMovement = true;

    private float currentMoveX;
    private float currentMoveY;
    private float moveXVelocity;
    private float moveYVelocity;

    void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        characterController = GetComponent<CharacterController>();
        playerLockOn = GetComponent<PlayerLockOn>();
    }

    void Update()
    {
        if (!useLockOnMovement)
            return;

        bool hasLockTarget = playerLockOn != null && playerLockOn.HasTarget();
        if (animator != null)
        {
            animator.SetBool(isLockedOnParam, hasLockTarget);
        }

        if (!hasLockTarget)
        {
            ResetMoveParams();
            return;
        }

        Transform target = playerLockOn.GetTargetTransform();
        if (target == null)
        {
            ResetMoveParams();
            return;
        }

        FaceTarget(target);
        HandleLockedMovement(target);
    }

    void FaceTarget(Transform target)
    {
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    void HandleLockedMovement(Transform target)
    {
        float inputX = Input.GetAxisRaw("Horizontal"); // A/D
        float inputY = Input.GetAxisRaw("Vertical");   // W/S

        // 锁敌下的局部方向：
        // forward = 面向目标方向
        // right   = 面向目标后的右侧方向
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        Vector3 move = (right * inputX + forward * inputY);
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        float speedMultiplier = 1f;
        if (inputY < 0f)
        {
            speedMultiplier = backwardSpeedMultiplier;
        }

        if (characterController != null)
        {
            characterController.Move(move * (moveSpeed * speedMultiplier) * Time.deltaTime);
        }
        else
        {
            transform.position += move * (moveSpeed * speedMultiplier) * Time.deltaTime;
        }

        // Animator 参数
        float targetMoveX = inputX;
        float targetMoveY = inputY;

        currentMoveX = Mathf.SmoothDamp(currentMoveX, targetMoveX, ref moveXVelocity, inputSmoothTime);
        currentMoveY = Mathf.SmoothDamp(currentMoveY, targetMoveY, ref moveYVelocity, inputSmoothTime);

        if (animator != null)
        {
            animator.SetFloat(moveXParam, currentMoveX);
            animator.SetFloat(moveYParam, currentMoveY);
        }
    }

    void ResetMoveParams()
    {
        currentMoveX = Mathf.SmoothDamp(currentMoveX, 0f, ref moveXVelocity, inputSmoothTime);
        currentMoveY = Mathf.SmoothDamp(currentMoveY, 0f, ref moveYVelocity, inputSmoothTime);

        if (animator != null)
        {
            animator.SetFloat(moveXParam, currentMoveX);
            animator.SetFloat(moveYParam, currentMoveY);
        }
    }
}