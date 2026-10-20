using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// Batch-mode entry points: -executeMethod BuildScript.BuildWindows (rebuilds the scene first).
public static class BuildScript
{
    public const string ExePath = "Build/Windows/TheCameraNeverLies.exe";

    [MenuItem("Camera Never Lies/Build Windows Player")]
    public static void BuildWindows()
    {
        SceneBuilder.BuildAll();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = SceneBuilder.ScenePaths,
            locationPathName = ExePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        var s = report.summary;
        Debug.Log($"[BuildScript] {s.result}: {s.totalErrors} errors, {s.totalSize / (1024 * 1024)} MB, {s.totalTime}");
        if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }
}
