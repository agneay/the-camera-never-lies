using System;
using UnityEngine;

/// Menus, prompts, viewfinder overlay, evidence gallery, notes, subtitles and the ending card.
/// Drawn with IMGUI in a 1080p virtual canvas so it scales to any resolution without extra packages.
public class UIManager : MonoBehaviour
{
    public static UIManager I;

    const float VH = 1080f;
    float VW;

    Texture2D white, noiseTex;
    GUIStyle sTitle, sTitleRed, sH1, sBody, sSmall, sButton, sPrompt, sCenter, sCenterBig, sNoteTitle, sNoteBody, sThumbCap, sBadge;
    bool stylesReady;

    string toast; float toastStart, toastUntil;
    string subtitle; float subtitleUntil;
    string hint; float hintStart, hintUntil;
    PhotoEntry review; float reviewStart, reviewUntil; bool reviewBig;

    bool noteOpen; string noteTitle, noteBody;
    Texture2D inspectTex; string inspectCaption;
    Action onReadClose;

    bool settingsOpen;
    PhotoEntry zoom;
    Vector2 scroll;
    float fade, fadeTarget, fadeSpeed = 1f;
    float endingStart = -1f;

    static readonly Color Ink = new Color(0.9f, 0.87f, 0.8f);
    static readonly Color Dim = new Color(0.62f, 0.6f, 0.56f);
    static readonly Color Blood = new Color(0.86f, 0.24f, 0.2f);

    public bool SettingsOpen => settingsOpen;
    public static bool Cinematic; // trailer recording: hide gameplay HUD, keep the viewfinder and photo review

    void Awake()
    {
        I = this;
        white = Texture2D.whiteTexture;
        noiseTex = new Texture2D(256, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        var px = new Color32[256 * 256];
        var r = new System.Random(7);
        for (int i = 0; i < px.Length; i++) { byte v = (byte)r.Next(256); px[i] = new Color32(v, v, v, (byte)r.Next(256)); }
        noiseTex.SetPixels32(px);
        noiseTex.Apply();
    }

    // ---------------- API ----------------

    public void Toast(string s, float dur = 3f) { toast = s; toastStart = Time.unscaledTime; toastUntil = toastStart + dur; }
    public void Subtitle(string s, float dur = 3.5f) { if (!Settings.Subtitles) return; subtitle = s; subtitleUntil = Time.unscaledTime + dur; }
    public void Hint(string s, float dur = 6f) { hint = s; hintStart = Time.unscaledTime; hintUntil = hintStart + dur; }

    public void ShowReview(PhotoEntry e, float dur, bool big)
    {
        review = e; reviewBig = big;
        reviewStart = Time.unscaledTime; reviewUntil = reviewStart + dur;
    }

    public void ShowNote(string title, string body, Action onClose)
    {
        CameraTool.I.Lower();
        noteOpen = true; inspectTex = null;
        noteTitle = title; noteBody = body; onReadClose = onClose;
        GameManager.I.SetState(GameState.Reading);
    }

    public void ShowInspect(Texture2D tex, string caption, Action onClose)
    {
        noteOpen = false; inspectTex = tex; inspectCaption = caption; onReadClose = onClose;
        GameManager.I.SetState(GameState.Reading);
    }

    public void CloseReading()
    {
        noteOpen = false; inspectTex = null;
        GameManager.I.SetState(GameState.Playing);
        var cb = onReadClose; onReadClose = null;
        cb?.Invoke();
    }

    public void FadeFromBlack(float seconds) { fade = 1f; fadeTarget = 0f; fadeSpeed = 1f / seconds; }
    public void CloseSettings() { if (settingsOpen) { settingsOpen = false; Settings.Save(); } }
    public bool GalleryBack() { if (zoom != null) { zoom = null; return true; } return false; }
    public void BeginEnding() { endingStart = Time.unscaledTime; }

    void Update()
    {
        var gm = GameManager.I;
        if (gm.State == GameState.Reading && Time.frameCount > gm.StateFrame + 1 && !PlayerController.ScriptedLook &&
            (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0)))
            CloseReading();
        fade = Mathf.MoveTowards(fade, fadeTarget, Time.unscaledDeltaTime * fadeSpeed);
    }

    // ---------------- Drawing ----------------

    void InitStyles()
    {
        stylesReady = true;
        GUIStyle Make(int size, Color c, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.UpperLeft, bool wrap = true)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fs, alignment = a, wordWrap = wrap, richText = true };
            s.normal.textColor = c;
            return s;
        }
        sTitle = Make(104, Ink, FontStyle.Bold, TextAnchor.UpperLeft, false);
        sTitleRed = Make(56, Blood, FontStyle.Italic, TextAnchor.UpperLeft, false);
        sH1 = Make(44, Ink, FontStyle.Bold);
        sBody = Make(28, Ink);
        sSmall = Make(22, Dim);
        sPrompt = Make(30, Ink, FontStyle.Bold, TextAnchor.MiddleCenter, false);
        sCenter = Make(28, Ink, FontStyle.Normal, TextAnchor.MiddleCenter);
        sCenterBig = Make(64, Ink, FontStyle.Italic, TextAnchor.MiddleCenter);
        sNoteTitle = Make(34, new Color(0.18f, 0.13f, 0.1f), FontStyle.Bold);
        sNoteBody = Make(28, new Color(0.2f, 0.16f, 0.13f), FontStyle.Italic);
        sThumbCap = Make(22, Ink, FontStyle.Normal, TextAnchor.UpperLeft, false);
        sThumbCap.clipping = TextClipping.Clip;
        sBadge = Make(18, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter, false);

        sButton = new GUIStyle(GUI.skin.label) { fontSize = 36, alignment = TextAnchor.MiddleLeft, richText = true };
        sButton.normal.textColor = new Color(0.78f, 0.75f, 0.7f);
        sButton.hover.textColor = new Color(1f, 0.5f, 0.42f);
        sButton.active.textColor = Blood;
        var hoverBg = new Texture2D(1, 1);
        hoverBg.SetPixel(0, 0, new Color(1f, 1f, 1f, 0.05f));
        hoverBg.Apply();
        sButton.hover.background = hoverBg;
        sButton.padding = new RectOffset(16, 8, 4, 4);
    }

    void Box(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = o; }

    void Text(Rect r, string s, GUIStyle st, float a = 1f, bool shadow = true)
    {
        if (shadow)
        {
            GUI.color = new Color(0, 0, 0, 0.75f * a);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), s, st);
        }
        GUI.color = new Color(1, 1, 1, a);
        GUI.Label(r, s, st);
        GUI.color = Color.white;
    }

    bool Button(Rect r, string label) => GUI.Button(r, label, sButton);

    void Corners(Rect r, float len, float th, Color c)
    {
        Box(new Rect(r.x, r.y, len, th), c); Box(new Rect(r.x, r.y, th, len), c);
        Box(new Rect(r.xMax - len, r.y, len, th), c); Box(new Rect(r.xMax - th, r.y, th, len), c);
        Box(new Rect(r.x, r.yMax - th, len, th), c); Box(new Rect(r.x, r.yMax - len, th, len), c);
        Box(new Rect(r.xMax - len, r.yMax - th, len, th), c); Box(new Rect(r.xMax - th, r.yMax - len, th, len), c);
    }

    void Noise(float alpha)
    {
        GUI.color = new Color(1, 1, 1, alpha);
        GUI.DrawTextureWithTexCoords(new Rect(0, 0, VW, VH), noiseTex,
            new Rect(UnityEngine.Random.value, UnityEngine.Random.value, VW / 256f, VH / 256f));
        GUI.color = Color.white;
    }

    void OnGUI()
    {
        if (!stylesReady) InitStyles();
        float scale = Screen.height / VH;
        VW = Screen.width / scale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        var gm = GameManager.I;
        var st = gm.State;

        if (st == GameState.Playing || st == GameState.Cutscene || st == GameState.Reading)
            DrawHUD(gm);

        if (fade > 0.001f) Box(new Rect(0, 0, VW, VH), new Color(0, 0, 0, fade));

        float rf = redFlashUntil - Time.unscaledTime;
        if (rf > 0f) Box(new Rect(0, 0, VW, VH), new Color(0.6f, 0f, 0f, Mathf.Clamp01(rf / redFlashLength) * 0.55f));

        switch (st)
        {
            case GameState.Menu: DrawMenu(); break;
            case GameState.Paused: DrawPause(gm); break;
            case GameState.Gallery: DrawGallery(); break;
            case GameState.Reading: if (noteOpen) DrawNote(); else if (inspectTex) DrawInspect(); break;
            case GameState.Ending: DrawEnding(); break;
            case GameState.Dead: DrawDead(); break;
        }
    }

    float redFlashUntil, redFlashLength = 1f, deadSince = -1f;

    public void RedFlash(float seconds)
    {
        redFlashLength = seconds;
        redFlashUntil = Time.unscaledTime + seconds;
    }

    void DrawDead()
    {
        if (deadSince < 0f) deadSince = Time.unscaledTime;
        float t = Time.unscaledTime - deadSince;
        Box(new Rect(0, 0, VW, VH), new Color(0.05f, 0f, 0f, 1f));
        Noise(0.06f);
        float a1 = Mathf.Clamp01((t - 0.3f) / 0.8f), a2 = Mathf.Clamp01((t - 1.4f) / 0.8f);
        var red = new GUIStyle(sCenterBig) { fontSize = 96, fontStyle = FontStyle.Bold };
        red.normal.textColor = Blood;
        Text(new Rect(0, VH / 2 - 210, VW, 120), "IT FOUND YOU.", red, a1);
        Text(new Rect(0, VH / 2 - 80, VW, 40), "Your last photograph is dated 00:00. You are in it.", sCenter, a1);

        if (a2 <= 0f) return;
        GUI.color = new Color(1, 1, 1, a2);
        float bx = VW / 2 - 200, by = VH / 2 + 40;
        if (Button(new Rect(bx, by, 400, 56), "Retry from checkpoint")) { deadSince = -1f; Chapter2Director.I.Retry(); }
        if (Button(new Rect(bx, by + 64, 400, 56), "Restart chapter")) { deadSince = -1f; GameManager.I.Restart(); }
        if (Button(new Rect(bx, by + 128, 400, 56), "Main Menu")) { deadSince = -1f; GameManager.I.MainMenu(); }
        GUI.color = Color.white;
        Text(new Rect(0, VH - 120, VW, 30), "Keep the lens on it to freeze it — but never for too long. The flash drives it back.", new GUIStyle(sCenter) { fontSize = 22 }, a2);
    }

    void DrawHUD(GameManager gm)
    {
        var cam = CameraTool.I;
        float vf = cam.RaiseBlend;
        float now = Time.unscaledTime;

        if (vf > 0.01f && gm.State != GameState.Reading) DrawViewfinder(gm, cam, vf);

        if (gm.State == GameState.Playing && !Cinematic)
        {
            var focus = PlayerController.I.Focused;
            if (vf < 0.5f)
            {
                float s = focus ? 8f : 5f;
                Box(new Rect(VW / 2 - s / 2, VH / 2 - s / 2, s, s), new Color(1, 1, 1, focus ? 0.9f : 0.35f));
            }
            if (focus) Text(new Rect(VW / 2 - 400, VH * 0.56f, 800, 50), "<color=#e8b04a>[E]</color>   " + focus.Prompt, sPrompt);

            // Stamina: a thin bar that only shows while recovering.
            float st = PlayerController.I.Stamina;
            if (st < 0.99f)
            {
                var r = new Rect(VW / 2 - 150, VH - 60, 300, 5);
                Box(r, new Color(1, 1, 1, 0.15f));
                Box(new Rect(r.x, r.y, r.width * st, r.height), st < 0.3f ? new Color(Blood.r, Blood.g, Blood.b, 0.8f) : new Color(1, 1, 1, 0.55f));
            }
        }

        // Objective
        float od = now - gm.ObjectiveChangedAt;
        if (!Cinematic && od < 8f && !string.IsNullOrEmpty(gm.Objective))
        {
            float a = Mathf.Clamp01(od / 0.4f) * Mathf.Clamp01((8f - od) / 1.5f);
            float oy = 40 + vf * 120f; // slide below the viewfinder readout
            Text(new Rect(48 + vf * 40f, oy, 900, 30), "OBJECTIVE", sSmall, a);
            Text(new Rect(48 + vf * 40f, oy + 28, 900, 80), gm.Objective, sBody, a);
        }

        // Photo review
        if (review != null && now < reviewUntil)
        {
            float t = now - reviewStart;
            float a = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((reviewUntil - now) / 0.3f);
            if (reviewBig)
            {
                float w = 1100, h = 619;
                var r = new Rect(VW / 2 - w / 2, VH / 2 - h / 2 - 30, w, h);
                Box(new Rect(r.x - 14, r.y - 14, r.width + 28, r.height + 70), new Color(0.9f, 0.88f, 0.83f, a));
                GUI.color = new Color(1, 1, 1, a);
                GUI.DrawTexture(r, review.tex);
                GUI.color = Color.white;
                var cap = new GUIStyle(sNoteTitle) { fontSize = 26 };
                Text(new Rect(r.x, r.yMax + 12, w, 40), review.title + "      " + review.time, cap, a, false);
            }
            else
            {
                float w = 384, h = 216;
                float slide = Mathf.SmoothStep(80f, 0f, Mathf.Clamp01(t / 0.25f));
                var r = new Rect(VW - w - 64 + slide, VH - h - 140, w, h);
                Box(new Rect(r.x - 6, r.y - 6, w + 12, h + 12), new Color(0.9f, 0.88f, 0.83f, a));
                GUI.color = new Color(1, 1, 1, a);
                GUI.DrawTexture(r, review.tex);
                GUI.color = Color.white;
                Text(new Rect(r.x, r.yMax + 10, w, 30), review.title, sSmall, a);
            }
        }

        // Hint
        if (!Cinematic && !string.IsNullOrEmpty(hint) && now < hintUntil)
        {
            float a = Mathf.Clamp01((now - hintStart) / 0.5f) * Mathf.Clamp01((hintUntil - now) / 1f);
            var s = new GUIStyle(sCenter) { fontSize = 24 };
            s.normal.textColor = Dim;
            Text(new Rect(VW / 2 - 700, VH - 200, 1400, 40), hint, s, a);
        }

        // Subtitles
        if (!Cinematic && !string.IsNullOrEmpty(subtitle) && now < subtitleUntil)
        {
            var size = sCenter.CalcSize(new GUIContent(subtitle));
            var r = new Rect(VW / 2 - size.x / 2 - 20, VH - 130, size.x + 40, 52);
            Box(r, new Color(0, 0, 0, 0.6f));
            Text(r, subtitle, sCenter, 1f, false);
        }

        // Toast
        if (!Cinematic && !string.IsNullOrEmpty(toast) && now < toastUntil)
        {
            float a = Mathf.Clamp01((now - toastStart) / 0.25f) * Mathf.Clamp01((toastUntil - now) / 0.6f);
            var size = sCenter.CalcSize(new GUIContent(toast));
            var r = new Rect(VW / 2 - size.x / 2 - 24, 120, size.x + 48, 54);
            Box(r, new Color(0.05f, 0.04f, 0.04f, 0.7f * a));
            Box(new Rect(r.x, r.yMax - 3, r.width, 3), new Color(Blood.r, Blood.g, Blood.b, 0.8f * a));
            Text(r, toast, sCenter, a, false);
        }
    }

    void DrawViewfinder(GameManager gm, CameraTool cam, float a)
    {
        Noise(0.05f * a);
        var c = new Color(1, 1, 1, 0.85f * a);
        float m = 56;
        var frame = new Rect(m, m, VW - 2 * m, VH - 2 * m);
        Corners(frame, 90, 3, c);

        bool blink = (Time.unscaledTime % 1f) < 0.6f;
        string clock = gm.AfterMidnight ? $"<color=#ff5040>{gm.ClockText}</color>" : gm.ClockText;
        Text(new Rect(m + 34, m + 24, 600, 40), (blink ? "<color=#ff4030>●</color>  " : "     ") + clock, sBody, a);
        Text(new Rect(m + 34, m + 62, 600, 30), "AF-S    ISO 3200    1/30    f2.8", sSmall, a);

        // Battery
        var b = new Rect(frame.xMax - 120, m + 30, 64, 26);
        Corners(b, 64, 2, c);
        Box(new Rect(b.xMax, b.y + 8, 5, 10), c);
        for (int i = 0; i < 3; i++) Box(new Rect(b.x + 6 + i * 19, b.y + 6, 14, 14), i < 2 ? c : new Color(1, 1, 1, 0.2f * a));
        Text(new Rect(frame.xMax - 320, m + 66, 260, 30), $"x{cam.ZoomFactor:0.0}", new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight }, a);
        if (cam.Film >= 0)
        {
            string film = cam.Film == 0 ? "<color=#ff5040>NO FILM</color>" : $"FILM  {cam.Film}";
            Text(new Rect(frame.xMax - 420, m + 100, 360, 36), film, new GUIStyle(sBody) { alignment = TextAnchor.UpperRight }, a);
        }

        Text(new Rect(m + 34, frame.yMax - 64, 800, 30), "[Left Mouse] CAPTURE     [Wheel] ZOOM     [Right Mouse] LOWER", sSmall, a);

        // Focus brackets: shape + label, not colour alone.
        var target = cam.FocusTarget;
        var fr = new Rect(VW / 2 - 110, VH / 2 - 75, 220, 150);
        var fc = target ? new Color(0.65f, 1f, 0.7f, 0.95f * a) : new Color(1, 1, 1, 0.5f * a);
        Corners(fr, target ? 34 : 22, target ? 3 : 2, fc);
        Box(new Rect(VW / 2 - 8, VH / 2 - 1, 16, 2), fc);
        Box(new Rect(VW / 2 - 1, VH / 2 - 8, 2, 16), fc);
        string status = cam.CooldownLeft > 0f ? "SAVING…" : target ? "● FOCUS LOCKED" : "";
        if (status != "") Text(new Rect(VW / 2 - 200, fr.yMax + 10, 400, 30), status, new GUIStyle(sSmall) { alignment = TextAnchor.UpperCenter }, a);
    }

    void DrawMenu()
    {
        Box(new Rect(0, 0, VW, VH), new Color(0.02f, 0.015f, 0.015f, 0.8f));
        Noise(0.04f);
        float x = 140;
        Text(new Rect(x, 150, 1200, 120), "THE CAMERA", sTitle);
        Text(new Rect(x, 255, 1200, 120), "NEVER LIES", sTitle);
        Text(new Rect(x + 6, 375, 1000, 80), "— does it?", sTitleRed);

        if (settingsOpen) { DrawSettings(x, 500); return; }

        float y = 500;
        if (Button(new Rect(x - 16, y, 620, 56), "New Game")) GameManager.I.NewGame();
        if (Button(new Rect(x - 16, y + 64, 620, 56), "Chapter 2 — Someone Was Here  <size=22><color=#d04030>harder</color></size>"))
            GameManager.I.LoadChapter(GameManager.Chapter2Scene);
        if (Button(new Rect(x - 16, y + 128, 620, 56), "Settings")) settingsOpen = true;
        if (Button(new Rect(x - 16, y + 192, 620, 56), "Quit")) GameManager.I.Quit();

        Text(new Rect(x, VH - 150, 1600, 30), "Chapter 1 · The Empty Studio      Chapter 2 · Someone Was Here", sSmall);
        Text(new Rect(x, VH - 115, 1700, 30),
            "WASD move  ·  Shift sprint  ·  Mouse look  ·  Right Mouse raise camera  ·  Left Mouse photograph  ·  E interact  ·  Tab evidence  ·  Esc pause", sSmall);
        Text(new Rect(x, VH - 80, 1600, 30), "Headphones recommended.", sSmall);
    }

    void DrawPause(GameManager gm)
    {
        Box(new Rect(0, 0, VW, VH), new Color(0, 0, 0, 0.78f));
        float x = 140;
        Text(new Rect(x, 120, 800, 80), "PAUSED", sH1);
        if (settingsOpen) { DrawSettings(x, 220); return; }

        float y = 220;
        if (Button(new Rect(x - 16, y, 420, 56), "Resume")) gm.Resume();
        if (Button(new Rect(x - 16, y + 64, 420, 56), "Settings")) settingsOpen = true;
        if (Button(new Rect(x - 16, y + 128, 420, 56), "Restart Chapter")) gm.Restart();
        if (Button(new Rect(x - 16, y + 192, 420, 56), "Main Menu")) gm.MainMenu();
        if (Button(new Rect(x - 16, y + 256, 420, 56), "Quit")) gm.Quit();

        float rx = Mathf.Max(700, VW - 820);
        Text(new Rect(rx, 130, 700, 30), "OBJECTIVE", sSmall);
        Text(new Rect(rx, 160, 700, 100), gm.Objective, sBody);
        Text(new Rect(rx, 300, 700, 30), "CONTROLS", sSmall);
        Text(new Rect(rx, 330, 700, 400),
            "W A S D — Move\nShift — Sprint\nMouse — Look\nRight Mouse — Raise / lower camera\nLeft Mouse — Take photograph\nMouse Wheel — Zoom (camera raised)\nE — Interact\nTab — Evidence\nEsc — Pause\nF11 — Fullscreen", sBody);
        Text(new Rect(rx, 700, 700, 30), $"Evidence: {EvidenceManager.I.Found.Count} / {EvidenceManager.I.TotalClues} clues", sSmall);
    }

    void DrawSettings(float x, float y)
    {
        float w = 640;
        Text(new Rect(x, y, w, 40), "SETTINGS", sSmall);
        y += 50;

        Text(new Rect(x, y, w, 36), $"Mouse sensitivity   {Settings.Sensitivity:0.0}", sBody);
        Settings.Sensitivity = GUI.HorizontalSlider(new Rect(x, y + 44, w, 24), Settings.Sensitivity, 0.3f, 6f);
        y += 90;
        Text(new Rect(x, y, w, 36), $"Master volume   {Mathf.RoundToInt(Settings.Volume * 100)}%", sBody);
        float v = GUI.HorizontalSlider(new Rect(x, y + 44, w, 24), Settings.Volume, 0f, 1f);
        if (!Mathf.Approximately(v, Settings.Volume)) { Settings.Volume = v; Settings.Apply(); }
        y += 90;
        Text(new Rect(x, y, w, 36), $"Brightness   {Settings.Brightness:0.00}", sBody);
        Settings.Brightness = GUI.HorizontalSlider(new Rect(x, y + 44, w, 24), Settings.Brightness, 0.6f, 1.8f);
        y += 100;

        if (Button(new Rect(x - 16, y, w, 52), "Subtitles:  " + (Settings.Subtitles ? "On" : "Off"))) Settings.Subtitles = !Settings.Subtitles;
        y += 60;
        if (Button(new Rect(x - 16, y, w, 52), "Reduced flashing & glitches:  " + (Settings.ReducedEffects ? "On" : "Off")))
            Settings.ReducedEffects = !Settings.ReducedEffects;
        y += 80;
        if (Button(new Rect(x - 16, y, 300, 56), "‹  Back")) CloseSettings();
    }

    void DrawGallery()
    {
        Box(new Rect(0, 0, VW, VH), new Color(0.03f, 0.025f, 0.025f, 0.94f));
        var em = EvidenceManager.I;
        Text(new Rect(100, 50, 800, 60), "EVIDENCE", sH1);
        Text(new Rect(100, 104, 1200, 30), $"{em.Found.Count} of {em.TotalClues} hidden clues found   ·   {em.Photos.Count} photographs", sSmall);
        Text(new Rect(VW - 600, 60, 500, 30), "[Tab] / [Esc]  Close", new GUIStyle(sSmall) { alignment = TextAnchor.UpperRight });

        if (zoom != null) { DrawZoom(); return; }

        if (em.Photos.Count == 0)
        {
            Text(new Rect(100, 200, 1400, 80), "No photographs yet.\nRaise the camera with [Right Mouse], then press [Left Mouse].", sBody);
            return;
        }

        const int cols = 4;
        float gap = 28, tw = (Mathf.Min(VW, 1920) - 200 - gap * (cols - 1)) / cols, th = tw * 9f / 16f;
        int rows = Mathf.CeilToInt(em.Photos.Count / (float)cols);
        var view = new Rect(100, 160, VW - 160, VH - 200);
        var content = new Rect(0, 0, VW - 200, rows * (th + 70));
        scroll = GUI.BeginScrollView(view, scroll, content);
        for (int i = 0; i < em.Photos.Count; i++)
        {
            var e = em.Photos[em.Photos.Count - 1 - i]; // newest first
            float x = (i % cols) * (tw + gap), y = (i / cols) * (th + 70);
            var r = new Rect(x, y, tw, th);
            Box(new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8), new Color(0.88f, 0.86f, 0.8f));
            GUI.DrawTexture(r, e.tex);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { zoom = e; e.isNew = false; }
            if (r.Contains(Event.current.mousePosition)) Box(r, new Color(1, 1, 1, 0.08f));
            if (e.isNew)
            {
                var br = new Rect(r.x + 8, r.y + 8, 64, 26);
                Box(br, Blood);
                GUI.Label(br, "NEW", sBadge);
            }
            Text(new Rect(x, y + th + 10, tw, 30), e.title, sThumbCap);
            Text(new Rect(x, y + th + 36, tw, 26), e.time, sSmall);
        }
        GUI.EndScrollView();
    }

    void DrawZoom()
    {
        float w = Mathf.Min(1150, VW - 760), h = w * 9f / 16f;
        var r = new Rect(100, 170, w, h);
        Box(new Rect(r.x - 12, r.y - 12, w + 24, h + 24), new Color(0.88f, 0.86f, 0.8f));
        GUI.DrawTexture(r, zoom.tex);
        float tx = r.xMax + 60, tw = VW - tx - 80;
        Text(new Rect(tx, 170, tw, 100), zoom.title, sH1);
        Text(new Rect(tx, 280, tw, 30), "Taken at " + zoom.time, sSmall);
        Text(new Rect(tx, 330, tw, 520), zoom.desc, sBody);
        if (Button(new Rect(r.x - 16, r.yMax + 40, 300, 56), "‹  Back")) zoom = null;
    }

    void DrawNote()
    {
        Box(new Rect(0, 0, VW, VH), new Color(0, 0, 0, 0.72f));
        float w = 820, h = 640;
        var r = new Rect(VW / 2 - w / 2, VH / 2 - h / 2, w, h);
        Box(new Rect(r.x + 10, r.y + 12, w, h), new Color(0, 0, 0, 0.5f));
        Box(r, new Color(0.84f, 0.8f, 0.7f));
        Box(new Rect(r.x, r.y, w, 8), new Color(0.7f, 0.64f, 0.52f));
        GUI.Label(new Rect(r.x + 60, r.y + 50, w - 120, 60), noteTitle, sNoteTitle);
        GUI.Label(new Rect(r.x + 60, r.y + 120, w - 120, h - 200), noteBody, sNoteBody);
        Text(new Rect(VW / 2 - 300, r.yMax + 24, 600, 40), "[E]  Close", sCenter);
    }

    void DrawInspect()
    {
        Box(new Rect(0, 0, VW, VH), new Color(0, 0, 0, 0.88f));
        float w = 1120, h = 630;
        var r = new Rect(VW / 2 - w / 2, VH / 2 - h / 2 - 60, w, h);
        Box(new Rect(r.x - 20, r.y - 20, w + 40, h + 40), new Color(0.86f, 0.83f, 0.76f));
        GUI.DrawTexture(r, inspectTex);
        Box(r, new Color(0.25f, 0.02f, 0.02f, 0.18f)); // safelight tint
        Text(new Rect(VW / 2 - 700, r.yMax + 40, 1400, 90), inspectCaption, sCenter);
        Text(new Rect(VW / 2 - 300, r.yMax + 140, 600, 40), "[E]  Put it down", new GUIStyle(sCenter) { fontSize = 24 });
    }

    void DrawEnding()
    {
        Box(new Rect(0, 0, VW, VH), Color.black);
        float t = endingStart < 0 ? 0 : Time.unscaledTime - endingStart;
        float a1 = Mathf.Clamp01((t - 0.8f) / 1.2f);
        float a2 = Mathf.Clamp01((t - 3.2f) / 1.0f);
        float a3 = Mathf.Clamp01((t - 6f) / 1.0f);

        var cfg = ChapterConfig.I;
        Text(new Rect(0, VH / 2 - 160, VW, 90), cfg ? cfg.endLine1 : "The camera never lies.", sCenterBig, a1);
        var red = new GUIStyle(sCenterBig);
        red.normal.textColor = Blood;
        Text(new Rect(0, VH / 2 - 70, VW, 90), cfg ? cfg.endLine2 : "— does it?", red, a2);

        if (a3 <= 0f) return;
        var em = EvidenceManager.I;
        string chapter = cfg ? $"END OF CHAPTER {ChapterConfig.NumberWord(cfg.number)}  ·  {cfg.title.ToUpper()}" : "THE END";
        Text(new Rect(0, VH / 2 + 50, VW, 40), chapter, sCenter, a3);
        string stats = $"Hidden clues found: {em.Found.Count} / {em.TotalClues}";
        if (Chapter2Director.I) stats += $"      Deaths: {Chapter2Director.I.Deaths}";
        Text(new Rect(0, VH / 2 + 92, VW, 40), stats, new GUIStyle(sCenter) { fontSize = 24 }, a3);
        if (cfg && !string.IsNullOrEmpty(cfg.teaser))
            Text(new Rect(0, VH / 2 + 128, VW, 40), cfg.teaser, new GUIStyle(sCenter) { fontSize = 22 }, a3);

        GUI.color = new Color(1, 1, 1, a3);
        float bx = VW / 2 - 200, by = VH / 2 + 180;
        var gm = GameManager.I;
        if (cfg && !string.IsNullOrEmpty(cfg.nextScene))
        {
            if (Button(new Rect(bx, by, 400, 56), $"Continue — {cfg.nextTitle}")) gm.LoadChapter(cfg.nextScene);
            by += 64;
        }
        if (Button(new Rect(bx, by, 400, 56), "Play Again")) gm.Restart();
        if (Button(new Rect(bx, by + 64, 400, 56), "Main Menu")) gm.MainMenu();
        if (Button(new Rect(bx, by + 128, 400, 56), "Quit")) gm.Quit();
        GUI.color = Color.white;
    }
}
