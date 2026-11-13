using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Health))]
public class NetEntity : NetworkBehaviour, IStateAuthorityChanged
{
    public static readonly List<NetEntity> All = new();

    [Networked] public int OwnerSlot { get; set; }
    [Networked] Vector3 SpawnPos { get; set; }
    [Networked] Quaternion SpawnRot { get; set; }
    [Networked] float Hp { get; set; }
    [Networked] float MaxHp { get; set; }
    [Networked] NetworkBool Dead { get; set; }
    [Networked] float Build { get; set; }
    [Networked] int Level { get; set; }
    [Networked] float Upgrading { get; set; }
    [Networked] NetworkBool Stunned { get; set; }
    [Networked] NetworkBool Repairing { get; set; }
    [Networked] int Rank { get; set; }
    [Networked] int HarvestAnim { get; set; }
    [Networked] float Cargo { get; set; }
    [Networked] int Producing { get; set; }
    [Networked] float Produced { get; set; }

    static readonly System.Type[] OwnerOnly =
    {
        typeof(Attacker), typeof(Building), typeof(Harvester), typeof(Turret),
        typeof(Siphon), typeof(Repairer), typeof(FreeHarvester), typeof(EmpMine), typeof(Refinery),
    };
    const float DespawnDelay = 3f;

    Health health;
    Construction construction;
    bool built;
    bool diedHere;

    public bool Online => Object != null && Object.IsValid;
    public bool IsRemote => Online && !HasStateAuthority;
    public bool IsDead => Online && Dead;
    public Health Health => health != null ? health : health = GetComponent<Health>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Hook()
    {
        All.Clear();
        UnitDeath.RemoveHook = go => go.TryGetComponent(out NetEntity e) && e.Online && e.Removed();
    }

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
        RefreshTeam();
        if (!HasStateAuthority) { transform.SetPositionAndRotation(SpawnPos, SpawnRot); MakeRemote(); }
        else if (TryGetComponent(out NavMeshAgent agent) && agent.enabled)
            agent.Warp(SpawnPos);

        if (Health.team == Creep.Team)
        {
            if (TryGetComponent(out TeamMember t)) Destroy(t);
            if (HasStateAuthority) gameObject.AddComponent<Creep>();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        All.Remove(this);
        if (hasState && !HasStateAuthority && !Dead && Net.InMatch && TryGetComponent(out Structure _)) Juice.Sell(gameObject, 0);
    }

    public void RefreshTeam()
    {
        Health.team = Net.ToTeam(OwnerSlot);
        if (TryGetComponent(out TeamMember tm)) tm.team = Health.team;
        if (TryGetComponent(out SelectableUnit unit)) unit.ApplyTeamLook();
    }

    bool remoteMode;

    void MakeRemote()
    {
        remoteMode = true;
        if (construction != null && !construction.remote) { construction.Complete(); construction = null; }
        foreach (var type in OwnerOnly)
            if (TryGetComponent(type, out var c)) ((UnityEngine.Behaviour)c).enabled = false;
        if (TryGetComponent(out NavMeshAgent agent)) agent.enabled = false;
        if (TryGetComponent(out SelectableUnit unit)) unit.IsRemote = true;
    }

    void MakeLocal()
    {
        remoteMode = false;
        if (Dead) return;
        if (construction != null) { construction.FinishRemote(); construction = null; built = true; }
        Health.SetFromNet(Hp, MaxHp);
        foreach (var type in OwnerOnly)
            if (TryGetComponent(type, out var c)) ((UnityEngine.Behaviour)c).enabled = true;
        if (TryGetComponent(out SelectableUnit unit)) unit.IsRemote = false;
        if (TryGetComponent(out NavMeshAgent agent))
        {
            agent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out var hit, 6f, NavMesh.AllAreas)) agent.Warp(hit.position);
        }
        if (Rank > 0)
        {
            if (!TryGetComponent(out Veterancy vet)) vet = gameObject.AddComponent<Veterancy>();
            var ranks = VeterancyData.Get.ranks;
            vet.SetKills(ranks[Mathf.Min(Rank, ranks.Length - 1)].kills, false);
        }
    }

    public void StateAuthorityChanged()
    {
        if (HasStateAuthority) { if (remoteMode) MakeLocal(); }
        else if (!remoteMode) MakeRemote();
    }

    public bool Orphaned => Online && Object.StateAuthority == PlayerRef.None;
    public void Claim() { if (Online && !HasStateAuthority) Object.RequestStateAuthority(); }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        Hp = Health.Current;
        MaxHp = Health.MaxHealth;
        if (construction == null) construction = GetComponent<Construction>();
        Build = construction != null ? construction.Progress : -1f;
        if (TryGetComponent(out BuildingUpgrade up)) { Level = up.level; Upgrading = up.IsUpgrading ? up.Progress + 1f : 0f; }
        if (TryGetComponent(out Veterancy vet)) Rank = vet.Rank;
        if (TryGetComponent(out Harvester harv)) { HarvestAnim = harv.AnimIndex; Cargo = harv.CargoFraction; }
        if (TryGetComponent(out Building fac)) { Producing = fac.Producing + 1; Produced = fac.Progress01; }
        if (TryGetComponent(out SelectableUnit su)) Stunned = su.IsStunned;
        if (TryGetComponent(out Structure st)) Repairing = st.Repairing;
    }

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
        if (TryGetComponent(out UpgradeFX ufx)) ufx.Remote = Upgrading > 0f ? Upgrading - 1f : null;
        if (Rank > 0)
        {
            if (!TryGetComponent(out Veterancy vet)) vet = gameObject.AddComponent<Veterancy>();
            vet.ShowRank(Rank);
        }

        if (TryGetComponent(out Harvester harvester)) harvester.ShowRemote(HarvestAnim, Cargo);
        if (TryGetComponent(out Building factory)) factory.ShowProduction(Producing - 1, Produced);
        if (TryGetComponent(out StatusFX status)) status.RemoteStunned = Stunned;
        if (TryGetComponent(out Structure building)) building.RemoteRepairing = Repairing;

        if (Dead) { diedHere = true; Health.DieRemote(); }
    }

    public void MarkDead() { if (HasStateAuthority) Dead = true; }

    bool Removed()
    {
        if (HasStateAuthority) Despawn(DespawnDelay);
        return true;
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
        Health.CreditKill();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
    public void RPC_OpenGate(float seconds) { if (TryGetComponent(out Building b)) b.OpenGate(seconds); }

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
