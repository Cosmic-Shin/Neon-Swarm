using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerProfile
{
    [System.Serializable]
    public class MatchRecord
    {
        public bool won;
        public bool online;
        public string opponents;
        public int difficulty;
        public float seconds;
        public int kills, unitsLost;
        public long when;
    }

    [System.Serializable]
    public class Tally
    {
        public int wins, games;
        public int Losses => games - wins;
        public float Rate => games > 0 ? (float)wins / games : 0f;
    }

    public string avatar = "";
    public Tally all = new(), skirmish = new(), online = new();
    public Tally[] vsAI = { new(), new(), new() };
    public int streak, bestStreak;
    public float secondsPlayed, fastestWin;
    public int[] totals = new int[MatchStats.Headers.Length];
    public List<MatchRecord> recent = new();

    public const int RecentKept = 10;

    public void Add(MatchRecord m, int[] stats)
    {
        Count(all, m.won);
        Count(m.online ? online : skirmish, m.won);
        if (!m.online && m.difficulty >= 0 && m.difficulty < vsAI.Length) Count(vsAI[m.difficulty], m.won);
        streak = m.won ? Mathf.Max(0, streak) + 1 : Mathf.Min(0, streak) - 1;
        bestStreak = Mathf.Max(bestStreak, streak);
        secondsPlayed += m.seconds;
        if (m.won && (fastestWin <= 0f || m.seconds < fastestWin)) fastestWin = m.seconds;
        if (totals == null || totals.Length != stats.Length) System.Array.Resize(ref totals, stats.Length);
        for (int i = 0; i < stats.Length; i++) totals[i] += stats[i];
        recent.Insert(0, m);
        if (recent.Count > RecentKept) recent.RemoveRange(RecentKept, recent.Count - RecentKept);
    }

    static void Count(Tally t, bool won) { t.games++; if (won) t.wins++; }

    public int Total(MatchStats.Kind k) => totals != null && (int)k < totals.Length ? totals[(int)k] : 0;

    public static readonly (string name, int wins)[] Ranks =
        { ("RECRUIT", 0), ("OPERATIVE", 3), ("NETRUNNER", 10), ("VANGUARD", 25), ("WARLORD", 50), ("LEGEND", 100) };

    public int RankIndex
    {
        get { int r = 0; for (int i = 0; i < Ranks.Length; i++) if (all.wins >= Ranks[i].wins) r = i; return r; }
    }

    public float RankProgress
    {
        get
        {
            int r = RankIndex;
            if (r + 1 >= Ranks.Length) return 1f;
            return Mathf.InverseLerp(Ranks[r].wins, Ranks[r + 1].wins, all.wins);
        }
    }
}
