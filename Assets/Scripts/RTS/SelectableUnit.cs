using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SelectableUnit : MonoBehaviour
{
    [SerializeField] GameObject selectionRing;
    [SerializeField] UnitData data;
    [SerializeField] Animator animator;

    static readonly int SpeedHash = Animator.StringToHash("Speed");

    public UnitData Data => data;
    public string DisplayName => data.displayName;
    public Sprite Icon => data.icon;
    public float BuildTime => data.buildTime;
    public int Cost => data.cost;
    public bool IsSelected => manager != null;
    public bool IsEnemy => health != null && health.team != 0;

    static readonly Color EnemyRing = new(1f, 0.1f, 0.15f);
    static readonly Color CreepRing = new(1f, 0.55f, 0.1f);
    Vector3 faceOnArrive;

    NavMeshAgent agent;
    Harvester harvester;
    Attacker attacker;
    Health health;
    GoodSelectionManager manager;
    int formationIndex = -1;
    OutpostCapture garrisonTarget;
    Transport boardTarget;
    float boardRepath;
    float stunnedUntil;

    public bool IsStunned => Time.time < stunnedUntil;
    public bool IsRemote { get; set; }
    Vector3 lastPos;
    float remoteSpeed;

    public void Stun(float seconds)
    {
        if (IsRemote) { GetComponent<NetEntity>().RPC_Status(seconds, 0f); return; }
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);
        if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
    }

    float slowedUntil;
    bool wasSlowed;
    public void Slow(float seconds)
    {
        if (IsRemote) { GetComponent<NetEntity>().RPC_Status(0f, seconds); return; }
        slowedUntil = Mathf.Max(slowedUntil, Time.time + seconds);
    }

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

    public void ApplyTeamLook()
    {
        if (!TryGetComponent(out UnitMarker marker)) { marker = gameObject.AddComponent<UnitMarker>(); marker.Init(selectionRing); }
        else marker.Recolor();
        if (selectionRing == null) return;
        selectionRing.SetActive(IsSelected);
        if (!IsEnemy) return;
        var mat = selectionRing.GetComponent<Renderer>().material;
        Color ring = health.team == Creep.Team ? CreepRing : Net.Online || Skirmish.Active ? Net.ColorOf(health.team) : EnemyRing;
        mat.color = ring;
        mat.SetColor("_EmissionColor", ring * 2f);
    }

    void RefreshStats()
    {
        if (harvester != null)
        {
            harvester.capacity = Stats.Value(Stat.HarvestCapacity, baseCapacity, this);
            harvester.harvestRate = Stats.Value(Stat.HarvestRate, baseHarvestRate, this);
            harvester.unloadTime = Stats.Value(Stat.UnloadTime, baseUnloadTime, this);
            return;
        }
        agent.speed = Stats.Value(Stat.MoveSpeed, baseSpeed, this) * (wasSlowed ? 0.5f : 1f) * boost;
    }

    float boost = 1f;
    public void SetBoost(float k)
    {
        if (harvester != null || Mathf.Approximately(k, boost)) return;
        boost = k;
        RefreshStats();
    }

    void Update()
    {
        if (IsRemote)
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            remoteSpeed = Mathf.Lerp(remoteSpeed, (transform.position - lastPos).magnitude / dt, 10f * dt);
            lastPos = transform.position;
            if (animator != null) animator.SetFloat(SpeedHash, remoteSpeed);
            return;
        }
        if (animator != null && agent.enabled) animator.SetFloat(SpeedHash, agent.velocity.magnitude);
        if (agent.enabled && agent.isOnNavMesh && agent.isStopped && !IsStunned) agent.isStopped = false;
        bool slowedNow = Time.time < slowedUntil;
        if (slowedNow != wasSlowed) { agent.speed *= slowedNow ? 0.5f : 2f; wasSlowed = slowedNow; }

        Settle();
        NextOrder();

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
            if (outpost.TryGarrison(GetComponent<TeamMember>())) Deselect();
        }
    }

    void Board()
    {
        if (boardTarget.Full || !boardTarget.isActiveAndEnabled) { boardTarget = null; return; }
        if (boardTarget.InReach(this)) { var w = boardTarget; boardTarget = null; w.TryLoad(this); return; }
        if (Time.time > boardRepath && agent.isOnNavMesh) { boardRepath = Time.time + 0.5f; agent.SetDestination(boardTarget.transform.position); }
    }

    public void WalkOut(Vector3 from, Vector3 to, float speed, Vector3? via = null, Vector3? thenGo = null) =>
        StartCoroutine(WalkOutRoutine(from, to, speed, via, thenGo));

    IEnumerator WalkOutRoutine(Vector3 from, Vector3 to, float speed, Vector3? via, Vector3? thenGo)
    {
        agent.enabled = false;
        if (harvester != null) harvester.enabled = false;
        transform.position = from;
        if (animator != null)
        {
            animator.SetFloat(SpeedHash, agent.speed);
            animator.speed = speed / agent.speed;
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
        manager.OnPatrolCommand += HandlePatrolCommand;
        if (selectionRing != null) { selectionRing.SetActive(true); if (isActiveAndEnabled) StartCoroutine(PopRing()); }
    }

    Vector3? ringScale;
    IEnumerator PopRing()
    {
        var t = selectionRing.transform;
        ringScale ??= t.localScale;
        for (float e = 0f; e < 0.3f; e += Time.deltaTime)
        {
            float k = e / 0.3f;
            t.localScale = ringScale.Value * (1f + 0.6f * (1f - k) * Mathf.Cos(k * Mathf.PI * 1.5f));
            yield return null;
        }
        t.localScale = ringScale.Value;
    }

    public void HideRing() { if (selectionRing != null) selectionRing.SetActive(false); }

    public void Deselect()
    {
        Unsubscribe();
        manager = null;
        if (selectionRing != null) selectionRing.SetActive(false);
    }

    Vector3 settleDest;
    float settleBest, settleSince;

    void Settle()
    {
        if (harvester != null || !agent.enabled || !agent.isOnNavMesh || agent.pathPending || !agent.hasPath) return;
        if (attacker != null && attacker.HasTarget) return;
        if (IsStunned) { settleSince = Time.time; return; }
        if ((agent.destination - settleDest).sqrMagnitude > 0.25f)
        {
            settleDest = agent.destination;
            settleBest = float.MaxValue;
            settleSince = Time.time;
        }
        float left = agent.remainingDistance;
        if (float.IsInfinity(left)) return;
        if (left < settleBest - 0.2f) { settleBest = left; settleSince = Time.time; return; }
        if (left < 20f && Time.time - settleSince > 0.6f + left * 0.15f) agent.ResetPath();
    }

    struct Order { public Vector3 at; public bool attack; }
    readonly List<Order> queue = new();
    bool patrol;
    LineRenderer pathLine;

    bool Queued(Vector3 at, bool attack)
    {
        if (manager == null || !manager.Queueing || Idle()) { queue.Clear(); patrol = false; return false; }
        if (patrol) { queue.Clear(); patrol = false; }
        queue.Add(new Order { at = at, attack = attack });
        return true;
    }

    bool Idle() =>
        agent.enabled && agent.isOnNavMesh && !agent.pathPending &&
        (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.6f) &&
        (attacker == null || !attacker.Busy) && garrisonTarget == null && boardTarget == null;

    void Go(Order o)
    {
        faceOnArrive = Vector3.zero;
        if (o.attack && attacker != null) attacker.AttackMove(o.at);
        else
        {
            if (attacker != null) attacker.Stop();
            if (agent.isOnNavMesh) agent.SetDestination(o.at);
        }
    }

    void NextOrder()
    {
        if (queue.Count > 0 && Idle())
        {
            var o = queue[0];
            queue.RemoveAt(0);
            if (patrol) queue.Add(o);
            Go(o);
        }
        ShowPath();
    }

    void ShowPath()
    {
        bool show = IsSelected && queue.Count > 0;
        if (pathLine == null)
        {
            if (!show) return;
            pathLine = new GameObject("OrderPath").AddComponent<LineRenderer>();
            pathLine.sharedMaterial = Juice.Additive;
            pathLine.widthMultiplier = 0.12f;
            pathLine.useWorldSpace = true;
            pathLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        pathLine.enabled = show;
        if (!show) return;
        Color c = patrol ? Juice.Gold : queue[0].attack ? Juice.Danger : new Color(0.3f, 1f, 1f);
        pathLine.startColor = pathLine.endColor = new Color(c.r, c.g, c.b, 0.55f);
        pathLine.positionCount = queue.Count + 2;
        Vector3 up = Vector3.up * 0.25f;
        pathLine.SetPosition(0, transform.position + up);
        pathLine.SetPosition(1, (agent.hasPath ? agent.destination : transform.position) + up);
        for (int i = 0; i < queue.Count; i++) pathLine.SetPosition(i + 2, queue[i].at + up);
    }

    void HandlePatrolCommand(List<Vector3> slots)
    {
        if (formationIndex < 0 || formationIndex >= slots.Count) return;
        garrisonTarget = null;
        boardTarget = null;
        if (harvester != null) harvester.Stop();
        var there = new Order { at = slots[formationIndex], attack = true };
        var here = new Order { at = transform.position, attack = true };
        queue.Clear();
        queue.Add(here);
        queue.Add(there);
        patrol = true;
        Go(there);
    }

    void HandleMoveCommand(List<Vector3> slots)
    {
        if (formationIndex < 0 || formationIndex >= slots.Count) return;
        if (Queued(slots[formationIndex], false)) return;
        boardTarget = null;
        garrisonTarget = null;
        if (harvester != null) harvester.Stop();
        if (attacker != null) attacker.Stop();
        if (agent.isOnNavMesh) agent.SetDestination(slots[formationIndex]);
        faceOnArrive = manager.Facing;
    }

    void HandleAttackMoveCommand(List<Vector3> slots)
    {
        if (attacker == null) { HandleMoveCommand(slots); return; }
        if (formationIndex < 0 || formationIndex >= slots.Count) return;
        if (Queued(slots[formationIndex], true)) return;
        garrisonTarget = null;
        attacker.AttackMove(slots[formationIndex]);
        faceOnArrive = manager.Facing;
    }

    void HandleStopCommand()
    {
        queue.Clear();
        patrol = false;
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

    void HandleHarvestCommand(OreNode ore)
    {
        if (harvester != null && harvester.enabled) harvester.OrderHarvest(ore);
    }

    void HandleAttackCommand(Health target)
    {
        queue.Clear();
        patrol = false;
        faceOnArrive = Vector3.zero;
        if (attacker != null) attacker.Attack(target);
    }

    void HandleBoardCommand(Transport wasp)
    {
        if (Transport.CanCarry(this) && !Flyer.IsAir(this)) boardTarget = wasp;
    }

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
        manager.OnPatrolCommand -= HandlePatrolCommand;
    }

    void OnDestroy()
    {
        if (pathLine != null) Destroy(pathLine.gameObject);
        Unsubscribe();
        Stats.Changed -= RefreshStats;
    }
}
