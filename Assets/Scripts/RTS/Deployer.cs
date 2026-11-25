using UnityEngine;
using UnityEngine.AI;

public class Deployer : MonoBehaviour
{
    public float deployTime = 20f / 24f;
    public float undeployTime = 20f / 24f;

    enum State { Mobile, Deploying, Deployed, Undeploying }
    State state;
    float until;
    NavMeshAgent agent;
    Attacker attacker;
    SelectableUnit unit;
    Animator anim;

    public bool Ready => state == State.Deployed;
    public bool Deployed => state is State.Deployed or State.Deploying;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        attacker = GetComponent<Attacker>();
        unit = GetComponent<SelectableUnit>();
        anim = GetComponentInChildren<Animator>();
    }

    bool WantsToMove => agent.enabled && agent.isOnNavMesh && agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.5f;

    void Trigger(string name)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return;
        foreach (var p in anim.parameters) if (p.name == name) { anim.SetTrigger(name); return; }
    }

    void Hold(bool on)
    {
        if (agent.enabled && agent.isOnNavMesh && (on || !unit.IsStunned)) agent.isStopped = on;
    }

    void Update()
    {
        switch (state)
        {
            case State.Mobile:
                if (attacker != null && attacker.HasTarget && !WantsToMove)
                {
                    state = State.Deploying;
                    until = Time.time + deployTime;
                    Hold(true);
                    Trigger("Deploy");
                    Sfx.At(Snd.Weld, transform.position, 0.35f);
                }
                break;
            case State.Deploying:
                if (Time.time >= until) state = State.Deployed;
                break;
            case State.Deployed:
                if (WantsToMove || attacker == null || !attacker.HasTarget)
                {
                    state = State.Undeploying;
                    until = Time.time + undeployTime;
                    Trigger("Undeploy");
                }
                break;
            case State.Undeploying:
                if (Time.time >= until) { state = State.Mobile; Hold(false); }
                break;
        }
    }
}
