using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// More life around the camera (added by CameraPan, next to Ambience):
//  - lightning: the sky flickers bright now and then while it rains, the thunder (Sfx) rolls in a moment later
//  - sounds from what's in view: air units' engine hum, harvesters' mining clinks, footsteps of moving units
//  - map features: data pulses flow along the chasms with motes rising out of them; bridge rails light up
//    while units cross
// Sounds and particles only near the view centre and never under the fog of war (Juice.Seen / Sfx.At).
public class WorldFX : MonoBehaviour
{
    const float Near = 70f; // how far from the view centre things are heard / animated
    static readonly int TintId = Shader.PropertyToID("_TintColor"), StId = Shader.PropertyToID("_MainTex_ST"), EmissionId = Shader.PropertyToID("_EmissionColor");
    static readonly Color Rail = new Color(0.2f, 0.9f, 1f) * 1.6f, ChasmMote = new(1f, 0.35f, 0.85f, 0.7f);

    Camera cam;
    Vector3 focus;
    float nextScan, nextStep, nextMine, nextFlash = -1f, moteDebt;
    readonly List<Health> flyers = new(), walkers = new();
    readonly List<Harvester> mining = new();

    // map features (built by MapBuilder): chasm pieces and bridges
    readonly List<Transform> chasms = new();
    Material flow;
    class Bridge { public Transform t; public float halfX, halfZ, glow; public Renderer[] rails; }
    readonly List<Bridge> bridges = new();
    MaterialPropertyBlock block; // made in Start (Unity forbids it in a field initializer)

    void Start()
    {
        cam = GetComponent<Camera>();
        block = new MaterialPropertyBlock();
        FindFeatures();
    }

    void Update()
    {
        var ray = new Ray(cam.transform.position, cam.transform.forward);
        focus = ray.direction.y < -0.01f ? ray.GetPoint(-ray.origin.y / ray.direction.y) : cam.transform.position;
        if (Time.time >= nextScan) { nextScan = Time.time + 0.25f; Scan(); }
        Lightning();
        Footsteps();
        Mining();
        Chasms();
        Bridges();
    }

    bool Close(Vector3 p) => (p.x - focus.x) * (p.x - focus.x) + (p.z - focus.z) * (p.z - focus.z) < Near * Near;

    // What's near the view, sorted into sound makers (4x a second is plenty).
    void Scan()
    {
        flyers.Clear(); walkers.Clear(); mining.Clear();
        float hum = 0f;
        foreach (var h in Health.All)
        {
            if (h == null || !Close(h.transform.position) || !Juice.Seen(h.transform.position)) continue;
            if (Flyer.IsAir(h)) { flyers.Add(h); hum += 1f - Vector3.Distance(Flat(h.transform.position), Flat(focus)) / Near; continue; }
            if (h.TryGetComponent(out Harvester hv) && hv.state == Harvester.State.Harvesting) { mining.Add(hv); continue; }
            if (h.TryGetComponent(out NavMeshAgent a) && a.enabled && a.velocity.sqrMagnitude > 1f && h.TryGetComponent(out SelectableUnit _)) walkers.Add(h);
        }
        Sfx.EngineLevel = Mathf.Clamp01(hum * 0.6f);
    }

    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

    void Footsteps()
    {
        if (walkers.Count == 0 || Time.time < nextStep) return;
        nextStep = Time.time + Mathf.Lerp(0.35f, 0.09f, Mathf.Clamp01(walkers.Count / 12f)); // a crowd marches faster
        var w = walkers[Random.Range(0, walkers.Count)];
        if (w != null) Sfx.At(Snd.Footstep, w.transform.position, 0.18f);
    }

    void Mining()
    {
        if (mining.Count == 0 || Time.time < nextMine) return;
        nextMine = Time.time + Random.Range(0.45f, 0.8f) / Mathf.Min(3, mining.Count);
        var hv = mining[Random.Range(0, mining.Count)];
        if (hv != null) Sfx.At(Snd.Mining, hv.transform.position, 0.3f);
    }

    // ---- lightning ----

    Light sun;
    float sunBase;

    void Lightning()
    {
        if (!GameSettings.Rain) return;
        if (nextFlash < 0f) { nextFlash = Time.time + Random.Range(12f, 30f); return; }
        if (Time.time < nextFlash) return;
        nextFlash = Time.time + Random.Range(30f, 75f);
        if (sun == null) { sun = RenderSettings.sun != null ? RenderSettings.sun : FindFirstObjectByType<Light>(); if (sun != null) sunBase = sun.intensity; }
        if (sun == null) return;
        float near = Random.value; // close strikes: brighter, louder, sooner
        StartCoroutine(Flash(Mathf.Lerp(2f, 5f, near) * GameSettings.FlashScale));
        Sfx.Thunder(Mathf.Lerp(2.5f, 0.4f, near), Mathf.Lerp(0.6f, 1f, near));
    }

    System.Collections.IEnumerator Flash(float peak)
    {
        Color ambient = RenderSettings.ambientLight;
        float[] beats = { 1f, 0.2f, 0.7f, 0f }; // flicker: bright, dip, bright again, out
        foreach (float b in beats)
        {
            float k = b * peak;
            sun.intensity = sunBase * (1f + k);
            RenderSettings.ambientLight = ambient * (1f + k * 0.6f);
            yield return new WaitForSeconds(Random.Range(0.04f, 0.09f));
        }
        sun.intensity = sunBase;
        RenderSettings.ambientLight = ambient;
    }

    void OnDisable()
    {
        if (sun != null) sun.intensity = sunBase;
        Sfx.EngineLevel = 0f;
    }

    // ---- chasms and bridges ----

    void FindFeatures()
    {
        var root = GameObject.Find("MapFeatures");
        if (root == null) return;
        // Pulses flowing along each chasm: a dashed additive texture scrolled on the centre stream.
        var tex = new Texture2D(16, 64, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
        var px = new Color32[16 * 64];
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 16; x++)
            {
                float v = y / 64f, across = 1f - Mathf.Abs((x + 0.5f) / 16f - 0.5f) * 2f;
                byte a = (byte)(Mathf.Clamp01((v < 0.18f ? 1f - v / 0.18f : 0f) * across) * 255); // a short bright head, then a gap
                px[y * 16 + x] = new Color32(a, a, a, a);
            }
        tex.SetPixels32(px);
        tex.Apply(true);
        flow = new Material(Shader.Find("Legacy Shaders/Particles/Additive")) { mainTexture = tex };
        flow.SetColor(TintId, new Color(1f, 0.3f, 0.85f, 0.9f) * 1.6f);

        foreach (Transform c in root.GetComponentsInChildren<Transform>())
        {
            if (c.name == "Chasm")
            {
                chasms.Add(c);
                var stream = c.Find("Stream");
                if (stream == null) continue;
                var r = stream.GetComponent<Renderer>();
                r.sharedMaterial = flow;
                r.GetPropertyBlock(block);
                block.SetVector(StId, new Vector4(1f, stream.localScale.y / 9f, 0f, Random.value)); // a pulse every 9 m
                r.SetPropertyBlock(block);
            }
            else if (c.name == "Bridge" || c.name == "SealedBridge")
            {
                var deck = c.Find("Deck");
                var rails = new List<Renderer>();
                foreach (Transform part in c) if (part.name == "Rail") rails.Add(part.GetComponent<Renderer>());
                if (deck != null) bridges.Add(new Bridge { t = c, halfX = deck.localScale.x / 2f, halfZ = deck.localScale.z / 2f, rails = rails.ToArray() });
            }
        }
    }

    void Chasms()
    {
        if (flow == null || chasms.Count == 0) return;
        flow.mainTextureOffset = new Vector2(0f, -Time.time * 0.6f); // ~5 m/s along every chasm
        for (moteDebt += 30f * Time.deltaTime; moteDebt >= 1f; moteDebt--)
        {
            var c = chasms[Random.Range(0, chasms.Count)];
            if (!Close(c.position)) continue;
            Vector3 at = c.position + c.forward * Random.Range(-0.5f, 0.5f) * c.Find("Void").localScale.y + c.right * Random.Range(-4f, 4f) + Vector3.up * 0.2f;
            if (Juice.Seen(at)) Particles.Emit("Mote", at, new Vector3(0f, Random.Range(1f, 2.5f), 0f), Random.Range(0.08f, 0.18f), Random.Range(1.5f, 3f), ChasmMote);
        }
    }

    // Rails glow brighter while any unit is on the bridge.
    void Bridges()
    {
        foreach (var b in bridges)
        {
            if (b.t == null) continue;
            bool busy = false;
            if (Close(b.t.position))
                foreach (var w in walkers)
                {
                    if (w == null) continue;
                    Vector3 l = b.t.InverseTransformPoint(w.transform.position);
                    if (Mathf.Abs(l.x) < b.halfX && Mathf.Abs(l.z) < b.halfZ) { busy = true; break; }
                }
            float pulse = busy ? 1.5f + Mathf.Sin(Time.time * 8f) * 0.6f : 0f;
            float glow = Mathf.MoveTowards(b.glow, pulse, Time.deltaTime * 4f);
            if (Mathf.Approximately(glow, b.glow)) continue;
            b.glow = glow;
            foreach (var r in b.rails)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(EmissionId, Rail * (1f + glow));
                r.SetPropertyBlock(block);
            }
        }
    }
}
