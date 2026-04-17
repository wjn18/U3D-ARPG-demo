using System;
using UnityEngine;

public class PlayerStatsRuntime : MonoBehaviour, IDamageable
{
    [Header("Config")]
    public PlayerStatsConfig statsConfig;

    [Header("Optional Tank Init")]
    public TankConfig tankConfigOnStart;

    [Header("Fallback Defaults (used if statsConfig is null)")]
    public float fallbackBaseMaxHP = 100f;
    public float fallbackHpPerLevel = 10f;

    public float fallbackBaseMaxSP = 100f;
    public float fallbackSpPerLevel = 5f;

    public int fallbackBaseExpToNext = 100;
    public float fallbackExpGrowthPow = 1.2f;

    [Header("AP")]
    public float fixedMaxAP = 100f;
    public float initialAP = 0f;

    [Header("SP Recovery")]
    public float spRecoveryDelay = 1f;
    public float spRecoveryAmountPerTick = 7f;
    public float spRecoveryTickInterval = 0.1f;

    [Header("Kill Rewards")]
    public float killHealPercentOfPlayerMaxHP = 0.01f;
    public float killRecoverSPFlat = 20f;

    public int level { get; private set; } = 1;

    public float maxHP { get; private set; }
    public float hp { get; private set; }

    public float maxSP { get; private set; }
    public float sp { get; private set; }

    public float maxAP { get; private set; }
    public float ap { get; private set; }

    public int exp { get; private set; }
    public int expToNext { get; private set; }

    public event Action<float, float> OnHPChanged;
    public event Action<float, float> OnSPChanged;
    public event Action<float, float> OnAPChanged;
    public event Action<int, int> OnExpChanged;
    public event Action<int> OnLevelChanged;

    bool isInitialized = false;

    bool useTankHPOverride = false;
    float tankBaseHPOverride = 0f;

    float lastSPSpendTime = -999f;
    float spRecoveryTickTimer = 0f;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        level = Mathf.Max(1, level);
        exp = Mathf.Max(0, exp);

        if (tankConfigOnStart != null)
        {
            useTankHPOverride = true;
            tankBaseHPOverride = Mathf.Max(1f, tankConfigOnStart.baseMaxHP);
        }

        RecalculateHPAndSP(refillHP: true, refillSP: true);

        maxAP = Mathf.Max(0f, fixedMaxAP);
        ap = Mathf.Clamp(initialAP, 0f, maxAP);

        expToNext = GetExpToNextForLevel(level);

        isInitialized = true;
        RaiseAll();
    }

    public void ApplyPlayerStatsConfig(PlayerStatsConfig newConfig, bool refillAll = true)
    {
        statsConfig = newConfig;
        RecalculateHPAndSP(refillAll, refillAll);

        maxAP = Mathf.Max(0f, fixedMaxAP);
        ap = Mathf.Clamp(ap, 0f, maxAP);

        expToNext = GetExpToNextForLevel(level);
        RaiseAll();
    }

    public void ApplyTankConfig(TankConfig tankCfg, bool refillHP = true)
    {
        if (tankCfg == null)
        {
            Debug.LogError("ApplyTankConfig: tankCfg is null");
            return;
        }

        useTankHPOverride = true;
        tankBaseHPOverride = Mathf.Max(1f, tankCfg.baseMaxHP);

        RecalculateHPAndSP(refillHP, refillSP: false);
        RaiseHP();
    }

    public void ClearTankHPOverride(bool refillHP = false)
    {
        useTankHPOverride = false;
        tankBaseHPOverride = 0f;

        RecalculateHPAndSP(refillHP, refillSP: false);
        RaiseHP();
    }

    void RecalculateHPAndSP(bool refillHP, bool refillSP)
    {
        float oldMaxHP = maxHP;
        float oldMaxSP = maxSP;

        maxHP = Mathf.Max(1f, GetMaxHPForLevel(level));
        maxSP = Mathf.Max(0f, GetMaxSPForLevel(level));

        if (refillHP || oldMaxHP <= 0f)
            hp = maxHP;
        else
            hp = Mathf.Clamp(hp, 0f, maxHP);

        if (refillSP || oldMaxSP <= 0f)
            sp = maxSP;
        else
            sp = Mathf.Clamp(sp, 0f, maxSP);
    }

    public void TakeDamage(float amount, GameObject attacker = null)
    {
        if (attacker == null || !attacker.CompareTag("Enemy"))
            return;

        ApplyDamage(amount);
    }

    public void ApplyDamage(float dmg)
    {
        if (!isInitialized)
            Initialize();

        float finalDamage = Mathf.Max(0f, dmg);

        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null)
            controller.ProcessIncomingHit(ref finalDamage);

        hp = Mathf.Max(0f, hp - finalDamage);
        RaiseHP();

        if (hp <= 0f)
        {
            Debug.Log("Player Dead");

            if (controller != null)
                controller.Die();
        }
    }

    public void Heal(float amount)
    {
        if (!isInitialized)
            Initialize();

        amount = Mathf.Max(0f, amount);
        if (amount <= 0f) return;

        float newHP = Mathf.Min(maxHP, hp + amount);
        if (Mathf.Approximately(newHP, hp)) return;

        hp = newHP;
        RaiseHP();
    }

    public void GrantKillRewards()
    {
        if (!isInitialized)
            Initialize();

        float healAmount = maxHP * Mathf.Max(0f, killHealPercentOfPlayerMaxHP);
        Heal(healAmount);
        RecoverSP(killRecoverSPFlat);
    }

    public bool HasEnoughSP(float amount)
    {
        if (!isInitialized)
            Initialize();

        return sp >= Mathf.Max(0f, amount);
    }

    public bool SpendSP(float amount)
    {
        if (!isInitialized)
            Initialize();

        amount = Mathf.Max(0f, amount);

        if (amount <= 0f)
            return true;

        if (sp < amount)
            return false;

        sp -= amount;
        lastSPSpendTime = Time.time;
        spRecoveryTickTimer = 0f;
        RaiseSP();
        return true;
    }

    public void RecoverSP(float amount)
    {
        if (!isInitialized)
            Initialize();

        amount = Mathf.Max(0f, amount);
        if (amount <= 0f) return;

        float newSP = Mathf.Min(maxSP, sp + amount);
        if (Mathf.Approximately(newSP, sp)) return;

        sp = newSP;
        RaiseSP();
    }

    public void SetSP(float value)
    {
        if (!isInitialized)
            Initialize();

        sp = Mathf.Clamp(value, 0f, maxSP);
        RaiseSP();
    }

    public void UpdateSPRecovery(bool allowRecovery, float deltaTime)
    {
        if (!isInitialized)
            Initialize();

        if (maxSP <= 0f)
            return;

        if (sp >= maxSP)
        {
            spRecoveryTickTimer = 0f;
            return;
        }

        if (Time.time - lastSPSpendTime < spRecoveryDelay)
            return;

        if (!allowRecovery)
            return;

        spRecoveryTickTimer += Mathf.Max(0f, deltaTime);

        while (spRecoveryTickTimer >= spRecoveryTickInterval)
        {
            spRecoveryTickTimer -= spRecoveryTickInterval;

            float oldSP = sp;
            sp = Mathf.Min(maxSP, sp + spRecoveryAmountPerTick);

            if (!Mathf.Approximately(oldSP, sp))
                RaiseSP();

            if (sp >= maxSP)
            {
                spRecoveryTickTimer = 0f;
                break;
            }
        }
    }

    public bool HasEnoughAP(float amount)
    {
        if (!isInitialized)
            Initialize();

        return ap >= Mathf.Max(0f, amount);
    }

    public bool SpendAP(float amount)
    {
        if (!isInitialized)
            Initialize();

        amount = Mathf.Max(0f, amount);

        if (amount <= 0f)
            return true;

        if (ap < amount)
            return false;

        ap -= amount;
        RaiseAP();
        return true;
    }

    public void GainAP(float amount)
    {
        if (!isInitialized)
            Initialize();

        amount = Mathf.Max(0f, amount);
        if (amount <= 0f)
            return;

        float newAP = Mathf.Clamp(ap + amount, 0f, maxAP);
        if (Mathf.Approximately(newAP, ap))
            return;

        ap = newAP;
        RaiseAP();
    }

    public void RecoverAP(float amount)
    {
        GainAP(amount);
    }

    public void SetAP(float value)
    {
        if (!isInitialized)
            Initialize();

        ap = Mathf.Clamp(value, 0f, maxAP);
        RaiseAP();
    }

    public void ResetAPToZero()
    {
        if (!isInitialized)
            Initialize();

        ap = 0f;
        RaiseAP();
    }

    public void AddExp(int amount)
    {
        if (!isInitialized)
            Initialize();

        exp += Mathf.Max(0, amount);

        if (expToNext <= 0)
            expToNext = GetExpToNextForLevel(level);

        while (exp >= expToNext)
        {
            exp -= expToNext;
            level++;

            RecalculateHPAndSP(refillHP: true, refillSP: true);
            maxAP = Mathf.Max(0f, fixedMaxAP);
            ap = Mathf.Clamp(ap, 0f, maxAP);

            expToNext = GetExpToNextForLevel(level);

            OnLevelChanged?.Invoke(level);
            RaiseHP();
            RaiseSP();
            RaiseAP();
        }

        RaiseExp();
    }

    public void SetLevel(int newLevel, bool refillAll = true)
    {
        if (!isInitialized)
            Initialize();

        level = Mathf.Max(1, newLevel);

        RecalculateHPAndSP(refillAll, refillAll);

        maxAP = Mathf.Max(0f, fixedMaxAP);
        ap = Mathf.Clamp(ap, 0f, maxAP);

        expToNext = GetExpToNextForLevel(level);

        OnLevelChanged?.Invoke(level);
        RaiseHP();
        RaiseSP();
        RaiseAP();
        RaiseExp();
    }

    public void ResetAllStats(int newLevel = 1)
    {
        level = Mathf.Max(1, newLevel);
        exp = 0;

        RecalculateHPAndSP(refillHP: true, refillSP: true);

        maxAP = Mathf.Max(0f, fixedMaxAP);
        ap = Mathf.Clamp(initialAP, 0f, maxAP);

        expToNext = GetExpToNextForLevel(level);

        OnLevelChanged?.Invoke(level);
        RaiseAll();
    }

    float GetMaxHPForLevel(int lv)
    {
        lv = Mathf.Max(1, lv);

        float baseHP;
        float perLevel;

        if (statsConfig != null)
        {
            baseHP = useTankHPOverride ? tankBaseHPOverride : statsConfig.baseMaxHP;
            perLevel = statsConfig.hpPerLevel;
        }
        else
        {
            baseHP = useTankHPOverride ? tankBaseHPOverride : fallbackBaseMaxHP;
            perLevel = fallbackHpPerLevel;
        }

        return baseHP + (lv - 1) * perLevel;
    }

    float GetMaxSPForLevel(int lv)
    {
        lv = Mathf.Max(1, lv);

        if (statsConfig != null)
            return statsConfig.baseMaxSP + (lv - 1) * statsConfig.spPerLevel;

        return fallbackBaseMaxSP + (lv - 1) * fallbackSpPerLevel;
    }

    int GetExpToNextForLevel(int lv)
    {
        lv = Mathf.Max(1, lv);

        if (statsConfig != null)
            return statsConfig.GetExpToNext(lv);

        return Mathf.RoundToInt(fallbackBaseExpToNext * Mathf.Pow(lv, fallbackExpGrowthPow));
    }

    void RaiseHP()
    {
        OnHPChanged?.Invoke(hp, maxHP);
    }

    void RaiseSP()
    {
        OnSPChanged?.Invoke(sp, maxSP);
    }

    void RaiseAP()
    {
        OnAPChanged?.Invoke(ap, maxAP);
    }

    void RaiseExp()
    {
        OnExpChanged?.Invoke(exp, expToNext);
    }

    void RaiseAll()
    {
        RaiseHP();
        RaiseSP();
        RaiseAP();
        RaiseExp();
        OnLevelChanged?.Invoke(level);
    }
}
