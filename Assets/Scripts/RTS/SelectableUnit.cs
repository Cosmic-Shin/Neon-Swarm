using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SelectableUnit : MonoBehaviour
{
    [SerializeField] GameObject selectionRing;
    [SerializeField] UnitData data;     // name, icon, cost, build time, health
    [SerializeField] Animator animator; // optional: Idle/Walk driven by "Speed"

    static readonly int SpeedHash = Animator.StringToHash("Speed");

    public UnitData Data => data;
    public string DisplayName => data.displayName;
    public Sprite Icon => data.icon;
    public float BuildTime => data.buildTime;
    public int Cost => data.cost;
    public bool IsSelected => manager != null;
    public bool IsEnemy => health != null && health.team != 0; // can't be selected; red ring always on

    static readonly Color EnemyRing = new(1f, 0.1f, 0.15f);
    static readonly Color CreepRing = new(1f, 0.55f, 0.1f);
    Vector3 faceOnArrive; // formation facing; zero = none

    NavMeshAgent agent;
    Harvester harvester; // only on workers
    Attacker attacker;   // only on armed units
    Health health;
    GoodSelectionManager manager;
    int formationIndex = -1;
    OutpostCapture garrisonTarget; // walking to our outpost to go inside
    Transport boardTarget;         // walking to our Wasp to climb aboard
    float boardRepath;
    float stunnedUntil;

    // EMP: can't move or shoot for a while.
    public bool IsStunned => Time.time < stunnedUntil;
    public bool IsRemote { get; set; } // multiplayer: another player's unit, moved by the network
    Vector3 lastPos;
    float remoteSpeed;

    public void Stun(float seconds)
    {
        if (IsRemote) { GetComponent<NetEntity>().RPC_Status(seconds, 0f); return; } // its owner applies it
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);
        if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
    }

    // Wire snare: half speed for a while.
    float slowedUntil;
    bool wasSlowed;
    public void Slow(float seconds)
    {
        if (IsRemote) { GetComponent<NetEntity>().RPC_Status(0f, seconds); return; }
        slowedUntil = Mathf.Max(slowedUntil, Time.time + seconds);
    }

    // Before research / veterancy; RefreshStats applies the bonuses.
    float baseSpeed, baseCapacity, baseHarvestRate, baseUnloadTime;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (data != null && data.moveSpeed > 0f) agent.speed = data.moveSpeed;
        harvester = GetComponent<Harvester>();
        attacker = GetComponent<Attacker>();
        health = GetComponent<Health>();
        baseSpeed = agent.speed;
        if (harvester != null) (baseCapacity, baseHarvestRate, baseUnloadTime) = (harvester.capacity, harvester.harvestRate, harvester.unloadTime);
        Stats.Changed += RefreshStats;
        RefreshStats();
        ApplyTeamLook();
    }

    // Enemy ring always on, tinted (multiplayer: that player's colour). Re-run once a network unit knows its team.
    public void ApplyTeamLook()
    {
        if (selectionRing == null) return;
        selectionRing.SetActive(IsEnemy || IsSelected);
        if (!IsEnemy) return;
        var mat = selectionRing.GetComponent<Renderer>().material; // per-enemy copy
        Color ring = health.team == Creep.Team ? CreepRing : Net.Online ? Net.ColorOf(health.team) : EnemyRing;
        mat.color = ring;
        mat.SetColor("_EmissionColor", ring * 2f);
    }

    void RefreshStats()
    {
        if (harvester != null) // it drives its own agent speed
        {
            harvester.capacity = Stats.Value(Stat.HarvestCapacity, baseCapacity, this);
            harvester.harvestRate = Stats.Value(Stat.HarvestRate, baseHarvestRate, this);
            harvester.unloadTime = Stats.Value(Stat.UnloadTime, baseUnloadTime, this);
            return;
        }
        agent.speed = Stats.Value(Stat.MoveSpeed, baseSpeed, this) * (wasSlowed ? 0.5f : 1f);
    }

    void Update()
    {
        if (IsRemote) // legs follow how fast the network moves it
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            remoteSpeed = Mathf.Lerp(remoteSpeed, (transform.position - lastPos).magnitude / dt, 10f * dt);
            lastPos = transform.position;
            if (animator != null) animator.SetFloat(SpeedHash, remoteSpeed);
            return;
        }
        if (animator != null && agent.enabled) animator.SetFloat(SpeedHash, agent.velocity.magnitude);
        if (agent.enabled && agent.isOnNavMesh && agent.isStopped && !IsStunned) agent.isStopped = false; // stun wore off
        bool slowedNow = Time.time < slowedUntil;
        if (slowedNow != wasSlowed) { agent.speed *= slowedNow ? 0.5f : 2f; wasSlowed = slowedNow; } // only on change (harvesters set their own speed)

        Settle();

        // Formation move done: turn to face the way the formation points (unless busy shooting).
        if (faceOnArrive != Vector3.zero && agent.enabled && agent.isOnNavMesh && !agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance + 0.2f && (attacker == null || !attacker.HasTarget))
        {
            var look = Quaternion.LookRotation(faceOnArrive);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, agent.angularSpeed * Time.deltaTime);
            if (Quaternion.Angle(transform.rotation, look) < 1f) faceOnArrive = Vector3.zero;
        }

        if (boardTarget != null && agent.enabled) Board();

        if (garrisonTarget != null && agent.enabled)
        {
            float r = garrisonTarget.captureRadius;
            if ((garrisonTarget.transform.position - transform.position).sqrMagnitude > r * r) return;
            var outpost = garrisonTarget;
            garrisonTarget = null;
            if (outpost.TryGarrison(GetComponent<TeamMember>())) Deselect(); // hidden inside now
        }
    }

    void Board()
    {
        if (boardTarget.Full || !boardTarget.isActiveAndEnabled) { boardTarget = null; return; }
        if (boardTarget.InReach(this)) { var w = boardTarget; boardTarget = null; w.TryLoad(this); return; }
        if (Time.time > boardRepath && agent.isOnNavMesh) { boardRepath = Time.time + 0.5f; agent.SetDestination(boardTarget.transform.position); } // it may be moving
    }

    // Leave a building: the building carves a hole in the navmesh, so walk out by hand,
    // then hand control back to the agent. via: optional stop on the way, e.g. the edge of a raised floor.
    // thenGo: the building's rally point (harvesters ignore it and go mining).
    public void WalkOut(Vector3 from, Vector3 to, float speed, Vector3? via = null, Vector3? thenGo = null) =>
        StartCoroutine(WalkOutRoutine(from, to, speed, via, thenGo));

    IEnumerator WalkOutRoutine(Vector3 from, Vector3 to, float speed, Vector3? via, Vector3? thenGo)
    {
        agent.enabled = false;
        if (harvester != null) harvester.enabled = false; // it would try to drive off on its own
        transform.position = from;
        if (animator != null)
        {
            animator.SetFloat(SpeedHash, agent.speed);
            animator.speed = speed / agent.speed; // slower legs to match the slower walk
        }

        if (via is Vector3 v)
            while ((transform.position - v).sqrMagnitude > 0.0001f)
            {
                transform.position = Vector3.MoveTowards(transform.position, v, speed * Time.deltaTime);
                yield return null;
            }

        while ((transform.position - to).sqrMagnitude > 0.0001f)
        {
            transform.position = Vector3.MoveTowards(transform.position, to, speed * Time.deltaTime);
            yield return null;
        }

        if (animator != null) animator.speed = 1f;
        agent.enabled = true;
        agent.Warp(to);
        if (harvester != null) harvester.enabled = true;
        else if (thenGo is Vector3 rally && agent.isOnNavMesh) agent.SetDestination(rally);
    }

    public void Select(GoodSelectionManager selectionManager, int index)
    {
        if (manager != null) return;
        manager = selectionManager;
        formationIndex = index;
        manager.OnMoveCommand += HandleMoveCommand;
        manager.OnHarvestCommand += HandleHarvestCommand;
        manager.OnAttackCommand += HandleAttackCommand;
        manager.OnGarrisonCommand += HandleGarrisonCommand;
        manager.OnBoardCommand += HandleBoardCommand;
        manager.OnAttackMoveCommand += HandleAttackMoveCommand;
        manager.OnStopCommand += HandleStopCommand;
        manager.OnHoldCommand += HandleHoldCommand;
        if (selectionRing != null) selectionRing.SetActive(true);
    }

    public void HideRing() { if (selectionRing != null) selectionRing.SetActive(false); }

    public void Deselect()
    {
        Unsubscribe();
        manager = null;
        if (selectionRing != null) selectionRing.SetActive(false);
    }

    // Crowded spot (others already standing on or around it, e.g. a shared rally point): once a unit stops
    // getting any closer for a moment it stops where it is, instead of shoving the others forever.
    // The nearer it is, the sooner it gives up; units still far off keep trying for longer.
    Vector3 settleDest;
    float settleBest, settleSince;

    void Settle()
    {
        if (harvester != null || !agent.enabled || !agent.isOnNavMesh || agent.pathPending || !agent.hasPath) return;
        if (attacker != null && attacker.HasTarget) return; // chasing or fighting: the Attacker steers
        if (IsStunned) { settleSince = Time.time; return; } // frozen by EMP isn't "stuck"
        if ((agent.destination - settleDest).sqrMagnitude > 0.25f) // new order
        {
            settleDest = agent.destination;
            settleBest = float.MaxValue;
            settleSince = Time.time;
        }
        float left = agent.remainingDistance;
        if (float.IsInfinity(left)) return;
        if (left < settleBest - 0.2f) { settleBest = left; settleSince = Time.time; return; } // still making progress
        if (left < 20f && Time.time - settleSince > 0.6f + left * 0.15f) agent.ResetPath();
    }

    // Each unit only uses its own slot.
    void HandleMoveCommand(List<Vector3> slots)
    {
        if (formationIndex < 0 || formationIndex >= slots.Count) return;
        boardTarget = null;
        garrisonTarget = null;
        if (harvester != null) harvester.Stop(); // a plain move pauses mining
        if (attacker != null) attacker.Stop();   // and drops the current target
        if (agent.isOnNavMesh) agent.SetDestination(slots[formationIndex]);
        faceOnArrive = manager.Facing;
    }

    // A + click: armed units fight their way there; others just walk.
    void HandleAttackMoveCommand(List<Vector3> slots)
    {
        if (attacker == null) { HandleMoveCommand(slots); return; }
        if (formationIndex < 0 || formationIndex >= slots.Count) return;
        garrisonTarget = null;
        attacker.AttackMove(slots[formationIndex]);
        faceOnArrive = manager.Facing;
    }

    void HandleStopCommand()
    {
        garrisonTarget = null;
        boardTarget = null;
        faceOnArrive = Vector3.zero;
        if (harvester != null) harvester.Stop();
        if (attacker != null) attacker.Stop();
        if (agent.isOnNavMesh) agent.ResetPath();
    }

    void HandleHoldCommand()
    {
        HandleStopCommand();
        if (attacker != null) attacker.Hold();
    }

    // Right-clicked ore: workers go mine it; other units ignore it.
    void HandleHarvestCommand(OreNode ore)
    {
        if (harvester != null && harvester.enabled) harvester.OrderHarvest(ore);
    }

    // Right-clicked an enemy: armed units go shoot it.
    void HandleAttackCommand(Health target)
    {
        faceOnArrive = Vector3.zero;
        if (attacker != null) attacker.Attack(target);
    }

    // Right-clicked our Wasp: infantry-sized units go aboard once they reach it.
    void HandleBoardCommand(Transport wasp)
    {
        if (Transport.CanCarry(this) && !Flyer.IsAir(this)) boardTarget = wasp;
    }

    // Right-clicked our outpost: armed units go inside once they reach it (harvesters just walk there).
    void HandleGarrisonCommand(OutpostCapture outpost)
    {
        if (attacker != null) garrisonTarget = outpost;
    }

    void Unsubscribe()
    {
        if (manager == null) return;
        manager.OnGarrisonCommand -= HandleGarrisonCommand;
        manager.OnBoardCommand -= HandleBoardCommand;
        manager.OnMoveCommand -= HandleMoveCommand;
        manager.OnHarvestCommand -= HandleHarvestCommand;
        manager.OnAttackCommand -= HandleAttackCommand;
        manager.OnAttackMoveCommand -= HandleAttackMoveCommand;
        manager.OnStopCommand -= HandleStopCommand;
        manager.OnHoldCommand -= HandleHoldCommand;
    }

    void OnDestroy()
    {
        Unsubscribe();
        Stats.Changed -= RefreshStats;
    }
}
