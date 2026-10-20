using UnityEngine;

/// Layer conventions used by the dual-reality camera.
public static class Layers
{
    public const int CameraOnly = 8;   // visible only through the viewfinder
    public const int NormalOnly = 9;   // visible only to the naked eye
    public const int PlayerBody = 10;  // only ever seen in "impossible" photographs

    public static int CameraOnlyMask => 1 << CameraOnly;
    public static int NormalOnlyMask => 1 << NormalOnly;
    public static int PlayerBodyMask => 1 << PlayerBody;

    public static int NormalViewMask => ~(CameraOnlyMask | PlayerBodyMask);
    public static int ViewfinderMask => ~(NormalOnlyMask | PlayerBodyMask);

    public static void SetRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform) SetRecursive(t.gameObject, layer);
    }
}

/// Player-facing options, persisted with PlayerPrefs.
public static class Settings
{
    public static float Sensitivity = 2f;
    public static float Volume = 0.8f;
    public static float Brightness = 1f;
    public static bool Subtitles = true;
    public static bool ReducedEffects = false;

    public static void Load()
    {
        Sensitivity = PlayerPrefs.GetFloat("cnl_sens", 2f);
        Volume = PlayerPrefs.GetFloat("cnl_vol", 0.8f);
        Brightness = PlayerPrefs.GetFloat("cnl_bright", 1f);
        Subtitles = PlayerPrefs.GetInt("cnl_subs", 1) == 1;
        ReducedEffects = PlayerPrefs.GetInt("cnl_reduced", 0) == 1;
        Apply();
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat("cnl_sens", Sensitivity);
        PlayerPrefs.SetFloat("cnl_vol", Volume);
        PlayerPrefs.SetFloat("cnl_bright", Brightness);
        PlayerPrefs.SetInt("cnl_subs", Subtitles ? 1 : 0);
        PlayerPrefs.SetInt("cnl_reduced", ReducedEffects ? 1 : 0);
        PlayerPrefs.Save();
        Apply();
    }

    public static void Apply()
    {
        AudioListener.volume = Volume;
    }
}
