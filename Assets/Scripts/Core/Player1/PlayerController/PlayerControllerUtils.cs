using System.Collections;
using UnityEngine;

public partial class PlayerController
{
    bool IsMoveHeld()
    {
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputY = Input.GetAxisRaw("Vertical");
        Vector2 input = new Vector2(inputX, inputY);
        return input.sqrMagnitude > (moveCancelInputThreshold * moveCancelInputThreshold);
    }

    bool HasAnimatorParameter(string paramName)
    {
        if (animator == null || string.IsNullOrEmpty(paramName)) return false;

        foreach (var p in animator.parameters)
        {
            if (p.name == paramName) return true;
        }
        return false;
    }

    void UpdateBlockReentryLock()
    {
        if (!blockLockedByLowSP)
            return;

        float minSP = GetBlockReenterMinSP();
        if (stats == null || stats.sp >= minSP)
            blockLockedByLowSP = false;
    }

    public float GetBlockReenterMinSP()
    {
        if (stats == null)
            return Mathf.Max(0f, blockSPCostPerHit);

        return Mathf.Max(0f, stats.maxSP * Mathf.Clamp01(blockReenterSPPercent));
    }

    public bool IsBlockLockedBySPRecovery()
    {
        return blockLockedByLowSP;
    }

    void ClearAttackMoveSpeedOverrideInternal()
    {
        useAttackMoveSpeedOverride = false;
        attackMoveSpeedOverride = 0f;
    }

    void SetWeaponTrailActive(bool active)
    {
        if (weaponTrailVFX == null)
            return;

        if (active)
        {
            weaponTrailVFX.SetTrailSet(GetCurrentTrailSet());
            weaponTrailVFX.TrailOn();
        }
        else
            weaponTrailVFX.TrailOff();
    }

    PlayerWeaponTrailController.TrailSet GetCurrentTrailSet()
    {
        if (sprintAttackActive)
            return PlayerWeaponTrailController.TrailSet.Sprint;

        if (heavyAttackActive || chargedAttackActive)
            return PlayerWeaponTrailController.TrailSet.Heavy;

        return PlayerWeaponTrailController.TrailSet.Normal;
    }

    void PlayPlayerAttackStartSFX()
    {
        if (combatAudioController == null)
            return;

        combatAudioController.PlayAttackStart();
    }

    void PlayPlayerAttackHitSFX()
    {
        if (combatAudioController == null)
            return;

        combatAudioController.PlayAttackHit();
    }

    void PlayPlayerAttackMissSFX()
    {
        if (combatAudioController == null)
            return;

        combatAudioController.PlayAttackMiss();
    }

    void PlayPlayerBlockedHitSFX()
    {
        if (combatAudioController == null)
            return;

        combatAudioController.PlayBlockedHit();
    }

    void PlayPlayerHurtSFX()
    {
        if (combatAudioController == null)
            return;

        combatAudioController.PlayHurt();
    }

    void PlayPlayerDieVoice()
    {
        if (combatAudioController == null)
            return;

        combatAudioController.PlayDieVoice();
    }

    void PlayPlayerBerserkVoice()
    {
        if (combatAudioController == null)
            return;

        combatAudioController.PlayBerserkVoice();
    }

    void PlayPlayerHitEffect(Collider hitCollider)
    {
        if (hitVFXController == null || hitCollider == null)
            return;

        Vector3 origin = attackPoint != null ? attackPoint.position : transform.position;
        Vector3 hitPosition = hitCollider.ClosestPoint(origin);
        if ((hitPosition - origin).sqrMagnitude < 0.0001f)
            hitPosition = hitCollider.bounds.center;

        Vector3 hitNormal = hitPosition - origin;
        if (hitNormal.sqrMagnitude < 0.0001f)
            hitNormal = -transform.forward;

        hitVFXController.PlayHitEffect(hitPosition, hitNormal);
    }

    void TryPlayPlayerAttackHitShake()
    {
        ScreenShakeController shakeController = ScreenShakeController.Instance;
        if (shakeController == null)
            return;

        if (heavyAttackActive)
        {
            shakeController.PlayPlayerHeavyHitShake();
            return;
        }

        if (!sprintAttackActive && currentAttackStep == 4)
            shakeController.PlayPlayerAttack4HitShake();
    }

    void TriggerPlayerHitStop()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (hitStopActive)
            return;

        int frames = Mathf.Max(0, playerHitStopFrames);
        if (frames <= 0)
            return;

        StartCoroutine(PlayerHitStopCoroutine(frames));
    }

    IEnumerator PlayerHitStopCoroutine(int frames)
    {
        hitStopActive = true;

        hitStopPreviousTimeScale = Time.timeScale;
        hitStopPreviousFixedDeltaTime = Time.fixedDeltaTime;

        Time.timeScale = 0f;
        Time.fixedDeltaTime = 0f;

        for (int i = 0; i < frames; i++)
            yield return new WaitForEndOfFrame();

        RestorePlayerHitStopTimeScale();
    }

    void RestorePlayerHitStopTimeScale()
    {
        if (!hitStopActive)
            return;

        Time.timeScale = hitStopPreviousTimeScale;
        Time.fixedDeltaTime = hitStopPreviousFixedDeltaTime;
        hitStopActive = false;
    }

    void InitializeBerserkVFX()
    {
        SetEffectObjectsActive(berserkEnterVFX, false, restartParticles: false);
        SetEffectObjectsActive(berserkActiveVFX, false, restartParticles: false);
    }

    void HandleBerserkStarted()
    {
        PlayBerserkEnterVFX();
        SetBerserkActiveVFX(true);
    }

    void HandleBerserkEnded()
    {
        SetEffectObjectsActive(berserkEnterVFX, false, restartParticles: false);
        SetBerserkActiveVFX(false);
    }

    void PlayBerserkEnterVFX()
    {
        if (berserkEnterVFX == null)
            return;

        for (int i = 0; i < berserkEnterVFX.Length; i++)
        {
            GameObject effectObject = berserkEnterVFX[i];
            if (effectObject == null)
                continue;

            effectObject.SetActive(true);
            RestartEffectParticles(effectObject);
            StartCoroutine(DisableEffectObjectAfterDelay(effectObject, GetEffectLifetime(effectObject)));
        }
    }

    void SetBerserkActiveVFX(bool active)
    {
        SetEffectObjectsActive(berserkActiveVFX, active, restartParticles: active);
    }

    void SetEffectObjectsActive(GameObject[] effectObjects, bool active, bool restartParticles)
    {
        if (effectObjects == null)
            return;

        for (int i = 0; i < effectObjects.Length; i++)
        {
            GameObject effectObject = effectObjects[i];
            if (effectObject == null)
                continue;

            effectObject.SetActive(active);

            if (!active)
            {
                StopEffectParticles(effectObject);
                continue;
            }

            if (restartParticles)
                RestartEffectParticles(effectObject);
        }
    }

    void RestartEffectParticles(GameObject effectObject)
    {
        ParticleSystem[] particleSystems = effectObject.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Play(true);
        }
    }

    void StopEffectParticles(GameObject effectObject)
    {
        ParticleSystem[] particleSystems = effectObject.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    float GetEffectLifetime(GameObject effectObject)
    {
        float longestLifetime = 0f;
        ParticleSystem[] particleSystems = effectObject.GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem.MainModule main = particleSystems[i].main;
            float startLifetime = main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants
                ? main.startLifetime.constantMax
                : main.startLifetime.constant;

            float totalLifetime = main.duration + Mathf.Max(0f, startLifetime);
            if (totalLifetime > longestLifetime)
                longestLifetime = totalLifetime;
        }

        if (longestLifetime > 0f)
            return longestLifetime;

        return Mathf.Max(0.1f, berserkEnterVFXFallbackLifetime);
    }

    IEnumerator DisableEffectObjectAfterDelay(GameObject effectObject, float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, delay));

        if (effectObject == null)
            yield break;

        StopEffectParticles(effectObject);
        effectObject.SetActive(false);
    }

    void ClearRollMoveSpeedOverrideInternal()
    {
        useRollMoveSpeedOverride = false;
        rollMoveSpeedOverride = 0f;
    }

    void StopRollVFX()
    {
        if (rollAfterImageVFX == null)
            return;

        rollAfterImageVFX.StopAfterImage();
    }

    void EndRollState()
    {
        if (animator != null && restoreAnimatorRootMotionAfterLockOnRoll)
            animator.applyRootMotion = cachedAnimatorApplyRootMotion;

        StopRollVFX();
        lockOnRollUsesScriptMotion = false;
        restoreAnimatorRootMotionAfterLockOnRoll = false;
        isRolling = false;
        ClearRollMoveSpeedOverrideInternal();
    }

    void EndAttackState()
    {
        isAttacking = false;
        sprintAttackActive = false;
        heavyAttackActive = false;
        chargedAttackActive = false;
        queueNextAttack = false;
        currentAttackStep = 0;
        canMoveCancelAttack = false;
        moveWasHeldWhenCancelWindowOpened = false;
        attackWindowActive = false;
        attackHitConfirmedThisWindow = false;
        avoidWindowOpen = false;
        ClearAttackMoveSpeedOverrideInternal();
        ClearCurrentAttackData();
        hitTargetsThisSwing.Clear();
        lastAttackFinishedTime = Time.time;
        SetWeaponTrailActive(false);

        animator.SetBool(queueNextAttackParam, false);
    }

    void ClearAttackStateForActionStart()
    {
        isAttacking = false;
        sprintAttackActive = false;
        heavyAttackActive = false;
        chargedAttackActive = false;
        queueNextAttack = false;
        currentAttackStep = 0;
        canMoveCancelAttack = false;
        moveWasHeldWhenCancelWindowOpened = false;
        attackWindowActive = false;
        attackHitConfirmedThisWindow = false;
        avoidWindowOpen = false;
        ClearAttackMoveSpeedOverrideInternal();
        ClearCurrentAttackData();
        hitTargetsThisSwing.Clear();
        SetWeaponTrailActive(false);
        animator.SetBool(queueNextAttackParam, false);
    }

    void ClearActionStateForHit(bool keepBlocking, bool keepSprint)
    {
        EndRollState();
        isAttacking = false;
        sprintAttackActive = false;
        heavyAttackActive = false;
        chargedAttackActive = false;
        queueNextAttack = false;
        currentAttackStep = 0;
        canMoveCancelAttack = false;
        moveWasHeldWhenCancelWindowOpened = false;
        attackWindowActive = false;
        attackHitConfirmedThisWindow = false;
        avoidWindowOpen = false;
        ClearAttackMoveSpeedOverrideInternal();
        ClearCurrentAttackData();
        hitTargetsThisSwing.Clear();
        SetWeaponTrailActive(false);
        animator.SetBool(queueNextAttackParam, false);

        if (!keepSprint)
            sprintMode = false;

        if (!keepBlocking)
            StopBlockingInternal();
    }

    PlayerAttackData GetAttackDataForStep(int step)
    {
        switch (step)
        {
            case 1: return attack1Data;
            case 2: return attack2Data;
            case 3: return attack3Data;
            case 4: return attack4Data;
            default: return null;
        }
    }

    void SetCurrentAttackData(PlayerAttackData data)
    {
        currentAttackData = data;
    }

    void ClearCurrentAttackData()
    {
        currentAttackData = null;
    }

    float GetCurrentAttackDamage()
    {
        float baseDamage = fallbackAttackDamage;

        if (currentAttackData != null)
            baseDamage = currentAttackData.damage;

        if (isBerserkActive)
            baseDamage *= berserkDamageMultiplier;

        return Mathf.Max(0f, baseDamage);
    }

    float GetCurrentAPGainPerHit()
    {
        float baseGain = 0f;

        if (currentAttackData != null)
            baseGain = currentAttackData.apGainPerHit;

        if (isBerserkActive)
            baseGain *= berserkAPGainMultiplier;

        return Mathf.Max(0f, baseGain);
    }

    float GetCurrentAttackRadius()
    {
        float radius = Mathf.Max(0f, attackRadius);

        if (isBerserkActive)
            radius *= berserkAttackRadiusMultiplier;

        return radius;
    }

    bool HasEnoughSPForAction(PlayerAttackData data)
    {
        if (stats == null || data == null)
            return true;

        return stats.HasEnoughSP(data.spCost);
    }

    bool TrySpendSPForAction(PlayerAttackData data)
    {
        if (stats == null || data == null)
            return true;

        return stats.SpendSP(data.spCost);
    }

    bool TrySpendSP(float amount)
    {
        if (stats == null)
            return true;

        return stats.SpendSP(amount);
    }

    public float GetIncomingDamageMultiplier()
    {
        if (isBerserkActive)
            return 1f;

        if (!isBlocking || isDead || IsBlockLockedBySPRecovery())
            return 1f;

        if (!CanBlockCurrentHit())
            return 1f;

        return Mathf.Clamp01(blockDamageMultiplier);
    }

    public float ModifyIncomingDamage(float incomingDamage)
    {
        return Mathf.Max(0f, incomingDamage) * GetIncomingDamageMultiplier();
    }

    public float GetBlockSPCostPerHit()
    {
        return Mathf.Max(0f, blockSPCostPerHit);
    }
}
