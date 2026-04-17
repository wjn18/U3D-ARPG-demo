using System.Collections;
using UnityEngine;

public class RollAfterImageController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SkinnedMeshRenderer[] skinnedMeshes;
    [SerializeField] private Material afterImageMaterial;
    [SerializeField] private TrailRenderer speedTrail;

    [Header("Timing")]
    [SerializeField] private float spawnInterval = 0.04f;
    [SerializeField] private float imageLifetime = 0.12f;

    [Header("Visual")]
    [SerializeField] private Color afterImageColor = new Color(0.75f, 0.9f, 1f, 0.22f);

    private bool spawning;
    private Coroutine spawnRoutine;

    private void Awake()
    {
        if (speedTrail != null)
        {
            speedTrail.emitting = false;
            speedTrail.Clear();
        }
    }

    public void StartAfterImage()
    {
        if (spawning) return;

        spawning = true;
        spawnRoutine = StartCoroutine(SpawnRoutine());

        if (speedTrail != null)
        {
            speedTrail.Clear();
            speedTrail.emitting = true;
        }
    }

    public void StopAfterImage()
    {
        spawning = false;

        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }

        if (speedTrail != null)
        {
            speedTrail.emitting = false;
        }
    }

    private IEnumerator SpawnRoutine()
    {
        while (spawning)
        {
            SpawnOneAfterImage();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnOneAfterImage()
    {
        if (skinnedMeshes == null || skinnedMeshes.Length == 0) return;
        if (afterImageMaterial == null) return;

        foreach (var smr in skinnedMeshes)
        {
            if (smr == null || !smr.gameObject.activeInHierarchy) continue;

            Mesh bakedMesh = new Mesh();
            smr.BakeMesh(bakedMesh);

            GameObject ghost = new GameObject("AfterImage");
            ghost.transform.position = smr.transform.position;
            ghost.transform.rotation = smr.transform.rotation;
            ghost.transform.localScale = Vector3.one;

            MeshFilter mf = ghost.AddComponent<MeshFilter>();
            MeshRenderer mr = ghost.AddComponent<MeshRenderer>();

            mf.sharedMesh = bakedMesh;

            Material matInstance = new Material(afterImageMaterial);
            matInstance.color = afterImageColor;
            mr.material = matInstance;

            AfterImageFade fade = ghost.AddComponent<AfterImageFade>();
            fade.Init(matInstance, afterImageColor, imageLifetime);

            Destroy(ghost, imageLifetime + 0.05f);
        }
    }
}