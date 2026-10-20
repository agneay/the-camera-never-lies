using System.Collections;
using UnityEngine;

/// The Figure's face lunging into the camera: scream, red flash, tearing image.
public class JumpScare : MonoBehaviour
{
    public static JumpScare I;
    public Transform face;   // child of the player camera, hidden until needed

    public bool Playing { get; private set; }

    void Awake()
    {
        I = this;
        if (face) face.gameObject.SetActive(false);
    }

    public IEnumerator Play(float hold, bool lethal)
    {
        Playing = true;
        CameraTool.I.Lower();
        var am = AudioManager.I;
        am.Play2D(am.scream, lethal ? 1f : 0.85f);
        am.Play2D(am.sting, lethal ? 1f : 0.7f);
        UIManager.I.RedFlash(lethal ? 0.5f : 0.25f);
        CameraTool.I.effect.GlitchPulse(1f, hold + 0.25f);

        face.gameObject.SetActive(true);
        Vector3 far = new Vector3(0f, -0.02f, 1.4f), near = new Vector3(0f, -0.05f, lethal ? 0.33f : 0.42f);
        float t = 0f;
        while (t < hold)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 0.11f);
            Vector3 jitter = Settings.ReducedEffects ? Vector3.zero : Random.insideUnitSphere * 0.012f;
            face.localPosition = Vector3.Lerp(far, near, 1f - (1f - k) * (1f - k)) + jitter;
            yield return null;
        }
        face.gameObject.SetActive(false);
        Playing = false;
    }
}
