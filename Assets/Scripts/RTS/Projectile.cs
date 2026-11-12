using System;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    Health target;
    Vector3 start, end;
    float speed, arc, travelled, length;
    Action<Vector3> onHit;

    public static void Fire(GameObject prefab, Vector3 from, Health target, float speed, float arc, Action<Vector3> onHit)
    {
        Vector3 to = Flyer.Center(target) + Vector3.up;
        var go = FxPool.Spawn(prefab, from, Quaternion.LookRotation(to - from));
        if (!go.TryGetComponent(out Projectile p)) p = go.AddComponent<Projectile>();
        p.target = target;
        p.start = from;
        p.end = to;
        p.speed = speed;
        p.arc = arc;
        p.travelled = 0f;
        p.length = Vector3.Distance(from, to);
        p.onHit = onHit;
        if (!go.TryGetComponent(out TrailRenderer trail)) trail = AddTrail(go, arc);
        trail.Clear();
    }

    static TrailRenderer AddTrail(GameObject go, float arc)
    {
        Color c = Color.white;
        float size = 0.3f;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            var m = r.sharedMaterial;
            if (m != null && m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent > 0.01f)
            {
                c = m.GetColor("_EmissionColor");
                c /= Mathf.Max(1f, c.maxColorComponent);
            }
            size = Mathf.Max(size, Mathf.Min(r.bounds.extents.x, r.bounds.extents.y));
        }
        var t = go.AddComponent<TrailRenderer>();
        t.sharedMaterial = Juice.Additive;
        t.time = arc > 0f ? 0.35f : 0.15f;
        t.minVertexDistance = 0.3f;
        t.widthMultiplier = Mathf.Clamp(size * 1.2f, 0.12f, 0.6f);
        t.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        t.startColor = new Color(c.r, c.g, c.b, 0.9f);
        t.endColor = new Color(c.r, c.g, c.b, 0f);
        t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return t;
    }

    void Update()
    {
        Vector3 before = transform.position;
        if (arc > 0f)
        {
            travelled += speed * Time.deltaTime;
            float k = Mathf.Clamp01(travelled / Mathf.Max(length, 0.01f));
            transform.position = Vector3.Lerp(start, end, k) + Vector3.up * (arc * 4f * k * (1f - k));
            if (k >= 1f) { Land(end); return; }
        }
        else
        {
            if (target != null && target.isActiveAndEnabled) end = Flyer.Center(target) + Vector3.up;
            transform.position = Vector3.MoveTowards(transform.position, end, speed * Time.deltaTime);
            if ((transform.position - end).sqrMagnitude < 0.04f) { Land(end); return; }
        }
        Vector3 v = transform.position - before;
        if (v.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(v);
    }

    void Land(Vector3 at)
    {
        var hit = onHit;
        onHit = null;
        target = null;
        FxPool.Despawn(gameObject);
        hit?.Invoke(at);
    }
}
