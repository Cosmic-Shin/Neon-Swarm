using System.Collections.Generic;

public static class BuildSlots
{
    public const int Max = 2;
    public const string FullReason = "2 BUILDINGS ALREADY IN PROGRESS";

    public static readonly HashSet<UnityEngine.Object> Busy = new();

    public static bool Free => FreeFor(0);

    public static bool FreeFor(int team)
    {
        Busy.RemoveWhere(o => o == null);
        int n = 0;
        foreach (var o in Busy) if (o is UnityEngine.Component c && TeamEconomy.TeamOf(c) == team) n++;
        return n < Max;
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => Busy.Clear();
}
