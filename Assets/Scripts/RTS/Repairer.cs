using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class Repairer : MonoBehaviour
{
    [SerializeField] float range = 10f;
    [SerializeField] float healPerSecond = 8f;

    const int BeamPoints = 8;
    Health self;
    readonly List<LineRenderer> beams = new();
    float nextSparkle;

    void Awake() => self = GetComponent<Health>();

    void OnDisable() { foreach (var b in beams) if (b != null) b.enabled = false; }

    void Update()
    {
        float r2 = range * range;
        float heal = Stats.Value(Stat.RepairRate, healPerSecond, this) * Time.deltaTime;
        int used = 0;
        bool sparkle = Time.time >= nextSparkle;
        if (sparkle) nextSparkle = Time.time + 0.25f;
        bool seen = Juice.Seen(transform.position);
        foreach (var h in Health.All)
            if (h != self && h.team == self.team && h.IsDamaged && h.TryGetComponent(out SelectableUnit _)
                && (h.transform.position - transform.position).sqrMagnitude <= r2)
            {
                h.Heal(heal);
                if (!seen) continue;
                Beam(used++, h);
                if (sparkle) Juice.Burst(h.transform.position + Vector3.up * 1.5f, Juice.Heal, 2, 1.5f, 0.2f, 0.6f, 3f);
            }
        for (int i = used; i < beams.Count; i++) beams[i].enabled = false;
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
            lr.startColor = new Color(Juice.Heal.r, Juice.Heal.g, Juice.Heal.b, 0.5f);
            lr.endColor = Juice.Heal;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beams.Add(lr);
        }
        var beam = beams[i];
        beam.enabled = true;
        Vector3 from = transform.position + Vector3.up * 1.5f, to = target.transform.position + Vector3.up * 1f;
        Vector3 side = Vector3.Cross(to - from, Vector3.up).normalized;
        for (int p = 0; p < BeamPoints; p++)
        {
            float k = p / (BeamPoints - 1f);
            float wave = Mathf.Sin(k * 10f - Time.time * 12f) * 0.25f * Mathf.Sin(k * Mathf.PI);
            beam.SetPosition(p, Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.8f + side * wave);
        }
    }
}
