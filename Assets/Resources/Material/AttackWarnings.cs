using UnityEngine;

public class AttackWarningBlink : MonoBehaviour
{
    [Header("Renderer")]
    public Renderer targetRenderer;

    [Header("Blink")]
    public float minAlpha = 0.15f;
    public float maxAlpha = 0.55f;
    public float blinkSpeed = 6f;
    public string colorProperty = "Color";

    Material mat;
    Color baseColor;

    void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        mat = targetRenderer.material; 
      
    }

    void Update()
    {
        if (!mat.HasProperty(colorProperty)) return;

        float t = (Mathf.Sin(Time.time * blinkSpeed) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);

        Color c = baseColor;
        c.a = alpha;
        mat.SetColor(colorProperty, c);
    }
}