using System.Collections.Generic;
using Fusion;

// One per player in a room. The owner writes their username (and, in a match, whether they are low on power
// and their match stats).
public class RoomPlayer : NetworkBehaviour
{
    public static readonly List<RoomPlayer> All = new();

    [Networked] public NetworkString<_32> Username { get; set; }
    [Networked] public NetworkBool LowPower { get; set; } // in a match: other PCs read it for our cloak fields and reveal rings
    [Networked, Capacity(8)] public NetworkArray<int> Stats => default; // in a match: our MatchStats, for the end screen

    public override void Spawned()
    {
        All.Add(this);
        Runner.MakeDontDestroyOnLoad(gameObject); // keep the roster when the match scene loads
        if (HasStateAuthority) Username = AccountService.Username ?? "Player";
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !Net.InMatch) return;
        LowPower = Structure.LowPower;
        for (int i = 0; i < MatchStats.Values.Length; i++) Stats.Set(i, MatchStats.Values[i]);
    }

    public static RoomPlayer Of(PlayerRef p)
    {
        foreach (var rp in All) if (rp != null && rp.Object != null && rp.Object.StateAuthority == p) return rp;
        return null;
    }

    public override void Despawned(NetworkRunner runner, bool hasState) => All.Remove(this);
}
