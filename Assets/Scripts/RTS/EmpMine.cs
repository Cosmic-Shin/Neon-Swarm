using UnityEngine;

[RequireComponent(typeof(Health))]
public class EmpMine : MonoBehaviour
{
    [SerializeField] float radius = 9f;
    [SerializeField] float stunTime = 4f;
    [SerializeField] float recharge = 15f;
    [SerializeField] GameObject pulseEffect;
    [SerializeField] GameObject ringEffect;

    Health self;
    float ready;
    public float Charge01 => recharge <= 0f ? 1f : 1f - Mathf.Clamp01((ready - Time.time) / recharge);
    public float LastPulse { get; private set; } = -99f;

    void Awake() => self = GetComponent<Health>();

    void Update()
    {
        if (Time.time < ready) return;
        float r2 = radius * radius;
        bool fired = false;
        foreach (var h in Health.All)
        {
            if (Net.Allied(h.team, self.team) || Flyer.IsAir(h) || !h.TryGetComponent(out SelectableUnit u)) continue;
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
