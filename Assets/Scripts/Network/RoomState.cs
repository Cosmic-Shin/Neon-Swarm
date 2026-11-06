using Fusion;
using UnityEngine;

// The room's shared setup, owned by the host (master client; passes to the next one if the host leaves):
// who sits in which player slot (= player number), each player's colour and team, which base each slot starts at,
// starting credits and the time limit. Only the host writes it (players ask for their colour/team by RPC);
// everyone reads it, in the room and in the match.
public partial class RoomState : NetworkBehaviour // outposts: RoomState.Outposts.cs
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
    [Networked, Capacity(RoomService.MaxPlayers)] NetworkArray<int> Colors => default; // slot -> TeamMember.TeamColors index
    [Networked, Capacity(RoomService.MaxPlayers)] NetworkArray<int> Teams => default;  // slot -> 0 = no team, 1..TeamCount
    public const int TeamCount = 4;

    // Ore: the host owns every patch's amount (and its regrowth); everyone else mirrors it and sends what
    // their harvesters mine. Patches are numbered by position, which is the same on every PC.
    const int MaxOre = 192;
    [Networked, Capacity(MaxOre)] NetworkArray<float> Ore => default;
    [Networked] NetworkBool OreReady { get; set; }
    static readonly System.Collections.Generic.List<OreNode> ores = new();

    public bool IsHost => Object != null && Object.IsValid && HasStateAuthority;

    public override void Spawned()
    {
        Instance = this;
        Runner.MakeDontDestroyOnLoad(gameObject); // carries the setup into the match
        if (HasStateAuthority && Credits == 0)
        {
            Credits = 5000;
            for (int i = 0; i < RoomService.MaxPlayers; i++) { Bases.Set(i, DefaultBases[i]); Colors.Set(i, i); }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;
        OreNode.TakenHook = null;
        ores.Clear();
        ClearOutposts();
    }

    // ---- ore ----
    static int OreIndex(OreNode node)
    {
        if (ores.Count != OreNode.All.Count)
        {
            ores.Clear();
            ores.AddRange(OreNode.All);
            ores.Sort((a, b) => a.transform.position.x != b.transform.position.x
                ? a.transform.position.x.CompareTo(b.transform.position.x)
                : a.transform.position.z.CompareTo(b.transform.position.z));
            if (ores.Count > MaxOre) Debug.LogError($"[GD5] {ores.Count} ore patches; only {MaxOre} are synced. Raise RoomState.MaxOre.");
        }
        return node == null ? -1 : ores.IndexOf(node);
    }

    void SyncOre()
    {
        if (!Net.InMatch || OreNode.All.Count == 0) return;
        OreIndex(null);
        OreNode.TakenHook = HasStateAuthority ? null : (node, got) =>
        {
            int i = OreIndex(node);
            if (i >= 0 && i < MaxOre) RPC_TakeOre(i, got);
        };
        int n = Mathf.Min(ores.Count, MaxOre);
        if (HasStateAuthority)
        {
            for (int i = 0; i < n; i++) if (ores[i]) Ore.Set(i, ores[i].amount);
            OreReady = true;
        }
        else if (OreReady)
            for (int i = 0; i < n; i++) if (ores[i]) ores[i].SetFromNet(Ore[i]);
    }

    // ponytail: the miner keeps what it took right away; if two players drain the last ore at once both get it.
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_TakeOre(int index, float amount)
    {
        if (index < ores.Count && ores[index]) ores[index].Take(amount);
    }

    // ---- chat ----
    public void SendChat(string text) => RPC_Chat(SlotOf(Runner.LocalPlayer), text);

    public static event System.Action<int, string> ChatReceived; // (sender slot, text): room screen log, match alerts

    [Rpc(RpcSources.All, RpcTargets.All)]
    void RPC_Chat(int slot, string text)
    {
        Debug.Log($"[GD5] chat {NameOf(slot)}: {text}");
        ChatReceived?.Invoke(slot, text);
    }

    public override void Render()
    {
        if (!HasStateAuthority) { SyncOre(); SyncOutposts(); }
    }

    // Host: seat newcomers in the first free slot, free the slots of players who left.
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        SyncOre();
        SyncOutposts();
        for (int i = 0; i < RoomService.MaxPlayers; i++)
        {
            var p = Slots[i];
            if (p != PlayerRef.None && !Contains(Runner.ActivePlayers, p)) Slots.Set(i, PlayerRef.None);
        }
        foreach (var p in Runner.ActivePlayers)
        {
            if (SlotOf(p) >= 0) continue;
            for (int i = 0; i < RoomService.MaxPlayers; i++)
                if (Slots[i] == PlayerRef.None) { Slots.Set(i, p); Teams.Set(i, 0); if (ColorTaken(i, Colors[i])) RPC_NextColor(i); break; }
        }
    }

    bool ColorTaken(int slot, int color)
    {
        for (int i = 0; i < RoomService.MaxPlayers; i++) if (i != slot && Slots[i] != PlayerRef.None && Colors[i] == color) return true;
        return false;
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

    bool Valid(int slot) => Object != null && Object.IsValid && slot >= 0 && slot < RoomService.MaxPlayers;
    public int ColorIndexOf(int slot) => Valid(slot) ? Colors[slot] : slot;
    public int TeamOf(int slot) => Valid(slot) ? Teams[slot] : 0;

    // ---- colour and team: each player picks their own (C&C style); the host applies it ----
    public void NextColor() => RPC_NextColor(SlotOf(Runner.LocalPlayer));
    public void NextTeam() => RPC_NextTeam(SlotOf(Runner.LocalPlayer));

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_NextColor(int slot)
    {
        if (!Valid(slot)) return;
        int n = TeamMember.TeamColors.Length;
        for (int step = 1; step < n; step++) // the next colour nobody else has
        {
            int c = (Colors[slot] + step) % n;
            if (!ColorTaken(slot, c)) { Colors.Set(slot, c); return; }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_NextTeam(int slot) { if (Valid(slot)) Teams.Set(slot, (Teams[slot] + 1) % (TeamCount + 1)); }

    // ---- map pings (Alt+click): shown to the sender's allies ----
    public void SendPing(Vector3 at) => RPC_Ping(SlotOf(Runner.LocalPlayer), at);

    [Rpc(RpcSources.All, RpcTargets.All)]
    void RPC_Ping(int slot, Vector3 at) { if (Net.Allied(Net.ToTeam(slot), 0)) Ping.Show(at, TeamMember.ColorOf(slot), NameOf(slot)); }

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
    public const int MinCredits = 500, MaxCredits = 1_000_000;
    public void SetCredits(int amount) { if (IsHost) Credits = Mathf.Clamp(amount, MinCredits, MaxCredits); }
    public void NextMinutes() { if (IsHost) Minutes = Next(MinuteOptions, Minutes); }

    static int Next(int[] options, int current) => options[(System.Array.IndexOf(options, current) + 1) % options.Length];
}
