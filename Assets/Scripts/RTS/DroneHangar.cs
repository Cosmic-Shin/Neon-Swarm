using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class DroneHangar : MonoBehaviour
{
    public SelectableUnit dronePrefab;
    public int capacity = 4;
    public float rebuildTime = 12f;
    public float guardRadius = 32f;
    public float launchGap = 0.4f;

    readonly List<SelectableUnit> drones = new();
    int stock;
    float rebuildAt, nextLaunch, nextScan;
    Health self, target;
    NetEntity net;
    Transform pad;
    Color color;

    public int Ready => stock;
    public int Out => drones.Count;
    public bool Busy => target != null || drones.Count > 0;
    public Health Target => target;
    public float LastLaunch { get; private set; } = -99f;
    public float LastDock { get; private set; } = -99f;

    void Awake()
    {
        self = GetComponent<Health>();
        net = GetComponent<NetEntity>();
        stock = capacity;
        foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Hangar_Pad")) { pad = t; break; }
    }

    void Start() => color = ProductionFX.HoloColor(gameObject);

    void Update()
    {
        if ((net != null && net.IsRemote) || dronePrefab == null || TryGetComponent(out Construction _)) return;
        drones.RemoveAll(d => d == null || !d.TryGetComponent(out Health h) || h.IsDead);
        if (self.IsDead) { foreach (var d in drones) d.GetComponent<Health>().TakeDamage(1e6f); drones.Clear(); enabled = false; return; }

        bool powered = !Structure.LowPowerOf(self.team);
        if (stock + drones.Count < capacity && powered) { if (Time.time >= rebuildAt) { stock++; rebuildAt = Time.time + rebuildTime; } }
        else if (stock + drones.Count >= capacity) rebuildAt = Time.time + rebuildTime;

        if (Time.time >= nextScan) { nextScan = Time.time + 0.3f; target = Scan(); }

        if (target != null)
        {
            if (stock > 0 && powered && Time.time >= nextLaunch) Launch();
            foreach (var d in drones)
                if (d.TryGetComponent(out Attacker a) && a.Target != target && a.CanHit(target)) a.Attack(target);
            return;
        }

        foreach (var d in drones.ToArray())
        {
            if (d.TryGetComponent(out Attacker a) && a.HasTarget && Near(a.Target.transform.position, guardRadius * 1.3f)) continue;
            if (a != null) a.Stop();
            Vector3 to = Home - d.transform.position; to.y = 0f;
            if (to.magnitude < 3f) { Dock(d); continue; }
            if (d.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent) && agent.isOnNavMesh && (agent.destination - Home).sqrMagnitude > 4f)
                agent.SetDestination(Home);
        }
    }

    Vector3 Home => pad != null ? pad.position : transform.position;

    bool Near(Vector3 p, float r) { Vector3 d = p - transform.position; d.y = 0f; return d.sqrMagnitude <= r * r; }

    Health Scan()
    {
        Health best = null;
        float bestD = float.MaxValue;
        foreach (var h in Health.All)
        {
            if (h == null || h.IsDead || Net.Allied(h.team, self.team) || h.CloakedFrom(transform.position)) continue;
            Vector3 d = h.transform.position - transform.position; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq > guardRadius * guardRadius) continue;
            if (h.TryGetComponent(out Structure _)) sq += 400f;
            if (sq < bestD) { bestD = sq; best = h; }
        }
        return best;
    }

    void Launch()
    {
        stock--;
        nextLaunch = Time.time + launchGap;
        LastLaunch = Time.time;
        Vector2 r = Random.insideUnitCircle * 1.5f;
        Vector3 at = Home + new Vector3(r.x, 0f, r.y);
        var d = Net.SpawnFor(self.team, dronePrefab.gameObject, at, transform.rotation).GetComponent<SelectableUnit>();
        Attacker.SetTeam(d, self.team);
        d.GetComponent<Health>().onDeath = () => Burnout(d);
        drones.Add(d);
        Juice.Burst(at + Vector3.up * 2f, color, 8, 6f, 0.12f, 0.4f);
        Sfx.At(Snd.UnitReady, at, 0.4f);
    }

    void Dock(SelectableUnit d)
    {
        drones.Remove(d);
        stock = Mathf.Min(capacity, stock + 1);
        LastDock = Time.time;
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
