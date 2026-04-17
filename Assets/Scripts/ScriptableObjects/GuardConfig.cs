using System;
using UnityEngine;

[Serializable]
public enum GuardActionType
{
    Attack1,
    Attack2,
    Heal
}

[Serializable]
public class GuardActionData
{
    public bool enabled = true;
    public string actionName = "Attack";
    public GuardActionType actionType = GuardActionType.Attack1;
    public float value = 10f;
    public float cooldownTime = 1f;
    public float preferredRange = 2.2f;
    public float actualRange = 2.2f;
    public int animatorChooseValue = 0;

    [Header("Action Audio")]
    public AudioClip[] actionVoiceClips;
    public AudioClip[] actionHitVoiceClips;

    [Header("Action VFX")]
    public GameObject attackHappenVFXPrefab;
    public GameObject attackHappenVFXDisplaySocket;
    public float attackHappenVFXLifetime = 1f;
    public GameObject hitEffectPrefab;
    public float hitEffectLifetime = 0.75f;
    public float hitEffectNormalOffset = 0.02f;
    public TrailRenderer[] slashTrails;
}

[Serializable]
public class GuardAudioConfig
{
    public AudioClip[] hurtVoiceClips;
    public AudioClip[] dieVoiceClips;
    public AudioClip[] moveClips;
    public float moveSoundInterval = 0.45f;
    public float moveSoundMinSpeed = 0.2f;
}

[Serializable]
public class GuardAnimationConfig
{
    public string speedParam = "Speed";
    public string attackTrigger = "Attack";
    public string chooseParam = "Choose";
    public string isWaitingIdleParam = "IsWaitingIdle";
    public float dampTime = 0.1f;
    public float waitIdleDelay = 5f;
    public float moveThreshold = 0.1f;

    [Header("Death")]
    public string deathTriggerParam = "IsDead";
    public string deathChooseParam = "DeadChose";
    [Range(0, 1)] public int deathAnimationIndex = 0;
    public float destroyAfterDeathDelay = 4f;
}

[CreateAssetMenu(menuName = "Config/Guard Config")]
public class GuardConfig : ScriptableObject
{
    public string guardId;
    public string guardName;

    [Header("Core Data")]
    public int level = 1;
    public float maxHP = 50f;

    [Header("Actions")]
    public GuardActionData[] actions =
    {
        new GuardActionData
        {
            actionName = "Attack 1",
            actionType = GuardActionType.Attack1,
            value = 10f,
            cooldownTime = 1f,
            preferredRange = 2.2f,
            actualRange = 2.2f,
            animatorChooseValue = 0
        },
        new GuardActionData
        {
            actionName = "Attack 2",
            actionType = GuardActionType.Attack2,
            value = 12f,
            cooldownTime = 1.2f,
            preferredRange = 2.2f,
            actualRange = 2.2f,
            animatorChooseValue = 2
        },
        new GuardActionData
        {
            enabled = false,
            actionName = "Heal",
            actionType = GuardActionType.Heal,
            value = 15f,
            cooldownTime = 8f,
            preferredRange = 0f,
            actualRange = 0f,
            animatorChooseValue = 1
        }
    };

    [Header("Audio")]
    public GuardAudioConfig audio = new GuardAudioConfig();

    [Header("Animation")]
    public GuardAnimationConfig animation = new GuardAnimationConfig();

    public GameObject guardPrefab;
}
