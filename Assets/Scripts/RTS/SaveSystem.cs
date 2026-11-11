using System.Collections.Generic;
using System.IO;
using UnityEngine;

// The whole offline game in one of 5 JSON slots (Esc menu / skirmish lobby slot list; F5 quick-saves to the
// last slot, F9 loads the newest): credits, ore, every unit and finished building (ours, AIs', creeps), research,
// upgrades, construction in progress, outposts, veterancy, the skirmish setup and the match clock.
// Loading restarts the scene and lays the save over it: scene objects are matched by where the scene put
// them, things built during play are made again from Resources/PrefabList.
[System.Serializable]
public class SaveData
{
    public int version = 2;
    // Shown in the slot list.
    public long savedAt;                           // UTC ticks
    public float elapsed;                          // seconds of play in this match
    public string summary;                         // "SKIRMISH  ·  vs 2 AI: Hard, Normal"
    // The skirmish lobby setup, so loading rebuilds the same match (AIs, teams, colours, bases, time limit).
    public bool skirmish;
    public int startCredits, minutes, playerSide, playerColor, playerBase;
    public string map; // the map's scene name (empty in old saves = the big map)
    public List<Skirmish.Opponent> opponents = new();
    public List<TeamResearch> teamResearch = new(); // AI research (yours is `research`)
    public int[] matchStats;                       // MatchStats.Values, for the end screen and your career stats

    public List<string> research = new();          // completed research ids
    public List<LabSave> labs = new();             // project running in each lab
    public List<UpgradeSave> upgrades = new();     // building upgrade levels (Mk II / III)
    public List<SiteSave> sites = new();           // construction sites in progress
    public List<OutpostSave> outposts = new();
    public List<VeterancySave> veterans = new();
    public int credits = -1;
    public List<int> aiCredits = new();            // skirmish: AI teams 1..7
    public List<EntitySave> entities = new();      // every unit and finished building, ours and theirs
    public List<OreSave> ores = new();
}

// origin: scene objects ("Chromejaw (2)@45.0,-30.0", where the scene put it); empty = made during play.
[System.Serializable] public class EntitySave { public string prefab; public string origin; public Vector3 position; public Quaternion rotation; public float health; public int team; }
[System.Serializable] public class OreSave { public string ore; public float amount; }

[System.Serializable] public class LabSave { public string lab; public string research; public float timeLeft; }
[System.Serializable] public class UpgradeSave { public string building; public int level; }
[System.Serializable] public class SiteSave { public string prefab; public Vector3 position; public Quaternion rotation; public float progress; public int team; }
[System.Serializable] public class TeamResearch { public int team; public List<string> ids = new(); }
[System.Serializable] public class OutpostSave { public string outpost; public int owner; public int capturing; public float progress; public float holdTimeLeft; public List<string> garrison = new(); }
[System.Serializable] public class VeterancySave { public string unit; public int kills; }

// Runs SaveSystem.Apply over a few frames after a load restarts the scene, then goes away.
public class SaveLoader : MonoBehaviour
{
    public void Run(SaveData d) => StartCoroutine(Go(d));
    System.Collections.IEnumerator Go(SaveData d) { yield return SaveSystem.Apply(d); Destroy(gameObject); }
}

public static class SaveSystem
{
    // ---- slots: save_1.json .. save_5.json ----
    public const int Slots = 5;

    public static string SlotPath(int slot) => Path.Combine(Application.persistentDataPath, $"save_{slot}.json");
    public static bool HasSave(int slot) => File.Exists(SlotPath(slot));
    public static bool HasAnySave { get { for (int s = 1; s <= Slots; s++) if (HasSave(s)) return true; return false; } }

    // The slot F5 saves to: the last one used (slot 1 at first).
    public static int LastSlot
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt("save.lastSlot", 1), 1, Slots);
        set { PlayerPrefs.SetInt("save.lastSlot", value); PlayerPrefs.Save(); }
    }

    // The newest save (F9), 0 = none.
    public static int NewestSlot
    {
        get
        {
            int best = 0; System.DateTime bestTime = System.DateTime.MinValue;
            for (int s = 1; s <= Slots; s++)
                if (HasSave(s) && File.GetLastWriteTimeUtc(SlotPath(s)) > bestTime) { best = s; bestTime = File.GetLastWriteTimeUtc(SlotPath(s)); }
            return best;
        }
    }

    public static void Save(int slot)
    {
        File.WriteAllText(SlotPath(slot), ToJson());
        LastSlot = slot;
    }

    // What the slot list shows (the whole save; they're small). Null = empty or unreadable.
    public static SaveData Info(int slot)
    {
        if (!HasSave(slot)) return null;
        try { return JsonUtility.FromJson<SaveData>(File.ReadAllText(SlotPath(slot))); }
        catch (System.Exception e) { Debug.LogWarning($"Save slot {slot}: {e.Message}"); return null; }
    }

    public static void Delete(int slot) { if (HasSave(slot)) File.Delete(SlotPath(slot)); }

    // Load a whole game, from the map or the main menu: the skirmish setup goes back first so the map
    // starts as the same match, then the save is laid over it (Hook -> SaveLoader -> Apply).
    static SaveData pending;

    public static bool LoadGame(int slot)
    {
        var d = Info(slot);
        if (d == null) return false;
        Skirmish.Active = d.skirmish;
        Skirmish.Opponents.Clear();
        if (d.skirmish)
        {
            Skirmish.Opponents.AddRange(d.opponents);
            Skirmish.StartCredits = d.startCredits;
            Skirmish.Minutes = d.minutes;
            Skirmish.PlayerSide = d.playerSide;
            Skirmish.PlayerColor = d.playerColor;
            Skirmish.PlayerBase = d.playerBase;
        }
        Skirmish.ElapsedBefore = d.elapsed;
        LastSlot = slot;
        pending = d;
        var map = System.Array.Find(GameMaps.All, m => m.scene == d.map) ?? GameMaps.All[0];
        UnityEngine.SceneManagement.SceneManager.LoadScene(map.scene);
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Hook()
    {
        // The old single save becomes slot 1.
        string old = Path.Combine(Application.persistentDataPath, "save.json");
        if (File.Exists(old) && !File.Exists(SlotPath(1))) File.Move(old, SlotPath(1));
        pending = null;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) =>
        {
            if (pending == null) return;
            new GameObject("LoadingSave").AddComponent<SaveLoader>().Run(pending);
            pending = null;
        };
    }

    // Frame 1 (everything has started): units and buildings, credits, ore.
    // Frame 2 (replaced objects are gone): research, upgrades, sites, outposts, veterancy, then health.
    public static System.Collections.IEnumerator Apply(SaveData d)
    {
        yield return null;
        RestoreWorld(d);
        yield return null;
        Restore(d);
        yield return null; // max health refreshed by research / upgrades / ranks
        foreach (var (h, f) in healthToSet) if (h != null) h.SetFraction(f);
        healthToSet.Clear();
        Alerts.Show("Game loaded", Alerts.Good);
    }

    static readonly List<(Health, float)> healthToSet = new();

    static bool IsEntity(Health h) =>
        (h.TryGetComponent(out SelectableUnit _) || h.TryGetComponent(out Structure _) || h.TryGetComponent(out Barrier _)) && !h.TryGetComponent(out OutpostCapture _);

    static string Origin(Health h)
    {
        if (h.name.Contains("(Clone)")) return "";
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return $"{h.name}@{h.SpawnPos.x.ToString("0.0", inv)},{h.SpawnPos.z.ToString("0.0", inv)}";
    }

    static string OreKey(OreNode o)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return $"{o.name}@{o.transform.position.x.ToString("0.0", inv)},{o.transform.position.z.ToString("0.0", inv)}";
    }

    static void CaptureWorld(SaveData d)
    {
        if (ResourceBank.Instance != null) d.credits = ResourceBank.Instance.Credits;
        for (int t = 1; t <= 7; t++) d.aiCredits.Add(TeamEconomy.Credits(t));
        foreach (var h in Object.FindObjectsByType<Health>(FindObjectsInactive.Include, FindObjectsSortMode.None)) // garrisoned units are inactive
        {
            if (!IsEntity(h) || h.IsDead || h.TryGetComponent(out Construction _)) continue; // sites are saved on their own
            if (h.transform.position.y < -10f) continue; // ponytail: riders inside a Wasp aren't saved; save the Wasp's cargo if it matters
            d.entities.Add(new EntitySave
            {
                prefab = PrefabName(h), origin = Origin(h), position = h.transform.position,
                rotation = h.transform.rotation, health = h.Fraction, team = h.team,
            });
        }
        foreach (var o in OreNode.All) d.ores.Add(new OreSave { ore = OreKey(o), amount = o.amount });
    }

    static void RestoreWorld(SaveData d)
    {
        var bank = ResourceBank.GetOrCreate();
        if (d.credits >= 0 && d.credits != bank.Credits)
        {
            if (d.credits > bank.Credits) bank.Add(d.credits - bank.Credits); else bank.Spend(bank.Credits - d.credits);
        }
        for (int i = 0; i < d.aiCredits.Count; i++) TeamEconomy.Set(i + 1, d.aiCredits[i]);

        // Scene objects still alive in the save are moved to where they were; the rest are gone.
        var byOrigin = new Dictionary<string, EntitySave>();
        foreach (var e in d.entities) if (!string.IsNullOrEmpty(e.origin)) byOrigin[e.origin] = e;
        foreach (var h in Object.FindObjectsByType<Health>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!IsEntity(h)) continue;
            if (byOrigin.TryGetValue(Origin(h), out var e)) { Place(h, e); byOrigin.Remove(Origin(h)); }
            else Object.Destroy(h.gameObject);
        }

        // Things made during play are made again.
        foreach (var e in d.entities)
        {
            if (!string.IsNullOrEmpty(e.origin)) continue;
            var prefab = PrefabList.Find(e.prefab);
            if (prefab == null) { Debug.LogWarning($"Save: no prefab called {e.prefab}"); continue; }
            Place(Object.Instantiate(prefab, e.position, e.rotation).GetComponent<Health>(), e);
        }

        var ore = new Dictionary<string, float>();
        foreach (var o in d.ores) ore[o.ore] = o.amount;
        foreach (var o in OreNode.All) if (ore.TryGetValue(OreKey(o), out float a)) o.SetFromNet(a);
    }

    static void Place(Health h, EntitySave e)
    {
        if (h.TryGetComponent(out SelectableUnit u)) Attacker.SetTeam(u, e.team);
        else h.team = e.team;
        if (h.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent) && agent.enabled && agent.isOnNavMesh) agent.Warp(e.position);
        h.transform.position = e.position; // flyers move themselves, not through the agent
        h.transform.rotation = e.rotation;
        healthToSet.Add((h, e.health));
    }

    public static string ToJson() => JsonUtility.ToJson(Capture(), true);

    // ---- what goes in ----
    public static SaveData Capture()
    {
        var d = new SaveData { savedAt = System.DateTime.UtcNow.Ticks, elapsed = Skirmish.Elapsed, skirmish = Skirmish.Active, map = GameMaps.Current.scene, matchStats = (int[])MatchStats.Values.Clone() };
        if (d.skirmish)
        {
            d.opponents.AddRange(Skirmish.Opponents);
            d.startCredits = Skirmish.StartCredits;
            d.minutes = Skirmish.Minutes;
            d.playerSide = Skirmish.PlayerSide;
            d.playerColor = Skirmish.PlayerColor;
            d.playerBase = Skirmish.PlayerBase;
            var enemies = Skirmish.Opponents.FindAll(o => !o.ally);
            d.summary = $"SKIRMISH  ·  vs {enemies.Count} AI: " + string.Join(", ", enemies.ConvertAll(o => o.difficulty.ToString()));
            foreach (var o in Skirmish.Opponents)
            {
                var tr = new TeamResearch { team = o.team };
                foreach (var r in ResearchManager.DoneOf(o.team)) tr.ids.Add(r.id);
                d.teamResearch.Add(tr);
            }
        }
        else d.summary = "SANDBOX";
        foreach (var r in ResearchManager.Done) d.research.Add(r.id);

        foreach (var b in Object.FindObjectsByType<Building>(FindObjectsSortMode.None))
            if (b.CurrentResearch != null) d.labs.Add(new LabSave { lab = Key(b), research = b.CurrentResearch.id, timeLeft = b.TimeLeft });

        foreach (var up in Object.FindObjectsByType<BuildingUpgrade>(FindObjectsSortMode.None))
            if (up.level > 1) d.upgrades.Add(new UpgradeSave { building = Key(up), level = up.level });

        foreach (var c in Object.FindObjectsByType<Construction>(FindObjectsSortMode.None))
            d.sites.Add(new SiteSave { prefab = PrefabName(c), position = c.PlacedAt, rotation = c.transform.rotation, progress = c.Progress, team = TeamEconomy.TeamOf(c) });

        foreach (var o in Object.FindObjectsByType<OutpostCapture>(FindObjectsSortMode.None))
        {
            var s = new OutpostSave { outpost = o.name, owner = o.ownerTeam, capturing = o.capturingTeam, progress = o.progress, holdTimeLeft = o.holdTimeLeft };
            foreach (var g in o.Garrison) if (g != null) s.garrison.Add(Key(g));
            d.outposts.Add(s);
        }

        foreach (var v in Object.FindObjectsByType<Veterancy>(FindObjectsInactive.Include, FindObjectsSortMode.None)) // garrisoned units are inactive
            if (v.Kills > 0) d.veterans.Add(new VeterancySave { unit = Key(v), kills = v.Kills });
        CaptureWorld(d);
        return d;
    }

    // ---- and back out ----
    public static void Restore(SaveData d)
    {
        var all = new Dictionary<string, ResearchData>();
        foreach (var r in Resources.FindObjectsOfTypeAll<ResearchData>()) all[r.id] = r; // loaded via the Research Lab prefab
        var done = new List<ResearchData>();
        foreach (var id in d.research) if (all.TryGetValue(id, out var r)) done.Add(r);
        ResearchManager.Restore(done);
        foreach (var tr in d.teamResearch)
        {
            var theirs = new List<ResearchData>();
            foreach (var id in tr.ids) if (all.TryGetValue(id, out var r)) theirs.Add(r);
            ResearchManager.Restore(theirs, tr.team);
        }
        if (d.matchStats != null) System.Array.Copy(d.matchStats, MatchStats.Values, Mathf.Min(d.matchStats.Length, MatchStats.Values.Length));

        foreach (var l in d.labs)
            if (all.TryGetValue(l.research, out var r) && Find<Building>(l.lab) is Building lab) lab.RestoreResearch(r, l.timeLeft);

        foreach (var u in d.upgrades)
            if (Find<BuildingUpgrade>(u.building) is BuildingUpgrade up) up.SetLevel(u.level);

        var placer = Object.FindFirstObjectByType<BuildingPlacer>();
        foreach (var s in d.sites)
        {
            var data = placer != null ? System.Array.Find(placer.Buildings, b => b.prefab.name == s.prefab) : null;
            if (data == null) { Debug.LogWarning($"Save: no building called {s.prefab}"); continue; }
            var b = Object.Instantiate(data.prefab, s.position, s.rotation);
            b.GetComponent<Health>().team = s.team; // before Start, like the AI's own sites: colours, power, build slots
            if (b.TryGetComponent(out TeamMember tm)) tm.team = s.team;
            var c = b.AddComponent<Construction>();
            c.duration = data.buildTime;
            c.barTemplate = placer.ProgressBarTemplate;
            c.startProgress = s.progress;
        }

        foreach (var o in d.outposts)
        {
            var op = System.Array.Find(Object.FindObjectsByType<OutpostCapture>(FindObjectsSortMode.None), x => x.name == o.outpost);
            if (op == null) continue;
            op.Restore(o.owner, o.capturing, o.progress, o.holdTimeLeft);
            foreach (var g in o.garrison) if (Find<TeamMember>(g) is TeamMember tm) op.TryGarrison(tm);
            op.holdTimeLeft = o.holdTimeLeft;
        }

        foreach (var v in d.veterans)
            if (Find<SelectableUnit>(v.unit) is SelectableUnit u)
            {
                if (!u.TryGetComponent(out Veterancy vet)) vet = u.gameObject.AddComponent<Veterancy>();
                vet.SetKills(v.kills, announce: false);
            }
    }

    // ponytail: objects are matched by prefab name + position (no saved world yet); give them saved ids once units/buildings are saved too.
    const float MatchRadius = 3f;

    static string Key(Component c)
    {
        Vector3 p = c.transform.position;
        var inv = System.Globalization.CultureInfo.InvariantCulture; // "12.5", not "12,5", whatever the PC's language
        return $"{PrefabName(c)}@{p.x.ToString("0.0", inv)},{p.z.ToString("0.0", inv)}";
    }

    // "Chromejaw (2)" placed in the scene and "Chromejaw(Clone)" are both the Chromejaw prefab.
    static string PrefabName(Component c)
    {
        string n = c.gameObject.name.Replace("(Clone)", "").Trim();
        int copy = n.IndexOf(" (");
        return copy > 0 ? n.Substring(0, copy) : n;
    }

    static T Find<T>(string key) where T : Component
    {
        int at = key.LastIndexOf('@');
        string name = key.Substring(0, at);
        var xz = key.Substring(at + 1).Split(',');
        var p = new Vector3(float.Parse(xz[0], System.Globalization.CultureInfo.InvariantCulture), 0f, float.Parse(xz[1], System.Globalization.CultureInfo.InvariantCulture));
        T best = null;
        float bestSqr = MatchRadius * MatchRadius;
        foreach (var c in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (PrefabName(c) != name) continue;
            Vector3 d = c.transform.position - p; d.y = 0f;
            if (d.sqrMagnitude <= bestSqr) { bestSqr = d.sqrMagnitude; best = c; }
        }
        return best;
    }
}
