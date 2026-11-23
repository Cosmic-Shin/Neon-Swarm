using System.Collections.Generic;
using UnityEngine;

public static class Barks
{
    public enum Kind { Select, Move, Attack, Hold }

    static readonly Dictionary<string, AudioClip> clips = new();
    static float ready;
    static string last;

    public static void Say(List<SelectableUnit> units, Kind kind)
    {
        if (Time.unscaledTime < ready) return;
        UnitData lead = null;
        foreach (var u in units)
            if (u != null && u.Data != null && (lead == null || u.Data.cost > lead.cost)) lead = u.Data;
        if (lead == null) return;
        var lines = Lines(lead, kind);
        if (lines.Length == 0) return;
        string pick = lines[Random.Range(0, lines.Length)];
        if (pick == last && lines.Length > 1) pick = lines[(System.Array.IndexOf(lines, pick) + 1) % lines.Length];
        if (Sfx.Bark(Clip(pick), PitchOf(lead.role)))
        {
            last = pick;
            ready = Time.unscaledTime + (kind == Kind.Select ? 2.4f : 1.6f);
        }
    }

    static AudioClip Clip(string name)
    {
        if (!clips.TryGetValue(name, out var c) || c == null) clips[name] = c = Resources.Load<AudioClip>("Sfx/Voice/Units/" + name);
        return c;
    }

    static float PitchOf(UnitData.Role role) => role switch
    {
        UnitData.Role.Scout => 1.18f,
        UnitData.Role.Infantry => 1.06f,
        UnitData.Role.Support => 1.12f,
        UnitData.Role.Heavy => 0.82f,
        UnitData.Role.Elite => 0.72f,
        _ => 1f,
    };

    static string[] Lines(UnitData d, Kind kind)
    {
        bool worker = d.role == UnitData.Role.Worker;
        switch (kind)
        {
            case Kind.Select:
                return d.role == UnitData.Role.Support ? new[] { "ready", "war_medic" } : new[] { "ready", "correct" };
            case Kind.Move:
                return worker ? new[] { "correct", "go" } : new[] { "go", "war_go_go_go", "war_cover_me", "war_watch_my_back" };
            case Kind.Hold:
                return new[] { "hold" };
            default:
                if (worker || d.damage <= 0f) return System.Array.Empty<string>();
                if (d.range >= 25f) return new[] { "war_sniper", "war_target_engaged" };
                if (d.role == UnitData.Role.Heavy || d.role == UnitData.Role.Elite) return new[] { "war_fire_in_the_hole", "war_rpg", "war_target_engaged" };
                return new[] { "war_target_engaged", "war_suppressing_fire" };
        }
    }
}
