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

    NavMeshAgent agent;
    Harvester harvester; // only on workers
    Attacker attacker;   // only on armed units
    Health health;
    GoodSelectionManager manager;
    int formationIndex = -1;
    OutpostCapture garrisonTarget; // walking to our outpost to go inside
    float stunnedUntil;

    // EMP: can't move or shoot for a while.
    public bool IsStunned => Time.time < stunnedUntil;
    public void Stun(float seconds)
    {
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);
        if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
    }

    // Wire snare: half speed for a while.
    float slowedUntil;
    bool wasSlowed;
    public void Slow(float seconds) => slowedUntil = Mathf.Max(slowedUntil, Time.time + seconds);

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (data != null && data.moveSpeed > 0f) agent.speed = data.moveSpeed;
        harvester = GetComponent<Harvester>();
        attacker = GetComponent<Attacker>();
        health = GetComponent<Health>();
        if (harvester != null && !IsEnemy) harvester.harvestRate *= Upgrades.Harvest; // research
        if (selectionRing != null) selectionRing.SetActive(IsEnemy);
        if (IsEnemy && selectionRing != null)
        {
            var mat = selectionRing.GetComponent<Renderer>().material; // per-enemy copy
            mat.color = EnemyRing;
            mat.SetColor("_EmissionColor", EnemyRing * 2f);
        }
    }

    void Update()
    {
        if (animator != null && agent.enabled) animator.SetFloat(SpeedHash, agent.velocity.magnitude);
        if (agent.enabled && agent.isOnNavMesh && agent.isStopped && !IsStunned) agent.isStopped = false; // stun wore off
        bool slowedNow = Time.time < slowedUntil;
        if (slowedNow != wasSlowed) { agent.speed *= slowedNow ? 0.5f : 2f; wasSlowed = slowedNow; } // only on change (harvesters set their own speed)

        if (garrisonTarget != null && agent.enabled)
        {
            float r = garrisonTarget.captureRadius;
            if ((garrisonTarget.transform.position - transform.position).sqrMagnitude > r * r) return;
            var outpost = garrisonTarget;
            garrisonTarget = null;
            if (outpost.TryGarrison(GetComponent<TeamMember>())) Deselect(); // hidden inside now
        }
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
        if (selectionRing != null) selectionRing.SetActive(true);
    }

    public void HideRing() { if (selectionRing != null) selectionRing.SetActive(false); }

    public void Deselect()
    {
        Unsubscribe();
        manager = null;
        if (selectionRing != null) selectionRing.SetActive(false);
    }

    // Each unit only uses its own slot.
    void HandleMoveCommand(List<Vector3> slots)
    {
        if (formationIndex < 0 || formationIndex >= slots.Count) return;
        garrisonTarget = null;
        if (harvester != null) harvester.Stop(); // a plain move pauses mining
        if (attacker != null) attacker.Stop();   // and drops the current target
        if (agent.isOnNavMesh) agent.SetDestination(slots[formationIndex]);
    }

    // Right-clicked ore: workers go mine it; other units ignore it.
    void HandleHarvestCommand(OreNode ore)
    {
        if (harvester != null && harvester.enabled) harvester.OrderHarvest(ore);
    }

    // Right-clicked an enemy: armed units go shoot it.
    void HandleAttackCommand(Health target)
    {
        if (attacker != null) attacker.Attack(target);
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
        manager.OnMoveCommand -= HandleMoveCommand;
        manager.OnHarvestCommand -= HandleHarvestCommand;
        manager.OnAttackCommand -= HandleAttackCommand;
    }

    void OnDestroy() => Unsubscribe();
}
