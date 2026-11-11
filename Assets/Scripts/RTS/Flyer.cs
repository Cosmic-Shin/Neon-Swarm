using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Air unit. Orders still go through the NavMeshAgent (move, attack, formations all work as for ground units),
// but this flies the unit there in a straight line, over buildings and cliffs, instead of walking the navmesh path.
// The model hovers `hover` metres up (on the Model child), bobs, and banks into turns.
// Also answers "is this in the air?" for targeting: who can shoot it and where to aim.
[RequireComponent(typeof(NavMeshAgent))]
public class Flyer : MonoBehaviour
{
    public float hover = 6f; // model height above the ground point
    public float size = 2f;  // radius: spacing between flyers, formation room, weapon reach

    static readonly List<Flyer> all = new();
    static readonly int SpeedHash = Animator.StringToHash("Speed");

    NavMeshAgent agent;
    Transform model;
    Animator anim;
    float bank, seed, speedNow;

    public static bool IsAir(Component c) => c != null && c.TryGetComponent(out Flyer _);
    public static float HeightOf(Component c) => c != null && c.TryGetComponent(out Flyer f) ? f.hover : 0f;
    public static float SizeOf(Component c) => c != null && c.TryGetComponent(out Flyer f) ? f.size : 0f;
    // Where the unit visibly is: the model in the air, the body on the ground.
    public static Vector3 Center(Component c) => c.transform.position + Vector3.up * HeightOf(c);

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updatePosition = false; // the agent only plans; we move
        agent.updateRotation = false;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        model = transform.Find("Model");
        anim = GetComponentInChildren<Animator>();
        seed = Random.value * 10f;
    }

    // Engine trail: a soft streak in its side's colour behind the model (team known by now).
    void Start()
    {
        if (model == null) return;
        var t = model.gameObject.AddComponent<TrailRenderer>();
        t.sharedMaterial = Juice.AdditiveLine;
        t.time = 0.45f;
        t.minVertexDistance = 0.3f;
        t.widthMultiplier = Mathf.Max(0.3f, size * 0.12f);
        t.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Color c = TryGetComponent(out Health h) ? Net.ColorOf(h.team) : Color.cyan;
        if (c == Color.white) c = Color.cyan;
        t.startColor = new Color(c.r, c.g, c.b, 0.55f);
        t.endColor = new Color(c.r, c.g, c.b, 0f);
    }

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    void LateUpdate() // after SelectableUnit / Attacker gave their orders this frame
    {
        float dt = Time.deltaTime;
        float turn = 0f;
        speedNow = 0f;
        if (agent.enabled && agent.isOnNavMesh) // off while launching from the Aerie or moved by the network
        {
            Vector3 pos = transform.position;
            if (!agent.isStopped && (agent.hasPath || agent.pathPending))
            {
                Vector3 to = agent.destination - pos; to.y = 0f;
                float dist = to.magnitude;
                if (dist < 0.3f) agent.ResetPath(); // arrived
                else
                {
                    Vector3 dir = to / dist;
                    var want = Quaternion.LookRotation(dir);
                    var next = Quaternion.RotateTowards(transform.rotation, want, agent.angularSpeed * 0.5f * dt);
                    turn = Mathf.DeltaAngle(transform.eulerAngles.y, next.eulerAngles.y) / Mathf.Max(dt, 1e-4f);
                    transform.rotation = next;
                    // Slow down to land on the spot instead of overshooting it.
                    speedNow = Mathf.Min(agent.speed, dist * 2f);
                    pos += dir * Mathf.Min(speedNow * dt, dist);
                }
            }
            pos += Separation() * dt;
            transform.position = pos;
            agent.nextPosition = pos; // keep the planner where we are (it clamps to the navmesh under buildings)
        }

        if (model != null) // bob and bank
        {
            bank = Mathf.Lerp(bank, Mathf.Clamp(-turn * 0.15f, -25f, 25f), 4f * dt);
            model.localPosition = new Vector3(0f, hover + Mathf.Sin(Time.time * 1.3f + seed) * hover * 0.03f, 0f);
            model.localRotation = Quaternion.Euler(0f, 0f, bank);
        }
        if (anim != null && agent.enabled) anim.SetFloat(SpeedHash, speedNow);
    }

    // Flyers drift apart instead of stacking inside each other.
    // ponytail: every flyer checks every other one; fine for dozens, use a grid if air armies get huge.
    Vector3 Separation()
    {
        Vector3 push = Vector3.zero;
        foreach (var f in all)
        {
            if (f == this) continue;
            Vector3 d = transform.position - f.transform.position; d.y = 0f;
            float room = (size + f.size) * 0.8f, sqr = d.sqrMagnitude;
            if (sqr >= room * room) continue;
            float dist = Mathf.Sqrt(sqr);
            push += (dist > 0.01f ? d / dist : Random.insideUnitSphere) * (room - dist) * 2f;
        }
        push.y = 0f;
        return push;
    }
}
