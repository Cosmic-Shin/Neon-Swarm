using System.Collections.Generic;
using UnityEngine;

// What's happening to a unit, shown on it:
//  - EMP-stunned: blue electric arcs crackle over it and a light flickers.
//  - Cloaked (ours or an ally's, inside a working Cloak Field): it turns into a see-through purple shimmer.
// Added to every unit by Health.Start. Other players' units: NetEntity sets RemoteStunned.
public class StatusFX : MonoBehaviour
{
    static readonly Color Electric = new(0.6f, 0.85f, 1f), CloakTint = new(0.55f, 0.35f, 1f);
    static Material cloakMat;
    const int Arcs = 3, ArcPoints = 6;

    public bool RemoteStunned;

    Health health;
    SelectableUnit unit;
    Renderer[] rends;
    Material[][] normal; // each renderer's own materials, while cloaked
    LineRenderer[] arcs;
    Light zap;
    float height = 2f, radius = 1f, nextArc;
    bool cloaked;

    void Start()
    {
        health = GetComponent<Health>();
        unit = GetComponent<SelectableUnit>();
        var list = new List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            if (r is MeshRenderer || r is SkinnedMeshRenderer) list.Add(r);
        rends = list.ToArray();
        var b = ConstructionSite.RendererBounds(gameObject);
        height = Mathf.Max(0.5f, b.size.y);
        radius = Mathf.Max(0.4f, Mathf.Max(b.extents.x, b.extents.z));
    }

    void OnDisable() => SetCloak(false);

    void Update()
    {
        bool alive = health != null && health.enabled;
        bool stunned = alive && ((unit != null && unit.IsStunned) || RemoteStunned) && Juice.Seen(transform.position);
        Stun(stunned);
        SetCloak(alive && Net.Allied(health.team, 0) && CloakField.Covers(health));
        if (cloaked && cloakMat != null) // slow shimmer
            cloakMat.SetColor("_Color", CloakTint * (1.1f + 0.35f * Mathf.Sin(Time.time * 3f)));
    }

    // ---- stun ----
    void Stun(bool on)
    {
        if (arcs == null)
        {
            if (!on) return;
            var mat = Resources.Load<Material>("HarvestFX");
            arcs = new LineRenderer[Arcs];
            for (int i = 0; i < Arcs; i++)
            {
                var lr = new GameObject("StunArc").AddComponent<LineRenderer>();
                lr.transform.SetParent(transform, false);
                lr.gameObject.layer = gameObject.layer;
                lr.sharedMaterial = mat;
                lr.positionCount = ArcPoints;
                lr.useWorldSpace = true;
                lr.widthMultiplier = 0.18f;
                lr.startColor = lr.endColor = Electric;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                arcs[i] = lr;
            }
            zap = ProductionFX.MakeLight(transform, gameObject.layer);
            zap.color = Electric;
            zap.range = 5f;
            zap.transform.localPosition = Vector3.up;
        }
        foreach (var a in arcs) a.enabled = on;
        zap.enabled = on;
        if (!on || Time.time < nextArc) return;

        nextArc = Time.time + 0.06f; // new random arcs ~16 times a second
        Vector3 c = transform.position;
        foreach (var lr in arcs)
        {
            Vector3 from = c + Random.insideUnitSphere * radius + Vector3.up * Random.Range(0.1f, height);
            Vector3 to = c + Random.insideUnitSphere * radius + Vector3.up * Random.Range(0.1f, height);
            for (int p = 0; p < ArcPoints; p++)
            {
                float k = p / (ArcPoints - 1f);
                lr.SetPosition(p, Vector3.Lerp(from, to, k) + (p > 0 && p < ArcPoints - 1 ? Random.insideUnitSphere * 0.25f : Vector3.zero));
            }
            lr.enabled = Random.value > 0.15f;
        }
        zap.intensity = Random.Range(1f, 4f);
        if (Random.value < 0.3f) Juice.Burst(c + Vector3.up * Random.Range(0.2f, height), Electric, 3, 2f, 0.12f, 0.25f);
    }

    // ---- cloak ----
    void SetCloak(bool on)
    {
        if (on == cloaked || rends == null) return;
        cloaked = on;
        if (on)
        {
            if (cloakMat == null)
            {
                cloakMat = new Material(Shader.Find("GD5/Hologram")) { name = "Cloaked" };
                cloakMat.SetFloat("_CutY", 1e5f);
            }
            normal = new Material[rends.Length][];
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] == null) continue;
                normal[i] = rends[i].sharedMaterials;
                rends[i].sharedMaterials = System.Array.ConvertAll(normal[i], _ => cloakMat);
            }
            Juice.Burst(transform.position + Vector3.up * height * 0.5f, CloakTint, 12, 2f, 0.15f, 0.4f);
        }
        else
        {
            for (int i = 0; i < rends.Length; i++)
                if (rends[i] != null && normal[i] != null) rends[i].sharedMaterials = normal[i];
            normal = null;
        }
    }
}
