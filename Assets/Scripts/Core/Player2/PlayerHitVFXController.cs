using UnityEngine;

public class PlayerHitVFXController : MonoBehaviour
{
    [Header("Hit VFX")]
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField] private float hitVfxLifetime = 0.75f;
    [SerializeField] private float hitVfxNormalOffset = 0.02f;

    public void PlayHitVFX(Vector3 position, Vector3 normal)
    {
        if (hitVfxPrefab == null)
            return;

        Vector3 safeNormal = normal.sqrMagnitude > 0.0001f
            ? normal.normalized
            : Vector3.up;

        Vector3 spawnPosition = position + safeNormal * Mathf.Max(0f, hitVfxNormalOffset);
        Quaternion rotation = Quaternion.LookRotation(safeNormal, Vector3.up);
        GameObject instance = Instantiate(hitVfxPrefab, spawnPosition, rotation);
        if (!instance.activeSelf)
            instance.SetActive(true);

        Destroy(instance, Mathf.Max(0.01f, hitVfxLifetime));
    }
}
