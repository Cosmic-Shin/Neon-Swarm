using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for the Chamfered Holo UI kit (Assets/CyberpunkRTS/UI/Resources/HoloUI).
/// Sprites are authored at 2x: Pixels Per Unit 200 makes them show at 1x on a canvas with Reference PPU 100.
/// 9-slice borders come from the table below (pixels, Left/Bottom/Right/Top).
/// </summary>
public class HoloUISpriteImporter : AssetPostprocessor
{
    public const string Folder = "Assets/CyberpunkRTS/UI/Resources/HoloUI";
    public const float PPU = 200f;

    public static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
    {
        { "Bar_BG", new Vector4(4, 4, 4, 4) },
        { "Bar_Fill_White", new Vector4(2, 2, 2, 2) },
        { "Btn_Danger", new Vector4(41, 41, 41, 41) },
        { "Btn_Danger_Hover", new Vector4(41, 41, 41, 41) },
        { "Btn_Disabled", new Vector4(41, 41, 41, 41) },
        { "Btn_Gold", new Vector4(41, 41, 41, 41) },
        { "Btn_Gold_Hover", new Vector4(41, 41, 41, 41) },
        { "Btn_Hover", new Vector4(41, 41, 41, 41) },
        { "Btn_Normal", new Vector4(41, 41, 41, 41) },
        { "Btn_Pressed", new Vector4(41, 41, 41, 41) },
        { "Btn_Small", new Vector4(29, 29, 29, 29) },
        { "Btn_Small_Hover", new Vector4(29, 29, 29, 29) },
        { "Btn_Small_On", new Vector4(29, 29, 29, 29) },
        { "Card_Building", new Vector4(41, 41, 41, 41) },
        { "Card_Empty", new Vector4(41, 41, 41, 41) },
        { "Card_Hover", new Vector4(41, 41, 41, 41) },
        { "Card_Locked", new Vector4(41, 41, 41, 41) },
        { "Card_LockedStripes", new Vector4(0, 0, 0, 0) },
        { "Card_Normal", new Vector4(41, 41, 41, 41) },
        { "Card_ProgressFill", new Vector4(2, 2, 2, 8) },
        { "Card_Selected", new Vector4(43, 43, 43, 43) },
        { "Chip_Cyan", new Vector4(41, 41, 41, 41) },
        { "Chip_Green", new Vector4(41, 41, 41, 41) },
        { "Chip_Pink", new Vector4(41, 41, 41, 41) },
        { "Deco_CornerBracket", new Vector4(0, 0, 0, 0) },
        { "Deco_Divider", new Vector4(2, 0, 2, 0) },
        { "Deco_HeaderTick", new Vector4(0, 0, 0, 0) },
        { "Fill_White", new Vector4(32, 32, 32, 32) },
        { "Input_Field", new Vector4(8, 8, 8, 8) },
        { "Input_Field_Focus", new Vector4(8, 8, 8, 8) },
        { "Outline_White", new Vector4(41, 41, 41, 41) },
        { "Panel_BuildBar", new Vector4(65, 65, 65, 65) },
        { "Panel_Frame", new Vector4(61, 61, 61, 61) },
        { "Panel_Frame_Pink", new Vector4(61, 61, 61, 61) },
        { "Panel_Inset", new Vector4(29, 29, 29, 29) },
        { "Panel_Minimap", new Vector4(65, 65, 65, 65) },
        { "Panel_Modal", new Vector4(89, 89, 89, 89) },
        { "Slider_Handle", new Vector4(0, 0, 0, 0) },
        { "Slider_Track", new Vector4(2, 2, 2, 2) },
        { "Toast_Cyan", new Vector4(48, 47, 48, 47) },
        { "Toast_Green", new Vector4(48, 47, 48, 47) },
        { "Toast_Pink", new Vector4(48, 47, 48, 47) },
        { "Tooltip", new Vector4(45, 45, 45, 45) },
    };

    static readonly HashSet<string> Tiled = new HashSet<string> { "Card_LockedStripes" };

    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').StartsWith(Folder + "/")) return;
        var ti = (TextureImporter)assetImporter;
        string name = Path.GetFileNameWithoutExtension(assetPath);
        Apply(ti, name);
    }

    static void Apply(TextureImporter ti, string name)
    {
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = PPU;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.sRGBTexture = true;
        ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.wrapMode = Tiled.Contains(name) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteMeshType = SpriteMeshType.FullRect;   // required for Sliced / Tiled images
        s.spriteAlignment = (int)SpriteAlignment.Center;
        s.spriteBorder = Borders.TryGetValue(name, out var b) ? b : Vector4.zero;
        ti.SetTextureSettings(s);
    }

    [MenuItem("Tools/Cyberpunk RTS/Reimport Holo UI Sprites")]
    public static void ReimportAll()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) { Debug.LogWarning("[HoloUI] Folder not found: " + Folder); return; }
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        Debug.Log("[HoloUI] Reimported Holo UI sprites.");
    }

    // If the PNGs were imported before this script compiled, fix them once.
    [InitializeOnLoadMethod]
    static void CheckOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (!AssetDatabase.IsValidFolder(Folder)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
            {
                var ti = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if (ti != null && (ti.textureType != TextureImporterType.Sprite || !Mathf.Approximately(ti.spritePixelsPerUnit, PPU)))
                { ReimportAll(); return; }
            }
        };
    }
}
