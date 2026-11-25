using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class ShieldField : MonoBehaviour
{
    static readonly List<ShieldField> active = new();
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    public float radius = 24f;
    [Range(0f, 0.9f)] public float reduction = 0.4f;
    public Color color = new(1f, 0.75f, 0.3f);
    public bool buildingsOnly;
    public bool needsPower = true;
    public bool showRing = true;
    public string domePart;
    public GameObject domePrefab;

    Health self;
    Renderer dome;
    LineRenderer ring;
    MaterialPropertyBlock block;
    float flash, nextBurst;

    public bool Online => self != null && !self.IsDead && enabled && !(needsPower && Net.LowPower(self.team)) && !Structure.Stunned(this);

    void Awake()
    {
        self = GetComponent<Health>();
        block = new MaterialPropertyBlock();
        if (!string.IsNullOrEmpty(domePart))
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == domePart && t.TryGetComponent(out Renderer r)) { dome = r; t.gameObject.SetActive(true); break; }
        if (dome == null && domePrefab != null)
        {
            var made = Instantiate(domePrefab, transform);
            made.name = "ShieldDome";
            made.transform.localPosition = Vector3.zero;
            var rs = made.GetComponentsInChildren<Renderer>();
            float size = 0.01f;
            foreach (var r in rs) size = Mathf.Max(size, r.bounds.extents.x, r.bounds.extents.z);
            made.transform.localScale *= radius / size;
            dome = rs.Length > 0 ? rs[0] : null;
            foreach (var r in rs) if (r != dome) r.enabled = false;
        }
        if (dome == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.name = "ShieldDome";
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * radius * 2f;
            dome = go.GetComponent<Renderer>();
            dome.sharedMaterial = Resources.Load<Material>("ShieldDome") ?? Juice.AdditiveLine;
        }
        dome.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dome.receiveShadows = false;
        ring = new GameObject("ShieldRing").AddComponent<LineRenderer>();
        ring.transform.SetParent(transform, false);
        ring.sharedMaterial = Juice.AdditiveLine;
        ring.startColor = ring.endColor = new Color(color.r, color.g, color.b, 0.5f);
        ring.widthMultiplier = 0.3f;
        ring.loop = true;
        ring.useWorldSpace = true;
        ring.positionCount = 64;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        for (int i = 0; i < 64; i++)
        {
            float a = i / 64f * Mathf.PI * 2f;
            ring.SetPosition(i, transform.position + new Vector3(Mathf.Cos(a) * radius, 0.2f, Mathf.Sin(a) * radius));
        }
    }

    void OnEnable() => active.Add(this);
    void OnDisable() { active.Remove(this); Show(false); }

    void Show(bool on)
    {
        if (dome != null) dome.enabled = on;
        if (ring != null) ring.enabled = on && showRing;
    }

    void Update()
    {
        bool on = Online && !TryGetComponent(out Construction _) && Juice.Seen(transform.position);
        Show(on);
        if (!on) return;
        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 2.5f);
        float a = 0.006f + 0.004f * Mathf.Sin(Time.time * 1.7f) + flash * 0.16f;
        dome.GetPropertyBlock(block);
        block.SetColor(ColorId, new Color(color.r, color.g, color.b, a));
        block.SetColor(EmissionId, color * (0.04f + flash * 1.1f));
        dome.SetPropertyBlock(block);
    }

    public static float Absorb(Health h, float amount)
    {
        foreach (var f in active)
        {
            if (!f.Online || f.TryGetComponent(out Construction _) || !Net.Allied(f.self.team, h.team)) continue;
            if (f.buildingsOnly && !h.TryGetComponent(out Structure _)) continue;
            if ((h.transform.position - f.transform.position).sqrMagnitude > f.radius * f.radius) continue;
            f.Hit(h);
            return amount * (1f - f.reduction);
        }
        return amount;
    }

    void Hit(Health h)
    {
        flash = Mathf.Min(1f, flash + 0.35f);
        if (Time.time < nextBurst || !Juice.Seen(h.transform.position)) return;
        nextBurst = Time.time + 0.15f;
        Juice.Burst(Flyer.Center(h) + Vector3.up * 1.2f, color, 6, 5f, 0.1f, 0.35f, 0.4f);
    }

    public static bool Covers(Health h)
    {
        foreach (var f in active)
            if (f.Online && Net.Allied(f.self.team, h.team) && (h.transform.position - f.transform.position).sqrMagnitude <= f.radius * f.radius) return true;
        return false;
    }
}
