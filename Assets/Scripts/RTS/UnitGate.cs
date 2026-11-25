using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Health))]
public class UnitGate : MonoBehaviour
{
    public float radius = 8f;

    static readonly int OpenHash = Animator.StringToHash("Open");
    static readonly int CloseHash = Animator.StringToHash("Close");

    Health self;
    NavMeshObstacle obstacle;
    Animator anim;
    bool open;
    float next;

    void Awake()
    {
        self = GetComponent<Health>();
        obstacle = GetComponent<NavMeshObstacle>();
        anim = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (Time.time < next) return;
        next = Time.time + 0.25f;
        if (TryGetComponent(out Construction _) || self.IsDead) return;
        bool want = false;
        float r2 = radius * radius;
        foreach (var h in Health.All)
        {
            if (h == self || h.IsDead || !Net.Allied(h.team, self.team) || Flyer.IsAir(h) || !h.TryGetComponent(out SelectableUnit _)) continue;
            if ((h.transform.position - transform.position).sqrMagnitude < r2) { want = true; break; }
        }
        if (want == open) return;
        open = want;
        if (obstacle != null) obstacle.enabled = !open;
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            anim.ResetTrigger(open ? CloseHash : OpenHash);
            anim.SetTrigger(open ? OpenHash : CloseHash);
        }
        if (Juice.Seen(transform.position)) Sfx.At(open ? Snd.Unload : Snd.Load, transform.position, 0.3f);
    }
}
