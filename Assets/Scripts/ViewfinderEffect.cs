using System.Collections;
using UnityEngine;

/// Full-screen grade: subtle film grain normally, desaturated noisy viewfinder look when the camera is raised,
/// warm "print" look for photographs, plus flash / blink / glitch pulses for scares.
[RequireComponent(typeof(Camera))]
public class ViewfinderEffect : MonoBehaviour
{
    public Material material;
    public bool photoMode;

    [HideInInspector] public float viewfinder, flash, blink, seed;
    public float glitch;

    Material mat;
    float pulse;

    void Awake()
    {
        if (material) mat = new Material(material);
    }

    public void GlitchPulse(float amount, float duration)
    {
        StartCoroutine(Pulse(amount, duration));
    }

    IEnumerator Pulse(float amount, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            pulse = Mathf.Max(pulse, amount * Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI));
            yield return null;
        }
        pulse = 0f;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (!mat) { Graphics.Blit(src, dst); return; }

        if (!photoMode) seed = (Time.time * 13.7f) % 100f;
        float g = Mathf.Max(glitch, pulse) * (Settings.ReducedEffects ? 0.3f : 1f);

        mat.SetFloat("_VF", photoMode ? 0f : viewfinder);
        mat.SetFloat("_Photo", photoMode ? 1f : 0f);
        mat.SetFloat("_Flash", flash);
        mat.SetFloat("_Blink", blink);
        mat.SetFloat("_Glitch", g);
        mat.SetFloat("_Seed", seed);
        mat.SetFloat("_Grain", photoMode ? 0.07f : Mathf.Lerp(0.03f, 0.085f, viewfinder));
        mat.SetFloat("_Vignette", photoMode ? 1.2f : Mathf.Lerp(0.9f, 1.35f, viewfinder));
        mat.SetFloat("_Brightness", Settings.Brightness * (photoMode ? 1.1f : 1f));
        Graphics.Blit(src, dst, mat);
    }

    void OnDisable()
    {
        pulse = 0f;
    }
}
