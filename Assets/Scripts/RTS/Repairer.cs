using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class Repairer : MonoBehaviour
{
    [SerializeField] float range = 10f;
    [SerializeField] float healPerSecond = 8f;
    public bool structures;
    public UnitData.Role[] roles = new UnitData.Role[0];
    public string beamFrom;
    public Color beamColor;
    public GameObject sparkFx;
    [Tooltip("Optional mesh beam, 1 unit long along +Z from its origin; stretched to each target every frame")]
    public GameObject beamMesh;
    public float beamWidth = 3f;
    [Tooltip("Optional Fx spawned on the ground under the target with each sparkle")]
    public GameObject groundRing;
    readonly List<Transform> meshes = new();
    Transform origin;

    const int BeamPoints = 8;
    Health self;
    readonly List<LineRenderer> beams = new();
    float nextSparkle;

    void Awake()
    {
        self = GetComponent<Health>();
        if (!string.IsNullOrEmpty(beamFrom)) foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == beamFrom) { origin = t; break; }
    }

    bool Wants(Health h)
    {
        if (h.TryGetComponent(out SelectableUnit u)) return roles == null || roles.Length == 0 || (u.Data != null && System.Array.IndexOf(roles, u.Data.role) >= 0);
        return structures && h.TryGetComponent(out Structure _) && !h.TryGetComponent(out Construction _);
    }

    Color Tint => beamColor.a > 0f ? beamColor : Juice.Heal;

    void OnDisable()
    {
        foreach (var b in beams) if (b != null) b.enabled = false;
        foreach (var m in meshes) if (m != null) m.gameObject.SetActive(false);
    }

    void Update()
    {
        float r2 = range * range;
        float heal = Stats.Value(Stat.RepairRate, healPerSecond, this) * Time.deltaTime;
        int used = 0;
        bool sparkle = Time.time >= nextSparkle;
        if (sparkle) nextSparkle = Time.time + 0.25f;
        bool seen = Juice.Seen(transform.position);
        foreach (var h in Health.All)
            if (h != self && h.team == self.team && h.IsDamaged && Wants(h)
                && (h.transform.position - transform.position).sqrMagnitude <= r2)
            {
                h.Heal(heal);
                if (!seen) continue;
                Beam(used++, h);
                if (sparkle) Juice.Burst(h.transform.position + Vector3.up * 1.5f, Tint, 2, 1.5f, 0.2f, 0.6f, 3f);
                if (sparkle && sparkFx != null) Fx.Spawn(sparkFx, h.transform.position + Vector3.up * (h.TryGetComponent(out Structure _) ? 2.5f : 1f), Quaternion.identity, 1f);
                if (sparkle && groundRing != null && Random.value < 0.5f)
                {
                    var p = h.transform.position;
                    float size = h.TryGetComponent(out Collider c) ? Mathf.Max(c.bounds.extents.x, c.bounds.extents.z) : 1f;
                    Fx.Spawn(groundRing, new Vector3(p.x, 0.1f, p.z), Quaternion.Euler(0f, Random.value * 360f, 0f), Mathf.Clamp(size, 0.8f, 6f));
                }
            }
        for (int i = used; i < beams.Count; i++) beams[i].enabled = false;
        for (int i = used; i < meshes.Count; i++) meshes[i].gameObject.SetActive(false);
    }

    void Beam(int i, Health target)
    {
        if (i >= beams.Count)
        {
            var lr = new GameObject("HealBeam").AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.gameObject.layer = gameObject.layer;
            lr.sharedMaterial = Resources.Load<Material>("HarvestFX");
            lr.positionCount = BeamPoints;
            lr.useWorldSpace = true;
            lr.startWidth = 0.12f;
            lr.endWidth = 0.25f;
            lr.startColor = new Color(Tint.r, Tint.g, Tint.b, 0.5f);
            lr.endColor = Tint;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beams.Add(lr);
        }
        var beam = beams[i];
        beam.enabled = true;
        Vector3 from = origin != null ? origin.position : transform.position + Vector3.up * 1.5f, to = target.transform.position + Vector3.up * (target.TryGetComponent(out Structure _) ? 2.5f : 1f);
        if (beamMesh != null)
        {
            if (i >= meshes.Count)
            {
                var m = Instantiate(beamMesh, transform).transform;
                m.name = "BeamMesh";
                meshes.Add(m);
            }
            var mesh = meshes[i];
            mesh.gameObject.SetActive(true);
            mesh.SetPositionAndRotation(from, Quaternion.LookRotation(to - from) * Quaternion.Euler(0f, 0f, Time.time * 360f));
            float w = beamWidth * (0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 9f, i));   // crackle
            mesh.localScale = Vector3.one;
            mesh.localScale = new Vector3(w / mesh.lossyScale.x, w / mesh.lossyScale.y, Vector3.Distance(from, to) / mesh.lossyScale.z);
            beam.startWidth = 0.05f;   // the line becomes a soft glow around the mesh
            beam.endWidth = 0.12f;
        }
        Vector3 side = Vector3.Cross(to - from, Vector3.up).normalized;
        for (int p = 0; p < BeamPoints; p++)
        {
            float k = p / (BeamPoints - 1f);
            float wave = Mathf.Sin(k * 10f - Time.time * 12f) * 0.25f * Mathf.Sin(k * Mathf.PI);
            beam.SetPosition(p, Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.8f + side * wave);
        }
    }
}
