using System.Collections.Generic;
using UnityEngine;

// One node of the research tree (Research Lab). Each can be researched once per match.
[CreateAssetMenu(menuName = "RTS/Research Data", fileName = "NewResearch")]
public class ResearchData : ScriptableObject
{
    public enum Branch { Econ, Assault, Heavy, Support, Defense, Elite }

    public string id;                     // "E1", used by saves
    public string displayName;
    [TextArea] public string description; // the effect, shown in the shop tooltip
    public Sprite icon;
    public Branch branch;
    [Range(1, 3)] public int tier = 1;    // needs the Core Nexus at this Mk level
    public int cost = 1000;
    public float researchTime = 30f;
    public ResearchData requires;         // must be researched first
    public StatMod[] effects;             // empty = no gameplay mechanic for it yet
}

// Research for the player's side: what's done, and what each lab is working on.
// One project per lab, labs run in parallel, the same node can't run in two labs.
// The lab's own queue (Building) pays and counts the time; a lab destroyed mid-project loses it.
public static class ResearchManager
{
    static readonly List<ResearchData> done = new();
    static readonly Dictionary<ResearchData, Building> running = new();

    public static IReadOnlyList<ResearchData> Done => done;
    public static IReadOnlyDictionary<ResearchData, Building> Running => running;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { done.Clear(); running.Clear(); } // fresh each play session

    public static bool IsDone(ResearchData r) => done.Contains(r);

    // Mk level of the player's best Core Nexus (1 without an upgraded one): gates research tiers and elite units.
    public static int CoreLevel
    {
        get
        {
            int best = 1;
            foreach (var s in Structure.All)
                if (s.isHQ && s.Health.team == 0 && s.TryGetComponent(out BuildingUpgrade up)) best = Mathf.Max(best, up.level);
            return best;
        }
    }

    // Why it can't start yet (null = it can).
    public static string Blocker(ResearchData r, Building lab)
    {
        if (IsDone(r)) return "Researched";
        if (running.TryGetValue(r, out var other) && other != null && other != lab) return "In progress in another lab";
        if (r.tier > CoreLevel) return $"Needs Core Nexus Mk {(r.tier == 2 ? "II" : "III")}";
        if (r.requires != null && !IsDone(r.requires)) return $"Requires {r.requires.displayName}";
        return null;
    }

    public static void Started(ResearchData r, Building lab) => running[r] = lab;
    public static void Stopped(ResearchData r) => running.Remove(r);

    public static void Complete(ResearchData r)
    {
        running.Remove(r);
        if (done.Contains(r)) return;
        done.Add(r);
        Stats.Refresh();
    }

    // Saves: put back what was done without the alerts.
    public static void Restore(IEnumerable<ResearchData> list)
    {
        done.Clear();
        done.AddRange(list);
        Stats.Refresh();
    }
}
