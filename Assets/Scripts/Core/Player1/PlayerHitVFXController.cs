using UnityEngine;

public class PlayerHitVFXController : MonoBehaviour
{
    [Header("Hit Effect")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private float hitEffectLifetime = 0.75f;
    [SerializeField] private float hitEffectNormalOffset = 0.02f;

    public void PlayHitEffect(Vector3 position, Vector3 normal)
    {
        if (hitEffectPrefab == null)
            return;

        Vector3 safeNormal = normal.sqrMagnitude > 0.0001f
            ? normal.normalized
            : Vector3.up;

        Vector3 spawnPosition = position + safeNormal * Mathf.Max(0f, hitEffectNormalOffset);
        Quaternion rotation = Quaternion.LookRotation(safeNormal, Vector3.up);
        GameObject instance = Instantiate(hitEffectPrefab, spawnPosition, rotation);
        Destroy(instance, Mathf.Max(0.01f, hitEffectLifetime));
    }
}
