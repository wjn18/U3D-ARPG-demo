using UnityEngine;

public class AttackStateNotifier : StateMachineBehaviour
{
    private PlayerCombatController combatController;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (combatController == null)
            combatController = animator.GetComponentInParent<PlayerCombatController>();

        if (combatController != null)
            combatController.NotifyAttackStarted();
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (combatController == null)
            combatController = animator.GetComponentInParent<PlayerCombatController>();

        if (combatController != null)
            combatController.NotifyAttackEnded();
    }
}
