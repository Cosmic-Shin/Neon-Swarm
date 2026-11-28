using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

public static partial class AegisBuilder
{
    const string Models = "Assets/CyberpunkRTS/Models/Aegis";
    const string MatDir = "Assets/CyberpunkRTS/Materials/Aegis";
    const string UnitDir = "Assets/Prefabs/Aegis/Units";
    const string BuildingDir = "Assets/Prefabs/Aegis/Buildings";
    const string UnitDataDir = "Assets/Data/Units/Aegis";
    const string BuildingDataDir = "Assets/Data/Buildings/Aegis";
    const string AnimDir = "Assets/Animations/Aegis";
    const string WreckDir = "Assets/Prefabs/Aegis/Wrecks";
    const string IconDir = "Assets/CyberpunkRTS/Icons/Aegis";
    const string PortraitDir = "Assets/CyberpunkRTS/UI/Portraits";
    const string FactionDir = "Assets/Resources/Factions";
    public const float BuildingScale = 3.4f;

    public static readonly Color Red = new(1f, 0.3f, 0.22f);
    public static readonly Color Amber = new(1f, 0.72f, 0.25f);

    static readonly Dictionary<string, (int end, int fire)> AttackClips = new()
    {
        { "Aegis_Peacekeeper", (514, 503) }, { "Aegis_Enforcer", (516, 504) }, { "Aegis_Lancer", (514, 503) },
        { "Aegis_MedicDrone", (516, 506) }, { "Aegis_Bastion", (516, 503) }, { "Aegis_Paladin", (518, 506) },
        { "Aegis_Interceptor", (512, 503) }, { "Aegis_Gunship", (516, 505) }, { "Aegis_Carrier", (530, 512) },
        { "Aegis_Colossus", (520, 510) }, { "Aegis_Technician", (516, 504) }, { "Aegis_AATrooper", (514, 503) },
        { "Aegis_StrikeBike", (512, 502) }, { "Aegis_Marksman", (520, 506) }, { "Aegis_Suppressor", (518, 506) },
        { "Aegis_Watcher", (512, 504) }, { "Aegis_PatrolDrone", (510, 503) }, { "Aegis_Dominion", (524, 510) },
        { "Aegis_Breaker", (516, 507) }, { "Aegis_Cadet", (510, 503) }, { "Aegis_Hailstorm", (524, 504) }, { "Aegis_Warden", (516, 505) },
        { "Aegis_Sapper", (512, 504) }, { "Aegis_Override", (514, 505) }, { "Aegis_Seeker", (508, 503) }, { "Aegis_Hammerhead", (524, 510) },
        { "Aegis_Mothership", (530, 506) }, { "Aegis_Tribunal", (526, 510) }, { "Aegis_Spotter", (510, 503) },
    };

    class U
    {
        public string name, template, twin, display, muzzles, desc;
        public float scale = 2.2f, hover;
        public System.Action<UnitData> tweak;
    }

    static readonly U[] Units =
    {
        new U { name = "Collector", template = "DataHarvester", twin = "DataHarvester", display = "Collector", scale = 2.4f,
            desc = "Armoured ore hauler. Mines crystal fields and brings the cargo to a Refinery or the Command Spire." },
        new U { name = "Peacekeeper", template = "Skitter", twin = "Skitter", display = "Peacekeeper",
            desc = "Aegis line trooper. Fielded in squads of three with burst rifles. Cheap, dependable, everywhere." },
        new U { name = "Enforcer", template = "Burner", twin = "Burner", display = "Enforcer",
            desc = "Riot-suit flamer. Sweeps a cone of fire that shreds packed infantry and swarms." },
        new U { name = "AATrooper", template = "Skitter", twin = "Skitter", display = "AA Trooper",
            desc = "Twin flak tubes on a shoulder rig. Cannot hit ground targets, but tears flyers out of the sky.",
            tweak = d => { d.squadSize = 2; d.hitsGround = false; d.hitsAir = true; d.weapon = DamageTable.Weapon.Cannon; d.damage = 16f; d.range = 18f; d.attackCooldown = 1.1f; d.cost = 500; d.maxHealth = 90f; } },
        new U { name = "Marksman", template = "SniperMantis", twin = "SniperMantis", display = "Marksman",
            desc = "Long-range rifle specialist. Picks off units from beyond their reach; fragile up close." },
        new U { name = "Technician", template = "Patchbot", twin = "Patchbot", display = "Technician", muzzles = "TEC_ToolPoint",
            desc = "Field engineer with a repair arc. Mends nearby units in the field." },
        new U { name = "Watcher", template = "GlitchMoth", twin = "GlitchMoth", display = "Watcher", muzzles = "WAT_Muzzle",
            desc = "Fast scout drone with a wide sensor sweep. Barely armed; use it to find the enemy." },
        new U { name = "StrikeBike", template = "Strider", twin = "Strider", display = "Strike Bike", scale = 2.4f,
            desc = "Fast attack bike. Hits and runs, great against harvesters and stragglers." },
        new U { name = "MedicDrone", template = "Patchbot", twin = "RelaySpire", display = "Medic Drone", muzzles = "MED_BeamPoint", hover = 1.4f,
            desc = "Hovering medic that pulses repair nanites into wounded units around it.",
            tweak = d => { var p = Twin<UnitData>("Patchbot"); d.damage = p.damage; d.range = p.range; d.attackCooldown = p.attackCooldown; d.maxHealth = p.maxHealth + 20f; d.cost = p.cost + 50; d.role = UnitData.Role.Support; } },
        new U { name = "Suppressor", template = "EMPCentipede", twin = "EMPCentipede", display = "Suppressor", muzzles = "SUP_PulsePoint",
            desc = "Crowd-control walker. Its EMP pulse stuns everything around the target." },
        new U { name = "Lancer", template = "CrusherTarantula", twin = "CrusherTarantula", display = "Lancer",
            desc = "Railgun tank. Punches through heavy armour and fortifications from mid range.",
            tweak = d => { d.weapon = DamageTable.Weapon.Cannon; d.buildingDamage = 1.3f; } },
        new U { name = "Bastion", template = "MortarScarab", twin = "MortarScarab", display = "Bastion",
            desc = "Mobile artillery. Lobs shells at long range with splash damage. Keep it behind the line." },
        new U { name = "Paladin", template = "BulwarkTick", twin = "BulwarkTick", display = "Paladin",
            desc = "Heavy assault mech with a shield dome. Soaks fire for the squad and hits back with an arm cannon." },
        new U { name = "Colossus", template = "QueenArachne", twin = "QueenArachne", display = "Colossus", muzzles = "COL_LanceMuzzle", scale = 1.5f,
            desc = "Aegis super-heavy walker. Twin plasma lances melt anything in front of it. Needs Command Spire Mk III.",
            tweak = d => { d.ability = UnitData.Ability.None; d.splash = 0f; d.damage *= 1.35f; d.extra = null; } },
        new U { name = "Interceptor", template = "HornetInterceptor", twin = "HornetInterceptor", display = "Interceptor", scale = 2.8f, hover = 6.5f,
            desc = "Air-superiority fighter. Missiles only hit other flyers." },
        new U { name = "Gunship", template = "DragonflyGunship", twin = "DragonflyGunship", display = "Gunship", scale = 3f, hover = 7f,
            desc = "Strafing gunship. Rakes ground targets with twin cannons." },
        new U { name = "Skylift", template = "WaspTransport", twin = "WaspTransport", display = "Skylift", scale = 2.8f, hover = 6f,
            desc = "Air transport. Carries up to five infantry or support units over walls and cliffs." },
        new U { name = "Carrier", template = "MothMothership", twin = "MothMothership", display = "Carrier", muzzles = "CAR_DropPoint", scale = 4f, hover = 10f,
            desc = "Heavy dropship. Drops Peacekeeper squads straight onto the target. Needs Command Spire Mk III." },
        new U { name = "Dominion", template = "MantaLeviathan", twin = "MantaLeviathan", display = "Dominion", scale = 4.5f, hover = 11f,
            desc = "Sky fortress. Wing turrets and an orbital lance make it the heaviest thing in the air. Needs Command Spire Mk III." },
        new U { name = "PatrolDrone", template = "SentryDrone", twin = "SentryDrone", display = "Patrol Drone", scale = 3f,
            desc = "Drone Bay interceptor. Launches by itself to chase off attackers." },

        // role fill-ins: Aegis counterparts for the Hive-only units (README "Role fill-ins"); Seeker before Mothership (its drone)
        new U { name = "Cadet", template = "Hackling", twin = "Hackling", display = "Cadet",
            desc = "Cheap rifle trooper trained in squads of three. Shoots ground and air; strength in numbers." },
        new U { name = "Sapper", template = "Ratline", twin = "Ratline", display = "Sapper",
            desc = "Breaching team of three. Launches demolition charges that wreck buildings; weak against units." },
        new U { name = "Override", template = "NetrunnerHive", twin = "NetrunnerHive", display = "Override",
            desc = "Electronic-warfare specialist. Hack bolts stun whatever they hit. Needs Command Spire Mk III." },
        new U { name = "Warden", template = "WireWeaver", twin = "WireWeaver", display = "Warden",
            desc = "Snare trooper. Fires stasis nets that leave a slowing field where they land." },
        new U { name = "Breaker", template = "Chromejaw", twin = "Chromejaw", display = "Breaker",
            desc = "Close-assault mech. Charges into the enemy line and pounds it apart with hydraulic gauntlets." },
        new U { name = "Hailstorm", template = "Overclock", twin = "Overclock", display = "Hailstorm",
            desc = "Rotary-cannon walker. Spins up a storm of fire, then has to vent the heat. Needs Command Spire Mk III." },
        new U { name = "Seeker", template = "LocustDrone", twin = "LocustDrone", display = "Seeker", scale = 4.5f, hover = 5.5f,
            desc = "Swarm of five tiny attack drones. Fast, fragile and hard to pin down." },
        new U { name = "Hammerhead", template = "BeetleBomber", twin = "BeetleBomber", display = "Hammerhead", scale = 2.4f, hover = 8f,
            desc = "Bomber. Opens its bay over the target and drops heavy charges. Cannot hit flyers." },
        new U { name = "Mothership", template = "LocustCarrier", twin = "LocustCarrier", display = "Mothership", scale = 3.8f, hover = 9.5f,
            desc = "Drone carrier. Launches Seekers at whatever it fights and docks them back to rebuild." },
        new U { name = "Tribunal", template = "JellyfishDreadnought", twin = "JellyfishDreadnought", display = "Tribunal", scale = 4.5f, hover = 12f,
            desc = "Titan. Drops low and shocks everything beneath it, stunning ground units in a wide field. Needs Command Spire Mk III." },
        new U { name = "Spotter", template = "FireflySpotter", twin = "FireflySpotter", display = "Spotter", scale = 3.5f, hover = 6f,
            desc = "Cheap scout flyer. Marks targets from the air and sees far; barely armed." },
    };

    class B
    {
        public string name, template, twin, display, desc;
        public string[] units;
        public System.Action<BuildingData> tweak;
    }

    static readonly B[] Buildings =
    {
        new B { name = "CommandSpire", template = "CoreNexus", display = "Command Spire",
            desc = "Aegis headquarters. Powers the base, accepts ore and unlocks higher tiers when upgraded." },
        new B { name = "BarracksBay", template = "AssemblyHive", twin = "AssemblyHive", display = "Barracks Bay",
            units = new[] { "Collector", "Peacekeeper", "Cadet", "Enforcer", "AATrooper", "Marksman", "Sapper", "Technician", "Warden", "Watcher", "Override" },
            desc = "Trains Aegis infantry and Collectors." },
        new B { name = "MotorPool", template = "AssemblyHive", display = "Motor Pool",
            units = new[] { "StrikeBike", "MedicDrone", "Breaker", "Lancer", "Suppressor", "Bastion", "Paladin", "Hailstorm", "Colossus" },
            desc = "Vehicle and mech factory: bikes, tanks, artillery and the Colossus.",
            tweak = d => { d.cost = 1500; d.buildTime = 22f; } },
        new B { name = "Airfield", template = "HiveAerie", twin = "HiveAerie", display = "Airfield",
            units = new[] { "Spotter", "Seeker", "Interceptor", "Gunship", "Hammerhead", "Skylift", "Mothership", "Carrier", "Tribunal", "Dominion" },
            desc = "Builds the Aegis air wing. Upgrade to Mk III for the Dominion and Carrier." },
        new B { name = "Refinery", template = "DataRefinery", twin = "DataRefinery", display = "Refinery",
            desc = "Turns Collector cargo into credits. Comes with a free Collector." },
        new B { name = "ExtractionRig", template = "DataSiphon", twin = "DataSiphon", display = "Extraction Rig",
            desc = "Pumps a slow, steady trickle of credits with no Collectors needed." },
        new B { name = "FusionReactor", template = "PowerPylon", twin = "PowerPylon", display = "Fusion Reactor",
            desc = "Produces power and extends your build area." },
        new B { name = "SentinelTurret", template = "NeonTurret", twin = "NeonTurret", display = "Sentinel Turret",
            desc = "Basic gun turret. Hits ground and air." },
        new B { name = "ArcTurret", template = "ArcCoil", twin = "ArcCoil", display = "Arc Turret",
            desc = "Short-range tesla turret. Its arc jumps between packed ground units; fast and deadly up close." },
        new B { name = "FlakBattery", template = "FlakLantern", twin = "FlakLantern", display = "Flak Battery",
            desc = "Anti-air battery with splash. Cannot hit ground targets." },
        new B { name = "RailBattery", template = "RailspikeTower", twin = "RailspikeTower", display = "Rail Battery",
            desc = "Heavy long-range rail cannon. Slow, devastating against ground targets." },
        new B { name = "PulseWall", template = "FirewallGate", twin = "FirewallGate", display = "Pulse Wall",
            desc = "Energy wall segment. Drag to lay a line." },
        new B { name = "PulseGate", template = "FirewallGate", twin = "FirewallGate", display = "Pulse Gate",
            desc = "Wall section that opens for your own ground units and stays shut for enemies. Drag to lay a line.",
            tweak = d => { d.cost = Mathf.RoundToInt(d.cost * 1.6f / 25f) * 25; } },
        new B { name = "PulsePost", template = "FirewallGate", twin = "FirewallGate", display = "Pulse Post",
            desc = "Sturdy wall corner and end post.",
            tweak = d => { d.dragLine = false; d.cost = Mathf.Max(50, Mathf.RoundToInt(d.cost * 0.6f / 25f) * 25); } },
        new B { name = "MineField", template = "EMPMineField", twin = "EMPMineField", display = "Mine Field",
            desc = "Buried EMP charges that stun enemy units walking over them." },
        new B { name = "SensorArray", template = "UplinkTower", twin = "UplinkTower", display = "Sensor Array",
            desc = "Radar: shows enemy units on the minimap through the fog. Unlocks advanced structures." },
        new B { name = "ShieldProjector", template = "CloakFieldGenerator", twin = "CloakFieldGenerator", display = "Shield Projector",
            desc = "Projects a protective field over nearby buildings and units." },
        new B { name = "TechCenter", template = "OverwatchSpire", twin = "OverwatchSpire", display = "Tech Center",
            desc = "Long-range sensor tower and command uplink. Sees far across the map." },
        new B { name = "ResearchLab", template = "ResearchLab", twin = "ResearchLab", display = "Research Lab",
            desc = "Researches upgrades for your army and economy." },
        new B { name = "OrbitalLance", template = "SwarmSilo", twin = "SwarmSilo", display = "Orbital Lance",
            desc = "Aegis superweapon. After a long charge, a satellite beam burns a wide area for nine seconds and EMP-stuns the centre. Needs Command Spire Mk III. One per player." },
        new B { name = "DroneBay", template = "SentryDroneHangar", twin = "SentryDroneHangar", display = "Drone Bay",
            desc = "Base defence: Patrol Drones launch by themselves to chase off attackers." },
    };

    [MenuItem("Tools/GD5/Build Aegis Faction")]
    public static void BuildAll()
    {
        MoveOldData();
        // models imported as static need animation on BEFORE the batch, or a newly animated export shows no take
        foreach (var path in ModelPaths())
            if (AssetImporter.GetAtPath(path) is ModelImporter m && !m.importAnimation)
            { m.importAnimation = true; m.animationType = ModelImporterAnimationType.Generic; m.SaveAndReimport(); }
        try
        {
            AssetDatabase.StartAssetEditing();
            var mats = Materials();
            Importers(mats);
        }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();
        Icons();
        foreach (var d in new[] { UnitDir, BuildingDir, UnitDataDir, BuildingDataDir, AnimDir, FactionDir }) Folder(d);
        fx = MakeFx();
        TuneFx();
        ConstructionKit();
        balance.Clear();
        HivePortraits();
        var unitData = new Dictionary<string, UnitData>();
        foreach (var u in Units) unitData[u.name] = MakeUnitData(u);
        var unitPrefabs = new Dictionary<string, GameObject>();
        foreach (var u in Units) unitPrefabs[u.name] = MakeUnit(u, unitData[u.name]);
        var carrier = unitData["Carrier"];
        carrier.extra = unitPrefabs["Peacekeeper"];
        EditorUtility.SetDirty(carrier);
        var buildingData = new Dictionary<string, BuildingData>();
        foreach (var b in Buildings) if (b.name != "CommandSpire") buildingData[b.name] = MakeBuildingData(b);
        research = MakeResearch(unitData, buildingData);
        foreach (var b in Buildings) MakeBuilding(b, buildingData.TryGetValue(b.name, out var d) ? d : null, unitPrefabs);
        foreach (var b in Buildings)
        {
            if (!buildingData.TryGetValue(b.name, out var d)) continue;
            d.prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{BuildingDir}/Aegis_{b.name}.prefab");
            if (b.twin != null && Twin<BuildingData>(b.twin) is BuildingData hive && hive.requires != null)
                d.requires = hive.requires.Select(r => buildingData.Values.FirstOrDefault(x => TwinOf(x) == r)).Where(x => x != null).ToArray();
            if (b.name == "MotorPool") d.requires = new[] { buildingData["Refinery"], buildingData["BarracksBay"] };
            EditorUtility.SetDirty(d);
        }
        Factions(buildingData, unitData, unitPrefabs);
        AIProfiles();
        var extra = NeutralAndProps();
        WriteBalance();
        var list = AssetDatabase.LoadAssetAtPath<PrefabList>("Assets/Resources/PrefabList.asset");
        if (list != null)
        {
            var all = list.prefabs.Where(p => p != null && !p.name.StartsWith("Aegis_")).ToList();
            all.AddRange(unitPrefabs.Values);
            foreach (var b in Buildings) all.Add(AssetDatabase.LoadAssetAtPath<GameObject>($"{BuildingDir}/Aegis_{b.name}.prefab"));
            all.RemoveAll(p => p.name.StartsWith("Neutral_Corp_"));
            all.AddRange(extra);
            list.prefabs = all.ToArray();
            EditorUtility.SetDirty(list);
        }
        AssetDatabase.SaveAssets();
        ReimportPrefabs();
        AssetDatabase.SaveAssets();
        Debug.Log("[GD5] Aegis faction built.");
    }

    // Hive unit cards get their portraits too (UI/Portraits/Hive/portrait_<asset name>.png)
    static void HivePortraits()
    {
        foreach (var g in AssetDatabase.FindAssets("t:UnitData", new[] { "Assets/Data/Units" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (path.Contains("/Aegis/")) continue;
            var d = AssetDatabase.LoadAssetAtPath<UnitData>(path);
            var art = AssetDatabase.LoadAssetAtPath<Sprite>($"{PortraitDir}/Hive/portrait_{d.name}.png");
            if (art == null || d.portrait == art) continue;
            d.portrait = art;
            EditorUtility.SetDirty(d);
        }
    }

    static readonly Dictionary<BuildingData, BuildingData> twinOf = new();
    static BuildingData TwinOf(BuildingData own) => twinOf.TryGetValue(own, out var t) ? t : null;

    static T Twin<T>(string name) where T : Object
    {
        string dir = typeof(T) == typeof(UnitData) ? "Assets/Data/Units" : "Assets/Data/Buildings";
        return AssetDatabase.LoadAssetAtPath<T>($"{dir}/{name}.asset");
    }

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        Folder(Path.GetDirectoryName(path).Replace("\\", "/"));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace("\\", "/"), Path.GetFileName(path));
    }

    static IEnumerable<string> ModelPaths()
    {
        foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { Models })) yield return AssetDatabase.GUIDToAssetPath(g);
        foreach (var g in AssetDatabase.FindAssets("Neutral_Corp t:Model", new[] { "Assets/CyberpunkRTS/Models/Environment" })) yield return AssetDatabase.GUIDToAssetPath(g);
    }

    static Dictionary<string, Material> Materials()
    {
        Folder(MatDir);
        var result = new Dictionary<string, Material>();
        foreach (var n in new[] { "Skitter_Graphite", "Dead_Ember", "Wreck_Charred", "Neutral_Gray", "Neutral_Glow", "Shield_Hologram" })
            if (AssetDatabase.LoadAssetAtPath<Material>($"Assets/CyberpunkRTS/Materials/{n}.mat") is Material m) result[n] = m;
        var source = new Dictionary<string, Material>();
        foreach (var p in ModelPaths())
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                if (o is Material m && !result.ContainsKey(m.name) && !source.ContainsKey(m.name)) source[m.name] = m;
        var shader = Shader.Find("Standard");
        foreach (var (name, src) in source)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew) mat = new Material(shader);
            mat.shader = shader;
            Color c = src.GetColor("_Color");
            mat.SetColor("_Color", c);
            float metal = name.Contains("Gold") || name.Contains("Gunmetal") ? 0.65f : name.Contains("Black") ? 0.45f : 0.1f;
            mat.SetFloat("_Metallic", metal);
            mat.SetFloat("_Glossiness", src.GetFloat("_Glossiness"));
            Color e = src.HasProperty("_EmissionColor") ? src.GetColor("_EmissionColor") : Color.black;
            float peak = e.maxColorComponent;
            if (peak > 0.01f)
            {
                float k = Mathf.Min(peak * 0.35f, 3f) / peak;
                if (name == "Aegis_TeamColor") k = 1.6f / peak;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(e.r * k, e.g * k, e.b * k));
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }
            if (c.a < 0.99f)
            {
                mat.SetFloat("_Mode", 3f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }
            mat.enableInstancing = true;
            if (isNew) AssetDatabase.CreateAsset(mat, path); else EditorUtility.SetDirty(mat);
            result[name] = mat;
        }
        return result;
    }

    static void Importers(Dictionary<string, Material> mats)
    {
        foreach (var path in ModelPaths())
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter imp)) continue;
            string name = Path.GetFileNameWithoutExtension(path);
            imp.globalScale = 1f;
            imp.useFileScale = true;
            imp.bakeAxisConversion = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            foreach (var kv in mats) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            var takes = imp.defaultClipAnimations;
            var take = takes != null && takes.Length > 0 ? takes[0] : null;
            var clips = new List<ModelImporterClipAnimation>();
            float off = take != null ? take.firstFrame - 1f : 0f;
            float last = take != null ? take.lastFrame - off : 0f;
            void Add(string clip, float a, float b, bool loop, float fire = -1f)
            {
                if (take == null || b > last + 0.5f) return;
                var c = new ModelImporterClipAnimation { name = clip, takeName = take.takeName, firstFrame = a + off, lastFrame = b + off, loopTime = loop, loopPose = false };
                if (fire > 0f) c.events = new[] { new AnimationEvent { functionName = "Fire", time = (fire - a) / (b - a) } };
                clips.Add(c);
            }
            if (path.Contains("/Units/"))
            {
                Add("Walk", 101, 125, true);
                Add("Idle", 201, 249, true);
                Add("Death", 1, 40, false);
                if (name == "Aegis_Collector") { Add("Harvest", 301, 349, true); Add("Unload", 401, 449, true); }
                if (AttackClips.TryGetValue(name, out var atk)) Add("Attack", 501, atk.end, false, atk.fire);
                if (name == "Aegis_Bastion") { Add("Deploy", 601, 620, false); Add("Undeploy", 621, 640, false); }
                if (name == "Aegis_Skylift") Add("Unload", 401, 449, false);
            }
            else if (path.Contains("/Buildings/") || path.Contains("/Environment/") || name == "Build_AegisCrane")
            {
                if (name == "Build_AegisCrane") Add("Idle", 1, 120, true);
                else Add("Idle", 1, 60, true);
                if (name == "Aegis_OrbitalLance") Add("Fire", 100, 200, false);
                if (name == "Aegis_RailBattery") Add("Fire", 100, 120, false);
                if (name is "Aegis_SentinelTurret" or "Aegis_FlakBattery" or "Aegis_ArcTurret") Add("Fire", 100, 112, false);
                if (name is "Aegis_MotorPool" or "Aegis_BarracksBay" or "Aegis_DroneBay") { Add("Open", 100, 120, false); Add("Close", 130, 150, false); }
                if (name == "Aegis_PulseGate") { Add("Open", 100, 114, false); Add("Close", 130, 144, false); }
            }
            imp.importAnimation = clips.Count > 0;
            imp.animationType = clips.Count > 0 ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            if (clips.Count > 0) imp.clipAnimations = clips.ToArray();
            imp.SaveAndReimport();
        }
    }

    static void Icons()
    {
        foreach (var dir in new[] { IconDir, "Assets/CyberpunkRTS/UI/Portraits/Aegis", "Assets/CyberpunkRTS/UI/Portraits/Hive", "Assets/CyberpunkRTS/UI/Faction" })
            foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (!(AssetImporter.GetAtPath(p) is TextureImporter ti)) continue;
                if (ti.textureType == TextureImporterType.Sprite && ti.spriteImportMode == SpriteImportMode.Single) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
    }

    static Sprite Icon(string kind, string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{IconDir}/icon_{kind}_{name}.png");

    static UnitData MakeUnitData(U u)
    {
        string path = $"{UnitDataDir}/{u.name}.asset";
        var twin = Twin<UnitData>(u.twin);
        var d = Object.Instantiate(twin);
        d.displayName = u.display;
        d.description = u.desc;
        d.icon = Icon("unit", u.name);
        d.portrait = AssetDatabase.LoadAssetAtPath<Sprite>($"{PortraitDir}/Aegis/portrait_{u.name}.png");
        d.muzzles = u.muzzles ?? "";
        u.tweak?.Invoke(d);
        Balance(u, d);
        Visuals(u, d);
        d.faction = FactionId.Aegis;
        var existing = AssetDatabase.LoadAssetAtPath<UnitData>(path);
        if (existing != null) { EditorUtility.CopySerialized(d, existing); existing.name = u.name; Object.DestroyImmediate(d); EditorUtility.SetDirty(existing); return existing; }
        AssetDatabase.CreateAsset(d, path);
        return d;
    }

    static BuildingData MakeBuildingData(B b)
    {
        string path = $"{BuildingDataDir}/{b.name}.asset";
        var twin = b.twin != null ? Twin<BuildingData>(b.twin) : Twin<BuildingData>("AssemblyHive");
        var d = Object.Instantiate(twin);
        d.displayName = b.display;
        d.description = b.desc;
        d.icon = Icon("building", b.name);
        d.requires = new BuildingData[0];
        b.tweak?.Invoke(d);
        d.faction = FactionId.Aegis;
        var existing = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
        if (existing != null) { EditorUtility.CopySerialized(d, existing); existing.name = b.name; Object.DestroyImmediate(d); d = existing; EditorUtility.SetDirty(d); }
        else AssetDatabase.CreateAsset(d, path);
        if (b.twin != null) twinOf[d] = twin;
        return d;
    }

    static Bounds LocalBounds(Transform root, bool skipEffects = true)
    {
        var b = new Bounds();
        bool any = false;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r is SpriteRenderer || r is LineRenderer || r is ParticleSystemRenderer) continue;
            if (skipEffects && (r.name == "SelectionRing" || r.name.Contains("Shadow") || r.name.Contains("_Damage") || r.name.Contains("Upgrade") || r.transform.parent.name.Contains("Upgrade") || r.transform.parent.name.Contains("_Damage") || r.name.Contains("Radius"))) continue;
            if (!r.gameObject.activeInHierarchy) continue;
            var wb = r.bounds;
            foreach (var c in Corners(wb))
            {
                var p = root.InverseTransformPoint(c);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
        }
        return b;
    }

    static IEnumerable<Vector3> Corners(Bounds b)
    {
        for (int i = 0; i < 8; i++)
            yield return b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
    }

    static Transform Part(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    static void Set(Object o, string field, Object value)
    {
        var so = new SerializedObject(o);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning($"[GD5] {o.GetType().Name}.{field} not found"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetFloat(Object o, string field, float value)
    {
        var so = new SerializedObject(o);
        var p = so.FindProperty(field);
        if (p == null) return;
        p.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetColor(Object o, string field, Color value)
    {
        var so = new SerializedObject(o);
        var p = so.FindProperty(field);
        if (p == null) return;
        p.colorValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static RuntimeAnimatorController UnitController(string aegis, bool harvester)
    {
        string fbx = $"{Models}/Units/Aegis_{aegis}.fbx";
        var clips = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
        AnimationClip Get(string n) => clips.TryGetValue(n, out var c) ? c : null;
        var baseCtrl = harvester
            ? AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Units/DataHarvester.controller")
            : AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/UnitLocomotion.controller");
        string path = $"{AnimDir}/Aegis_{aegis}.overrideController";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if (ctrl == null) { ctrl = new AnimatorOverrideController(baseCtrl); AssetDatabase.CreateAsset(ctrl, path); }
        ctrl.runtimeAnimatorController = baseCtrl;
        var list = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        ctrl.GetOverrides(list);
        for (int i = 0; i < list.Count; i++)
        {
            string n = list[i].Key.name;
            AnimationClip own = n.Contains("Walk") ? Get("Walk") : n.Contains("Idle") ? Get("Idle")
                : n.Contains("Attack") ? Get("Attack") : n.Contains("Harvest") ? Get("Harvest") ?? Get("Idle")
                : n.Contains("Unload") ? Get("Unload") ?? Get("Idle") : null;
            list[i] = new KeyValuePair<AnimationClip, AnimationClip>(list[i].Key, own);
        }
        ctrl.ApplyOverrides(list);
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    static GameObject MakeUnit(U u, UnitData data)
    {
        var root = PrefabUtility.LoadPrefabContents($"Assets/Prefabs/Units/{u.template}.prefab");
        try
        {
            root.name = "Aegis_" + u.name;
            var model = root.transform.Find("Model");
            for (int i = model.childCount - 1; i >= 0; i--) Object.DestroyImmediate(model.GetChild(i).gameObject);
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/Units/Aegis_{u.name}.fbx");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx, model);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = fbx.transform.localRotation;
            inst.transform.localScale = Vector3.one;
            model.localScale = Vector3.one * u.scale;
            bool flyer = root.TryGetComponent(out Flyer fly);
            float hover = flyer ? u.hover > 0f ? u.hover : model.localPosition.y : u.hover;
            model.localPosition = new Vector3(0f, hover, 0f);

            var anim = inst.TryGetComponent(out Animator found) ? found : inst.AddComponent<Animator>();
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            anim.runtimeAnimatorController = UnitController(u.name, root.TryGetComponent(out Harvester _));

            var b = LocalBounds(root.transform);
            float foot = Mathf.Max(b.size.x, b.size.z);
            if (root.TryGetComponent(out BoxCollider box)) { box.center = b.center; box.size = b.size; }
            if (!flyer && root.TryGetComponent(out NavMeshAgent agent))
            {
                agent.radius = Mathf.Clamp(foot * 0.42f, 0.4f, 3f);
                agent.height = Mathf.Max(1f, b.max.y);
            }
            if (flyer) { fly.hover = hover; fly.size = foot * 0.5f; EditorUtility.SetDirty(fly); }
            if (root.transform.Find("SelectionRing") is Transform ring)
            {
                float r = flyer ? foot * 1.1f : foot * 0.85f + 0.3f;
                ring.localScale = new Vector3(r, ring.localScale.y, r);
                ring.localPosition = new Vector3(0f, ring.localPosition.y, 0f);
            }
            if (root.transform.Find("HealthBar") is Transform bar) bar.localPosition = new Vector3(0f, b.max.y + 0.6f, 0f);
            if (root.transform.Find("AirShadow") is Transform shadow) shadow.localScale = Vector3.one * foot * 0.6f;
            foreach (var rk in root.GetComponentsInChildren<RecoilKick>(true)) rk.target = model;

            WreckFor(root, u.name, u.scale, 20f);
            UnitExtras(root, u, inst, anim);
            foreach (var pivotName in new[] { "LAN_TurretPivot", "BAS_TurretPivot" })
                if (Part(inst.transform, pivotName) != null) { var aim = root.TryGetComponent(out AimPivot found2) ? found2 : root.AddComponent<AimPivot>(); aim.pivotName = pivotName; aim.turnSpeed = 140f; }
            var su = root.GetComponent<SelectableUnit>();
            Set(su, "data", data);
            Set(su, "animator", anim);
            string path = $"{UnitDir}/Aegis_{u.name}.prefab";
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void ShieldMaterial()
    {
        const string path = "Assets/Resources/ShieldDome.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = mat == null;
        if (isNew) mat = new Material(Shader.Find("Standard"));
        mat.SetFloat("_Mode", 2f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_Color", new Color(1f, 0.75f, 0.3f, 0.05f));
        mat.SetColor("_EmissionColor", new Color(0.3f, 0.2f, 0.08f));
        mat.SetFloat("_Glossiness", 0.9f);
        mat.renderQueue = 3100;
        mat.enableInstancing = true;
        if (isNew) AssetDatabase.CreateAsset(mat, path); else EditorUtility.SetDirty(mat);
    }

    static void WreckFor(GameObject root, string name, float scale, float life)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/Wrecks/Wreck_Aegis_{name}.fbx");
        if (fbx == null || !root.TryGetComponent(out UnitDeath death)) return;
        Folder(WreckDir);
        var w = new GameObject("Wreck_Aegis_" + name);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx, w.transform);
        inst.transform.localRotation = fbx.transform.localRotation;
        inst.transform.localScale = fbx.transform.localScale * scale;
        var prefab = PrefabUtility.SaveAsPrefabAsset(w, $"{WreckDir}/Wreck_Aegis_{name}.prefab");
        Object.DestroyImmediate(w);
        death.mode = UnitDeath.DeathMode.SwapToWreck;
        death.wreckPrefab = prefab;
        SetFloat(death, "wreckLifetime", life);
    }

    static RuntimeAnimatorController BuildingController(string aegis)
    {
        string fbx = $"{Models}/Buildings/Aegis_{aegis}.fbx";
        var clips = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
        if (clips.Count == 0) return null;
        string path = $"{AnimDir}/Aegis_{aegis}.controller";
        var ctrl = FreshController(path);
        var sm = ctrl.layers[0].stateMachine;
        AnimatorState idle = null;
        if (clips.TryGetValue("Idle", out var idleClip)) { idle = sm.AddState("Idle"); idle.motion = idleClip; sm.defaultState = idle; }
        else { idle = sm.AddState("Rest"); sm.defaultState = idle; }
        AnimatorState open = null;
        foreach (var n in new[] { "Open", "Close", "Fire" })
        {
            if (!clips.TryGetValue(n, out var clip)) continue;
            ctrl.AddParameter(n, AnimatorControllerParameterType.Trigger);
            var st = sm.AddState(n);
            st.motion = clip;
            var to = sm.AddAnyStateTransition(st);
            to.AddCondition(AnimatorConditionMode.If, 0f, n);
            to.duration = 0.05f;
            to.canTransitionToSelf = false;
            if (n == "Open") { open = st; var hold = sm.AddState("Opened"); hold.motion = null; var t = st.AddTransition(hold); t.hasExitTime = true; t.exitTime = 1f; t.duration = 0f; st.writeDefaultValues = false; hold.writeDefaultValues = false; }
            else { var back = st.AddTransition(idle); back.hasExitTime = true; back.exitTime = 1f; back.duration = 0.1f; }
        }
        return ctrl;
    }

    static void MakeBuilding(B b, BuildingData data, Dictionary<string, GameObject> units)
    {
        var root = PrefabUtility.LoadPrefabContents($"Assets/Prefabs/Buildings/{b.template}.prefab");
        try
        {
            root.name = "Aegis_" + b.name;
            float old = root.transform.localScale.x;
            root.transform.localScale = Vector3.one;
            var drop = new List<GameObject>();
            foreach (Transform c in root.transform)
            {
                var src = PrefabUtility.GetCorrespondingObjectFromSource(c.gameObject);
                bool isModel = c.name == "Model" || (src != null && AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(src)) is ModelImporter);
                if (isModel) { drop.Add(c.gameObject); continue; }
                c.localPosition *= old;
                c.localScale *= old;
            }
            foreach (var g in drop) Object.DestroyImmediate(g);

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/Buildings/Aegis_{b.name}.fbx");
            var holder = new GameObject("Model").transform;
            holder.SetParent(root.transform, false);
            holder.localScale = Vector3.one * BuildingScale;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx, holder);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = fbx.transform.localRotation;
            inst.transform.localScale = fbx.transform.localScale;

            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith("_Upgrade_Mk2") || t.name.EndsWith("_Upgrade_Mk3") || t.name.EndsWith("_Damage") || t.name.Contains("_Damage_")) t.gameObject.SetActive(false);

            var ctrl = BuildingController(b.name);
            if (ctrl != null)
            {
                var anim = inst.TryGetComponent(out Animator found) ? found : inst.AddComponent<Animator>();
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullCompletely;
            }

            var bounds = LocalBounds(root.transform);
            if (root.TryGetComponent(out BoxCollider box)) { box.center = bounds.center; box.size = bounds.size; }
            if (root.TryGetComponent(out NavMeshObstacle obs)) { obs.carving = true; obs.center = bounds.center; obs.size = bounds.size; }
            if (root.transform.Find("HealthBar") is Transform bar) { bar.localPosition = new Vector3(bounds.center.x, bounds.max.y + 1.2f, bounds.center.z); bar.localScale = Vector3.one; }

            foreach (var g in root.GetComponents<Gate>()) Object.DestroyImmediate(g);
            foreach (var g in root.GetComponents<AerieFX>()) Object.DestroyImmediate(g);
            if (root.transform.Find("PadEdge") is Transform pad) Object.DestroyImmediate(pad.gameObject);

            if (root.TryGetComponent(out Building building))
            {
                var spawn = root.transform.Find("SpawnPoint");
                var own = Part(inst.transform, "BB_SpawnPoint") ?? Part(inst.transform, "MP_SpawnPoint") ?? Part(inst.transform, "AF_LandPoint");
                if (spawn != null && own != null)
                {
                    Vector3 p = root.transform.InverseTransformPoint(own.position);
                    Vector3 dir = new Vector3(p.x, 0f, p.z).normalized;
                    if (dir == Vector3.zero) dir = Vector3.forward;
                    spawn.localPosition = new Vector3(p.x, 0f, p.z) + dir * 3f;
                }
                if (b.units != null && b.units.Length > 0)
                {
                    var so = new SerializedObject(building);
                    var arr = so.FindProperty("unitPrefabs");
                    arr.arraySize = b.units.Length;
                    for (int i = 0; i < b.units.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = units[b.units[i]].GetComponent<SelectableUnit>();
                    so.FindProperty("gate").objectReferenceValue = null;
                    so.FindProperty("padEdge").objectReferenceValue = null;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                if (ctrl != null && ctrl.animationClips.Any(c => c.name == "Open")) root.AddComponent<Gate>();
            }

            var dock = Part(inst.transform, "RF_DockPoint") ?? Part(inst.transform, "ER_DockPoint");
            if (root.TryGetComponent(out Refinery _))
            {
                Vector3 at = dock != null ? dock.position : root.transform.TransformPoint(new Vector3(0f, 0f, bounds.max.z + 4f));
                if (b.name == "CommandSpire" && dock == null) at = root.transform.TransformPoint(new Vector3(0f, 0f, bounds.min.z - 6f));
                var d = new GameObject("Refinery_DockPoint").transform;
                d.SetParent(inst.transform, true);
                d.position = new Vector3(at.x, 0f, at.z);
                if (b.name == "Refinery" && obs != null)
                {
                    Vector3 local = root.transform.InverseTransformPoint(d.position);
                    var ob = bounds;
                    if (Mathf.Abs(local.z) >= Mathf.Abs(local.x))
                    {
                        float cut = local.z > 0 ? Mathf.Min(ob.max.z, local.z - 3f) : Mathf.Max(ob.min.z, local.z + 3f);
                        float lo = local.z > 0 ? ob.min.z : cut, hi = local.z > 0 ? cut : ob.max.z;
                        obs.center = new Vector3(ob.center.x, ob.center.y, (lo + hi) * 0.5f);
                        obs.size = new Vector3(ob.size.x, ob.size.y, Mathf.Max(1f, hi - lo));
                    }
                }
            }
            if (root.TryGetComponent(out FreeHarvester fh)) Set(fh, "harvesterPrefab", units["Collector"].GetComponent<SelectableUnit>());
            if (root.TryGetComponent(out DroneHangar hangar))
            {
                hangar.dronePrefab = units["PatrolDrone"].GetComponent<SelectableUnit>();
                var launch = Part(inst.transform, "DB_LaunchPoint");
                var padT = new GameObject("Hangar_Pad").transform;
                padT.SetParent(inst.transform, false);
                padT.position = launch != null ? launch.position : root.transform.position + Vector3.up * bounds.max.y;
            }
            if (root.TryGetComponent(out Turret turret))
            {
                SetColor(turret, "color", b.name == "FlakBattery" ? Amber : Red);
                var head = Part(inst.transform, "ST_Head") ?? Part(inst.transform, "FB_Head") ?? Part(inst.transform, "RB_Yaw") ?? Part(inst.transform, "AT_Head");
                if (head != null) { var aim = root.TryGetComponent(out AimPivot had) ? had : root.AddComponent<AimPivot>(); aim.pivotName = head.name; aim.turnSpeed = b.name == "RailBattery" ? 70f : 200f; }
                foreach (var rk in root.GetComponentsInChildren<RecoilKick>(true)) if (head != null) rk.target = head; else Object.DestroyImmediate(rk);
            }
            else foreach (var rk in root.GetComponentsInChildren<RecoilKick>(true)) Object.DestroyImmediate(rk);
            if (root.TryGetComponent(out BuildRadius radius)) { var so = new SerializedObject(radius); var p = so.FindProperty("radius"); if (p.floatValue < 30f) { p.floatValue = 30f; so.ApplyModifiedPropertiesWithoutUndo(); } }
            if (root.TryGetComponent(out SwarmSilo lance)) { lance.orbital = true; EditorUtility.SetDirty(lance); }
            WreckFor(root, b.name, BuildingScale, 45f);
            if (root.TryGetComponent(out CloakField cloak)) { Object.DestroyImmediate(cloak); root.AddComponent<ShieldField>(); ShieldMaterial(); }
            if (root.TryGetComponent(out Structure st))
            {
                Set(st, "data", data);
                var so = new SerializedObject(st);
                if (so.FindProperty("fallbackName") is SerializedProperty fb) { fb.stringValue = b.display; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            BuildingExtras(root, b, data, inst);
            PrefabUtility.SaveAsPrefabAsset(root, $"{BuildingDir}/Aegis_{b.name}.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void Factions(Dictionary<string, BuildingData> buildings, Dictionary<string, UnitData> units, Dictionary<string, GameObject> unitPrefabs)
    {
        var hive = Faction("Hive");
        hive.displayName = "The Hive";
        hive.id = FactionId.Hive;
        hive.tagline = "Rogue machine swarm. Cheap, fast and many.";
        hive.color = new Color(0f, 1f, 0.9f);
        hive.order = 0;
        hive.emblem = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/CyberpunkRTS/UI/Resources/HoloUI/Hive_Emblem.png") ?? hive.emblem;
        hive.hq = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Buildings/CoreNexus.prefab");
        hive.factory = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Buildings/AssemblyHive.prefab");
        hive.harvester = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Units/DataHarvester.prefab");
        if (hive.buildings == null || hive.buildings.Length == 0)
            hive.buildings = AssetDatabase.FindAssets("t:BuildingData", new[] { "Assets/Data/Buildings" })
                .Select(g => AssetDatabase.LoadAssetAtPath<BuildingData>(AssetDatabase.GUIDToAssetPath(g))).Where(d => d.prefab != null).ToArray();
        hive.twins = new global::Faction.Twin[0];
        EditorUtility.SetDirty(hive);

        var aegis = Faction("Aegis");
        aegis.displayName = "Aegis Corp";
        aegis.id = FactionId.Aegis;
        aegis.tagline = "Corporate security army. Tough, disciplined and well paid.";
        aegis.color = Red;
        aegis.order = 1;
        aegis.emblem = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/CyberpunkRTS/UI/Faction/Aegis_Emblem_256.png");
        aegis.hq = AssetDatabase.LoadAssetAtPath<GameObject>($"{BuildingDir}/Aegis_CommandSpire.prefab");
        aegis.factory = AssetDatabase.LoadAssetAtPath<GameObject>($"{BuildingDir}/Aegis_BarracksBay.prefab");
        aegis.harvester = unitPrefabs["Collector"];
        aegis.buildings = buildings.Values.ToArray();
        var twins = new List<global::Faction.Twin>();
        foreach (var u in Units) twins.Add(new global::Faction.Twin { hive = Twin<UnitData>(u.twin), own = units[u.name] });
        // every Hive unit has its own Aegis twin in Units
        foreach (var kv in buildings) if (TwinOf(kv.Value) is BuildingData h) twins.Add(new global::Faction.Twin { hive = h, own = kv.Value });
        twins.Add(new global::Faction.Twin { hive = hive.hq, own = aegis.hq });
        twins.Add(new global::Faction.Twin { hive = hive.factory, own = aegis.factory });
        twins.Add(new global::Faction.Twin { hive = hive.harvester, own = aegis.harvester });
        aegis.twins = twins.Where(t => t.hive != null && t.own != null).ToArray();
        EditorUtility.SetDirty(aegis);
    }

    static Faction Faction(string name)
    {
        string path = $"{FactionDir}/{name}.asset";
        var f = AssetDatabase.LoadAssetAtPath<Faction>(path);
        if (f != null) return f;
        f = ScriptableObject.CreateInstance<Faction>();
        AssetDatabase.CreateAsset(f, path);
        return f;
    }
}
