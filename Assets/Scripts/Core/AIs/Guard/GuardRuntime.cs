using System;
using UnityEngine;
using UnityEngine.AI;

public class GuardRuntime : MonoBehaviour, IDamageable
{
    [Header("Config")]
    public GuardConfig config;

    [Header("Runtime")]
    public float hp;
    public bool isDead = false;

    private GuardAI guardAI;

    public event Action<float, float> OnHPChanged;
    public event Action OnDied;

    void Awake()
    {
        guardAI = GetComponent<GuardAI>();
        if (config == null && guardAI != null)
            config = guardAI.config;

        if (config == null)
        {
            Debug.LogError($"{name}: GuardRuntime 没有绑定 config!");
            hp = 1f;
            return;
        }

        hp = config.maxHP;
        OnHPChanged?.Invoke(hp, config.maxHP);
    }

    public void TakeDamage(float amount, GameObject attacker = null)
    {
        if (isDead) return;

        hp = Mathf.Max(0f, hp - Mathf.Max(0f, amount));
        OnHPChanged?.Invoke(hp, config != null ? config.maxHP : 1f);

        if (hp <= 0f)
        {
            Die();
            return;
        }

        if (guardAI != null)
            guardAI.PlayHurtVoice();

        if (attacker != null && guardAI != null)
        {
            EnemyRuntime er = attacker.GetComponentInParent<EnemyRuntime>();
            if (er == null)
                er = attacker.GetComponentInChildren<EnemyRuntime>(true);

            if (er != null)
            {
                guardAI.NotifyBeingAttacked(er);
                return;
            }

            BOSSAI bossAI = attacker.GetComponentInParent<BOSSAI>();
            if (bossAI == null)
                bossAI = attacker.GetComponentInChildren<BOSSAI>(true);

            if (bossAI != null)
            {
                guardAI.NotifyBeingAttacked(bossAI);
            }
        }
    }

    void Die()
    {
        if (isDead) return;

        isDead = true;
        if (guardAI != null)
            guardAI.PlayDieVoice();

        StopCombatLogic();
        PlayDeathAnimation();

        OnDied?.Invoke();

        float destroyDelay = GetDestroyAfterDeathDelay();
        if (destroyDelay > 0f)
            Destroy(gameObject, destroyDelay);
    }

    void StopCombatLogic()
    {
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }

        if (guardAI != null)
            guardAI.enabled = false;
    }

    void PlayDeathAnimation()
    {
        Animator animator = GetComponent<Animator>();
        if (animator == null)
            return;

        GuardAnimationConfig animationConfig = config != null ? config.animation : null;
        string speedParam = animationConfig != null ? animationConfig.speedParam : "Speed";
        string attackTrigger = animationConfig != null ? animationConfig.attackTrigger : "Attack";
        string deathTrigger = animationConfig != null ? animationConfig.deathTriggerParam : "IsDead";
        string deathChoose = animationConfig != null ? animationConfig.deathChooseParam : "DeadChose";
        int deathIndex = animationConfig != null ? animationConfig.deathAnimationIndex : 0;

        animator.ResetTrigger(attackTrigger);
        animator.SetFloat(speedParam, 0f);
        animator.SetInteger(deathChoose, Mathf.Clamp(deathIndex, 0, 1));
        animator.SetTrigger(deathTrigger);
    }

    float GetDestroyAfterDeathDelay()
    {
        GuardAnimationConfig animationConfig = config != null ? config.animation : null;
        return animationConfig != null ? Mathf.Max(0f, animationConfig.destroyAfterDeathDelay) : 4f;
    }

    public bool CanHeal()
    {
        if (isDead || config == null)
            return false;

        return hp < config.maxHP;
    }

    public void Heal(float amount)
    {
        if (isDead || config == null)
            return;

        hp = Mathf.Min(config.maxHP, hp + Mathf.Max(0f, amount));
        OnHPChanged?.Invoke(hp, config.maxHP);
    }
}
