using UnityEngine;

public partial class PlayerController
{
    void UpdateAnimatorParams(bool lockedOn)
    {
        animator.SetBool(isLockedOnParam, lockedOn);
        animator.SetBool(isBlockingParam, isBlocking);
        animator.SetBool(isDeadParam, isDead);
        animator.SetBool(isSprintingParam, sprintMode);

        animator.SetFloat(speedParam, currentSpeed, 0.1f, Time.deltaTime);
        animator.SetFloat(moveXParam, currentMoveX, 0.08f, Time.deltaTime);
        animator.SetFloat(moveYParam, currentMoveY, 0.08f, Time.deltaTime);
    }

    void SyncRollingStateFromAnimator()
    {
        bool wasRolling = isRolling;
        isRolling = IsInRollAnimation();

        if (wasRolling && !isRolling)
            EndRollState();
    }

    void SyncHitReactionStateFromAnimator()
    {
        isInHitReaction = IsInHitReactionAnimation();
    }

    void SyncPowerUpStateFromAnimator()
    {
        if (animator == null)
            return;

        bool inPowerUp = IsInPowerUpAnimation();

        if (isPoweringUp && !inPowerUp)
            isPoweringUp = false;
    }

    bool IsLockedOn()
    {
        return playerLockOn != null && playerLockOn.HasTarget();
    }

    bool IsInCombatLikeAnimation()
    {
        if (animator == null) return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);

            if (current.IsTag("Attack") || current.IsTag("Block") || current.IsTag("Roll") || current.IsTag("Hit") ||
                next.IsTag("Attack") || next.IsTag("Block") || next.IsTag("Roll") || next.IsTag("Hit"))
            {
                return true;
            }
        }

        return current.IsTag("Attack") || current.IsTag("Block") || current.IsTag("Roll") || current.IsTag("Hit");
    }

    bool IsInRollAnimation()
    {
        if (animator == null) return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            if (current.IsTag("Roll") || next.IsTag("Roll"))
                return true;
        }

        return current.IsTag("Roll");
    }

    bool IsInHitReactionAnimation()
    {
        if (animator == null) return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            if (current.IsTag("Hit") || next.IsTag("Hit"))
                return true;
        }

        return current.IsTag("Hit");
    }

    bool IsInPowerUpAnimation()
    {
        if (animator == null)
            return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);

        bool currentMatch = (!string.IsNullOrEmpty(powerUpStateTag) && current.IsTag(powerUpStateTag)) ||
                            (!string.IsNullOrEmpty(powerUpStateName) && current.IsName(powerUpStateName));

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            bool nextMatch = (!string.IsNullOrEmpty(powerUpStateTag) && next.IsTag(powerUpStateTag)) ||
                             (!string.IsNullOrEmpty(powerUpStateName) && next.IsName(powerUpStateName));

            return currentMatch || nextMatch;
        }

        return currentMatch;
    }

    void SyncAttackStateFromAnimator()
    {
        if (animator == null) return;

        bool inAttackAnim = IsInAttackAnimation();

        if (isAttacking && !inAttackAnim)
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
    }

    bool IsInAttackAnimation()
    {
        if (animator == null) return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            return current.IsTag("Attack") || next.IsTag("Attack");
        }

        return current.IsTag("Attack");
    }

    void TriggerCommonHitReaction()
    {
        if (animator == null || string.IsNullOrEmpty(hitTriggerParam))
            return;

        animator.ResetTrigger(hitTriggerParam);
        animator.SetTrigger(hitTriggerParam);
    }

    void TriggerAnimatorTrigger(string triggerName)
    {
        if (animator == null || string.IsNullOrEmpty(triggerName))
            return;

        animator.ResetTrigger(hitSmallTriggerParam);
        animator.ResetTrigger(hitBigTriggerParam);
        animator.ResetTrigger(blockedHitTriggerParam);
        animator.ResetTrigger(attackTriggerParam);
        animator.ResetTrigger(heavyAttackTriggerParam);
        animator.ResetTrigger(chargedAttackTriggerParam);
        animator.ResetTrigger(sprintAttackTriggerParam);
        animator.ResetTrigger(rollTriggerParam);
        animator.ResetTrigger(powerUpTriggerParam);
        animator.ResetTrigger(interruptAttackParam);

        animator.SetTrigger(triggerName);
    }

    public void AE_BeginAttackWindow()
    {
        hitTargetsThisSwing.Clear();
        attackHitConfirmedThisWindow = false;
        attackWindowActive = true;
    }

    public void AE_EndAttackWindow()
    {
        if (attackWindowActive && !attackHitConfirmedThisWindow)
            PlayPlayerAttackMissSFX();

        attackWindowActive = false;
        attackHitConfirmedThisWindow = false;
    }

    public void AE_PlayAttackSFX()
    {
        PlayPlayerAttackStartSFX();
    }

    public void AE_SlashVFXOn()
    {
        SetWeaponTrailActive(true);
    }

    public void AE_SlashVFXOff()
    {
        SetWeaponTrailActive(false);
    }

    public void AE_DoAttackHit()
    {
        AE_BeginAttackWindow();
    }

    public void AE_SetAttackMoveSpeed(float speed)
    {
        useAttackMoveSpeedOverride = true;
        attackMoveSpeedOverride = Mathf.Max(0f, speed);
    }

    public void AE_ClearAttackMoveSpeed()
    {
        ClearAttackMoveSpeedOverrideInternal();
    }

    public void AE_SetSprintAttackMoveSpeed()
    {
        useAttackMoveSpeedOverride = true;
        attackMoveSpeedOverride = sprintAttackDefaultMoveSpeed;
    }

    public void AE_SetRollMoveSpeed(float speed)
    {
        useRollMoveSpeedOverride = true;
        rollMoveSpeedOverride = Mathf.Max(0f, speed);
    }

    public void AE_ClearRollMoveSpeed()
    {
        ClearRollMoveSpeedOverrideInternal();
    }

    public void AE_EndRoll()
    {
        EndRollState();
    }

    public void AE_EnableMoveCancel()
    {
        if (!isAttacking) return;

        canMoveCancelAttack = true;
        moveWasHeldWhenCancelWindowOpened = IsMoveHeld();
    }

    public void AE_DisableMoveCancel()
    {
        canMoveCancelAttack = false;
        moveWasHeldWhenCancelWindowOpened = false;
    }

    public void AvoidWindowsOn()
    {
        avoidWindowOpen = true;
    }

    public void AvoidWindowsOff()
    {
        avoidWindowOpen = false;
    }

    public void AE_EnteredAttackStep2()
    {
        CorrectFacingBeforeLockOnAttack();
        CacheStandardAttackMotionDirection();

        currentAttackStep = 2;
        sprintAttackActive = false;
        heavyAttackActive = false;
        chargedAttackActive = false;
        queueNextAttack = false;
        canMoveCancelAttack = false;
        moveWasHeldWhenCancelWindowOpened = false;
        attackWindowActive = false;
        attackHitConfirmedThisWindow = false;
        ClearAttackMoveSpeedOverrideInternal();
        hitTargetsThisSwing.Clear();
        SetCurrentAttackData(attack2Data);
        TrySpendSPForAction(attack2Data);
        animator.SetBool(queueNextAttackParam, false);
    }

    public void AE_EnteredAttackStep3()
    {
        CorrectFacingBeforeLockOnAttack();
        CacheStandardAttackMotionDirection();

        currentAttackStep = 3;
        sprintAttackActive = false;
        heavyAttackActive = false;
        chargedAttackActive = false;
        queueNextAttack = false;
        canMoveCancelAttack = false;
        moveWasHeldWhenCancelWindowOpened = false;
        attackWindowActive = false;
        attackHitConfirmedThisWindow = false;
        ClearAttackMoveSpeedOverrideInternal();
        hitTargetsThisSwing.Clear();
        SetCurrentAttackData(attack3Data);
        TrySpendSPForAction(attack3Data);
        animator.SetBool(queueNextAttackParam, false);
    }

    public void AE_EnteredAttackStep4()
    {
        CorrectFacingBeforeLockOnAttack();
        CacheStandardAttackMotionDirection();

        currentAttackStep = 4;
        sprintAttackActive = false;
        heavyAttackActive = false;
        chargedAttackActive = false;
        queueNextAttack = false;
        canMoveCancelAttack = false;
        moveWasHeldWhenCancelWindowOpened = false;
        attackWindowActive = false;
        attackHitConfirmedThisWindow = false;
        ClearAttackMoveSpeedOverrideInternal();
        hitTargetsThisSwing.Clear();
        SetCurrentAttackData(attack4Data);
        TrySpendSPForAction(attack4Data);
        animator.SetBool(queueNextAttackParam, false);
    }

    public void AE_EndAttack()
    {
        if (queueNextAttack && currentAttackStep < 4)
            return;

        EndAttackState();
    }

    public void AE_EndSprintAttack()
    {
        EndAttackState();
    }
}
