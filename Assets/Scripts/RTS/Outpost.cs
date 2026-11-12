using UnityEngine;

[RequireComponent(typeof(OutpostCapture), typeof(Health))]
public class Outpost : MonoBehaviour
{
    [SerializeField] Material laserMaterial;
    [SerializeField] float range = 20f;
    [SerializeField] float baseDamage = 6f;
    [SerializeField] float cooldown = 1f;

    public static System.Action<Outpost, Health> ShotHook;

    OutpostCapture capture;
    Health health;
    LineRenderer laser;
    float nextShot, laserOff;

    void Awake()
    {
        capture = GetComponent<OutpostCapture>();
        health = GetComponent<Health>();
        health.onDeath = Fall;
        capture.OnOwnerChanged += OwnerChanged;

        laser = new GameObject("Laser").AddComponent<LineRenderer>();
        laser.transform.SetParent(transform, false);
        laser.material = laserMaterial;
        laser.widthMultiplier = 0.25f;
        laser.positionCount = 2;
        laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        laser.enabled = false;

        OwnerChanged(capture.ownerTeam);
        started = true;
    }

    bool started;

    void CaptureFX(int team)
    {
        Color c = team >= 0 ? Net.ColorOf(team) : new Color(0.6f, 0.6f, 0.7f);
        Vector3 p = transform.position;
        Juice.Ring(p + Vector3.up * 0.15f, c, 1f, 16f, 0.7f, 0.4f);
        Juice.Ring(p + Vector3.up * 0.15f, c, 0.5f, 10f, 0.5f, 0.25f);
        Juice.Burst(p + Vector3.up * 2f, c, 30, 8f, 0.2f, 0.9f, 1.5f);
        var beam = new GameObject("CaptureBeam").AddComponent<LineRenderer>();
        beam.sharedMaterial = laser.material;
        beam.positionCount = 2;
        beam.SetPosition(0, p);
        beam.SetPosition(1, p + Vector3.up * 45f);
        beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        beam.startColor = beam.endColor = c * 2f;
        Juice.Play(beam.gameObject, 1.2f, k => beam.widthMultiplier = Mathf.Lerp(2.5f, 0f, k));
    }

    void OwnerChanged(int team)
    {
        if (team == 0) { Alerts.ShowAt("Outpost captured", Alerts.Good, transform.position); Sfx.Play(Snd.OutpostWon, 0.8f); }
        else if (health.team == 0) { Alerts.ShowAt("Outpost lost", Alerts.Bad, transform.position); Sfx.Play(Snd.OutpostLost, 0.8f); }
        if (started && health.team != team && Juice.Seen(transform.position)) CaptureFX(team);
        health.team = team;
        health.Revive();
        health.enabled = team >= 0;
        laser.material.color = Net.ColorOf(team) * 3f;
    }

    public void ShowShot(Health target)
    {
        laser.SetPosition(0, transform.position + Vector3.up * 6f);
        laser.SetPosition(1, Flyer.Center(target) + Vector3.up * 1f);
        laser.enabled = true;
        laserOff = Time.time + 0.1f;
    }

    void Fall()
    {
        capture.EjectAll();
        capture.holdTimeLeft = 0f;
        capture.progress = 0f;
    }

    void Update()
    {
        if (laser.enabled && Time.time >= laserOff) laser.enabled = false;
        if (capture.remote || !capture.IsOwned || Time.time < nextShot) return;

        float damage = baseDamage, reach = range;
        foreach (var g in capture.Garrison)
        {
            if (g == null || !g.TryGetComponent(out SelectableUnit u)) continue;
            damage += Stats.Value(Stat.Damage, u.Data.damage, u);
            reach = Mathf.Max(reach, Stats.Value(Stat.Range, u.Data.range, u) * capture.garrisonRangeBonus);
        }

        Health target = null;
        float best = reach * reach;
        foreach (var h in Health.All)
        {
            if (Net.Allied(h.team, capture.ownerTeam) || h.CloakedFrom(transform.position)) continue;
            float sqr = (h.transform.position - transform.position).sqrMagnitude;
            if (sqr < best) { best = sqr; target = h; }
        }
        if (target == null) return;

        nextShot = Time.time + cooldown;
        ShowShot(target);
        ShotHook?.Invoke(this, target);
        target.TakeDamage(damage, health);
    }
}
