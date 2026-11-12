using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Sets the Neon Swarm "Hive Emblem" as the game icon (Player Settings: default icon + every Standalone size).
/// Runs once automatically after import; re-run from Tools > Cyberpunk RTS > Apply Game Icon.
/// Icons live in Assets/CyberpunkRTS/UI/Icon (NeonSwarm_Icon_<size>.png). The .ico and SVGs for
/// itch.io / shortcuts are in Assets/CyberpunkRTS/UI/Icon~ (ignored by Unity).
/// </summary>
public class GameIconSetup : AssetPostprocessor
{
    const string Folder = "Assets/CyberpunkRTS/UI/Icon";
    const string Stamp = "Library/CyberpunkRTS_GameIcon.txt";
    const string Version = "hive-emblem-2-hex";
    static readonly int[] Available = { 1024, 512, 256, 128, 64, 48, 32, 24, 16 };

    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').StartsWith(Folder + "/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Default;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.filterMode = FilterMode.Bilinear;
        ti.wrapMode = TextureWrapMode.Clamp;
    }

    [InitializeOnLoadMethod]
    static void AutoApply()
    {
        EditorApplication.delayCall += () =>
        {
            if (File.Exists(Stamp) && File.ReadAllText(Stamp) == Version) return;
            if (Load(1024) == null) return; // not imported yet; next reload will try again
            Apply();
            File.WriteAllText(Stamp, Version);
        };
    }

    static Texture2D Load(int size) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/NeonSwarm_Icon_{size}.png");

    static Texture2D Closest(int size)
    {
        int best = Available[0];
        foreach (var s in Available) if (s >= size && s < best) best = s;   // smallest one that is >= size
        if (size > Available[0]) best = Available[0];
        return Load(best) ?? Load(1024);
    }

    [MenuItem("Tools/Cyberpunk RTS/Apply Game Icon")]
    public static void Apply()
    {
        var big = Load(1024);
        if (!big) { Debug.LogWarning("[GameIcon] Missing " + Folder + "/NeonSwarm_Icon_1024.png"); return; }

        // Default icon (used by every platform without its own override).
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { big }, IconKind.Any);

        // Standalone: hand-made small sizes stay crisp in the taskbar / title bar.
        var sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any);
        var tex = new Texture2D[sizes.Length];
        for (int i = 0; i < sizes.Length; i++) tex[i] = Closest(sizes[i]);
        PlayerSettings.SetIcons(NamedBuildTarget.Standalone, tex, IconKind.Any);

        AssetDatabase.SaveAssets();
        Debug.Log("[GameIcon] Neon Swarm icon applied (" + string.Join(", ", sizes) + ").");
    }
}
