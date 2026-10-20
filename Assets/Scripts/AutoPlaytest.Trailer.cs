using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using Proc = System.Diagnostics;

/// "-trailer -shots <dir>": plays a scripted showcase of both chapters and records it.
/// Frames go straight into ffmpeg (fixed 30 fps via Time.captureFramerate, so speed doesn't matter);
/// game audio is captured offline with AudioRenderer and written as a WAV; key moments are saved as PNG stills.
public partial class AutoPlaytest
{
    const int Fps = 30;

    bool trailerMode, recording;
    Proc.Process ffmpeg;
    Stream ffIn;
    int frameCount;
    readonly List<float> audioSamples = new List<float>(48000 * 2 * 90);
    int audioChannels = 2;
    string pendingStill;

    // Title-card overlay
    string cardTitle, cardSub;
    float cardAlpha, black = 1f;
    bool cardBig, endCard;
    GUIStyle cardTitleStyle, cardSubStyle, endTitleStyle, endRedStyle, endSmallStyle;

    // ------------------------------------------------------------------ recording

    /// "-clean": same shots, no captions, no video — just caption-free PNG stills for store pages.
    bool cleanStills;

    void StartRecording()
    {
        cleanStills = Environment.GetCommandLineArgs().Contains("-clean");
        Directory.CreateDirectory(Path.Combine(shotDir, "stills"));
        Time.captureFramerate = Fps;
        var cfg = AudioSettings.GetConfiguration();
        audioChannels = cfg.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
        if (!cleanStills) AudioRenderer.Start();
        recording = true;
        StartCoroutine(Recorder());
    }

    IEnumerator Recorder()
    {
        var eof = new WaitForEndOfFrame();
        while (recording)
        {
            yield return eof;
            if (!recording) break;
            CaptureFrame();
        }
    }

    void CaptureFrame()
    {
        if (cleanStills)
        {
            if (pendingStill == null) return;
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(shotDir, "stills", pendingStill + ".png"), shot.EncodeToPNG());
            pendingStill = null;
            Destroy(shot);
            return;
        }

        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        if (ffmpeg == null) StartFfmpeg(tex);
        var data = tex.GetRawTextureData<byte>();
        ffIn.Write(data.ToArray(), 0, data.Length);
        frameCount++;

        if (pendingStill != null)
        {
            File.WriteAllBytes(Path.Combine(shotDir, "stills", pendingStill + ".png"), tex.EncodeToPNG());
            pendingStill = null;
        }
        Destroy(tex);

        int n = AudioRenderer.GetSampleCountForCaptureFrame();
        if (n > 0)
        {
            var buf = new NativeArray<float>(n * audioChannels, Allocator.Temp);
            if (AudioRenderer.Render(buf)) audioSamples.AddRange(buf.ToArray());
            buf.Dispose();
        }
    }

    void StartFfmpeg(Texture2D first)
    {
        string fmt = first.format switch
        {
            TextureFormat.RGBA32 => "rgba",
            TextureFormat.ARGB32 => "argb",
            TextureFormat.BGRA32 => "bgra",
            _ => "rgb24"
        };
        Log($"capturing {first.width}x{first.height} {first.format} -> {fmt}");
        Directory.CreateDirectory(Path.Combine(shotDir, "stills"));
        string outFile = Path.Combine(shotDir, "trailer_video.mp4");
        var psi = new Proc.ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-y -loglevel error -f rawvideo -pix_fmt {fmt} -s {first.width}x{first.height} -r {Fps} -i - " +
                        $"-vf \"vflip,scale=1920:1080:flags=lanczos\" -c:v libx264 -preset slow -crf 17 -pix_fmt yuv420p \"{outFile}\"",
            UseShellExecute = false,
            RedirectStandardInput = true,
            CreateNoWindow = true
        };
        ffmpeg = Proc.Process.Start(psi);
        ffIn = ffmpeg.StandardInput.BaseStream;
    }

    void StopRecording()
    {
        recording = false;
        Time.captureFramerate = 0;
        if (cleanStills) { Log("clean stills captured"); return; }
        AudioRenderer.Stop();
        ffIn.Flush();
        ffIn.Close();
        ffmpeg.WaitForExit(120000);
        WriteWav(Path.Combine(shotDir, "trailer_audio.wav"), audioSamples, audioChannels, AudioSettings.outputSampleRate);
        Log($"trailer recorded: {frameCount} frames ({frameCount / (float)Fps:0.0}s), {audioSamples.Count / audioChannels} audio frames @ {AudioSettings.outputSampleRate} Hz");
    }

    static void WriteWav(string path, List<float> samples, int channels, int rate)
    {
        using var w = new BinaryWriter(File.Create(path));
        int dataBytes = samples.Count * 4;
        w.Write("RIFF".ToCharArray()); w.Write(36 + dataBytes); w.Write("WAVE".ToCharArray());
        w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)3); w.Write((short)channels);
        w.Write(rate); w.Write(rate * channels * 4); w.Write((short)(channels * 4)); w.Write((short)32);
        w.Write("data".ToCharArray()); w.Write(dataBytes);
        foreach (var s in samples) w.Write(s);
    }

    // ------------------------------------------------------------------ helpers

    static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    IEnumerator Sec(float s) { yield return new WaitForSeconds(s); }

    IEnumerator Dolly(Vector3 p0, Vector3 p1, Vector3 l0, Vector3 l1, float dur)
    {
        var p = PlayerController.I;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float k = Ease(t / dur);
            p.Teleport(Vector3.Lerp(p0, p1, k));
            p.LookAt(Vector3.Lerp(l0, l1, k));
            yield return null;
        }
    }

    IEnumerator Fade(float to, float dur)
    {
        float from = black;
        for (float t = 0; t < dur; t += Time.deltaTime) { black = Mathf.Lerp(from, to, t / dur); yield return null; }
        black = to;
    }

    /// Shows a lower-third (or full-screen) title card for `hold` seconds with fades; runs alongside the shot.
    IEnumerator Card(string title, string sub, float hold, bool big = false)
    {
        cardTitle = title; cardSub = sub; cardBig = big;
        for (float t = 0; t < 0.45f; t += Time.deltaTime) { cardAlpha = t / 0.45f; yield return null; }
        cardAlpha = 1f;
        yield return new WaitForSeconds(hold);
        for (float t = 0; t < 0.45f; t += Time.deltaTime) { cardAlpha = 1f - t / 0.45f; yield return null; }
        cardAlpha = 0f;
    }

    void Still(string name) => pendingStill = name;

    void OnGUI()
    {
        if (!trailerMode) return;
        GUI.depth = -100;
        float scale = Screen.height / 1080f, vw = Screen.width / scale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        if (cardTitleStyle == null)
        {
            cardTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 54, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter, richText = true };
            cardTitleStyle.normal.textColor = new Color(0.93f, 0.9f, 0.84f);
            cardSubStyle = new GUIStyle(cardTitleStyle) { fontSize = 30, fontStyle = FontStyle.Normal };
            cardSubStyle.normal.textColor = new Color(0.75f, 0.72f, 0.66f);
            endTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 120, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            endTitleStyle.normal.textColor = new Color(0.93f, 0.9f, 0.84f);
            endRedStyle = new GUIStyle(endTitleStyle) { fontSize = 64, fontStyle = FontStyle.Italic };
            endRedStyle.normal.textColor = new Color(0.86f, 0.24f, 0.2f);
            endSmallStyle = new GUIStyle(cardSubStyle) { fontSize = 28 };
        }

        var white = Texture2D.whiteTexture;
        if (black > 0.001f)
        {
            GUI.color = new Color(0, 0, 0, black);
            GUI.DrawTexture(new Rect(0, 0, vw, 1080), white);
        }

        if (endCard)
        {
            Shadowed(new Rect(0, 300, vw, 140), "THE CAMERA NEVER LIES", endTitleStyle, cardAlpha);
            Shadowed(new Rect(0, 440, vw, 90), "— does it?", endRedStyle, cardAlpha);
            Shadowed(new Rect(0, 600, vw, 40), "A first-person photo-horror demo  ·  2 chapters  ·  Made in Unity", endSmallStyle, cardAlpha);
            Shadowed(new Rect(0, 650, vw, 40), "Headphones recommended", endSmallStyle, cardAlpha * 0.8f);
        }
        else if (!cleanStills && cardAlpha > 0.001f && !string.IsNullOrEmpty(cardTitle))
        {
            if (cardBig)
            {
                Shadowed(new Rect(0, 460, vw, 80), cardTitle, cardTitleStyle, cardAlpha);
                if (!string.IsNullOrEmpty(cardSub)) Shadowed(new Rect(0, 545, vw, 50), cardSub, cardSubStyle, cardAlpha);
            }
            else
            {
                GUI.color = new Color(0, 0, 0, 0.45f * cardAlpha);
                GUI.DrawTexture(new Rect(0, 1080 - 230, vw, 230), white);
                Shadowed(new Rect(0, 1080 - 190, vw, 70), cardTitle, cardTitleStyle, cardAlpha);
                if (!string.IsNullOrEmpty(cardSub)) Shadowed(new Rect(0, 1080 - 115, vw, 50), cardSub, cardSubStyle, cardAlpha);
            }
        }
        GUI.color = Color.white;
    }

    static void Shadowed(Rect r, string s, GUIStyle st, float a)
    {
        GUI.color = new Color(0, 0, 0, 0.8f * a);
        GUI.Label(new Rect(r.x + 3, r.y + 3, r.width, r.height), s, st);
        GUI.color = new Color(1, 1, 1, a);
        GUI.Label(r, s, st);
    }

    // ------------------------------------------------------------------ the trailer

    IEnumerator RunTrailer()
    {
        trailerMode = true;
        UIManager.Cinematic = true;
        yield return Wait(1.5f);

        var gm = GameManager.I;
        gm.StartGame();
        yield return null;
        StartRecording();
        var p = PlayerController.I;

        // ---------------- Chapter 1 ----------------
        p.Teleport(new Vector3(1.0f, 0.05f, 0.6f));
        p.LookAt(new Vector3(3f, 1.0f, 3.6f));
        yield return Card("A missing photographer left you a camera.", null, 2.2f, true);

        StartCoroutine(Fade(0f, 1.2f));
        StartCoroutine(Card("\"Do not trust any photograph taken after midnight.\"", null, 3.6f));
        yield return Dolly(new Vector3(1.0f, 0.05f, 0.6f), new Vector3(2.5f, 0.05f, 2.3f), new Vector3(3f, 1.2f, 4.5f), new Vector3(2.75f, 0.8f, 3.6f), 5.2f);
        Still("01_reception");

        // The door that isn't there
        var writing = Find<Photographable>("WallWriting");
        var symbol = Find<Photographable>("EyeSymbol");
        p.Teleport(new Vector3(4.4f, 0.05f, 2.4f));
        p.LookAt(writing.FocusPoint);
        CameraTool.I.Raise();
        StartCoroutine(Card("Your camera sees what the world forgets.", null, 3.4f));
        yield return Dolly(new Vector3(4.4f, 0.05f, 2.4f), new Vector3(4.6f, 0.05f, 2.6f), writing.FocusPoint, symbol.FocusPoint, 3.2f);
        Still("02_eye_symbol");
        yield return Sec(0.6f);
        CameraTool.I.TryCapture();
        yield return Sec(2.2f);
        CameraTool.I.Lower();
        yield return Sec(1.3f);
        Still("03_door_revealed");

        // Portraits
        p.Teleport(new Vector3(11f, 0.05f, 6.6f));
        CameraTool.I.Raise();
        StartCoroutine(Card("Every photograph is evidence.", null, 3.2f));
        yield return Dolly(new Vector3(10.6f, 0.05f, 6.8f), new Vector3(11.4f, 0.05f, 6.8f), new Vector3(8f, 1.75f, 9.86f), new Vector3(14f, 1.75f, 9.86f), 4.6f);
        Still("04_portraits");
        CameraTool.I.Lower();

        // Corridor — the signature scare
        p.Teleport(new Vector3(17.5f, 0.05f, 8f));
        p.LookAt(new Vector3(29.5f, 1.6f, 8f));
        CameraTool.I.Raise();
        yield return Dolly(new Vector3(17.2f, 0.05f, 8f), new Vector3(18.6f, 0.05f, 8f), new Vector3(29.5f, 1.7f, 8f), new Vector3(29.5f, 1.6f, 8f), 2.4f);
        Still("05_corridor_viewfinder");
        CameraTool.I.TryCapture();
        yield return Sec(1.2f);
        StartCoroutine(Card("Some photographs change.", null, 2.6f));
        yield return Sec(1.4f);
        Still("06_behind_you");
        yield return Sec(2.2f);
        CameraTool.I.Lower();

        // Darkroom ending
        var darkDoor = Find<DoorInteractable>("DarkroomDoor");
        darkDoor.Open();
        var print = FindAnyObjectByType<DarkroomPhotoInteractable>();
        p.Teleport(new Vector3(28f, 0.05f, 10.3f));
        p.LookAt(print.transform.position);
        yield return Sec(1.0f);
        print.Interact();
        yield return Sec(0.3f);
        StartCoroutine(Card("It shows you. Right now.", null, 2.0f));
        yield return Sec(1.6f);
        Still("07_darkroom_print");
        yield return Sec(1.1f);
        UIManager.I.CloseReading();
        var flash = HorrorDirector.I.flashLight;
        float timeout = 10f;
        while (!flash.enabled && timeout > 0f) { timeout -= Time.deltaTime; yield return null; }
        yield return null;
        Still("08_darkroom_flash");
        yield return Sec(0.5f);
        black = 1f;

        // ---------------- Chapter 2 ----------------
        cardTitle = "CHAPTER 2"; cardSub = "SOMEONE WAS HERE"; cardBig = true; cardAlpha = 0f;
        StartCoroutine(Card("CHAPTER 2", "SOMEONE WAS HERE", 2.0f, true));
        gm.LoadChapter(GameManager.Chapter2Scene);
        yield return Sec(1.2f);
        yield return null;
        gm = GameManager.I;
        p = PlayerController.I;
        var dir = Chapter2Director.I;
        var ghost = GhostHunter.I;
        dir.AutoHunts = false;
        dir.Activated = true;
        ghost.Suspended = true;
        yield return Sec(1.4f);

        p.Teleport(new Vector3(1.2f, 0.05f, 1f));
        StartCoroutine(Fade(0f, 1.0f));
        StartCoroutine(Card("It follows whoever holds the camera.", null, 3.0f));
        StartCoroutine(FlickerLater(dir, 2.6f));
        yield return Dolly(new Vector3(1.2f, 0.05f, 1f), new Vector3(6.5f, 0.05f, 1f), new Vector3(10f, 1.6f, 1f), new Vector3(16f, 1.5f, 1f), 4.6f);
        Still("09_hallway");

        // Through the lens it cannot move
        p.Teleport(new Vector3(6f, 0.05f, 1f));
        p.LookAt(new Vector3(16f, 1.6f, 1f));
        ghost.BeginHunt(new Vector3(11.8f, 0, 1f));
        ghost.Suspended = false;
        CameraTool.I.Raise();
        StartCoroutine(Card("Through the lens, it cannot move.", null, 2.0f));
        yield return Sec(1.6f);
        Still("10_ghost_viewfinder");
        yield return Sec(0.9f);
        StartCoroutine(Card("But it hates being watched.", null, 1.6f));
        yield return Sec(1.6f);
        Still("11_ghost_lunge");
        yield return Sec(0.3f);
        CameraTool.I.TryCapture();
        yield return null;
        yield return null;
        Still("12_ghost_flash");
        StartCoroutine(Card("Your flash is your only weapon.", "Film is scarce.", 1.8f));
        yield return Sec(2.4f);
        CameraTool.I.Lower();
        ghost.Suspended = true;

        // Quick cuts: the camera lies
        StartCoroutine(Card("The camera lies.", "Learn when.", 4.0f));
        yield return QuickCut(new Vector3(5f, 0.05f, 7.1f), new Vector3(5f, 2.18f, 8.4f), "13_clock_lens");
        yield return QuickCut(new Vector3(14f, 0.05f, 6.4f), new Vector3(14f, 1.75f, 8.8f), "14_family_lens");
        CameraTool.I.Lower();
        p.Teleport(new Vector3(18.2f, 0.05f, 11.3f));
        p.LookAt(new Vector3(15.7f, 1.05f, 11.3f));
        yield return Sec(1.4f);
        Still("15_doll");

        // Don't let it find you
        p.Teleport(new Vector3(9f, 0.05f, 1f));
        p.LookAt(new Vector3(3f, 1.55f, 1f));
        ghost.BeginHunt(new Vector3(5.2f, 0, 1f));
        ghost.SpeedMultiplier = 0.9f;
        ghost.Suspended = false;
        StartCoroutine(Card("Don't let it find you.", null, 1.4f));
        bool scareStill = false;
        timeout = 8f;
        while (gm.State != GameState.Dead && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            if (!scareStill && JumpScare.I.Playing && JumpScare.I.face.localPosition.z < 0.5f) { scareStill = true; Still("16_jumpscare"); }
            yield return null;
        }
        yield return Sec(2.2f);
        Still("17_it_found_you");
        yield return Sec(0.6f);

        // End card
        yield return Fade(1f, 0.6f);
        endCard = true;
        for (float t = 0; t < 0.8f; t += Time.deltaTime) { cardAlpha = t / 0.8f; yield return null; }
        cardAlpha = 1f;
        yield return Sec(0.2f);
        Still("18_title_card");
        yield return Sec(3.8f);

        StopRecording();
        Log("TRAILER DONE");
        yield return Wait(0.5f);
        Application.Quit();
    }

    IEnumerator QuickCut(Vector3 pos, Vector3 look, string still)
    {
        var p = PlayerController.I;
        CameraTool.I.Lower();
        yield return null;
        p.Teleport(pos);
        p.LookAt(look);
        CameraTool.I.Raise();
        yield return Dolly(pos, pos + PlayerController.I.FlatForward * 0.35f, look, look, 1.6f);
        Still(still);
        yield return null;
    }

    IEnumerator FlickerLater(Chapter2Director dir, float delay)
    {
        yield return new WaitForSeconds(delay);
        foreach (var l in dir.hallLights) l.Burst(1.2f);
        AudioManager.I.Play2D(AudioManager.I.staticBurst, 0.6f);
    }
}
