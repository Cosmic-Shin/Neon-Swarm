using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(SelectableUnit))]
public class Attacker : MonoBehaviour
{
    [SerializeField] Material laserMaterial;
    [SerializeField] float aggroExtra = 6f;
    [SerializeField] float leashExtra = 10f;

    static readonly Color PlayerLaser = new Color(0.2f, 1f, 1f) * 3f;
    static readonly Color EnemyLaser = new Color(1f, 0.15f, 0.2f) * 3f;

    NavMeshAgent agent;
    Health self;
    UnitData data;
    SelectableUnit unit;
    Health target;
    bool ordered;
    float nextShot;
    LineRenderer laser;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        self = GetComponent<Health>();
        unit = GetComponent<SelectableUnit>();
        data = unit.Data;
        nextScan = Random.value * ScanEvery;

        laser = new GameObject("Laser").AddComponent<LineRenderer>();
        laser.transform.SetParent(transform, false);
        laser.material = laserMaterial;
        laser.material.color = self.team == 0 ? PlayerLaser : EnemyLaser;
        laser.widthMultiplier = 0.15f;
        laser.positionCount = 2;
        laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        laser.enabled = false;

        attackEvents = GetComponentInChildren<AttackEvents>(true);
        if (attackEvents != null)
        {
            attackEvents.OnFire += FireNow;
            var anim = attackEvents.GetComponent<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
                foreach (var c in anim.runtimeAnimatorController.animationClips) if (c.name == "Attack") clipLength = c.length;
        }
        kick = GetComponentInChildren<RecoilKick>(true);
        switch (data.ability)
        {
            case UnitData.Ability.Brood: gameObject.AddComponent<Brood>(); break;
            case UnitData.Ability.Overheat: heat = gameObject.AddComponent<Overheat>(); break;
            case UnitData.Ability.Charge: gameObject.AddComponent<Charge>(); break;
        }
        var pattern = new System.Text.RegularExpressions.Regex(string.IsNullOrEmpty(data.muzzles) ? "Muzzle(-?\\d)?$" : data.muzzles);
        foreach (var t in GetComponentsInChildren<Transform>(true)) if (pattern.IsMatch(t.name)) muzzles.Add(t);
    }

    AttackEvents attackEvents;
    float clipLength;
    RecoilKick kick;
    Overheat heat;
    readonly System.Collections.Generic.List<Transform> muzzles = new();
    Health pending;
    bool pendingReal;

    Vector3? attackMove;
    bool resume;
    bool hold;

    public bool HasTarget => target != null;
    public bool Busy => target != null || attackMove != null;
    public Health Target => target;
    float range = -1f;
    float Range => range >= 0f ? range : range = Stats.Value(Stat.Range, data.range, this);
    void OnEnable() => Stats.Changed += ForgetRange;
    void OnDisable() => Stats.Changed -= ForgetRange;
    void ForgetRange() => range = -1f;

    const float ScanEvery = 0.25f;
    float nextScan;
    Health Scan(float radius)
    {
        if (Time.time < nextScan) return null;
        nextScan = Time.time + ScanEvery;
        return FindEnemy(radius);
    }
    public void Attack(Health t) { Stop(); if (!CanHit(t)) return; target = t; ordered = true; }
    public bool CanHit(Health h) => Flyer.IsAir(h) ? data.hitsAir : data.hitsGround;
    public void Stop() { target = null; ordered = false; attackMove = null; hold = false; }
    public void Hold() { Stop(); hold = true; if (agent.isOnNavMesh) agent.ResetPath(); }

    public void AttackMove(Vector3 to)
    {
        Stop();
        if (NavMesh.SamplePosition(to, out NavMeshHit hit, 12f, NavMesh.AllAreas)) to = hit.position;
        attackMove = to;
        if (agent.isOnNavMesh) agent.SetDestination(to);
    }

    public void Retaliate(Health from)
    {
        if (target == null && (attackMove != null || !IsMoving()) && !Net.Allied(from.team, self.team) && CanHit(from)) target = from;
    }

    bool IsMoving() => agent.enabled && (agent.pathPending || (agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.5f));

    void Update()
    {
        if (!agent.enabled || data.damage <= 0f) return;

        if (unit.IsStunned) return;
        if (target != null && (target.IsDead || !target.isActiveAndEnabled || target.CloakedFrom(transform.position))) target = null;
        if (target == null)
        {
            if (ordered) { ordered = false; if (agent.isOnNavMesh) agent.ResetPath(); }
            if (attackMove is Vector3 dest)
            {
                target = Scan(Range + aggroExtra);
                if (target == null)
                {
                    if (resume) { resume = false; if (agent.isOnNavMesh) agent.SetDestination(dest); }
                    else if (!IsMoving()) attackMove = null;
                    return;
                }
                resume = true;
            }
            else
            {
                if (IsMoving()) return;
                target = Scan(hold ? Range : Range + aggroExtra);
                if (target == null) return;
            }
        }

        Vector3 aim = AimPoint(target, transform.position);
        Vector3 to = aim - transform.position;
        to.y = 0f;
        float dist = to.magnitude - Radius(target);

        if (dist > Range)
        {
            if (hold && !ordered) { target = null; return; }
            if (!ordered && dist > Range + leashExtra) { target = null; return; }
            if (agent.isOnNavMesh) agent.SetDestination(aim);
            return;
        }

        if (agent.hasPath) agent.ResetPath();
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), agent.angularSpeed * Time.deltaTime);

        if (Time.time < nextShot || pending != null || (heat != null && !heat.Ready)) return;
        nextShot = Time.time + Mathf.Max(clipLength, data.attackCooldown / Stats.Value(Stat.FireRate, 1f, this));
        Fire(target);
        if (heat != null) heat.Shot();
    }

    void Fire(Health t)
    {
        Begin(t, true);
        if (TryGetComponent(out NetEntity net)) net.SendShot(t);
    }

    public void ShowShot(Health t) { if (t != null) Begin(t, false); }

    void Begin(Health t, bool real)
    {
        if (attackEvents == null) { Release(t, real); return; }
        pending = t;
        pendingReal = real;
        attackEvents.PlayAttack();
        CancelInvoke(nameof(FireNow));
        Invoke(nameof(FireNow), clipLength + 0.1f);
    }

    void FireNow()
    {
        if (pending == null) return;
        Health t = pending;
        pending = null;
        CancelInvoke(nameof(FireNow));
        Release(t, pendingReal);
    }

    void Release(Health t, bool real)
    {
        if (t == null) return;
        if (kick != null) kick.Kick(t.transform.position - transform.position);
        switch (data.style)
        {
            case UnitData.WeaponStyle.ShockPulse: Pulse(real); return;
            case UnitData.WeaponStyle.PodDrop: if (real) DropPod(t); break;
            case UnitData.WeaponStyle.FlameStream: StartCoroutine(FlameStream(t)); break;
        }
        if (muzzles.Count == 0) { Shoot(t, real, transform.position + Vector3.up * (1f + Flyer.HeightOf(this)) + transform.forward * 0.8f, 1f); return; }
        foreach (var m in muzzles) Shoot(t, real, m.position, 1f / muzzles.Count);
    }

    System.Collections.IEnumerator FlameStream(Health t)
    {
        var flame = data.visual.muzzleFlash;
        Transform nozzle = muzzles.Count > 0 ? muzzles[0] : transform;
        Sfx.At(Snd.Flame, nozzle.position, 0.6f);
        LightPool.Flash(nozzle.position, Juice.Fire, 4f, 9f, 0.5f);
        for (float e = 0f; e < 0.45f && flame != null; e += 0.07f)
        {
            yield return new WaitForSeconds(0.07f);
            if (t == null) yield break;
            Vector3 to = Flyer.Center(t) + Vector3.up;
            Fx.Spawn(flame, nozzle.position, Quaternion.LookRotation(to - nozzle.position));
        }
    }

    readonly System.Collections.Generic.List<SelectableUnit> pods = new();
    const int MaxPods = 4;

    void DropPod(Health t)
    {
        pods.RemoveAll(p => p == null || p.GetComponent<Health>().IsDead);
        if (pods.Count >= MaxPods || data.extra == null) return;
        Vector3 from = muzzles.Count > 0 ? muzzles[0].position : Flyer.Center(this);
        Vector3 ground = new(from.x, 0f, from.z);
        if (NavMesh.SamplePosition(ground, out NavMeshHit hit, 15f, NavMesh.AllAreas)) ground = hit.position;
        var pod = Net.Spawn(data.extra, from, transform.rotation).GetComponent<SelectableUnit>();
        SetTeam(pod, self.team);
        pod.GetComponent<Health>().onDeath = () =>
        {
            if (pod == null) return;
            pod.HideRing();
            if (pod.TryGetComponent(out UnitDeath d)) d.Die(); else Net.Destroy(pod.gameObject);
        };
        pod.WalkOut(from, ground, 14f, null, t.transform.position);
        pods.Add(pod);
        Juice.Burst(from, ProductionFX.HoloColor(gameObject), 10, 5f, 0.15f, 0.5f, -1f);
    }

    public static void SetTeam(SelectableUnit u, int team)
    {
        if (Net.Online) return;
        u.GetComponent<Health>().team = team;
        if (u.TryGetComponent(out TeamMember tm)) tm.team = team;
        u.ApplyTeamLook();
    }

    void Pulse(bool real)
    {
        float r = data.splash > 0f ? Stats.Value(Stat.Splash, data.splash, this) : 8f;
        Vector3 ground = new(transform.position.x, 0.3f, transform.position.z);
        Color c = ProductionFX.HoloColor(gameObject);
        Juice.Ring(ground, c, 1f, r, 0.4f, 0.4f);
        Juice.Flash(Flyer.Center(this), c, 6f, r * 2f, 0.3f);
        Sfx.At(Snd.Zap, ground, 0.8f);

        hits.Clear();
        foreach (var h in Health.All)
        {
            Vector3 d = h.transform.position - transform.position; d.y = 0f;
            if (!Net.Allied(h.team, self.team) && !Flyer.IsAir(h) && d.sqrMagnitude <= r * r) hits.Add(h);
        }
        for (int i = 0; i < Mathf.Min(6, muzzles.Count) && data.extra != null; i++)
        {
            Vector3 from = muzzles[Random.Range(0, muzzles.Count)].position;
            Vector2 rand = Random.insideUnitCircle * r * 0.6f;
            Vector3 to = i < hits.Count ? hits[i].transform.position + Vector3.up * 0.5f : ground + new Vector3(rand.x, 0f, rand.y);
            Fx.Spawn(data.extra, from, Quaternion.LookRotation(to - from), new Vector3(1f, 1f, Vector3.Distance(from, to)));
        }
        if (!real) return;
        float dmg = Stats.Value(Stat.Damage, data.damage, this), stun = Stats.Value(Stat.StunTime, data.stunOnHit, this);
        foreach (var h in hits.ToArray()) Hit(h, dmg, stun);
    }

    void HideLaser() => laser.enabled = false;

    void Shoot(Health t, bool real, Vector3 muzzle, float share)
    {
        var v = data.visual;
        Vector3 aim = AimPoint(t, transform.position) + Vector3.up * (1f + Flyer.HeightOf(t));
        float dmg = Stats.Value(Stat.Damage, data.damage, this) * share;
        float splash = data.splash > 0f ? Stats.Value(Stat.Splash, data.splash, this) : 0f;
        float stun = Stats.Value(Stat.StunTime, data.stunOnHit, this);
        if (v.muzzleFlash != null) Fx.Spawn(v.muzzleFlash, muzzle, Quaternion.LookRotation(aim - muzzle));
        Sfx.At(Sfx.ShotOf(data.damage, data.splash, data.stunOnHit > 0f), muzzle, 0.5f);
        LightPool.Flash(muzzle, Color.Lerp(ProductionFX.HoloColor(gameObject), Color.white, 0.5f), data.damage >= 30f ? 4f : 2.5f);

        if (v.projectile != null)
        {
            Projectile.Fire(v.projectile, muzzle, t, v.speed, v.arc, at => Land(t, at, dmg, splash, stun, muzzle, real));
            return;
        }
        if (!v.hideBeam)
        {
            laser.material.color = self.team == 0 ? PlayerLaser : EnemyLaser;
            laser.SetPosition(0, muzzle);
            laser.SetPosition(1, aim);
            laser.enabled = true;
            CancelInvoke(nameof(HideLaser));
            Invoke(nameof(HideLaser), 0.08f);
        }
        Land(t, aim, dmg, splash, stun, muzzle, real);
    }

    readonly System.Collections.Generic.List<Health> hits = new();

    static readonly Color Spark = new(1f, 0.9f, 0.55f), Dirt = new(0.32f, 0.27f, 0.24f, 0.9f), DirtChunk = new(0.45f, 0.38f, 0.3f), Fireball = new(1f, 0.55f, 0.15f), Smoke = new(0.2f, 0.2f, 0.22f, 0.8f);

    static void ImpactByWeapon(DamageTable.Weapon w, Vector3 at, float splash)
    {
        if (!Juice.Seen(at)) return;
        Vector3 ground = new(at.x, 0.2f, at.z);
        switch (w)
        {
            case DamageTable.Weapon.Gun:
                Juice.Burst(at, Spark, 4, 7f, 0.05f, 0.22f, 0.3f);
                break;
            case DamageTable.Weapon.Cannon:
                Juice.Burst(ground, DirtChunk, 7, 5f, 0.14f, 0.8f, 1.4f, gravity: true);
                for (int i = 0; i < 2; i++) Particles.Emit("Dust", ground, Vector3.up * 1.5f + Random.insideUnitSphere * 1.2f, 1.4f, 1f, Dirt);
                break;
            case DamageTable.Weapon.Explosive:
                Juice.Flash(at, Fireball, 3f, Mathf.Max(6f, splash * 2f), 0.25f);
                Juice.Burst(at, Juice.Fire, 10, 6f, 0.16f, 0.5f, 0.9f, gravity: true);
                for (int i = 0; i < 2; i++) Particles.Emit("Smoke", at + Random.insideUnitSphere * 0.5f, Vector3.up * 1.8f, Mathf.Max(1.2f, splash * 0.5f), 1.4f, Smoke);
                break;
        }
    }

    void Land(Health t, Vector3 at, float dmg, float splash, float stun, Vector3 from, bool real)
    {
        var v = data.visual;
        float r = splash > 0f ? splash : 1f;
        Vector3 flat = at - from; flat.y = 0f;
        if (v.impact != null) Fx.Spawn(v.impact, at, Quaternion.identity, splash > 0f ? r * 0.6f : r);
        if (splash > 0f) Sfx.At(Snd.BoomSmall, at, 0.4f);
        ImpactByWeapon(data.weapon, at, splash);
        bool rapid = data.attackCooldown < 1f;
        if (v.scorch != null && (!rapid || Random.value < 0.25f))
            Fx.Spawn(v.scorch, new Vector3(at.x, 0.04f, at.z), flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat) : Quaternion.identity, r * 0.8f);

        if (!real) return;
        if (splash <= 0f) { if (t != null && t.isActiveAndEnabled) Hit(t, dmg, stun); return; }
        hits.Clear();
        float s2 = splash * splash;
        bool air = Flyer.IsAir(t);
        foreach (var h in Health.All)
        {
            Vector3 d = h.transform.position - at; d.y = 0f;
            if (!Net.Allied(h.team, self.team) && d.sqrMagnitude <= s2 && Flyer.IsAir(h) == air) hits.Add(h);
        }
        foreach (var h in hits) Hit(h, dmg, stun);
    }

    void Hit(Health h, float dmg, float stun)
    {
        bool isUnit = h.TryGetComponent(out SelectableUnit u);
        dmg *= DamageTable.Factor(data.weapon, h);
        h.TakeDamage(isUnit ? dmg : dmg * data.buildingDamage, self);
        if (!isUnit) return;
        if (stun > 0f) u.Stun(stun);
        if (data.slowOnHit > 0f) u.Slow(data.slowOnHit);
    }

    Health FindEnemy(float radius)
    {
        Health best = null;
        float bestSqr = radius * radius;
        Vector3 me = transform.position;
        float near = radius + 25f;
        var all = Health.All;
        for (int i = 0; i < all.Count; i++)
        {
            var h = all[i];
            Vector3 d = h.transform.position - me;
            if (d.x * d.x + d.z * d.z > near * near || Net.Allied(h.team, self.team) || !CanHit(h) || h.CloakedFrom(me)) continue;
            float sqr = (AimPoint(h, me) - me).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = h; }
        }
        return best;
    }

    static Vector3 AimPoint(Health h, Vector3 from) =>
        !h.TryGetComponent(out NavMeshAgent _) && h.TryGetComponent(out Collider c) ? c.ClosestPoint(from) : h.transform.position;
    static float Radius(Health h) => Flyer.IsAir(h) ? Flyer.SizeOf(h) * 0.5f : h.TryGetComponent(out NavMeshAgent a) ? a.radius * h.transform.lossyScale.x : 0f;
}
