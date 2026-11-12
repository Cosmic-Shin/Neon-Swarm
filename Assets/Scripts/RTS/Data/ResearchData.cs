using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "RTS/Research Data", fileName = "NewResearch")]
public class ResearchData : ScriptableObject
{
    public enum Branch { Econ, Assault, Heavy, Support, Defense, Elite }

    public string id;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;
    public Branch branch;
    [Range(1, 3)] public int tier = 1;
    public int cost = 1000;
    public float researchTime = 30f;
    public ResearchData requires;
    public StatMod[] effects;
}

public static class ResearchManager
{
    static readonly Dictionary<int, List<ResearchData>> done = new();
    static readonly Dictionary<(int team, ResearchData r), Building> running = new();

    public static IReadOnlyList<ResearchData> Done => DoneOf(0);
    public static IReadOnlyList<ResearchData> DoneOf(int team) => List(team);
    static List<ResearchData> List(int team) => done.TryGetValue(team, out var l) ? l : done[team] = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => Clear();

    public static void Clear() { done.Clear(); running.Clear(); }

    public static bool IsDone(ResearchData r, int team = 0) => List(team).Contains(r);

    public static int CoreLevel => CoreLevelOf(0);

    public static int CoreLevelOf(int team)
    {
        int best = 1;
        foreach (var s in Structure.All)
            if (s.isHQ && s.Health.team == team && s.TryGetComponent(out BuildingUpgrade up)) best = Mathf.Max(best, up.level);
        return best;
    }

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

    public static void Restore(IEnumerable<ResearchData> list, int team = 0)
    {
        var mine = List(team);
        mine.Clear();
        mine.AddRange(list);
        Stats.Refresh();
    }
}
