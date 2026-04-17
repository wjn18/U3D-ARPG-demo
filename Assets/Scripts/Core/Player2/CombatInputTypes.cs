using System;

public enum CombatInputType
{
    X,
    Y
}

public enum CombatExecutionState
{
    Locomotion,
    LockOnLocomotion,
    Attack,
    Hit,
    Roll,
    Knockback,
    Dead
}

public enum BufferConsumeMode
{
    ClearAll,
    ConsumeMatchedOnly
}

[Serializable]
public struct CombatBufferedInput
{
    public CombatInputType inputType;
    public float timestamp;

    public CombatBufferedInput(CombatInputType inputType, float timestamp)
    {
        this.inputType = inputType;
        this.timestamp = timestamp;
    }
}
