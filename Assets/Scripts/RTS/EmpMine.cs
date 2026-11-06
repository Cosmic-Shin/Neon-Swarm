using UnityEngine;

// EMP Mine Field: when enemy units come within radius it pulses, stunning every enemy unit in range
// (can't move or shoot), then recharges. Needs no power.
[RequireComponent(typeof(Health))]
public class EmpMine : MonoBehaviour
{
    [SerializeField] float radius = 9f;
    [SerializeField] float stunTime = 4f;
    [SerializeField] float recharge = 15f;
    [SerializeField] GameObject pulseEffect; // Fx, sized to the radius (pulse wall)
    [SerializeField] GameObject ringEffect;  // Fx, sized to the radius (dashed ring marking the area)

    Health self;
    float ready;

    void Awake() => self = GetComponent<Health>();

    void Update()
    {
        if (Time.time < ready) return;
        float r2 = radius * radius;
        bool fired = false;
        foreach (var h in Health.All)
        {
            if (Net.Allied(h.team, self.team) || !h.TryGetComponent(out SelectableUnit u)) continue;
            if ((h.transform.position - transform.position).sqrMagnitude > r2) continue;
            u.Stun(stunTime);
            fired = true;
        }
        if (!fired) return;
        ready = Time.time + recharge;
        Fx.Spawn(pulseEffect, transform.position, Quaternion.identity, radius);
        Fx.Spawn(ringEffect, transform.position + Vector3.up * 0.05f, Quaternion.identity, radius);
    }
}
