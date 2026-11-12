using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;


public class Brood : MonoBehaviour
{
    const float Interval = 15f;
    const int BaseSize = 2;

    SelectableUnit unit;
    Attacker attacker;
    Health health;
    readonly List<SelectableUnit> brood = new();
    float next;

    public int Count { get { brood.RemoveAll(b => b == null); return brood.Count; } }
    public int Max => Mathf.RoundToInt(Stats.Value(Stat.BroodSize, BaseSize, this));
    public float NextIn => Mathf.Max(0f, next - Time.time);

    void Start()
    {
        unit = GetComponent<SelectableUnit>();
        attacker = GetComponent<Attacker>();
        health = GetComponent<Health>();
        next = Time.time + Interval;
    }

    void Update()
    {
        if (unit.IsRemote || !health.enabled || unit.Data.extra == null || Time.time < next) return;
        next = Time.time + Stats.Value(Stat.AbilityCooldown, Interval, this);
        brood.RemoveAll(b => b == null || b.GetComponent<Health>().IsDead);
        if (brood.Count >= Mathf.RoundToInt(Stats.Value(Stat.BroodSize, BaseSize, this))) return;

        Vector3 from = transform.position + transform.forward * 1.5f;
        Vector3 to = from + Quaternion.Euler(0f, Random.Range(-60f, 60f), 0f) * transform.forward * 5f;
        if (NavMesh.SamplePosition(to, out NavMeshHit hit, 6f, NavMesh.AllAreas)) to = hit.position;
        var hatchling = Net.Spawn(unit.Data.extra, from, transform.rotation).GetComponent<SelectableUnit>();
        Attacker.SetTeam(hatchling, health.team);
        hatchling.GetComponent<Health>().onDeath = () =>
        {
            if (hatchling == null) return;
            hatchling.HideRing();
            if (hatchling.TryGetComponent(out UnitDeath d)) d.Die(); else Net.Destroy(hatchling.gameObject);
        };
        hatchling.WalkOut(from, to, 8f, null, attacker != null && attacker.Target != null ? attacker.Target.transform.position : to);
        brood.Add(hatchling);
        if (Juice.Seen(from))
        {
            Color c = ProductionFX.HoloColor(gameObject);
            Juice.Burst(from + Vector3.up, c, 14, 3f, 0.15f, 0.5f, 1f);
            Juice.Ring(from + Vector3.up * 0.1f, c, 0.3f, 2.5f, 0.4f, 0.15f);
            Sfx.At(Snd.Ability, from, 0.5f);
        }
    }
}

public class Overheat : MonoBehaviour
{
    const float BaseBurst = 4f, VentTime = 3f;

    UnitData data;
    float heat;
    float ventingUntil;
    float nextPuff;

    public bool Ready => Time.time >= ventingUntil;
    public float Heat => heat;
    public float VentLeft => Mathf.Max(0f, ventingUntil - Time.time);
    public const float Vent = VentTime;

    void Awake() => data = GetComponent<SelectableUnit>().Data;

    public void Shot()
    {
        float burst = Stats.Value(Stat.BurstTime, BaseBurst, this);
        heat += data.attackCooldown / burst * Stats.Value(Stat.OverheatRate, 1f, this);
        if (heat < 1f) return;
        heat = 0f;
        ventingUntil = Time.time + VentTime;
        if (Juice.Seen(transform.position)) Juice.Burst(transform.position + Vector3.up * 1.5f, Juice.Fire, 12, 4f, 0.15f, 0.4f, 1f, true);
        Sfx.At(Snd.Vent, transform.position, 0.45f);
    }

    void Update()
    {
        if (Ready) { heat = Mathf.Max(0f, heat - Time.deltaTime * 0.25f); return; }
        if (Time.time < nextPuff || !Juice.Seen(transform.position)) return;
        nextPuff = Time.time + 0.08f;
        Particles.Emit("Steam", transform.position + Vector3.up * 1.6f + Random.insideUnitSphere * 0.4f, Vector3.up * 2.5f + Random.insideUnitSphere, 0.9f, 1.2f, new Color(0.85f, 0.88f, 0.95f, 0.45f));
    }
}

public class Charge : MonoBehaviour
{
    const float Cooldown = 10f, Duration = 1.2f, Boost = 2.6f;

    SelectableUnit unit;
    Attacker attacker;
    Health health;
    float ready, until = -1f, nextDust;

    public bool Charging => until > 0f;
    public float CooldownLeft => Mathf.Max(0f, ready - Time.time);
    public float CooldownTotal => Stats.Value(Stat.AbilityCooldown, Cooldown, this);

    void Start()
    {
        unit = GetComponent<SelectableUnit>();
        attacker = GetComponent<Attacker>();
        health = GetComponent<Health>();
    }

    void Update()
    {
        if (until > 0f)
        {
            if (Time.time >= until || !health.enabled) { until = -1f; unit.SetBoost(1f); return; }
            if (Time.time >= nextDust && Juice.Seen(transform.position))
            {
                nextDust = Time.time + 0.05f;
                Particles.Emit("Dust", transform.position - transform.forward + Random.insideUnitSphere * 0.5f, Vector3.up, 1.6f, 0.8f, new Color(0.55f, 0.5f, 0.45f, 0.5f));
            }
            return;
        }
        if (unit.IsRemote || !health.enabled || Time.time < ready || unit.IsStunned) return;
        var t = attacker.Target;
        if (t == null) return;
        float d = Vector3.Distance(t.transform.position, transform.position);
        if (d < unit.Data.range + 4f || d > 30f) return;

        ready = Time.time + Stats.Value(Stat.AbilityCooldown, Cooldown, this);
        until = Time.time + Duration;
        unit.SetBoost(Boost);
        if (Juice.Seen(transform.position))
        {
            Juice.Ring(transform.position + Vector3.up * 0.1f, Juice.Danger, 0.5f, 4f, 0.35f, 0.25f);
            Sfx.At(Snd.Ability, transform.position, 0.6f);
        }
    }

    void OnDisable() { if (until > 0f && unit != null) unit.SetBoost(1f); until = -1f; }
}
