using UnityEngine;

public class GuardWeaponTrailController : MonoBehaviour
{
    public enum TrailSet
    {
        Attack1,
        Attack2
    }

    [Header("Trail Sets")]
    [SerializeField] private TrailRenderer[] attack1Trails;
    [SerializeField] private TrailRenderer[] attack2Trails;

    private GuardAI guardAI;
    private TrailSet activeTrailSet = TrailSet.Attack1;
    private TrailRenderer[] activeActionTrails;

    void Awake()
    {
        guardAI = GetComponent<GuardAI>();
        if (guardAI == null)
            guardAI = GetComponentInParent<GuardAI>();

        SetAllTrailState(false, clearTrails: true);
    }

    void OnEnable()
    {
        if (guardAI != null)
            guardAI.OnAction += HandleAction;
    }

    void OnDisable()
    {
        if (guardAI != null)
            guardAI.OnAction -= HandleAction;

        TrailOff();
    }

    void HandleAction(GuardActionData action)
    {
        if (action == null)
            return;

        activeActionTrails = HasAnyTrail(action.slashTrails) ? action.slashTrails : null;

        switch (action.actionType)
        {
            case GuardActionType.Attack2:
                activeTrailSet = TrailSet.Attack2;
                break;
            default:
                activeTrailSet = TrailSet.Attack1;
                break;
        }
    }

    public void SetTrailSet(TrailSet trailSet)
    {
        activeTrailSet = trailSet;
    }

    public void SetTrailSetByIndex(int trailSetIndex)
    {
        activeTrailSet = trailSetIndex == 1 ? TrailSet.Attack2 : TrailSet.Attack1;
    }

    public void TrailOn()
    {
        SetTrailState(GetTrailsForActiveSet(), true, clearTrails: true);
    }

    public void TrailOff()
    {
        SetAllTrailState(false, clearTrails: false);
    }

    public void AE_TrailOn()
    {
        TrailOn();
    }

    public void AE_TrailOff()
    {
        TrailOff();
    }

    TrailRenderer[] GetTrailsForActiveSet()
    {
        if (HasAnyTrail(activeActionTrails))
            return activeActionTrails;

        switch (activeTrailSet)
        {
            case TrailSet.Attack2:
                if (HasAnyTrail(attack2Trails))
                    return attack2Trails;
                break;
        }

        return attack1Trails;
    }

    void SetAllTrailState(bool enabled, bool clearTrails)
    {
        SetTrailState(attack1Trails, enabled, clearTrails);
        SetTrailState(attack2Trails, enabled, clearTrails);
        SetTrailState(activeActionTrails, enabled, clearTrails);
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
