using System.Collections.Generic;
using Fusion;

// One per player in a room. The owner writes their username (and, in a match, whether they are low on power).
public class RoomPlayer : NetworkBehaviour
{
    public static readonly List<RoomPlayer> All = new();

    [Networked] public NetworkString<_32> Username { get; set; }
    [Networked] public NetworkBool LowPower { get; set; } // in a match: other PCs read it for our cloak fields and reveal rings

    public override void Spawned()
    {
        All.Add(this);
        Runner.MakeDontDestroyOnLoad(gameObject); // keep the roster when the match scene loads
        if (HasStateAuthority) Username = AccountService.Username ?? "Player";
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority && Net.InMatch) LowPower = Structure.LowPower;
    }

    public override void Despawned(NetworkRunner runner, bool hasState) => All.Remove(this);
}
