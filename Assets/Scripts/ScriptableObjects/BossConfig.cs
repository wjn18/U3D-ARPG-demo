using System;
using UnityEngine;

[Serializable]
public class BossAudioConfig
{
    public AudioClip[] hurtVoiceClips;
    public AudioClip[] dieVoiceClips;
    public AudioClip[] moveClips;
}

[Serializable]
public class BossAvoidConfig
{
    public float moveDistance = 2f;
    public AudioClip[] sfxClips;
    public GameObject vfxPrefab;
    public string vfxSocketId;
    public float vfxLifetime = 1f;
}

[CreateAssetMenu(menuName = "Config/Boss Config")]
public class BossConfig : ScriptableObject
{
    public string bossId = "Uriel";
    public string bossName = "Uriel";

    [Header("Core Data")]
    public float maxHP = 1000f;
    public float maxRV = 200f;

    [Header("AI")]
    public float engageDistance = 25f;
    public float approachStopDistance = 4.3f;
    public float repathInterval = 0.4f;
    public float combatDistanceTolerance = 0.35f;

    [Header("Stagger")]
    [Range(0f, 1f)] public float alwaysOpenThresholdPercent = 0.3f;
    public float recoverDelay = 4f;
    public float recoverPerSecond = 10f;
    public float initialStaggerWindowDuration = 2f;
    public float superArmorDuration = 6f;
    [InspectorName("RV reduction after being parried")]
    public float rvReductionAfterParried = 50f;
    public float kneelIdleHoldDuration = 2f;
    public float standBoolReleaseDelay = 1f;
    public float executeDistance = 4f;
    public float executeDamage = 80f;

    [Header("Attacks")]
    public BossAttackData[] attacks = CreateDefaultAttackSet();

    [Header("Avoid")]
    public BossAvoidConfig avoid = new BossAvoidConfig();

    [Header("Audio")]
    public BossAudioConfig audio = new BossAudioConfig();

    public static BossAttackData[] CreateDefaultAttackSet()
    {
        return new[]
        {
            new BossAttackData
            {
                displayName = "Normal 1",
                attackIndex = 0,
                category = BossAttackCategory.Normal,
                cooldown = 12f,
                minRange = 0f,
                maxRange = 4.3f,
                preferredDistance = 4f,
                attackPriority = 10,
                startupDuration = 0.25f,
                activeDuration = 0.2f,
                recoveryDuration = 0.45f,
                alignBeforeAttack = true,
                useRootMotionPosition = true,
                useRootMotionRotation = true,
                trackTargetUntilDirectionLock = false,
                requireDirectionLockEvent = false,
                opensMeleeWindow = true,
                firesProjectile = false,
                startupMoveMaxDistance = 0f,
                damage = 20f,
                meleeWindows = new[]
                {
                    new BossMeleeWindowData { windowIndex = 0, damage = 20f, hitboxIds = Array.Empty<string>() }
                },
                warningAreaId = string.Empty,
                interruptibleInStartup = false,
                interruptibleInActive = false,
                interruptibleInRecovery = true
            },
            new BossAttackData
            {
                displayName = "Normal 2",
                attackIndex = 1,
                category = BossAttackCategory.Normal,
                cooldown = 12f,
                minRange = 0f,
                maxRange = 4.3f,
                preferredDistance = 4f,
                attackPriority = 10,
                startupDuration = 0.28f,
                activeDuration = 0.2f,
                recoveryDuration = 0.42f,
                alignBeforeAttack = true,
                useRootMotionPosition = true,
                useRootMotionRotation = true,
                trackTargetUntilDirectionLock = false,
                requireDirectionLockEvent = false,
                opensMeleeWindow = true,
                firesProjectile = false,
                startupMoveMaxDistance = 0f,
                damage = 15f,
                meleeWindows = new[]
                {
                    new BossMeleeWindowData { windowIndex = 0, damage = 15f, hitboxIds = Array.Empty<string>() }
                },
                warningAreaId = string.Empty,
                interruptibleInStartup = false,
                interruptibleInActive = false,
                interruptibleInRecovery = true
            },
            new BossAttackData
            {
                displayName = "Normal 3",
                attackIndex = 2,
                category = BossAttackCategory.Normal,
                cooldown = 12f,
                minRange = 0f,
                maxRange = 5.5f,
                preferredDistance = 5f,
                attackPriority = 11,
                startupDuration = 0.32f,
                activeDuration = 0.22f,
                recoveryDuration = 0.42f,
                alignBeforeAttack = true,
                useRootMotionPosition = true,
                useRootMotionRotation = true,
                trackTargetUntilDirectionLock = false,
                requireDirectionLockEvent = false,
                opensMeleeWindow = true,
                firesProjectile = false,
                startupMoveMaxDistance = 0f,
                damage = 15f,
                meleeWindows = new[]
                {
                    new BossMeleeWindowData { windowIndex = 0, damage = 15f, hitboxIds = Array.Empty<string>() }
                },
                warningAreaId = string.Empty,
                interruptibleInStartup = false,
                interruptibleInActive = false,
                interruptibleInRecovery = true
            },
            new BossAttackData
            {
                displayName = "Melee Skill 1",
                attackIndex = 3,
                category = BossAttackCategory.MeleeSkill,
                cooldown = 17f,
                minRange = 0f,
                maxRange = 4.3f,
                preferredDistance = 4f,
                attackPriority = 20,
                startupDuration = 0.35f,
                activeDuration = 0.22f,
                recoveryDuration = 0.38f,
                alignBeforeAttack = true,
                useRootMotionPosition = true,
                useRootMotionRotation = false,
                trackTargetUntilDirectionLock = true,
                requireDirectionLockEvent = true,
                maxTurnAngle = 35f,
                turnSpeed = 360f,
                opensMeleeWindow = true,
                firesProjectile = false,
                startupMoveMaxDistance = 0f,
                damage = 10f,
                meleeWindows = new[]
                {
                    new BossMeleeWindowData { windowIndex = 0, damage = 10f, hitboxIds = Array.Empty<string>() }
                },
                warningAreaId = string.Empty,
                interruptibleInStartup = false,
                interruptibleInActive = false,
                interruptibleInRecovery = true
            },
            new BossAttackData
            {
                displayName = "Melee Skill 2",
                attackIndex = 4,
                category = BossAttackCategory.MeleeSkill,
                cooldown = 20f,
                minRange = 0f,
                maxRange = 6.5f,
                preferredDistance = 5.5f,
                attackPriority = 21,
                startupDuration = 0.38f,
                activeDuration = 0.24f,
                recoveryDuration = 0.4f,
                alignBeforeAttack = true,
                useRootMotionPosition = true,
                useRootMotionRotation = false,
                trackTargetUntilDirectionLock = true,
                requireDirectionLockEvent = true,
                maxTurnAngle = 35f,
                turnSpeed = 360f,
                opensMeleeWindow = true,
                firesProjectile = false,
                startupMoveMaxDistance = 0f,
                damage = 25f,
                meleeWindows = new[]
                {
                    new BossMeleeWindowData { windowIndex = 0, damage = 25f, hitboxIds = Array.Empty<string>() }
                },
                warningAreaId = string.Empty,
                interruptibleInStartup = false,
                interruptibleInActive = false,
                interruptibleInRecovery = true
            },
            new BossAttackData
            {
                displayName = "Melee Skill 3",
                attackIndex = 5,
                category = BossAttackCategory.MeleeSkill,
                cooldown = 25f,
                minRange = 0f,
                maxRange = 6.5f,
                preferredDistance = 5f,
                attackPriority = 22,
                startupDuration = 0.42f,
                activeDuration = 0.24f,
                recoveryDuration = 0.42f,
                alignBeforeAttack = true,
                useRootMotionPosition = true,
                useRootMotionRotation = false,
                trackTargetUntilDirectionLock = true,
                requireDirectionLockEvent = true,
                maxTurnAngle = 35f,
                turnSpeed = 360f,
                opensMeleeWindow = true,
                firesProjectile = false,
                startupMoveMaxDistance = 0f,
                damage = 20f,
                meleeWindows = new[]
                {
                    new BossMeleeWindowData { windowIndex = 0, damage = 20f, hitboxIds = Array.Empty<string>() },
                    new BossMeleeWindowData { windowIndex = 1, damage = 20f, hitboxIds = Array.Empty<string>() }
                },
                warningAreaId = string.Empty,
                interruptibleInStartup = false,
                interruptibleInActive = false,
                interruptibleInRecovery = true
            },
            new BossAttackData
            {
                displayName = "Ranged",
                attackIndex = 6,
                category = BossAttackCategory.Ranged,
                cooldown = 30f,
                minRange = 4.5f,
                maxRange = 12f,
                preferredDistance = 10f,
                attackPriority = 18,
                startupDuration = 0.4f,
                activeDuration = 0.2f,
                recoveryDuration = 0.45f,
                alignBeforeAttack = true,
                useRootMotionPosition = true,
                useRootMotionRotation = false,
                trackTargetUntilDirectionLock = true,
                requireDirectionLockEvent = true,
                maxTurnAngle = 35f,
                turnSpeed = 360f,
                opensMeleeWindow = false,
                firesProjectile = true,
                startupMoveMaxDistance = 0f,
                damage = 30f,
                projectileSpeed = 20f,
                projectileLifeTime = 5f,
                warningAreaId = string.Empty,
                interruptibleInStartup = false,
                interruptibleInActive = false,
                interruptibleInRecovery = true
            }
        };
    }
}
