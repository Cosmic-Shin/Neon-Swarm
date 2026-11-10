using System.Collections.Generic;

// Building crew limit: at most Max of a team's buildings can be under construction or being upgraded at once.
// Walls don't count (they're laid in long lines). Construction and BuildingUpgrade register themselves here.
public static class BuildSlots
{
    public const int Max = 2;
    public const string FullReason = "2 BUILDINGS ALREADY IN PROGRESS";

    public static readonly HashSet<UnityEngine.Object> Busy = new(); // sites and upgrades in progress (every team)

    public static bool Free => FreeFor(0);

    // Single player: each AI has its own crew.
    public static bool FreeFor(int team)
    {
        Busy.RemoveWhere(o => o == null); // destroyed mid-job
        int n = 0;
        foreach (var o in Busy) if (o is UnityEngine.Component c && TeamEconomy.TeamOf(c) == team) n++;
        return n < Max;
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => Busy.Clear();
}
