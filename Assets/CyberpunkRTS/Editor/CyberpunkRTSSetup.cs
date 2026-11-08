#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>One-click setup for the Cyberpunk RTS kit exported from Blender. Menu: Tools > Cyberpunk RTS.</summary>
public static class CyberpunkRTSSetup
{
    const string Root = "Assets/CyberpunkRTS";
    const string Models = Root + "/Models";
    const string MatDir = Root + "/Materials";
    const string PrefabDir = Root + "/Prefabs";
    const string AnimDir = Root + "/Animation";
    static readonly HashSet<string> Animated = new HashSet<string> { "BulwarkTick", "Chromejaw", "CrusherTarantula", "QueenArachne", "Overclock", "NetrunnerHive" };

    struct M
    {
        public string n; public Color c; public float metal, smooth; public Color e; public float alpha;
        public M(string n, float r, float g, float b, float metal, float rough, float er, float eg, float eb, float es, float a)
        {
            this.n = n; c = new Color(r, g, b, a); this.metal = metal; smooth = Mathf.Clamp01(1f - rough);
            float k = Mathf.Min(es * 0.35f, 3f); e = new Color(er * k, eg * k, eb * k); alpha = a;
        }
    }

    static readonly M[] Mats =
    {
        new M("Dead_Ember", 0.4000f, 0.0600f, 0.0100f, 0.300f, 0.850f, 1.0000f, 0.1800f, 0.0300f, 1.800f, 1.000f),
        new M("Env_Asphalt", 0.0300f, 0.0300f, 0.0360f, 0.000f, 0.850f, 1.0000f, 1.0000f, 1.0000f, 0.000f, 1.000f),
        new M("Env_Concrete", 0.3200f, 0.3300f, 0.3700f, 0.000f, 0.900f, 1.0000f, 1.0000f, 1.0000f, 0.000f, 1.000f),
        new M("Env_NeonLime", 0.5500f, 1.0000f, 0.1000f, 0.000f, 0.500f, 0.5500f, 1.0000f, 0.1000f, 6.000f, 1.000f),
        new M("Env_NeonOrange", 1.0000f, 0.3500f, 0.0200f, 0.000f, 0.800f, 1.0000f, 0.3500f, 0.0200f, 6.000f, 1.000f),
        new M("Env_Rust", 0.3200f, 0.1400f, 0.0600f, 0.600f, 0.700f, 1.0000f, 1.0000f, 1.0000f, 0.000f, 1.000f),
        new M("FX_Scorch", 0.0120f, 0.0090f, 0.0100f, 0.000f, 1.000f, 1.0000f, 1.0000f, 1.0000f, 0.000f, 0.850f),
        new M("Neutral_Glow", 0.8500f, 0.9200f, 1.0000f, 0.000f, 0.800f, 0.8500f, 0.9200f, 1.0000f, 6.000f, 1.000f),
        new M("Neutral_Gray", 0.4200f, 0.4400f, 0.4800f, 0.300f, 0.500f, 1.0000f, 1.0000f, 1.0000f, 0.000f, 1.000f),
        new M("Shield_Hologram", 0.0000f, 0.9000f, 1.0000f, 0.000f, 0.500f, 0.0000f, 0.9000f, 1.0000f, 1.500f, 0.220f),
        new M("Skitter_Graphite", 0.0200f, 0.0200f, 0.0250f, 0.700f, 0.300f, 1.0000f, 1.0000f, 1.0000f, 0.000f, 1.000f),
        new M("Skitter_NeonCyan", 0.0000f, 1.0000f, 0.9000f, 0.000f, 0.500f, 0.0000f, 1.0000f, 0.9000f, 10.000f, 1.000f),
        new M("Skitter_NeonPink", 1.0000f, 0.0500f, 0.6000f, 0.000f, 0.500f, 1.0000f, 0.0500f, 0.6000f, 8.000f, 1.000f),
        new M("Skitter_NeonYellow", 1.0000f, 0.8500f, 0.0000f, 0.000f, 0.500f, 1.0000f, 0.8500f, 0.0000f, 6.000f, 1.000f),
        new M("Skitter_White", 0.9200f, 0.9200f, 0.9400f, 0.100f, 0.250f, 1.0000f, 1.0000f, 1.0000f, 0.000f, 1.000f),
        new M("Wreck_Charred", 0.0500f, 0.0450f, 0.0450f, 0.400f, 0.800f, 1.0000f, 1.0000f, 1.0000f, 0.000f, 1.000f),
    };

    // Attack clips authored in Blender: unit -> (last frame, fire frame). Clip starts at frame 501.
    static readonly Dictionary<string, (int end, int fire)> AttackClips = new Dictionary<string, (int end, int fire)> {
        { "Chromejaw", (518, 510) }, { "Burner", (516, 505) }, { "MortarScarab", (516, 503) }, { "SniperMantis", (514, 503) },
        { "QueenArachne", (519, 510) }, { "JellyfishDreadnought", (519, 509) }, { "MothMothership", (519, 511) },
        { "MantaLeviathan", (518, 506) }, { "BeetleBomber", (519, 510) }, { "DragonflyGunship", (513, 505) } };

    [MenuItem("Tools/Cyberpunk RTS/1. Setup Materials, Imports and Prefabs")]
    public static void SetupAll()
    {
        var mats = CreateMaterials();
        ConfigureImporters(mats);
        SetupIcons();
        BuildPrefabs();
        BuildEnvironmentPrefabs();
        BuildConstructionResources();
        BuildAirResources();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CyberpunkRTS] Setup complete: materials, import settings, death animations, prefabs.");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static Dictionary<string, Material> CreateMaterials()
    {
        EnsureFolder(MatDir);
        var dict = new Dictionary<string, Material>();
        var shader = Shader.Find("Standard");
        foreach (var m in Mats)
        {
            string path = $"{MatDir}/{m.n}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew) mat = new Material(shader);
            mat.shader = shader;
            mat.SetColor("_Color", m.c);
            mat.SetFloat("_Metallic", m.metal);
            mat.SetFloat("_Glossiness", m.smooth);
            if (m.e.maxColorComponent > 0.001f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", m.e);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }
            if (m.alpha < 0.99f)
            {
                mat.SetFloat("_Mode", 3f);
                mat.SetInt("_SrcBlend", (int)BlendMode.One);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }
            if (isNew) AssetDatabase.CreateAsset(mat, path); else EditorUtility.SetDirty(mat);
            dict[m.n] = mat;
        }
        AssetDatabase.SaveAssets();
        return dict;
    }

    static void ConfigureImporters(Dictionary<string, Material> mats)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Models }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is ModelImporter imp)) continue;
            string name = Path.GetFileNameWithoutExtension(path);
            imp.globalScale = 1f;
            imp.useFileScale = true;
            imp.bakeAxisConversion = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            foreach (var kv in mats)
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            bool isUnit = path.Contains("/Models/Units/");
            if (isUnit)
            {
                imp.importAnimation = true;
                imp.animationType = ModelImporterAnimationType.Generic;
                var takes = imp.defaultClipAnimations;
                if (takes != null && takes.Length > 0)
                {
                    var take = takes[0];
                    float off = take.firstFrame - 1f;   // Blender frame 1 == first frame of the take
                    var clips = new List<ModelImporterClipAnimation>();
                    ModelImporterClipAnimation Make(string clipName, float a, float b, bool loop)
                    {
                        return new ModelImporterClipAnimation { name = clipName, takeName = take.takeName,
                            firstFrame = a + off, lastFrame = b + off, loopTime = loop, loopPose = false };
                    }
                    clips.Add(Make("Walk", 101, 125, true));
                    clips.Add(Make("Idle", 201, 249, true));
                    if (Animated.Contains(name)) clips.Add(Make("Death", 1, 40, false));
                    if (name == "DataHarvester") { clips.Add(Make("Harvest", 301, 349, true)); clips.Add(Make("Unload", 401, 449, true)); }
                    if (AttackClips.TryGetValue(name, out var atk))
                    {
                        var ac = Make("Attack", 501, atk.end, false);
                        ac.events = new[] { new AnimationEvent { functionName = "Fire", time = (atk.fire - 501f) / (atk.end - 501f) } };
                        clips.Add(ac);
                    }
                    imp.clipAnimations = clips.ToArray();
                }
            }
            else
            {
                imp.importAnimation = false;
                imp.animationType = ModelImporterAnimationType.None;
            }
            imp.SaveAndReimport();
        }
    }

    static void SetupIcons()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Icons" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single; // GD5: else new icons can import as "Multiple" with no sprites
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }
    }

    static AnimatorController CreateController(string name, string modelPath)
    {
        EnsureFolder(AnimDir);
        var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
        AnimationClip Get(string n) => clips.FirstOrDefault(c => c.name == n);
        string ctrlPath = $"{AnimDir}/{name}.controller";
        AssetDatabase.DeleteAsset(ctrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.motion = Get("Idle");
        var walk = sm.AddState("Walk"); walk.motion = Get("Walk");
        sm.defaultState = idle;
        var toWalk = idle.AddTransition(walk); toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"); toWalk.hasExitTime = false; toWalk.duration = 0.15f;
        var toIdle = walk.AddTransition(idle); toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"); toIdle.hasExitTime = false; toIdle.duration = 0.2f;
        var deathClip = Get("Death");
        if (deathClip)
        {
            var death = sm.AddState("Death"); death.motion = deathClip;
            var t = sm.AddAnyStateTransition(death);
            t.AddCondition(AnimatorConditionMode.If, 0, "Die");
            t.hasExitTime = false; t.duration = 0.05f; t.canTransitionToSelf = false;
        }
        var atkClip = Get("Attack");
        if (atkClip)
        {
            ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            var atkState = sm.AddState("Attack"); atkState.motion = atkClip;
            var toAtk = sm.AddAnyStateTransition(atkState);
            toAtk.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            toAtk.hasExitTime = false; toAtk.duration = 0.05f; toAtk.canTransitionToSelf = true;
            var back = atkState.AddTransition(idle); back.hasExitTime = true; back.exitTime = 1f; back.duration = 0.1f;
        }
        foreach (var extra in new[] { "Harvest", "Unload" })
        {
            var clip = Get(extra);
            if (clip) sm.AddState(extra).motion = clip;
        }
        return ctrl;
    }

    static void AddSelectionCollider(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        var bc = go.AddComponent<BoxCollider>();
        bc.center = go.transform.InverseTransformPoint(b.center);
        bc.size = b.size;
    }

    static void BuildPrefabs()
    {
        foreach (var folder in new[] { "Units", "Buildings" })
        {
            EnsureFolder($"{PrefabDir}/{folder}");
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { $"{Models}/{folder}" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!model) continue;
                var modelGo = (GameObject)PrefabUtility.InstantiatePrefab(model);
                GameObject go = modelGo;
                if (folder == "Units")
                {
                    // Walk/Idle key the FBX root's transform, so the Animator must not sit on the prefab root
                    // (it would pin every unit to the origin). Root = empty mover, model = animated child.
                    go = new GameObject();
                    modelGo.name = "Model";
                    modelGo.transform.SetParent(go.transform, false);
                }
                go.name = name;
                AddSelectionCollider(go);
                var d = go.AddComponent<UnitDeath>();
                var wreck = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/Wrecks/Wreck_{name}.fbx");
                Animator anim = null;
                if (folder == "Units")
                {
                    anim = modelGo.GetComponent<Animator>();
                    if (!anim) anim = modelGo.AddComponent<Animator>();
                    anim.applyRootMotion = false;
                    anim.runtimeAnimatorController = CreateController(name, path);
                    d.animator = anim;
                }
                if (folder == "Units" && Animated.Contains(name))
                {
                    d.mode = UnitDeath.DeathMode.AnimateThenWreck;
                    d.wreckPrefab = wreck;
                }
                else if (folder == "Buildings" && wreck)
                {
                    d.mode = UnitDeath.DeathMode.SwapToWreck;
                    d.wreckPrefab = wreck;
                }
                else
                {
                    d.mode = UnitDeath.DeathMode.BreakApart;
                    if (folder == "Buildings") { d.explosionForce = 10f; d.explosionRadius = 3f; d.maxPhysicsPieces = 40; }
                }
                if (name == "DataHarvester")
                {
                    var h = go.AddComponent<Harvester>();
                    d.disableOnDeath = new Behaviour[] { h };
                }
                if (name == "DataRefinery") go.AddComponent<Refinery>();
                if (folder == "Units" && !go.GetComponent<TeamMember>()) go.AddComponent<TeamMember>();
                if (name == "DerelictOutpost")
                {
                    Object.DestroyImmediate(d);          // the outpost is never destroyed, it reverts to ruins
                    go.AddComponent<OutpostCapture>();
                }
                if (folder == "Buildings" && go.GetComponentsInChildren<Transform>(true).Any(t => t.name.EndsWith("_Upgrade_Mk2")))
                {
                    var up = go.AddComponent<BuildingUpgrade>();
                    var costs = new Dictionary<string, (int[] c, float[] t)> {
                        { "CoreNexus",    (new[] { 0, 0, 1500, 3000 }, new[] { 0f, 0f, 60f, 90f }) },
                        { "AssemblyHive", (new[] { 0, 0, 1000, 0 },    new[] { 0f, 0f, 45f, 0f }) },
                        { "DataRefinery", (new[] { 0, 0, 1200, 0 },    new[] { 0f, 0f, 45f, 0f }) },
                        { "NeonTurret",   (new[] { 0, 0, 500, 0 },     new[] { 0f, 0f, 30f, 0f }) },
                        { "PowerPylon",   (new[] { 0, 0, 400, 0 },     new[] { 0f, 0f, 25f, 0f }) },
                        { "UplinkTower",  (new[] { 0, 0, 800, 0 },     new[] { 0f, 0f, 40f, 0f }) },
                        { "HiveAerie",    (new[] { 0, 0, 1200, 3000 }, new[] { 0f, 0f, 45f, 90f }) },
                    };
                    if (costs.TryGetValue(name, out var ct)) { up.upgradeCost = ct.c; up.upgradeTime = ct.t; }
                }
                var flyerStats = new Dictionary<string, (float alt, float spd)> {
                    { "DragonflyGunship", (2.2f, 4f) }, { "HornetInterceptor", (2.6f, 6f) }, { "BeetleBomber", (2.8f, 2.5f) },
                    { "LocustCarrier", (2.6f, 3f) }, { "FireflySpotter", (3.0f, 4.5f) }, { "WaspTransport", (2.0f, 3.5f) }, { "LocustDrone", (2.0f, 6f) }, { "JellyfishDreadnought", (4.2f, 1.2f) }, { "MothMothership", (4.6f, 1.6f) }, { "MantaLeviathan", (3.8f, 2.6f) } };
                if (folder == "Units" && flyerStats.TryGetValue(name, out var fs))
                {
                    var fu = go.AddComponent<FlyingUnit>(); fu.cruiseAltitude = fs.alt; fu.speed = fs.spd;
                    if (name == "JellyfishDreadnought" || name == "MothMothership" || name == "MantaLeviathan") { fu.shadowScale = 4f; fu.turnSpeed = 45f; fu.bankAngle = 8f; }
                    if (name == "LocustCarrier") go.AddComponent<SwarmCarrier>();
                }
                if (name == "HiveAerie") go.AddComponent<AirPad>();
                if (folder == "Units" && AttackClips.ContainsKey(name) && anim && !anim.GetComponent<AttackEvents>()) anim.gameObject.AddComponent<AttackEvents>();
                PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{folder}/{name}.prefab");
                Object.DestroyImmediate(go);
            }
        }
    }

    static void BuildEnvironmentPrefabs()
    {
        EnsureFolder($"{PrefabDir}/Environment");
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/Environment/Ore_DataCrystal.fbx");
        if (!model) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        go.name = "Ore_DataCrystal";
        AddSelectionCollider(go);
        go.AddComponent<OreNode>();
        PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/Environment/Ore_DataCrystal.prefab");
        Object.DestroyImmediate(go);
    }

    [MenuItem("Tools/Cyberpunk RTS/4. Build Economy Demo Scene")]
    public static void BuildEconomyScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var refPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Buildings/DataRefinery.prefab");
        var harvPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Units/DataHarvester.prefab");
        var orePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Environment/Ore_DataCrystal.prefab");
        if (!refPrefab || !harvPrefab || !orePrefab) { Debug.LogError("[CyberpunkRTS] Run '1. Setup' first."); return; }
        EnsureFolder(Root + "/Scenes");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.16f, 0.16f, 0.24f);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground"; ground.transform.position = new Vector3(0f, 0f, 8f); ground.transform.localScale = new Vector3(4f, 1f, 4f);
        var asphalt = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Env_Asphalt.mat");
        if (asphalt) ground.GetComponent<Renderer>().sharedMaterial = asphalt;
        var sun = new GameObject("Moonlight").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 0.8f; sun.color = new Color(0.75f, 0.8f, 1f);
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f); sun.shadows = LightShadows.Soft;
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f); cam.allowHDR = true;
        camGo.transform.position = new Vector3(2f, 12f, -6f); camGo.transform.rotation = Quaternion.Euler(52f, 0f, 0f);

        new GameObject("ResourceBank").AddComponent<ResourceBank>();
        PrefabUtility.InstantiatePrefab(refPrefab);   // at the origin, dock pad facing +Z toward the ore

        var field = new GameObject("OreField_A").transform;
        var rnd = new System.Random(7);
        for (int x = -1; x <= 1; x++)
            for (int z = 0; z < 3; z++)
            {
                var o = (GameObject)PrefabUtility.InstantiatePrefab(orePrefab, field);
                o.transform.position = new Vector3(x * 1.7f + (float)(rnd.NextDouble() - 0.5) * 0.5f, 0f, 10f + z * 1.7f + (float)(rnd.NextDouble() - 0.5) * 0.5f);
                o.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
            }
        var field2 = new GameObject("OreField_B").transform;
        for (int i = 0; i < 4; i++)
        {
            var o = (GameObject)PrefabUtility.InstantiatePrefab(orePrefab, field2);
            o.transform.position = new Vector3(9f + (i % 2) * 1.7f, 0f, 6f + (i / 2) * 1.7f);
            o.transform.rotation = Quaternion.Euler(0f, i * 77f, 0f);
        }
        for (int i = 0; i < 3; i++)
        {
            var h = (GameObject)PrefabUtility.InstantiatePrefab(harvPrefab);
            h.transform.position = new Vector3(-2.5f + i * 2.5f, 0f, 3.5f);
        }
        new GameObject("DeathTester").AddComponent<DeathTester>();
        EditorApplication.ExecuteMenuItem("Tools/Cyberpunk RTS/3. Add Neon Bloom to Open Scene");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), $"{Root}/Scenes/CyberpunkRTS_Economy.unity");
        Debug.Log("[CyberpunkRTS] Economy demo built. Press Play and watch the harvesters work.");
    }

    [MenuItem("Tools/Cyberpunk RTS/5. Build Outpost Demo Scene")]
    public static void BuildOutpostScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var outpost = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Buildings/DerelictOutpost.prefab");
        var strider = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Units/Strider.prefab");
        var crusher = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Units/Chromejaw.prefab");
        if (!outpost || !strider || !crusher) { Debug.LogError("[CyberpunkRTS] Run '1. Setup' first."); return; }
        EnsureFolder(Root + "/Scenes");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.16f, 0.16f, 0.24f);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground"; ground.transform.localScale = new Vector3(3f, 1f, 3f);
        var asphalt = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Env_Asphalt.mat");
        if (asphalt) ground.GetComponent<Renderer>().sharedMaterial = asphalt;
        var sun = new GameObject("Moonlight").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 0.8f; sun.color = new Color(0.75f, 0.8f, 1f);
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f); sun.shadows = LightShadows.Soft;
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f); cam.allowHDR = true;
        camGo.transform.position = new Vector3(0f, 9f, -8f); camGo.transform.rotation = Quaternion.Euler(48f, 0f, 0f);
        var op = (GameObject)PrefabUtility.InstantiatePrefab(outpost);
        for (int i = 0; i < 4; i++)
        {
            var u = (GameObject)PrefabUtility.InstantiatePrefab(strider);
            u.name = "Hive_Strider_" + i; u.transform.position = new Vector3(-6f + i * 0.9f, 0f, -4f);
            u.GetComponent<TeamMember>().team = 0;
        }
        for (int i = 0; i < 2; i++)
        {
            var u = (GameObject)PrefabUtility.InstantiatePrefab(crusher);
            u.name = "Aegis_Chromejaw_" + i; u.transform.position = new Vector3(5f + i * 1.2f, 0f, 4f);
            u.GetComponent<TeamMember>().team = 1;
        }
        new GameObject("OutpostDemo").AddComponent<OutpostDemo>();
        new GameObject("DeathTester").AddComponent<DeathTester>();
        EditorApplication.ExecuteMenuItem("Tools/Cyberpunk RTS/3. Add Neon Bloom to Open Scene");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), $"{Root}/Scenes/CyberpunkRTS_Outpost.unity");
        Debug.Log("[CyberpunkRTS] Outpost demo built. Press Play: 1/2 send squads, G garrison, E eject.");
    }

    static void BuildConstructionResources()
    {
        string dir = Root + "/Resources/Construction";
        EnsureFolder(dir);
        foreach (var n in new[] { "Build_Pad", "Build_Pole", "Build_Rail", "Build_ScanFrame", "Build_Drone" })
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/Construction/{n}.fbx");
            if (!model) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.name = n;
            PrefabUtility.SaveAsPrefabAsset(go, $"{dir}/{n}.prefab");
            Object.DestroyImmediate(go);
        }
        AssetDatabase.DeleteAsset($"{dir}/Hologram.mat");
        AssetDatabase.CopyAsset($"{MatDir}/Shield_Hologram.mat", $"{dir}/Hologram.mat");
        AssetDatabase.DeleteAsset($"{dir}/Beam.mat");
        AssetDatabase.CopyAsset($"{MatDir}/Skitter_NeonYellow.mat", $"{dir}/Beam.mat");
    }

    [MenuItem("Tools/Cyberpunk RTS/6. Build Construction Demo Scene")]
    public static void BuildConstructionScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureFolder(Root + "/Scenes");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.16f, 0.16f, 0.24f);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground"; ground.transform.localScale = new Vector3(4f, 1f, 4f);
        var asphalt = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Env_Asphalt.mat");
        if (asphalt) ground.GetComponent<Renderer>().sharedMaterial = asphalt;
        var sun = new GameObject("Moonlight").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 0.8f; sun.color = new Color(0.75f, 0.8f, 1f);
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f); sun.shadows = LightShadows.Soft;
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f); cam.allowHDR = true;
        camGo.transform.position = new Vector3(0f, 11f, -9f); camGo.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
        var bank = new GameObject("ResourceBank").AddComponent<ResourceBank>();
        bank.startingCredits = 20000;
        var placer = new GameObject("BuildPlacer").AddComponent<BuildPlacer>();
        var list = new (string n, int c, float t)[] {
            ("CoreNexus", 3000, 16f), ("AssemblyHive", 1500, 12f), ("DataRefinery", 1200, 12f), ("PowerPylon", 300, 6f),
            ("NeonTurret", 500, 8f), ("ResearchLab", 1200, 12f), ("UplinkTower", 800, 10f), ("ArcCoil", 900, 10f), ("RailspikeTower", 1100, 10f) };
        foreach (var e in list)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Buildings/{e.n}.prefab");
            if (p) placer.buildable.Add(new BuildPlacer.Entry { prefab = p, cost = e.c, buildTime = e.t });
        }
        var tester = new GameObject("DeathTester").AddComponent<DeathTester>();
        tester.clickToKill = false;
        EditorApplication.ExecuteMenuItem("Tools/Cyberpunk RTS/3. Add Neon Bloom to Open Scene");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), $"{Root}/Scenes/CyberpunkRTS_Construction.unity");
        Debug.Log("[CyberpunkRTS] Construction demo built. Press Play, pick a building with 1-9, click to place.");
    }

    static void BuildAirResources()
    {
        string dir = Root + "/Resources/Air";
        EnsureFolder(dir);
        var shadowModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/FX/FX_AirShadow.fbx");
        if (shadowModel)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(shadowModel);
            go.name = "AirShadow";
            PrefabUtility.SaveAsPrefabAsset(go, $"{dir}/AirShadow.prefab");
            Object.DestroyImmediate(go);
        }
        AssetDatabase.DeleteAsset($"{dir}/LocustDrone.prefab");
        AssetDatabase.CopyAsset($"{PrefabDir}/Units/LocustDrone.prefab", $"{dir}/LocustDrone.prefab");
    }

    [MenuItem("Tools/Cyberpunk RTS/7. Build Air Demo Scene")]
    public static void BuildAirScene()
    {
        var aerie = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Buildings/HiveAerie.prefab");
        if (!aerie) { Debug.LogError("[CyberpunkRTS] Run '1. Setup' first."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureFolder(Root + "/Scenes");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.16f, 0.16f, 0.24f);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground"; ground.transform.localScale = new Vector3(5f, 1f, 5f);
        var asphalt = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Env_Asphalt.mat");
        if (asphalt) ground.GetComponent<Renderer>().sharedMaterial = asphalt;
        var sun = new GameObject("Moonlight").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 0.8f; sun.color = new Color(0.75f, 0.8f, 1f);
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f); sun.shadows = LightShadows.Soft;
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f); cam.allowHDR = true;
        camGo.transform.position = new Vector3(0f, 10f, -9f); camGo.transform.rotation = Quaternion.Euler(48f, 0f, 0f);
        var bank = new GameObject("ResourceBank").AddComponent<ResourceBank>(); bank.startingCredits = 40000;
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(aerie);
        var pad = inst.GetComponent<AirPad>();
        foreach (var e in new (string n, int c, float t)[] { ("DragonflyGunship", 900, 6f), ("HornetInterceptor", 700, 5f), ("BeetleBomber", 1400, 8f),
                                                             ("LocustCarrier", 1600, 9f), ("FireflySpotter", 400, 4f), ("WaspTransport", 1000, 7f) })
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Units/{e.n}.prefab");
            if (p && pad) pad.buildable.Add(new AirPad.Entry { prefab = p, cost = e.c, buildTime = e.t });
        }
        foreach (var e in new (string n, int c, float t)[] { ("JellyfishDreadnought", 6000, 20f), ("MothMothership", 5000, 18f), ("MantaLeviathan", 5500, 18f) })
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Units/{e.n}.prefab");
            if (p && pad) pad.buildable.Add(new AirPad.Entry { prefab = p, cost = e.c, buildTime = e.t, requiredLevel = 3, titan = true, maxAlive = 1 });
        }
        new GameObject("AirDemo").AddComponent<AirDemo>();
        new GameObject("DeathTester").AddComponent<DeathTester>();
        EditorApplication.ExecuteMenuItem("Tools/Cyberpunk RTS/3. Add Neon Bloom to Open Scene");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), $"{Root}/Scenes/CyberpunkRTS_Air.unity");
        Debug.Log("[CyberpunkRTS] Air demo built. Press Play: 1-6 build flyers, right-click to send them, L releases Locust drones.");
    }

    [MenuItem("Tools/Cyberpunk RTS/2. Build Test Scene")]
    public static void BuildTestScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureFolder(Root + "/Scenes");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.16f, 0.16f, 0.24f);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = new Vector3(36f, 0f, 6f);
        ground.transform.localScale = new Vector3(9f, 1f, 4f);
        var asphalt = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Env_Asphalt.mat");
        if (asphalt) ground.GetComponent<Renderer>().sharedMaterial = asphalt;

        var sun = new GameObject("Moonlight").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 0.8f; sun.color = new Color(0.75f, 0.8f, 1f);
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        sun.shadows = LightShadows.Soft;

        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f); cam.allowHDR = true;
        camGo.transform.position = new Vector3(6f, 7f, -8f);
        camGo.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

        Place($"{PrefabDir}/Units", 0f, 2.4f);
        Place($"{PrefabDir}/Buildings", 7f, 4.5f);

        new GameObject("DeathTester").AddComponent<DeathTester>();
        EditorApplication.ExecuteMenuItem("Tools/Cyberpunk RTS/3. Add Neon Bloom to Open Scene");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), $"{Root}/Scenes/CyberpunkRTS_Test.unity");
        Debug.Log("[CyberpunkRTS] Test scene built. Press Play: WASD to pan, scroll to zoom, click units to kill them.");
    }

    static void Place(string folder, float z, float spacing)
    {
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        var parent = new GameObject(Path.GetFileName(folder)).transform;
        for (int i = 0; i < guids.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.transform.position = new Vector3(i * spacing, 0f, z);
            inst.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }
    }
}
#endif
