using UnityEngine;
using UnityEngine.Serialization;

public enum BossAttackCategory
{
    Normal,
    MeleeSkill,
    Ranged
}

public enum BossAttackPhase
{
    None,
    Startup,
    Active,
    Recovery
}

[System.Serializable]
public class BossMeleeWindowData
{
    public int windowIndex;
    public float damage = 20f;
    public string[] hitboxIds;
}

[System.Serializable]
public class BossAttackDefinition
{
    public string displayName = "Attack";
    public int attackIndex;
    public BossAttackCategory category;
    public float cooldown = 1f;
    public float minRange = 0f;
    public float maxRange = 3f;
    public float preferredDistance = 0f;
    [FormerlySerializedAs("priority"), SerializeField, InspectorName("Attack Priority")]
    private int attackSelectionPriority = 0;
    [SerializeField, InspectorName("Priority")]
    private int hitReactionPriority = 1;

    public int attackPriority
    {
        get => attackSelectionPriority;
        set => attackSelectionPriority = value;
    }

    public int priority
    {
        get => hitReactionPriority;
        set => hitReactionPriority = value;
    }

    [Header("Animation")]
    public string animationTrigger = "AttackTrigger";
    public string animationStateName;

    [Header("Timings")]
    public float startupDuration = 0.25f;
    public float activeDuration = 0.2f;
    public float recoveryDuration = 0.35f;

    [Header("Motion")]
    public bool alignBeforeAttack = true;
    public bool useRootMotionPosition = true;
    public bool useRootMotionRotation = true;
    public bool trackTargetUntilDirectionLock = false;
    public bool requireDirectionLockEvent = false;
    public float maxTurnAngle = 35f;
    public float turnSpeed = 360f;

    [Header("Payload")]
    public bool opensMeleeWindow = true;
    public bool firesProjectile = false;

    [Header("Damage")]
    public float damage = 20f;
    public float actualAttackRange = 0f;

    [Header("Melee Windows")]
    public BossMeleeWindowData[] meleeWindows;

    [Header("Projectile")]
    public float projectileSpeed = 20f;
    public float projectileLifeTime = 5f;

    [Header("Interrupt")]
    public bool interruptibleInStartup = false;
    public bool interruptibleInActive = false;
    public bool interruptibleInRecovery = true;

    [Header("Audio")]
    public AudioClip[] attackVoiceClips;
    public AudioClip[] attackSwingClips;
    public AudioClip[] attackHitClips;
    public AudioClip[] attackMissClips;

    [Header("VFX")]
    public GameObject hitEffectPrefab;
    public float hitEffectLifetime = 0.75f;
    public float hitEffectNormalOffset = 0.02f;
    public TrailRenderer[] slashTrails;

    public float TotalDuration => Mathf.Max(0f, startupDuration) + Mathf.Max(0f, activeDuration) + Mathf.Max(0f, recoveryDuration);
    public float ActualRange => actualAttackRange > 0f ? actualAttackRange : maxRange;

    public bool IsInRange(float distance)
    {
        return distance >= minRange && distance <= ActualRange;
    }

    public float PreferredRange
    {
        get
        {
            if (preferredDistance > 0f)
                return preferredDistance;

            if (maxRange <= minRange)
                return maxRange;

            return (minRange + maxRange) * 0.5f;
        }
    }

    public BossAttackPhase EvaluatePhase(float elapsed)
    {
        if (elapsed < 0f)
            return BossAttackPhase.None;

        float startupEnd = Mathf.Max(0f, startupDuration);
        if (elapsed < startupEnd)
            return BossAttackPhase.Startup;

        float activeEnd = startupEnd + Mathf.Max(0f, activeDuration);
        if (elapsed < activeEnd)
            return BossAttackPhase.Active;

        float recoveryEnd = activeEnd + Mathf.Max(0f, recoveryDuration);
        if (elapsed < recoveryEnd)
            return BossAttackPhase.Recovery;

        return BossAttackPhase.Recovery;
    }

    public bool IsInterruptible(BossAttackPhase phase)
    {
        switch (phase)
        {
            case BossAttackPhase.Startup:
                return interruptibleInStartup;
            case BossAttackPhase.Active:
                return interruptibleInActive;
            case BossAttackPhase.Recovery:
                return interruptibleInRecovery;
            default:
                return true;
        }
    }

    public BossMeleeWindowData GetMeleeWindow(int windowIndex)
    {
        if (meleeWindows == null || meleeWindows.Length == 0)
            return null;

        for (int i = 0; i < meleeWindows.Length; i++)
        {
            BossMeleeWindowData window = meleeWindows[i];
            if (window == null)
                continue;

            if (window.windowIndex == windowIndex)
                return window;
        }

        return null;
    }
}

[System.Serializable]
public class BossAttackData : BossAttackDefinition
{
}
