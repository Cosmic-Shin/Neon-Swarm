#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Runs Tools > Cyberpunk RTS > 1. Setup automatically after Unity compiles, whenever the kit version below
/// is newer than the last one set up in this project. Bump KitVersion when new models are exported.
/// Does not touch the open scene.
/// </summary>
[InitializeOnLoad]
static class CyberpunkRTSAutoSetup
{
    const string KitVersion = "2026-11-06-attack-clips";
    static readonly string StampPath = Path.Combine("Library", "CyberpunkRTS_KitVersion.txt");

    static CyberpunkRTSAutoSetup()
    {
        EditorApplication.delayCall += TryRun;
    }

    static void TryRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryRun;
            return;
        }
        string done = File.Exists(StampPath) ? File.ReadAllText(StampPath).Trim() : "";
        if (done == KitVersion) return;
        try
        {
            Debug.Log("[CyberpunkRTS] New kit content found, running setup automatically...");
            CyberpunkRTSSetup.SetupAll();
            File.WriteAllText(StampPath, KitVersion);
            Debug.Log("[CyberpunkRTS] Auto-setup finished. Demo scenes: Tools > Cyberpunk RTS > 2-7.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[CyberpunkRTS] Auto-setup failed: " + e);
        }
    }

    [MenuItem("Tools/Cyberpunk RTS/Re-run Auto Setup")]
    static void Force()
    {
        if (File.Exists(StampPath)) File.Delete(StampPath);
        TryRun();
    }
}
#endif
