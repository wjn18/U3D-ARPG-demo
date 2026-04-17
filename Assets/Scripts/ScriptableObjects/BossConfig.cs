using System;
using UnityEngine;

[Serializable]
public class BossAudioConfig
{
    public AudioClip[] hurtVoiceClips;
    public AudioClip[] dieVoiceClips;
    public AudioClip[] moveClips;
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
    public float kneelIdleHoldDuration = 2f;
    public float standBoolReleaseDelay = 1f;
    public float executeDistance = 4f;
    public float executeDamage = 80f;

    [Header("Attacks")]
    public BossAttackData[] attacks = CreateDefaultAttackSet();

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
                priority = 10,
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
                damage = 20f,
                actualAttackRange = 4.3f,
                meleeWindows = new[]
                {
                    new BossMeleeWindowData { windowIndex = 0, damage = 20f, radius = 1f, facingAngle = 60f }
                },
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
                priority = 10,
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
                damage = 15f,
                actualAttackRange = 4.3f,
                meleeWindows = new[]
                {
                    new BossMeleeWindowData { windowIndex = 0, damage = 15f, radius = 1f, facingAngle = 60f }
                },
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
                priority = 11,
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
                damage = 15f,
                actualAttackRange = 5.5f,
                meleeWindows = new[]
                {
                    new BossMeleeWindowData { windowIndex = 0, damage = 15f, radius = 3f, facingAngle = 60f }
                },
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
                priority = 20,
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
                damage = 10f,
                actualAttackRange = 4.3f,
                meleeWindows = new[]
                {
                    new BossMeleeWindowData { windowIndex = 0, damage = 10f, radius = 2f, facingAngle = 60f }
                },
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
                priority = 21,
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
                damage = 25f,
                actualAttackRange = 6.5f,
                meleeWindows = new[]
                {
                    new BossMeleeWindowData { windowIndex = 0, damage = 25f, radius = 3f, facingAngle = 60f }
                },
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
                priority = 22,
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
                damage = 20f,
                actualAttackRange = 6.5f,
                meleeWindows = new[]
                {
                    new BossMeleeWindowData { windowIndex = 0, damage = 20f, radius = 3f, facingAngle = 80f },
                    new BossMeleeWindowData { windowIndex = 1, damage = 20f, radius = 3f, facingAngle = 80f }
                },
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
                priority = 18,
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
                damage = 30f,
                actualAttackRange = 12f,
                projectileSpeed = 20f,
                projectileLifeTime = 5f,
                interruptibleInStartup = false,
                interruptibleInActive = false,
                interruptibleInRecovery = true
            }
        };
    }
}
