using System.Collections.Generic;
using UnityEngine;

// A player's career: every finished match adds to it (MatchManager.End -> ProfileService.Record).
// Plain data with a few read-only helpers; ProfileService stores it as JSON in Unity Cloud Save.
[System.Serializable]
public class PlayerProfile
{
    // One finished match, newest first in PlayerProfile.recent.
    [System.Serializable]
    public class MatchRecord
    {
        public bool won;
        public bool online;       // multiplayer room, else skirmish
        public string opponents;  // "2 AI: Hard, Normal" / "3 players"
        public int difficulty;    // skirmish: hardest enemy AI (Skirmish.Difficulty), -1 online
        public float seconds;
        public int kills, unitsLost;
        public long when;         // UTC ticks
    }

    // Wins / games per mode and per AI difficulty.
    [System.Serializable]
    public class Tally
    {
        public int wins, games;
        public int Losses => games - wins;
        public float Rate => games > 0 ? (float)wins / games : 0f;
    }

    public Tally all = new(), skirmish = new(), online = new();
    public Tally[] vsAI = { new(), new(), new() }; // Easy, Normal, Hard
    public int streak, bestStreak;                 // current: + wins in a row, - losses in a row
    public float secondsPlayed, fastestWin;        // fastestWin 0 = none yet
    public int[] totals = new int[MatchStats.Headers.Length]; // MatchStats.Kind order
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

    // Rank by wins: name, wins needed.
    public static readonly (string name, int wins)[] Ranks =
        { ("RECRUIT", 0), ("OPERATIVE", 3), ("NETRUNNER", 10), ("VANGUARD", 25), ("WARLORD", 50), ("LEGEND", 100) };

    public int RankIndex
    {
        get { int r = 0; for (int i = 0; i < Ranks.Length; i++) if (all.wins >= Ranks[i].wins) r = i; return r; }
    }

    // 0..1 towards the next rank (1 at the top rank).
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
