using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// C&C-style construction. Begin() places a building as a construction site:
/// a hazard pad and scaffold sized to the building, a hologram outline of the finished building,
/// the real building rising out of the ground, a scan frame climbing with progress, and drones welding.
/// The building's scripts and colliders stay off until it is finished.
/// Kit models are loaded from Resources/Construction (created by Tools > Cyberpunk RTS > 1. Setup).
/// </summary>
public class ConstructionSite : MonoBehaviour
{
    public static readonly List<ConstructionSite> All = new List<ConstructionSite>();
    public static event System.Action<GameObject> OnBuildingComplete;

    public GameObject Building { get; private set; }
    public float Progress { get; private set; }
    public float buildTime = 20f;
    public Bounds WorldBounds { get; private set; }

    static GameObject padP, poleP, railP, scanP, droneP;
    static Material holoMat, beamMat;
    static bool loaded;

    readonly List<Behaviour> disabled = new List<Behaviour>();
    readonly List<Collider> disabledCols = new List<Collider>();
    readonly List<GameObject> parts = new List<GameObject>();
    readonly List<Transform> poles = new List<Transform>();
    readonly List<(GameObject go, float level)> rails = new List<(GameObject, float)>();
    readonly List<Drone> drones = new List<Drone>();
    GameObject ghost; Transform scan;
    Vector3 basePos, center; float height; float hx, hz; Vector3 buildingScale;
    bool done;
    float k = 1f;        // GD5 edit: kit size multiplier (our buildings are 4x the pack's)
    bool external;       // GD5 edit: building was placed by the caller, who pauses its scripts

    class Drone { public Transform t, emitter; public LineRenderer beam; public float angle, speed, radius, bob; public Vector3 target; public float retarget; public List<Transform> rotors = new List<Transform>(); }

    static void Load()
    {
        if (loaded) return; loaded = true;
        padP = Resources.Load<GameObject>("Construction/Build_Pad");
        poleP = Resources.Load<GameObject>("Construction/Build_Pole");
        railP = Resources.Load<GameObject>("Construction/Build_Rail");
        scanP = Resources.Load<GameObject>("Construction/Build_ScanFrame");
        droneP = Resources.Load<GameObject>("Construction/Build_Drone");
        holoMat = Resources.Load<Material>("Construction/Hologram");
        beamMat = Resources.Load<Material>("Construction/Beam");
        if (!padP || !holoMat) Debug.LogWarning("[CyberpunkRTS] Construction kit not found in Resources/Construction. Run Tools > Cyberpunk RTS > 1. Setup.");
    }

    public static Material HologramMaterial { get { Load(); return holoMat; } }

    /// <summary>Start building 'prefab' at pos/rot. Returns the site; listen to OnBuildingComplete for the result.</summary>
    public static ConstructionSite Begin(GameObject prefab, Vector3 pos, Quaternion rot, float time)
    {
        Load();
        var go = new GameObject(prefab.name + "_ConstructionSite");
        go.transform.SetPositionAndRotation(pos, rot);
        var site = go.AddComponent<ConstructionSite>();
        site.buildTime = Mathf.Max(0.1f, time);
        site.Init(prefab, pos, rot);
        return site;
    }

    /// <summary>GD5 edit: build an already-placed building in place. The caller pauses its scripts.</summary>
    public static ConstructionSite Attach(GameObject building, float time, float kitScale)
    {
        Load();
        var t = building.transform;
        var go = new GameObject(building.name + "_ConstructionSite");
        go.transform.SetPositionAndRotation(t.position, t.rotation);
        var site = go.AddComponent<ConstructionSite>();
        site.buildTime = Mathf.Max(0.1f, time);
        site.k = kitScale; site.external = true;
        site.Init(building, t.position, t.rotation);
        return site;
    }

    /// <summary>GD5 edit: jump ahead, e.g. when loading a save.</summary>
    public void Skip(float progress) => Progress = Mathf.Clamp01(progress);

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Init(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        basePos = pos;
        Building = external ? prefab : Instantiate(prefab, pos, rot);
        if (!external)
        {
            Building.name = prefab.name;
            foreach (var b in Building.GetComponentsInChildren<Behaviour>(true)) if (b.enabled) { b.enabled = false; disabled.Add(b); }
            foreach (var c in Building.GetComponentsInChildren<Collider>(true)) if (c.enabled) { c.enabled = false; disabledCols.Add(c); }
        }
        buildingScale = Building.transform.localScale;

        var b0 = RendererBounds(Building);
        WorldBounds = b0;
        height = Mathf.Max(0.3f, b0.max.y - pos.y);
        center = new Vector3(b0.center.x, pos.y, b0.center.z);
        hx = b0.extents.x + 0.25f * k; hz = b0.extents.z + 0.25f * k;

        ghost = MakeHologram(prefab, pos, rot, new Color(0f, 0.9f, 0.85f, 0.35f));
        Building.transform.position = basePos + Vector3.down * height;

        if (padP)
        {
            var pad = Instantiate(padP, center + Vector3.up * 0.005f, Quaternion.identity, transform);
            pad.transform.localScale = new Vector3(hx * 2f + 0.3f, 1f, hz * 2f + 0.3f);
            parts.Add(pad);
        }
        if (poleP)
            foreach (var (sx, sz) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
            {
                var p = Instantiate(poleP, center + new Vector3(sx * hx, 0f, sz * hz), Quaternion.identity, transform);
                p.transform.localScale = new Vector3(k, 0.01f, k);
                poles.Add(p.transform); parts.Add(p);
            }
        if (railP)
        {
            int levels = Mathf.Max(2, Mathf.CeilToInt(height / (0.6f * k)));
            for (int i = 1; i <= levels; i++)
            {
                float y = height * 1.05f * i / levels;
                foreach (var s in new[] { 1, -1 })
                {
                    var rX = Instantiate(railP, center + new Vector3(0f, y, s * hz), Quaternion.identity, transform);
                    rX.transform.localScale = new Vector3(hx * 2f, k * 0.4f, k * 0.4f);   // GD5: thinner, rails bloom
                    var rZ = Instantiate(railP, center + new Vector3(s * hx, y, 0f), Quaternion.Euler(0f, 90f, 0f), transform);
                    rZ.transform.localScale = new Vector3(hz * 2f, k * 0.4f, k * 0.4f);
                    rX.SetActive(false); rZ.SetActive(false);
                    rails.Add((rX, y)); rails.Add((rZ, y));
                    parts.Add(rX); parts.Add(rZ);
                }
            }
        }
        if (scanP)
        {
            var sf = Instantiate(scanP, center, Quaternion.identity, transform);
            sf.transform.localScale = new Vector3(hx * 2f, k, hz * 2f);
            KeepBarsThin(sf, 0.03f * k, Mathf.Min(hx, hz));
            scan = sf.transform; parts.Add(sf);
        }
        if (droneP)
        {
            int count = Mathf.Max(hx, hz) > 1.2f * k ? 3 : 2;
            for (int i = 0; i < count; i++)
            {
                var d = new Drone { t = Instantiate(droneP, center, Quaternion.identity, transform).transform };
                d.angle = i * 360f / count; d.speed = Random.Range(25f, 40f) * (i % 2 == 0 ? 1 : -1);
                d.t.localScale = Vector3.one * k;
                d.radius = Mathf.Max(hx, hz) + 0.5f * k; d.bob = Random.Range(0f, 6f);
                foreach (var t in d.t.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "BuildDrone_Emitter") d.emitter = t;
                    else if (t.name.StartsWith("BuildDrone_Rotor")) d.rotors.Add(t);
                }
                var beamGo = new GameObject("Beam"); beamGo.transform.SetParent(d.t, false);
                d.beam = beamGo.AddComponent<LineRenderer>();
                d.beam.positionCount = 2; d.beam.widthMultiplier = 0.02f * k; d.beam.useWorldSpace = true;
                d.beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (beamMat) d.beam.sharedMaterial = beamMat;
                drones.Add(d);
            }
        }
    }

    void Update()
    {
        if (done) { FlyAway(); return; }
        if (!Building) { Cancel(); return; }   // GD5 edit: destroyed while being built
        Progress = Mathf.Min(1f, Progress + Time.deltaTime / buildTime);
        float p = Progress;

        // scaffold goes up first (0-15%), building rises after (10-100%)
        float poleK = Mathf.Clamp01(p / 0.15f);
        foreach (var pole in poles) pole.localScale = new Vector3(1f, Mathf.Max(0.01f, height * 1.08f * poleK), 1f);
        foreach (var (go, level) in rails) go.SetActive(height * 1.08f * poleK >= level);
        float riseK = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p - 0.1f) / 0.9f));
        Building.transform.position = basePos + Vector3.down * height * (1f - riseK);
        float scanY = Mathf.Lerp(0.05f, height, riseK);
        if (scan) scan.position = center + Vector3.up * scanY;

        foreach (var d in drones)
        {
            d.angle += d.speed * Time.deltaTime;
            float a = d.angle * Mathf.Deg2Rad;
            Vector3 pos = center + new Vector3(Mathf.Cos(a) * d.radius, Mathf.Min(scanY, height) + (0.45f + Mathf.Sin(Time.time * 2f + d.bob) * 0.08f) * k, Mathf.Sin(a) * d.radius);
            d.t.position = pos;
            Vector3 look = center - pos; look.y = 0f;
            if (look.sqrMagnitude > 1e-4f) d.t.rotation = Quaternion.LookRotation(look);
            foreach (var r in d.rotors) r.Rotate(Vector3.up, 1500f * Time.deltaTime, Space.Self);
            d.retarget -= Time.deltaTime;
            if (d.retarget <= 0f)
            {
                d.retarget = Random.Range(0.25f, 0.6f);
                d.target = center + new Vector3(Random.Range(-hx, hx) * 0.6f, Mathf.Max(0.05f, scanY + Random.Range(-0.15f, 0.05f) * k), Random.Range(-hz, hz) * 0.6f);
            }
            if (d.beam)
            {
                d.beam.enabled = Random.value > 0.08f;   // flicker
                d.beam.SetPosition(0, d.emitter ? d.emitter.position : pos);
                d.beam.SetPosition(1, d.target);
            }
        }
        if (Progress >= 1f) Complete();
    }

    void Complete()
    {
        done = true;
        Building.transform.position = basePos;
        foreach (var b in disabled) if (b) b.enabled = true;
        foreach (var c in disabledCols) if (c) c.enabled = true;
        if (ghost) Destroy(ghost);
        foreach (var go in parts) if (go) Destroy(go);
        foreach (var d in drones) if (d.beam) d.beam.enabled = false;
        StartCoroutine(Pop(Building.transform, buildingScale));
        All.Remove(this);
        OnBuildingComplete?.Invoke(Building);
        Destroy(gameObject, 1.6f);
    }

    void FlyAway()
    {
        foreach (var d in drones)
            if (d.t)
            {
                d.t.position += (Vector3.up * 3f + d.t.forward * -1.5f) * k * Time.deltaTime;
                foreach (var r in d.rotors) r.Rotate(Vector3.up, 2000f * Time.deltaTime, Space.Self);
            }
    }

    static IEnumerator Pop(Transform t, Vector3 s)
    {
        for (float k = 0f; k < 0.25f; k += Time.deltaTime)
        {
            if (!t) yield break;
            t.localScale = s * (1f + 0.05f * Mathf.Sin(k / 0.25f * Mathf.PI));
            yield return null;
        }
        if (t) t.localScale = s;
    }

    /// <summary>Cancel: removes the site and the unfinished building.</summary>
    public void Cancel()
    {
        if (done) return;
        if (Building) Destroy(Building);
        if (ghost) Destroy(ghost);
        Destroy(gameObject);
    }

    /// <summary>Stretching the frame to the footprint also thickens its bars; shrink each bar's thin
    /// horizontal sides back to 'thickness' (otherwise big buildings get a blooming wall of light).</summary>
    public static void KeepBarsThin(GameObject frame, float thickness, float thinBelow)
    {
        foreach (var mf in frame.GetComponentsInChildren<MeshFilter>())
        {
            var t = mf.transform;
            var size = mf.GetComponent<Renderer>().bounds.size;
            var ls = t.localScale;
            for (int a = 0; a < 3; a++)
            {
                var axis = Vector3.zero; axis[a] = 1f;
                var w = t.TransformDirection(axis);
                int wi = Mathf.Abs(w.x) > 0.9f ? 0 : Mathf.Abs(w.z) > 0.9f ? 2 : 1;
                if (wi != 1 && size[wi] < thinBelow && size[wi] > thickness) ls[a] *= thickness / size[wi];
            }
            t.localScale = ls;
        }
    }

    // ---------------- helpers shared with BuildPlacer ----------------
    public static Bounds RendererBounds(GameObject go)
    {
        // GD5 fix: meshes only, a turret's laser LineRenderer made the site 200m long
        var rs = System.Array.FindAll(go.GetComponentsInChildren<Renderer>(), r => r is MeshRenderer || r is SkinnedMeshRenderer);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    /// <summary>A see-through copy of a prefab with no scripts or colliders, tinted with 'tint'.</summary>
    public static GameObject MakeHologram(GameObject prefab, Vector3 pos, Quaternion rot, Color tint)
    {
        Load();
        var holder = new GameObject("_holoHolder"); holder.SetActive(false);     // inactive parent: no Awake/OnEnable on the copy
        var g = Instantiate(prefab, pos, rot, holder.transform);
        g.name = prefab.name + "_Hologram";
        var scripts = g.GetComponentsInChildren<MonoBehaviour>(true);   // GD5 fix: reverse, so RequireComponent dependents go first
        for (int i = scripts.Length - 1; i >= 0; i--) DestroyImmediate(scripts[i]);
        foreach (var c in g.GetComponentsInChildren<Animator>(true)) DestroyImmediate(c);
        foreach (var c in g.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        foreach (var t in g.GetComponentsInChildren<Transform>(true))
            if (t.name.Contains("_Upgrade_Mk") || t.name == "Outpost_Ruined" || t.name == "HealthBar") t.gameObject.SetActive(false);
        if (holoMat)
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = holoMat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        g.transform.SetParent(null, true);
        Destroy(holder);
        Tint(g, tint);
        return g;
    }

    static readonly int ColorId = Shader.PropertyToID("_Color"), EmissionId = Shader.PropertyToID("_EmissionColor");
    public static void Tint(GameObject g, Color c)
    {
        var block = new MaterialPropertyBlock();
        foreach (var r in g.GetComponentsInChildren<Renderer>(true))
        {
            r.GetPropertyBlock(block);
            block.SetColor(ColorId, c);
            block.SetColor(EmissionId, new Color(c.r, c.g, c.b) * 0.8f);
            r.SetPropertyBlock(block);
        }
    }

    void OnGUI()
    {
        if (done || !Building || external) return;   // GD5: our Construction draws its own bar
        var cam = Camera.main; if (!cam) return;
        Vector3 sp = cam.WorldToScreenPoint(basePos + Vector3.up * (height + 0.6f));
        if (sp.z < 0f) return;
        float w = 120f, x = sp.x - w / 2f, y = Screen.height - sp.y;
        GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(new Rect(x - 2, y - 2, w + 4, 12), Texture2D.whiteTexture);
        GUI.color = new Color(0f, 0.9f, 0.85f); GUI.DrawTexture(new Rect(x, y, w * Progress, 8), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
