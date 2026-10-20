using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState { Menu, Playing, Paused, Gallery, Reading, Cutscene, Ending, Dead }

/// Tracks game state, objective progress, story flags and the in-game clock.
public class GameManager : MonoBehaviour
{
    public static GameManager I;
    static bool startImmediately; // set by Restart so the reloaded scene skips the title screen

    public GameState State { get; private set; } = GameState.Menu;
    public int StateFrame { get; private set; }
    public string Objective { get; private set; } = "";
    public float ObjectiveChangedAt { get; private set; } = -100f;
    public bool AfterMidnight { get; private set; }

    readonly HashSet<string> flags = new HashSet<string>();
    float clockMinutes = 23 * 60 + 47;

    void Awake()
    {
        I = this;
        Settings.Load();
        QualitySettings.pixelLightCount = 8;
        Application.targetFrameRate = 120;
    }

    public const string Chapter1Scene = "Chapter1";
    public const string Chapter2Scene = "Chapter2";

    public static event System.Action GameStarted;

    void Start()
    {
        var cfg = ChapterConfig.I;
        if (cfg)
        {
            clockMinutes = cfg.clockStartMinutes;
            AfterMidnight = cfg.startAfterMidnight;
        }
        if (startImmediately)
        {
            startImmediately = false;
            StartGame();
        }
        else SetState(GameState.Menu);
    }

    public void StartGame()
    {
        var cfg = ChapterConfig.I;
        SetState(GameState.Playing);
        AudioManager.I.StartAmbience();
        UIManager.I.FadeFromBlack(2.5f);
        SetObjective(cfg ? cfg.startObjective : "");
        if (cfg && !string.IsNullOrEmpty(cfg.startHint)) UIManager.I.Hint(cfg.startHint, 8f);
        if (cfg && cfg.startFilm >= 0) CameraTool.I.Film = cfg.startFilm;
        GameStarted?.Invoke();
    }

    public bool IsPlaying => State == GameState.Playing;

    public void SetState(GameState s)
    {
        State = s;
        StateFrame = Time.frameCount;
        bool locked = s == GameState.Playing || s == GameState.Cutscene || s == GameState.Reading;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
        Time.timeScale = (s == GameState.Paused || s == GameState.Gallery || s == GameState.Menu) ? 0f : 1f;
    }

    /// Loads a chapter scene and starts it straight away (no title screen).
    public void LoadChapter(string scene)
    {
        startImmediately = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene);
    }

    /// Starts Chapter 1 from the title screen, wherever the title screen is showing.
    public void NewGame()
    {
        if (SceneManager.GetActiveScene().name == Chapter1Scene) StartGame();
        else LoadChapter(Chapter1Scene);
    }

    /// True only if no state change happened this frame or last (prevents one key press doing two things).
    public bool InputReady => IsPlaying && Time.frameCount > StateFrame + 1;

    public void SetObjective(string o)
    {
        if (o == Objective) return;
        Objective = o;
        ObjectiveChangedAt = Time.unscaledTime;
    }

    public bool Has(string flag) => flags.Contains(flag);
    public void Set(string flag) => flags.Add(flag);

    public string ClockText
    {
        get
        {
            if (AfterMidnight && clockMinutes >= 23 * 60) return "00:00"; // Chapter 1: frozen at midnight
            int m = (int)clockMinutes;
            return $"{m / 60:00}:{m % 60:00}";
        }
    }

    public void StrikeMidnight() => AfterMidnight = true;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F11)) Screen.fullScreen = !Screen.fullScreen;

        if (State == GameState.Playing && (!AfterMidnight || clockMinutes < 23 * 60))
            clockMinutes = Mathf.Min(clockMinutes + Time.deltaTime / 30f, clockMinutes < 23 * 60 ? 23 * 60 - 1 : 23 * 60 + 59);

        if (Time.frameCount <= StateFrame + 1 || PlayerController.ScriptedLook) return;

        switch (State)
        {
            case GameState.Playing:
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) Pause();
                else if (Input.GetKeyDown(KeyCode.Tab)) OpenGallery();
                break;
            case GameState.Paused:
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
                {
                    if (UIManager.I.SettingsOpen) UIManager.I.CloseSettings();
                    else Resume();
                }
                break;
            case GameState.Gallery:
                if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape))
                {
                    if (!UIManager.I.GalleryBack()) Resume();
                }
                break;
        }
    }

    public void Pause() => SetState(GameState.Paused);
    public void Resume() { UIManager.I.CloseSettings(); SetState(GameState.Playing); }

    public void OpenGallery()
    {
        CameraTool.I.Lower();
        SetState(GameState.Gallery);
    }

    public void Restart()
    {
        startImmediately = true;
        Reload();
    }

    public void MainMenu()
    {
        startImmediately = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(Chapter1Scene);
    }

    static void Reload()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnApplicationFocus(bool focus)
    {
        if (focus) SetState(State); // re-apply cursor lock after alt-tab
    }
}
