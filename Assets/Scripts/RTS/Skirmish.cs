using System.Collections.Generic;
using UnityEngine;

// Single-player match against AI opponents, set up on the main menu's Skirmish screen (laid out like a
// multiplayer room). You are team 0; AIs are teams 1-7 (their seat number). Like online, everyone has a
// colour, a lobby team (same team number = allies, 0 = on your own) and a base spot.
public static class Skirmish
{
    public enum Difficulty { Easy, Normal, Hard }

    [System.Serializable]
    public class Opponent
    {
        public int team;              // 1..7: its seat in the lobby
        public Difficulty difficulty = Difficulty.Normal;
        public int side;              // lobby team: 0 none, 1-4
        public int color;             // index into TeamMember.TeamColors
        public int baseIndex;         // Base_1..Base_8 = 0..7
        public bool ally => Allied(0, team);
        public string Name => ally ? $"Ally AI {team} ({difficulty})" : $"AI {team} ({difficulty})";
    }

    public static bool Active;                     // this RTS scene was started from the Skirmish screen
    public static int StartCredits = 5000;
    public static int Minutes;                     // time limit, 0 = none
    public static int PlayerSide, PlayerColor, PlayerBase; // yours, as for an Opponent
    public static float ElapsedBefore;             // a loaded save: play time before this scene started
    public static float Elapsed => Time.timeSinceLevelLoad + ElapsedBefore; // match clock, offline
    public static readonly List<Opponent> Opponents = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { Active = false; Opponents.Clear(); Minutes = 0; PlayerSide = PlayerColor = PlayerBase = 0; ElapsedBefore = 0f; }

    // A plain loop: a lambda here allocated on every call, and Allied/SideOf run thousands of times a frame.
    public static Opponent Find(int team)
    {
        for (int i = 0; i < Opponents.Count; i++) if (Opponents[i].team == team) return Opponents[i];
        return null;
    }

    // Which side a team fights for: lobby teams are one side each (100 + team), everyone else is alone.
    public static int SideOf(int team)
    {
        int lobby = team == 0 ? PlayerSide : Find(team)?.side ?? 0;
        return lobby != 0 ? 100 + lobby : team;
    }

    public static bool Allied(int a, int b) => a == b || (a != Creep.Team && b != Creep.Team && Active && SideOf(a) == SideOf(b));

    // TeamMember.ColorOf offline: the colour picked in the lobby.
    public static int ColorIndexOf(int team) => team == 0 ? PlayerColor : Find(team)?.color ?? team;
}

// Credits per team. Yours stay in the ResourceBank (the HUD counter, saves, events);
// AI teams keep theirs here. Everything that earns or spends goes through this.
public static class TeamEconomy
{
    static readonly int[] credits = new int[16];
    static readonly float[] incomeBonus = new float[16];
    static readonly int[] earned = new int[16]; // total income so far (AIs split it between base and army)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { System.Array.Clear(credits, 0, credits.Length); System.Array.Clear(incomeBonus, 0, incomeBonus.Length); System.Array.Clear(earned, 0, earned.Length); }

    public static int Earned(int team) => Valid(team) ? earned[team] : 0;

    static bool Valid(int team) => team > 0 && team < credits.Length;

    public static int TeamOf(Component c) => c != null && c.TryGetComponent(out Health h) ? h.team : 0;

    public static int Credits(int team) => team == 0 ? ResourceBank.GetOrCreate().Credits : Valid(team) ? credits[team] : 0;

    public static void Set(int team, int amount)
    {
        if (team == 0)
        {
            var bank = ResourceBank.GetOrCreate();
            if (amount > bank.Credits) bank.Add(amount - bank.Credits); else bank.Spend(bank.Credits - amount);
        }
        else if (Valid(team)) credits[team] = amount;
    }

    // Hard AI: +15% on what its harvesters and siphons bring in.
    public static void SetIncomeBonus(int team, float bonus) { if (Valid(team)) incomeBonus[team] = bonus; }

    public static void Add(int team, int amount)
    {
        if (team == 0) { ResourceBank.GetOrCreate().Add(amount); return; }
        if (Valid(team)) credits[team] += amount;
    }

    // Income (harvest, siphon): AI difficulty bonus applies.
    public static void Earn(int team, int amount)
    {
        if (!Valid(team)) { Add(team, amount); return; }
        int n = Mathf.RoundToInt(amount * (1f + incomeBonus[team]));
        earned[team] += n;
        Add(team, n);
    }

    public static bool Spend(int team, int amount)
    {
        if (team == 0) return ResourceBank.GetOrCreate().Spend(amount);
        if (!Valid(team) || credits[team] < amount) return false;
        credits[team] -= amount;
        return true;
    }
}
