using UnityEngine;

/// <summary>Locust Swarm Carrier: Release() launches mini attack drones that orbit the carrier, then burn out.</summary>
public class SwarmCarrier : MonoBehaviour
{
    public int dronesPerRelease = 6;
    public float droneLifetime = 12f;
    public float orbitRadius = 1.2f;
    public float cooldown = 15f;
    float readyAt;
    static GameObject dronePrefab; static bool loaded;

    public bool Ready => Time.time >= readyAt;

    public bool Release()
    {
        if (!Ready) return false;
        if (!loaded) { loaded = true; dronePrefab = Resources.Load<GameObject>("Air/LocustDrone"); }
        if (!dronePrefab) { Debug.LogWarning("[CyberpunkRTS] Resources/Air/LocustDrone missing. Run Tools > Cyberpunk RTS > 1. Setup."); return false; }
        readyAt = Time.time + cooldown;
        var team = GetComponent<TeamMember>();
        for (int i = 0; i < dronesPerRelease; i++)
        {
            var d = Instantiate(dronePrefab, transform.position, transform.rotation);
            var fu = d.GetComponent<FlyingUnit>(); if (fu) Destroy(fu);   // drones follow the carrier instead
            var tm = d.GetComponent<TeamMember>(); if (tm && team) tm.team = team.team;
            var s = d.AddComponent<LocustSwarmDrone>();
            s.carrier = transform; s.angle = i * 360f / dronesPerRelease; s.radius = orbitRadius * Random.Range(0.8f, 1.2f); s.life = droneLifetime;
            var a = d.GetComponentInChildren<Animator>(); if (a) a.SetFloat("Speed", 1f);
        }
        return true;
    }
}

public class LocustSwarmDrone : MonoBehaviour
{
    public Transform carrier;
    public float angle, radius = 1.2f, life = 12f, orbitSpeed = 160f;
    float seed;
    void Start() { seed = Random.value * 10f; }
    void Update()
    {
        life -= Time.deltaTime;
        if (!carrier || life <= 0f) { Destroy(gameObject); return; }
        angle += orbitSpeed * Time.deltaTime;
        Vector3 target = carrier.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius
                         + Vector3.up * Mathf.Sin(Time.time * 3f + seed) * 0.25f;
        Vector3 dir = target - transform.position;
        transform.position = Vector3.Lerp(transform.position, target, 6f * Time.deltaTime);
        if (dir.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(dir);
    }
}
