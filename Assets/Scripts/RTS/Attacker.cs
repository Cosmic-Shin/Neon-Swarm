using UnityEngine;
using UnityEngine.AI;

// Shoots enemies: walks into range of an ordered target, or picks one nearby while idle.
[RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(SelectableUnit))]
public class Attacker : MonoBehaviour
{
    [SerializeField] Material laserMaterial; // unlit; tinted per team
    [SerializeField] float aggroExtra = 6f;  // idle units notice enemies this far past their range
    [SerializeField] float leashExtra = 10f; // and give up chasing past this

    static readonly Color PlayerLaser = new Color(0.2f, 1f, 1f) * 3f; // HDR so the bloom catches it
    static readonly Color EnemyLaser = new Color(1f, 0.15f, 0.2f) * 3f;

    NavMeshAgent agent;
    Health self;
    UnitData data;
    SelectableUnit unit;
    Health target;
    bool ordered;  // player told us to attack this target: chase it anywhere
    float nextShot;
    LineRenderer laser;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        self = GetComponent<Health>();
        unit = GetComponent<SelectableUnit>();
        data = unit.Data;

        laser = new GameObject("Laser").AddComponent<LineRenderer>();
        laser.transform.SetParent(transform, false);
        laser.material = laserMaterial;
        laser.material.color = self.team == 0 ? PlayerLaser : EnemyLaser;
        laser.widthMultiplier = 0.15f;
        laser.positionCount = 2;
        laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        laser.enabled = false;
    }

    Vector3? attackMove; // walking here, but fighting anything met on the way
    bool resume;         // attack-move was interrupted by a fight: walk on afterwards
    bool hold;           // hold position: never walk, only shoot what's in range

    public bool HasTarget => target != null;
    float Range => Stats.Value(Stat.Range, data.range, this); // research / veterancy
    public void Attack(Health t) { Stop(); target = t; ordered = true; }
    public void Stop() { target = null; ordered = false; attackMove = null; hold = false; }
    public void Hold() { Stop(); hold = true; if (agent.isOnNavMesh) agent.ResetPath(); }

    public void AttackMove(Vector3 to)
    {
        Stop();
        attackMove = to;
        if (agent.isOnNavMesh) agent.SetDestination(to);
    }

    // Got shot: answer back unless busy or walking somewhere (attack-move counts as looking for a fight).
    public void Retaliate(Health from)
    {
        if (target == null && (attackMove != null || !IsMoving()) && from.team != self.team) target = from;
    }

    bool IsMoving() => agent.enabled && (agent.pathPending || (agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.5f));

    void Update()
    {
        if (!agent.enabled || data.damage <= 0f) return; // walking out of a building / unarmed

        if (unit.IsStunned) return; // EMP
        if (target != null && (target.IsDead || !target.isActiveAndEnabled || target.CloakedFrom(transform.position))) target = null; // dead, garrisoned, outpost gone neutral, or cloaked
        if (target == null) // none yet, or it died
        {
            if (ordered) { ordered = false; if (agent.isOnNavMesh) agent.ResetPath(); }
            if (attackMove is Vector3 dest)
            {
                target = FindEnemy(Range + aggroExtra);
                if (target == null)
                {
                    if (resume) { resume = false; if (agent.isOnNavMesh) agent.SetDestination(dest); }
                    else if (!IsMoving()) attackMove = null; // arrived
                    return;
                }
                resume = true;
            }
            else
            {
                if (IsMoving()) return; // a move order beats looking for fights
                target = FindEnemy(hold ? Range : Range + aggroExtra);
                if (target == null) return;
            }
        }

        Vector3 aim = AimPoint(target, transform.position);
        Vector3 to = aim - transform.position;
        to.y = 0f;
        float dist = to.magnitude - Radius(target);

        if (dist > Range)
        {
            if (hold && !ordered) { target = null; return; } // holding: don't chase
            if (!ordered && dist > Range + leashExtra) { target = null; return; } // let it go
            if (agent.isOnNavMesh) agent.SetDestination(aim);
            return;
        }

        if (agent.hasPath) agent.ResetPath();
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), agent.angularSpeed * Time.deltaTime);

        if (Time.time < nextShot) return;
        nextShot = Time.time + data.attackCooldown / Stats.Value(Stat.FireRate, 1f, this);
        Fire(target);
    }

    void Fire(Health t)
    {
        Shoot(t, true);
        if (TryGetComponent(out NetEntity net)) net.SendShot(t); // other players see the shot
    }

    // Multiplayer: another player's unit fired; show it, damage comes from its owner.
    public void ShowShot(Health t) { if (t != null) Shoot(t, false); }

    void HideLaser() => laser.enabled = false;

    void Shoot(Health t, bool real)
    {
        var v = data.visual;
        Vector3 muzzle = transform.position + Vector3.up + transform.forward * 0.8f;
        Vector3 aim = AimPoint(t, transform.position) + Vector3.up;
        float dmg = Stats.Value(Stat.Damage, data.damage, this);
        float splash = data.splash > 0f ? Stats.Value(Stat.Splash, data.splash, this) : 0f; // worked out now: the shell can land after we die
        float stun = Stats.Value(Stat.StunTime, data.stunOnHit, this);
        if (v.muzzleFlash != null) Fx.Spawn(v.muzzleFlash, muzzle, Quaternion.LookRotation(aim - muzzle));

        if (v.projectile != null) // damage lands when the shot does
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
            Invoke(nameof(HideLaser), 0.08f); // Invoke: also runs on other players' copies, whose Update is off
        }
        Land(t, aim, dmg, splash, stun, muzzle, real);
    }

    readonly System.Collections.Generic.List<Health> hits = new();

    // Impact effects, then damage: the target, or everything near the landing point (splash).
    // Can run after this unit died (a shell still in the air), so it only uses data and self's fields.
    void Land(Health t, Vector3 at, float dmg, float splash, float stun, Vector3 from, bool real)
    {
        var v = data.visual;
        float r = splash > 0f ? splash : 1f;
        Vector3 flat = at - from; flat.y = 0f;
        if (v.impact != null) Fx.Spawn(v.impact, at, Quaternion.identity, r);
        bool rapid = data.attackCooldown < 1f;
        if (v.scorch != null && (!rapid || Random.value < 0.25f)) // rapid fire would carpet the ground
            Fx.Spawn(v.scorch, new Vector3(at.x, 0.04f, at.z), flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat) : Quaternion.identity, r * 0.8f);

        if (!real) return; // just the look
        if (splash <= 0f) { if (t != null && t.isActiveAndEnabled) Hit(t, dmg, stun); return; }
        hits.Clear(); // copy first: kills change Health.All
        float s2 = splash * splash;
        foreach (var h in Health.All)
        {
            Vector3 d = h.transform.position - at; d.y = 0f;
            if (h.team != self.team && d.sqrMagnitude <= s2) hits.Add(h);
        }
        foreach (var h in hits) Hit(h, dmg, stun);
    }

    void Hit(Health h, float dmg, float stun)
    {
        bool isUnit = h.TryGetComponent(out SelectableUnit u);
        h.TakeDamage(isUnit ? dmg : dmg * data.buildingDamage, self);
        if (!isUnit) return;
        if (stun > 0f) u.Stun(stun);
        if (data.slowOnHit > 0f) u.Slow(data.slowOnHit);
    }

    // ponytail: scans every unit each idle frame; fine for dozens, use a physics overlap if it grows to hundreds.
    Health FindEnemy(float radius)
    {
        Health best = null;
        float bestSqr = radius * radius;
        foreach (var h in Health.All)
        {
            if (h.team == self.team || h.CloakedFrom(transform.position)) continue;
            float sqr = (AimPoint(h, transform.position) - transform.position).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = h; }
        }
        return best;
    }

    // Units: their centre (minus agent radius). Buildings: the nearest point of their wall, so big ones are reachable.
    static Vector3 AimPoint(Health h, Vector3 from) =>
        !h.TryGetComponent(out NavMeshAgent _) && h.TryGetComponent(out Collider c) ? c.ClosestPoint(from) : h.transform.position;
    static float Radius(Health h) => h.TryGetComponent(out NavMeshAgent a) ? a.radius * h.transform.lossyScale.x : 0f;
}
