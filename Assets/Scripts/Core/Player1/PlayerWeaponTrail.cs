using UnityEngine;
using UnityEngine.Serialization;

public class PlayerWeaponTrailController : MonoBehaviour
{
    public enum TrailSet
    {
        Normal,
        Heavy,
        Sprint
    }

    [Header("Trail Sets")]
    [FormerlySerializedAs("trails")]
    [SerializeField] private TrailRenderer[] normalAttackTrails;
    [SerializeField] private TrailRenderer[] heavyAttackTrails;
    [SerializeField] private TrailRenderer[] sprintAttackTrails;

    private TrailSet activeTrailSet = TrailSet.Normal;

    void Awake()
    {
        SetAllTrailState(false, clearTrails: true);
    }

    public void SetTrailSet(TrailSet trailSet)
    {
        activeTrailSet = trailSet;
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
        switch (activeTrailSet)
        {
            case TrailSet.Heavy:
                if (heavyAttackTrails != null && heavyAttackTrails.Length > 0)
                    return heavyAttackTrails;
                break;

            case TrailSet.Sprint:
                if (sprintAttackTrails != null && sprintAttackTrails.Length > 0)
                    return sprintAttackTrails;
                break;
        }

        return normalAttackTrails;
    }

    void SetAllTrailState(bool enabled, bool clearTrails)
    {
        SetTrailState(normalAttackTrails, enabled, clearTrails);
        SetTrailState(heavyAttackTrails, enabled, clearTrails);
        SetTrailState(sprintAttackTrails, enabled, clearTrails);
    }

    void SetTrailState(TrailRenderer[] trails, bool enabled, bool clearTrails)
    {
        if (trails == null)
            return;

        for (int i = 0; i < trails.Length; i++)
        {
            TrailRenderer trail = trails[i];
            if (trail == null)
                continue;

            if (clearTrails)
                trail.Clear();

            trail.emitting = enabled;
        }
    }
}
