using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwarmStrike : MonoBehaviour
{
    const float RiseTime = 1.5f, WarningTime = 5f, DropTime = 0.6f, SwarmTime = 12f, Tick = 0.5f, StunTime = 4f, Height = 130f, TotalDamage = 2100f;
    const int DroneCount = 40;

    class Drone { public Transform t; public Vector3 vel, aim, scale; public float nextAim, phase; }

    Health source;
    int team;
    bool authority;
    Vector3 target;
    GameObject podPrefab, shockBig, shockPulse, shockRing, bolt, locustPrefab;
    readonly List<Drone> drones = new();
    readonly List<Health> hits = new();
    readonly List<Health> stunned = new();

    public static void Begin(SwarmSilo silo, Vector3 from, Vector3 at, bool authority)
    {
        var s = new GameObject("SwarmStrike").AddComponent<SwarmStrike>();
        s.source = silo.GetComponent<Health>();
        s.team = silo.Team;
        s.authority = authority;
        s.target = at;
        s.podPrefab = silo.podPrefab;
        s.shockBig = silo.shockBig;
        s.shockPulse = silo.shockPulse;
        s.shockRing = silo.shockRing;
        s.bolt = silo.bolt;
        s.locustPrefab = silo.locustPrefab;
#if DEVELOPMENT_BUILD
        Debug.Log($"[GD5] silo launch team {s.team} target {at} authority {authority}");
#endif
        s.StartCoroutine(s.Run(from));
    }

    public static bool InBlast(Health h, Vector3 centre, int team, float radius)
    {
        if (h == null || h.IsDead || !h.enabled || Net.Allied(h.team, team)) return false;
        Vector3 p = h.transform.position;
        p.y = centre.y;
        if ((p - centre).sqrMagnitude > (radius + 40f) * (radius + 40f)) return false;
        if (h.TryGetComponent(out Structure _) && h.TryGetComponent(out Collider col)) p = col.ClosestPoint(centre);
        p.y = centre.y;
        return (p - centre).sqrMagnitude <= radius * radius;
    }

    public static void Count(Vector3 centre, int team, out int buildings, out int units)
    {
        buildings = units = 0;
        foreach (var h in Health.All)
        {
            if (h.team == Creep.Team || !Juice.Seen(h.transform.position) || !InBlast(h, centre, team, SwarmSilo.StrikeRadius)) continue;
            if (h.TryGetComponent(out Structure _)) buildings++;
            else if (h.TryGetComponent(out SelectableUnit _)) units++;
        }
    }

    public static UnityEngine.UI.Text MakeLabel(string name, int size)
    {
        var root = new GameObject(name, typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 700;
        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        var text = new GameObject("Text", typeof(RectTransform), typeof(UnityEngine.UI.Text), typeof(UnityEngine.UI.Outline)).GetComponent<UnityEngine.UI.Text>();
        text.transform.SetParent(root.transform, false);
        text.rectTransform.sizeDelta = new Vector2(1000f, 100f);
        text.font = HoloSkin.BoldFont;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        text.color = Color.white;
        var outline = text.GetComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0.25f, 0f, 0.12f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    static LineRenderer MakeLine(string name, Color c, float width)
    {
        var lr = new GameObject(name).AddComponent<LineRenderer>();
        lr.sharedMaterial = new Material(Juice.AdditiveLine) { renderQueue = 3300 };
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.widthMultiplier = width;
        lr.startColor = lr.endColor = c;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return lr;
    }

    IEnumerator Run(Vector3 from)
    {
        var pod = Instantiate(podPrefab, from, Quaternion.LookRotation(Vector3.up));
        pod.transform.localScale = Vector3.one * 4f;
        AddTrail(pod);
        float nextPuff = 0f;
        for (float t = 0f; t < RiseTime; t += Time.deltaTime)
        {
            float k = t / RiseTime;
            pod.transform.position = from + Vector3.up * Height * k * k;
            if (t >= nextPuff && Juice.Seen(from))
            {
                nextPuff = t + 0.03f;
                Vector3 tail = pod.transform.position - Vector3.up * 3f;
                Particles.Emit("Smoke", tail + Random.insideUnitSphere, Random.insideUnitSphere * 1.5f, Random.Range(3f, 5.5f), Random.Range(1f, 1.8f), new Color(0.5f, 0.45f, 0.52f, 0.45f));
                Juice.Burst(tail, Juice.Fire, 3, 7f, 0.16f, 0.3f, -0.4f);
            }
            yield return null;
        }
        pod.SetActive(false);

        bool enemyShot = !Net.Allied(team, 0);
        bool danger = InDanger();
        Alerts.ShowAt("Warning: swarm launch detected", Alerts.Bad, target);
        if (enemyShot) Sfx.Say(Line.Incoming, 1f);
        Sfx.Duck(0.5f, WarningTime + 2f);

        Vector3 mark = target + Vector3.up * 0.3f;
        var outer = SwarmSilo.MakeRing("StrikeWarning", SwarmSilo.StrikeRadius, new Color(1f, 0.12f, 0.3f), 0.8f);
        var inner = SwarmSilo.MakeRing("StrikeWarningEmp", SwarmSilo.EmpRadius, SwarmSilo.Pink, 0.35f);
        var closing = SwarmSilo.MakeRing("StrikeClosing", SwarmSilo.StrikeRadius, Color.white, 0.14f);
        var sweep = MakeLine("StrikeSweep", new Color(1f, 0.2f, 0.45f, 0.45f), 0.16f);
        sweep.endColor = new Color(1f, 0.2f, 0.45f, 0.08f);
        var sky = MakeLine("StrikeBeam", new Color(1f, 0.1f, 0.4f, 0.32f), 0.2f);
        sky.endColor = new Color(1f, 0.1f, 0.4f, 0f);
        sky.SetPosition(0, mark);
        sky.SetPosition(1, mark + Vector3.up * Height);
        outer.transform.position = inner.transform.position = closing.transform.position = mark;
        var count = MakeLabel("StrikeCountdown", 46);
        count.rectTransform.anchorMin = count.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        count.rectTransform.anchoredPosition = new Vector2(0f, -150f);

        int shown = -1;
        bool dropping = false, coverSaid = false;
        for (float left = WarningTime; left > 0f; left -= Time.deltaTime)
        {
            float elapsed = WarningTime - left;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (10f + elapsed * 3f));
            outer.widthMultiplier = 0.22f + 0.3f * pulse;
            outer.startColor = outer.endColor = Color.Lerp(SwarmSilo.Pink, new Color(1f, 0.08f, 0.1f), pulse);
            inner.transform.Rotate(0f, 0f, (60f + elapsed * 50f) * Time.deltaTime, Space.Self);
            float beat = left - Mathf.Floor(left);
            closing.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.7f, beat);
            closing.startColor = closing.endColor = new Color(1f, 1f, 1f, (1f - beat) * 0.8f);
            float a = elapsed * (2.5f + elapsed * 0.9f);
            sweep.SetPosition(0, mark);
            sweep.SetPosition(1, mark + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * SwarmSilo.StrikeRadius);
            sky.widthMultiplier = 0.12f + (elapsed / WarningTime) * 0.5f * (0.6f + 0.4f * pulse);

            int sec = Mathf.CeilToInt(left);
            if (sec != shown)
            {
                shown = sec;
                count.text = (danger ? "TAKE COVER  " : "SWARM IMPACT  ") + sec;
                Sfx.Cue("error_004", 0.5f, 0.85f + (5 - sec) * 0.09f);
                if (danger) Juice.EdgeFlash(SwarmSilo.Pink, 0.28f + (5 - sec) * 0.06f);
            }
            count.transform.localScale = Vector3.one * (1f + 0.22f * beat * beat);
            count.color = Color.Lerp(Color.white, new Color(1f, 0.2f, 0.4f), elapsed / WarningTime);
            if (!coverSaid && left <= 2.2f)
            {
                coverSaid = true;
                if (InDanger()) Sfx.Say(Line.TakeCover, 1f);
            }
            if (left <= DropTime)
            {
                if (!dropping)
                {
                    dropping = true;
                    pod.SetActive(true);
                    pod.transform.rotation = Quaternion.LookRotation(Vector3.down);
                    if (pod.GetComponentInChildren<TrailRenderer>() is TrailRenderer trail) trail.Clear();
                    Sfx.Cue("thrusterFire_003", 0.85f, 1.7f);
                }
                float k = 1f - left / DropTime;
                pod.transform.position = target + Vector3.up * Height * (1f - k * k);
            }
            yield return null;
        }
        Destroy(pod);
        foreach (var lr in new[] { outer, inner, closing, sweep, sky }) SwarmSilo.Remove(lr);
        Destroy(count.transform.parent.gameObject);

        Impact();
        yield return Swarm();
        foreach (var d in drones) if (d.t != null) Destroy(d.t.gameObject);
        Destroy(gameObject);
    }

    bool InDanger()
    {
        foreach (var h in Health.All)
            if (h.team == 0 && !h.IsDead && !Net.Allied(team, 0) && InBlast(h, target, team, SwarmSilo.StrikeRadius)) return true;
        return false;
    }

    static void AddTrail(GameObject pod)
    {
        Transform tail = pod.transform;
        foreach (var t in pod.GetComponentsInChildren<Transform>()) if (t.name == "SPod_Exhaust") { tail = t; break; }
        var trail = tail.gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = Juice.AdditiveLine;
        trail.time = 0.55f;
        trail.widthMultiplier = 2.4f;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        trail.startColor = new Color(1f, 0.75f, 0.45f);
        trail.endColor = new Color(1f, 0.05f, 0.6f, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void Collect(float radius)
    {
        hits.Clear();
        foreach (var h in Health.All) if (InBlast(h, target, team, radius)) hits.Add(h);
    }

    void Impact()
    {
#if DEVELOPMENT_BUILD
        Debug.Log($"[GD5] silo impact team {team} at {target} authority {authority}");
#endif
        Vector3 ground = target + Vector3.up * 0.2f;
        float r = SwarmSilo.StrikeRadius;
        Fx.Spawn(shockPulse, ground, Quaternion.identity, new Vector3(SwarmSilo.EmpRadius, 0.6f, SwarmSilo.EmpRadius));
        Juice.Flash(target + Vector3.up * 8f, Color.white, 1.4f, 40f, 0.18f);
        Juice.Flash(target + Vector3.up * 7f, SwarmSilo.Pink, 2.8f, 46f, 0.9f);
        Juice.Ring(ground, SwarmSilo.Pink, 2f, r * 1.6f, 0.8f, 1.1f);
        Juice.Ring(ground, Color.white, 1f, SwarmSilo.EmpRadius, 0.4f, 0.6f);
        Juice.Ring(ground, Color.cyan, 1f, r * 1.2f, 1.1f, 0.3f);
        Juice.Burst(target + Vector3.up, Juice.Fire, 160, 34f, 0.3f, 1.3f, 0.8f, true);
        Juice.Burst(target + Vector3.up, SwarmSilo.Pink, 110, 26f, 0.22f, 1f, 0.5f);
        Juice.Shake(target, 1.5f);
        Juice.Scorch(target, r * 1.5f, 45f);
        for (int i = 0; i < 6; i++)
        {
            Vector2 c = Random.insideUnitCircle * r * 0.7f;
            Juice.Scorch(target + new Vector3(c.x, 0f, c.y), Random.Range(5f, 11f), 40f);
        }
        for (int i = 0; i < 36; i++)
        {
            float a = i * Mathf.PI * 2f / 36f;
            Vector3 dir = new(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Particles.Emit("Dust", ground + dir * 3f, dir * Random.Range(14f, 26f) + Vector3.up, Random.Range(6f, 10f), Random.Range(1.2f, 2.2f), new Color(0.55f, 0.42f, 0.55f, 0.5f));
        }
        for (int i = 0; i < 14; i++)
            Particles.Emit("Smoke", ground + Random.insideUnitSphere * 5f, Vector3.up * Random.Range(5f, 13f) + Random.insideUnitSphere * 2f, Random.Range(6f, 11f), Random.Range(2.5f, 4.5f), new Color(0.26f, 0.22f, 0.3f, 0.65f));

        Sfx.CueAt("lowFrequency_explosion_001", target, 1f, 0.72f, 0.3f);
        Sfx.CueAt("explosionCrunch_001", target, 1f, 0.7f, 0.2f);
        Sfx.CueAt("forceField_003", target, 0.9f, 0.6f, 0.1f);
        Sfx.Duck(0.3f, 2.5f);
        if (Seen()) Juice.EdgeFlash(SwarmSilo.Pink, 0.3f);
        StartCoroutine(HitStop());

        Collect(SwarmSilo.EmpRadius);
        stunned.AddRange(hits);
        foreach (var h in hits)
        {
            Arc(target + Vector3.up * 2f, Flyer.Center(h) + Vector3.up, 1f);
            if (authority) Stun(h);
        }
        StartCoroutine(SpawnDrones());
    }

    bool Seen()
    {
        var cam = Camera.main;
        if (cam == null || !Juice.Seen(target)) return false;
        Vector3 v = cam.WorldToViewportPoint(target);
        return v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
    }

    IEnumerator HitStop()
    {
        if (Net.InMatch || GameSettings.ReduceFlashing || !Seen() || !Mathf.Approximately(Time.timeScale, 1f)) yield break;
        Time.timeScale = 0.3f;
        yield return new WaitForSecondsRealtime(0.28f);
        if (Mathf.Approximately(Time.timeScale, 0.3f)) Time.timeScale = 1f;
    }

    void Arc(Vector3 from, Vector3 to, float width)
    {
        if (bolt == null) return;
        Vector3 d = to - from;
        if (d.sqrMagnitude < 0.25f) return;
        Fx.Spawn(bolt, from, Quaternion.LookRotation(d), new Vector3(width, width, d.magnitude));
    }

    public static void Stun(Health h)
    {
        if (h.TryGetComponent(out NetEntity n) && n.IsRemote) { n.RPC_Status(StunTime, 0f); return; }
        if (h.TryGetComponent(out SelectableUnit u)) u.Stun(StunTime);
        else if (h.TryGetComponent(out Structure s)) s.Stun(StunTime);
    }

    IEnumerator SpawnDrones()
    {
        var model = locustPrefab != null ? locustPrefab.transform.Find("Model") : null;
        if (model == null) yield break;
        var hold = new GameObject("Hold");
        hold.SetActive(false);
        hold.transform.SetParent(transform, false);
        var template = Instantiate(model.gameObject, hold.transform);
        foreach (var m in template.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(m);
        foreach (var r in template.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (template.TryGetComponent(out Animator a)) { a.cullingMode = AnimatorCullingMode.CullCompletely; a.fireEvents = false; }
        var trail = template.AddComponent<TrailRenderer>();
        trail.sharedMaterial = Juice.AdditiveLine;
        trail.time = 0.22f;
        trail.minVertexDistance = 0.4f;
        trail.widthMultiplier = 0.32f;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        trail.startColor = new Color(1f, 0.15f, 0.65f, 0.75f);
        trail.endColor = new Color(0.2f, 0.9f, 1f, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Vector3 scale = template.transform.localScale;

        for (int i = 0; i < DroneCount; i++)
        {
            var go = Instantiate(template, target + Vector3.up * 2f, Quaternion.identity, transform);
            Vector2 c = Random.insideUnitCircle.normalized;
            drones.Add(new Drone
            {
                t = go.transform, scale = scale, phase = Random.value * 10f,
                vel = new Vector3(c.x, Random.Range(0.7f, 1.5f), c.y) * Random.Range(16f, 30f), aim = RandomAim(),
                nextAim = Time.time + Random.Range(0.4f, 0.9f),
            });
            if (i % 5 == 4) yield return null;
        }
        Destroy(hold);
    }

    Vector3 RandomAim()
    {
        Vector2 c = Random.insideUnitCircle * SwarmSilo.StrikeRadius;
        return target + new Vector3(c.x, Random.Range(2.5f, 9f), c.y);
    }

    IEnumerator Swarm()
    {
        float nextTick = 0f, nextShot = 0f, nextArc = 0f, nextHum = 0f;
        for (float t = 0f; t < SwarmTime; t += Time.deltaTime)
        {
            if (t >= nextTick)
            {
                nextTick += Tick;
                Collect(SwarmSilo.StrikeRadius);
                foreach (var h in hits)
                {
                    if (authority) h.TakeDamage(TotalDamage * Tick / SwarmTime, source);
                    if (h != null && Juice.Seen(h.transform.position)) Juice.Burst(Flyer.Center(h) + Vector3.up, SwarmSilo.Pink, 6, 8f, 0.1f, 0.3f, 0.6f);
                }
            }
            hits.RemoveAll(h => h == null || h.IsDead);
            if (t >= nextShot && hits.Count > 0 && drones.Count > 0)
            {
                nextShot = t + 0.05f;
                var d = drones[Random.Range(0, drones.Count)];
                var h = hits[Random.Range(0, hits.Count)];
                Vector3 to = Flyer.Center(h) + Vector3.up * Random.Range(0.5f, 3f) + Random.insideUnitSphere * 1.5f;
                if (d.t != null && (d.t.position - to).sqrMagnitude < 26f * 26f)
                {
                    Arc(d.t.position, to, 0.28f);
                    if (Juice.Seen(to)) { Juice.Burst(to, Juice.Fire, 2, 5f, 0.09f, 0.2f, 0.4f); LightPool.Flash(to, SwarmSilo.Pink, 1.6f, 6f, 0.06f); }
                }
            }
            if (t >= nextHum && hits.Count > 0) { nextHum = t + 0.14f; Sfx.At(Snd.Laser, target, 0.3f); }
            stunned.RemoveAll(h => h == null || h.IsDead);
            if (t < StunTime && t >= nextArc && stunned.Count > 0)
            {
                nextArc = t + 0.11f;
                var h = stunned[Random.Range(0, stunned.Count)];
                Vector3 c = Flyer.Center(h) + Vector3.up * 2f;
                Arc(c + Random.onUnitSphere * 4f, c + Random.onUnitSphere * 4f, 0.5f);
                if (Juice.Seen(c)) Juice.Burst(c, Color.cyan, 3, 5f, 0.1f, 0.25f, 0.5f);
                Sfx.At(Snd.Zap, c, 0.16f);
            }
            float vol = Mathf.Clamp01(t / 0.6f) * Mathf.Clamp01((SwarmTime - t) / 1.2f);
            Sfx.SwarmLoop(vol * Loudness(), 2.2f + 0.25f * Mathf.Sin(t * 3f));
            MoveDrones(t > SwarmTime - 1f, 1f);
            yield return null;
        }
        Sfx.SwarmLoop(0f, 1f);
        for (float leave = 0f; leave < 1f; leave += Time.deltaTime)
        {
            MoveDrones(true, 1f - leave);
            yield return null;
        }
    }

    float Loudness()
    {
        var cam = Camera.main;
        if (cam == null || !Juice.Seen(target)) return 0.12f;
        Vector3 focus = cam.transform.position + cam.transform.forward * 30f;
        float d = Vector2.Distance(new Vector2(target.x, target.z), new Vector2(focus.x, focus.z));
        return Mathf.Clamp(1.15f - d / 110f, 0.12f, 1f);
    }

    void MoveDrones(bool leaving, float size)
    {
        float dt = Time.deltaTime;
        hits.RemoveAll(h => h == null || h.IsDead);
        foreach (var d in drones)
        {
            if (d.t == null) continue;
            if (leaving) d.aim = d.t.position + new Vector3(Mathf.Sin(d.phase) * 8f, 45f, Mathf.Cos(d.phase) * 8f);
            else if (Time.time >= d.nextAim || (d.aim - d.t.position).sqrMagnitude < 4f)
            {
                d.nextAim = Time.time + Random.Range(0.45f, 1.2f);
                d.aim = hits.Count > 0 && Random.value < 0.65f ? Flyer.Center(hits[Random.Range(0, hits.Count)]) + Vector3.up * Random.Range(1.5f, 6f) + Random.insideUnitSphere * 4f : RandomAim();
                ClampAim(d);
            }
            Vector3 wobble = new Vector3(Mathf.Sin(Time.time * 7f + d.phase), Mathf.Sin(Time.time * 5f + d.phase * 2f) * 0.5f, Mathf.Cos(Time.time * 6f + d.phase)) * 7f;
            d.vel = Vector3.MoveTowards(d.vel, (d.aim - d.t.position).normalized * (leaving ? 42f : 25f) + wobble, 85f * dt);
            d.t.position += d.vel * dt;
            if (d.t.position.y < 1f) { var p = d.t.position; p.y = 1f; d.t.position = p; d.vel.y = Mathf.Abs(d.vel.y); }
            if (d.vel.sqrMagnitude > 0.1f) d.t.rotation = Quaternion.LookRotation(d.vel) * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 4f + d.phase) * 35f);
            d.t.localScale = d.scale * size;
        }
    }

    void ClampAim(Drone d)
    {
        Vector3 off = d.aim - target;
        off.y = 0f;
        if (off.magnitude <= SwarmSilo.StrikeRadius) return;
        off = off.normalized * SwarmSilo.StrikeRadius;
        d.aim = new Vector3(target.x + off.x, d.aim.y, target.z + off.z);
    }

    void OnDestroy() => Sfx.SwarmLoop(0f, 1f);
}
