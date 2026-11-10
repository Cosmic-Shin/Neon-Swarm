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

// Research per team: what's done, and what each lab is working on. Team 0 is you; single-player AIs
// research for their own team. One project per lab, labs run in parallel, the same node can't run in
// two of a team's labs. The lab's own queue (Building) pays and counts the time; a lab destroyed
// mid-project loses it.
public static class ResearchManager
{
    static readonly Dictionary<int, List<ResearchData>> done = new();
    static readonly Dictionary<(int team, ResearchData r), Building> running = new();

    public static IReadOnlyList<ResearchData> Done => DoneOf(0);
    public static IReadOnlyList<ResearchData> DoneOf(int team) => List(team);
    static List<ResearchData> List(int team) => done.TryGetValue(team, out var l) ? l : done[team] = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { done.Clear(); running.Clear(); } // fresh each play session

    public static bool IsDone(ResearchData r, int team = 0) => List(team).Contains(r);

    // Mk level of the player's best Core Nexus (1 without an upgraded one): gates research tiers and elite units.
    public static int CoreLevel => CoreLevelOf(0);

    // Any team's (single-player AIs upgrade their own Core Nexus).
    public static int CoreLevelOf(int team)
    {
        int best = 1;
        foreach (var s in Structure.All)
            if (s.isHQ && s.Health.team == team && s.TryGetComponent(out BuildingUpgrade up)) best = Mathf.Max(best, up.level);
        return best;
    }

    // Why it can't start yet (null = it can).
    public static string Blocker(ResearchData r, Building lab)
    {
        int team = lab.Team;
        if (IsDone(r, team)) return "Researched";
        if (running.TryGetValue((team, r), out var other) && other != null && other != lab) return "In progress in another lab";
        if (r.tier > CoreLevelOf(team)) return $"Needs Core Nexus Mk {(r.tier == 2 ? "II" : "III")}";
        if (r.requires != null && !IsDone(r.requires, team)) return $"Requires {r.requires.displayName}";
        return null;
    }

    public static void Started(ResearchData r, Building lab) => running[(lab.Team, r)] = lab;
    // By lab, not team: also called while the lab is being destroyed.
    public static void Stopped(ResearchData r, Building lab)
    {
        foreach (var k in new List<(int, ResearchData)>(running.Keys)) if (k.Item2 == r && running[k] == lab) running.Remove(k);
    }

    public static void Complete(ResearchData r, int team = 0)
    {
        running.Remove((team, r));
        var list = List(team);
        if (list.Contains(r)) return;
        list.Add(r);
        Stats.Refresh();
    }

    // Saves: put back what was done without the alerts.
    public static void Restore(IEnumerable<ResearchData> list)
    {
        var mine = List(0);
        mine.Clear();
        mine.AddRange(list);
        Stats.Refresh();
    }
}
