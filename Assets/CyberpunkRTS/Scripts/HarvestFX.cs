using UnityEngine;

/// <summary>
/// Mining look for a harvester: two crackling beams from the drill bits into the crystal, sparks where
/// they hit, glowing data bits flying into the cargo glass and a flickering light, tinted like the ore
/// (lime, or red for rich ore). Driven only by Harvester.AnimIndex, so it also plays on other players'
/// copies in multiplayer. Added by Harvester.Awake; children of the harvester, so fog of war hides it too.
/// </summary>
public class HarvestFX : MonoBehaviour
{
    static readonly Color Lime = new(0.45f, 1f, 0.3f), Red = new(1f, 0.2f, 0.15f);
    const int ArcPoints = 7;
    const float SparksPerSecond = 70f, BitsPerSecond = 40f, BitLife = 0.5f;

    Harvester harvester;
    Transform[] drills;
    Transform cargo;
    LineRenderer[] beams;
    ParticleSystem particles;
    Light glow;
    OreNode ore;
    int visibleLayer;
    float sparkDebt, bitDebt, flashTimer;

    void Start()
    {
        harvester = GetComponent<Harvester>();
        drills = new[] { Find("Harv_DrillBitL"), Find("Harv_DrillBitR") };
        cargo = Find("Harv_CargoGlass") ?? transform;
        var material = Resources.Load<Material>("HarvestFX");

        var root = new GameObject("HarvestFX").transform;
        root.SetParent(transform, false);
        visibleLayer = root.gameObject.layer = gameObject.layer;

        beams = new LineRenderer[drills.Length];
        for (int i = 0; i < beams.Length; i++)
        {
            var lr = new GameObject("Beam").AddComponent<LineRenderer>();
            lr.transform.SetParent(root, false);
            lr.gameObject.layer = visibleLayer;
            lr.sharedMaterial = material;
            lr.positionCount = ArcPoints;
            lr.useWorldSpace = true;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.enabled = false;
            beams[i] = lr;
        }

        particles = new GameObject("Bits").AddComponent<ParticleSystem>();
        particles.transform.SetParent(root, false);
        particles.gameObject.layer = visibleLayer;
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.maxParticles = 300;
        main.startLifetime = BitLife;
        var emission = particles.emission; emission.enabled = false; // we Emit() by hand
        var shape = particles.shape; shape.enabled = false;
        var size = particles.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        var pr = particles.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = material;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particles.Play();

        glow = new GameObject("Glow").AddComponent<Light>();
        glow.transform.SetParent(root, false);
        glow.type = LightType.Point;
        glow.range = 6f;
        glow.shadows = LightShadows.None;
        glow.enabled = false;
    }

    Transform Find(string n)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
        return null;
    }

    void LateUpdate()
    {
        if (harvester == null) return;
        bool hiddenByFog = beams[0].gameObject.layer != visibleLayer;
        bool mining = harvester.AnimIndex == 1 && !hiddenByFog && (ore = PickOre()) != null;
        foreach (var b in beams) b.enabled = mining;
        glow.enabled = mining;
        if (!mining) { ore = null; return; }

        Color c = ore.valueMultiplier > 1f ? Red : Lime;
        Vector3 hit = HitPoint();
        float t = Time.time;

        for (int i = 0; i < beams.Length; i++)
        {
            var lr = beams[i];
            Vector3 from = drills[i] != null ? drills[i].position : transform.position + Vector3.up * 0.5f;
            Vector3 to = hit + Random.insideUnitSphere * 0.15f;
            float jag = Vector3.Distance(from, to) * 0.12f;
            for (int p = 0; p < ArcPoints; p++)
            {
                float k = p / (ArcPoints - 1f);
                Vector3 pos = Vector3.Lerp(from, to, k);
                if (p > 0 && p < ArcPoints - 1) pos += Random.insideUnitSphere * jag * Mathf.Sin(k * Mathf.PI);
                lr.SetPosition(p, pos);
            }
            float w = 0.11f + 0.07f * Mathf.PerlinNoise(t * 20f, i * 3.1f);
            lr.startWidth = w * 0.6f;
            lr.endWidth = w;
            lr.startColor = new Color(c.r, c.g, c.b, 0.6f);
            lr.endColor = Color.Lerp(c, Color.white, 0.4f);
        }

        glow.transform.position = hit + Vector3.up * 0.3f;
        glow.color = c;
        glow.intensity = 3f + 3f * Mathf.PerlinNoise(t * 15f, 0.5f);

        // Sparks spraying off the crystal.
        sparkDebt += SparksPerSecond * Time.deltaTime;
        var sp = new ParticleSystem.EmitParams { position = hit, startLifetime = 0.35f };
        for (; sparkDebt >= 1f; sparkDebt--)
        {
            Vector3 v = Random.onUnitSphere; v.y = Mathf.Abs(v.y) + 0.3f;
            sp.velocity = v * Random.Range(2f, 5f);
            sp.startSize = Random.Range(0.06f, 0.13f);
            sp.startColor = Color.Lerp(c, Color.white, Random.value * 0.6f);
            particles.Emit(sp, 1);
        }

        // A flash on the crystal a few times a second, like the drills biting in.
        flashTimer -= Time.deltaTime;
        if (flashTimer <= 0f)
        {
            flashTimer = Random.Range(0.12f, 0.3f);
            particles.Emit(new ParticleSystem.EmitParams
            {
                position = hit, velocity = Vector3.zero, startLifetime = 0.18f,
                startSize = Random.Range(0.8f, 1.3f), startColor = Color.Lerp(c, Color.white, 0.3f),
            }, 1);
        }

        // Data bits flying from the crystal into the cargo glass (they arrive as they fade).
        bitDebt += BitsPerSecond * Time.deltaTime;
        var bp = new ParticleSystem.EmitParams { startLifetime = BitLife };
        for (; bitDebt >= 1f; bitDebt--)
        {
            Vector3 start = hit + Random.insideUnitSphere * 0.5f;
            bp.position = start;
            bp.velocity = (cargo.position - start) / BitLife + Vector3.up * Random.Range(0f, 0.8f);
            bp.startSize = Random.Range(0.14f, 0.24f);
            bp.startColor = c;
            particles.Emit(bp, 1);
        }
    }

    // The patch being mined: the harvester's own target, or (other players' copies) the nearest one.
    OreNode PickOre()
    {
        if (harvester.TargetOre != null) return harvester.TargetOre;
        if (ore != null) return ore;
        OreNode best = null; float bd = 9f; // within 3 m
        foreach (var o in OreNode.All)
        {
            float d = (o.transform.position - transform.position).sqrMagnitude;
            if (d < bd) { bd = d; best = o; }
        }
        return best;
    }

    Vector3 HitPoint()
    {
        var col = ore.GetComponentInChildren<Collider>();
        Vector3 centre = col != null ? col.bounds.center : ore.transform.position + Vector3.up * 0.3f;
        return col != null ? Vector3.Lerp(centre, col.bounds.ClosestPoint(cargo.position), 0.35f) : centre;
    }
}
