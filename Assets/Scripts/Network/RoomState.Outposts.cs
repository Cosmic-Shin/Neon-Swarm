using System.Collections.Generic;
using Fusion;
using UnityEngine;

// Outposts in multiplayer: the host runs every outpost (capture, shooting, its health) and publishes
// the owner, capture progress and health; everyone else shows that. Their shots at an outpost go to the
// host, and the host's outpost lasers are replayed on the other PCs. Garrisoning is off online.
// Owners are stored as player slot + 1 (0 = neutral), since teams are relative on each PC.
public partial class RoomState
{
    const int MaxOutposts = 16;
    [Networked, Capacity(MaxOutposts)] NetworkArray<int> OutOwner => default;
    [Networked, Capacity(MaxOutposts)] NetworkArray<int> OutCapturing => default;
    [Networked, Capacity(MaxOutposts)] NetworkArray<float> OutProgress => default;
    [Networked, Capacity(MaxOutposts)] NetworkArray<float> OutHp => default;
    [Networked] NetworkBool OutpostsReady { get; set; }
    const int MaxBarriers = 64; // destructible walls (map feature): the host runs their health too
    [Networked, Capacity(MaxBarriers)] NetworkArray<float> WallHp => default;
    static readonly List<Barrier> barriers = new(); // keeps destroyed ones (null) so the numbers stay put
    static bool barriersFound;

    static readonly List<OutpostCapture> outposts = new();

    // Numbered by position, the same on every PC (they are fixed scene objects).
    static void FindOutposts()
    {
        if (!barriersFound) // once per match, numbered by position like the outposts
        {
            barriersFound = true;
            barriers.AddRange(FindObjectsByType<Barrier>(FindObjectsSortMode.None));
            barriers.Sort((a, b) => a.transform.position.x != b.transform.position.x
                ? a.transform.position.x.CompareTo(b.transform.position.x)
                : a.transform.position.z.CompareTo(b.transform.position.z));
            if (barriers.Count > MaxBarriers) Debug.LogError($"[GD5] {barriers.Count} walls; only {MaxBarriers} are synced.");
        }
        outposts.RemoveAll(o => o == null);
        if (outposts.Count > 0) return;
        outposts.AddRange(FindObjectsByType<OutpostCapture>(FindObjectsSortMode.None));
        outposts.Sort((a, b) => a.transform.position.x != b.transform.position.x
            ? a.transform.position.x.CompareTo(b.transform.position.x)
            : a.transform.position.z.CompareTo(b.transform.position.z));
        if (outposts.Count > MaxOutposts) Debug.LogError($"[GD5] {outposts.Count} outposts; only {MaxOutposts} are synced.");
    }

    static int ToStored(int team) => team < 0 ? 0 : Net.ToSlot(team) + 1;
    static int FromStored(int v) => v <= 0 ? -1 : Net.ToTeam(v - 1);

    void SyncOutposts()
    {
        if (!Net.InMatch) return;
        FindOutposts();
        int n = Mathf.Min(outposts.Count, MaxOutposts);
        bool host = HasStateAuthority; // can change: the next player becomes host if this one leaves

        Outpost.ShotHook = host ? (o, target) =>
        {
            int i = outposts.IndexOf(o.GetComponent<OutpostCapture>());
            if (i >= 0) RPC_OutpostShot(i, NetEntity.IdOf(target));
        } : null;
        Health.RemoteDamage = host ? null : (h, amount, from) =>
        {
            int i = h.TryGetComponent(out OutpostCapture o) ? outposts.IndexOf(o) : -1;
            if (i >= 0 && i < n) { RPC_OutpostDamage(i, amount, NetEntity.IdOf(from)); return true; }
            int w = h.TryGetComponent(out Barrier b) ? barriers.IndexOf(b) : -1;
            if (w >= 0 && w < MaxBarriers) { RPC_BarrierDamage(w, amount); return true; }
            return false;
        };

        // Walls: the host publishes health (0 = broken); everyone else follows and breaks theirs.
        for (int w = 0; w < barriers.Count && w < MaxBarriers; w++)
        {
            var b = barriers[w];
            var hp = b != null ? b.GetComponent<Health>() : null;
            if (host) WallHp.Set(w, hp != null && !hp.IsDead ? hp.Current : 0f);
            else if (OutpostsReady && hp != null && !hp.IsDead)
            {
                if (WallHp[w] <= 0f) { hp.SetFromNet(0f, hp.MaxHealth); hp.DieRemote(); }
                else hp.SetFromNet(WallHp[w], hp.MaxHealth);
            }
        }

        for (int i = 0; i < n; i++)
        {
            var o = outposts[i];
            o.remote = !host;
            var hp = o.GetComponent<Health>();
            if (host)
            {
                OutOwner.Set(i, ToStored(o.ownerTeam));
                OutCapturing.Set(i, ToStored(o.capturingTeam));
                OutProgress.Set(i, o.progress);
                OutHp.Set(i, hp.Current);
            }
            else if (OutpostsReady)
            {
                o.SetRemote(FromStored(OutOwner[i]), FromStored(OutCapturing[i]), OutProgress[i]);
                hp.SetFromNet(OutHp[i], hp.MaxHealth);
            }
        }
        if (host) OutpostsReady = true;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_OutpostDamage(int index, float amount, NetworkId from)
    {
        if (index >= outposts.Count || outposts[index] == null) return;
        Health attacker = from.IsValid && Runner.TryFindObject(from, out var obj) ? obj.GetComponent<Health>() : null;
        outposts[index].GetComponent<Health>().TakeDamage(amount, attacker);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    void RPC_OutpostShot(int index, NetworkId target)
    {
        if (index >= outposts.Count || outposts[index] == null) return;
        if (target.IsValid && Runner.TryFindObject(target, out var obj))
            outposts[index].GetComponent<Outpost>().ShowShot(obj.GetComponent<Health>());
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_BarrierDamage(int index, float amount)
    {
        if (index < barriers.Count && barriers[index] != null) barriers[index].GetComponent<Health>().TakeDamage(amount);
    }

    static void ClearOutposts()
    {
        outposts.Clear();
        barriers.Clear();
        barriersFound = false;
        Outpost.ShotHook = null;
        Health.RemoteDamage = null;
    }
}
