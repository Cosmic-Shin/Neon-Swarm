using System.Collections.Generic;
using UnityEngine;

public class BuildingFX : MonoBehaviour
{
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    const float Kit = 4f;

    Structure s;
    Collider body;

    Transform drone;
    LineRenderer beam;
    readonly List<Transform> rotors = new();
    float angle, retarget, nextSpark, nextPulse;
    Vector3 weld;

    List<(Renderer r, int slot, Color c)> neon;
    MaterialPropertyBlock block;
    LineRenderer bolt;
    bool dimmed;
    float nextFlicker;

    void Awake()
    {
        s = GetComponent<Structure>();
        body = GetComponent<Collider>();
    }

    void OnDisable() { Repair(false); Power(false); }
    void OnDestroy() { if (drone != null) Destroy(drone.gameObject); }

    void Update()
    {
        Bounds b = body != null ? body.bounds : new Bounds(transform.position, Vector3.one * 6f);
        bool seen = Juice.Seen(b.center);
        Repair(seen && s.Health.enabled && (s.Repairing || s.RemoteRepairing), b);
        Power(seen && s.Health.enabled && s.power < 0 && Net.LowPower(s.Health.team), b);
    }

    void Repair(bool on, Bounds b = default)
    {
        if (!on)
        {
            if (drone != null) { Juice.Burst(drone.position, Juice.Heal, 10, 3f); Destroy(drone.gameObject); drone = null; }
            return;
        }
        if (drone == null)
        {
            var prefab = Resources.Load<GameObject>("Construction/Build_Drone");
            drone = prefab != null ? Instantiate(prefab).transform : new GameObject("RepairDrone").transform;
            drone.localScale = Vector3.one * Kit;
            rotors.Clear();
            foreach (var t in drone.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("BuildDrone_Rotor")) rotors.Add(t);
            beam = new GameObject("Beam").AddComponent<LineRenderer>();
            beam.transform.SetParent(drone, false);
            beam.sharedMaterial = Resources.Load<Material>("HarvestFX");
            beam.positionCount = 2;
            beam.useWorldSpace = true;
            beam.widthMultiplier = 0.12f;
            beam.startColor = beam.endColor = Juice.Heal;
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        angle += 40f * Time.deltaTime;
        float a = angle * Mathf.Deg2Rad, radius = Mathf.Max(b.extents.x, b.extents.z) + 2f;
        Vector3 pos = new(b.center.x + Mathf.Cos(a) * radius, b.max.y * 0.85f + Mathf.Sin(Time.time * 2f) * 0.3f, b.center.z + Mathf.Sin(a) * radius);
        drone.position = pos;
        Vector3 look = b.center - pos; look.y = 0f;
        if (look.sqrMagnitude > 1e-4f) drone.rotation = Quaternion.LookRotation(look);
        foreach (var r in rotors) r.Rotate(Vector3.up, 1500f * Time.deltaTime, Space.Self);

        retarget -= Time.deltaTime;
        if (retarget <= 0f)
        {
            retarget = Random.Range(0.3f, 0.6f);
            Vector3 aim = new(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y + 0.5f, b.max.y), Random.Range(b.min.z, b.max.z));
            weld = body != null ? body.ClosestPoint(pos + (aim - pos) * 0.5f) : aim;
        }
        beam.enabled = Random.value > 0.1f;
        beam.SetPosition(0, pos);
        beam.SetPosition(1, weld);

        if (Time.time >= nextSpark) { nextSpark = Time.time + 0.08f; Juice.Burst(weld, Juice.Heal, 3, 3f, 0.12f, 0.4f, 0.5f, true); }
        if (Time.time >= nextPulse)
        {
            nextPulse = Time.time + 1.2f;
            Juice.Ring(new Vector3(b.center.x, 0.2f, b.center.z), Juice.Heal, Mathf.Max(b.extents.x, b.extents.z), Mathf.Max(b.extents.x, b.extents.z) + 2f, 0.6f, 0.15f);
        }
    }

    void Power(bool low, Bounds b = default)
    {
        if (!low)
        {
            if (dimmed) SetNeon(1f);
            dimmed = false;
            if (bolt != null) bolt.enabled = false;
            return;
        }
        if (neon == null) CollectNeon();
        dimmed = true;
        if (Time.time >= nextFlicker)
        {
            nextFlicker = Time.time + Random.Range(0.04f, 0.12f);
            SetNeon(Random.value < 0.07f ? 0.9f : 0.12f + 0.12f * Random.value);
        }

        if (bolt == null)
        {
            bolt = new GameObject("LowPowerBolt").AddComponent<LineRenderer>();
            bolt.transform.SetParent(transform, true);
            bolt.gameObject.layer = gameObject.layer;
            bolt.sharedMaterial = Resources.Load<Material>("HarvestFX");
            bolt.positionCount = 6;
            bolt.useWorldSpace = true;
            bolt.widthMultiplier = 0.45f;
            bolt.startColor = bolt.endColor = Juice.Danger;
            bolt.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        bolt.enabled = Mathf.Repeat(Time.time, 1f) < 0.75f;
        var cam = Camera.main;
        Vector3 right = cam != null ? cam.transform.right : Vector3.right, up = Vector3.up;
        Vector3 top = new(b.center.x, b.max.y + 3.5f, b.center.z);
        Vector2[] shape = { new(0.5f, 1.5f), new(-0.4f, 0.2f), new(0.3f, 0.2f), new(-0.5f, -1.5f), new(0.4f, -0.1f), new(-0.3f, -0.1f) };
        for (int i = 0; i < shape.Length; i++) bolt.SetPosition(i, top + right * shape[i].x + up * shape[i].y);
    }

    void CollectNeon()
    {
        neon = new List<(Renderer, int, Color)>();
        block = new MaterialPropertyBlock();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] && mats[i].HasProperty(EmissionId) && mats[i].IsKeywordEnabled("_EMISSION"))
                {
                    Color c = mats[i].GetColor(EmissionId);
                    if (c.maxColorComponent > 0.001f) neon.Add((r, i, c));
                }
        }
    }

    void SetNeon(float k)
    {
        if (neon == null) return;
        foreach (var (r, slot, c) in neon)
        {
            if (!r) continue;
            r.GetPropertyBlock(block, slot);
            block.SetColor(EmissionId, c * k);
            r.SetPropertyBlock(block, slot);
        }
    }
}
