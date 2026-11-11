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
    public float Charge01 => recharge <= 0f ? 1f : 1f - Mathf.Clamp01((ready - Time.time) / recharge); // 1 = armed
    public float LastPulse { get; private set; } = -99f;

    void Awake() => self = GetComponent<Health>();

    void Update()
    {
        if (Time.time < ready) return;
        float r2 = radius * radius;
        bool fired = false;
        foreach (var h in Health.All)
        {
            if (Net.Allied(h.team, self.team) || Flyer.IsAir(h) || !h.TryGetComponent(out SelectableUnit u)) continue; // flyers pass over mines
            if ((h.transform.position - transform.position).sqrMagnitude > r2) continue;
            u.Stun(stunTime);
            fired = true;
        }
        if (!fired) return;
        ready = Time.time + recharge;
        LastPulse = Time.time;
        Fx.Spawn(pulseEffect, transform.position, Quaternion.identity, radius);
        Sfx.At(Snd.Emp, transform.position, 0.9f);
        Fx.Spawn(ringEffect, transform.position + Vector3.up * 0.05f, Quaternion.identity, radius);
    }
}
