using UnityEngine;

public class Glow : MonoBehaviour
{
    [Header("Color")]
    public Color glowColor = Color.green;   // pick HDR color if you want
    public float maxIntensity = 1.8f;       // keep around 0.8–2.0 to avoid white
    public float speed = 2f;

    [Header("Off Time")]
    [Range(0f, 0.5f)]
    public float offThreshold = 0.15f;      // part of the cycle fully OFF

    Material mat;
    Color baseColor;

    void Awake()
    {
        var r = GetComponent<Renderer>();
        mat = r.material;
        mat.EnableKeyword("_EMISSION");

        // Normalize the color manually (keep hue, remove "brightness")
        Color c = glowColor.linear; // work in linear space
        float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        baseColor = (max > 0.0001f) ? (c / max) : Color.black;

        mat.SetColor("_EmissionColor", Color.black);
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f; // 0..1

        if (t < offThreshold)
        {
            mat.SetColor("_EmissionColor", Color.black);
            return;
        }

        float pulse = (t - offThreshold) / (1f - offThreshold); // 0..1
        pulse = Mathf.SmoothStep(0f, 1f, pulse);

        float intensity = pulse * maxIntensity;

        // Convert back to gamma for material (Unity handles it, but this is safe)
        mat.SetColor("_EmissionColor", baseColor * intensity);
    }
}
