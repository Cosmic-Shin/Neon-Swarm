using UnityEngine;
using UnityEngine.AI;

// Neutral guard at a red ore zone: fights anyone who comes close, never strays far from its post,
// and heals back up once it's home and left alone. Its own team, so it fights players and AI alike.
[RequireComponent(typeof(Attacker))]
public class Creep : MonoBehaviour
{
    public const int Team = 9;

    [SerializeField] float guardRadius = 24f; // chases no further than this from home
    [SerializeField] float regen = 0.04f;     // fraction of max health per second while home and idle

    Vector3 home;
    NavMeshAgent agent;
    Attacker attacker;
    Health health;
    bool returning;

    void Awake()
    {
        home = transform.position;
        agent = GetComponent<NavMeshAgent>();
        attacker = GetComponent<Attacker>();
        health = GetComponent<Health>();
        Destroy(GetComponent<TeamMember>()); // so it can't capture or contest outposts
    }

    void Update()
    {
        float d2 = (transform.position - home).sqrMagnitude;
        // Too far, or done fighting somewhere off its post: walk home (ignoring shots on the way).
        if (!returning && agent.isOnNavMesh && (d2 > guardRadius * guardRadius || (!attacker.HasTarget && d2 > 9f)))
        {
            returning = true;
            attacker.Stop();
            agent.SetDestination(home);
        }
        if (returning) { if (d2 < 9f) returning = false; else return; }
        if (!attacker.HasTarget && health.IsDamaged) health.Heal(regen * health.MaxHealth * Time.deltaTime);
    }
}
