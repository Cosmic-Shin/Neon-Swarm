using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

// Network glue for one unit or building (multiplayer only; it sits on the network variant of the prefab).
// The owner (state authority) runs the game scripts as usual and publishes health, death, construction,
// upgrade level and veterancy rank. Every other PC gets a "remote" copy: game logic off, moved by
// NetworkTransform, mirroring that state. Damage, stuns and kill credit go to the owner by RPC.
[RequireComponent(typeof(Health))]
public class NetEntity : NetworkBehaviour
{
    public static readonly List<NetEntity> All = new();

    [Networked] public int OwnerSlot { get; set; }
    [Networked] Vector3 SpawnPos { get; set; }      // buildings have no NetworkTransform: this places them
    [Networked] Quaternion SpawnRot { get; set; }
    [Networked] float Hp { get; set; }
    [Networked] float MaxHp { get; set; }
    [Networked] NetworkBool Dead { get; set; }
    [Networked] float Build { get; set; }     // construction 0..1; -1 = not under construction
    [Networked] int Level { get; set; }       // building upgrade Mk level
    [Networked] int Rank { get; set; }        // veterancy rank (the glow)
    [Networked] int HarvestAnim { get; set; } // harvesters: Idle / Harvest / Unload
    [Networked] float Cargo { get; set; }     // harvesters: how full, 0..1

    // Scripts that only the owner runs (they move, shoot, spend or earn).
    static readonly System.Type[] OwnerOnly =
    {
        typeof(Attacker), typeof(Building), typeof(Harvester), typeof(Turret),
        typeof(Siphon), typeof(Repairer), typeof(FreeHarvester), typeof(EmpMine), typeof(Refinery),
    };
    const float DespawnDelay = 3f; // after death, so every PC sees it die (and wrecks get spawned) first

    Health health;
    Construction construction;
    bool built;     // remote: construction seen finished
    bool diedHere;  // remote: death already played

    public bool Online => Object != null && Object.IsValid;
    public bool IsRemote => Online && !HasStateAuthority;
    public bool IsDead => Online && Dead;
    public Health Health => health != null ? health : health = GetComponent<Health>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Hook()
    {
        All.Clear();
        // The pack's death effect ends by destroying the object; a networked one must be despawned by its owner.
        UnitDeath.RemoveHook = go => go.TryGetComponent(out NetEntity e) && e.Online && e.Removed();
    }

    // Net.Spawn, before anyone sees it.
    public void Init(int slot, Vector3 pos, Quaternion rot)
    {
        OwnerSlot = slot;
        SpawnPos = pos;
        SpawnRot = rot;
        Build = -1f;
        Level = 1;
    }

    public override void Spawned()
    {
        All.Add(this);
        Health.team = Net.ToTeam(OwnerSlot);
        if (TryGetComponent(out TeamMember tm)) tm.team = Health.team;
        if (TryGetComponent(out SelectableUnit unit)) unit.ApplyTeamLook();
        if (!HasStateAuthority) MakeRemote();
        else if (TryGetComponent(out NavMeshAgent agent) && agent.enabled)
            agent.Warp(SpawnPos); // it was created at the map origin first and its agent snapped there

        // Neutral creeps: owned and run by the host; nobody can capture outposts with them.
        if (Health.team == Creep.Team)
        {
            if (TryGetComponent(out TeamMember t)) Destroy(t);
            if (HasStateAuthority) gameObject.AddComponent<Creep>();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState) => All.Remove(this);

    void MakeRemote()
    {
        transform.SetPositionAndRotation(SpawnPos, SpawnRot); // units: NetworkTransform takes over from here
        foreach (var type in OwnerOnly)
            if (TryGetComponent(type, out var c)) ((UnityEngine.Behaviour)c).enabled = false;
        if (TryGetComponent(out NavMeshAgent agent)) agent.enabled = false; // NetworkTransform moves it
        if (TryGetComponent(out SelectableUnit unit)) unit.IsRemote = true;
    }

    // ---- owner: publish ----
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        Hp = Health.Current;
        MaxHp = Health.MaxHealth;
        if (construction == null) construction = GetComponent<Construction>();
        Build = construction != null ? construction.Progress : -1f;
        if (TryGetComponent(out BuildingUpgrade up)) Level = up.level;
        if (TryGetComponent(out Veterancy vet)) Rank = vet.Rank;
        if (TryGetComponent(out Harvester harv)) { HarvestAnim = harv.AnimIndex; Cargo = harv.CargoFraction; }
    }

    // ---- everyone else: mirror ----
    public override void Render()
    {
        if (!IsRemote || diedHere) return;
        Health.SetFromNet(Hp, MaxHp);

        if (Build >= 0f && !built)
        {
            if (construction == null)
            {
                construction = gameObject.AddComponent<Construction>();
                construction.remote = true;
                construction.startProgress = Build;
            }
            construction.SetRemoteProgress(Build);
        }
        else if (Build < 0f && construction != null)
        {
            construction.FinishRemote();
            construction = null;
            built = true;
        }

        if (TryGetComponent(out BuildingUpgrade up) && Level > 0 && up.level != Level) up.SetLevel(Level, true);
        if (Rank > 0)
        {
            if (!TryGetComponent(out Veterancy vet)) vet = gameObject.AddComponent<Veterancy>();
            vet.ShowRank(Rank);
        }

        if (TryGetComponent(out Harvester harvester)) harvester.ShowRemote(HarvestAnim, Cargo);

        if (Dead) { diedHere = true; Health.DieRemote(); }
    }

    // ---- owner side of a death / removal ----
    public void MarkDead() { if (HasStateAuthority) Dead = true; }

    bool Removed()
    {
        if (HasStateAuthority) Despawn(DespawnDelay);
        return true; // remote: wait for the owner's despawn
    }

    public void Despawn(float delay)
    {
        if (!HasStateAuthority) return;
        if (delay <= 0f) Runner.Despawn(Object);
        else StartCoroutine(DespawnLater(delay));
    }

    IEnumerator DespawnLater(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (Online) Runner.Despawn(Object);
    }

    // ---- RPCs ----
    public static NetworkId IdOf(Component c) =>
        c != null && c.TryGetComponent(out NetEntity e) && e.Online ? e.Object.Id : default;

    Health Find(NetworkId id) => id.IsValid && Runner.TryFindObject(id, out var o) ? o.GetComponent<Health>() : null;

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Damage(float amount, NetworkId from) => Health.TakeDamage(amount, Find(from));

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Status(float stun, float slow)
    {
        if (!TryGetComponent(out SelectableUnit u)) return;
        if (stun > 0f) u.Stun(stun);
        if (slow > 0f) u.Slow(slow);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_AddKill()
    {
        if (!TryGetComponent(out Veterancy vet)) vet = gameObject.AddComponent<Veterancy>();
        vet.AddKill();
    }

    // Owner's factory let a unit out: everyone else opens the door too.
    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    public void RPC_OpenGate(float seconds) { if (TryGetComponent(out Building b)) b.OpenGate(seconds); }

    // Owner fired: everyone else replays the shot's look (no damage; that went by RPC_Damage).
    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    public void RPC_Shot(NetworkId a, NetworkId b, NetworkId c)
    {
        var first = Find(a);
        if (first == null) return;
        if (TryGetComponent(out Attacker attacker)) attacker.ShowShot(first);
        else if (TryGetComponent(out Turret turret))
        {
            var list = new List<Health> { first };
            if (Find(b) is Health hb) list.Add(hb);
            if (Find(c) is Health hc) list.Add(hc);
            turret.ShowShots(list);
        }
    }

    public void SendShot(Health a, Health b = null, Health c = null)
    {
        if (Online && HasStateAuthority) RPC_Shot(IdOf(a), IdOf(b), IdOf(c));
    }
}
