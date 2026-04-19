using UnityEngine;

public class BossRuntime : MonoBehaviour
{
    [Header("Config")]
    public BossConfig config;

    [Header("HP")]
    public float maxHP;
    public float currentHP;

    [Header("RV")]
    public float maxRV;
    public float currentRV;

    [Header("State")]
    public BOSSAI.BossState currentState = BOSSAI.BossState.Idle;
    public bool isDead;
    public bool isKneeling;
    public int currentAttackIndex = -1;
    public BossAttackPhase currentAttackPhase = BossAttackPhase.None;
    public Transform currentTarget;

    public void Initialize(BossConfig sourceConfig, Transform target)
    {
        if (sourceConfig != null)
            config = sourceConfig;

        currentTarget = target;

        float configuredMaxHP = config != null ? config.maxHP : 0f;
        float configuredMaxRV = config != null ? config.maxRV : 0f;

        maxHP = Mathf.Max(1f, maxHP > 0f ? maxHP : configuredMaxHP);
        maxRV = Mathf.Max(1f, maxRV > 0f ? maxRV : configuredMaxRV);

        currentHP = Mathf.Clamp(currentHP > 0f ? currentHP : maxHP, 0f, maxHP);
        currentRV = Mathf.Clamp(currentRV > 0f ? currentRV : maxRV, 0f, maxRV);

        isDead = currentHP <= 0f;
        currentState = isDead ? BOSSAI.BossState.Dead : BOSSAI.BossState.Idle;
        currentAttackIndex = -1;
        currentAttackPhase = BossAttackPhase.None;
        isKneeling = false;
    }

    void OnValidate()
    {
        maxHP = Mathf.Max(0f, maxHP);
        maxRV = Mathf.Max(0f, maxRV);

        if (maxHP > 0f)
            currentHP = Mathf.Clamp(currentHP, 0f, maxHP);
        else
            currentHP = Mathf.Max(0f, currentHP);

        if (maxRV > 0f)
            currentRV = Mathf.Clamp(currentRV, 0f, maxRV);
        else
            currentRV = Mathf.Max(0f, currentRV);
    }

    public void SetState(BOSSAI.BossState state)
    {
        currentState = state;
        isDead = state == BOSSAI.BossState.Dead;
    }

    public void SetAttackRuntime(int attackIndex, BossAttackPhase phase)
    {
        currentAttackIndex = attackIndex;
        currentAttackPhase = phase;
    }

    public void ClearAttackRuntime()
    {
        currentAttackIndex = -1;
        currentAttackPhase = BossAttackPhase.None;
    }

    public void TakeDamage(float amount)
    {
        currentHP = Mathf.Max(0f, currentHP - Mathf.Max(0f, amount));
        if (currentHP <= 0f)
            SetState(BOSSAI.BossState.Dead);
    }

    public void SetRV(float value)
    {
        currentRV = Mathf.Clamp(value, 0f, maxRV);
    }

    public float GetHPPercent()
    {
        return maxHP > 0f ? Mathf.Clamp01(currentHP / maxHP) : 0f;
    }

    public float GetRVPercent()
    {
        return maxRV > 0f ? Mathf.Clamp01(currentRV / maxRV) : 0f;
    }
}
