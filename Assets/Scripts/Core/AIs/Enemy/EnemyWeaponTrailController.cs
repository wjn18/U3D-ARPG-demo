using UnityEngine;

public class EnemyWeaponTrailController : MonoBehaviour
{
    [Header("Trail Sets")]
    [SerializeField] private TrailRenderer[] attackTrails;

    void Awake()
    {
        SetTrailState(false, clearTrails: true);
    }

    void OnDisable()
    {
        TrailOff();
    }

    public void TrailOn()
    {
        SetTrailState(true, clearTrails: true);
    }

    public void TrailOff()
    {
        SetTrailState(false, clearTrails: false);
    }

    public void AE_TrailOn()
    {
        TrailOn();
    }

    public void AE_TrailOff()
    {
        TrailOff();
    }

    void SetTrailState(bool enabled, bool clearTrails)
    {
        if (attackTrails == null)
            return;

        for (int i = 0; i < attackTrails.Length; i++)
        {
            TrailRenderer trail = attackTrails[i];
            if (trail == null)
                continue;

            if (clearTrails)
                trail.Clear();

            trail.emitting = enabled;
        }
    }
}
