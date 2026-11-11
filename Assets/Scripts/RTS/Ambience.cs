using System.Collections.Generic;
using UnityEngine;

// The living world around the camera (added by CameraPan):
//  - rain streaks with ripples on the wet ground (Settings can turn it off)
//  - ore crystals shrink as they're mined, pop and sparkle on each stage change, glint now and then
//  - neon signs, streetlights and vending machines flicker, steam rises from vents, dust motes drift
// Everything is emitted only near what the camera looks at, and nothing under the fog of war.
public class Ambience : MonoBehaviour
{
    static readonly Color RainColor = new(0.6f, 0.75f, 1f, 0.3f), RippleColor = new(0.55f, 0.75f, 1f, 0.45f),
        SteamColor = new(0.7f, 0.75f, 0.85f, 0.22f), MoteColor = new(0.7f, 0.9f, 1f, 0.6f),
        Green = new(0.4f, 1f, 0.5f), Red = new(1f, 0.3f, 0.3f);
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    const float AreaX = 75f, AreaZ = 55f; // half-size of the area around the view centre that gets weather

    Camera cam;
    float rainDebt, rippleDebt, moteDebt;

    void Start()
    {
        cam = GetComponent<Camera>();
        FindCity();
    }

    void Update()
    {
        Vector3 focus = Focus();
        float dt = Time.deltaTime;
        if (GameSettings.Rain) Rain(focus, dt);
        Motes(focus, dt);
        Ores(dt);
        Signs();
        Vents(focus);
    }

    // Where the camera looks at the ground.
    Vector3 Focus()
    {
        var ray = new Ray(cam.transform.position, cam.transform.forward);
        return ray.direction.y < -0.01f ? ray.GetPoint(-ray.origin.y / ray.direction.y) : cam.transform.position;
    }

    static Vector3 Around(Vector3 focus, float y) =>
        new(focus.x + Random.Range(-AreaX, AreaX), y, focus.z + Random.Range(-AreaZ, AreaZ));

    // ---- weather ----

    void Rain(Vector3 focus, float dt)
    {
        Vector3 fall = new(2.5f, -38f, 1f); // a little wind
        for (rainDebt += 900f * dt; rainDebt >= 1f; rainDebt--)
        {
            Vector3 at = Around(focus, Random.Range(22f, 34f));
            Particles.Emit("Rain", at, fall, 0.07f, at.y / -fall.y, RainColor);
        }
        for (rippleDebt += 220f * dt; rippleDebt >= 1f; rippleDebt--)
        {
            Vector3 at = Around(focus, 0.06f);
            if (Juice.Seen(at)) Particles.Emit("Ripple", at, Vector3.zero, Random.Range(0.6f, 1.1f), 0.45f, RippleColor);
        }
        // Drops landing on roofs and walls: a tiny upward splash where a raindrop meets anything standing.
        for (splashDebt += 70f * dt; splashDebt >= 1f; splashDebt--)
        {
            Vector3 top = Around(focus, 40f);
            if (!Physics.Raycast(top, Vector3.down, out RaycastHit hit, 39.5f, ~0, QueryTriggerInteraction.Ignore) || hit.point.y < 0.5f) continue; // ground: the ripples do that
            if (!Juice.Seen(hit.point)) continue;
            for (int i = 0; i < 3; i++)
                Particles.Emit("Mote", hit.point + Vector3.up * 0.05f, new Vector3(Random.Range(-1f, 1f), Random.Range(1.2f, 2.2f), Random.Range(-1f, 1f)), 0.06f, 0.25f, RainColor * 2f);
        }
    }

    float splashDebt;

    void Motes(Vector3 focus, float dt)
    {
        for (moteDebt += 25f * dt; moteDebt >= 1f; moteDebt--)
        {
            Vector3 at = Around(focus, Random.Range(0.5f, 10f));
            if (Juice.Seen(at)) Particles.Emit("Mote", at, Random.insideUnitSphere * 0.4f + new Vector3(0.3f, 0.05f, 0f), Random.Range(0.05f, 0.12f), 6f, MoteColor);
        }
    }

    // ---- ore ----

    class OreLook { public Vector3 scale; public int stage; public float popAt = -10f; }
    readonly Dictionary<OreNode, OreLook> ores = new();

    static int Stage(float f) => f > 0.66f ? 3 : f > 0.33f ? 2 : f > 0.001f ? 1 : 0; // same as OreNode's

    void Ores(float dt)
    {
        foreach (var o in OreNode.All)
        {
            if (o == null) continue;
            float f = o.amount / Mathf.Max(1f, o.maxAmount);
            int st = Stage(f);
            if (!ores.TryGetValue(o, out var look)) ores[o] = look = new OreLook { scale = o.transform.localScale, stage = st };
            Vector3 p = o.transform.position;
            bool seen = Juice.Seen(p);
            Color c = o.valueMultiplier > 1f ? Red : Green;

            if (st != look.stage)
            {
                bool grew = st > look.stage;
                look.stage = st;
                look.popAt = Time.time;
                if (seen)
                {
                    Juice.Burst(p + Vector3.up * 1.5f, c, grew ? 18 : 10, 3f, 0.2f, 0.6f, 1f);
                    if (grew) Juice.Ring(p + Vector3.up * 0.15f, c, 0.5f, 4f, 0.6f, 0.2f);
                }
            }

            // Within a stage it shrinks a little as it's mined; on a stage change it springs.
            float inStage = st == 3 ? (f - 0.66f) / 0.34f : st == 2 ? (f - 0.33f) / 0.33f : st == 1 ? f / 0.33f : 1f;
            float k = Mathf.Clamp01((Time.time - look.popAt) / 0.35f);
            float spring = 1f + 0.2f * (1f - k) * Mathf.Sin(k * Mathf.PI * 2f);
            o.transform.localScale = look.scale * (0.85f + 0.15f * Mathf.Clamp01(inStage)) * spring;

            if (seen && st > 0 && Random.value < dt * 0.5f) // glint
                Juice.Burst(p + new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(0.8f, 2.5f), Random.Range(-1.5f, 1.5f)), Color.Lerp(c, Color.white, 0.7f), 1, 0.2f, 0.4f, 0.4f, 0f);
        }
    }

    // ---- city ----

    readonly List<(Transform t, List<(Renderer r, int slot, Color c)> neon)> signs = new();
    readonly List<Vector3> vents = new();
    readonly Dictionary<int, float> flickering = new(); // sign index -> until
    MaterialPropertyBlock block;
    float nextFlicker, nextFlickerFrame, nextVent;

    void FindCity()
    {
        block = new MaterialPropertyBlock();
        var city = GameObject.Find("City");
        if (city == null) return;
        var rng = new System.Random(11); // same vents every game
        foreach (Transform group in city.transform)
            foreach (Transform prop in group)
            {
                string n = prop.name;
                if (n.Contains("Billboard") || n.Contains("Streetlight") || n.Contains("Vending"))
                {
                    var neon = new List<(Renderer, int, Color)>();
                    foreach (var r in prop.GetComponentsInChildren<Renderer>())
                    {
                        var mats = r.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++)
                            if (mats[i] && mats[i].IsKeywordEnabled("_EMISSION") && mats[i].GetColor(EmissionId).maxColorComponent > 0.01f)
                                neon.Add((r, i, mats[i].GetColor(EmissionId)));
                    }
                    if (neon.Count > 0) signs.Add((prop, neon));
                }
                if ((group.name == "Roads" || n.Contains("Ruin")) && rng.NextDouble() < 0.35)
                    vents.Add(prop.position + new Vector3((float)rng.NextDouble() * 8f - 4f, 0.1f, (float)rng.NextDouble() * 8f - 4f));
            }
    }

    // Now and then a sign stutters: a few quick on/off blinks, then back to normal.
    void Signs()
    {
        if (signs.Count == 0) return;
        if (Time.time >= nextFlicker)
        {
            nextFlicker = Time.time + Random.Range(0.2f, 0.6f);
            int i = Random.Range(0, signs.Count);
            if (signs[i].t != null && Juice.Seen(signs[i].t.position)) flickering[i] = Time.time + Random.Range(0.3f, 1.2f);
        }
        if (Time.time < nextFlickerFrame || flickering.Count == 0) return;
        nextFlickerFrame = Time.time + 0.05f;
        var done = new List<int>();
        foreach (var kv in flickering)
        {
            bool over = Time.time >= kv.Value;
            SetSign(kv.Key, over ? 1f : Random.value < 0.5f ? 0.08f : 1.3f);
            if (over) done.Add(kv.Key);
        }
        foreach (int i in done) flickering.Remove(i);
    }

    void SetSign(int i, float k)
    {
        foreach (var (r, slot, c) in signs[i].neon)
        {
            if (!r) continue;
            r.GetPropertyBlock(block, slot);
            block.SetColor(EmissionId, c * k);
            r.SetPropertyBlock(block, slot);
        }
    }

    void Vents(Vector3 focus)
    {
        if (Time.time < nextVent) return;
        nextVent = Time.time + 0.12f;
        foreach (var v in vents)
        {
            if (Mathf.Abs(v.x - focus.x) > AreaX * 1.2f || Mathf.Abs(v.z - focus.z) > AreaZ * 1.2f || !Juice.Seen(v)) continue;
            Particles.Emit("Steam", v + Random.insideUnitSphere * 0.3f, new Vector3(0.4f, Random.Range(1.8f, 2.8f), 0.1f), Random.Range(1f, 1.6f), 3f, SteamColor);
        }
    }
}
