using UnityEngine;

public partial class PlayerController
{
    void HandleSprintToggleInput()
    {
        if (Input.GetKeyDown(sprintKey))
        {
            if (sprintMode)
            {
                ExitSprintMode();
                return;
            }

            if (isDead) return;
            if (isRolling) return;
            if (isInHitReaction) return;
            if (isBlocking) return;
            if (isPoweringUp) return;
            if (stats != null && sprintSPDrainPerSecond > 0f && !stats.HasEnoughSP(sprintSPDrainPerSecond * Time.deltaTime))
            {
                if (!stats.HasEnoughSP(0.1f))
                    return;
            }

            sprintMode = true;
        }
    }

    void ExitSprintMode()
    {
        sprintMode = false;
    }

    void HandleSprintSPDrain()
    {
        if (!sprintMode)
            return;

        if (isDead || isPoweringUp)
        {
            ExitSprintMode();
            return;
        }

        if (stats == null)
            return;

        float drainPerSecond = Mathf.Max(0f, sprintSPDrainPerSecond);
        if (drainPerSecond <= 0f)
            return;

        float drainAmount = drainPerSecond * Time.deltaTime;

        if (!stats.SpendSP(drainAmount))
        {
            ExitSprintMode();
        }
    }

    void HandleRollInput()
    {
        if (!Input.GetKeyDown(rollKey))
            return;

        if (isDead || isRolling || isInHitReaction || isPoweringUp)
            return;

        if (!CanStartRollNow())
            return;

        StartRoll();
    }

    void HandleBerserkInput()
    {
        if (!Input.GetKeyDown(berserkKey))
            return;

        if (!CanActivateBerserk())
            return;

        StartBerserk();
    }

    bool CanActivateBerserk()
    {
        if (stats == null) return false;
        if (isDead) return false;
        if (isBerserkActive) return false;
        if (isPoweringUp) return false;
        if (isRolling) return false;
        if (isAttacking) return false;
        if (isBlocking) return false;
        if (isInHitReaction) return false;
        if (!IsAPFull()) return false;
        if (!HasAnimatorParameter(powerUpTriggerParam)) return false;

        return true;
    }

    bool IsAPFull()
    {
        if (stats == null) return false;
        return stats.maxAP > 0f && stats.ap >= stats.maxAP - 0.01f;
    }

    void StartBerserk()
    {
        if (stats == null)
            return;

        if (berserkConsumesFullAP)
            stats.SpendAP(stats.maxAP);

        isBerserkActive = true;
        isPoweringUp = true;
        berserkEndTime = Time.time + berserkDuration;
        PlayPlayerBerserkVoice();
        HandleBerserkStarted();

        ExitSprintMode();
        StopBlockingInternal();
        ResetLeftMouseAttackTracking();
        ClearAttackStateForActionStart();

        animator.ResetTrigger(attackTriggerParam);
        animator.ResetTrigger(heavyAttackTriggerParam);
        animator.ResetTrigger(sprintAttackTriggerParam);
        animator.ResetTrigger(rollTriggerParam);
        animator.ResetTrigger(powerUpTriggerParam);
        animator.SetTrigger(powerUpTriggerParam);
    }

    void UpdateBerserkTimer()
    {
        if (!isBerserkActive)
            return;

        if (Time.time >= berserkEndTime)
        {
            isBerserkActive = false;
            isPoweringUp = false;
            HandleBerserkEnded();
        }
    }

    void StartRoll()
    {
        if (!TrySpendSP(rollSPCost))
            return;

        bool lockedOn = IsLockedOn();
        Vector2 moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );
        Vector3 currentVelocity = characterController != null ? characterController.velocity : Vector3.zero;
        RollDirection rollDirection = GetRollDirection(moveInput, currentVelocity, transform, lockedOn);

        isRolling = true;
        cachedRollMotionDirection = GetWorldDirectionForRollDirection(rollDirection);
        useRollMoveSpeedOverride = true;
        rollMoveSpeedOverride = lockedOn ? lockOnRollMoveSpeed : rollDefaultMoveSpeed;
        lockOnRollUsesScriptMotion = lockedOn;

        if (animator != null)
        {
            cachedAnimatorApplyRootMotion = animator.applyRootMotion;
            restoreAnimatorRootMotionAfterLockOnRoll = lockedOn;

            if (lockedOn)
                animator.applyRootMotion = false;
        }

        ExitSprintMode();
        StopBlockingInternal();
        ResetLeftMouseAttackTracking();

        ClearAttackStateForActionStart();

        animator.ResetTrigger(attackTriggerParam);
        animator.ResetTrigger(heavyAttackTriggerParam);
        animator.ResetTrigger(chargedAttackTriggerParam);
        animator.ResetTrigger(sprintAttackTriggerParam);
        animator.ResetTrigger(rollTriggerParam);
        if (HasAnimatorParameter(rollDirectionParam))
            animator.SetInteger(rollDirectionParam, (int)rollDirection);
        animator.SetTrigger(rollTriggerParam);
    }

    void HandleCombatInput()
    {
        if (isRolling || isInHitReaction || isPoweringUp || IsInPowerUpAnimation())
        {
            ResetRightMouseAttackTracking();
            return;
        }

        bool wantsBlock = Input.GetKey(blockKey);

        if (!isAttacking)
        {
            if (wantsBlock)
            {
                ExitSprintMode();
                ResetRightMouseAttackTracking();

                if (CanStartBlockingNow())
                    StartOrMaintainBlock();
                else
                    StopBlockingInternal();
            }
            else
            {
                StopBlockingInternal();
            }
        }
        else
        {
            StopBlockingInternal();
        }

        HandleLeftMouseAttackInput();
        HandleRightMouseAttackInput();
    }

    bool TryExecuteBoss()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            4f,
            targetLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (var col in hits)
        {
            if (col == null) continue;

            if (IsOwnCollider(col))
                continue;

            BossStaggerSystem boss = col.GetComponentInParent<BossStaggerSystem>();
            if (boss == null) continue;

            if (boss.TryExecute(transform))
                return true;
        }

        return false;
    }

    float GetCurrentAttackStaggerValue()
    {
        if (chargedAttackActive)
            return 35f;

        if (heavyAttackActive)
            return 25f;

        if (sprintAttackActive)
            return 20f;

        switch (currentAttackStep)
        {
            case 1: return 10f;
            case 2: return 10f;
            case 3: return 10f;
            case 4: return 15f;
        }

        return 10f;
    }

    BossStaggerSystem.PlayerHitType GetCurrentBossHitType()
    {
        if (chargedAttackActive || heavyAttackActive)
            return BossStaggerSystem.PlayerHitType.Heavy;

        if (sprintAttackActive)
            return BossStaggerSystem.PlayerHitType.Sprint;

        return BossStaggerSystem.PlayerHitType.Normal;
    }

    void HandleLeftMouseAttackInput()
    {
        if (TryExecuteBoss())
            return;

        if (isDead || isBlocking || isPoweringUp || IsInPowerUpAnimation())
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        if (!isAttacking)
        {
            if (CanStartSprintAttack())
            {
                StartSprintAttack();
                return;
            }

            StartFirstAttack();
            return;
        }

        if (!sprintAttackActive && !heavyAttackActive && currentAttackStep < 4)
        {
            int nextStep = currentAttackStep + 1;
            PlayerAttackData nextAttack = GetAttackDataForStep(nextStep);

            if (HasEnoughSPForAction(nextAttack))
            {
                queueNextAttack = true;
                animator.SetBool(queueNextAttackParam, true);
            }
        }
    }

    void ResetLeftMouseAttackTracking()
    {
    }

    void HandleRightMouseAttackInput()
    {
        if (isDead || isBlocking || isPoweringUp || IsInPowerUpAnimation())
        {
            ResetRightMouseAttackTracking();
            return;
        }

        if (Input.GetMouseButtonDown(1))
        {
            rightMouseTracking = true;
            chargedAttackTriggeredThisPress = false;
            rightMousePressedTime = Time.time;

            if (!isAttacking)
                StartHeavyAttack();
        }

        if (rightMouseTracking &&
            !chargedAttackTriggeredThisPress &&
            Input.GetMouseButton(1) &&
            Time.time - rightMousePressedTime >= heavyAttackHoldTime)
        {
            if (StartChargedAttack())
                chargedAttackTriggeredThisPress = true;
        }

        if (Input.GetMouseButtonUp(1))
            ResetRightMouseAttackTracking();
    }

    void ResetRightMouseAttackTracking()
    {
        rightMouseTracking = false;
        chargedAttackTriggeredThisPress = false;
        rightMousePressedTime = -999f;
    }

    bool CanStartBlockingNow()
    {
        if (isDead || isRolling || isAttacking || isPoweringUp)
            return false;

        if (IsBlockLockedBySPRecovery())
            return false;

        return true;
    }

    void StartOrMaintainBlock()
    {
        if (isBlocking)
            return;

        isBlocking = true;
        animator.SetBool(isBlockingParam, true);
    }

    void StopBlockingInternal()
    {
        if (!isBlocking)
            return;

        isBlocking = false;
        animator.SetBool(isBlockingParam, false);
    }

    bool CanStartSprintAttack()
    {
        if (!sprintMode) return false;

        Vector2 moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        return moveInput.sqrMagnitude > 0.0001f;
    }

    void StartSprintAttack()
    {
        if (!TrySpendSPForAction(sprintAttackData))
            return;

        isAttacking = true;
        sprintAttackActive = true;
        heavyAttackActive = false;
        chargedAttackActive = false;
        queueNextAttack = false;
        currentAttackStep = 0;
        canMoveCancelAttack = false;
        moveWasHeldWhenCancelWindowOpened = false;
        attackWindowActive = false;
        attackHitConfirmedThisWindow = false;
        hitTargetsThisSwing.Clear();
        SetCurrentAttackData(sprintAttackData);

        useAttackMoveSpeedOverride = true;
        attackMoveSpeedOverride = sprintAttackDefaultMoveSpeed;
        CacheSprintAttackMotionDirection();

        ExitSprintMode();
        StopBlockingInternal();

        animator.SetBool(queueNextAttackParam, false);
        animator.ResetTrigger(attackTriggerParam);
        animator.ResetTrigger(heavyAttackTriggerParam);
        animator.ResetTrigger(chargedAttackTriggerParam);
        animator.ResetTrigger(sprintAttackTriggerParam);
        animator.SetTrigger(sprintAttackTriggerParam);
    }

    bool StartHeavyAttack()
    {
        if (!TrySpendSPForAction(heavyAttackData))
            return false;

        isAttacking = true;
        sprintAttackActive = false;
        heavyAttackActive = true;
        chargedAttackActive = false;
        queueNextAttack = false;
        currentAttackStep = 0;
        canMoveCancelAttack = false;
        moveWasHeldWhenCancelWindowOpened = false;
        attackWindowActive = false;
        attackHitConfirmedThisWindow = false;
        hitTargetsThisSwing.Clear();
        SetCurrentAttackData(heavyAttackData);

        ClearAttackMoveSpeedOverrideInternal();

        ExitSprintMode();
        StopBlockingInternal();

        animator.SetBool(queueNextAttackParam, false);
        animator.ResetTrigger(attackTriggerParam);
        animator.ResetTrigger(heavyAttackTriggerParam);
        animator.ResetTrigger(chargedAttackTriggerParam);
        animator.ResetTrigger(sprintAttackTriggerParam);

        CorrectFacingBeforeLockOnAttack();
        CacheStandardAttackMotionDirection();

        animator.SetTrigger(heavyAttackTriggerParam);
        return true;
    }

    bool StartChargedAttack()
    {
        if (isDead || isRolling || isInHitReaction || isPoweringUp)
            return false;

        if (!TrySpendSPForAction(chargedAttackData))
            return false;

        isAttacking = true;
        sprintAttackActive = false;
        heavyAttackActive = false;
        chargedAttackActive = true;
        queueNextAttack = false;
        currentAttackStep = 0;
        canMoveCancelAttack = false;
        moveWasHeldWhenCancelWindowOpened = false;
        attackWindowActive = false;
        attackHitConfirmedThisWindow = false;
        hitTargetsThisSwing.Clear();
        SetCurrentAttackData(chargedAttackData);

        ClearAttackMoveSpeedOverrideInternal();

        ExitSprintMode();
        StopBlockingInternal();

        animator.SetBool(queueNextAttackParam, false);
        animator.ResetTrigger(attackTriggerParam);
        animator.ResetTrigger(heavyAttackTriggerParam);
        animator.ResetTrigger(chargedAttackTriggerParam);
        animator.ResetTrigger(sprintAttackTriggerParam);

        CorrectFacingBeforeLockOnAttack();
        CacheStandardAttackMotionDirection();

        animator.SetTrigger(chargedAttackTriggerParam);
        return true;
    }

    void StartFirstAttack()
    {
        if (!TrySpendSPForAction(attack1Data))
            return;

        if (Time.time - lastAttackFinishedTime > comboResetTime)
            currentAttackStep = 0;

        isAttacking = true;
        sprintAttackActive = false;
        heavyAttackActive = false;
        chargedAttackActive = false;
        queueNextAttack = false;
        currentAttackStep = 1;
        canMoveCancelAttack = false;
        moveWasHeldWhenCancelWindowOpened = false;
        attackWindowActive = false;
        attackHitConfirmedThisWindow = false;
        hitTargetsThisSwing.Clear();
        SetCurrentAttackData(attack1Data);

        ClearAttackMoveSpeedOverrideInternal();

        ExitSprintMode();
        StopBlockingInternal();

        animator.SetBool(queueNextAttackParam, false);
        animator.ResetTrigger(attackTriggerParam);
        animator.ResetTrigger(heavyAttackTriggerParam);
        animator.ResetTrigger(chargedAttackTriggerParam);

        CorrectFacingBeforeLockOnAttack();
        CacheStandardAttackMotionDirection();

        animator.SetTrigger(attackTriggerParam);
    }

    void HandleAttackMoveCancel()
    {
        if (!isAttacking) return;
        if (!canMoveCancelAttack) return;
        if (isRolling) return;
        if (isPoweringUp) return;

        bool moveHeldNow = IsMoveHeld();

        if (moveWasHeldWhenCancelWindowOpened)
        {
            if (!moveHeldNow)
                moveWasHeldWhenCancelWindowOpened = false;
            return;
        }

        bool moveJustPressed = moveHeldNow && !previousMoveHeld;
        if (!moveJustPressed) return;

        InterruptAttackByMovement();
    }

    void InterruptAttackByMovement()
    {
        ClearActionStateForHit(keepBlocking: false, keepSprint: true);
        lastAttackFinishedTime = Time.time;
        ResetLeftMouseAttackTracking();

        animator.SetBool(queueNextAttackParam, false);

        if (HasAnimatorParameter(interruptAttackParam))
        {
            animator.ResetTrigger(attackTriggerParam);
            animator.ResetTrigger(heavyAttackTriggerParam);
            animator.ResetTrigger(sprintAttackTriggerParam);
            animator.SetTrigger(interruptAttackParam);
        }
    }

    void ProcessActiveAttackHitbox()
    {
        if (!attackWindowActive) return;
        if (attackPoint == null) return;

        Collider[] hits = Physics.OverlapSphere(
            attackPoint.position,
            GetCurrentAttackRadius(),
            targetLayers,
            QueryTriggerInteraction.Ignore
        );

        Debug.Log("Player hitbox count = " + hits.Length);

        float damage = GetCurrentAttackDamage();
        float apGainPerHit = GetCurrentAPGainPerHit();

        foreach (Collider col in hits)
        {
            if (col == null) continue;

            if (IsOwnCollider(col))
                continue;

            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;

            if (IsOwnDamageable(damageable))
                continue;

            if (hitTargetsThisSwing.Contains(damageable))
                continue;

            EnemyRuntime enemyRuntime = (damageable as EnemyRuntime) ?? col.GetComponentInParent<EnemyRuntime>();
            BossStaggerSystem boss = col.GetComponentInParent<BossStaggerSystem>();

            bool isValidEnemyTarget = enemyRuntime != null || boss != null;
            if (!isValidEnemyTarget)
                continue;

            bool firstHitThisWindow = !attackHitConfirmedThisWindow;
            hitTargetsThisSwing.Add(damageable);

            EnemyAnimationController enemyAnim = col.GetComponentInParent<EnemyAnimationController>();
            bool wasAliveBeforeHit = enemyRuntime != null && !enemyRuntime.IsDead();
            bool shouldTriggerHeavyHit = (heavyAttackActive || chargedAttackActive) && enemyAnim != null && wasAliveBeforeHit;

            damageable.TakeDamage(damage, gameObject);
            HealOnHitIfBerserk();
            PlayPlayerHitEffect(col);

            if (boss != null)
                boss.TakeStaggerDamage(GetCurrentAttackStaggerValue(), GetCurrentBossHitType());

            if (shouldTriggerHeavyHit)
                enemyAnim.PlayHeavyHitReaction();

            if (stats != null && apGainPerHit > 0f)
                stats.GainAP(apGainPerHit);

            if (stats != null && enemyRuntime != null && wasAliveBeforeHit && enemyRuntime.IsDead())
                stats.GrantKillRewards();

            if (firstHitThisWindow)
            {
                attackHitConfirmedThisWindow = true;
                PlayPlayerAttackHitSFX();
                TryPlayPlayerAttackHitShake();
                TriggerPlayerHitStop();
            }
        }
    }

    void HealOnHitIfBerserk()
    {
        if (!isBerserkActive || stats == null)
            return;

        if (berserkHealPerAttack <= 0f)
            return;

        stats.Heal(berserkHealPerAttack);
    }

    public void ProcessIncomingHit(ref float damage)
    {
        if (isDead)
            return;

        float safeDamage = Mathf.Max(0f, damage);

        if (isBerserkActive)
        {
            damage = safeDamage;
            return;
        }

        TriggerCommonHitReaction();

        if (IsBlocking())
        {
            if (CanBlockCurrentHit())
            {
                ConsumeSPForBlockedHit();
                damage = safeDamage * Mathf.Clamp01(blockDamageMultiplier);
                PlayBlockedHitReaction();
                return;
            }

            BreakBlockFromInsufficientSP();
            damage = safeDamage;
            return;
        }

        PlaySmallHitReaction();
        damage = safeDamage;
    }

    bool CanBlockCurrentHit()
    {
        if (!isBlocking || isDead)
            return false;

        float cost = Mathf.Max(0f, blockSPCostPerHit);

        if (stats == null)
            return true;

        return stats.sp >= cost;
    }

    void ConsumeSPForBlockedHit()
    {
        float cost = Mathf.Max(0f, blockSPCostPerHit);
        if (cost <= 0f || stats == null)
            return;

        stats.SpendSP(cost);
    }

    void PlayBlockedHitReaction()
    {
        ClearActionStateForHit(keepBlocking: true, keepSprint: false);
        PlayPlayerBlockedHitSFX();
        TriggerAnimatorTrigger(blockedHitTriggerParam);
    }

    void PlaySmallHitReaction()
    {
        ClearActionStateForHit(keepBlocking: false, keepSprint: false);
        PlayPlayerHurtSFX();
        TriggerAnimatorTrigger(hitSmallTriggerParam);
    }

    void PlayBigHitReaction()
    {
        ClearActionStateForHit(keepBlocking: false, keepSprint: false);
        PlayPlayerHurtSFX();
        TriggerAnimatorTrigger(hitBigTriggerParam);
    }

    void BreakBlockFromInsufficientSP()
    {
        blockLockedByLowSP = true;
        PlayBigHitReaction();
    }

    bool IsOwnCollider(Collider col)
    {
        if (col == null)
            return false;

        return col.transform.root == transform.root;
    }

    bool IsOwnDamageable(IDamageable damageable)
    {
        if (damageable == null)
            return false;

        Component component = damageable as Component;
        if (component == null)
            return false;

        return component.transform.root == transform.root;
    }
}
