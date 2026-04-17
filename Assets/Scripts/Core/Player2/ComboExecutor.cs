using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ComboExecutor
{
    [Header("Fallback Triggers")]
    [SerializeField] private string lightAttackTrigger = "AttackTrigger";
    [SerializeField] private string heavyAttackTrigger = "HeavyAttackTrigger";
    [SerializeField] private string chargedAttackTrigger = "ChargedAttackTrigger";
    [SerializeField] private string sprintAttackTrigger = "SprintAttackTrigger";

    [Header("Legacy Queue Cleanup")]
    [SerializeField] private string queueNextAttackParam = "QueueNextAttack";

    [Header("Buffer Consumption")]
    [SerializeField] private BufferConsumeMode bufferConsumeMode = BufferConsumeMode.ClearAll;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private float lastExecutedSpecialEndTimestamp = float.NegativeInfinity;

    public void ResetRuntimeState()
    {
        lastExecutedSpecialEndTimestamp = float.NegativeInfinity;
    }

    public bool TryExecute(
        Animator animator,
        CombatInputBuffer inputBuffer,
        IList<ComboDefinition> comboDefinitions,
        bool allowFallbackAttacks,
        out string executedAction)
    {
        executedAction = string.Empty;

        if (animator == null || inputBuffer == null)
            return false;

        if (TryExecuteSpecialCombo(animator, inputBuffer, comboDefinitions, out executedAction))
            return true;

        if (!allowFallbackAttacks)
            return false;

        return TryExecuteFallbackAttack(animator, inputBuffer, out executedAction);
    }

    public bool TryExecuteSpecialCombo(
        Animator animator,
        CombatInputBuffer inputBuffer,
        IList<ComboDefinition> comboDefinitions,
        out string executedAction)
    {
        executedAction = string.Empty;

        if (!ComboMatcher.TryFindBestMatch(
                inputBuffer.History,
                comboDefinitions,
                lastExecutedSpecialEndTimestamp,
                out ComboMatchResult match))
        {
            return false;
        }

        ClearNormalAttackRequests(animator);

        if (!TrySetTrigger(animator, match.Definition.AnimatorTrigger))
            return false;

        lastExecutedSpecialEndTimestamp = match.EndTimestamp;
        ConsumeBufferedInputs(inputBuffer, match);

        executedAction = match.Definition.ComboName;
        if (debugLogs)
            Debug.Log("[ComboExecutor] Executed combo: " + executedAction);

        return true;
    }

    void ClearNormalAttackRequests(Animator animator)
    {
        ResetTriggerIfPresent(animator, lightAttackTrigger);
        ResetTriggerIfPresent(animator, heavyAttackTrigger);
        ResetTriggerIfPresent(animator, chargedAttackTrigger);
        ResetTriggerIfPresent(animator, sprintAttackTrigger);

        if (HasBoolParameter(animator, queueNextAttackParam))
            animator.SetBool(queueNextAttackParam, false);
    }

    bool TryExecuteFallbackAttack(
        Animator animator,
        CombatInputBuffer inputBuffer,
        out string executedAction)
    {
        executedAction = string.Empty;

        if (!inputBuffer.TryPeekPending(out CombatBufferedInput bufferedInput))
            return false;

        string triggerName = bufferedInput.inputType == CombatInputType.X
            ? lightAttackTrigger
            : heavyAttackTrigger;

        if (!TrySetTrigger(animator, triggerName))
            return false;

        inputBuffer.ConsumePending(bufferedInput);
        executedAction = bufferedInput.inputType.ToString();

        if (debugLogs)
            Debug.Log("[ComboExecutor] Executed fallback attack: " + executedAction);

        return true;
    }

    void ConsumeBufferedInputs(CombatInputBuffer inputBuffer, ComboMatchResult match)
    {
        switch (bufferConsumeMode)
        {
            case BufferConsumeMode.ClearAll:
                inputBuffer.ClearAll();
                break;

            case BufferConsumeMode.ConsumeMatchedOnly:
                int removedCount = inputBuffer.ConsumePendingSequence(match.Definition.InputSequence, match.EndTimestamp);
                if (removedCount <= 0)
                    inputBuffer.ConsumePendingUpToTimestamp(match.EndTimestamp, match.Length);
                break;
        }
    }

    static bool TrySetTrigger(Animator animator, string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName))
            return false;

        if (!HasTriggerParameter(animator, triggerName))
        {
            Debug.LogWarning("[ComboExecutor] Missing Animator trigger: " + triggerName, animator);
            return false;
        }

        animator.ResetTrigger(triggerName);
        animator.SetTrigger(triggerName);
        return true;
    }

    static void ResetTriggerIfPresent(Animator animator, string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName))
            return;

        if (!HasTriggerParameter(animator, triggerName))
            return;

        animator.ResetTrigger(triggerName);
    }

    static bool HasTriggerParameter(Animator animator, string triggerName)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name != triggerName)
                continue;

            return parameter.type == AnimatorControllerParameterType.Trigger;
        }

        return false;
    }

    static bool HasBoolParameter(Animator animator, string paramName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(paramName))
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name != paramName)
                continue;

            return parameter.type == AnimatorControllerParameterType.Bool;
        }

        return false;
    }
}
