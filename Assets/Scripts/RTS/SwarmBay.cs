using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Attacker))]
public class SwarmBay : MonoBehaviour
{
    public SelectableUnit dronePrefab;
    public int capacity = 4;
    public float rebuildTime = 6f;
    public float launchGap = 0.35f;

    readonly List<SelectableUnit> drones = new();
    int stock;
    float rebuildAt, nextLaunch;
    Attacker attacker;
    SelectableUnit self;
    Health health;
    Color color;

    void Awake()
    {
        attacker = GetComponent<Attacker>();
        self = GetComponent<SelectableUnit>();
        health = GetComponent<Health>();
        stock = capacity;
    }

    void Start() => color = ProductionFX.HoloColor(gameObject);

    void Update()
    {
        if (self.IsRemote || dronePrefab == null) return;
        drones.RemoveAll(d => d == null || !d.TryGetComponent(out Health h) || h.IsDead);
        if (health.IsDead) { foreach (var d in drones) d.GetComponent<Health>().TakeDamage(1e6f); drones.Clear(); enabled = false; return; }

        if (stock + drones.Count < capacity) { if (Time.time >= rebuildAt) { stock++; rebuildAt = Time.time + rebuildTime; } }
        else rebuildAt = Time.time + rebuildTime;

        Health target = attacker.Target;
        if (target != null)
        {
            if (stock > 0 && Time.time >= nextLaunch) Launch();
            foreach (var d in drones)
                if (d.TryGetComponent(out Attacker a) && a.Target != target && a.CanHit(target)) a.Attack(target);
            return;
        }

        foreach (var d in drones.ToArray())
        {
            if (d.TryGetComponent(out Attacker a) && a.HasTarget) continue;
            Vector3 to = transform.position - d.transform.position; to.y = 0f;
            if (to.magnitude < 3f) { Dock(d); continue; }
            if (d.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent) && agent.isOnNavMesh && (agent.destination - transform.position).sqrMagnitude > 4f)
                agent.SetDestination(transform.position);
        }
    }

    void Launch()
    {
        stock--;
        nextLaunch = Time.time + launchGap;
        Vector2 r = Random.insideUnitCircle * 1.5f;
        Vector3 at = transform.position + new Vector3(r.x, 0f, r.y);
        var d = Net.SpawnFor(health.team, dronePrefab.gameObject, at, transform.rotation).GetComponent<SelectableUnit>();
        Attacker.SetTeam(d, health.team);
        d.GetComponent<Health>().onDeath = () => Burnout(d);
        drones.Add(d);
        Juice.Burst(Flyer.Center(this), color, 8, 6f, 0.12f, 0.4f);
    }

    void Dock(SelectableUnit d)
    {
        drones.Remove(d);
        stock = Mathf.Min(capacity, stock + 1);
        Juice.Ring(Flyer.Center(d), color, 0.3f, 1.5f, 0.3f, 0.1f);
        Net.Destroy(d.gameObject);
    }

    static void Burnout(SelectableUnit d)
    {
        if (d == null) return;
        Juice.Burst(Flyer.Center(d), Juice.Fire, 10, 5f, 0.12f, 0.5f, 0f, true);
        d.HideRing();
        Net.Destroy(d.gameObject);
    }
}
