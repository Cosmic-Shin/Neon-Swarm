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
        public int[] stats;
        public string map;
        public int startCredits;
        public int faction;   // FactionId played (old records: 0 = Hive)
    }

    [System.Serializable]
    public class Tally
    {
        public int wins, games;
        public int Losses => games - wins;
        public float Rate => games > 0 ? (float)wins / games : 0f;
    }

    public string avatar = "";
    public string[] badges = new string[0];
    public Tally all = new(), skirmish = new(), online = new();
    public Tally[] vsAI = { new(), new(), new() };
    public int streak, bestStreak;
    public float secondsPlayed, fastestWin, secondsInGame;
    public int[] totals = new int[MatchStats.Headers.Length];
    public List<MatchRecord> recent = new();

    public const int RecentKept = 10;

    public Tally[] factions = { new(), new() };   // by FactionId

    /// <summary>Record for one faction. Games from before factions were tracked count as Hive (the only faction then).</summary>
    public Tally As(FactionId f)
    {
        int n = System.Enum.GetValues(typeof(FactionId)).Length;
        if (factions == null || factions.Length < n)
        {
            var old = factions ?? new Tally[0];
            factions = new Tally[n];
            for (int i = 0; i < n; i++) factions[i] = i < old.Length && old[i] != null ? old[i] : new Tally();
        }
        int games = 0, wins = 0;
        foreach (var t in factions) { games += t.games; wins += t.wins; }
        if (games < all.games) { factions[0].games += all.games - games; factions[0].wins += Mathf.Max(0, all.wins - wins); }
        return factions[(int)f];
    }

    /// <summary>The faction played most (ties go to the one with more wins); null before any game.</summary>
    public FactionId? Main
    {
        get
        {
            if (all.games == 0) return null;
            FactionId best = FactionId.Hive;
            foreach (FactionId f in System.Enum.GetValues(typeof(FactionId)))
                if (As(f).games > As(best).games || (As(f).games == As(best).games && As(f).wins > As(best).wins)) best = f;
            return best;
        }
    }

    public void Add(MatchRecord m, int[] stats)
    {
        Count(As((FactionId)m.faction), m.won);   // As() backfills older games first, so count it before 'all'
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
