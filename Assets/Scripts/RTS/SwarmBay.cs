using System.Collections.Generic;
using UnityEngine;

// Locust Carrier: carries a hangar of attack drones (C&C carrier style). When it starts a fight the drones
// pour out one by one and go for its target; when the fight is over they fly home and dock. Lost drones are
// rebuilt in the hangar over time. If the carrier dies its drones burn out with it.
[RequireComponent(typeof(Attacker))]
public class SwarmBay : MonoBehaviour
{
    public SelectableUnit dronePrefab; // the Locust Swarm drone
    public int capacity = 4;
    public float rebuildTime = 6f;     // seconds per lost drone
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
        if (self.IsRemote || dronePrefab == null) return; // its owner runs the drones
        drones.RemoveAll(d => d == null || !d.TryGetComponent(out Health h) || h.IsDead);
        if (health.IsDead) { foreach (var d in drones) d.GetComponent<Health>().TakeDamage(1e6f); drones.Clear(); enabled = false; return; }

        // Rebuild lost drones.
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

        // No fight: come home and dock.
        foreach (var d in drones.ToArray())
        {
            if (d.TryGetComponent(out Attacker a) && a.HasTarget) continue; // still busy with something
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
        var d = Net.Spawn(dronePrefab.gameObject, at, transform.rotation).GetComponent<SelectableUnit>();
        Attacker.SetTeam(d, health.team);
        d.GetComponent<Health>().onDeath = () => Burnout(d); // no "unit lost" alert or wreck for a drone
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
