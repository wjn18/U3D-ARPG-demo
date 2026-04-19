using System;
using System.Collections.Generic;
using UnityEngine;

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

public enum CombatLocomotionKind
{
    Free,
    LockOn
}

public enum RollDirection
{
    Front = 0,
    Back = 1,
    Left = 2,
    Right = 3
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

public interface ICombatPrioritySource
{
    int GetCombatPriority();
}

[Serializable]
public class CombatAttackData
{
    public string attackName;
    public string animatorStateName;
    public AnimationClip animation;
    public float damage = 10f;
    public float spCost = 10f;
    public float apGainPerHit = 1f;
    public float maxMoveDistance = 2f;
    public float hitRadius = 1f;
    public int priority = 1;
    public float transitionDuration = 0.05f;
    public AudioClip attackVoice;
    public AudioClip attackSwing;
    public AudioClip attackHit;
    public GameObject attackVFX;
    public string vfxSocketId;
}

[Serializable]
public class ChargedCombatAttackData
{
    public CombatAttackData attack = new CombatAttackData();
    public float holdTime = 0.5f;
    public string hitboxSocketId;
}

[Serializable]
public class CombatAvoidData
{
    public RollDirection direction = RollDirection.Front;
    public string animatorStateName;
    public AnimationClip animation;
    public float moveDistance = 3f;
    public float transitionDuration = 0.05f;
    public GameObject vfx;
    public string vfxSocketId;
    public AudioClip dodgeSound;
}

[Serializable]
public class CombatLocomotionData
{
    public CombatLocomotionKind locomotionKind = CombatLocomotionKind.Free;
    public string animatorStateName;
    public AnimationClip animation;
    public GameObject loopingVFX;
    public string vfxSocketId;
    public AudioClip movementSound;
}

[Serializable]
public class CombatHitData
{
    public string hitName;
    public string animatorStateName;
    public AnimationClip animation;
    public float moveDistance = 0.5f;
    public float transitionDuration = 0.1f;
}

[CreateAssetMenu(menuName = "Combat/Player Combat Config", fileName = "PlayerCombatConfig")]
public class PlayerCombatConfig : ScriptableObject
{
    [Header("Attack Groups")]
    public List<CombatAttackData> lightAttacks = new List<CombatAttackData>();
    public List<CombatAttackData> heavyAttacks = new List<CombatAttackData>();
    public CombatAttackData sprintAttack = new CombatAttackData();
    public ChargedCombatAttackData chargedAttack = new ChargedCombatAttackData();

    [Header("Avoid")]
    public List<CombatAvoidData> avoidDirections = new List<CombatAvoidData>();

    [Header("Locomotion")]
    public List<CombatLocomotionData> locomotionConfigs = new List<CombatLocomotionData>();

    [Header("Hit")]
    public List<CombatHitData> smallHits = new List<CombatHitData>();
    public List<CombatHitData> bigHits = new List<CombatHitData>();

    [Header("Priority")]
    public int locomotionPriority = 1;
    public int avoidPriority = 2;

    public CombatAvoidData GetAvoidData(RollDirection direction)
    {
        for (int i = 0; i < avoidDirections.Count; i++)
        {
            CombatAvoidData avoidData = avoidDirections[i];
            if (avoidData != null && avoidData.direction == direction)
                return avoidData;
        }

        return null;
    }

    public CombatLocomotionData GetLocomotionData(CombatLocomotionKind locomotionKind)
    {
        for (int i = 0; i < locomotionConfigs.Count; i++)
        {
            CombatLocomotionData locomotionData = locomotionConfigs[i];
            if (locomotionData != null && locomotionData.locomotionKind == locomotionKind)
                return locomotionData;
        }

        return null;
    }
}

[Serializable]
public class CombatTransformSocketBinding
{
    public string id;
    public Transform socket;
}

[Serializable]
public class CombatBoxColliderSocketBinding
{
    public string id;
    public BoxCollider boxCollider;
}

public class CombatSocketMap : MonoBehaviour
{
    [SerializeField] private List<CombatTransformSocketBinding> transformSockets = new List<CombatTransformSocketBinding>();
    [SerializeField] private List<CombatBoxColliderSocketBinding> boxColliderSockets = new List<CombatBoxColliderSocketBinding>();

    public bool TryGetTransform(string socketId, out Transform socket)
    {
        socket = null;

        if (string.IsNullOrWhiteSpace(socketId))
            return false;

        for (int i = 0; i < transformSockets.Count; i++)
        {
            CombatTransformSocketBinding binding = transformSockets[i];
            if (binding == null || binding.socket == null)
                continue;

            if (!string.Equals(binding.id, socketId, StringComparison.Ordinal))
                continue;

            socket = binding.socket;
            return true;
        }

        return false;
    }

    public bool TryGetBoxCollider(string socketId, out BoxCollider boxCollider)
    {
        boxCollider = null;

        if (string.IsNullOrWhiteSpace(socketId))
            return false;

        for (int i = 0; i < boxColliderSockets.Count; i++)
        {
            CombatBoxColliderSocketBinding binding = boxColliderSockets[i];
            if (binding == null || binding.boxCollider == null)
                continue;

            if (!string.Equals(binding.id, socketId, StringComparison.Ordinal))
                continue;

            boxCollider = binding.boxCollider;
            return true;
        }

        return false;
    }
}
