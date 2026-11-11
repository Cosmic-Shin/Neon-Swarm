using System.Collections.Generic;
using UnityEngine;

// Hit points + team. Dies (is destroyed) at 0.
public class Health : MonoBehaviour
{
    public static readonly List<Health> All = new(); // every living thing, for target searches
    public static Vector3? LastBuildingDeath; // where (and when) a building last blew up: the match-end camera goes there
    public static float LastBuildingDeathAt;

    [SerializeField] float maxHealth = 100f;
    public int team; // 0 = player, 1 = enemy

    float baseMax; // before research / upgrades / veterancy
    float current;
    NetEntity net; // multiplayer: another player's copy forwards damage to its owner

    public float Fraction => current / maxHealth;
    public bool IsDamaged => current < maxHealth;
    public bool IsDead => current <= 0f;

    // Paint it in its owner's colour (network units know their team by now: NetEntity.Spawned runs first).
    void Start()
    {
        if (!TryGetComponent(out OutpostCapture _)) TeamLook.Apply(gameObject, team);
        if (TryGetComponent(out SelectableUnit _))
        {
            gameObject.AddComponent<StatusFX>(); // stunned / cloaked look
            gameObject.AddComponent<UnitMotion>(); // lean, footprints, dust
        }
        hitFx = gameObject.AddComponent<HitFX>(); // white flash, damage numbers, smoking when hurt
    }

    HitFX hitFx;

    // Death look: flyers crash first, everything else breaks apart where it is.
    void PlayDeath(UnitDeath death) { if (!AirCrash.Begin(this, death)) death.Die(); }

    void Awake()
    {
        // Units take their max health from their UnitData; anything else uses the field.
        if (TryGetComponent(out SelectableUnit unit) && unit.Data != null) maxHealth = unit.Data.maxHealth;
        baseMax = maxHealth;
        current = maxHealth;
        SpawnPos = transform.position;
        net = GetComponent<NetEntity>();
        if (TryGetComponent(out TeamMember tm)) tm.team = team; // the pack's outpost capture counts TeamMembers
        Stats.Changed += RefreshMax;
        RefreshMax();
    }

    void OnDestroy() => Stats.Changed -= RefreshMax;

    // Bonuses changed: keep the same fraction of a new max.
    void RefreshMax()
    {
        float max = Stats.Value(Stat.MaxHealth, baseMax, this);
        if (Mathf.Approximately(max, maxHealth)) return;
        current *= max / maxHealth;
        maxHealth = max;
    }

    public System.Action onDeath; // set = called instead of destroying (outposts fall back to neutral)
    public void Revive() => current = maxHealth;
    public void SetFraction(float f) => current = Mathf.Clamp(f, 0.01f, 1f) * maxHealth; // loading a save
    public Vector3 SpawnPos { get; private set; } // where it started (saves match scene objects by it)
    public float MaxHealth => maxHealth;

    // Inside a friendly cloak field, the viewer isn't close enough to spot it, and no enemy Uplink Mk II reveals it.
    public bool CloakedFrom(Vector3 viewer) =>
        (transform.position - viewer).sqrMagnitude > CloakField.RevealRange * CloakField.RevealRange && CloakField.Covers(this) && !Revealer.Reveals(this);
    public float Current => current;
    public void Heal(float amount) => current = Mathf.Min(maxHealth, current + amount);

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    // Multiplayer, someone else's object: its owner sends health and death.
    public void SetFromNet(float hp, float max)
    {
        if (max > 0f) maxHealth = max;
        if (synced && hp < current && team != 0 && Net.Allied(team, 0) && TryGetComponent(out Structure _)) Alerts.AllyAttacked(this);
        if (synced && hp < current && hp > 0f && hitFx != null) hitFx.Hurt(current - hp);
        synced = true;
        current = hp;
    }
    bool synced; // had a first value from the owner (so a lower one means damage)

    public void DieRemote()
    {
        enabled = false;
        if (TryGetComponent(out Structure _)) { Juice.BuildingExplosion(gameObject); LastBuildingDeath = transform.position; LastBuildingDeathAt = Time.time; }
        if (TryGetComponent(out SelectableUnit unit)) { unit.HideRing(); if (unit.TryGetComponent(out UnitMarker marker)) marker.Remove(); Sfx.At(Snd.BoomSmall, transform.position, 0.7f); }
        if (TryGetComponent(out UnitDeath death)) PlayDeath(death); // just the look; the owner despawns it
    }

    // from: who shot us, so an idle unit can shoot back (and gets the kill).
    /// <summary>Multiplayer: damage to scene objects another PC runs (outposts). True = it was sent there.</summary>
    public static System.Func<Health, float, Health, bool> RemoteDamage;

    public void TakeDamage(float amount, Health from = null)
    {
        if (net != null && net.IsRemote) { if (!net.IsDead) net.RPC_Damage(amount, NetEntity.IdOf(from)); return; } // owner applies it
        if (RemoteDamage != null && net == null && RemoteDamage(this, amount, from)) return; // multiplayer: a host-run outpost
        if (IsDead) return;
        current = Mathf.Max(0f, current - amount);
        Minimap.Fight(transform.position, Net.Allied(team, 0)); // a flash on the minimap where the fighting is
        if (team == 0 && from != null && !Net.Allied(from.team, team)) Alerts.Attacked(this);
        if (IsDead) { Die(from); return; }
        if (hitFx != null) hitFx.Hurt(amount);
        if (from != null && TryGetComponent(out Attacker attacker)) attacker.Retaliate(from);
    }

    // This one (ours, or a host-run creep) made a kill: the match score, and veterancy for units.
    public void CreditKill()
    {
        if (team == 0) MatchStats.Add(MatchStats.Kind.Kills);
        if (!TryGetComponent(out SelectableUnit _)) return;
        if (!TryGetComponent(out Veterancy vet)) vet = gameObject.AddComponent<Veterancy>();
        vet.AddKill();
    }

    void Die(Health killer)
    {
        enabled = false; // leaves Health.All, so nobody keeps targeting the wreck
        if (net != null) net.MarkDead(); // multiplayer: everyone plays the death
        if (killer != null && !Net.Allied(killer.team, team))
        {
            if (killer.TryGetComponent(out NetEntity kn) && kn.IsRemote) kn.RPC_AddKill(); // another player's: its owner counts it
            else killer.CreditKill();
        }
        if (onDeath != null) { onDeath(); return; }
        if (team == 0)
        {
            if (TryGetComponent(out Structure s)) Alerts.Show($"Building destroyed: {s.Name}", Alerts.Bad);
            else Alerts.Show("Unit lost", Alerts.Bad, 5f);
            MatchStats.Add(TryGetComponent(out Structure _) ? MatchStats.Kind.BuildingsLost : MatchStats.Kind.UnitsLost);
        }
        if (TryGetComponent(out Structure _)) { Juice.BuildingExplosion(gameObject); LastBuildingDeath = transform.position; LastBuildingDeathAt = Time.time; }
        if (TryGetComponent(out SelectableUnit unit)) { unit.HideRing(); if (unit.TryGetComponent(out UnitMarker marker)) marker.Remove(); Sfx.At(Snd.BoomSmall, transform.position, 0.7f); } // or it flies off as debris
        // Pack effect: neon flickers out, the model breaks into physics debris.
        if (TryGetComponent(out UnitDeath death)) PlayDeath(death);
        else Net.Destroy(gameObject);
    }
}
