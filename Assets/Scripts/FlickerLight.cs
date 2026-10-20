using UnityEngine;

/// Practical light with subtle flicker, occasional instability and scripted bursts / blackouts.
[RequireComponent(typeof(Light))]
public class FlickerLight : MonoBehaviour
{
    public float baseIntensity;
    public float flickerAmount = 0.06f;
    public bool unstable;
    public Renderer bulb;   // emissive bulb dimmed together with the light

    Light l;
    float burstUntil, seed;
    bool off;
    Material bulbMat;
    Color bulbEmission;

    void Awake()
    {
        l = GetComponent<Light>();
        if (baseIntensity <= 0f) baseIntensity = l.intensity;
        seed = Random.value * 100f;
        if (bulb)
        {
            bulbMat = bulb.material;
            bulbEmission = bulbMat.GetColor("_EmissionColor");
        }
    }

    public void Burst(float duration) => burstUntil = Time.time + duration;
    public void SetOff(bool value) => off = value;

    void Update()
    {
        float i;
        if (off) i = 0f;
        else
        {
            float n = Mathf.PerlinNoise(seed, Time.time * 2.5f);
            i = baseIntensity * (1f - flickerAmount + n * flickerAmount * 2f);

            if (unstable && Random.value < 0.003f) burstUntil = Time.time + Random.Range(0.08f, 0.35f);
            if (Time.time < burstUntil)
            {
                if (Settings.ReducedEffects) i *= 0.35f;
                else i *= Random.value < 0.5f ? 0.08f : 1.05f;
            }
        }
        l.intensity = i;
        if (bulbMat) bulbMat.SetColor("_EmissionColor", bulbEmission * (baseIntensity > 0 ? i / baseIntensity : 0f));
    }
}
