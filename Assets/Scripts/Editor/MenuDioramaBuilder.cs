using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class MenuDioramaBuilder
{
    const string LayoutPath = "Assets/CyberpunkRTS/UI/MenuDiorama~/MenuDiorama_layout.json";
    const string ScenePath = "Assets/Scenes/Login.unity";
    const string MatFolder = "Assets/Materials/Diorama";
    static readonly Color Navy = new(0.035f, 0.025f, 0.075f);

    [System.Serializable] class Layout { public Cam camera; public Obj[] objects; public Lamp[] lights; }
    [System.Serializable] class Cam { public float[] blender_pos, look_at_blender; public float unity_vertical_fov; }
    [System.Serializable] class Obj { public string prefab, name; public float[] blender_pos; public float blender_rotZ_deg, scale; }
    [System.Serializable] class Lamp { public string name, type; public float[] blender_pos, color; public float blender_watts, radius; }

    static Vector3 U(float[] b) => new(b[0], b[2], b[1]);

    [MenuItem("Tools/Cyberpunk RTS/Build Menu Diorama")]
    public static void Build()
    {
        var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (var go in scene.GetRootGameObjects())
            if (go.name is "MenuDiorama" or "NeonBloom_Volume") Object.DestroyImmediate(go);

        var root = new GameObject("MenuDiorama").transform;
        var wet = WetMaterials();
        var byName = new Dictionary<string, Transform>();
        var skitters = new List<Transform>();
        var ores = new List<Transform>();

        foreach (var o in layout.objects)
        {
            var source = FindAsset(o.prefab);
            if (source == null) { Debug.LogWarning("[Diorama] missing " + o.prefab); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(source, root);
            if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = o.name;
            var src = ((GameObject)source).transform;
            go.transform.SetPositionAndRotation(U(o.blender_pos), Quaternion.Euler(0f, -o.blender_rotZ_deg, 0f) * src.localRotation);
            go.transform.localScale = src.localScale * o.scale;
            Strip(go);
            var extras = new List<GameObject>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Plaza_Hex") || t.name.StartsWith("Plaza_Center") || t.name.StartsWith("Plaza_Lamp") && !t.name.StartsWith("Plaza_LampTop")) extras.Add(t.gameObject);
                if (t.name.StartsWith("Plaza_LampTop")) { t.position = new Vector3(t.position.x, 0.02f, t.position.z); t.localScale *= 0.55f; }
            }
            foreach (var e in extras) Object.DestroyImmediate(e);
            bool tile = o.prefab.StartsWith("Tile_");
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name.Contains("_Upgrade_")) t.gameObject.SetActive(false);
            bool moves = go.GetComponent<Animator>() != null || o.prefab == "HiveAerie" || o.prefab == "CoreNexus";
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                if (tile) for (int i = 0; i < mats.Length; i++) if (mats[i] != null && wet.TryGetValue(mats[i].name, out var w)) mats[i] = w;
                r.sharedMaterials = mats;
                r.shadowCastingMode = tile ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }
            if (!moves) foreach (var t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, (StaticEditorFlags)~0);
            byName[o.name] = go.transform;
            if (o.prefab == "Skitter") skitters.Add(go.transform);
            if (o.prefab == "Ore_DataCrystal") ores.Add(go.transform);
        }

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        Object.DestroyImmediate(ground.GetComponent<Collider>());
        ground.name = "WetGround";
        ground.transform.SetParent(root, false);
        ground.transform.position = new Vector3(-2f, -0.04f, 6f);
        ground.transform.localScale = new Vector3(14f, 1f, 14f);
        ground.GetComponent<Renderer>().sharedMaterial = wet["__ground"];
        ground.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(ground, (StaticEditorFlags)~0);

        var aerie = Get(byName, "D_HiveAerie");
        if (aerie != null) aerie.gameObject.AddComponent<AerieFX>();
        var flyerSrc = FindAsset("FireflySpotter");
        Transform flyer = null;
        if (flyerSrc != null)
        {
            var f = (GameObject)PrefabUtility.InstantiatePrefab(flyerSrc, root);
            PrefabUtility.UnpackPrefabInstance(f, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            f.name = "D_Flyer";
            f.transform.localScale = Vector3.one * 1.6f;
            Strip(f);
            flyer = f.transform;
        }

        Transform Actor(string prefab, float scale)
        {
            var src = FindAsset(prefab);
            if (src == null) return null;
            var a = (GameObject)PrefabUtility.InstantiatePrefab(src, root);
            PrefabUtility.UnpackPrefabInstance(a, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            a.name = "D_Extra_" + prefab;
            a.transform.localScale = Vector3.one * scale;
            Strip(a);
            return a.transform;
        }
        var traffic = new List<Transform>();
        foreach (var (n, s) in new[] { ("HornetInterceptor", 5f), ("DragonflyGunship", 3.5f), ("WaspTransport", 4f), ("MantaLeviathan", 2f), ("HornetInterceptor", 5f), ("DragonflyGunship", 3.5f), ("LocustDrone", 9f) })
            if (Actor(n, s) is Transform t) traffic.Add(t);
        var patrol = new List<Transform>();
        for (int i = 0; i < 2; i++) if (Actor("Strider", 1.3f) is Transform t) patrol.Add(t);

        Lights(layout.lights, root);

        var probe = new GameObject("WetReflections").AddComponent<ReflectionProbe>();
        probe.transform.SetParent(root, false);
        probe.transform.position = new Vector3(-2f, 2f, 4f);
        probe.size = new Vector3(70f, 30f, 70f);
        probe.mode = ReflectionProbeMode.Baked;
        probe.resolution = 256;
        probe.boxProjection = false;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.07f, 0.08f, 0.16f);
        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.03f;
        RenderSettings.fogColor = Navy;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

        var cam = Camera.main;
        cam.transform.position = U(layout.camera.blender_pos);
        Vector3 look = U(layout.camera.look_at_blender);
        cam.transform.rotation = Quaternion.LookRotation(look - cam.transform.position);
        cam.fieldOfView = layout.camera.unity_vertical_fov;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 150f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Navy;
        cam.allowHDR = true;
        cam.renderingPath = RenderingPath.DeferredShading;
        CyberpunkRTSPostFX.AddBloom();

        var d = root.gameObject.AddComponent<MenuDiorama>();
        d.cam = cam;
        d.lookAt = look;
        d.harvester = Get(byName, "D_DataHarvester");
        var refinery = Get(byName, "D_DataRefinery");
        d.refineryDock = refinery != null ? Child(refinery, "Refinery_DockPoint") : null;
        d.queen = Get(byName, "D_QueenArachne");
        d.aerie = aerie;
        d.flyer = flyer;
        d.ores = ores.ToArray();
        d.skitters = skitters.GetRange(0, Mathf.Min(3, skitters.Count)).ToArray();
        Vector3 army = Vector3.zero;
        foreach (var s in skitters) army += s.position;
        d.armyCentre = skitters.Count > 0 ? army / skitters.Count : Vector3.zero;
        var nexus = Get(byName, "D_CoreNexus");
        var spin = new List<Transform>();
        if (nexus != null) foreach (var n in new[] { "Nexus_CoreOrbitA", "Nexus_CoreOrbitB", "Nexus_CoreOrbitC", "Nexus_EnergyCore" }) if (Child(nexus, n) is Transform c) spin.Add(c);
        d.spinners = spin.ToArray();
        d.traffic = traffic.ToArray();
        d.patrol = patrol.ToArray();
        var ruins = new List<Transform>();
        foreach (var n in new[] { "D_RuinT1", "D_RuinT2", "D_RuinT3", "D_RuinT0" }) if (Get(byName, n) is Transform t) ruins.Add(t);
        d.adTowers = ruins.ToArray();
        d.searchlightSpots = new[] { new Vector3(-20f, 0f, 40f), new Vector3(-6f, 0f, 44f), new Vector3(8f, 0f, 42f), new Vector3(22f, 0f, 36f) };

        if (GameObject.Find("Canvas")?.transform.Find("MenuBackground") is Transform bg && bg.TryGetComponent(out UnityEngine.UI.RawImage bgImage))
        {
            bgImage.material = null;
            bgImage.texture = null;
            bgImage.color = Color.clear;
            EditorUtility.SetDirty(bgImage);
        }
        if (GameObject.Find("Canvas") is GameObject canvas)
        {
            if (!canvas.TryGetComponent(out GlitchSplash splash)) splash = canvas.AddComponent<GlitchSplash>();
            splash.image = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/CyberpunkRTS/UI/Splash/CosmicShin_Lockup.png");
            EditorUtility.SetDirty(splash);
            foreach (var n in new[] { "SignInPanel", "LobbyPanel" })
                if (canvas.transform.Find(n) is RectTransform rt) { rt.anchoredPosition = new Vector2(-600f, -60f); EditorUtility.SetDirty(rt); }
            if (canvas.transform.Find("Title") is RectTransform title)
            {
                if (title.TryGetComponent(out UnityEngine.UI.Outline outline)) Object.DestroyImmediate(outline);
                if (title.TryGetComponent(out UnityEngine.UI.Text text)) Object.DestroyImmediate(text);
                if (!title.TryGetComponent(out GlitchTitle glitch)) glitch = title.gameObject.AddComponent<GlitchTitle>();
                glitch.scale = 0.9f;
                title.anchorMin = title.anchorMax = title.pivot = new Vector2(0f, 1f);
                title.anchoredPosition = new Vector2(110f, -36f);
                title.sizeDelta = new Vector2(474f, 196f) * glitch.scale;
                EditorUtility.SetDirty(title);
            }
        }

        if (!AssetDatabase.IsValidFolder("Assets/Scenes/Login")) AssetDatabase.CreateFolder("Assets/Scenes", "Login");
        Lightmapping.BakeReflectionProbe(probe, "Assets/Scenes/Login/MenuDioramaReflection.exr");
        RenderSettings.customReflectionTexture = probe.bakedTexture;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Diorama] built {layout.objects.Length} objects, {layout.lights.Length} lights");
    }

    static Transform Get(Dictionary<string, Transform> d, string n) => d.TryGetValue(n, out var t) ? t : null;

    static Transform Child(Transform root, string n)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
        return null;
    }

    static Object FindAsset(string name)
    {
        foreach (var folder in new[] { "Buildings", "Units", "Environment" })
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CyberpunkRTS/Prefabs/{folder}/{name}.prefab") is GameObject p) return p;
        return AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CyberpunkRTS/Models/Environment/{name}.fbx");
    }

    static void Strip(GameObject go)
    {
        for (int pass = 0; pass < 3; pass++)
            foreach (var c in go.GetComponentsInChildren<Component>(true))
                if (c != null && !(c is Transform || c is MeshFilter || c is Renderer || c is Animator || c is LODGroup || c is ParticleSystem || c is Light))
                    Object.DestroyImmediate(c);
    }

    static Dictionary<string, Material> WetMaterials()
    {
        if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Diorama");
        var result = new Dictionary<string, Material>();
        Material Make(string file, Material from, Color c, float smooth, float metal)
        {
            string path = $"{MatFolder}/{file}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = from != null ? new Material(from) : new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
            m.color = c;
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", metal);
            EditorUtility.SetDirty(m);
            return m;
        }
        foreach (var name in new[] { "Env_Concrete", "Env_Asphalt" })
        {
            var g = AssetDatabase.FindAssets(name + " t:Material");
            var src = g.Length > 0 ? AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g[0])) : null;
            result[name] = Make(name + "_Wet", src, name == "Env_Concrete" ? new Color(0.06f, 0.065f, 0.08f) : new Color(0.02f, 0.02f, 0.03f), 0.9f, 0.2f);
        }
        result["__ground"] = Make("WetGround", null, new Color(0.012f, 0.013f, 0.022f), 0.94f, 0.35f);
        return result;
    }

    static void Lights(Lamp[] lamps, Transform root)
    {
        bool shadowUsed = false;
        foreach (var l in lamps)
        {
            var light = new GameObject(l.name).AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.position = U(l.blender_pos);
            light.color = new Color(l.color[0], l.color[1], l.color[2]);
            light.shadows = LightShadows.None;
            if (l.type == "SUN")
            {
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(38f, 95f, 0f);
                light.intensity = 0.22f;
                continue;
            }
            light.type = LightType.Point;
            bool area = l.type == "AREA";
            light.range = area ? 45f : Mathf.Clamp(6f + Mathf.Sqrt(l.blender_watts) * 0.5f, 8f, 34f);
            light.intensity = area ? Mathf.Clamp(l.blender_watts / 2500f, 0.6f, 1.8f) : l.name.StartsWith("D_L_City") ? 2.2f : Mathf.Clamp(l.blender_watts / 180f, 1f, 6f);
            light.renderMode = area || l.name is "D_L_Nexus" or "D_L_City1" or "D_L_City2" ? LightRenderMode.ForcePixel : LightRenderMode.Auto;
            if (!shadowUsed && l.name == "D_L_Nexus") { light.shadows = LightShadows.Soft; light.shadowStrength = 0.7f; shadowUsed = true; }
        }
    }
}
