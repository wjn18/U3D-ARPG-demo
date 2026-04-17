using UnityEngine;

public class PlayerCombatStateBehaviour : StateMachineBehaviour
{
    [SerializeField] private CombatExecutionState stateOnEnter = CombatExecutionState.Attack;
    [SerializeField] private bool applyStateOnExit = false;
    [SerializeField] private CombatExecutionState stateOnExit = CombatExecutionState.Locomotion;

    private PlayerCombatController combatController;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        ResolveController(animator);

        if (combatController != null)
            combatController.SetCombatState(stateOnEnter);
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!applyStateOnExit)
            return;

        ResolveController(animator);

        if (combatController != null)
            combatController.SetCombatState(stateOnExit);
    }

    void ResolveController(Animator animator)
    {
        if (combatController == null && animator != null)
            combatController = animator.GetComponentInParent<PlayerCombatController>();
    }
}
