using System.Collections.Generic;
using UnityEngine;

// Defence tower: shoots the nearest enemies in range. Goes offline while the player is low on power.
// targets > 1 arcs to several enemies at once; splash > 0 also hurts everything near the target.
[RequireComponent(typeof(Health))]
public class Turret : MonoBehaviour
{
    [SerializeField] Material laserMaterial;
    [SerializeField] float range = 18f;
    [SerializeField] float damage = 12f;
    [SerializeField] float cooldown = 0.8f;
    [SerializeField] int targets = 1;
    [SerializeField] float splash = 0f;
    [SerializeField] float beamWidth = 0.2f;
    [SerializeField] float beamTime = 0.08f;
    [SerializeField] Color color = new(0.2f, 1f, 1f);
    [SerializeField] WeaponVisual visual = new();  // projectile / flash / impact; no projectile = beam
    [SerializeField] GameObject bolt;              // lightning (Fx, 1 m along +Z) stretched shooter -> target instead of a beam

    public float Range => Stats.Value(Stat.Range, range, this); // research / upgrades

    Health self;
    LineRenderer[] beams;
    float nextShot, muzzleHeight;
    readonly List<Health> found = new();

    void Awake()
    {
        self = GetComponent<Health>();
        muzzleHeight = GetComponent<Collider>().bounds.size.y * 0.8f; // measured before construction squashes it
        beams = new LineRenderer[targets];
        for (int i = 0; i < targets; i++)
        {
            var lr = new GameObject("Beam").AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.material = laserMaterial;
            lr.material.color = color * 3f; // HDR so the bloom catches it
            lr.widthMultiplier = beamWidth;
            lr.positionCount = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.enabled = false;
            beams[i] = lr;
        }
    }

    void Update()
    {
        if (Time.time < nextShot) return;
        if (self.team == 0 && Structure.LowPower) return; // offline

        // Nearest enemies first.
        found.Clear();
        float r2 = Range * Range;
        foreach (var h in Health.All)
            if (!Net.Allied(h.team, self.team) && (h.transform.position - transform.position).sqrMagnitude <= r2 && !h.CloakedFrom(transform.position)) found.Add(h);
        if (found.Count == 0) return;
        found.Sort((a, b) => (a.transform.position - transform.position).sqrMagnitude.CompareTo((b.transform.position - transform.position).sqrMagnitude));
        if (found.Count > targets) found.RemoveRange(targets, found.Count - targets);

        nextShot = Time.time + cooldown / Stats.Value(Stat.FireRate, 1f, this);
        Shoot(found, true);
        if (TryGetComponent(out NetEntity net)) // other players see the shots
            net.SendShot(found[0], found.Count > 1 ? found[1] : null, found.Count > 2 ? found[2] : null);
    }

    // Multiplayer: another player's tower fired; show it, damage comes from its owner.
    public void ShowShots(List<Health> list) => Shoot(list, false);

    void HideBeams() { foreach (var b in beams) b.enabled = false; }

    void Shoot(List<Health> list, bool real)
    {
        float dmg = Stats.Value(Stat.Damage, damage, this);
        Vector3 muzzle = transform.position + Vector3.up * muzzleHeight;
        int shots = Mathf.Min(targets, list.Count);
        for (int i = 0; i < shots; i++)
        {
            Health t = list[i];
            Vector3 from = i == 0 ? muzzle : list[i - 1].transform.position + Vector3.up; // arcs chain target to target
            Vector3 hit = t.transform.position + Vector3.up;
            if (i == 0 && visual.muzzleFlash != null) Fx.Spawn(visual.muzzleFlash, muzzle, Quaternion.LookRotation(hit - muzzle));

            if (bolt != null)
            {
                Fx.Spawn(bolt, from, Quaternion.LookRotation(hit - from), new Vector3(1f, 1f, Vector3.Distance(from, hit)));
                Strike(t, hit, dmg, real);
            }
            else if (visual.projectile != null)
            {
                Projectile.Fire(visual.projectile, from, t, visual.speed, visual.arc, at => Strike(t, at, dmg, real));
            }
            else
            {
                beams[i].SetPosition(0, from);
                beams[i].SetPosition(1, hit);
                beams[i].enabled = true;
                Strike(t, hit, dmg, real);
            }
        }
        CancelInvoke(nameof(HideBeams));
        Invoke(nameof(HideBeams), beamTime); // Invoke: also runs on other players' copies, whose Update is off
    }

    // Impact effects, then damage the target or everything near the landing point.
    void Strike(Health t, Vector3 at, float dmg, bool real)
    {
        if (visual.impact != null) Fx.Spawn(visual.impact, at, Quaternion.identity, splash > 0f ? splash : 1f);
        if (visual.scorch != null) Fx.Spawn(visual.scorch, new Vector3(at.x, 0.04f, at.z), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), splash > 0f ? splash * 0.8f : 1f);
        if (!real) return; // just the look
        if (splash <= 0f) { if (t != null && t.isActiveAndEnabled) t.TakeDamage(dmg, self); return; }
        float s2 = splash * splash;
        var hits = new List<Health>();
        foreach (var h in Health.All)
        {
            Vector3 d = h.transform.position - at; d.y = 0f;
            if (!Net.Allied(h.team, self.team) && d.sqrMagnitude <= s2) hits.Add(h); // copy: deaths edit Health.All
        }
        foreach (var h in hits) h.TakeDamage(dmg, self);
    }
}
