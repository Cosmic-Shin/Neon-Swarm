using System.Collections.Generic;
using UnityEngine;

public class UpgradeFX : MonoBehaviour
{
    const float Kit = 4f, FlashTime = 0.6f, SparksPerSecond = 40f, ScanPeriod = 2.5f;

    public float? Remote;

    class Drone { public Transform t, emitter; public LineRenderer beam; public float angle, speed, retarget; public Vector3 target; public readonly List<Transform> rotors = new(); }

    BuildingUpgrade up;
    Transform root;
    readonly List<GameObject> holos = new();
    readonly List<Drone> drones = new();
    Material holoMat;
    Transform scan;
    ParticleSystem sparks;
    Light glow;
    Bounds parts, body;
    float flash, sparkDebt;
    int layer = -1;
    Color color;

    void Awake() => up = GetComponent<BuildingUpgrade>();

    void OnDestroy() => Clear();

    void Update()
    {
        float p = Remote ?? (up.IsUpgrading ? up.Progress : -1f);
        if (p >= 0f && root == null && up.level < up.maxLevel) Begin(up.level + 1);
        if (root == null) return;
        if (gameObject.layer != layer) SyncLayer();

        if (p < 0f) { Finish(); return; }

        float cut = parts.min.y + parts.size.y * Mathf.Clamp01(p);
        float flicker = Random.value < 0.05f ? 0.4f : 1f;
        holoMat.SetFloat("_CutY", cut);
        holoMat.SetColor("_Color", color * 1.6f * flicker);

        if (scan != null) scan.position = new Vector3(body.center.x, body.min.y + body.size.y * Mathf.PingPong(Time.time / ScanPeriod, 1f), body.center.z);

        float radius = Mathf.Max(body.extents.x, body.extents.z) + 0.5f * Kit;
        foreach (var d in drones)
        {
            d.angle += d.speed * Time.deltaTime;
            float a = d.angle * Mathf.Deg2Rad;
            Vector3 pos = new(body.center.x + Mathf.Cos(a) * radius, cut + (0.45f + Mathf.Sin(Time.time * 2f + d.speed) * 0.08f) * Kit, body.center.z + Mathf.Sin(a) * radius);
            d.t.position = pos;
            Vector3 look = body.center - pos; look.y = 0f;
            if (look.sqrMagnitude > 1e-4f) d.t.rotation = Quaternion.LookRotation(look);
            foreach (var r in d.rotors) r.Rotate(Vector3.up, 1500f * Time.deltaTime, Space.Self);
            d.retarget -= Time.deltaTime;
            if (d.retarget <= 0f)
            {
                d.retarget = Random.Range(0.25f, 0.6f);
                d.target = new Vector3(Random.Range(parts.min.x, parts.max.x), cut, Random.Range(parts.min.z, parts.max.z));
            }
            d.beam.enabled = Random.value > 0.08f;
            d.beam.SetPosition(0, d.emitter != null ? d.emitter.position : pos);
            d.beam.SetPosition(1, d.target);
        }

        sparkDebt += SparksPerSecond * Time.deltaTime;
        var e = new ParticleSystem.EmitParams { startColor = Color.Lerp(color, new Color(1f, 0.8f, 0.4f), 0.5f), startSize = 0.08f, startLifetime = 0.6f };
        for (; sparkDebt >= 1f && drones.Count > 0; sparkDebt--)
        {
            e.position = drones[Random.Range(0, drones.Count)].target;
            e.velocity = (Random.insideUnitSphere + Vector3.up * 0.8f) * Random.Range(2f, 5f);
            sparks.Emit(e, 1);
        }

        glow.transform.position = new Vector3(parts.center.x, cut + 1f, parts.center.z);
        glow.color = color;
        glow.intensity = (2.5f + Mathf.Sin(Time.time * 12f) * 1.2f) * flicker;
    }

    void Begin(int level)
    {
        color = ProductionFX.HoloColor(gameObject);
        body = TryGetComponent(out Collider c) ? c.bounds : new Bounds(transform.position, Vector3.one * 8f);
        root = new GameObject("UpgradeFX").transform;
        flash = 0f;

        holoMat = new Material(Shader.Find("GD5/Hologram"));
        var rends = new List<Renderer>();
        foreach (var part in up.PartsFor(level))
        {
            if (part == null) continue;
            var copy = Instantiate(part.gameObject, part.parent);
            copy.name = part.name + "_Holo";
            foreach (var comp in copy.GetComponentsInChildren<Component>(true))
                if (!(comp is Transform || comp is MeshFilter || comp is MeshRenderer || comp is SkinnedMeshRenderer)) DestroyImmediate(comp);
            foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials = System.Array.ConvertAll(r.sharedMaterials, _ => holoMat);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rends.Add(r);
            }
            copy.SetActive(true);
            holos.Add(copy);
        }
        parts = body;
        if (rends.Count > 0)
        {
            parts = rends[0].bounds;
            foreach (var r in rends) parts.Encapsulate(r.bounds);
        }

        float hx = body.extents.x + 0.25f * Kit, hz = body.extents.z + 0.25f * Kit;
        var scanPrefab = Resources.Load<GameObject>("Construction/Build_ScanFrame");
        if (scanPrefab != null)
        {
            var sf = Instantiate(scanPrefab, body.center, Quaternion.identity, root);
            sf.transform.localScale = new Vector3(hx * 2f, Kit, hz * 2f);
            ConstructionSite.KeepBarsThin(sf, 0.03f * Kit, Mathf.Min(hx, hz));
            scan = sf.transform;
        }
        var dronePrefab = Resources.Load<GameObject>("Construction/Build_Drone");
        var beamMat = Resources.Load<Material>("Construction/Beam");
        for (int i = 0; dronePrefab != null && i < 2; i++)
        {
            var d = new Drone { t = Instantiate(dronePrefab, body.center, Quaternion.identity, root).transform, angle = i * 180f, speed = i == 0 ? 35f : -28f };
            d.t.localScale = Vector3.one * Kit;
            foreach (var t in d.t.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "BuildDrone_Emitter") d.emitter = t;
                else if (t.name.StartsWith("BuildDrone_Rotor")) d.rotors.Add(t);
            }
            d.beam = new GameObject("Beam").AddComponent<LineRenderer>();
            d.beam.transform.SetParent(d.t, false);
            d.beam.positionCount = 2;
            d.beam.widthMultiplier = 0.02f * Kit;
            d.beam.useWorldSpace = true;
            d.beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (beamMat != null) d.beam.sharedMaterial = beamMat;
            drones.Add(d);
        }

        sparks = ProductionFX.MakeSparks(root, gameObject.layer);
        glow = ProductionFX.MakeLight(root, gameObject.layer);
        glow.range = 14f;
        glow.enabled = true;
        layer = -1;
    }

    void Finish()
    {
        if (flash <= 0f)
        {
            flash = FlashTime;
            foreach (var h in holos) if (h != null) Destroy(h);
            holos.Clear();
            foreach (var d in drones) if (d.t != null) Destroy(d.t.gameObject);
            drones.Clear();
            if (scan != null) Destroy(scan.gameObject);
        }
        flash -= Time.deltaTime;
        glow.intensity = 10f * Mathf.Clamp01(flash / FlashTime);
        glow.transform.position = parts.center;
        if (flash <= 0f) Clear();
    }

    void Clear()
    {
        foreach (var h in holos) if (h != null) Destroy(h);
        holos.Clear();
        drones.Clear();
        if (root != null) Destroy(root.gameObject);
        if (holoMat != null) Destroy(holoMat);
        root = null;
        scan = null;
        flash = 0f;
    }

    void SyncLayer()
    {
        layer = gameObject.layer;
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        foreach (var h in holos) foreach (var t in h.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }
}
