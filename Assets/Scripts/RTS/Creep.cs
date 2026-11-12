using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Attacker))]
public class Creep : MonoBehaviour
{
    public const int Team = 9;

    [SerializeField] float guardRadius = 24f;
    [SerializeField] float regen = 0.04f;

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
        Destroy(GetComponent<TeamMember>());
    }

    void Update()
    {
        float d2 = (transform.position - home).sqrMagnitude;
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
