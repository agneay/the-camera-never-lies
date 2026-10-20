using System.Collections;
using UnityEngine;

/// Plays ambience, one-shots and camera sounds. Every clip is synthesised at startup, so the project ships no audio files.
public class AudioManager : MonoBehaviour
{
    public static AudioManager I;

    [HideInInspector] public AudioClip ambience, drone, shutter, toggleUp, toggleDown, sting, whisper, creak,
        drawer, click, unlock, rattle, warp, heartbeat, paper, whoosh;
    [HideInInspector] public AudioClip[] steps;
    [HideInInspector] public AudioClip scream, breath, chime, staticBurst, ghostStep, heartbeatLoop;

    AudioSource ambSrc, droneSrc, uiSrc, heartSrc;
    const int SR = 44100;
    System.Random rng = new System.Random(1337);

    void Awake()
    {
        I = this;
        Generate();
        ambSrc = MakeSource(true);
        droneSrc = MakeSource(true);
        uiSrc = MakeSource(false);
        heartSrc = MakeSource(true);
        heartSrc.clip = heartbeatLoop;
        heartSrc.volume = 0f;
    }

    /// 0 = silent, 1 = pounding. Pitch rises with fear.
    public void SetHeartbeat(float intensity)
    {
        if (intensity > 0.01f && !heartSrc.isPlaying) heartSrc.Play();
        heartSrc.volume = Mathf.MoveTowards(heartSrc.volume, intensity * 0.9f, Time.deltaTime * 1.5f);
        heartSrc.pitch = 1f + intensity * 0.6f;
        if (heartSrc.volume <= 0.001f && heartSrc.isPlaying) heartSrc.Stop();
    }

    public AudioSource Loop3D(AudioClip c, Transform parent, float volume, float minDistance)
    {
        var g = new GameObject("loop_" + c.name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = Vector3.up * 1.6f;
        var s = g.AddComponent<AudioSource>();
        s.clip = c;
        s.loop = true;
        s.spatialBlend = 1f;
        s.volume = volume;
        s.minDistance = minDistance;
        s.maxDistance = 22f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.dopplerLevel = 0f;
        s.Play();
        return s;
    }

    AudioSource MakeSource(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.loop = loop;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        return s;
    }

    // ---------------- Playback ----------------

    public void StartAmbience()
    {
        ambSrc.clip = ambience;
        ambSrc.volume = 0.32f;
        ambSrc.Play();
    }

    public void StopAll()
    {
        ambSrc.Stop();
        StopHeartbeat();
        droneSrc.Stop();
    }

    public void SetDrone(bool on) => StartCoroutine(FadeDrone(on ? 0.45f : 0f));

    IEnumerator FadeDrone(float target)
    {
        if (target > 0f && !droneSrc.isPlaying)
        {
            droneSrc.clip = drone;
            droneSrc.volume = 0f;
            droneSrc.Play();
        }
        while (!Mathf.Approximately(droneSrc.volume, target))
        {
            droneSrc.volume = Mathf.MoveTowards(droneSrc.volume, target, Time.deltaTime * 0.25f);
            yield return null;
        }
        if (target <= 0f) droneSrc.Stop();
    }

    public void Play2D(AudioClip c, float volume = 1f)
    {
        if (c) uiSrc.PlayOneShot(c, volume);
    }

    public void PlayAt(AudioClip c, Vector3 pos, float volume = 1f, float minDistance = 1.5f, float pitch = 1f)
    {
        if (!c) return;
        var g = new GameObject("sfx_" + c.name);
        g.transform.position = pos;
        var s = g.AddComponent<AudioSource>();
        s.clip = c;
        s.spatialBlend = 1f;
        s.volume = volume;
        s.pitch = pitch;
        s.minDistance = minDistance;
        s.maxDistance = 30f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.dopplerLevel = 0f;
        s.Play();
        Destroy(g, c.length / Mathf.Max(0.1f, pitch) + 0.2f);
    }

    public void Footstep(Vector3 pos, float volume)
    {
        PlayAt(steps[rng.Next(steps.Length)], pos, volume, 1f, 0.9f + (float)rng.NextDouble() * 0.2f);
    }

    // ---------------- Synthesis ----------------

    float N() => (float)(rng.NextDouble() * 2.0 - 1.0);
    static float[] Buf(float seconds) => new float[(int)(seconds * SR)];
    const float TAU = Mathf.PI * 2f;

    static AudioClip Clip(string name, float[] d, float peak)
    {
        float m = 0.0001f;
        for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
        float k = peak / m;
        for (int i = 0; i < d.Length; i++) d[i] *= k;
        var c = AudioClip.Create(name, d.Length, 1, SR, false);
        c.SetData(d, 0);
        return c;
    }

    /// Makes a loop seamless by crossfading its tail into its head.
    static float[] Loop(float[] x, int fade)
    {
        int n = x.Length - fade;
        var o = new float[n];
        for (int i = 0; i < n; i++) o[i] = x[i];
        for (int i = 0; i < fade; i++)
        {
            float a = (float)i / fade;
            o[i] = x[i] * a + x[n + i] * (1f - a);
        }
        return o;
    }

    void Generate()
    {
        ambience = MakeAmbience();
        drone = MakeDrone();
        shutter = MakeShutter();
        toggleUp = MakeToggle(1650f);
        toggleDown = MakeToggle(1150f);
        steps = new[] { MakeStep(0.12f, 72f), MakeStep(0.16f, 64f), MakeStep(0.1f, 80f), MakeStep(0.14f, 68f) };
        sting = MakeSting();
        whisper = MakeWhisper();
        creak = MakeCreak();
        drawer = MakeDrawer();
        click = MakeClick();
        unlock = MakeUnlock();
        rattle = MakeRattle();
        warp = MakeWarp();
        heartbeat = MakeHeartbeat();
        paper = MakePaper();
        whoosh = MakeWhoosh();
        scream = MakeScream();
        breath = MakeBreath();
        chime = MakeChime();
        staticBurst = MakeStatic();
        ghostStep = MakeGhostStep();
        heartbeatLoop = MakeHeartbeatLoop();
    }

    public void StopHeartbeat()
    {
        if (heartSrc) { heartSrc.volume = 0f; heartSrc.Stop(); }
    }

    AudioClip MakeScream()
    {
        var x = Buf(1.5f);
        float ph1 = 0, ph2 = 0, ph3 = 0, lp = 0;
        for (int i = 0; i < x.Length; i++)
        {
            float t = (float)i / SR;
            float env = (1f - Mathf.Exp(-t * 60f)) * Mathf.Exp(-t * 1.6f);
            float f = 620f + 900f * Mathf.Exp(-t * 2.5f) + 60f * Mathf.Sin(TAU * 11f * t);
            ph1 += f / SR; ph2 += f * 1.49f / SR; ph3 += f * 0.51f / SR;
            float saw = 2f * (ph1 - Mathf.Floor(ph1)) - 1f;
            float s = saw * 0.5f + Mathf.Sin(TAU * ph2) * 0.4f + Mathf.Sin(TAU * ph3) * 0.4f;
            lp += (N() - lp) * 0.6f;
            s += lp * 0.7f;
            x[i] = (float)System.Math.Tanh(s * 2.2f) * env;
        }
        return Clip("scream", x, 0.95f);
    }

    AudioClip MakeBreath()
    {
        float len = 3.6f;
        var x = Buf(len);
        float lp1 = 0, lp2 = 0;
        for (int i = 0; i < x.Length; i++)
        {
            float t = (float)i / SR;
            float n = N();
            lp1 += (n - lp1) * 0.25f;
            lp2 += (n - lp2) * 0.03f;
            float band = lp1 - lp2;
            float u = t / len;
            float env = u < 0.45f ? Mathf.Pow(Mathf.Sin(Mathf.PI * u / 0.45f), 2f) * 0.8f
                      : u > 0.5f && u < 0.95f ? Mathf.Pow(Mathf.Sin(Mathf.PI * (u - 0.5f) / 0.45f), 2f) : 0f;
            x[i] = band * env * (1f + 0.3f * Mathf.Sin(TAU * 31f * t));
        }
        return Clip("breath", x, 0.55f);
    }

    AudioClip MakeChime()
    {
        var x = Buf(3.2f);
        float[] partials = { 1f, 2.0f, 2.76f, 5.4f, 8.93f };
        for (int i = 0; i < x.Length; i++)
        {
            float t = (float)i / SR;
            float s = 0;
            for (int k = 0; k < partials.Length; k++)
                s += Mathf.Sin(TAU * 330f * partials[k] * t) * Mathf.Exp(-t * (1.2f + k * 0.9f)) / (k + 1);
            x[i] = s * (1f - Mathf.Exp(-t * 400f));
        }
        return Clip("chime", x, 0.75f);
    }

    AudioClip MakeStatic()
    {
        var x = Buf(1.3f);
        float lp = 0, gate = 1f;
        for (int i = 0; i < x.Length; i++)
        {
            float t = (float)i / SR;
            if (i % 600 == 0) gate = rng.NextDouble() < 0.3 ? 0.15f : 1f;
            float n = N();
            lp += (n - lp) * 0.4f;
            float env = Mathf.Min(1f, t * 20f) * Mathf.Min(1f, (1.3f - t) * 4f);
            x[i] = (n - lp * 0.5f) * gate * env + 0.15f * Mathf.Sin(TAU * 60f * t) * env;
        }
        return Clip("static", x, 0.6f);
    }

    AudioClip MakeGhostStep()
    {
        var x = Buf(0.45f);
        float lp = 0;
        for (int i = 0; i < x.Length; i++)
        {
            float t = (float)i / SR;
            lp += (N() - lp) * 0.06f;
            x[i] = lp * Mathf.Exp(-t * 12f) * 2f + Mathf.Sin(TAU * 46f * t) * Mathf.Exp(-t * 14f) * 0.9f;
        }
        AddClick(x, 0.02f, 0.25f, 300f);
        return Clip("ghost_step", x, 0.8f);
    }

    AudioClip MakeHeartbeatLoop()
    {
        float beat = 0.82f;
        var x = Buf(beat);
        foreach (float off in new[] { 0f, 0.24f })
        {
            int s = (int)(off * SR);
            float amp = off == 0f ? 1f : 0.7f;
            for (int i = s; i < x.Length; i++)
            {
                float t = (float)(i - s) / SR;
                if (t > 0.3f) break;
                x[i] += amp * Mathf.Sin(TAU * 50f * t) * Mathf.Exp(-t * 15f) * (1f - Mathf.Exp(-t * 180f));
            }
        }
        return Clip("heartbeat_loop", x, 0.9f);
    }

    AudioClip MakeAmbience()
    {
        var x = Buf(13f);
        float b = 0, lp = 0;
        for (int i = 0; i < x.Length; i++)
        {
            float t = (float)i / SR;
            b = (b + 0.02f * N()) / 1.02f;
            lp += (N() - lp) * 0.04f;
            float swell = 0.75f + 0.25f * Mathf.Sin(TAU * 0.07f * t);
            x[i] = (b * 3.2f + lp * 0.06f) * swell + 0.018f * Mathf.Sin(TAU * 50f * t) + 0.01f * Mathf.Sin(TAU * 100f * t);
        }
        return Clip("ambience", Loop(x, SR), 0.55f);
    }

    AudioClip MakeDrone()
    {
        var x = Buf(9f);
        float b = 0;
        for (int i = 0; i < x.Length; i++)
        {
            float t = (float)i / SR;
            b = (b + 0.02f * N()) / 1.02f;
            float lfo = 0.6f + 0.4f * Mathf.Sin(TAU * 0.18f * t);
            x[i] = (Mathf.Sin(TAU * 55f * t) + 0.8f * Mathf.Sin(TAU * 55.8f * t) + 0.35f * Mathf.Sin(TAU * 82.6f * t)
                    + 0.2f * Mathf.Sin(TAU * 116.6f * t)) * lfo + b * 2f;
        }
        return Clip("drone", Loop(x, SR), 0.6f);
    }

    void AddClick(float[] x, float start, float amp, float tone)
    {
        int s = (int)(start * SR);
        for (int i = s; i < x.Length; i++)
        {
            float t = (float)(i - s) / SR;
            if (t > 0.12f) break;
            x[i] += amp * (N() * Mathf.Exp(-t * 320f) * 0.8f
                           + Mathf.Sin(TAU * tone * t) * Mathf.Exp(-t * 500f) * 0.35f
                           + Mathf.Sin(TAU * 150f * t) * Mathf.Exp(-t * 55f) * 0.45f);
        }
    }

    AudioClip MakeShutter()
    {
        var x = Buf(0.3f);
        AddClick(x, 0f, 0.8f, 2300f);
        AddClick(x, 0.075f, 1f, 1900f);
        return Clip("shutter", x, 0.9f);
    }

    AudioClip MakeToggle(float beep)
    {
        var x = Buf(0.16f);
        AddClick(x, 0f, 0.5f, 2600f);
        int s = (int)(0.035f * SR);
        for (int i = s; i < x.Length; i++)
        {
            float t = (float)(i - s) / SR;
            if (t > 0.055f) break;
            x[i] += 0.18f * Mathf.Sin(TAU * beep * t) * Mathf.Min(1f, (0.055f - t) * 200f);
        }
        return Clip("toggle", x, 0.5f);
    }

    AudioClip MakeStep(float len, float thump)
    {
        var x = Buf(len + 0.1f);
        float lp = 0;
        for (int i = 0; i < x.Length; i++)
        {
            float t = (float)i / SR;
            lp += (N() - lp) * 0.12f;
            x[i] = lp * Mathf.Exp(-t * 28f) * 1.4f + Mathf.Sin(TAU * thump * t) * Mathf.Exp(-t * 26f) * 0.7f;
        }
        return Clip("step", x, 0.7f);
    }

    AudioClip MakeSting()
    {
        var x = Buf(3.4f);
        float[] f = { 196f, 207.7f, 293.7f, 415.3f, 587.3f, 622.3f };
        float lp = 0;
        for (int i = 0; i < x.Length; i++)
        {
            float t = (float)i / SR;
            float env = (1f - Mathf.Exp(-t * 35f)) * Mathf.Exp(-t * 1.1f);
            float trem = 1f + 0.35f * Mathf.Sin(TAU * 7.5f * t);
            float s = 0;
            for (int k = 0; k < f.Length; k++) s += Mathf.Sin(TAU * f[k] * t * (1f + 0.002f * k * t));
            lp += (N() - lp) * 0.3f;
            float boom = Mathf.Sin(TAU * 42f * t) * Mathf.Exp(-t * 3.5f) * 2.2f;
            x[i] = s * env * trem * 0.35f + lp * env * 0.9f + boom;
        }
        return Clip("sting", x, 0.95f);
    }

    AudioClip MakeWhisper()
    {
        var x = Buf(2.4f);
        float lp1 = 0, lp2 = 0;
        float[] c = new float[9], w = new float[9];
        for (int k = 0; k < c.Length; k++) { c[k] = 0.15f + k * 0.24f + N() * 0.05f; w[k] = 0.05f + Mathf.Abs(N()) * 0.06f; }
        for (int i = 0; i < x.Length; i++)
        {
            float t = (float)i / SR;
            float n = N();
            lp1 += (n - lp1) * 0.5f;
            lp2 += (n - lp2) * 0.06f;
            float band = lp1 - lp2;
            float env = 0;
            for (int k = 0; k < c.Length; k++) { float d = (t - c[k]) / w[k]; env += Mathf.Exp(-d * d); }
            x[i] = band * env;
        }
        return Clip("whisper", x, 0.5f);
    }

    AudioClip MakeCreak()
    {
        var x = Buf(1.5f);
        float ph = 0, lp = 0;
        for (int i = 0; i < x.Length; i++)
        {
            float t = (float)i / SR;
            float f = 95f + 45f * Mathf.Sin(TAU * 1.1f * t) + N() * 12f;
            ph += f / SR;
            float saw = 2f * (ph - Mathf.Floor(ph)) - 1f;
            lp += (saw - lp) * 0.18f;
            float env = Mathf.Sqrt(Mathf.Sin(Mathf.PI * t / 1.5f)) * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(TAU * 8f * t)));
            x[i] = lp * env;
        }
        return Clip("creak", x, 0.5f);
    }

    AudioClip MakeDrawer()
    {
        var x = Buf(1.0f);
        float lp = 0;
        for (int i = 0; i < x.Length; i++)
        {
            float t = (float)i / SR;
            lp += (N() - lp) * 0.08f;
            float env = t < 0.8f ? (0.5f + 0.5f * Mathf.Abs(Mathf.Sin(TAU * 21f * t))) * Mathf.Sin(Mathf.PI * t / 0.8f) : 0f;
            x[i] = lp * env;
        }
        AddClick(x, 0.8f, 1.2f, 600f);
        return Clip("drawer", x, 0.6f);
    }

    AudioClip MakeClick()
    {
        var x = Buf(0.06f);
        AddClick(x, 0f, 1f, 3200f);
        return Clip("click", x, 0.5f);
    }

    AudioClip MakeUnlock()
    {
        var x = Buf(0.5f);
        AddClick(x, 0f, 0.7f, 1500f);
        AddClick(x, 0.2f, 1f, 900f);
        return Clip("unlock", x, 0.8f);
    }

    AudioClip MakeRattle()
    {
        var x = Buf(0.7f);
        for (int k = 0; k < 7; k++) AddClick(x, k * 0.075f + Mathf.Abs(N()) * 0.02f, 0.6f + Mathf.Abs(N()) * 0.4f, 900f + N() * 200f);
        return Clip("rattle", x, 0.7f);
    }

    AudioClip MakeWarp()
    {
        float len = 2.6f;
        var x = Buf(len);
        float lp = 0, ph = 0;
        for (int i = 0; i < x.Length; i++)
        {
            float t = (float)i / SR;
            float rise = t < 1.5f ? Mathf.Pow(t / 1.5f, 2.5f) : 0f;
            float f = Mathf.Lerp(60f, 420f, Mathf.Clamp01(t / 1.5f));
            ph += f / SR;
            lp += (N() - lp) * Mathf.Lerp(0.02f, 0.4f, Mathf.Clamp01(t / 1.5f));
            float s = (Mathf.Sin(TAU * ph) * 0.5f + lp) * rise;
            if (t >= 1.5f)
            {
                float tt = t - 1.5f;
                s += Mathf.Sin(TAU * 38f * tt) * Mathf.Exp(-tt * 3f) * 1.4f + lp * Mathf.Exp(-tt * 6f);
            }
            x[i] = s;
        }
        return Clip("warp", x, 0.9f);
    }

    AudioClip MakeHeartbeat()
    {
        var x = Buf(3.4f);
        for (int b = 0; b < 4; b++)
        {
            foreach (float off in new[] { 0f, 0.26f })
            {
                int s = (int)((b * 0.82f + off) * SR);
                float amp = off == 0f ? 1f : 0.7f;
                for (int i = s; i < x.Length; i++)
                {
                    float t = (float)(i - s) / SR;
                    if (t > 0.3f) break;
                    x[i] += amp * Mathf.Sin(TAU * 48f * t) * Mathf.Exp(-t * 16f) * (1f - Mathf.Exp(-t * 180f));
                }
            }
        }
        return Clip("heartbeat", x, 0.9f);
    }

    AudioClip MakePaper()
    {
        var x = Buf(0.5f);
        float lp = 0;
        for (int i = 0; i < x.Length; i++)
        {
            float t = (float)i / SR;
            float n = N();
            lp += (n - lp) * 0.3f;
            float crackle = rng.NextDouble() < 0.004 ? 3f : 1f;
            x[i] = (n - lp) * Mathf.Sin(Mathf.PI * t / 0.5f) * crackle;
        }
        return Clip("paper", x, 0.35f);
    }

    AudioClip MakeWhoosh()
    {
        float len = 1.1f;
        var x = Buf(len);
        float lp = 0;
        for (int i = 0; i < x.Length; i++)
        {
            float t = (float)i / SR;
            float u = t / len;
            lp += (N() - lp) * Mathf.Lerp(0.02f, 0.25f, Mathf.Sin(Mathf.PI * u));
            x[i] = lp * Mathf.Pow(Mathf.Sin(Mathf.PI * u), 2f);
        }
        return Clip("whoosh", x, 0.7f);
    }
}
