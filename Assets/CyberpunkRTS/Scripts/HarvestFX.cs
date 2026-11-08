using UnityEngine;

/// <summary>
/// Mining look for a harvester: two crackling beams from the drill bits into the crystal, sparks where
/// they hit, glowing data bits flying into the cargo glass and a flickering light, tinted like the ore
/// (lime, or red for rich ore). Driven only by Harvester.AnimIndex, so it also plays on other players'
/// copies in multiplayer. Added by Harvester.Awake; children of the harvester, so fog of war hides it too.
/// Unloading at a refinery: the cargo streams up a spiralling light column into the funnel above the dock,
/// part of it flows on into the silos, and the credits pop up as "+$N" when they land (Delivered).
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
    float sparkDebt, bitDebt, flashTimer, siloDebt, pulseTimer;
    Color cargoColor = Lime;     // colour of the last ore mined
    Transform funnel, arm;       // unloading: the refinery funnel above our dock, and its intake arm on the roof
    readonly System.Collections.Generic.List<Transform> silos = new();

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
        bool unloading = !mining && harvester.AnimIndex == 2 && !hiddenByFog && FindFunnel();
        foreach (var b in beams) b.enabled = mining;
        glow.enabled = mining || unloading;
        if (unloading) { Unload(); return; }
        if (!mining) { ore = null; return; }
        funnel = null;

        Color c = ore.valueMultiplier > 1f ? Red : Lime;
        cargoColor = c;
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

    // ---- unloading ----

    // The funnel above our dock on the nearest refinery (Mk II refineries have a second one), and its silos.
    bool FindFunnel()
    {
        if (funnel != null && funnel.gameObject.activeInHierarchy) return true;
        funnel = null;
        silos.Clear();
        Refinery refinery = null; float bd = 15f * 15f;
        foreach (var r in Refinery.All)
        {
            if (r == null) continue;
            float d = (r.transform.position - transform.position).sqrMagnitude;
            if (d < bd) { bd = d; refinery = r; }
        }
        if (refinery == null) return false;
        float best = float.MaxValue, bestArm = float.MaxValue;
        arm = null;
        foreach (var t in refinery.GetComponentsInChildren<Transform>())
        {
            if (t.name == "Ref_Silo-1" || t.name == "Ref_Silo1") silos.Add(t);
            float d = (t.position - transform.position).sqrMagnitude;
            if (t.name.EndsWith("IntakeArmGlow") && d < bestArm) { bestArm = d; arm = t; }
            if (t.name.EndsWith("FunnelGlow") && d < best) { best = d; funnel = t; }
        }
        if (funnel == null) // no funnel (the Core Nexus also takes ore): stream into the building's core
        {
            funnel = refinery.transform.Find("GD5_Intake");
            if (funnel == null && refinery.TryGetComponent(out Collider body))
            {
                funnel = new GameObject("GD5_Intake").transform;
                funnel.SetParent(refinery.transform, false);
                Vector3 above = refinery.dockPoint != null ? refinery.dockPoint.position : refinery.transform.position;
                above.y = body.bounds.min.y + body.bounds.size.y * 0.35f;
                Vector3 core = new(body.bounds.center.x, above.y, body.bounds.center.z);
                funnel.position = Vector3.Lerp(body.ClosestPoint(above), core, 0.35f); // just inside the wall facing the dock
            }
        }
        return funnel != null;
    }

    void Unload()
    {
        Color c = cargoColor;
        float t = Time.time;
        Vector3 from = cargo.position, to = funnel.position;

        // A light column spiralling up from the cargo glass into the funnel.
        var lr = beams[0];
        lr.enabled = true;
        Vector3 up = (to - from).normalized;
        Vector3 side = Vector3.Cross(up, Vector3.forward).sqrMagnitude > 0.01f ? Vector3.Cross(up, Vector3.forward).normalized : Vector3.right;
        Vector3 side2 = Vector3.Cross(up, side);
        for (int p = 0; p < ArcPoints; p++)
        {
            float k = p / (ArcPoints - 1f);
            float a = k * 9f - t * 10f;
            lr.SetPosition(p, Vector3.Lerp(from, to, k) + (side * Mathf.Cos(a) + side2 * Mathf.Sin(a)) * 0.3f * Mathf.Sin(k * Mathf.PI));
        }
        float w = 0.5f + 0.15f * Mathf.Sin(t * 14f);
        lr.startWidth = w;
        lr.endWidth = w * 1.4f;
        lr.startColor = Color.Lerp(c, Color.white, 0.3f);
        lr.endColor = Color.Lerp(c, Color.white, 0.6f);

        glow.transform.position = Vector3.Lerp(from, to, 0.3f);
        glow.color = c;
        glow.intensity = 1.8f + 0.8f * Mathf.Sin(t * 10f);

        // Data bits rising into the funnel...
        bitDebt += BitsPerSecond * 1.5f * Time.deltaTime;
        var bp = new ParticleSystem.EmitParams { startLifetime = BitLife };
        for (; bitDebt >= 1f; bitDebt--)
        {
            Vector3 start = from + Random.insideUnitSphere * 0.4f;
            Vector3 swirl = Vector3.Cross(up, start - from).normalized * Random.Range(1f, 2.5f); // spin round the column
            bp.position = start;
            bp.velocity = (to - start) / BitLife + swirl;
            bp.startSize = Random.Range(0.25f, 0.45f);
            bp.startColor = Color.Lerp(c, Color.white, Random.value * 0.4f);
            particles.Emit(bp, 1);
        }

        // A pulse of light on the dock pad, like a pump beating.
        pulseTimer -= Time.deltaTime;
        if (pulseTimer <= 0f)
        {
            pulseTimer = 0.35f;
            particles.Emit(new ParticleSystem.EmitParams
            {
                position = new Vector3(from.x, transform.position.y + 0.2f, from.z), velocity = Vector3.zero,
                startLifetime = 0.3f, startSize = 3.5f, startColor = new Color(c.r, c.g, c.b, 0.6f),
            }, 1);
        }

        // ...and out of the intake arm on the roof, arcing over into the silos (the part you can see).
        if (silos.Count == 0) return;
        Vector3 roof = arm != null ? arm.position : to + Vector3.up;
        siloDebt += 45f * Time.deltaTime;
        const float SiloLife = 0.9f;
        var sp = new ParticleSystem.EmitParams { startLifetime = SiloLife };
        for (; siloDebt >= 1f; siloDebt--)
        {
            Vector3 silo = silos[Random.Range(0, silos.Count)].position + Vector3.up * Random.Range(0f, 2f);
            sp.position = roof + Random.insideUnitSphere * 0.2f;
            sp.velocity = (silo - sp.position) / SiloLife; // arrives as it fades
            sp.startSize = Random.Range(0.35f, 0.6f);
            sp.startColor = Color.Lerp(c, Color.white, Random.value * 0.3f);
            particles.Emit(sp, 1);
        }
    }

    // Owner only (Harvester, when the unload finishes): the credits pop up over the funnel.
    public void Delivered(int credits)
    {
        if (credits <= 0) return;
        Vector3 at = (funnel != null ? funnel.position : transform.position) + Vector3.up * 2f;
        CreditsPopup.Show(at, $"+${credits}", cargoColor);
        if (particles != null) // a burst at the funnel as it lands
            for (int i = 0; i < 25; i++)
                particles.Emit(new ParticleSystem.EmitParams { position = at - Vector3.up * 2f, velocity = Random.onUnitSphere * 4f, startLifetime = 0.4f, startSize = 0.15f, startColor = cargoColor }, 1);
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
