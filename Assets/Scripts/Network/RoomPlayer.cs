using System.Collections.Generic;
using Fusion;

// One per player in a room. The owner writes their username; everyone else receives it.
public class RoomPlayer : NetworkBehaviour
{
    public static readonly List<RoomPlayer> All = new();

    [Networked] public NetworkString<_32> Username { get; set; }

    public override void Spawned()
    {
        All.Add(this);
        Runner.MakeDontDestroyOnLoad(gameObject); // keep the roster when the match scene loads
        if (HasStateAuthority) Username = AccountService.Username ?? "Player";
    }

    public override void Despawned(NetworkRunner runner, bool hasState) => All.Remove(this);
}
