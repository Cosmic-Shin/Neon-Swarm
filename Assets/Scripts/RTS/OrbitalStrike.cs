using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrbitalStrike : MonoBehaviour
{
    const float WarningTime = 5f, BurnTime = 9f, Tick = 0.5f, Height = 160f, TotalDamage = 2100f;

    public static readonly Color Hot = new(1f, 0.38f, 0.12f);
    static readonly Color Gold = new(1f, 0.8f, 0.35f);

    Health source;
    int team;
    bool authority;
    Vector3 target;
    GameObject shockPulse, bolt;
    readonly List<Health> hits = new();
    LineRenderer core, glow, spot;
    GameObject beamPrefab, impactPrefab, beamMesh, impactMesh;
    Vector3 beamScale, impactScale;
    float beamOffset;
    Light beamLight;

    public static void Begin(SwarmSilo lance, Vector3 from, Vector3 at, bool authority)
    {
        var s = new GameObject("OrbitalStrike").AddComponent<OrbitalStrike>();
        s.source = lance.GetComponent<Health>();
        s.team = lance.Team;
        s.authority = authority;
        s.target = at;
        s.shockPulse = lance.shockPulse;
        s.bolt = lance.bolt;
        s.beamPrefab = lance.beamPrefab;
        s.impactPrefab = lance.impactPrefab;
        s.StartCoroutine(s.Run(from));
    }

    static LineRenderer Beam(string name, Color c, float width)
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

    static void Drop(LineRenderer lr)
    {
        if (lr == null) return;
        Destroy(lr.sharedMaterial);
        Destroy(lr.gameObject);
    }

    IEnumerator Run(Vector3 from)
    {
        var uplink = Beam("LanceUplink", Gold, 1.2f);
        uplink.endColor = new Color(Gold.r, Gold.g, Gold.b, 0f);
        uplink.SetPosition(0, from);
        uplink.SetPosition(1, from + Vector3.up * Height);
        if (Juice.Seen(from))
        {
            Juice.Flash(from + Vector3.up * 2f, Gold, 10f, 40f, 0.6f);
            Juice.Ring(new Vector3(from.x, 0.3f, from.z), Hot, 3f, 26f, 0.8f, 0.6f);
            Juice.Shake(from, 0.5f);
        }
        Sfx.CueAt("forceField_003", from, 1f, 0.55f, 0.1f);
        Sfx.CueAt("laserLarge_004", from, 1f, 0.6f, 0.1f);
        if (Net.Allied(team, 0)) Sfx.Say(Line.Launch, 1f);

        bool enemyShot = !Net.Allied(team, 0);
        bool danger = InDanger();
        Alerts.ShowAt("Warning: orbital strike targeting", Alerts.Bad, target);
        if (enemyShot) Sfx.Say(Line.Incoming, 1f);
        Sfx.Duck(0.5f, WarningTime + 2f);

        Vector3 mark = target + Vector3.up * 0.3f;
        var outer = SwarmSilo.MakeRing("LanceWarning", SwarmSilo.StrikeRadius, Hot, 0.8f);
        var inner = SwarmSilo.MakeRing("LanceWarningEmp", SwarmSilo.EmpRadius, Gold, 0.35f);
        var closing = SwarmSilo.MakeRing("LanceClosing", SwarmSilo.StrikeRadius, Color.white, 0.14f);
        var sight = Beam("LanceSight", new Color(1f, 0.45f, 0.2f, 0.4f), 0.15f);
        sight.endColor = new Color(1f, 0.45f, 0.2f, 0f);
        sight.SetPosition(0, mark);
        sight.SetPosition(1, mark + Vector3.up * Height);
        outer.transform.position = inner.transform.position = closing.transform.position = mark;
        var count = SwarmStrike.MakeLabel("LanceCountdown", 46);
        count.rectTransform.anchorMin = count.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        count.rectTransform.anchoredPosition = new Vector2(0f, -150f);

        int shown = -1;
        bool coverSaid = false;
        for (float left = WarningTime; left > 0f; left -= Time.deltaTime)
        {
            float elapsed = WarningTime - left;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (10f + elapsed * 3f));
            outer.widthMultiplier = 0.22f + 0.3f * pulse;
            inner.transform.Rotate(0f, 0f, (40f + elapsed * 60f) * Time.deltaTime, Space.Self);
            float beat = left - Mathf.Floor(left);
            closing.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.7f, beat);
            closing.startColor = closing.endColor = new Color(1f, 1f, 1f, (1f - beat) * 0.8f);
            sight.widthMultiplier = 0.1f + elapsed / WarningTime * 0.6f * (0.6f + 0.4f * pulse);
            float fade = Mathf.Clamp01(1f - elapsed / 1.2f);
            uplink.widthMultiplier = 1.2f * fade;
            int sec = Mathf.CeilToInt(left);
            if (sec != shown)
            {
                shown = sec;
                count.text = (danger ? "TAKE COVER  " : "ORBITAL STRIKE  ") + sec;
                Sfx.Cue("error_004", 0.5f, 0.85f + (5 - sec) * 0.09f);
                if (danger) Juice.EdgeFlash(Hot, 0.28f + (5 - sec) * 0.06f);
            }
            count.transform.localScale = Vector3.one * (1f + 0.22f * beat * beat);
            count.color = Color.Lerp(Color.white, Hot, elapsed / WarningTime);
            if (!coverSaid && left <= 2.2f)
            {
                coverSaid = true;
                if (InDanger()) Sfx.Say(Line.TakeCover, 1f);
            }
            yield return null;
        }
        foreach (var lr in new[] { outer, inner, closing }) SwarmSilo.Remove(lr);
        Drop(sight);
        Drop(uplink);
        Destroy(count.transform.parent.gameObject);

        Impact();
        yield return Burn();
        Drop(core);
        Drop(glow);
        SwarmSilo.Remove(spot);
        if (beamLight != null) Destroy(beamLight.gameObject);
        if (beamMesh != null) Destroy(beamMesh);
        if (impactMesh != null) Destroy(impactMesh);
        Destroy(gameObject);
    }

    static Bounds Bounds(GameObject go)
    {
        var b = new Bounds(go.transform.position, Vector3.zero);
        bool any = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
        return b;
    }

    bool InDanger()
    {
        foreach (var h in Health.All)
            if (h.team == 0 && !h.IsDead && !Net.Allied(team, 0) && SwarmStrike.InBlast(h, target, team, SwarmSilo.StrikeRadius)) return true;
        return false;
    }

    void Collect(Vector3 at, float radius)
    {
        hits.Clear();
        foreach (var h in Health.All) if (SwarmStrike.InBlast(h, at, team, radius)) hits.Add(h);
    }

    void Impact()
    {
        Vector3 ground = target + Vector3.up * 0.2f;
        float r = SwarmSilo.StrikeRadius;
        core = Beam("LanceCore", Color.white, 3.5f);
        glow = Beam("LanceGlow", new Color(Hot.r, Hot.g, Hot.b, 0.55f), 11f);
        spot = SwarmSilo.MakeRing("LanceSpot", 3f, Hot, 1.4f);
        beamLight = new GameObject("LanceLight").AddComponent<Light>();
        beamLight.type = LightType.Point;
        beamLight.color = Hot;
        beamLight.range = 60f;
        beamLight.shadows = LightShadows.None;
        if (beamPrefab != null)
        {
            beamMesh = Instantiate(beamPrefab, target, Quaternion.identity);
            var b = Bounds(beamMesh);
            float h = Mathf.Max(0.01f, b.size.y), w = Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
            beamMesh.transform.localScale = Vector3.Scale(beamMesh.transform.localScale, new Vector3(5f / w, Height / h, 5f / w));
            beamScale = beamMesh.transform.localScale;
            beamOffset = beamMesh.transform.position.y - Bounds(beamMesh).min.y;
        }
        if (impactPrefab != null)
        {
            impactMesh = Instantiate(impactPrefab, target + Vector3.up * 0.1f, Quaternion.identity);
            var b = Bounds(impactMesh);
            impactMesh.transform.localScale *= 9f / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
            impactScale = impactMesh.transform.localScale;
        }
        Aim(target, 1f);

        if (shockPulse != null) Fx.Spawn(shockPulse, ground, Quaternion.identity, new Vector3(SwarmSilo.EmpRadius, 0.6f, SwarmSilo.EmpRadius));
        Juice.Flash(target + Vector3.up * 8f, Color.white, 1.6f, 45f, 0.2f);
        Juice.Flash(target + Vector3.up * 7f, Hot, 2.8f, 50f, 0.9f);
        Juice.Ring(ground, Hot, 2f, r * 1.7f, 0.8f, 1.1f);
        Juice.Ring(ground, Gold, 1f, SwarmSilo.EmpRadius, 0.4f, 0.6f);
        Juice.Burst(target + Vector3.up, Juice.Fire, 160, 34f, 0.3f, 1.3f, 0.8f, true);
        Juice.Burst(target + Vector3.up, Gold, 90, 26f, 0.22f, 1f, 0.5f);
        Juice.Shake(target, 1.6f);
        Juice.Scorch(target, r * 0.8f, 45f);
        for (int i = 0; i < 36; i++)
        {
            float a = i * Mathf.PI * 2f / 36f;
            Vector3 dir = new(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Particles.Emit("Dust", ground + dir * 3f, dir * Random.Range(14f, 26f) + Vector3.up, Random.Range(6f, 10f), Random.Range(1.2f, 2.2f), new Color(0.6f, 0.45f, 0.35f, 0.5f));
        }
        Sfx.CueAt("lowFrequency_explosion_001", target, 1f, 0.7f, 0.3f);
        Sfx.CueAt("explosionCrunch_001", target, 1f, 0.7f, 0.2f);
        Sfx.CueAt("laserLarge_000", target, 1f, 0.45f, 0.2f);
        Sfx.Duck(0.3f, 2.5f);
        if (Seen()) Juice.EdgeFlash(Hot, 0.3f);

        Collect(target, SwarmSilo.EmpRadius);
        foreach (var h in hits)
        {
            Arc(target + Vector3.up * 2f, Flyer.Center(h) + Vector3.up, 1f);
            if (authority) SwarmStrike.Stun(h);
        }
    }

    void Aim(Vector3 at, float k)
    {
        Vector3 sky = at + Vector3.up * Height;
        core.SetPosition(0, sky);
        core.SetPosition(1, at);
        glow.SetPosition(0, sky);
        glow.SetPosition(1, at);
        float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 47f);
        core.widthMultiplier = 2f * k * flicker;
        glow.widthMultiplier = 6.5f * k * (0.9f + 0.1f * Mathf.Sin(Time.time * 13f));
        var c = glow.startColor;
        glow.startColor = glow.endColor = new Color(c.r, c.g, c.b, 0.55f * k);
        beamLight.transform.position = at + Vector3.up * 4f;
        if (beamMesh != null) { beamMesh.transform.position = at + Vector3.up * beamOffset; beamMesh.transform.localScale = new Vector3(beamScale.x * k, beamScale.y, beamScale.z * k); }
        if (impactMesh != null) { impactMesh.transform.position = at + Vector3.up * 0.1f; impactMesh.transform.localScale = impactScale * Mathf.Max(0.05f, k) * (0.9f + 0.15f * Mathf.Sin(Time.time * 17f)); impactMesh.transform.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World); }
        if (spot != null) { spot.transform.position = at + Vector3.up * 0.3f; spot.transform.localScale = Vector3.one * (0.8f + 0.25f * Mathf.Sin(Time.time * 23f)) * Mathf.Max(0.2f, k); spot.startColor = spot.endColor = new Color(1f, 0.6f, 0.25f, 0.9f * k); }
        beamLight.intensity = 6f * k * GameSettings.FlashScale;
    }

    IEnumerator Burn()
    {
        float r = SwarmSilo.StrikeRadius;
        float nextTick = 0f, nextFx = 0f, nextScorch = 0f;
        Vector3 last = target;
        for (float t = 0f; t < BurnTime; t += Time.deltaTime)
        {
            float k = t / BurnTime;
            float swing = Mathf.Sin(k * Mathf.PI) * r * 0.75f;
            float a = t * 2.1f;
            Vector3 at = target + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * swing;
            float fade = Mathf.Clamp01((BurnTime - t) / 0.8f) * Mathf.Clamp01(t / 0.15f + 0.4f);
            Aim(at, fade);
            bool seen = Juice.Seen(at);
            if (t >= nextFx && seen)
            {
                nextFx = t + 0.05f;
                Juice.Burst(at + Vector3.up * 0.5f, Hot, 6, 12f, 0.14f, 0.4f, 0.6f);
                Particles.Emit("Smoke", at + Random.insideUnitSphere * 2f, Vector3.up * Random.Range(6f, 12f), Random.Range(4f, 7f), Random.Range(1.6f, 2.8f), new Color(0.3f, 0.24f, 0.22f, 0.5f));
                Juice.Shake(at, 0.12f);
            }
            if (t >= nextScorch && (at - last).sqrMagnitude > 9f)
            {
                nextScorch = t + 0.12f;
                Juice.Scorch(at, Random.Range(4f, 6f), 35f);
                last = at;
            }
            if (t >= nextTick)
            {
                nextTick += Tick;
                Collect(target, r);
                foreach (var h in hits)
                {
                    if (authority) h.TakeDamage(TotalDamage * Tick / BurnTime, source);
                    if (seen && Random.value < 0.35f) Arc(at + Vector3.up * 1.5f, Flyer.Center(h) + Vector3.up, 0.6f);
                }
                Sfx.CueAt("laserSmall_002", at, 0.5f, Random.Range(0.5f, 0.7f), 0.1f);
            }
            yield return null;
        }
    }

    bool Seen()
    {
        var cam = Camera.main;
        if (cam == null || !Juice.Seen(target)) return false;
        Vector3 v = cam.WorldToViewportPoint(target);
        return v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
    }

    void Arc(Vector3 from, Vector3 to, float width)
    {
        if (bolt == null) return;
        Vector3 d = to - from;
        if (d.sqrMagnitude < 0.25f) return;
        Fx.Spawn(bolt, from, Quaternion.LookRotation(d), new Vector3(width, width, d.magnitude));
    }
}
