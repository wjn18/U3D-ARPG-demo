using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ComboDefinition
{
    [SerializeField] private string comboName = "New Combo";
    [SerializeField] private List<CombatInputType> inputSequence = new List<CombatInputType>();
    [SerializeField] private float maxAllowedGap = 0.35f;
    [SerializeField] private string animatorTrigger = "ComboTrigger";
    [SerializeField] private int priority = 100;

    public string ComboName => comboName;
    public IReadOnlyList<CombatInputType> InputSequence => inputSequence;
    public float MaxAllowedGap => Mathf.Max(0.01f, maxAllowedGap);
    public string AnimatorTrigger => animatorTrigger;
    public int Priority => priority;
    public int Length => inputSequence != null ? inputSequence.Count : 0;
    public bool IsValid => Length > 0 && !string.IsNullOrWhiteSpace(animatorTrigger);

    public ComboDefinition()
    {
    }

    public ComboDefinition(string comboName, string animatorTrigger, float maxAllowedGap, int priority, params CombatInputType[] inputSequence)
    {
        this.comboName = comboName;
        this.animatorTrigger = animatorTrigger;
        this.maxAllowedGap = maxAllowedGap;
        this.priority = priority;
        this.inputSequence = new List<CombatInputType>(inputSequence ?? Array.Empty<CombatInputType>());
    }
}
