using System;
using UnityEngine;

// A visible shot. Flies to its target (homing while the target lives) or along an arc (artillery, aimed at
// where the target stood), then calls onHit with the landing point, where the damage is dealt.
public class Projectile : MonoBehaviour
{
    Health target;
    Vector3 start, end;
    float speed, arc, travelled, length;
    Action<Vector3> onHit;

    public static void Fire(GameObject prefab, Vector3 from, Health target, float speed, float arc, Action<Vector3> onHit)
    {
        Vector3 to = target.transform.position + Vector3.up;
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
    }

    void Update()
    {
        Vector3 before = transform.position;
        if (arc > 0f)
        {
            // Lobbed: fixed landing spot, height peaks halfway.
            travelled += speed * Time.deltaTime;
            float k = Mathf.Clamp01(travelled / Mathf.Max(length, 0.01f));
            transform.position = Vector3.Lerp(start, end, k) + Vector3.up * (arc * 4f * k * (1f - k));
            if (k >= 1f) { Land(end); return; }
        }
        else
        {
            if (target != null && target.isActiveAndEnabled) end = target.transform.position + Vector3.up; // home in
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
