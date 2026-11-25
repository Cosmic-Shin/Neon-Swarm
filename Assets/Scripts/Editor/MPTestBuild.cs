using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Development build used as the second client in multiplayer tests
/// (Builds/MPTest/GD5.exe -autojoin room -team N; see RoomService DEVELOPMENT_BUILD hooks).</summary>
public static class MPTestBuild
{
    [MenuItem("Tools/GD5/Build MP Test Client")]
    public static void Build()
    {
        var scenes = System.Array.ConvertAll(EditorBuildSettings.scenes, s => s.path);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/MPTest/GD5.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development,
        });
        Debug.Log($"[MPTest] {report.summary.result} in {report.summary.totalTime.TotalSeconds:0}s, {report.summary.totalErrors} errors");
    }
}
