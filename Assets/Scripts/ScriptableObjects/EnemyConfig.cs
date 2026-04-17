using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Config/Enemy Config")]
public class EnemyConfig : ScriptableObject
{
    public string enemyId;
    public string enemyName ;

    [Header("Core Data")]
    public int level = 1;
    public float maxHP = 50f;
    public int expReward = 20;

    [Header("Combat Data")]
    public float attackDamage = 14f;

    public GameObject enemyPrefab;
}
