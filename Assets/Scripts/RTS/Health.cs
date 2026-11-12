using System.Collections.Generic;
using UnityEngine;

public class Health : MonoBehaviour
{
    public static readonly List<Health> All = new();
    public static Vector3? LastBuildingDeath;
    public static float LastBuildingDeathAt;

    [SerializeField] float maxHealth = 100f;
    public int team;

    float baseMax;
    float current;
    NetEntity net;

    public float Fraction => current / maxHealth;
    public bool IsDamaged => current < maxHealth;
    public bool IsDead => current <= 0f;

    void Start()
    {
        if (!TryGetComponent(out OutpostCapture _)) TeamLook.Apply(gameObject, team);
        if (TryGetComponent(out SelectableUnit _))
        {
            gameObject.AddComponent<StatusFX>();
            gameObject.AddComponent<UnitMotion>();
        }
        hitFx = gameObject.AddComponent<HitFX>();
    }

    HitFX hitFx;

    void PlayDeath(UnitDeath death) { if (!AirCrash.Begin(this, death)) death.Die(); }

    void Awake()
    {
        if (TryGetComponent(out SelectableUnit unit) && unit.Data != null) maxHealth = unit.Data.maxHealth;
        baseMax = maxHealth;
        current = maxHealth;
        SpawnPos = transform.position;
        net = GetComponent<NetEntity>();
        if (TryGetComponent(out TeamMember tm)) tm.team = team;
        Stats.Changed += RefreshMax;
        RefreshMax();
    }

    void OnDestroy() => Stats.Changed -= RefreshMax;

    void RefreshMax()
    {
        float max = Stats.Value(Stat.MaxHealth, baseMax, this);
        if (Mathf.Approximately(max, maxHealth)) return;
        current *= max / maxHealth;
        maxHealth = max;
    }

    public System.Action onDeath;
    public void Revive() => current = maxHealth;
    public void SetFraction(float f) => current = Mathf.Clamp(f, 0.01f, 1f) * maxHealth;
    public Vector3 SpawnPos { get; private set; }
    public float MaxHealth => maxHealth;

    public bool CloakedFrom(Vector3 viewer) =>
        (transform.position - viewer).sqrMagnitude > CloakField.RevealRange * CloakField.RevealRange && CloakField.Covers(this) && !Revealer.Reveals(this);
    public float Current => current;
    public void Heal(float amount) => current = Mathf.Min(maxHealth, current + amount);

    [Tooltip("Map scenery (destructible walls): left out of Health.All, so nothing targets it on its own, fog doesn't hide it and splash skips it. Only a direct attack order hits it.")]
    public bool scenery;

    void OnEnable() { if (!scenery) All.Add(this); }
    void OnDisable() => All.Remove(this);

    public void SetFromNet(float hp, float max)
    {
        if (max > 0f) maxHealth = max;
        if (synced && hp < current && team != 0 && Net.Allied(team, 0) && TryGetComponent(out Structure _)) Alerts.AllyAttacked(this);
        if (synced && hp < current && hp > 0f && hitFx != null) hitFx.Hurt(current - hp);
        synced = true;
        current = hp;
    }
    bool synced;

    public void DieRemote()
    {
        enabled = false;
        if (scenery) Sfx.At(Snd.WallBreak, transform.position);
        if (TryGetComponent(out Structure _)) { Juice.BuildingExplosion(gameObject); LastBuildingDeath = transform.position; LastBuildingDeathAt = Time.time; }
        if (TryGetComponent(out SelectableUnit unit)) { unit.HideRing(); if (unit.TryGetComponent(out UnitMarker marker)) marker.Remove(); Sfx.At(Snd.BoomSmall, transform.position, 0.7f); }
        if (TryGetComponent(out UnitDeath death)) PlayDeath(death);
    }

    public static System.Func<Health, float, Health, bool> RemoteDamage;

    public void TakeDamage(float amount, Health from = null)
    {
        if (net != null && net.IsRemote) { if (!net.IsDead) net.RPC_Damage(amount, NetEntity.IdOf(from)); return; }
        if (RemoteDamage != null && net == null && RemoteDamage(this, amount, from)) return;
        if (IsDead) return;
        current = Mathf.Max(0f, current - amount);
        Minimap.Fight(transform.position, Net.Allied(team, 0));
        if (team == 0 && from != null && !Net.Allied(from.team, team)) Alerts.Attacked(this);
        if (IsDead) { Die(from); return; }
        Sfx.At(Snd.Hit, transform.position, 0.22f);
        if (scenery && Juice.Seen(transform.position))
        {
            Vector3 at = transform.position + Vector3.up * Random.Range(1f, 4f) + transform.right * Random.Range(-2.5f, 2.5f);
            Juice.Burst(at, Juice.Fire, 8, 5f, 0.12f, 0.4f, 1f, true);
            Particles.Emit("Dust", at, Vector3.up * 0.8f, 1.2f, 1f, new Color(0.5f, 0.45f, 0.5f, 0.5f));
        }
        if (hitFx != null) hitFx.Hurt(amount);
        if (from != null && TryGetComponent(out Attacker attacker)) attacker.Retaliate(from);
    }

    public void CreditKill()
    {
        if (team == 0) MatchStats.Add(MatchStats.Kind.Kills);
        if (!TryGetComponent(out SelectableUnit _)) return;
        if (!TryGetComponent(out Veterancy vet)) vet = gameObject.AddComponent<Veterancy>();
        vet.AddKill();
    }

    void Die(Health killer)
    {
        enabled = false;
        if (net != null) net.MarkDead();
        if (killer != null && !Net.Allied(killer.team, team))
        {
            if (killer.TryGetComponent(out NetEntity kn) && kn.IsRemote) kn.RPC_AddKill();
            else killer.CreditKill();
            if (killer.team == 0 && team != Creep.Team && TryGetComponent(out Structure _)) Sfx.Say(Line.Destroyed, 12f);
        }
        if (onDeath != null) { onDeath(); return; }
        if (scenery) Sfx.At(Snd.WallBreak, transform.position);
        if (team == 0)
        {
            if (TryGetComponent(out Structure s)) Alerts.Show($"Building destroyed: {s.Name}", Alerts.Bad);
            else Alerts.Show("Unit lost", Alerts.Bad, 5f);
            MatchStats.Add(TryGetComponent(out Structure _) ? MatchStats.Kind.BuildingsLost : MatchStats.Kind.UnitsLost);
        }
        if (TryGetComponent(out Structure _)) { Juice.BuildingExplosion(gameObject); LastBuildingDeath = transform.position; LastBuildingDeathAt = Time.time; }
        if (TryGetComponent(out SelectableUnit unit)) { unit.HideRing(); if (unit.TryGetComponent(out UnitMarker marker)) marker.Remove(); Sfx.At(Snd.BoomSmall, transform.position, 0.7f); }
        if (TryGetComponent(out UnitDeath death)) PlayDeath(death);
        else Net.Destroy(gameObject);
    }
}
