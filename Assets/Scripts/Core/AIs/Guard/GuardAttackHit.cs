using UnityEngine;

public class GuardAttackHit : MonoBehaviour
{
    public GuardAI guardAI;

    void Awake()
    {
        if (guardAI == null)
            guardAI = GetComponentInParent<GuardAI>();
    }

    public void DealDamage()
    {
        if (guardAI != null)
        {
            guardAI.ApplyAttackDamageNow();
        }
    }

    public void ApplyCurrentAction()
    {
        DealDamage();
    }

    public void HealNow()
    {
        if (guardAI != null)
        {
            guardAI.ApplyHealNow();
        }
    }
}
