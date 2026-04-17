using UnityEngine;

public class AfterImageFade : MonoBehaviour
{
    private Material mat;
    private Color startColor;
    private float lifetime;
    private float timer;

    private Vector3 startScale;
    private Vector3 endScale;

    public void Init(Material material, Color color, float life)
    {
        mat = material;
        startColor = color;
        lifetime = life;

        startScale = transform.localScale;
        endScale = startScale * 0.92f;
    }

    private void Update()
    {
        if (mat == null) return;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / lifetime);

        Color c = startColor;
        c.a = Mathf.Lerp(startColor.a, 0f, t);
        mat.color = c;

        transform.localScale = Vector3.Lerp(startScale, endScale, t);

        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}