using UnityEditor;
using UnityEngine;

/// <summary>Import settings for Assets/CyberpunkRTS/UI/Resources/HoloCursors (Unity "Cursor" texture type).</summary>
public class HoloCursorImporter : AssetPostprocessor
{
    public const string Folder = "Assets/CyberpunkRTS/UI/Resources/HoloCursors";

    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').StartsWith(Folder + "/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Cursor;   // readable, RGBA32, no mips
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.isReadable = true;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.filterMode = FilterMode.Bilinear;
        ti.wrapMode = TextureWrapMode.Clamp;
    }

    [MenuItem("Tools/Cyberpunk RTS/Reimport Cursors")]
    public static void ReimportAll()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) return;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        Debug.Log("[HoloCursor] Reimported cursors.");
    }

    [InitializeOnLoadMethod]
    static void CheckOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (!AssetDatabase.IsValidFolder(Folder)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
            {
                var ti = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if (ti != null && ti.textureType != TextureImporterType.Cursor) { ReimportAll(); return; }
            }
        };
    }
}
