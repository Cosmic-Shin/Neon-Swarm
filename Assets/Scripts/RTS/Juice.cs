using UnityEngine;

// One-shot effects built in code (no prefabs to wire up): expanding rings, spark bursts, light flashes and
// camera shake, plus the big moments made from them: a building exploding or being sold, a promotion,
// an attack order locking onto its target. Nothing plays where the fog of war hides it.
public static class Juice
{
    public static readonly Color Gold = new(1f, 0.8f, 0.25f), Heal = new(0.35f, 1f, 0.45f), Danger = new(1f, 0.25f, 0.2f), Fire = new(1f, 0.55f, 0.15f);

    static ParticleSystem glow, sparks; // shared: glowing bits (no gravity) and falling sparks
    static Material additive;

    static Material Additive => additive != null ? additive : additive = Resources.Load<Material>("HarvestFX");

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
    }

    // Sold (or removed): the building turns into a hologram that sinks into the ground; credits pop up.
    // credits = 0: another player's building, no popup.
    public static void Sell(GameObject building, int credits)
    {
        var b = ConstructionSite.RendererBounds(building);
        if (!Seen(b.center)) return;
        Color c = ProductionFX.HoloColor(building);
        var holo = ConstructionSite.MakeHologram(building, building.transform.position, building.transform.rotation, new Color(c.r, c.g, c.b, 0.5f));
        holo.transform.localScale = building.transform.lossyScale;
        Vector3 start = holo.transform.position;
        float height = b.size.y;
        Play(holo, 1.2f, k =>
        {
            holo.transform.position = start + Vector3.down * height * k * k;
            ConstructionSite.Tint(holo, new Color(c.r, c.g, c.b, 0.5f * (1f - k)));
            if (Random.value < 0.6f) Burst(new Vector3(Random.Range(b.min.x, b.max.x), b.min.y + height * (1f - k * k), Random.Range(b.min.z, b.max.z)), c, 2, 3f, 0.2f, 0.7f, 2f);
        });
        Ring(new Vector3(b.center.x, 0.15f, b.center.z), c, Mathf.Max(b.extents.x, b.extents.z), Mathf.Max(b.extents.x, b.extents.z) * 1.6f, 0.8f, 0.3f);
        if (credits > 0) CreditsPopup.Show(b.center + Vector3.up * b.extents.y, $"+${credits}", Gold);
    }

    // A unit ranks up: golden ring and burst, a flash and its new rank name floating up.
    public static void Promoted(GameObject unit, string rankName)
    {
        Vector3 p = unit.transform.position;
        if (!Seen(p)) return;
        Ring(p + Vector3.up * 0.2f, Gold, 0.5f, 4f, 0.6f, 0.2f, unit.transform);
        Burst(p + Vector3.up * 1f, Gold, 30, 4f, 0.2f, 0.7f, 1.5f);
        Flash(p + Vector3.up * 2f, Gold, 6f, 10f, 0.5f);
        CreditsPopup.Show(p + Vector3.up * 4f, rankName.ToUpper(), Gold);
    }

    // Attack order: a red reticle closes in on the target twice.
    public static void TargetLock(Health target)
    {
        if (target == null) return;
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
