#if UNITY_EDITOR && UNITY_POST_PROCESSING_STACK_V2
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

/// <summary>Adds the neon bloom look (Post Processing v2) to the open scene's main camera.</summary>
public static class CyberpunkRTSPostFX
{
    const string ProfilePath = "Assets/CyberpunkRTS/PostFX/NeonBloom.asset";

    [MenuItem("Tools/Cyberpunk RTS/3. Add Neon Bloom to Open Scene")]
    public static void AddBloom()
    {
        var profile = GetOrCreateProfile();
        var cam = Camera.main;
        if (!cam) cam = Object.FindFirstObjectByType<Camera>();
        if (!cam) { Debug.LogWarning("[CyberpunkRTS] No camera in the open scene."); return; }
        cam.allowHDR = true;

        var layer = cam.GetComponent<PostProcessLayer>();
        if (!layer) layer = cam.gameObject.AddComponent<PostProcessLayer>();
        var res = AssetDatabase.LoadAssetAtPath<PostProcessResources>("Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset");
        if (!res)
        {
            var g = AssetDatabase.FindAssets("t:PostProcessResources");
            if (g.Length > 0) res = AssetDatabase.LoadAssetAtPath<PostProcessResources>(AssetDatabase.GUIDToAssetPath(g[0]));
        }
        if (res) layer.Init(res);
        layer.volumeTrigger = cam.transform;
        layer.volumeLayer = 1 << 0;   // Default layer
        layer.antialiasingMode = PostProcessLayer.Antialiasing.FastApproximateAntialiasing;

        var vol = Object.FindFirstObjectByType<PostProcessVolume>();
        if (!vol)
        {
            var go = new GameObject("NeonBloom_Volume");
            go.layer = 0;
            vol = go.AddComponent<PostProcessVolume>();
        }
        vol.isGlobal = true;
        vol.priority = 1f;
        vol.sharedProfile = profile;

        EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
        Debug.Log("[CyberpunkRTS] Neon bloom added. Tweak it at " + ProfilePath);
    }

    static PostProcessProfile GetOrCreateProfile()
    {
        var p = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(ProfilePath);
        if (p) return p;
        if (!AssetDatabase.IsValidFolder("Assets/CyberpunkRTS/PostFX")) AssetDatabase.CreateFolder("Assets/CyberpunkRTS", "PostFX");
        p = ScriptableObject.CreateInstance<PostProcessProfile>();
        AssetDatabase.CreateAsset(p, ProfilePath);

        var bloom = p.AddSettings<Bloom>();
        bloom.enabled.Override(true);
        bloom.intensity.Override(4f);
        bloom.threshold.Override(1.0f);
        bloom.softKnee.Override(0.6f);
        bloom.diffusion.Override(7f);
        Store(bloom, p);

        var cg = p.AddSettings<ColorGrading>();
        cg.enabled.Override(true);
        cg.tonemapper.Override(Tonemapper.ACES);
        cg.postExposure.Override(0.35f);
        cg.saturation.Override(12f);
        Store(cg, p);

        var vig = p.AddSettings<Vignette>();
        vig.enabled.Override(true);
        vig.intensity.Override(0.3f);
        vig.smoothness.Override(0.4f);
        Store(vig, p);

        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
        return p;
    }

    static void Store(PostProcessEffectSettings s, PostProcessProfile p)
    {
        s.name = s.GetType().Name;
        s.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(s, p);
    }
}
#endif
