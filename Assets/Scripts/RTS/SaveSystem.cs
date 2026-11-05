using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Match state for the save (and later the cloud backend: ToJson/FromJson are the strings to upload).
// Covers research, building upgrade levels, construction in progress, outposts and veterancy.
// The rest of the world (units, buildings, credits) isn't saved yet, so Load puts these back onto
// matching objects already in the scene: same prefab within a few metres of the saved spot.
[System.Serializable]
public class SaveData
{
    public int version = 1;
    public List<string> research = new();          // completed research ids
    public List<LabSave> labs = new();             // project running in each lab
    public List<UpgradeSave> upgrades = new();     // building upgrade levels (Mk II / III)
    public List<SiteSave> sites = new();           // construction sites in progress
    public List<OutpostSave> outposts = new();
    public List<VeterancySave> veterans = new();
}

[System.Serializable] public class LabSave { public string lab; public string research; public float timeLeft; }
[System.Serializable] public class UpgradeSave { public string building; public int level; }
[System.Serializable] public class SiteSave { public string prefab; public Vector3 position; public Quaternion rotation; public float progress; }
[System.Serializable] public class OutpostSave { public string outpost; public int owner; public int capturing; public float progress; public float holdTimeLeft; public List<string> garrison = new(); }
[System.Serializable] public class VeterancySave { public string unit; public int kills; }

public static class SaveSystem
{
    public static string DefaultPath => Path.Combine(Application.persistentDataPath, "save.json");

    public static void Save(string path = null) => File.WriteAllText(path ?? DefaultPath, ToJson());

    public static bool Load(string path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path)) return false;
        FromJson(File.ReadAllText(path));
        return true;
    }

    public static string ToJson() => JsonUtility.ToJson(Capture(), true);
    public static void FromJson(string json) => Restore(JsonUtility.FromJson<SaveData>(json));

    // ---- what goes in ----
    public static SaveData Capture()
    {
        var d = new SaveData();
        foreach (var r in ResearchManager.Done) d.research.Add(r.id);

        foreach (var b in Object.FindObjectsByType<Building>(FindObjectsSortMode.None))
            if (b.CurrentResearch != null) d.labs.Add(new LabSave { lab = Key(b), research = b.CurrentResearch.id, timeLeft = b.TimeLeft });

        foreach (var up in Object.FindObjectsByType<BuildingUpgrade>(FindObjectsSortMode.None))
            if (up.level > 1) d.upgrades.Add(new UpgradeSave { building = Key(up), level = up.level });

        foreach (var c in Object.FindObjectsByType<Construction>(FindObjectsSortMode.None))
            d.sites.Add(new SiteSave { prefab = PrefabName(c), position = c.PlacedAt, rotation = c.transform.rotation, progress = c.Progress });

        foreach (var o in Object.FindObjectsByType<OutpostCapture>(FindObjectsSortMode.None))
        {
            var s = new OutpostSave { outpost = o.name, owner = o.ownerTeam, capturing = o.capturingTeam, progress = o.progress, holdTimeLeft = o.holdTimeLeft };
            foreach (var g in o.Garrison) if (g != null) s.garrison.Add(Key(g));
            d.outposts.Add(s);
        }

        foreach (var v in Object.FindObjectsByType<Veterancy>(FindObjectsInactive.Include, FindObjectsSortMode.None)) // garrisoned units are inactive
            if (v.Kills > 0) d.veterans.Add(new VeterancySave { unit = Key(v), kills = v.Kills });
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

    static string PrefabName(Component c) => c.gameObject.name.Replace("(Clone)", "").Trim();

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
