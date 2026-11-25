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
            if (o.prefab.StartsWith("Aegis_")) go = AegisActor(go, o.prefab, o.name, o.scale, -o.blender_rotZ_deg);
            var extras = new List<GameObject>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Plaza_Hex") || t.name.StartsWith("Plaza_Center") || t.name.StartsWith("Plaza_Lamp") && !t.name.StartsWith("Plaza_LampTop")) extras.Add(t.gameObject);
                if (t.name.StartsWith("Plaza_LampTop")) { t.position = new Vector3(t.position.x, 0.02f, t.position.z); t.localScale *= 0.55f; }
            }
            foreach (var e in extras) Object.DestroyImmediate(e);
            bool tile = o.prefab.StartsWith("Tile_");
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name.Contains("_Upgrade_")) t.gameObject.SetActive(false);
            bool moves = go.GetComponentInChildren<Animator>() != null || o.prefab == "HiveAerie" || o.prefab == "CoreNexus";
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
        var aegisFront = Recompose(root, byName);

        var probe = new GameObject("WetReflections").AddComponent<ReflectionProbe>();
        probe.transform.SetParent(root, false);
        probe.transform.position = new Vector3(10f, 2f, 2f);
        probe.size = new Vector3(90f, 30f, 70f);
        probe.mode = ReflectionProbeMode.Baked;
        probe.resolution = 256;
        probe.boxProjection = false;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.07f, 0.08f, 0.16f);
        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.022f;
        RenderSettings.fogColor = Navy;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

        var cam = Camera.main;
        cam.transform.position = new Vector3(22f, 10f, -26f);   // v3: frames both bases right of the menu panel
        Vector3 look = U(layout.camera.look_at_blender);
        look = LookAt;
        cam.transform.rotation = Quaternion.LookRotation(look - cam.transform.position);
        cam.fieldOfView = 40f;
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
        d.aegisFront = aegisFront;
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

    // ---- v3 composition (GD5): the pack layout put the Aegis force inside the Hive base. Two bases, one standoff. ----
    // Unity coords. The Hive base sits around the origin, its army at z -7..-10 facing +x.
    static readonly (string name, string prefab, Vector3 pos, float yaw, float scale)[] AegisBase =
    {
        ("DA_B_CommandSpire",   "Aegis_CommandSpire",   new Vector3(19f, 0f, 4.5f),   -115f, 0.85f),
        ("DA_B_FusionReactor",  "Aegis_FusionReactor",  new Vector3(23.5f, 0f, -1.5f),-100f, 0.85f),
        ("DA_B_BarracksBay",    "Aegis_BarracksBay",    new Vector3(14.5f, 0f, 9f),   -150f, 0.85f),
        ("DA_B_ShieldProjector","Aegis_ShieldProjector",new Vector3(19.5f, 0f, -6f),  -100f, 0.85f),
        ("DA_B_Sentinel0",      "Aegis_SentinelTurret", new Vector3(13f, 0f, 2f),     -100f, 0.85f),
        ("DA_B_Sentinel1",      "Aegis_SentinelTurret", new Vector3(15.5f, 0f, -11.5f),-80f, 0.85f),
    };

    // where the existing DA_ units go: a front line at x 12..17 facing the swarm, heavies behind, air above the base
    static readonly (string name, Vector3 pos)[] AegisLine =
    {
        ("DA_Peacekeeper0", new Vector3(10.6f, 0f, -6.0f)), ("DA_Peacekeeper1", new Vector3(10.9f, 0f, -7.6f)), ("DA_Peacekeeper2", new Vector3(10.4f, 0f, -4.4f)),
        ("DA_Peacekeeper3", new Vector3(11.3f, 0f, -9.2f)), ("DA_Peacekeeper4", new Vector3(10.8f, 0f, -2.8f)),
        ("DA_Marksman",     new Vector3(12.4f, 0f, -1.8f)), ("DA_Lancer0",      new Vector3(12.4f, 0f, -6.8f)),
        ("DA_Paladin0",     new Vector3(12.8f, 0f, -4.4f)), ("DA_Paladin1",     new Vector3(13.2f, 0f, -9.6f)),
        ("DA_Bastion",      new Vector3(14.6f, 0f, -7.8f)), ("DA_Colossus",     new Vector3(17.5f, 0f, -0.5f)),
        ("DA_Gunship0",     new Vector3(14.5f, 2.6f, 1.5f)), ("DA_Interceptor0", new Vector3(17f, 4f, -9.5f)),
        ("DA_Dominion",     new Vector3(22f, 6f, 4f)),
    };

    public static readonly Vector3 LookAt = new(4f, 1f, -1f);

    static Transform[] Recompose(Transform root, Dictionary<string, Transform> byName)
    {
        // the swarm steps 3 m toward the Aegis line (it was hidden behind the menu panel), the line steps back 1.5 m
        var swarmShift = new Vector3(3f, 0f, 0f);
        foreach (var kv in byName)
            if (kv.Key.StartsWith("D_Skitter") || kv.Key is "D_Chromejaw" or "D_Strider" or "D_CrusherTarantula" or "D_QueenArachne") kv.Value.position += swarmShift;
        Vector3 hiveArmy = new Vector3(2f, 0f, -8.5f) + swarmShift;
        var front = new List<Transform>();
        foreach (var (name, linePos) in AegisLine)
        {
            if (Get(byName, name) is not Transform t) continue;
            var pos = linePos + new Vector3(1.5f, 0f, 0f);
            t.position = pos;
            Vector3 to = hiveArmy - new Vector3(pos.x, 0f, pos.z);
            t.rotation = Quaternion.LookRotation(to.normalized + new Vector3(0f, 0f, Random.Range(-0.12f, 0.12f)));
            if (pos.y < 0.5f && pos.x < 15.5f) front.Add(t);
        }
        foreach (var (name, prefab, pos, yaw, scale) in AegisBase)
        {
            var src = FindAsset(prefab) as GameObject;
            if (src == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, root);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f) * src.transform.localRotation);
            Strip(go);
            var holder = AegisActor(go, prefab, name, scale, yaw);
            foreach (var t in holder.GetComponentsInChildren<Transform>(true))
                if (t.name.Contains("_Upgrade_Mk") || t.name.EndsWith("_Damage") || t.name.Contains("_Damage_")) t.gameObject.SetActive(false);
            foreach (var r in holder.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.On;
            byName[name] = holder.transform;
        }
        // the crate stack sat right in front of the lens
        if (Get(byName, "D_Crates0") is Transform crates) crates.position = new Vector3(-7f, 0f, -13f);
        if (Get(byName, "D_RuinS2") is Transform ruin) ruin.position = new Vector3(-18f, 0f, 3f);   // stood inside the Aegis line

        // warm corporate key light over the Aegis base, cool rim from the swarm side
        void Lamp(string n, Vector3 at, Color c, float range, float intensity)
        {
            var l = new GameObject(n).AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.transform.position = at;
            l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity;
            l.renderMode = LightRenderMode.ForcePixel;
        }
        Lamp("DA_L_Key", new Vector3(18f, 7f, -1f), new Color(1f, 0.62f, 0.32f), 22f, 2.4f);
        Lamp("DA_L_Front", new Vector3(12f, 3f, -8f), new Color(1f, 0.5f, 0.28f), 10f, 1.8f);
        Lamp("DA_L_Spire", new Vector3(19f, 8f, 5f), new Color(1f, 0.72f, 0.45f), 12f, 1.6f);
        Lamp("D_L_SwarmRim", new Vector3(5f, 2.5f, -11f), new Color(0.3f, 0.9f, 1f), 10f, 1.4f);
        return front.ToArray();
    }

    static Transform Get(Dictionary<string, Transform> d, string n) => d.TryGetValue(n, out var t) ? t : null;

    static Transform Child(Transform root, string n)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
        return null;
    }

    static Object FindAsset(string name)
    {
        if (name.StartsWith("Aegis_"))
            foreach (var folder in new[] { "Units", "Buildings" })
                if (AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CyberpunkRTS/Models/Aegis/{folder}/{name}.fbx") is GameObject fbx) return fbx;
        foreach (var folder in new[] { "Buildings", "Units", "Environment" })
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CyberpunkRTS/Prefabs/{folder}/{name}.prefab") is GameObject p) return p;
        return AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CyberpunkRTS/Models/Environment/{name}.fbx");
    }

    static GameObject AegisActor(GameObject model, string prefab, string name, float scale, float yaw)
    {
        var holder = new GameObject(name);
        holder.transform.SetParent(model.transform.parent, false);
        holder.transform.SetPositionAndRotation(model.transform.position, Quaternion.Euler(0f, yaw, 0f));
        holder.transform.localScale = Vector3.one * scale;
        var rot = model.transform.rotation;
        model.transform.SetParent(holder.transform, true);
        model.transform.rotation = rot;
        model.transform.localScale = Vector3.one;
        model.name = prefab;
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"Assets/Animations/Aegis/{prefab}.overrideController")
            ?? AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"Assets/Animations/Aegis/{prefab}.controller");
        if (ctrl != null)
        {
            var anim = model.TryGetComponent(out Animator a) ? a : model.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
        }
        return holder;
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
            if (l.name.StartsWith("DA_L_")) light.intensity = Mathf.Min(light.intensity, 1.6f);
            light.renderMode = area || l.name is "D_L_Nexus" or "D_L_City1" or "D_L_City2" ? LightRenderMode.ForcePixel : LightRenderMode.Auto;
            if (!shadowUsed && l.name == "D_L_Nexus") { light.shadows = LightShadows.Soft; light.shadowStrength = 0.7f; shadowUsed = true; }
        }
    }
}
