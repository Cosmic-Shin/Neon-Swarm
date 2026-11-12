using System.Collections.Generic;
using UnityEngine;

public class Radar : MonoBehaviour
{
    static readonly List<Radar> active = new();
    static readonly Color Scan = new(0.3f, 1f, 0.9f);
    const float SweepLength = 30f, SweepSpeed = 120f, PulseEvery = 3f;

    Health health;
    LineRenderer sweep;
    float angle, nextPulse, top = -1f;

    void Awake()
    {
        health = GetComponent<Health>();
        sweep = new GameObject("RadarSweep").AddComponent<LineRenderer>();
        sweep.transform.SetParent(transform, false);
        sweep.sharedMaterial = Resources.Load<Material>("HarvestFX");
        sweep.positionCount = 2;
        sweep.useWorldSpace = true;
        sweep.startWidth = 0.15f;
        sweep.endWidth = 1.2f;
        sweep.startColor = new Color(Scan.r, Scan.g, Scan.b, 0.8f);
        sweep.endColor = new Color(Scan.r, Scan.g, Scan.b, 0f);
        sweep.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sweep.enabled = false;
    }

    void OnEnable() => active.Add(this);
    void OnDisable() { active.Remove(this); sweep.enabled = false; }

    public static bool Active => !Structure.LowPower && active.Exists(r => r.health.team == 0);

    void Update()
    {
        bool on = health.enabled && !Net.LowPower(health.team);
        sweep.enabled = on;
        if (!on) return;
        sweep.gameObject.layer = gameObject.layer;
        if (top < 0f) top = ConstructionSite.RendererBounds(gameObject).max.y;

        angle += SweepSpeed * Time.deltaTime;
        Vector3 from = new(transform.position.x, top, transform.position.z);
        Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        sweep.SetPosition(0, from);
        sweep.SetPosition(1, from + dir * SweepLength + Vector3.down * (top - 0.5f));

        if (Time.time < nextPulse || !Juice.Seen(from)) return;
        nextPulse = Time.time + PulseEvery;
        Juice.Ring(from, Scan, 1f, SweepLength, 1.2f, 0.12f);
        Juice.Ring(new Vector3(from.x, 0.2f, from.z), Scan, 2f, SweepLength * 1.3f, 1.6f, 0.2f);
    }
}
