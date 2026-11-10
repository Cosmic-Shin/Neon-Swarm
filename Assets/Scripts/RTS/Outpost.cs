using UnityEngine;

// Glue between the pack's OutpostCapture and our combat.
// Owned: it can be shot (at 0 HP it drops back to neutral and kicks the garrison out) and it shoots
// enemies in range, harder and further for every unit stationed inside. Neutral: not a target.
[RequireComponent(typeof(OutpostCapture), typeof(Health))]
public class Outpost : MonoBehaviour
{
    [SerializeField] Material laserMaterial;
    [SerializeField] float range = 20f;      // empty outpost; garrisoned units' range x garrisonRangeBonus if longer
    [SerializeField] float baseDamage = 6f;  // empty outpost; each garrisoned unit adds its own damage
    [SerializeField] float cooldown = 1f;

    /// <summary>Multiplayer (host): told about every shot so the other PCs can draw the laser.</summary>
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
    }

    void OwnerChanged(int team)
    {
        if (team == 0) Alerts.ShowAt("Outpost captured", Alerts.Good, transform.position);
        else if (health.team == 0) Alerts.ShowAt("Outpost lost", Alerts.Bad, transform.position);
        health.team = team;
        health.Revive();
        health.enabled = team >= 0; // neutral ruins aren't a target
        laser.material.color = Net.ColorOf(team) * 3f;
    }

    public void ShowShot(Health target)
    {
        laser.SetPosition(0, transform.position + Vector3.up * 6f);
        laser.SetPosition(1, Flyer.Center(target) + Vector3.up * 1f);
        laser.enabled = true;
        laserOff = Time.time + 0.1f;
    }

    // Shot down: empty it and let OutpostCapture decay it to neutral (it does once hold time and progress hit 0).
    void Fall()
    {
        capture.EjectAll();
        capture.holdTimeLeft = 0f;
        capture.progress = 0f;
    }

    void Update()
    {
        if (laser.enabled && Time.time >= laserOff) laser.enabled = false;
        if (capture.remote || !capture.IsOwned || Time.time < nextShot) return; // remote: the host shoots

        float damage = baseDamage, reach = range;
        foreach (var g in capture.Garrison)
        {
            if (g == null || !g.TryGetComponent(out SelectableUnit u)) continue;
            damage += Stats.Value(Stat.Damage, u.Data.damage, u);   // research + veterancy carry inside
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
