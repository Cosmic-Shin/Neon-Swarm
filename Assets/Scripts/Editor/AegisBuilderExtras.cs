using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

public static partial class AegisBuilder
{
    const string FxDir = "Assets/Prefabs/Aegis/FX";
    const string NeutralDir = "Assets/Prefabs/Aegis/Neutral";
    const string PropDir = "Assets/Prefabs/Aegis/Props";
    const string ResearchDir = "Assets/Data/Research/Aegis";
    const string KitDir = "Assets/Resources/Construction/Aegis";

    static Dictionary<string, GameObject> fx = new();
    static Dictionary<string, ResearchData[]> research = new();
    static readonly List<string> balance = new();

    static readonly Dictionary<string, string> Muzzles = new()
    {
        { "MedicDrone", "MED_BeamPoint" }, { "Bastion", "BAS_Muzzle" }, { "Paladin", "PAL_Muzzle" }, { "Interceptor", "INT_Muzzle" },
        { "Gunship", "GUN_Muzzle" }, { "Carrier", "CAR_DropPoint" }, { "Colossus", "COL_LanceMuzzle" }, { "Technician", "TEC_ToolPoint" },
        { "AATrooper", "AAT_Muzzle" }, { "StrikeBike", "BIKE_Muzzle" }, { "Marksman", "MRK_Muzzle" }, { "Suppressor", "SUP_PulsePoint" },
        { "Watcher", "WAT_Muzzle" }, { "PatrolDrone", "PD_Muzzle" }, { "Dominion", "DOM_Muzzle" },
    };

    static readonly Dictionary<string, string> Shots = new()
    {
        { "Peacekeeper", "P_AegisTracer" }, { "AATrooper", "P_AegisFlak" }, { "Marksman", "P_AegisSniper" }, { "Watcher", "P_AegisTracer" },
        { "StrikeBike", "P_AegisTracer" }, { "Suppressor", "P_AegisEMP" }, { "Lancer", "P_AegisRailSlug" }, { "Bastion", "P_AegisShell" },
        { "Paladin", "P_AegisBolt" }, { "Colossus", "P_AegisRailSlug" }, { "Interceptor", "P_AegisMissile" }, { "Gunship", "P_AegisTracer" },
        { "Dominion", "P_AegisMissile" }, { "PatrolDrone", "P_AegisBolt" },
    };

    static readonly Dictionary<string, string> ResearchNames = new()
    {
        { "A4_NetrunnerProtocols", "Suppression Protocols" }, { "X1_BroodChamber", "Field Command Uplink" },
        { "X3_HiveMindUplink", "Orbital Uplink" }, { "E2_OverclockedDrills", "Hardened Drill Heads" },
    };

    static void MoveOldData()
    {
        foreach (var (from, to) in new[] { ("Assets/Data/Aegis/Units", UnitDataDir), ("Assets/Data/Aegis/Buildings", BuildingDataDir) })
            if (AssetDatabase.IsValidFolder(from) && !AssetDatabase.IsValidFolder(to))
            {
                string err = AssetDatabase.MoveAsset(from, to);
                if (!string.IsNullOrEmpty(err)) Debug.LogWarning("[GD5] move " + from + ": " + err);
            }
        if (AssetDatabase.IsValidFolder("Assets/Data/Aegis") && AssetDatabase.FindAssets("", new[] { "Assets/Data/Aegis" }).Length == 0)
            AssetDatabase.DeleteAsset("Assets/Data/Aegis");
    }

    static GameObject PartPrefab(string name, string fbxPath, string template, string dir, params string[] keep)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (src == null) { Debug.LogWarning("[GD5] missing " + fbxPath); return null; }
        GameObject root = template != null ? PrefabUtility.LoadPrefabContents($"Assets/Prefabs/FX/{template}.prefab") : new GameObject(name);
        try
        {
            if (template != null) for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
            inst.transform.localRotation = src.transform.localRotation;
            inst.transform.localScale = Vector3.one;
            if (keep.Length > 0)
            {
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                for (int i = inst.transform.childCount - 1; i >= 0; i--)
                {
                    var c = inst.transform.GetChild(i);
                    if (!keep.Any(k => c.name.StartsWith(k))) Object.DestroyImmediate(c.gameObject);
                }
            }
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            root.name = name;
            Folder(dir);
            return PrefabUtility.SaveAsPrefabAsset(root, $"{dir}/{name}.prefab");
        }
        finally
        {
            if (template != null) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root);
        }
    }

    static Dictionary<string, GameObject> MakeFx()
    {
        string P(string n) => $"{Models}/Projectiles/{n}.fbx";
        string F(string n) => $"{Models}/FX/{n}.fbx";
        var d = new Dictionary<string, GameObject>
        {
            ["P_AegisTracer"] = PartPrefab("P_AegisTracer", P("Proj_AegisTracer"), "P_SMGTracer", FxDir),
            ["P_AegisRailSlug"] = PartPrefab("P_AegisRailSlug", P("Proj_AegisRailSlug"), "P_RailSpike", FxDir),
            ["P_AegisShell"] = PartPrefab("P_AegisShell", P("Proj_AegisShell"), "P_PlasmaShell", FxDir),
            ["P_AegisMissile"] = PartPrefab("P_AegisMissile", P("Proj_AegisMissile"), "P_SMGTracer", FxDir),
            ["P_AegisFlak"] = PartPrefab("P_AegisFlak", P("Proj_AegisFlak"), "P_FlakShell", FxDir, "AFlak_Body", "AFlak_Nose", "AFlak_Shell", "AFlak_Trail"),
            ["P_AegisSniper"] = PartPrefab("P_AegisSniper", P("Proj_AegisSniper"), "P_SMGTracer", FxDir),
            ["P_AegisEMP"] = PartPrefab("P_AegisEMP", P("Proj_AegisEMP"), "P_EMPPulse", FxDir),
            ["P_AegisBolt"] = PartPrefab("P_AegisBolt", P("Proj_AegisBolt"), "P_SMGTracer", FxDir),
            ["Flash_Aegis"] = PartPrefab("Flash_Aegis", F("FX_AegisMuzzleFlashes"), "Flash_Rifle", FxDir, "AFlash_Cone"),
            ["Flash_AegisBike"] = PartPrefab("Flash_AegisBike", F("FX_AegisBikeFlash"), "Flash_Rifle", FxDir),
            ["AegisDmg_Spark"] = PartPrefab("AegisDmg_Spark", F("FX_AegisDamage"), "Flash_Rifle", FxDir, "ADmg_Spark"),
            ["AegisRepair_Sparks"] = PartPrefab("AegisRepair_Sparks", F("FX_AegisRepair"), "Flash_Rifle", FxDir, "ARep_Spark", "ARep_Ring"),
            ["Lance_Beam"] = PartPrefab("Lance_Beam", F("FX_AegisBeams"), null, FxDir, "ABeam_OrbCore", "ABeam_OrbGlow", "ABeam_Orbital"),
            ["Lance_Impact"] = PartPrefab("Lance_Impact", F("FX_AegisBeams"), null, FxDir, "ABeam_OrbImpact"),
            ["Shield_Dome"] = PartPrefab("Shield_Dome", F("FX_AegisShield"), null, FxDir, "AShield_Dome"),
        };
        return d;
    }

    static AnimatorController FreshController(string path)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (ctrl == null) return AnimatorController.CreateAnimatorControllerAtPath(path);
        foreach (var p in ctrl.parameters) ctrl.RemoveParameter(p);
        var sm = ctrl.layers[0].stateMachine;
        foreach (var t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
        foreach (var s in sm.states) sm.RemoveState(s.state);
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    static void ReimportPrefabs()
    {
        foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Aegis", "Assets/Prefabs/Net" }))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceUpdate);
    }

    static RuntimeAnimatorController ClipController(string fbxPath, string outName)
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
        if (!clips.TryGetValue("Idle", out var idle)) return null;
        string path = $"{AnimDir}/{outName}.controller";
        var ctrl = FreshController(path);
        var st = ctrl.layers[0].stateMachine.AddState("Idle");
        st.motion = idle;
        ctrl.layers[0].stateMachine.defaultState = st;
        return ctrl;
    }

    static void ConstructionKit()
    {
        Folder(KitDir);
        Folder(AnimDir);
        foreach (var n in new[] { "Build_AegisPad", "Build_AegisPadLarge", "Build_AegisScaffold", "Build_AegisScaffoldLarge", "Build_AegisScanFrame", "Build_AegisWelder", "Build_AegisCrane" })
        {
            string fbx = $"{Models}/Construction/{n}.fbx";
            var go = PartPrefab(n, fbx, null, KitDir);
            if (go == null || n != "Build_AegisCrane") continue;
            var ctrl = ClipController(fbx, n);
            if (ctrl == null) continue;
            var root = PrefabUtility.LoadPrefabContents($"{KitDir}/{n}.prefab");
            var model = root.transform.GetChild(0).gameObject;
            var anim = model.TryGetComponent(out Animator a) ? a : model.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            PrefabUtility.SaveAsPrefabAsset(root, $"{KitDir}/{n}.prefab");
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // per-unit tuning from equal-squad duels vs the Hive twins (2026-11-26)
    static readonly Dictionary<string, System.Action<UnitData>> Tuning = new()
    {
        ["Lancer"] = d => d.maxHealth = 760f,        // 3 vs 3 Crushers: 2 alive at 920, still 2 alive at 820
        ["Paladin"] = d => d.maxHealth = 320f,       // Bulwark parity: the shield is its edge (won 8 then 5 of 12 left with more HP)
        ["Suppressor"] = d => d.damage = 10.5f,      // stun-splash is swingy: lost at 10, won 5-of-6 at 11
        ["Watcher"] = d => d.maxHealth = 75f,
        ["Interceptor"] = d => d.damage = 11f,       // attack clip (0.46 s) is longer than its 0.4 s cooldown
        ["StrikeBike"] = d => d.maxHealth = 160f,    // 12 vs 12 Striders: 6 left
    };

    static void Balance(U u, UnitData d)
    {
        float hp = d.maxHealth, speed = d.moveSpeed;
        int cost = d.cost;
        d.maxHealth = Mathf.Round(d.maxHealth * 1.15f / 5f) * 5f;
        d.cost = Mathf.RoundToInt(d.cost * 1.05f / 25f) * 25;   // was x1.15: with +15% HP that left Aegis ~13% worse value per credit (and 10% slower)
        if (d.moveSpeed > 0f) d.moveSpeed *= 0.9f;
        if (Muzzles.TryGetValue(u.name, out var m)) d.muzzles = m;
        if (Tuning.TryGetValue(u.name, out var tune)) tune(d);
        balance.Add($"| {u.display} | {u.twin} | {d.role} | {hp:0} -> {d.maxHealth:0} | {cost} -> {d.cost} | {(speed > 0f ? $"{speed:0.#} -> {d.moveSpeed:0.#}" : "agent x0.9")} | {d.damage:0.#} / {d.range:0.#} m |");
    }

    static void Visuals(U u, UnitData d)
    {
        if (!Shots.TryGetValue(u.name, out var shot) || !fx.TryGetValue(shot, out var proj) || proj == null) return;
        d.visual ??= new WeaponVisual();
        d.visual.projectile = proj;
        d.visual.hideBeam = true;
        d.visual.muzzleFlash = u.name == "StrikeBike" ? fx["Flash_AegisBike"] : fx["Flash_Aegis"];
    }

    static RuntimeAnimatorController CombatController(string aegis)
    {
        string fbx = $"{Models}/Units/Aegis_{aegis}.fbx";
        var clips = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
        AnimationClip Get(string n) => clips.TryGetValue(n, out var c) ? c : null;
        string path = $"{AnimDir}/Aegis_{aegis}.controller";
        var ctrl = FreshController(path);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle"); idle.motion = Get("Idle"); sm.defaultState = idle;
        var walk = sm.AddState("Walk"); walk.motion = Get("Walk");
        var toWalk = idle.AddTransition(walk); toWalk.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed"); toWalk.hasExitTime = false; toWalk.duration = 0.15f;
        var toIdle = walk.AddTransition(idle); toIdle.AddCondition(AnimatorConditionMode.Less, 0.3f, "Speed"); toIdle.hasExitTime = false; toIdle.duration = 0.15f;
        var attack = sm.AddState("Attack"); attack.motion = Get("Attack");
        var toAttack = sm.AddAnyStateTransition(attack); toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack"); toAttack.duration = 0.05f; toAttack.canTransitionToSelf = true;
        var back = attack.AddTransition(idle); back.hasExitTime = true; back.exitTime = 1f; back.duration = 0.1f;
        var death = sm.AddState("Death"); death.motion = Get("Death");
        var toDeath = sm.AddAnyStateTransition(death); toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Die"); toDeath.duration = 0.05f; toDeath.canTransitionToSelf = false;
        foreach (var n in new[] { "Deploy", "Undeploy", "Unload" })
        {
            if (Get(n) == null && !(aegis == "Bastion" && n != "Unload")) continue;
            ctrl.AddParameter(n, AnimatorControllerParameterType.Trigger);
            var s = sm.AddState(n); s.motion = Get(n);
            var t = sm.AddAnyStateTransition(s); t.AddCondition(AnimatorConditionMode.If, 0f, n); t.duration = 0.05f; t.canTransitionToSelf = false;
            var r = s.AddTransition(idle); r.hasExitTime = true; r.exitTime = 1f; r.duration = 0.1f;
        }
        return ctrl;
    }

    static void UnitExtras(GameObject root, U u, GameObject inst, Animator anim)
    {
        if (root.TryGetComponent(out NavMeshAgent agent)) agent.speed *= 0.9f;
        if (!root.TryGetComponent(out Harvester _)) anim.runtimeAnimatorController = CombatController(u.name);
        // Attack clip with a "Fire" event -> Attacker waits for it (AttackEvents receives the event on the Animator's object)
        bool hasAttack = anim.runtimeAnimatorController != null && System.Array.Exists(anim.runtimeAnimatorController.animationClips, c => c != null && c.name == "Attack");
        var events = anim.GetComponent<AttackEvents>();
        if (hasAttack && events == null) anim.gameObject.AddComponent<AttackEvents>();
        else if (!hasAttack && events != null) Object.DestroyImmediate(events);
        if (root.TryGetComponent(out UnitDeath death))
        {
            death.animator = anim;
            death.deathTrigger = "Die";
            death.deathClipLength = 40f / 24f;
            if (death.wreckPrefab != null && !root.TryGetComponent(out Harvester _)) death.mode = UnitDeath.DeathMode.AnimateThenWreck;
        }
        if (root.TryGetComponent(out Repairer rep))
        {
            if (u.name == "Technician")
            {
                rep.structures = true;
                rep.roles = new[] { UnitData.Role.Heavy };
                rep.beamFrom = "TEC_ToolPoint";
                rep.beamColor = Amber;
                rep.sparkFx = fx.TryGetValue("AegisRepair_Sparks", out var s) ? s : null;
            }
            else
            {
                rep.structures = false;
                rep.roles = new[] { UnitData.Role.Infantry, UnitData.Role.Support, UnitData.Role.Scout };
                rep.beamFrom = "MED_BeamPoint";
            }
        }
        if (u.name == "Skylift" && root.TryGetComponent(out Transport carrier)) carrier.vehicles = true;
        if (u.name == "Bastion" && !root.TryGetComponent(out Deployer _)) root.AddComponent<Deployer>();
        if (u.name == "Paladin")
        {
            var shield = root.TryGetComponent(out ShieldField had) ? had : root.AddComponent<ShieldField>();
            shield.radius = 3.5f;
            shield.reduction = 0.15f;  // 0.3: Paladins won with 8 of 12 left vs equal Bulwarks, 0.2: 5 of 12
            shield.needsPower = false;
            shield.showRing = false;
            shield.domePart = "PAL_ShieldDome";
        }
    }

    static void BuildingExtras(GameObject root, B b, BuildingData data, GameObject inst)
    {
        bool wall = b.name is "PulseWall" or "PulseGate" or "PulsePost" or "MineField";
        bool mk2 = Part(inst.transform, $"Aegis_{b.name}_Upgrade_Mk2") != null;
        bool mk3 = Part(inst.transform, $"Aegis_{b.name}_Upgrade_Mk3") != null;
        if (!wall && mk2)
        {
            var up = root.TryGetComponent(out BuildingUpgrade had) ? had : root.AddComponent<BuildingUpgrade>();
            up.maxLevel = mk3 ? 3 : 2;
            int baseCost = data != null ? data.cost : 2000;
            if (!root.TryGetComponent(out UpgradeEffects effects))
            {
                effects = root.AddComponent<UpgradeEffects>();
                up.upgradeCost = new[] { 0, 0, Mathf.RoundToInt(baseCost * 0.6f / 50f) * 50, Mathf.RoundToInt(baseCost * 1.2f / 50f) * 50 };
                var who = data != null ? new[] { data } : new BuildingData[0];
                var levels = new List<UpgradeEffects.Level>
                {
                    new() { level = 2, displayName = "Mk II Reinforcement", description = "+25% health.", mods = new[] { new StatMod { stat = Stat.MaxHealth, percent = 0.25f, buildings = who } } },
                };
                if (mk3) levels.Add(new() { level = 3, displayName = "Mk III Reinforcement", description = "+25% more health.", mods = new[] { new StatMod { stat = Stat.MaxHealth, percent = 0.25f, buildings = who } } });
                effects.levels = levels.ToArray();
            }
            else if (!mk3) effects.levels = effects.levels.Where(l => l.level <= 2).ToArray();
        }
        else if (root.TryGetComponent(out BuildingUpgrade stray) && !mk2)
        {
            if (root.TryGetComponent(out UpgradeEffects e)) Object.DestroyImmediate(e);
            Object.DestroyImmediate(stray);
        }

        if (research.TryGetValue(b.name, out var list))
        {
            var building = root.TryGetComponent(out Building had) ? had : root.AddComponent<Building>();
            var so = new SerializedObject(building);
            var ups = so.FindProperty("upgrades");
            ups.arraySize = list.Length;
            for (int i = 0; i < list.Length; i++) ups.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            if (so.FindProperty("spawnPoint").objectReferenceValue == null) so.FindProperty("spawnPoint").objectReferenceValue = root.transform;
            var units = so.FindProperty("unitPrefabs");
            if (units.arraySize > 0 && b.units == null) units.arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        if (b.name == "DroneBay" && !root.TryGetComponent(out Gate _)) root.AddComponent<Gate>();
        if (b.name == "PulseGate")
        {
            if (!root.TryGetComponent(out UnitGate _)) root.AddComponent<UnitGate>();
            if (root.TryGetComponent(out NavMeshObstacle obstacle)) obstacle.carving = false;
        }
        if (root.TryGetComponent(out SwarmSilo lance))
        {
            lance.beamPrefab = fx.TryGetValue("Lance_Beam", out var beam) ? beam : null;
            lance.impactPrefab = fx.TryGetValue("Lance_Impact", out var hit) ? hit : null;
            var aim = root.TryGetComponent(out AimPivot ap) ? ap : root.AddComponent<AimPivot>();
            aim.pivotName = "OL_Yaw";
            aim.turnSpeed = 35f;
            aim.idleSweep = 0f;
        }
        if (root.TryGetComponent(out ShieldField field))
        {
            field.buildingsOnly = true;
            field.domePrefab = fx.TryGetValue("Shield_Dome", out var dome) ? dome : null;
        }
        if (root.TryGetComponent(out Turret turret))
        {
            string shot = b.name == "FlakBattery" ? "P_AegisFlak" : b.name == "RailBattery" ? "P_AegisRailSlug" : "P_AegisTracer";
            var so = new SerializedObject(turret);
            so.FindProperty("visual.projectile").objectReferenceValue = fx[shot];
            so.FindProperty("visual.muzzleFlash").objectReferenceValue = fx["Flash_Aegis"];
            so.FindProperty("visual.hideBeam").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        if (root.TryGetComponent(out DamageFx dmg) && fx.TryGetValue("AegisDmg_Spark", out var spark) && spark != null) Set(dmg, "spark", spark);
    }

    static Dictionary<UnitData, UnitData> UnitTwins(Dictionary<string, UnitData> units)
    {
        var map = new Dictionary<UnitData, UnitData>();
        foreach (var u in Units) if (Twin<UnitData>(u.twin) is UnitData h && !map.ContainsKey(h)) map[h] = units[u.name];
        foreach (var (h, a) in new[] { ("Hackling", "Peacekeeper"), ("Ratline", "StrikeBike"), ("WireWeaver", "Suppressor"), ("Chromejaw", "Paladin"), ("Overclock", "Colossus"), ("NetrunnerHive", "Colossus"),
            ("FireflySpotter", "Interceptor"), ("LocustDrone", "Gunship"), ("BeetleBomber", "Gunship"), ("LocustCarrier", "Carrier"), ("JellyfishDreadnought", "Dominion") })
            if (Twin<UnitData>(h) is UnitData hd && !map.ContainsKey(hd)) map[hd] = units[a];
        return map;
    }

    static Dictionary<string, ResearchData[]> MakeResearch(Dictionary<string, UnitData> units, Dictionary<string, BuildingData> buildings)
    {
        Folder(ResearchDir);
        var unitMap = UnitTwins(units);
        var made = new Dictionary<ResearchData, ResearchData>();
        var hive = AssetDatabase.FindAssets("t:ResearchData", new[] { "Assets/Data/Research" })
            .Select(g => AssetDatabase.GUIDToAssetPath(g)).Where(p => !p.Contains("/Aegis/"))
            .Select(p => AssetDatabase.LoadAssetAtPath<ResearchData>(p)).Where(r => r != null).ToList();
        foreach (var h in hive)
        {
            string path = $"{ResearchDir}/AG_{h.name}.asset";
            var r = Object.Instantiate(h);
            r.name = "AG_" + h.name;
            r.id = "AG_" + h.id;
            r.faction = FactionId.Aegis;
            if (ResearchNames.TryGetValue(h.name, out var n)) r.displayName = n;
            if (!string.IsNullOrEmpty(r.description)) r.description = r.description.Replace("Core Nexus", "Command Spire").Replace("Hive", "Aegis");
            if (r.effects != null)
                foreach (var e in r.effects)
                {
                    if (e.units != null) e.units = e.units.Where(x => x != null && unitMap.ContainsKey(x)).Select(x => unitMap[x]).Distinct().ToArray();
                    if (e.buildings != null) e.buildings = e.buildings.Select(x => buildings.Values.FirstOrDefault(a => TwinOf(a) == x)).Where(x => x != null).ToArray();
                }
            var existing = AssetDatabase.LoadAssetAtPath<ResearchData>(path);
            if (existing != null) { EditorUtility.CopySerialized(r, existing); existing.name = "AG_" + h.name; Object.DestroyImmediate(r); r = existing; EditorUtility.SetDirty(r); }
            else AssetDatabase.CreateAsset(r, path);
            made[h] = r;
        }
        foreach (var (h, r) in made) { r.requires = h.requires != null && made.TryGetValue(h.requires, out var req) ? req : null; EditorUtility.SetDirty(r); }
        var all = made.Values.ToArray();
        return new Dictionary<string, ResearchData[]>
        {
            ["ResearchLab"] = all.Where(r => r.branch is ResearchData.Branch.Econ or ResearchData.Branch.Assault or ResearchData.Branch.Heavy or ResearchData.Branch.Support).ToArray(),
            ["TechCenter"] = all.Where(r => r.branch is ResearchData.Branch.Defense or ResearchData.Branch.Elite).ToArray(),
        };
    }

    static void AIProfiles()
    {
        var aegis = AssetDatabase.LoadAssetAtPath<Faction>($"{FactionDir}/Aegis.asset");
        foreach (var level in new[] { "Easy", "Normal", "Hard" })
        {
            var src = AssetDatabase.LoadAssetAtPath<AIProfile>($"Assets/Resources/AI/{level}.asset");
            if (src == null || aegis == null) continue;
            var p = AIPlayer.SwapProfile(src, aegis);
            string path = $"Assets/Resources/AI/Aegis_{level}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<AIProfile>(path);
            if (existing != null) { EditorUtility.CopySerialized(p, existing); existing.name = "Aegis_" + level; EditorUtility.SetDirty(existing); Object.DestroyImmediate(p); }
            else { p.name = "Aegis_" + level; AssetDatabase.CreateAsset(p, path); }
        }
    }

    static List<GameObject> NeutralAndProps()
    {
        var made = new List<GameObject>();
        Folder(NeutralDir);
        var template = "Assets/Prefabs/Buildings/DerelictOutpost.prefab";
        foreach (var (name, kind) in new[] { ("Neutral_Corp_CapturePoint", CorpSite.Kind.CapturePoint), ("Neutral_Corp_RepairBay", CorpSite.Kind.RepairBay),
            ("Neutral_Corp_DataVault", CorpSite.Kind.DataVault), ("Neutral_Corp_Outpost", CorpSite.Kind.Outpost) })
        {
            string fbxPath = $"Assets/CyberpunkRTS/Models/Environment/{name}.fbx";
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null) continue;
            var root = PrefabUtility.LoadPrefabContents(template);
            try
            {
                root.name = name;
                var old = LocalBounds(root.transform);
                float foot = Mathf.Max(old.size.x, old.size.z);
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    var c = root.transform.GetChild(i);
                    var src = PrefabUtility.GetCorrespondingObjectFromSource(c.gameObject);
                    if (c.name == "Model" || (src != null && AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(src)) is ModelImporter)) Object.DestroyImmediate(c.gameObject);
                }
                var holder = new GameObject("Model").transform;
                holder.SetParent(root.transform, false);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx, holder);
                inst.transform.localRotation = fbx.transform.localRotation;
                var raw = LocalBounds(holder);
                holder.localScale = Vector3.one * (raw.size.x > 0.01f ? Mathf.Clamp(foot * 0.75f / Mathf.Max(raw.size.x, raw.size.z), 1f, 8f) : BuildingScale);
                var ctrl = ClipController(fbxPath, name);
                if (ctrl != null) { var anim = inst.TryGetComponent(out Animator a) ? a : inst.AddComponent<Animator>(); anim.runtimeAnimatorController = ctrl; }
                var site = root.TryGetComponent(out CorpSite had) ? had : root.AddComponent<CorpSite>();
                site.kind = kind;
                made.Add(PrefabUtility.SaveAsPrefabAsset(root, $"{NeutralDir}/{name}.prefab"));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        Folder(PropDir);
        var props = new List<GameObject>();
        foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { $"{Models}/Props" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var root = new GameObject(fbx.name);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            inst.transform.localRotation = fbx.transform.localRotation;
            inst.transform.localScale = fbx.transform.localScale * BuildingScale;
            var b = LocalBounds(root.transform, false);
            if (!fbx.name.Contains("StreetTile") && !fbx.name.Contains("LandingPad"))
            {
                var box = root.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size;
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.NavigationStatic);
            props.Add(PrefabUtility.SaveAsPrefabAsset(root, $"{PropDir}/{fbx.name}.prefab"));
            Object.DestroyImmediate(root);
        }
        const string palettePath = "Assets/Resources/MapProps.asset";
        var palette = AssetDatabase.LoadAssetAtPath<PrefabList>(palettePath);
        if (palette == null) { palette = ScriptableObject.CreateInstance<PrefabList>(); AssetDatabase.CreateAsset(palette, palettePath); }
        palette.prefabs = props.ToArray();
        EditorUtility.SetDirty(palette);
        return made;
    }

    static void WriteBalance()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!--");
        sb.AppendLine("Aegis placeholder balance (generated by Tools > GD5 > Build Aegis Faction, AegisBuilder.Balance).");
        sb.AppendLine("Each Aegis unit copies the stats of its closest Hive counterpart with the same role, then:");
        sb.AppendLine("HP x1.15, cost x1.15 (rounded to 25), move speed x0.9 (UnitData.moveSpeed when set, otherwise the NavMeshAgent speed).");
        sb.AppendLine("Edit the tables in AegisBuilder.cs and re-run the tool; hand edits to the assets are overwritten.");
        sb.AppendLine("-->");
        sb.AppendLine();
        sb.AppendLine("# Aegis balance");
        sb.AppendLine();
        sb.AppendLine("| Aegis unit | Hive counterpart | Role | HP | Cost | Speed | Damage / range |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var row in balance) sb.AppendLine(row);
        File.WriteAllText($"{UnitDataDir}/AegisBalance.md", sb.ToString());
        AssetDatabase.ImportAsset($"{UnitDataDir}/AegisBalance.md");
    }
}
