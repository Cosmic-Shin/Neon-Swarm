using System.Collections.Generic;
using Fusion;
using UnityEngine;

public partial class RoomState
{
    public const float BotDelay = 60f;
    const int StatCount = 8;

    [Networked, Capacity(RoomService.MaxPlayers)] NetworkArray<NetworkString<_32>> Names => default;
    [Networked, Capacity(RoomService.MaxPlayers)] NetworkArray<NetworkString<_64>> Ids => default;
    [Networked, Capacity(RoomService.MaxPlayers)] NetworkArray<float> AwaySince => default;
    [Networked, Capacity(RoomService.MaxPlayers)] NetworkArray<NetworkBool> Bot => default;
    [Networked, Capacity(RoomService.MaxPlayers)] NetworkArray<NetworkBool> Returned => default;
    [Networked, Capacity(RoomService.MaxPlayers)] public NetworkArray<int> SavedCredits => default;
    [Networked, Capacity(RoomService.MaxPlayers)] NetworkArray<NetworkString<_128>> SavedResearch => default;
    [Networked, Capacity(RoomService.MaxPlayers * StatCount)] NetworkArray<int> SavedStats => default;
    [Networked, Capacity(RoomService.MaxSpectators)] public NetworkArray<PlayerRef> Watchers => default;

    readonly HashSet<int> adopted = new();
    readonly Dictionary<int, AIPlayer> brains = new();

    public bool IsAway(int slot) => Valid(slot) && AwaySince[slot] > 0f;
    public bool IsBot(int slot) => Valid(slot) && Bot[slot];
    public bool HasReturned(int slot) => Valid(slot) && Returned[slot];
    public float BotIn(int slot) => IsAway(slot) && !Bot[slot] ? Mathf.Max(0f, BotDelay - (Runner.SimulationTime - AwaySince[slot])) : 0f;

    public int WatcherOf(PlayerRef p)
    {
        for (int i = 0; i < RoomService.MaxSpectators; i++) if (Watchers[i] == p) return i;
        return -1;
    }

    public string WatcherName(int i)
    {
        var rp = RoomPlayer.Of(Watchers[i]);
        return rp != null ? rp.Username.ToString() : "Spectator";
    }

    static string KeyOf(RoomPlayer rp)
    {
        string id = rp.PlayerId.ToString();
        return id.Length > 0 ? id : "name:" + rp.Username;
    }

    void UpdateSeats()
    {
        for (int i = 0; i < RoomService.MaxPlayers; i++)
        {
            var p = Slots[i];
            if (p == PlayerRef.None) continue;
            if (Contains(Runner.ActivePlayers, p))
            {
                var rp = RoomPlayer.Of(p);
                if (rp != null && rp.Username.Length > 0) { Names.Set(i, rp.Username); Ids.Set(i, KeyOf(rp)); }
                continue;
            }
            if (!Net.InMatch) { Slots.Set(i, PlayerRef.None); Ready.Set(i, false); }
            else if (AwaySince[i] <= 0f) GoAway(i);
        }
        for (int i = 0; i < RoomService.MaxSpectators; i++)
            if (Watchers[i] != PlayerRef.None && !Contains(Runner.ActivePlayers, Watchers[i])) Watchers.Set(i, PlayerRef.None);

        foreach (var p in Runner.ActivePlayers)
        {
            if (SlotOf(p) >= 0 || WatcherOf(p) >= 0) continue;
            if (Net.InMatch)
            {
                var rp = RoomPlayer.Of(p);
                if (rp == null || rp.Username.Length == 0) continue;
                int back = AwaySlotFor(KeyOf(rp));
                if (back >= 0) Return(back, p); else AddWatcher(p);
                continue;
            }
            if (!TakeSeat(p)) AddWatcher(p);
        }

        if (!Net.InMatch)
            for (int i = 0; i < RoomService.MaxPlayers; i++) { AwaySince.Set(i, 0f); Bot.Set(i, false); Returned.Set(i, false); }
    }

    bool TakeSeat(PlayerRef p)
    {
        for (int i = 0; i < RoomService.MaxPlayers; i++)
            if (Slots[i] == PlayerRef.None)
            {
                Slots.Set(i, p); Teams.Set(i, 0); Ready.Set(i, false);
                if (ColorTaken(i, Colors[i])) RPC_NextColor(i);
                return true;
            }
        return false;
    }

    bool AddWatcher(PlayerRef p)
    {
        for (int i = 0; i < RoomService.MaxSpectators; i++)
            if (Watchers[i] == PlayerRef.None) { Watchers.Set(i, p); return true; }
        return false;
    }

    int AwaySlotFor(string key)
    {
        for (int i = 0; i < RoomService.MaxPlayers; i++)
            if (AwaySince[i] > 0f && Ids[i].ToString() == key) return i;
        return -1;
    }

    void GoAway(int slot)
    {
        AwaySince.Set(slot, Mathf.Max(0.01f, Runner.SimulationTime));
        Returned.Set(slot, false);
        RPC_Notice(slot, 0);
    }

    void Return(int slot, PlayerRef p)
    {
        Slots.Set(slot, p);
        AwaySince.Set(slot, 0f);
        Bot.Set(slot, false);
        Returned.Set(slot, true);
        adopted.Remove(slot);
        DropBrain(slot);
        RPC_Notice(slot, 2);
    }

    public void ToggleWatch() => RPC_ToggleWatch(Runner.LocalPlayer);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_ToggleWatch(PlayerRef p)
    {
        if (Net.InMatch) return;
        int w = WatcherOf(p);
        if (w >= 0) { if (TakeSeat(p)) Watchers.Set(w, PlayerRef.None); return; }
        int s = SlotOf(p);
        if (s >= 0 && AddWatcher(p)) { Slots.Set(s, PlayerRef.None); Ready.Set(s, false); }
    }

    void RunAwayPlayers()
    {
        foreach (var e in NetEntity.All)
            if (e != null && e.Orphaned && (IsAway(e.OwnerSlot) || e.OwnerSlot == Creep.Team)) e.Claim();

        for (int slot = 0; slot < RoomService.MaxPlayers; slot++)
        {
            if (!IsAway(slot))
            {
                DropBrain(slot);
                var rp = Slots[slot] != PlayerRef.None ? RoomPlayer.Of(Slots[slot]) : null;
                if (rp != null && rp.Live)
                {
                    SavedCredits.Set(slot, rp.Credits);
                    SavedResearch.Set(slot, rp.Research.ToString());
                    for (int k = 0; k < StatCount; k++) SavedStats.Set(slot * StatCount + k, rp.Stats[k]);
                }
                continue;
            }
            int team = Net.ToTeam(slot);
            if (adopted.Add(slot))
            {
                TeamEconomy.Set(team, SavedCredits[slot]);
                ResearchManager.Restore(ParseResearch(SavedResearch[slot].ToString()), team);
            }
            else
            {
                SavedCredits.Set(slot, TeamEconomy.Credits(team));
                SavedResearch.Set(slot, ResearchIds(team));
            }
            if (!Bot[slot] && Runner.SimulationTime - AwaySince[slot] >= BotDelay) { Bot.Set(slot, true); RPC_Notice(slot, 1); }
            if (Bot[slot] && !brains.ContainsKey(slot) && MatchManager.Instance != null)
                brains[slot] = MatchManager.Instance.MakeBot(team, Bases[slot]);
        }
    }

    void DropBrain(int slot)
    {
        if (!brains.TryGetValue(slot, out var brain)) return;
        brains.Remove(slot);
        if (brain != null) Destroy(brain.gameObject);
    }

    public void LoadSaved(int slot)
    {
        TeamEconomy.Set(0, SavedCredits[slot]);
        ResearchManager.Restore(ParseResearch(SavedResearch[slot].ToString()), 0);
        for (int k = 0; k < StatCount && k < MatchStats.Values.Length; k++) MatchStats.Values[k] = SavedStats[slot * StatCount + k];
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_Notice(int slot, int kind)
    {
        string name = NameOf(slot);
        if (slot == SlotOf(Runner.LocalPlayer) && kind == 2) return;
        string text = kind switch
        {
            0 => $"{name} disconnected. A bot takes over in {BotDelay:0} s if they don't return.",
            1 => $"{name} has been replaced by a bot.",
            _ => $"{name} is back in the game.",
        };
        Alerts.Show(text, kind == 2 ? Alerts.Good : Alerts.Info, 6f);
    }

    static Dictionary<string, ResearchData> allResearch;

    static List<ResearchData> ParseResearch(string ids)
    {
        if (allResearch == null)
        {
            allResearch = new();
            foreach (var r in Resources.FindObjectsOfTypeAll<ResearchData>()) if (!string.IsNullOrEmpty(r.id)) allResearch[r.id] = r;
        }
        var list = new List<ResearchData>();
        foreach (var id in ids.Split(',')) if (allResearch.TryGetValue(id, out var r)) list.Add(r);
        return list;
    }

    public static string ResearchIds(int team)
    {
        var ids = new List<string>();
        foreach (var r in ResearchManager.DoneOf(team)) ids.Add(r.id);
        return string.Join(",", ids);
    }
}

public partial class RoomState
{
    const int MaxBans = 16;
    [Networked, Capacity(MaxBans)] NetworkArray<NetworkString<_64>> Banned => default;
    readonly HashSet<PlayerRef> sentAway = new();

    public void Kick(PlayerRef p, bool ban)
    {
        if (!IsHost || p == PlayerRef.None || p == Runner.LocalPlayer) return;
        if (ban && RoomPlayer.Of(p) is RoomPlayer rp) AddBan(KeyOf(rp));
        sentAway.Add(p);
        RPC_Kicked(p, ban);
    }

    void AddBan(string key)
    {
        for (int i = 0; i < MaxBans; i++) if (Banned[i].ToString() == key) return;
        for (int i = 0; i < MaxBans; i++) if (Banned[i].Length == 0) { Banned.Set(i, key); return; }
        for (int i = 1; i < MaxBans; i++) Banned.Set(i - 1, Banned[i]);
        Banned.Set(MaxBans - 1, key);
    }

    bool IsBanned(string key)
    {
        for (int i = 0; i < MaxBans; i++) if (Banned[i].Length > 0 && Banned[i].ToString() == key) return true;
        return false;
    }

    void SendAwayBanned()
    {
        foreach (var p in Runner.ActivePlayers)
        {
            if (p == Runner.LocalPlayer || sentAway.Contains(p)) continue;
            var rp = RoomPlayer.Of(p);
            if (rp == null || rp.Username.Length == 0 || !IsBanned(KeyOf(rp))) continue;
            sentAway.Add(p);
            RPC_Kicked(p, true);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_Kicked(PlayerRef player, NetworkBool ban)
    {
        if (player != Runner.LocalPlayer) return;
        RoomService.Instance.Kicked(ban ? "You are banned from this room." : "The host removed you from the room.");
    }
}
