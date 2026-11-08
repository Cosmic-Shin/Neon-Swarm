using UnityEngine;

// Your own numbers for the end-of-match screen, counted where things happen.
// Online, RoomPlayer publishes them so every PC can show everyone's.
public static class MatchStats
{
    public enum Kind { UnitsBuilt, UnitsLost, BuildingsBuilt, BuildingsLost, Kills, Earned, Research }
    public static readonly string[] Headers = { "UNITS\nBUILT", "UNITS\nLOST", "BUILDINGS\nBUILT", "BUILDINGS\nLOST", "KILLS", "CREDITS\nEARNED", "RESEARCH" };
    public static readonly int[] Values = new int[Headers.Length];

    public static void Add(Kind k, int n = 1) => Values[(int)k] += n;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset() => System.Array.Clear(Values, 0, Values.Length);
}
