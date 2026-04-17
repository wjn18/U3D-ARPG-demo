using UnityEngine;

[System.Serializable]
public class PlayerAttackData
{
    public string actionName = "Attack";
    public float damage = 10f;
    public float spCost = 10f;
    public float apGainPerHit = 0f;
}