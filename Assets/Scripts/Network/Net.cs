using Fusion;
using UnityEngine;

public static class Net
{
    public static NetworkRunner Runner => RoomService.Instance != null ? RoomService.Instance.Runner : null;
    public static bool InMatch;
    public static bool Online => InMatch && Runner != null && Runner.IsRunning;
    static bool InRoom => Runner != null && Runner.IsRunning && RoomState.Instance != null;

    public static int LocalSlot => InRoom ? Mathf.Max(0, RoomState.Instance.SlotOf(Runner.LocalPlayer)) : 0;
    public static bool Watching => InRoom && RoomState.Instance.SlotOf(Runner.LocalPlayer) < 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { InMatch = false; table = null; }

    static bool IsSlot(int slot) => slot >= 0 && slot < RoomService.MaxPlayers;

    public static int ToTeam(int slot)
    {
        if (!InRoom || !IsSlot(slot)) return slot;
        return Watching ? slot + 1 : (slot - LocalSlot + RoomService.MaxPlayers) % RoomService.MaxPlayers;
    }

    public static int ToSlot(int team)
    {
        if (!InRoom) return team;
        if (Watching) return team >= 1 && team <= RoomService.MaxPlayers ? team - 1 : team;
        return IsSlot(team) ? (team + LocalSlot) % RoomService.MaxPlayers : team;
    }

    public static Color ColorOf(int team) => team == Creep.Team ? CreepColor : team < 0 ? Color.white : TeamMember.ColorOf(ToSlot(team));
    public static readonly Color CreepColor = new(1f, 0.55f, 0.1f);

    public static bool Allied(int a, int b)
    {
        if (a == b) return true;
        if (!InRoom) return Skirmish.Allied(a, b);
        int sa = ToSlot(a), sb = ToSlot(b);
        if (!IsSlot(sa) || !IsSlot(sb)) return false;
        int ta = RoomState.Instance.TeamOf(sa);
        return ta != 0 && ta == RoomState.Instance.TeamOf(sb);
    }

    public static bool LowPower(int team)
    {
        if (team == 0 && !Watching) return Structure.LowPower;
        if (!InRoom) return Structure.LowPowerOf(team);
        int slot = ToSlot(team);
        if (!IsSlot(slot)) return false;
        var rp = RoomState.Instance.IsAway(slot) ? null : RoomPlayer.Of(RoomState.Instance.Slots[slot]);
        return rp != null ? rp.LowPower : Structure.LowPowerOf(team);
    }

    public static string NameOf(int team) => RoomState.Instance != null ? RoomState.Instance.NameOf(ToSlot(team)) : $"Player {team + 1}";

    static NetPrefabs table;

    public static GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, int slot = -1)
    {
        if (!Online) return Object.Instantiate(prefab, pos, rot);
        if (table == null) table = Resources.Load<NetPrefabs>("NetPrefabs");
        var net = table != null ? table.Find(prefab) : null;
        if (net == null)
        {
            Debug.LogError($"[Net] No network version of {prefab.name}; spawning it locally only.");
            return Object.Instantiate(prefab, pos, rot);
        }
        if (slot < 0) slot = LocalSlot;
        return Runner.Spawn(net, pos, rot, null, (r, o) => o.GetComponent<NetEntity>().Init(slot, pos, rot)).gameObject;
    }

    public static T Spawn<T>(T prefab, Vector3 pos, Quaternion rot) where T : Component =>
        Spawn(prefab.gameObject, pos, rot).GetComponent<T>();

    public static GameObject SpawnFor(int team, GameObject prefab, Vector3 pos, Quaternion rot) => Spawn(prefab, pos, rot, ToSlot(team));

    public static T SpawnFor<T>(int team, T prefab, Vector3 pos, Quaternion rot) where T : Component =>
        SpawnFor(team, prefab.gameObject, pos, rot).GetComponent<T>();

    public static void Destroy(GameObject go)
    {
        if (go.TryGetComponent(out NetEntity e) && e.Online) e.Despawn(0f);
        else Object.Destroy(go);
    }
}
