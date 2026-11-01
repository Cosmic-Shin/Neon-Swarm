using System.Collections.Generic;
using UnityEngine;

// Hit points + team. Dies (is destroyed) at 0.
public class Health : MonoBehaviour
{
    public static readonly List<Health> All = new(); // every living thing, for target searches

    [SerializeField] float maxHealth = 100f;
    public int team; // 0 = player, 1 = enemy

    float current;

    public float Fraction => current / maxHealth;
    public bool IsDamaged => current < maxHealth;
    public bool IsDead => current <= 0f;

    void Awake()
    {
        // Units take their max health from their UnitData; anything else uses the field.
        if (TryGetComponent(out SelectableUnit unit) && unit.Data != null)
        {
            maxHealth = unit.Data.maxHealth;
            if (team == 0) maxHealth *= Upgrades.Armor;
        }
        current = maxHealth;
        if (TryGetComponent(out TeamMember tm)) tm.team = team; // the pack's outpost capture counts TeamMembers
    }

    public System.Action onDeath; // set = called instead of destroying (outposts fall back to neutral)
    public void Revive() => current = maxHealth;
    public float MaxHealth => maxHealth;

    // Inside a friendly cloak field and the viewer isn't close enough to spot it.
    public bool CloakedFrom(Vector3 viewer) =>
        (transform.position - viewer).sqrMagnitude > CloakField.RevealRange * CloakField.RevealRange && CloakField.Covers(this);
    public float Current => current;
    public void Heal(float amount) => current = Mathf.Min(maxHealth, current + amount);
    public void ScaleMax(float k) { maxHealth *= k; current *= k; } // armor research

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    // from: who shot us, so an idle unit can shoot back.
    public void TakeDamage(float amount, Health from = null)
    {
        if (IsDead) return;
        current = Mathf.Max(0f, current - amount);
        if (team == 0 && from != null && from.team != team) Alerts.Attacked(this);
        if (IsDead) { Die(); return; }
        if (from != null && TryGetComponent(out Attacker attacker)) attacker.Retaliate(from);
    }

    void Die()
    {
        enabled = false; // leaves Health.All, so nobody keeps targeting the wreck
        if (onDeath != null) { onDeath(); return; }
        if (team == 0)
        {
            if (TryGetComponent(out Structure s)) Alerts.Show($"Building destroyed: {s.Name}", Alerts.Bad);
            else Alerts.Show("Unit lost", Alerts.Bad, 5f);
        }
        if (TryGetComponent(out SelectableUnit unit)) unit.HideRing(); // or it flies off as debris
        // Pack effect: neon flickers out, the model breaks into physics debris.
        if (TryGetComponent(out UnitDeath death)) death.Die();
        else Destroy(gameObject);
    }
}
