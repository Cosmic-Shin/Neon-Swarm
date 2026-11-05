using Fusion;
using UnityEngine;

// The room's shared setup, owned by the host (master client; passes to the next one if the host leaves):
// who sits in which player slot (= player number and colour), which base each slot starts at,
// starting credits and the time limit. Only the host writes it; everyone reads it, in the room and in the match.
public class RoomState : NetworkBehaviour
{
    public static RoomState Instance { get; private set; }

    public static readonly int[] CreditOptions = { 2500, 5000, 10000, 20000 };
    public static readonly int[] MinuteOptions = { 0, 15, 30, 45, 60 }; // 0 = no limit
    static readonly int[] DefaultBases = { 0, 4, 2, 6, 1, 5, 3, 7 };    // 2 players start opposite each other

    [Networked, Capacity(RoomService.MaxPlayers)] public NetworkArray<PlayerRef> Slots => default;
    [Networked, Capacity(RoomService.MaxPlayers)] public NetworkArray<int> Bases => default; // slot -> base 0..7
    [Networked] public int Credits { get; set; }
    [Networked] public int Minutes { get; set; }
    [Networked] public float MatchStart { get; set; } // Runner.SimulationTime when the map loaded; 0 = not yet

    public bool IsHost => Object != null && Object.IsValid && HasStateAuthority;

    public override void Spawned()
    {
        Instance = this;
        Runner.MakeDontDestroyOnLoad(gameObject); // carries the setup into the match
        if (HasStateAuthority && Credits == 0)
        {
            Credits = 5000;
            for (int i = 0; i < RoomService.MaxPlayers; i++) Bases.Set(i, DefaultBases[i]);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;
    }

    // Host: seat newcomers in the first free slot, free the slots of players who left.
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        for (int i = 0; i < RoomService.MaxPlayers; i++)
        {
            var p = Slots[i];
            if (p != PlayerRef.None && !Contains(Runner.ActivePlayers, p)) Slots.Set(i, PlayerRef.None);
        }
        foreach (var p in Runner.ActivePlayers)
        {
            if (SlotOf(p) >= 0) continue;
            for (int i = 0; i < RoomService.MaxPlayers; i++)
                if (Slots[i] == PlayerRef.None) { Slots.Set(i, p); break; }
        }
    }

    static bool Contains(System.Collections.Generic.IEnumerable<PlayerRef> list, PlayerRef p)
    {
        foreach (var x in list) if (x == p) return true;
        return false;
    }

    public int SlotOf(PlayerRef p)
    {
        for (int i = 0; i < RoomService.MaxPlayers; i++) if (Slots[i] == p) return i;
        return -1;
    }

    public int SlotAtBase(int baseIndex)
    {
        for (int i = 0; i < RoomService.MaxPlayers; i++) if (Bases[i] == baseIndex) return i;
        return -1;
    }

    public string NameOf(int slot)
    {
        if (slot < 0 || slot >= RoomService.MaxPlayers) return "?";
        var p = Slots[slot];
        foreach (var rp in RoomPlayer.All) if (rp != null && rp.Object.StateAuthority == p) return rp.Username.ToString();
        return $"Player {slot + 1}";
    }

    // ---- host only ----

    // Move a player to a base; whoever had it takes theirs (bases stay one per slot).
    public void MoveToBase(int slot, int baseIndex)
    {
        if (!IsHost) return;
        int other = SlotAtBase(baseIndex);
        if (other < 0 || other == slot) return;
        int mine = Bases[slot];
        Bases.Set(slot, baseIndex);
        Bases.Set(other, mine);
    }

    public void NextCredits() { if (IsHost) Credits = Next(CreditOptions, Credits); }
    public void NextMinutes() { if (IsHost) Minutes = Next(MinuteOptions, Minutes); }

    static int Next(int[] options, int current) => options[(System.Array.IndexOf(options, current) + 1) % options.Length];
}
