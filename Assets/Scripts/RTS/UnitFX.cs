using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Shared world-space particle systems, one per look; effects Emit() single particles into them.
//  Smoke: grey puffs that grow and fade (alpha blended, so they show on the dark ground)
//  Dust:  like smoke, smaller and quicker
//  Steam: like smoke, bigger and slower
//  Print: faint wet footprints lying flat on the ground
//  Ripple: rain rings on the ground
//  Rain:  falling streaks
//  Mote:  floating specks that fade in and out
public static class Particles
{
    static readonly Dictionary<string, ParticleSystem> made = new();

    public static void Emit(string kind, Vector3 at, Vector3 velocity, float size, float life, Color c)
    {
        var e = new ParticleSystem.EmitParams { position = at, velocity = velocity, startSize = size, startLifetime = life, startColor = c, applyShapeToPosition = false };
        Get(kind).Emit(e, 1);
    }

    static ParticleSystem Get(string kind)
    {
        if (made.TryGetValue(kind, out var ps) && ps != null) return ps;
        ps = ProductionFX.MakeSparks(null, 0);
        ps.name = "Particles_" + kind;
        var main = ps.main;
        main.maxParticles = 3000;
        main.gravityModifier = 0f;
        var pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var smoke = Resources.Load<Material>("SmokeFX");
        switch (kind)
        {
            case "Smoke": pr.sharedMaterial = smoke; size.size = Grow(0.4f, 1.6f); col.color = Fade(0.1f, 0.3f); break;
            case "Dust": pr.sharedMaterial = smoke; size.size = Grow(0.5f, 1.5f); col.color = Fade(0.1f, 0.2f); break;
            case "Steam": pr.sharedMaterial = smoke; size.size = Grow(0.3f, 2f); col.color = Fade(0.2f, 0.3f); break;
            case "Print": pr.renderMode = ParticleSystemRenderMode.HorizontalBillboard; size.enabled = false; col.color = Fade(0.02f, 0.6f); break;
            case "Ripple":
                pr.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                pr.sharedMaterial = new Material(pr.sharedMaterial) { name = "Ripple", mainTexture = RingTexture() };
                size.size = Grow(0.15f, 1f);
                col.color = Fade(0.01f, 0.01f);
                break;
            case "Rain": pr.renderMode = ParticleSystemRenderMode.Stretch; pr.velocityScale = 0.035f; pr.lengthScale = 1f; size.enabled = false; col.enabled = false; break;
            case "Mote": size.enabled = false; col.color = Fade(0.3f, 0.3f); break;
        }
        made[kind] = ps;
        return ps;
    }

    static ParticleSystem.MinMaxCurve Grow(float from, float to) => new(1f, AnimationCurve.Linear(0f, from, 1f, to));

    // Fades in over the first 'fadeIn' of its life, holds, fades out after 1 - fadeOut.
    static Gradient Fade(float fadeIn, float fadeOut)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(1f, 1f - fadeOut), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    // A thin soft ring, drawn once.
    static Texture2D RingTexture()
    {
        const int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x - n / 2 + 0.5f, y - n / 2 + 0.5f).magnitude / (n / 2f);
                float a = Mathf.Exp(-Mathf.Pow((d - 0.8f) / 0.09f, 2f));
                t.SetPixel(x, y, new Color(a, a, a, a));
            }
        t.Apply();
        return t;
    }
}

// On everything with Health (added by Health.Start). When it loses health:
//  - it flashes white for a moment
//  - optional floating damage numbers (Settings), summed over a quarter second
//  - units below 35% trail smoke and the odd spark
// Health.TakeDamage calls Hurt on the owner's PC, Health.SetFromNet on everyone else's.
public class HitFX : MonoBehaviour
{
    static readonly int ColorId = Shader.PropertyToID("_Color"), EmissionId = Shader.PropertyToID("_EmissionColor");
    static readonly Color OwnDamage = new(1f, 0.4f, 0.4f), EnemyDamage = new(1f, 0.9f, 0.45f), SmokeColor = new(0.22f, 0.22f, 0.25f, 0.85f);
    static MaterialPropertyBlock tmp;

    Health health;
    SelectableUnit unit;
    StatusFX status;
    readonly List<(Renderer r, int slot, Color c)> slots = new();
    (bool had, Color c)[] before; // each slot's own _Color override before the flash
    bool collected, flashing;
    float flashEnd, lastFlash = -1f, pending, pendingSince, nextSmoke, top = 2f;
    CreditsPopup live; // the damage number currently counting up over this unit
    float liveTotal;

    void Awake()
    {
        health = GetComponent<Health>();
        unit = GetComponent<SelectableUnit>();
    }

    public void Hurt(float amount)
    {
        if (amount <= 0f || !Juice.Seen(transform.position)) return;
        if (!collected) Collect();
        if (status == null) status = GetComponent<StatusFX>();
        if (Time.time - lastFlash > 0.12f && (status == null || !status.Cloaked)) Flash();
        if (GameSettings.DamageNumbers) { if (pending <= 0f) pendingSince = Time.time; pending += amount; }
    }

    void Collect()
    {
        collected = true;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] && mats[i].HasProperty(ColorId) && !mats[i].name.StartsWith("SelectionRing") && !mats[i].shader.name.StartsWith("GD5/"))
                    slots.Add((r, i, mats[i].GetColor(ColorId)));
        }
        before = new (bool, Color)[slots.Count];
        top = Mathf.Max(1f, ConstructionSite.RendererBounds(gameObject).max.y - transform.position.y);
    }

    void Flash()
    {
        tmp ??= new MaterialPropertyBlock();
        for (int i = 0; i < slots.Count; i++)
        {
            var (r, s, c) = slots[i];
            if (!r) continue;
            r.GetPropertyBlock(tmp, s);
            if (!flashing) before[i] = (tmp.HasColor(ColorId), tmp.HasColor(ColorId) ? tmp.GetColor(ColorId) : default);
            tmp.SetColor(ColorId, GameSettings.ReduceFlashing ? Color.Lerp(c, Color.white, 0.25f) * 1.2f : Color.Lerp(c, Color.white, 0.75f) * 2.2f);
            r.SetPropertyBlock(tmp, s);
        }
        flashing = true;
        lastFlash = Time.time;
        flashEnd = Time.time + 0.07f;
    }

    // Puts _Color back as it was; keeps whatever glow others (veterancy, building lights) set meanwhile.
    public void EndFlash()
    {
        if (!flashing) return;
        flashing = false;
        for (int i = 0; i < slots.Count; i++)
        {
            var (r, s, _) = slots[i];
            if (!r) continue;
            r.GetPropertyBlock(tmp, s);
            bool glow = tmp.HasColor(EmissionId);
            Color e = glow ? tmp.GetColor(EmissionId) : default;
            tmp.Clear();
            if (glow) tmp.SetColor(EmissionId, e);
            if (before[i].had) tmp.SetColor(ColorId, before[i].c);
            r.SetPropertyBlock(tmp, s);
        }
    }

    void Update()
    {
        if (flashing && Time.time >= flashEnd) EndFlash();

        if (pending > 0f && Time.time - pendingSince > 0.25f)
        {
            // One number per target that adds up while the fight goes on (a new one once it has floated off).
            if (live != null && live.Age < 0.9f) { liveTotal += pending; live.Restart(Mathf.CeilToInt(liveTotal).ToString()); }
            else
            {
                liveTotal = pending;
                Vector3 at = transform.position + Vector3.up * (top + 0.5f) + Random.insideUnitSphere * 0.2f;
                live = CreditsPopup.Show(at, Mathf.CeilToInt(liveTotal).ToString(), Net.Allied(health.team, 0) ? OwnDamage : EnemyDamage);
            }
            pending = 0f;
        }

        if (unit != null && health.enabled && health.Fraction < 0.35f && Time.time >= nextSmoke)
        {
            nextSmoke = Time.time + Random.Range(0.1f, 0.2f);
            if (!collected) Collect();
            if (!Juice.Seen(transform.position)) return;
            Vector3 at = transform.position + Vector3.up * top * 0.75f + Random.insideUnitSphere * 0.3f;
            Particles.Emit("Smoke", at, Vector3.up * 1.6f + Random.insideUnitSphere * 0.4f, Mathf.Clamp(top * 0.35f, 0.6f, 2.5f), 1.6f, SmokeColor);
            if (Random.value < 0.2f) Juice.Burst(at, Juice.Fire, 3, 3f, 0.12f, 0.5f, 0.8f, true);
        }
    }

    void OnDestroy() => EndFlash();
}

// A flyer shot down: it spins and falls trailing smoke and fire, then blows up on the ground and breaks apart there.
public class AirCrash : MonoBehaviour
{
    static readonly Color SmokeColor = new(0.15f, 0.14f, 0.15f, 0.9f);

    // true = the crash took over; it calls death.Die() when it hits the ground.
    public static bool Begin(Health h, UnitDeath death)
    {
        if (death == null || !h.TryGetComponent(out Flyer f) || h.transform.Find("Model") == null) return false;
        var crash = h.gameObject.AddComponent<AirCrash>();
        crash.StartCoroutine(crash.Fall(f, death));
        return true;
    }

    IEnumerator Fall(Flyer f, UnitDeath death)
    {
        if (TryGetComponent(out HitFX hit)) hit.EndFlash();
        f.enabled = false;
        if (TryGetComponent(out NavMeshAgent agent)) agent.enabled = false;
        if (TryGetComponent(out Attacker attacker)) attacker.enabled = false;
        var bar = GetComponentInChildren<HealthBar>();
        if (bar != null) bar.gameObject.SetActive(false);

        var model = transform.Find("Model");
        var anim = model.GetComponentInChildren<Animator>();
        if (anim != null) anim.speed = 0.3f; // wings / rotors winding down
        float h0 = model.localPosition.y, size = f.size;
        float time = Mathf.Clamp(0.8f + h0 * 0.06f, 1f, 1.8f);
        float spin = Random.value < 0.5f ? -1f : 1f;
        Quaternion rot0 = model.localRotation;
        Vector3 drift = transform.forward * Mathf.Min(8f, 2f + size * 1.5f); // keeps some of its speed

        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            model.localPosition = new Vector3(0f, h0 * (1f - k * k), 0f);
            model.localRotation = Quaternion.Euler(30f * k, spin * 420f * k * k, spin * 30f * k) * rot0;
            transform.position += drift * Time.deltaTime / time;
            if (Juice.Seen(transform.position))
            {
                if (Random.value < 0.6f) Particles.Emit("Smoke", model.position + Random.insideUnitSphere * size * 0.3f, Vector3.up * 0.6f, size * 0.7f, 1.6f, SmokeColor);
                if (Random.value < 0.35f) Juice.Burst(model.position, Juice.Fire, 2, 2f, size * 0.15f, 0.4f, 0.3f);
            }
            yield return null;
        }

        Vector3 ground = new(transform.position.x, 0.15f, transform.position.z);
        bool big = size >= 5f;
        Sfx.At(big ? Snd.BoomBig : Snd.BoomSmall, ground, 0.9f);
        if (Juice.Seen(ground))
        {
            Juice.Flash(ground + Vector3.up * 2f, Juice.Fire, big ? 10f : 5f, size * 5f, 0.5f);
            Juice.Ring(ground, Juice.Fire, size * 0.5f, size * 3f, 0.5f, big ? 0.6f : 0.3f);
            Juice.Burst(ground + Vector3.up, Juice.Fire, big ? 40 : 15, size * 1.5f, size * 0.3f, 0.5f, 0.4f);
            Juice.Burst(ground + Vector3.up, Juice.Fire, big ? 60 : 20, size * 2f, 0.15f, 1f, 1f, true);
            for (int i = 0; i < (big ? 14 : 6); i++)
                Particles.Emit("Dust", ground + Random.insideUnitSphere * size, Random.insideUnitSphere * 2f + Vector3.up, size * 0.8f, 1.5f, new Color(0.45f, 0.42f, 0.4f, 0.6f));
            Juice.Shake(ground, big ? 0.7f : 0.2f);
        }
        death.Die(); // debris and scorch where it landed
    }
}

// Ground units: lean into turns and starts/stops, faint wet footprints, and heavy units kick up dust.
public class UnitMotion : MonoBehaviour
{
    static readonly Color PrintColor = new(0.35f, 0.6f, 0.85f, 0.22f), DustColor = new(0.5f, 0.47f, 0.43f, 0.35f);

    Health health;
    Transform model;
    Quaternion rest;
    Vector3 lastPos, vel;
    float lastYaw, pitch, roll, walked, stride, radius, nextDust, leanScale;
    bool heavy, left;

    void Start()
    {
        model = transform.Find("Model");
        if (model == null || TryGetComponent(out Flyer _) || model.localPosition.y > 1f) { enabled = false; return; } // flyers / hovering drones
        health = GetComponent<Health>();
        rest = model.localRotation;
        radius = TryGetComponent(out NavMeshAgent agent) ? agent.radius * transform.lossyScale.x : 1f;
        stride = Mathf.Max(0.8f, radius * 1.3f);
        leanScale = 1f / (1f + radius * 0.4f); // big things lean less
        var data = GetComponent<SelectableUnit>().Data;
        heavy = radius > 1.6f || (data != null && (data.role == UnitData.Role.Heavy || data.role == UnitData.Role.Elite));
        lastPos = transform.position;
        lastYaw = transform.eulerAngles.y;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || !health.enabled) return; // dead: the pieces are on their own
        Vector3 p = transform.position;
        Vector3 v = (p - lastPos) / dt;
        v.y = 0f;
        lastPos = p;
        if (v.sqrMagnitude > 2500f) v = vel; // warped (spawn, network snap)
        Vector3 newVel = Vector3.Lerp(vel, v, 10f * dt);
        float accel = Vector3.Dot((newVel - vel) / dt, transform.forward);
        vel = newVel;
        float yaw = transform.eulerAngles.y, turn = Mathf.DeltaAngle(lastYaw, yaw) / dt;
        lastYaw = yaw;
        float speed = vel.magnitude;

        pitch = Mathf.Lerp(pitch, Mathf.Clamp(accel * 0.3f, -7f, 7f) * leanScale, 8f * dt);
        roll = Mathf.Lerp(roll, Mathf.Clamp(-turn * speed * 0.005f, -8f, 8f) * leanScale, 8f * dt);
        model.localRotation = Quaternion.Euler(pitch, 0f, roll) * rest;

        if (speed < 0.5f || !Juice.Seen(p)) return;
        walked += speed * dt;
        if (walked >= stride)
        {
            walked = 0f;
            left = !left;
            Vector3 side = transform.right * radius * 0.35f * (left ? -1f : 1f);
            Particles.Emit("Print", new Vector3(p.x, p.y + 0.06f, p.z) + side, Vector3.zero, radius * 0.45f, 5f, PrintColor);
        }
        if (heavy && speed > 1.5f && Time.time >= nextDust)
        {
            nextDust = Time.time + 0.1f;
            Vector3 at = p - transform.forward * radius * 0.6f + transform.right * Random.Range(-radius, radius) * 0.6f + Vector3.up * 0.3f;
            Particles.Emit("Dust", at, Vector3.up * 0.7f + Random.insideUnitSphere * 0.5f, radius * 0.9f, 1.2f, DustColor);
        }
    }
}

// A few reusable point lights for muzzle flashes (making a new light per shot would pile up).
public class LightPool : MonoBehaviour
{
    const int Size = 10;
    static LightPool I;
    readonly Light[] lights = new Light[Size];
    readonly float[] born = new float[Size], life = new float[Size], peak = new float[Size];
    int next;

    public static void Flash(Vector3 at, Color c, float intensity = 2.5f, float range = 7f, float time = 0.07f)
    {
        if (!Juice.Seen(at)) return;
        if (I == null) I = new GameObject("MuzzleLights").AddComponent<LightPool>();
        int i = I.next++ % Size;
        var l = I.lights[i];
        l.transform.position = at;
        l.color = c;
        l.range = range;
        intensity *= GameSettings.FlashScale;
        l.intensity = intensity;
        l.enabled = true;
        I.born[i] = Time.time;
        I.life[i] = time;
        I.peak[i] = intensity;
    }

    void Awake()
    {
        for (int i = 0; i < Size; i++)
        {
            var l = new GameObject("MuzzleLight").AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.type = LightType.Point;
            l.shadows = LightShadows.None;
            l.enabled = false;
            lights[i] = l;
        }
    }

    void Update()
    {
        for (int i = 0; i < Size; i++)
        {
            if (!lights[i].enabled) continue;
            float k = (Time.time - born[i]) / life[i];
            if (k >= 1f) lights[i].enabled = false;
            else lights[i].intensity = peak[i] * (1f - k);
        }
    }
}
