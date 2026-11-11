using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// C&C-style worker: finds ore -> drives there -> mines until full -> returns to the nearest refinery ->
/// docks and unloads -> credits are added to the ResourceBank -> repeats.
/// Uses a NavMeshAgent if one is present and on a NavMesh, otherwise drives in a straight line.
/// </summary>
public class Harvester : MonoBehaviour
{
    public enum State { Idle, ToOre, Harvesting, ToRefinery, WaitingForDock, Unloading }

    [Header("Economy")]
    public float capacity = 100f;
    public float harvestRate = 20f;
    public float unloadTime = 2f;
    public bool autoHarvest = true;

    [Header("Movement")]
    public float moveSpeed = 2.2f;
    public float turnSpeed = 300f;
    [Tooltip("Set to 180 if the model drives backwards in your project.")]
    public float modelYawOffset = 0f;

    [Header("Debug (read only)")]
    public State state = State.Idle;
    public float cargo;
    float cargoValue; // cargo weighted by each ore's valueMultiplier

    Animator anim;
    NavMeshAgent agent;
    Transform cargoFill; Vector3 fillScale; int fillAxis = 1;
    OreNode ore; Refinery refinery; float timer;
    static readonly int SpeedId = Animator.StringToHash("Speed");

    public OreNode TargetOre => ore;
    int Team => TeamEconomy.TeamOf(this); // GD5: whose harvester (refinery, danger check, credits)

    void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        if (!GetComponent<HarvestFX>()) gameObject.AddComponent<HarvestFX>();
        agent = GetComponent<NavMeshAgent>();
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == "Harvester_CargoFill") { cargoFill = t; break; }
        if (cargoFill)
        {
            fillScale = cargoFill.localScale;
            Vector3 up = cargoFill.parent ? cargoFill.parent.InverseTransformDirection(Vector3.up) : Vector3.up;
            up = new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z));
            fillAxis = up.x > up.y && up.x > up.z ? 0 : (up.y >= up.z ? 1 : 2);
        }
        SetCargoVisual(cargo);
    }

    void OnDisable()
    {
        if (refinery) refinery.Release(this);
        LeaveOre();
    }

    float nextDangerCheck;

    void Update()
    {
        // Enemies showed up at our ore: bank what we have, or move to a safer field.
        if ((state == State.ToOre || state == State.Harvesting) && ore && OreNode.IsDangerous != null && Time.time >= nextDangerCheck)
        {
            nextDangerCheck = Time.time + 0.5f;
            if (OreNode.IsDangerous(ore.transform.position, Team)) { if (cargo > capacity * 0.3f) GoRefinery(); else PickOre(); }
        }

        switch (state)
        {
            case State.Idle:
                if (!autoHarvest) break;
                SetSpeed(0);
                if (Time.time < nextDangerCheck) break; // every ore may be unsafe: don't search every frame
                nextDangerCheck = Time.time + 0.5f;
                Decide();
                break;

            case State.ToOre:
                if (!ore || ore.IsDepleted) { PickOre(); break; }
                if (MoveTo(ore.transform.position, ore.harvestRadius))
                {
                    Face(ore.transform.position);
                    state = State.Harvesting;
                    Play("Harvest");
                }
                break;

            case State.Harvesting:
                if (!ore || ore.IsDepleted)
                {
                    if (cargo >= capacity * 0.6f) GoRefinery(); else PickOre();
                    break;
                }
                float got = ore.Take(Mathf.Min(harvestRate * Time.deltaTime, capacity - cargo));
                cargo += got;
                cargoValue += got * ore.valueMultiplier;
                SetCargoVisual(cargo);
                if (cargo >= capacity - 0.01f) GoRefinery();
                break;

            case State.ToRefinery:
                if (!refinery) { GoRefinery(); break; }
                if (!refinery.TryDock(this)) { state = State.WaitingForDock; break; }
                if (MoveTo(refinery.DockPositionOf(this), 0.12f))
                {
                    transform.rotation = refinery.DockFacing * Quaternion.Euler(0, modelYawOffset, 0);
                    timer = 0f;
                    state = State.Unloading;
                    Play("Unload");
                }
                break;

            case State.WaitingForDock:
                if (!refinery) { GoRefinery(); break; }
                if (refinery.TryDock(this)) { state = State.ToRefinery; break; }
                if (MoveTo(refinery.QueuePosition(this), 0.25f)) SetSpeed(0);
                break;

            case State.Unloading:
                if (!refinery) { GoRefinery(); break; }
                timer += Time.deltaTime;
                float unloadFor = unloadTime * refinery.unloadTimeMultiplier; // GD5: the Core Nexus unloads slowly
                SetCargoVisual(cargo * (1f - timer / unloadFor));
                if (timer >= unloadFor)
                {
                    int earned = Mathf.RoundToInt(cargoValue * refinery.creditsPerUnit);
                    TeamEconomy.Earn(Team, earned); // GD5: single-player AIs earn their own credits
                    if (Team == 0)
                    {
                        MatchStats.Add(MatchStats.Kind.Earned, earned);
                        if (TryGetComponent(out HarvestFX fx)) fx.Delivered(earned); // GD5: "+$N" over the refinery
                        Sfx.At(Snd.Deposit, transform.position, 0.5f); // GD5: a little "ka-ching"
                    }
                    cargo = 0f; cargoValue = 0f;
                    SetCargoVisual(0f);
                    refinery.Release(this);
                    Play("Idle");
                    PickOre();
                }
                break;
        }
    }

    // ---------- decisions ----------
    void Decide()
    {
        if (cargo >= capacity * 0.95f) GoRefinery(); else PickOre();
    }

    void PickOre()
    {
        LeaveOre();
        ore = OreNode.FindNearest(transform.position, Team);
        if (ore)
        {
            ore.ActiveHarvesters++;
            Play("Idle");
            state = State.ToOre;
        }
        else if (cargo > 0f) GoRefinery();
        else state = State.Idle;
    }

    void GoRefinery()
    {
        LeaveOre();
        refinery = Refinery.FindNearest(transform.position, Team);
        Play("Idle");
        state = refinery ? State.ToRefinery : State.Idle;
    }

    void LeaveOre()
    {
        if (ore) ore.ActiveHarvesters = Mathf.Max(0, ore.ActiveHarvesters - 1);
        ore = null;
    }

    /// <summary>Player gave a plain move order: stop the harvest loop until ordered back to ore.</summary>
    public void Stop()
    {
        if (refinery) refinery.Release(this);
        LeaveOre();
        autoHarvest = false;
        if (agent) agent.stoppingDistance = 0f;
        Play("Idle");
        state = State.Idle;
    }

    /// <summary>Order the harvester to a specific ore patch (e.g. from a right-click).</summary>
    public void OrderHarvest(OreNode target)
    {
        if (!target) return;
        autoHarvest = true;
        if (refinery) refinery.Release(this);
        LeaveOre();
        ore = target; ore.ActiveHarvesters++;
        Play("Idle");
        state = State.ToOre;
    }

    // ---------- movement ----------
    bool MoveTo(Vector3 target, float stopDistance)
    {
        Vector3 to = target - transform.position; to.y = 0f;
        float dist = to.magnitude;
        if (dist <= stopDistance)
        {
            SetSpeed(0);
            if (agent && agent.isOnNavMesh) agent.ResetPath();
            return true;
        }
        if (agent && agent.isOnNavMesh)
        {
            agent.speed = moveSpeed;
            agent.stoppingDistance = stopDistance;
            agent.SetDestination(target);
            SetSpeed(agent.velocity.magnitude);
            return !agent.pathPending && agent.remainingDistance <= stopDistance;
        }
        Vector3 dir = to / dist;
        Quaternion want = Quaternion.LookRotation(dir) * Quaternion.Euler(0, modelYawOffset, 0);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
        Vector3 fwd = transform.rotation * Quaternion.Euler(0, -modelYawOffset, 0) * Vector3.forward;
        float facing = Mathf.Clamp01(Vector3.Dot(fwd, dir));
        transform.position += dir * Mathf.Min(moveSpeed * facing * Time.deltaTime, dist);
        SetSpeed(facing);
        return false;
    }

    void Face(Vector3 p)
    {
        Vector3 d = p - transform.position; d.y = 0;
        if (d.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(d) * Quaternion.Euler(0, modelYawOffset, 0);
    }

    // ---------- visuals ----------
    void SetSpeed(float v) { if (anim) anim.SetFloat(SpeedId, v > 0.05f ? 1f : 0f); }

    // Multiplayer: the owner publishes these; other PCs replay them with ShowRemote.
    static readonly string[] AnimStates = { "Idle", "Harvest", "Unload" };
    public int AnimIndex { get; private set; }
    public float CargoFraction { get; private set; }

    public void ShowRemote(int animIndex, float cargoFraction)
    {
        if (animIndex != AnimIndex && animIndex >= 0 && animIndex < AnimStates.Length) Play(AnimStates[animIndex]);
        if (!Mathf.Approximately(cargoFraction, CargoFraction)) SetCargoVisual(cargoFraction * capacity);
    }

    void Play(string stateName)
    {
        AnimIndex = System.Array.IndexOf(AnimStates, stateName);
        if (anim && anim.HasState(0, Animator.StringToHash(stateName))) anim.CrossFadeInFixedTime(stateName, 0.2f);
    }

    void SetCargoVisual(float c)
    {
        float f = Mathf.Clamp01(c / capacity);
        CargoFraction = f;
        if (!cargoFill) return;
        cargoFill.gameObject.SetActive(f > 0.01f);
        Vector3 s = fillScale; s[fillAxis] = fillScale[fillAxis] * Mathf.Max(0.02f, f);
        cargoFill.localScale = s;
    }
}
