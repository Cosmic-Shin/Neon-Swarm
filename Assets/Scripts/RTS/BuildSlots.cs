using System.Collections.Generic;

// Building crew limit: at most Max of your buildings can be under construction or being upgraded at once.
// Walls don't count (they're laid in long lines). Construction and BuildingUpgrade register themselves here.
public static class BuildSlots
{
    public const int Max = 2;
    public const string FullReason = "2 BUILDINGS ALREADY IN PROGRESS";

    public static readonly HashSet<UnityEngine.Object> Busy = new(); // sites and upgrades in progress

    public static bool Free
    {
        get
        {
            Busy.RemoveWhere(o => o == null); // destroyed mid-job
            return Busy.Count < Max;
        }
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => Busy.Clear();
}
