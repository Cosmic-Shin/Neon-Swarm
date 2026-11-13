using System.Collections.Generic;
using Fusion;

public class RoomPlayer : NetworkBehaviour
{
    public static readonly List<RoomPlayer> All = new();

    [Networked] public NetworkString<_32> Username { get; set; }
    [Networked] public NetworkString<_64> PlayerId { get; set; }
    [Networked] public NetworkBool LowPower { get; set; }
    [Networked] public int Credits { get; set; }
    [Networked] public NetworkString<_128> Research { get; set; }
    [Networked] public NetworkBool Live { get; set; }
    public static bool Publish;
    [Networked, Capacity(8)] public NetworkArray<int> Stats => default;

    public override void Spawned()
    {
        All.Add(this);
        Runner.MakeDontDestroyOnLoad(gameObject);
        if (HasStateAuthority)
        {
            Username = AccountService.Username ?? "Player";
            PlayerId = AccountService.SignedIn ? AccountService.PlayerId : "";
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !Net.InMatch || !Publish) return;
        Live = true;
        LowPower = Structure.LowPower;
        Credits = ResourceBank.GetOrCreate().Credits;
        Research = RoomState.ResearchIds(0);
        for (int i = 0; i < MatchStats.Values.Length; i++) Stats.Set(i, MatchStats.Values[i]);
    }

    public static RoomPlayer Of(PlayerRef p)
    {
        foreach (var rp in All) if (rp != null && rp.Object != null && rp.Object.StateAuthority == p) return rp;
        return null;
    }

    public static List<(string name, string id)> Strangers()
    {
        var list = new List<(string, string)>();
        foreach (var rp in All)
        {
            if (rp == null || rp.Object == null || rp.HasStateAuthority) continue;
            string id = rp.PlayerId.ToString();
            if (id.Length > 0 && !FriendsHub.IsKnown(id)) list.Add((rp.Username.ToString(), id));
        }
        return list;
    }

    public override void Despawned(NetworkRunner runner, bool hasState) => All.Remove(this);
}
