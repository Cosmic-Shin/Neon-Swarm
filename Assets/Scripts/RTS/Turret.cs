using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class Turret : MonoBehaviour
{
    [SerializeField] Material laserMaterial;
    [SerializeField] float range = 18f;
    [SerializeField] float damage = 12f;
    [SerializeField] float cooldown = 0.8f;
    [SerializeField] int targets = 1;
    [SerializeField] float splash = 0f;
    [SerializeField] bool hitsGround = true;
    [SerializeField] bool hitsAir = true;
    public bool HitsAir => hitsAir;
    public bool HitsGround => hitsGround;
    [SerializeField] DamageTable.Weapon weapon;
    [SerializeField] float beamWidth = 0.2f;
    [SerializeField] float beamTime = 0.08f;
    [SerializeField] Color color = new(0.2f, 1f, 1f);
    [SerializeField] WeaponVisual visual = new();
    [SerializeField] GameObject bolt;

    public float Range => Stats.Value(Stat.Range, range, this);

    Health self;
    LineRenderer[] beams;
    float nextShot, muzzleHeight;
    readonly List<Health> found = new();
    RecoilKick kick;

    void Awake()
    {
        kick = GetComponent<RecoilKick>();
        self = GetComponent<Health>();
        muzzleHeight = GetComponent<Collider>().bounds.size.y * 0.8f;
        beams = new LineRenderer[targets];
        for (int i = 0; i < targets; i++)
        {
            var lr = new GameObject("Beam").AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.material = laserMaterial;
            lr.material.color = color * 3f;
            lr.widthMultiplier = beamWidth;
            lr.positionCount = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.enabled = false;
            beams[i] = lr;
        }
    }

    public Health Aim { get; private set; }
    public float LastShot { get; private set; } = -99f;
    public float Charge01 => Mathf.Clamp01(1f - (nextShot - Time.time) / Mathf.Max(0.01f, cooldown / Stats.Value(Stat.FireRate, 1f, this)));
    public bool Online => !Structure.LowPowerOf(self.team);
    float nextAim;

    void Update()
    {
        if (!Online) { Aim = null; return; }
        if (Time.time >= nextAim)
        {
            nextAim = Time.time + 0.25f;
            Scan();
            Aim = found.Count > 0 ? found[0] : null;
        }
        if (Time.time < nextShot) return;
        if (Aim == null) return;
        Scan();
        if (found.Count == 0) return;
        Aim = found[0];

        nextShot = Time.time + cooldown / Stats.Value(Stat.FireRate, 1f, this);
        Shoot(found, true);
        if (TryGetComponent(out NetEntity net))
            net.SendShot(found[0], found.Count > 1 ? found[1] : null, found.Count > 2 ? found[2] : null);
    }

    void Scan()
    {
        found.Clear();
        float r2 = Range * Range;
        foreach (var h in Health.All)
            if (!Net.Allied(h.team, self.team) && (Flyer.IsAir(h) ? hitsAir : hitsGround) && (h.transform.position - transform.position).sqrMagnitude <= r2 && !h.CloakedFrom(transform.position)) found.Add(h);
        found.Sort((a, b) => (a.transform.position - transform.position).sqrMagnitude.CompareTo((b.transform.position - transform.position).sqrMagnitude));
        if (found.Count > targets) found.RemoveRange(targets, found.Count - targets);
    }

    List<Transform> muzzles;
    int nextMuzzle;
    Vector3 NextMuzzle()
    {
        if (muzzles == null)
        {
            muzzles = new List<Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name.Contains("_Muzzle") && !t.name.Contains("Glow") && !t.name.Contains("Brake")) muzzles.Add(t);
        }
        for (int i = 0; i < muzzles.Count; i++)
        {
            var m = muzzles[nextMuzzle++ % muzzles.Count];
            if (m != null && m.gameObject.activeInHierarchy) return m.position;
        }
        return transform.position + Vector3.up * muzzleHeight;
    }

    public void ShowShots(List<Health> list) => Shoot(list, false);

    void HideBeams() { foreach (var b in beams) b.enabled = false; }

    void Shoot(List<Health> list, bool real)
    {
        LastShot = Time.time;
        if (list.Count > 0) Aim = list[0];
        float dmg = Stats.Value(Stat.Damage, damage, this);
        Vector3 muzzle = NextMuzzle();
        int shots = Mathf.Min(targets, list.Count);
        if (kick != null && shots > 0) kick.Kick(list[0].transform.position - transform.position);
        for (int i = 0; i < shots; i++)
        {
            Health t = list[i];
            Vector3 from = i == 0 ? muzzle : Flyer.Center(list[i - 1]) + Vector3.up;
            Vector3 hit = Flyer.Center(t) + Vector3.up;
            if (i == 0 && visual.muzzleFlash != null) Fx.Spawn(visual.muzzleFlash, muzzle, Quaternion.LookRotation(hit - muzzle));
            if (i == 0) Sfx.At(Sfx.ShotOf(damage, splash, bolt != null), muzzle, 0.6f);
            if (i == 0) LightPool.Flash(muzzle, Color.Lerp(ProductionFX.HoloColor(gameObject), Color.white, 0.5f), damage >= 50f ? 6f : 3f, 10f);

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
        Invoke(nameof(HideBeams), beamTime);
    }

    void Strike(Health t, Vector3 at, float dmg, bool real)
    {
        if (visual.impact != null) Fx.Spawn(visual.impact, at, Quaternion.identity, splash > 0f ? splash : 1f);
        if (splash > 0f) Sfx.At(Snd.BoomSmall, at, 0.4f);
        if (visual.scorch != null) Fx.Spawn(visual.scorch, new Vector3(at.x, 0.04f, at.z), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), splash > 0f ? splash * 0.8f : 1f);
        if (!real) return;
        if (splash <= 0f) { if (t != null && t.isActiveAndEnabled) t.TakeDamage(dmg * DamageTable.Factor(weapon, t), self); return; }
        float s2 = splash * splash;
        bool air = Flyer.IsAir(t);
        var hits = new List<Health>();
        foreach (var h in Health.All)
        {
            Vector3 d = h.transform.position - at; d.y = 0f;
            if (!Net.Allied(h.team, self.team) && d.sqrMagnitude <= s2 && Flyer.IsAir(h) == air) hits.Add(h);
        }
        foreach (var h in hits) h.TakeDamage(dmg * DamageTable.Factor(weapon, h), self);
    }
}
