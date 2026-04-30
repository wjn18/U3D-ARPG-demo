using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
public class BossAnimatorController : MonoBehaviour
{
    public enum AttackMotionState
    {
        None,
        AligningBeforeAttack,
        InRootMotionAttack
    }

    [Header("Refs")]
    public Transform target;
    public NavMeshAgent agent;

    [Header("Animator Params")]
    public string moveXParam = "MoveX";
    public string moveYParam = "MoveY";
    public string attackTriggerParam = "AttackTrigger";
    public string avoidTriggerParam = "AvoidTrigger";
    public string deathTriggerParam = "DeathTrigger";
    public string attackIndexParam = "AttackIndex";

    [Header("State Names")]
    public string avoidStateName = "avoid_back";
    public string kneelStateName = "Kneel Down";
    public string kneelIdleStateName = "Kneel Idle";
    public string standStateName = "Stand";
    public string hitReactionStateName = "Standing React Large Gut";
    public string hitSmallReactionStateName = "Standing React Large From Left";
    public string hitBigReactionStateName = "Standing React Large From Left";
    public string parryReactionStateName = "hit_parried";

    [Header("Facing")]
    public bool alwaysFaceTarget = true;
    public float rotateSpeed = 12f;
    public float faceStopDistance = 0.05f;

    [Header("Attack Facing")]
    public float preAttackFaceAngle = 10f;
    public float preAttackRotateSpeed = 18f;
    public float maxAlignTime = 0.4f;

    [Header("Animation")]
    public float moveDampTime = 0.08f;
    public float maxMoveSpeed = 3.5f;
    public bool clampInput = true;
    public bool useDeltaPositionFallback = true;

    private Animator anim;
    private Vector3 lastPosition;
    private bool isDead;

    private AttackMotionState attackMotionState = AttackMotionState.None;
    private BossAttackDefinition activeAttack;
    private int pendingAttackIndex = -1;
    private float alignTimer = 0f;
    private bool attackTriggered = false;
    private BossAttackPhase attackPhase = BossAttackPhase.None;
    private Vector3 attackStartForward = Vector3.forward;
    private Vector3 attackLockedForward = Vector3.forward;
    private bool attackDirectionLocked = true;

    public Animator Animator => anim;
    public bool IsAligningBeforeAttack => attackMotionState == AttackMotionState.AligningBeforeAttack;
    public bool IsInRootMotionAttack => attackMotionState == AttackMotionState.InRootMotionAttack;
    public bool HasPendingAttackTrigger => attackTriggered && attackMotionState == AttackMotionState.None;
    public bool IsBusyWithAttackMotion => IsAligningBeforeAttack || IsInRootMotionAttack || HasPendingAttackTrigger;
    public BossAttackDefinition ActiveAttack => activeAttack;
    public BossAttackPhase CurrentAttackPhase => attackPhase;

    void Awake()
    {
        anim = GetComponent<Animator>();

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        lastPosition = transform.position;

        if (agent != null)
        {
            agent.updateRotation = false;
            agent.updatePosition = true;
        }

        anim.applyRootMotion = false;
    }

    void Update()
    {
        if (anim == null || isDead)
            return;

        UpdateAttackMotionState();
        UpdateAttackFacing();

        if (!IsBusyWithAttackMotion && target != null && ShouldFaceTargetNormally())
            FaceTargetNormally();

        UpdateMoveParams();
        lastPosition = transform.position;
    }

    void UpdateAttackMotionState()
    {
        if (attackMotionState == AttackMotionState.AligningBeforeAttack)
        {
            UpdatePreAttackAlignment();
            return;
        }

        if (attackMotionState == AttackMotionState.InRootMotionAttack)
        {
            if (!IsInAttackState())
                EndRootMotionAttack();
            return;
        }

        if (IsInAttackState() && attackTriggered)
            BeginRootMotionAttack();
    }

    void UpdateAttackFacing()
    {
        if (attackMotionState != AttackMotionState.InRootMotionAttack)
            return;

        if (activeAttack == null)
            return;

        if (!activeAttack.trackTargetUntilDirectionLock)
            return;

        Vector3 desiredForward = attackDirectionLocked
            ? attackLockedForward
            : GetClampedDirectionToTarget();

        if (desiredForward.sqrMagnitude < 0.0001f)
            return;

        Quaternion desiredRotation = Quaternion.LookRotation(desiredForward, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            desiredRotation,
            Mathf.Max(0f, activeAttack.turnSpeed) * Time.deltaTime
        );
    }

    bool ShouldFaceTargetNormally()
    {
        if (!alwaysFaceTarget)
            return false;

        if (IsInDeathState())
            return false;

        if (IsInAttackState())
            return false;

        if (IsInAvoidState())
            return false;

        if (IsInKneelLikeState())
            return false;

        return true;
    }

    void FaceTargetNormally()
    {
        Vector3 toTarget = GetPlanarDirectionToTarget();
        if (toTarget.sqrMagnitude < faceStopDistance * faceStopDistance)
            return;

        Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
    }

    void UpdatePreAttackAlignment()
    {
        if (target == null)
        {
            TriggerPendingAttackImmediately();
            return;
        }

        alignTimer += Time.deltaTime;

        Vector3 toTarget = GetPlanarDirectionToTarget();
        if (toTarget.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                preAttackRotateSpeed * Time.deltaTime
            );
        }

        float angle = GetAngleToTarget();
        if (angle <= preAttackFaceAngle || alignTimer >= maxAlignTime)
            TriggerPendingAttackImmediately();
    }

    float GetAngleToTarget()
    {
        Vector3 toTarget = GetPlanarDirectionToTarget();
        if (toTarget.sqrMagnitude < 0.0001f)
            return 0f;

        return Vector3.Angle(GetPlanarForward(), toTarget.normalized);
    }

    void UpdateMoveParams()
    {
        Vector3 worldVelocity = GetWorldVelocity();
        worldVelocity.y = 0f;

        Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);

        float moveX = 0f;
        float moveY = 0f;

        if (maxMoveSpeed > 0.001f)
        {
            moveX = localVelocity.x / maxMoveSpeed;
            moveY = localVelocity.z / maxMoveSpeed;
        }

        if (clampInput)
        {
            moveX = Mathf.Clamp(moveX, -1f, 1f);
            moveY = Mathf.Clamp(moveY, -1f, 1f);
        }

        anim.SetFloat(moveXParam, moveX, moveDampTime, Time.deltaTime);
        anim.SetFloat(moveYParam, moveY, moveDampTime, Time.deltaTime);
    }

    Vector3 GetWorldVelocity()
    {
        if (agent != null && agent.enabled && attackMotionState != AttackMotionState.InRootMotionAttack)
            return agent.velocity;

        if (useDeltaPositionFallback)
        {
            Vector3 delta = transform.position - lastPosition;
            return delta / Mathf.Max(Time.deltaTime, 0.0001f);
        }

        return Vector3.zero;
    }

    public void RequestAttack(BossAttackDefinition attack)
    {
        if (isDead || attack == null)
            return;

        if (IsInKneelLikeState())
            return;

        activeAttack = attack;
        pendingAttackIndex = attack.attackIndex;
        attackPhase = BossAttackPhase.Startup;
        attackTriggered = false;
        attackStartForward = GetPlanarForward();
        attackLockedForward = attackStartForward;
        attackDirectionLocked = !attack.trackTargetUntilDirectionLock;

        if (attack.alignBeforeAttack && target != null)
        {
            attackMotionState = AttackMotionState.AligningBeforeAttack;
            alignTimer = 0f;
            StopAgentForAttack();
        }
        else
        {
            TriggerPendingAttackImmediately();
        }
    }

    void TriggerPendingAttackImmediately()
    {
        if (pendingAttackIndex < 0 || isDead)
            return;

        if (IsInKneelLikeState())
        {
            ResetAttackData();
            return;
        }

        attackMotionState = AttackMotionState.None;
        alignTimer = 0f;

        anim.SetInteger(attackIndexParam, pendingAttackIndex);
        anim.SetTrigger(attackTriggerParam);

        attackTriggered = true;
        pendingAttackIndex = -1;
    }

    void BeginRootMotionAttack()
    {
        attackMotionState = AttackMotionState.InRootMotionAttack;
        StopAgentForAttack();
        anim.applyRootMotion = ShouldApplyRootMotionPosition();
    }

    void EndRootMotionAttack()
    {
        anim.applyRootMotion = false;

        if (agent != null && agent.enabled)
        {
            agent.updatePosition = true;
            agent.updateRotation = false;
            agent.Warp(transform.position);
            agent.isStopped = true;
        }

        ResetAttackData();
    }

    void ResetAttackData()
    {
        attackMotionState = AttackMotionState.None;
        activeAttack = null;
        pendingAttackIndex = -1;
        alignTimer = 0f;
        attackTriggered = false;
        attackPhase = BossAttackPhase.None;
        attackDirectionLocked = true;
        attackStartForward = GetPlanarForward();
        attackLockedForward = attackStartForward;
    }

    void StopAgentForAttack()
    {
        if (agent == null || !agent.enabled)
            return;

        agent.isStopped = true;
        agent.ResetPath();
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.velocity = Vector3.zero;
    }

    public void SyncAttackPhase(BossAttackPhase phase)
    {
        attackPhase = phase;

        if (activeAttack == null)
            return;

        if (!activeAttack.trackTargetUntilDirectionLock)
            return;

        if (!activeAttack.requireDirectionLockEvent && phase == BossAttackPhase.Active)
            LockCurrentAttackDirection();
    }

    public void AbortAttackMotion()
    {
        anim.applyRootMotion = false;

        if (agent != null && agent.enabled)
        {
            agent.updatePosition = true;
            agent.updateRotation = false;
            agent.Warp(transform.position);
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        ResetAttackData();
    }

    void OnAnimatorMove()
    {
        if (anim == null)
            return;

        if (IsInAttackState() && attackTriggered && attackMotionState != AttackMotionState.InRootMotionAttack)
            BeginRootMotionAttack();

        if (attackMotionState != AttackMotionState.InRootMotionAttack)
            return;

        Vector3 deltaPos = anim.deltaPosition;
        Quaternion deltaRot = anim.deltaRotation;

        if (ShouldApplyRootMotionPosition())
            transform.position += deltaPos;

        if (ShouldApplyRootMotionRotation())
            transform.rotation = transform.rotation * deltaRot;

        if (agent != null && agent.enabled)
            agent.nextPosition = transform.position;
    }

    bool ShouldApplyRootMotionPosition()
    {
        return activeAttack == null || activeAttack.useRootMotionPosition;
    }

    bool ShouldApplyRootMotionRotation()
    {
        if (activeAttack == null)
            return true;

        if (activeAttack.trackTargetUntilDirectionLock)
            return false;

        return activeAttack.useRootMotionRotation;
    }

    public void PlayDeath()
    {
        if (isDead)
            return;

        isDead = true;
        AbortAttackMotion();

        anim.applyRootMotion = false;
        anim.SetTrigger(deathTriggerParam);

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.updatePosition = false;
            agent.updateRotation = false;
        }
    }

    public bool PlayAvoid()
    {
        if (anim == null || isDead)
            return false;

        if (IsInAttackState() || IsInKneelLikeState() || IsInAvoidState())
            return false;

        anim.SetTrigger(avoidTriggerParam);
        return true;
    }

    public void LockCurrentAttackDirection()
    {
        if (activeAttack == null || !activeAttack.trackTargetUntilDirectionLock)
            return;

        attackLockedForward = GetClampedDirectionToTarget();
        if (attackLockedForward.sqrMagnitude < 0.0001f)
            return;

        attackDirectionLocked = true;
        transform.rotation = Quaternion.LookRotation(attackLockedForward, Vector3.up);
    }

    public Vector3 GetCurrentAttackDirection()
    {
        if (activeAttack != null && activeAttack.trackTargetUntilDirectionLock)
        {
            if (attackDirectionLocked)
                return attackLockedForward;

            return GetClampedDirectionToTarget();
        }

        return GetPlanarForward();
    }

    Vector3 GetClampedDirectionToTarget()
    {
        Vector3 toTarget = GetPlanarDirectionToTarget();
        if (toTarget.sqrMagnitude < 0.0001f)
            return attackLockedForward;

        Vector3 startForward = attackStartForward;
        if (startForward.sqrMagnitude < 0.0001f)
            startForward = GetPlanarForward();

        float maxAngle = activeAttack != null ? Mathf.Max(0f, activeAttack.maxTurnAngle) : 0f;

        return Vector3.RotateTowards(
            startForward.normalized,
            toTarget.normalized,
            maxAngle * Mathf.Deg2Rad,
            0f
        ).normalized;
    }

    Vector3 GetPlanarDirectionToTarget()
    {
        if (target == null)
            return Vector3.zero;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        return toTarget;
    }

    Vector3 GetPlanarForward()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return Vector3.forward;

        return forward.normalized;
    }

    public bool IsInAttackState()
    {
        if (anim == null)
            return false;

        AnimatorStateInfo current = anim.GetCurrentAnimatorStateInfo(0);
        if (anim.IsInTransition(0))
        {
            AnimatorStateInfo next = anim.GetNextAnimatorStateInfo(0);
            return current.IsTag("Attack") || next.IsTag("Attack");
        }

        return current.IsTag("Attack");
    }

    public bool IsInDeathState()
    {
        if (anim == null)
            return false;

        AnimatorStateInfo current = anim.GetCurrentAnimatorStateInfo(0);
        if (anim.IsInTransition(0))
        {
            AnimatorStateInfo next = anim.GetNextAnimatorStateInfo(0);
            return current.IsTag("Death") || next.IsTag("Death");
        }

        return current.IsTag("Death");
    }

    public bool IsInStateByName(string stateName)
    {
        if (anim == null || string.IsNullOrEmpty(stateName))
            return false;

        AnimatorStateInfo current = anim.GetCurrentAnimatorStateInfo(0);
        if (current.IsName(stateName))
            return true;

        if (anim.IsInTransition(0))
        {
            AnimatorStateInfo next = anim.GetNextAnimatorStateInfo(0);
            if (next.IsName(stateName))
                return true;
        }

        return false;
    }

    public bool IsInKneelState()
    {
        return IsInStateByName(kneelStateName);
    }

    public bool IsInAvoidState()
    {
        return IsInStateByName(avoidStateName);
    }

    public bool IsInKneelIdleState()
    {
        return IsInStateByName(kneelIdleStateName);
    }

    public bool IsInStandState()
    {
        return IsInStateByName(standStateName);
    }

    public bool IsInKneelLikeState()
    {
        return IsInKneelState() || IsInKneelIdleState() || IsInStandState();
    }

    public bool IsInStaggerReactionState()
    {
        return IsInStateByName(hitReactionStateName)
            || IsInStateByName(hitSmallReactionStateName)
            || IsInStateByName(hitBigReactionStateName)
            || IsInStateByName(parryReactionStateName);
    }

    public float GetCurrentNormalizedTime()
    {
        if (anim == null)
            return 0f;

        return anim.GetCurrentAnimatorStateInfo(0).normalizedTime;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
