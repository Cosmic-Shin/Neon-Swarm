using UnityEngine;

// One-shot effects built in code (no prefabs to wire up): expanding rings, spark bursts, light flashes and
// camera shake, plus the big moments made from them: a building exploding or being sold, a promotion,
// an attack order locking onto its target. Nothing plays where the fog of war hides it.
public static class Juice
{
    public static readonly Color Gold = new(1f, 0.8f, 0.25f), Heal = new(0.35f, 1f, 0.45f), Danger = new(1f, 0.25f, 0.2f), Fire = new(1f, 0.55f, 0.15f);

    static ParticleSystem glow, sparks; // shared: glowing bits (no gravity) and falling sparks
    static Material additive;

    public static Material Additive => additive != null ? additive : additive = Resources.Load<Material>("HarvestFX");

    // Can the player see this spot? (Own and allied stuff is always in sight.)
    public static bool Seen(Vector3 at) => FogOfWar.Instance == null || FogOfWar.Instance.IsVisible(at);

    // ---- building blocks ----

    // A flat ring on the ground (or at any height) growing from 'from' to 'to' metres while it fades.
    // follow: keeps the ring centred on it (a moving target).
    public static void Ring(Vector3 at, Color c, float from, float to, float time, float width = 0.25f, Transform follow = null)
    {
        var lr = new GameObject("JuiceRing").AddComponent<LineRenderer>();
        lr.sharedMaterial = Additive;
        lr.loop = true;
        lr.positionCount = 48;
        lr.useWorldSpace = true;
        lr.widthMultiplier = width;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Vector3 offset = follow != null ? at - follow.position : Vector3.zero;
        Play(lr.gameObject, time, k =>
        {
            Vector3 centre = follow != null ? follow.position + offset : at;
            float r = Mathf.Lerp(from, to, 1f - (1f - k) * (1f - k)); // ease out
            for (int i = 0; i < lr.positionCount; i++)
            {
                float a = i * Mathf.PI * 2f / lr.positionCount;
                lr.SetPosition(i, centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
            lr.startColor = lr.endColor = new Color(c.r, c.g, c.b, 1f - k);
        });
    }

    // Glowing bits flying out (gravity = falling sparks instead).
    public static void Burst(Vector3 at, Color c, int count, float speed, float size = 0.15f, float life = 0.6f, float upward = 0.5f, bool gravity = false)
    {
        var ps = System(gravity);
        var e = new ParticleSystem.EmitParams { startLifetime = life };
        for (int i = 0; i < count; i++)
        {
            Vector3 v = Random.onUnitSphere; v.y = Mathf.Abs(v.y) + upward;
            e.position = at;
            e.velocity = v.normalized * speed * Random.Range(0.5f, 1f);
            e.startSize = size * Random.Range(0.7f, 1.3f);
            e.startColor = Color.Lerp(c, Color.white, Random.value * 0.4f);
            ps.Emit(e, 1);
        }
    }

    public static void Flash(Vector3 at, Color c, float intensity, float range, float time)
    {
        var l = new GameObject("JuiceFlash").AddComponent<Light>();
        l.transform.position = at;
        l.type = LightType.Point;
        l.color = c;
        l.range = range;
        l.shadows = LightShadows.None;
        Play(l.gameObject, time, k => l.intensity = intensity * (1f - k) * (1f - k));
    }

    // Shakes the camera; weaker the further 'at' is from what the camera looks at.
    public static void Shake(Vector3 at, float strength)
    {
        if (!GameSettings.Shake) return;
        var cam = Camera.main;
        if (cam == null) return;
        var focus = cam.transform.position + cam.transform.forward * 30f;
        float d = Vector3.Distance(new Vector3(at.x, 0f, at.z), new Vector3(focus.x, 0f, focus.z));
        float k = Mathf.Clamp01(1f - d / 90f);
        if (k <= 0f) return;
        var shake = cam.GetComponent<CameraShake>() ?? cam.gameObject.AddComponent<CameraShake>();
        shake.Add(strength * k);
    }

    // Runs step(0..1) every frame for 'time' seconds, then destroys go.
    public static void Play(GameObject go, float time, System.Action<float> step)
    {
        var a = go.AddComponent<JuiceAnim>();
        a.time = time;
        a.step = step;
        step(0f);
    }

    static ParticleSystem System(bool gravity)
    {
        ref ParticleSystem ps = ref (gravity ? ref sparks : ref glow);
        if (ps != null) return ps;
        ps = ProductionFX.MakeSparks(null, 0);
        ps.name = gravity ? "JuiceSparks" : "JuiceGlow";
        var main = ps.main;
        main.maxParticles = 1500;
        if (!gravity)
        {
            main.gravityModifier = 0f;
            ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Billboard;
        }
        return ps;
    }

    // ---- moments ----

    // A building blows up: fireball, shockwave rings, flying sparks, a flash and a camera shake.
    public static void BuildingExplosion(GameObject building)
    {
        Sfx.At(Snd.BoomBig, building.transform.position);
        var b = ConstructionSite.RendererBounds(building);
        if (!Seen(b.center)) return;
        float size = Mathf.Max(b.extents.x, b.extents.z);
        Color team = ProductionFX.HoloColor(building);
        Vector3 ground = new(b.center.x, 0.15f, b.center.z);
        Flash(b.center, Fire, 12f, size * 4f, 0.8f);
        Ring(ground, Fire, size * 0.5f, size * 3.5f, 0.7f, 0.8f);
        Ring(ground + Vector3.up * 0.1f, Color.white, size * 0.3f, size * 2.2f, 0.4f, 0.3f);
        Burst(b.center, Fire, 30, 3f, size * 0.5f, 0.5f, 0.2f);          // fireball
        Burst(b.center, team, 50, size * 2.5f, 0.25f, 0.8f, 0.3f);       // neon bits
        Burst(b.center, Fire, 90, size * 3f, 0.18f, 1.2f, 0.8f, true);  // sparks raining down
        Shake(b.center, 0.8f);
        Aftermath(b);
    }

    static readonly Color Soot = new(0.12f, 0.11f, 0.12f, 0.85f);

    // A building just placed: rings ripple out across the ground grid from its footprint, with a flash
    // and a burst of grid-coloured bits where it lands.
    public static void PlacedRipple(GameObject building)
    {
        var col = building.GetComponent<BoxCollider>();
        Vector3 size = col != null ? Vector3.Scale(col.size, building.transform.lossyScale) : Vector3.one * 8f;
        float r = Mathf.Max(size.x, size.z) * 0.5f;
        Vector3 at = new(building.transform.position.x, 0.12f, building.transform.position.z);
        Color c = new(0.3f, 0.95f, 1f);
        Ring(at, c, r * 0.8f, r * 2.6f, 0.55f, 0.25f);
        Ring(at, c, r * 1.0f, r * 4.0f, 0.9f, 0.12f);
        Ring(at, new Color(1f, 0.2f, 0.85f), r * 1.2f, r * 5.5f, 1.3f, 0.08f);
        Flash(at + Vector3.up * 2f, c, 4f, r * 4f, 0.35f);
        Burst(at, c, 24, r * 1.5f, 0.12f, 0.6f, 0.4f);
    }

    static Material additiveLine; // vertex colour does the colour; shared by every scan ring
    public static Material AdditiveLine
    {
        get
        {
            if (additiveLine != null) return additiveLine;
            additiveLine = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            additiveLine.SetColor("_TintColor", new Color(1.2f, 1.2f, 1.2f, 1f)); // over 0.5 so it blooms
            return additiveLine;
        }
    }

    // A new unit from a factory: once it's out of the door (after `delay`), a ring of light in its colour
    // sweeps up its body, with a flash at its feet and sparks.
    public static void Materialize(GameObject unit, float delay)
    {
        if (unit == null) return;
        Color c = ProductionFX.HoloColor(unit);
        var go = new GameObject("Materialize");
        go.transform.SetParent(unit.transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.sharedMaterial = AdditiveLine;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.enabled = false;
        const int N = 32;
        lr.positionCount = N;
        float radius = 1f, height = 2f;
        bool started = false;
        Play(go, delay + 0.75f, k =>
        {
            float t = k * (delay + 0.75f) - delay;
            if (t < 0f || unit == null) return;
            if (!started)
            {
                started = true;
                var b = ConstructionSite.RendererBounds(unit);
                float scale = Mathf.Max(0.01f, unit.transform.lossyScale.x);
                radius = Mathf.Max(b.extents.x, b.extents.z) * 1.1f / scale;
                height = (b.max.y - unit.transform.position.y) / scale;
                for (int i = 0; i < N; i++) { float a = i * Mathf.PI * 2f / N; lr.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius); }
                if (!Seen(unit.transform.position)) return;
                lr.enabled = true;
                Vector3 feet = unit.transform.position + Vector3.up * 0.15f;
                Ring(feet, c, radius * scale * 0.4f, radius * scale * 2f, 0.45f, 0.15f);
                Flash(feet + Vector3.up, c, 3f, radius * scale * 5f, 0.3f);
            }
            float p = Mathf.Clamp01(t / 0.75f);
            go.transform.localPosition = Vector3.up * height * p;
            float w = 0.12f * (1f - p) + 0.03f;
            lr.widthMultiplier = w;
            lr.startColor = lr.endColor = new Color(c.r, c.g, c.b, 1f - p * 0.7f);
            if (lr.enabled && Random.value < 0.5f) Burst(go.transform.position + Random.insideUnitSphere * radius * 0.5f, c, 2, 2f, 0.1f, 0.4f, 0.3f);
        });
    }

    // After the big blast: a chain of smaller ones through the wreck for a second and a half, then a few
    // fires that burn and smoke on the spot for several seconds.
    static void Aftermath(Bounds b)
    {
        float size = Mathf.Max(b.extents.x, b.extents.z);
        var fires = new Vector3[Mathf.Clamp(Mathf.RoundToInt(size / 3f), 2, 5)];
        for (int i = 0; i < fires.Length; i++) fires[i] = new Vector3(Random.Range(b.min.x, b.max.x), 0.3f, Random.Range(b.min.z, b.max.z));
        var anchor = new GameObject("BuildingAftermath");
        float nextPop = 0.15f, nextFire = 0f;
        Play(anchor, 9f, k =>
        {
            float t = k * 9f;
            if (t < 1.6f && t >= nextPop) // secondary explosions
            {
                nextPop = t + Random.Range(0.15f, 0.35f);
                Vector3 p = new(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.center.y + b.extents.y * 0.5f), Random.Range(b.min.z, b.max.z));
                Flash(p, Fire, 5f, size * 1.5f, 0.25f);
                Burst(p, Fire, 14, size * 0.8f, 0.18f, 0.5f, 0.6f, true);
                Particles.Emit("Smoke", p, Vector3.up * 2f, size * 0.4f, 1.6f, Soot);
                Sfx.At(Snd.BoomSmall, p, 0.5f);
            }
            if (t >= nextFire) // lingering fires, dying down
            {
                nextFire = t + 0.12f;
                float left = 1f - k;
                foreach (var f in fires)
                {
                    if (Random.value > left) continue;
                    Burst(f + Random.insideUnitSphere * 0.6f, Fire, 2, 2.5f, 0.2f * left + 0.08f, 0.6f, 2.5f);
                    if (Random.value < 0.35f) Particles.Emit("Smoke", f + Vector3.up * 1.5f, Vector3.up * 2.2f + Random.insideUnitSphere * 0.4f, 1.5f + size * 0.15f, 2.4f, Soot);
                }
            }
        });
    }

    // Sold (or removed): the building turns into a hologram that sinks into the ground; credits pop up.
    // credits = 0: another player's building, no popup.
    public static void Sell(GameObject building, int credits)
    {
        Sfx.At(Snd.Coin, building.transform.position);
        var b = ConstructionSite.RendererBounds(building);
        if (!Seen(b.center)) return;
        Color c = ProductionFX.HoloColor(building);
        // A hologram copy that dissolves top-down: a scan line sweeps from roof to ground (GD5/Hologram draws
        // only below _CutY and lights the cut), throwing sparks off the line as it goes.
        var holo = ConstructionSite.MakeHologram(building, building.transform.position, building.transform.rotation, new Color(c.r, c.g, c.b, 0.5f));
        holo.transform.localScale = building.transform.lossyScale;
        var mat = new Material(Shader.Find("GD5/Hologram"));
        mat.SetColor("_Color", new Color(c.r * 1.5f, c.g * 1.5f, c.b * 1.5f, 1f));
        foreach (var r in holo.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }
        float top = b.max.y, bottom = b.min.y;
        Play(holo, 1.4f, k =>
        {
            float cut = Mathf.Lerp(top, bottom, k * k * (3f - 2f * k));
            mat.SetFloat("_CutY", cut);
            if (Random.value < 0.8f) Burst(new Vector3(Random.Range(b.min.x, b.max.x), cut, Random.Range(b.min.z, b.max.z)), c, 3, 4f, 0.15f, 0.6f, 0.5f, true);
        });
        Ring(new Vector3(b.center.x, 0.15f, b.center.z), c, Mathf.Max(b.extents.x, b.extents.z), Mathf.Max(b.extents.x, b.extents.z) * 1.6f, 0.8f, 0.3f);
        if (credits > 0) CreditsPopup.Show(b.center + Vector3.up * b.extents.y, $"+${credits}", Gold);
    }

    // A unit ranks up: golden ring and burst, a flash and its new rank name floating up.
    public static void Promoted(GameObject unit, string rankName)
    {
        Sfx.At(Snd.Promote, unit.transform.position);
        Vector3 p = unit.transform.position;
        if (!Seen(p)) return;
        Ring(p + Vector3.up * 0.2f, Gold, 0.5f, 4f, 0.6f, 0.2f, unit.transform);
        Burst(p + Vector3.up * 1f, Gold, 30, 4f, 0.2f, 0.7f, 1.5f);
        Flash(p + Vector3.up * 2f, Gold, 6f, 10f, 0.5f);
        CreditsPopup.Show(p + Vector3.up * 4f, rankName.ToUpper(), Gold);
    }

    // Move order: a ring and four chevrons close in on the spot.
    public static void MoveMarker(Vector3 at, Color c)
    {
        at.y += 0.12f;
        Ring(at, c, 2.4f, 0.3f, 0.35f, 0.18f);
        for (int i = 0; i < 4; i++)
        {
            float a = (i * 90f + 45f) * Mathf.Deg2Rad;
            Vector3 dir = new(Mathf.Cos(a), 0f, Mathf.Sin(a)), side = Vector3.Cross(Vector3.up, dir);
            var lr = Line(c, 0.16f, 3);
            Play(lr.gameObject, 0.45f, k =>
            {
                float d = Mathf.Lerp(3.2f, 0.5f, 1f - (1f - k) * (1f - k));
                Vector3 tip = at + dir * d;
                lr.SetPosition(0, tip + dir * 0.6f + side * 0.6f); // V pointing at the spot
                lr.SetPosition(1, tip);
                lr.SetPosition(2, tip + dir * 0.6f - side * 0.6f);
                lr.startColor = lr.endColor = new Color(c.r, c.g, c.b, 1f - k);
            });
        }
    }

    // A thin line from a unit to where it was sent, fading out.
    public static void OrderLine(Vector3 from, Vector3 to, Color c)
    {
        var lr = Line(c, 0.07f, 2);
        from.y += 0.2f; to.y += 0.15f;
        Play(lr.gameObject, 0.5f, k =>
        {
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startColor = new Color(c.r, c.g, c.b, 0.15f * (1f - k));
            lr.endColor = new Color(c.r, c.g, c.b, 0.6f * (1f - k));
        });
    }

    static LineRenderer Line(Color c, float width, int points)
    {
        var lr = new GameObject("JuiceLine").AddComponent<LineRenderer>();
        lr.sharedMaterial = Additive;
        lr.positionCount = points;
        lr.useWorldSpace = true;
        lr.widthMultiplier = width;
        lr.startColor = lr.endColor = c;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return lr;
    }

    // Construction done: it settles with a thump, a dust ring rolls out and sparks fly off the roof.
    public static void ConstructionDone(GameObject building)
    {
        var b = ConstructionSite.RendererBounds(building);
        Sfx.At(Snd.Cannon, b.center, 0.5f);
        if (!Seen(b.center)) return;
        float size = Mathf.Max(b.extents.x, b.extents.z);
        Vector3 ground = new(b.center.x, 0.15f, b.center.z);
        Color dust = new(0.55f, 0.5f, 0.45f, 0.55f);
        Ring(ground, new Color(0.8f, 0.75f, 0.65f), size * 0.9f, size * 2.2f, 0.8f, 0.5f);
        for (int i = 0; i < 28; i++)
        {
            float a = i * Mathf.PI * 2f / 28f;
            Vector3 dir = new(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Particles.Emit("Dust", ground + dir * size * 0.9f, dir * Random.Range(2f, 4f) + Vector3.up * 0.4f, size * 0.4f, 1.4f, dust);
        }
        Burst(new Vector3(b.center.x, b.max.y, b.center.z), ProductionFX.HoloColor(building), 25, 5f, 0.15f, 0.7f, 1f);
        Flash(b.center + Vector3.up * b.extents.y, ProductionFX.HoloColor(building), 4f, size * 3f, 0.4f);
        Shake(b.center, 0.2f);
    }

    // Attack order: a red reticle closes in on the target twice.
    public static void TargetLock(Health target)
    {
        if (target == null) return;
        Sfx.Play(Snd.Ack, 0.5f);
        float r = target.TryGetComponent(out Collider c) ? Mathf.Max(c.bounds.extents.x, c.bounds.extents.z) + 0.5f : 1.5f;
        Vector3 at = target.transform.position + Vector3.up * 0.2f;
        Ring(at, Danger, r * 3f, r, 0.3f, 0.2f, target.transform);
        var t = target.transform;
        var delay = new GameObject("JuiceDelay");
        Play(delay, 0.15f, k => { if (k >= 1f && t != null) Ring(t.position + Vector3.up * 0.2f, Danger, r * 2f, r * 0.9f, 0.3f, 0.12f, t); });
        Flash(at + Vector3.up, Danger, 3f, 6f, 0.3f);
    }
}

// Drives Juice.Play: calls step with 0..1, then removes the object.
public class JuiceAnim : MonoBehaviour
{
    public float time;
    public System.Action<float> step;
    float age;

    void Update()
    {
        age += Time.deltaTime;
        float k = Mathf.Clamp01(age / time);
        step(k);
        if (k >= 1f) Destroy(gameObject);
    }
}

// Camera shake on top of CameraPan: the offset is added after everything moved the camera
// and taken off again before CameraPan reads the position next frame.
[DefaultExecutionOrder(-1000)]
public class CameraShake : MonoBehaviour
{
    float strength;
    Vector3 applied;

    public void Add(float s) => strength = Mathf.Min(1.5f, strength + s);

    void Update() { transform.position -= applied; applied = Vector3.zero; }

    void LateUpdate()
    {
        if (strength <= 0.001f) return;
        applied = Random.insideUnitSphere * strength;
        transform.position += applied;
        strength = Mathf.MoveTowards(strength, 0f, Time.unscaledDeltaTime * 2.5f);
    }
}
