using UnityEngine;

// Field medic (Patchbot): slowly heals damaged friendly units nearby, not itself.
[RequireComponent(typeof(Health))]
public class Repairer : MonoBehaviour
{
    [SerializeField] float range = 10f;
    [SerializeField] float healPerSecond = 8f; // per unit in range

    Health self;

    void Awake() => self = GetComponent<Health>();

    void Update()
    {
        float r2 = range * range;
        float heal = Stats.Value(Stat.RepairRate, healPerSecond, this) * Time.deltaTime; // Nano-Welders
        foreach (var h in Health.All)
            if (h != self && h.team == self.team && h.IsDamaged && h.TryGetComponent(out SelectableUnit _)
                && (h.transform.position - transform.position).sqrMagnitude <= r2)
                h.Heal(heal);
    }
}
