using UnityEngine;

// Look of a factory at work: a hologram of the unit being made (or the icon of the research project) is
// printed bottom-up above the roof (scan ring at the print line), a light pulses and sparks spray at the exit.
// Flashes when it is done.
// Fed by Building.Update on the owner's PC and by NetEntity.Render everywhere else.
public class ProductionFX : MonoBehaviour
{
    const float HoloSize = 5f, SpinSpeed = 40f, FlashTime = 0.5f, SparksPerSecond = 25f;
    static readonly Color Cyan = new(0.3f, 1f, 1f);

    public Transform exit; // the door: sparks + light here
    public Transform titanDock; // Hive Aerie Mk III: titans are printed full size here instead of above the roof

    Object shown;   // the unit prefab or research icon on show
    bool picture;   // research icon: a flat card that faces the camera
    float boost;    // brightness: a flat card has no rim glow, so it needs more
    Vector3 mid; // centre of the body, on the ground
    float progress, holoBase, holoHeight, roof, sparkDebt, flash;
    Transform holo;
    Material holoMat;
    LineRenderer ring, beam;
    ParticleSystem sparks;
    Light glow;
    Color color;
    Vector3 glowHome;
    bool titan;           // printing a titan at the mooring mast
    float titanRadius;
    Vector3 titanHead;    // the mast top the projector beam rises from
    readonly System.Collections.Generic.List<Transform> tips = new();
    readonly System.Collections.Generic.List<LineRenderer> welds = new();

    // what = the unit prefab or research icon in production (null = nothing), progress 0..1.
    public void Show(Object what, float progress01)
    {
        if (what != shown)
        {
            if (shown != null && holo != null) flash = FlashTime; // finished (or cancelled): flash the old one out
            else Clear();
            shown = what;
            if (what != null) { Clear(); flash = 0f; Build(what); }
        }
        progress = progress01;
    }

    void Start()
    {
        // Top of the main body (the collider), not of the tallest antenna.
        mid = new Vector3(transform.position.x, 0f, transform.position.z);
        if (TryGetComponent(out Collider body)) { roof = body.bounds.max.y; mid = new Vector3(body.bounds.center.x, 0f, body.bounds.center.z); }
        else foreach (var r in GetComponentsInChildren<Renderer>()) roof = Mathf.Max(roof, r.bounds.max.y);
        var material = Resources.Load<Material>("HarvestFX");
        int layer = gameObject.layer;

        ring = new GameObject("PrintRing").AddComponent<LineRenderer>();
        ring.transform.SetParent(transform, false);
        ring.gameObject.layer = layer;
        ring.sharedMaterial = material;
        ring.loop = true;
        ring.positionCount = 32;
        ring.widthMultiplier = 0.08f;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.enabled = false;

        // Projector cone from the roof up to the hologram.
        beam = new GameObject("ProjectorBeam").AddComponent<LineRenderer>();
        beam.transform.SetParent(transform, false);
        beam.gameObject.layer = layer;
        beam.sharedMaterial = material;
        beam.positionCount = 2;
        beam.widthCurve = AnimationCurve.Linear(0f, 0.4f, 1f, HoloSize * 0.9f);
        beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beam.enabled = false;

        var at = exit != null ? exit : transform;
        sparks = MakeSparks(at, layer);
        glow = MakeLight(at, layer);
        glow.transform.position = at.position + Vector3.up * (at.position.y < 1.5f ? 1.5f : -1f);
        glowHome = glow.transform.position;
    }

    // Falling, stretched sparks that the caller Emit()s by hand (also used by UpgradeFX).
    public static ParticleSystem MakeSparks(Transform parent, int layer)
    {
        var ps = new GameObject("Sparks").AddComponent<ParticleSystem>();
        ps.transform.SetParent(parent, false);
        ps.gameObject.layer = layer;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.maxParticles = 200;
        main.gravityModifier = 1.5f;
        var emission = ps.emission; emission.enabled = false;
        var shape = ps.shape; shape.enabled = false;
        var pr = ps.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = Resources.Load<Material>("HarvestFX");
        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.velocityScale = 0.05f;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }

    public static Light MakeLight(Transform parent, int layer)
    {
        var l = new GameObject("WorkLight").AddComponent<Light>();
        l.transform.SetParent(parent, false);
        l.gameObject.layer = layer;
        l.type = LightType.Point;
        l.range = 8f;
        l.shadows = LightShadows.None;
        l.enabled = false;
        return l;
    }

    // Hologram colour for this building: its team's (cyan for your own side offline, like TeamLook).
    public static Color HoloColor(GameObject go)
    {
        int team = go.TryGetComponent(out Health h) ? h.team : 0;
        Color c = !Net.Online && team == 0 ? Cyan : Net.ColorOf(team);
        return c == Color.white ? Cyan : c;
    }

    void Build(Object what)
    {
        color = HoloColor(gameObject);
        ring.startColor = ring.endColor = color;
        holoMat = new Material(Shader.Find("GD5/Hologram"));
        holoBase = roof + 0.3f;
        picture = what is Sprite;
        boost = picture ? 5f : 1.6f;
        glow.transform.position = glowHome; glow.range = 8f;
        titan = what is SelectableUnit su && su.Data.role == UnitData.Role.Elite && titanDock != null && titanDock.gameObject.activeInHierarchy;
        if (what is Sprite icon) BuildCard(icon);
        else BuildModel((SelectableUnit)what);
        if (titan) PlaceAtDock();
    }

    void BuildCard(Sprite icon)
    {
        var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
        DestroyImmediate(card.GetComponent<Collider>());
        holo = card.transform;
        holo.name = "Hologram";
        holo.SetParent(transform, false);
        card.layer = gameObject.layer;
        var r = card.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.sharedMaterial = holoMat;

        // Just this sprite's part of its texture.
        Texture tex = icon.texture;
        Rect px = icon.textureRect;
        holoMat.mainTexture = tex;
        holoMat.mainTextureScale = new Vector2(px.width / tex.width, px.height / tex.height);
        holoMat.mainTextureOffset = new Vector2(px.x / tex.width, px.y / tex.height);

        float size = HoloSize * 0.7f;
        holoHeight = size * px.height / Mathf.Max(px.width, px.height);
        Vector3 world = new(size * px.width / Mathf.Max(px.width, px.height), holoHeight, 1f);
        holo.localScale = new Vector3(world.x / transform.lossyScale.x, world.y / transform.lossyScale.y, 1f);
        holo.position = mid + Vector3.up * (holoBase + holoHeight * 0.5f);
    }

    void BuildModel(SelectableUnit unit)
    {
        var model = unit.transform.Find("Model");
        // Copied under an inactive parent so none of its scripts wake up, then stripped to just the looks.
        var hold = new GameObject();
        hold.SetActive(false);
        holo = Instantiate(model != null ? model.gameObject : unit.gameObject, hold.transform).transform;
        holo.name = "Hologram";
        foreach (var c in holo.GetComponentsInChildren<Component>(true))
            if (!(c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer || c is Animator)) DestroyImmediate(c);
        holo.SetParent(transform, false);
        Destroy(hold);

        var rends = holo.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rends)
        {
            r.sharedMaterials = System.Array.ConvertAll(r.sharedMaterials, _ => holoMat);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.gameObject.layer = gameObject.layer;
        }

        // Fit it to HoloSize, standing on a spot just above the roof.
        holo.localRotation = Quaternion.identity;
        holo.position = Vector3.zero;
        holo.localScale = Vector3.one;
        Bounds b = Bounds(rends);
        float scale = HoloSize / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.y, b.size.z));
        holo.localScale *= scale;
        b = Bounds(rends);
        holoHeight = b.size.y;
        holo.position += new Vector3(mid.x - b.center.x, holoBase - b.min.y, mid.z - b.center.z);
    }

    // Titan print: a beam up the mast to the print line, welding arcs from each clamp tip to it, sparks raining off.
    void WeldAndBeam(float cut, float radius)
    {
        beam.SetPosition(0, titanHead);
        beam.SetPosition(1, new Vector3(titanHead.x, Mathf.Max(cut, titanHead.y + 0.5f), titanHead.z));
        beam.startColor = color * 0.6f;
        beam.endColor = color * 0.2f;
        glow.color = color;
        glow.intensity = 6f + Mathf.Sin(Time.time * 20f) * 3f;

        var p = new ParticleSystem.EmitParams { startColor = Color.Lerp(color, new Color(1f, 0.8f, 0.4f), 0.5f), startSize = 0.18f, startLifetime = 1.2f };
        for (int i = 0; i < welds.Count && i < tips.Count; i++)
        {
            float a = Random.value * Mathf.PI * 2f;
            Vector3 spot = new(mid.x + Mathf.Cos(a) * radius * 0.8f, cut, mid.z + Mathf.Sin(a) * radius * 0.8f);
            welds[i].SetPosition(0, tips[i].position);
            welds[i].SetPosition(1, spot);
            welds[i].startColor = color * 3f;
            welds[i].endColor = Color.white * 3f;
            for (int s = 0; s < 3; s++)
            {
                p.position = spot;
                p.velocity = (Random.insideUnitSphere + Vector3.up * 0.3f) * Random.Range(3f, 8f);
                sparks.Emit(p, 1);
            }
        }
    }

    // Titan: real size, in the mast clamps, facing the way it will leave, so the finished titan appears right where it was printed.
    void PlaceAtDock()
    {
        holo.SetParent(transform, false);
        var model = ((SelectableUnit)shown).transform.Find("Model");
        if (model != null) holo.localScale = model.localScale;
        holo.SetPositionAndRotation(titanDock.position, transform.rotation);
        Bounds b = Bounds(holo.GetComponentsInChildren<Renderer>(true));
        holoBase = b.min.y;
        holoHeight = b.size.y;
        titanRadius = Mathf.Max(b.extents.x, b.extents.z) * 1.05f;
        mid = new Vector3(b.center.x, 0f, b.center.z);
        titanHead = new Vector3(titanDock.position.x, titanDock.parent.position.y, titanDock.position.z);
        foreach (Transform t in titanDock.parent) if (t.name == "AerMk3_Head") titanHead = t.position;

        tips.Clear();
        foreach (Transform t in titanDock.parent) if (t.name.StartsWith("AerMk3_ClampTip")) tips.Add(t);
        var material = Resources.Load<Material>("HarvestFX");
        while (welds.Count < tips.Count)
        {
            var lr = new GameObject("Weld").AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.sharedMaterial = material;
            lr.positionCount = 2;
            lr.widthMultiplier = 0.12f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            welds.Add(lr);
        }
        glow.transform.position = titanDock.position;
        glow.range = titanRadius * 3f;
    }

    static Bounds Bounds(Renderer[] rends)
    {
        if (rends.Length == 0) return new Bounds();
        Bounds b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        return b;
    }

    void Clear()
    {
        if (holo != null) Destroy(holo.gameObject);
        if (holoMat != null) Destroy(holoMat);
        holo = null;
    }

    void LateUpdate()
    {
        if (ring == null) return; // before Start
        bool working = holo != null && (shown != null || flash > 0f);
        ring.enabled = beam.enabled = glow.enabled = working;
        foreach (var w in welds) w.enabled = working && titan && shown != null && Random.value > 0.2f; // crackle
        if (!working) return;

        if (titan) { } // moored: no spin
        else if (!picture) holo.RotateAround(mid, Vector3.up, SpinSpeed * Time.deltaTime);
        else if (Camera.main != null) // the card turns to face the camera (upright)
            holo.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(holo.position - Camera.main.transform.position, Vector3.up));
        float flicker = Random.value < 0.05f ? 0.4f : 1f;

        if (shown == null) // done: whole hologram flashes bright and fades
        {
            flash -= Time.deltaTime;
            float k = Mathf.Clamp01(flash / FlashTime);
            holoMat.SetFloat("_CutY", 1e4f);
            holoMat.SetColor("_Color", color * boost * (1f + 2f * k) * k);
            glow.color = color;
            glow.intensity = 8f * k;
            ring.enabled = beam.enabled = false;
            if (flash <= 0f) Clear();
            return;
        }

        float cut = holoBase + holoHeight * Mathf.Clamp01(progress);
        holoMat.SetFloat("_CutY", cut);
        holoMat.SetColor("_Color", color * boost * flicker);

        // Ring at the print line.
        float radius = titan ? titanRadius : HoloSize * 0.55f;
        for (int i = 0; i < ring.positionCount; i++)
        {
            float a = i * Mathf.PI * 2f / ring.positionCount;
            ring.SetPosition(i, new Vector3(mid.x + Mathf.Cos(a) * radius, cut, mid.z + Mathf.Sin(a) * radius));
        }

        if (titan) { WeldAndBeam(cut, radius); return; }
        beam.SetPosition(0, mid + Vector3.up * roof);
        beam.SetPosition(1, mid + Vector3.up * cut);
        beam.startColor = color * 0.5f;
        beam.endColor = color * 0.15f;

        glow.color = color;
        glow.intensity = (2f + Mathf.Sin(Time.time * 12f) * 1.2f) * flicker;

        sparkDebt += SparksPerSecond * Time.deltaTime;
        var p = new ParticleSystem.EmitParams { startColor = Color.Lerp(color, new Color(1f, 0.8f, 0.4f), 0.5f), startSize = 0.08f, startLifetime = 0.6f };
        Vector3 outward = Vector3.ProjectOnPlane(sparks.transform.position - mid, Vector3.up).normalized;
        Vector3 from = sparks.transform.position;
        from.y = Random.Range(0.3f, Mathf.Max(from.y, 2f)); // anywhere up the doorway
        for (; sparkDebt >= 1f; sparkDebt--)
        {
            p.position = from;
            p.velocity = (outward + Random.insideUnitSphere * 0.8f + Vector3.up * 0.6f) * Random.Range(3f, 6f);
            sparks.Emit(p, 1);
        }
    }
}
