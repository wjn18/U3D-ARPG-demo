using UnityEngine;
using UnityEngine.Serialization;

public class BossWeaponTrailController : MonoBehaviour
{
    public enum TrailSet
    {
        Normal,
        MeleeSkill1,
        MeleeSkill2,
        MeleeSkill3,
        Ranged
    }

    [Header("Trail Sets")]
    [FormerlySerializedAs("trails")]
    [SerializeField] private TrailRenderer[] normalAttackTrails;
    [SerializeField] private TrailRenderer[] meleeSkill1Trails;
    [SerializeField] private TrailRenderer[] meleeSkill2Trails;
    [SerializeField] private TrailRenderer[] meleeSkill3Trails;
    [SerializeField] private TrailRenderer[] rangedAttackTrails;

    private TrailSet activeTrailSet = TrailSet.Normal;
    private TrailRenderer[] activeAttackTrails;

    private void Awake()
    {
        SetAllTrailState(false, clearTrails: true);
    }

    public void SetTrailSet(TrailSet trailSet, TrailRenderer[] attackTrails = null)
    {
        activeTrailSet = trailSet;
        activeAttackTrails = HasAnyTrail(attackTrails) ? attackTrails : null;
    }

    public void TrailOn()
    {
        SetTrailState(GetTrailsForActiveSet(), true, clearTrails: true);
    }

    public void TrailOff()
    {
        SetAllTrailState(false, clearTrails: false);
    }

    TrailRenderer[] GetTrailsForActiveSet()
    {
        if (HasAnyTrail(activeAttackTrails))
            return activeAttackTrails;

        switch (activeTrailSet)
        {
            case TrailSet.MeleeSkill1:
                if (HasAnyTrail(meleeSkill1Trails))
                    return meleeSkill1Trails;
                break;

            case TrailSet.MeleeSkill2:
                if (HasAnyTrail(meleeSkill2Trails))
                    return meleeSkill2Trails;
                break;

            case TrailSet.MeleeSkill3:
                if (HasAnyTrail(meleeSkill3Trails))
                    return meleeSkill3Trails;
                break;

            case TrailSet.Ranged:
                if (HasAnyTrail(rangedAttackTrails))
                    return rangedAttackTrails;
                break;
        }

        return normalAttackTrails;
    }

    void SetAllTrailState(bool enabled, bool clearTrails)
    {
        SetTrailState(normalAttackTrails, enabled, clearTrails);
        SetTrailState(meleeSkill1Trails, enabled, clearTrails);
        SetTrailState(meleeSkill2Trails, enabled, clearTrails);
        SetTrailState(meleeSkill3Trails, enabled, clearTrails);
        SetTrailState(rangedAttackTrails, enabled, clearTrails);
        SetTrailState(activeAttackTrails, enabled, clearTrails);
    }

    bool HasAnyTrail(TrailRenderer[] trails)
    {
        if (trails == null || trails.Length == 0)
            return false;

        for (int i = 0; i < trails.Length; i++)
        {
            if (trails[i] != null)
                return true;
        }

        return false;
    }

    void SetTrailState(TrailRenderer[] trails, bool enabled, bool clearTrails)
    {
        if (trails == null)
            return;

        foreach (var trail in trails)
        {
            if (trail == null) continue;

            if (clearTrails)
                trail.Clear();

            trail.emitting = false;
            trail.emitting = enabled;
        }
    }
}
