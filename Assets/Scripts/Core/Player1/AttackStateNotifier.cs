using UnityEngine;

public class AttackStateNotifier : StateMachineBehaviour
{
    private PlayerAttackCancelController attackCancel;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (attackCancel == null)
            attackCancel = animator.GetComponentInParent<PlayerAttackCancelController>();

        if (attackCancel != null)
            attackCancel.NotifyAttackStarted();
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (attackCancel == null)
            attackCancel = animator.GetComponentInParent<PlayerAttackCancelController>();

        if (attackCancel != null)
            attackCancel.NotifyAttackEnded();
    }
}