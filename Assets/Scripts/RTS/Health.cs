using System.Collections.Generic;
using UnityEngine;

// Hit points + team. Dies (is destroyed) at 0.
public class Health : MonoBehaviour
{
    public static readonly List<Health> All = new(); // every living thing, for target searches

    [SerializeField] float maxHealth = 100f;
    public int team; // 0 = player, 1 = enemy

    float baseMax; // before research / upgrades / veterancy
    float current;
    NetEntity net; // multiplayer: another player's copy forwards damage to its owner

    public float Fraction => current / maxHealth;
    public bool IsDamaged => current < maxHealth;
    public bool IsDead => current <= 0f;

    void Awake()
    {
        // Units take their max health from their UnitData; anything else uses the field.
        if (TryGetComponent(out SelectableUnit unit) && unit.Data != null) maxHealth = unit.Data.maxHealth;
        baseMax = maxHealth;
        current = maxHealth;
        net = GetComponent<NetEntity>();
        if (TryGetComponent(out TeamMember tm)) tm.team = team; // the pack's outpost capture counts TeamMembers
        Stats.Changed += RefreshMax;
        RefreshMax();
    }

    void OnDestroy() => Stats.Changed -= RefreshMax;

    // Bonuses changed: keep the same fraction of a new max.
    void RefreshMax()
    {
        float max = Stats.Value(Stat.MaxHealth, baseMax, this);
        if (Mathf.Approximately(max, maxHealth)) return;
        current *= max / maxHealth;
        maxHealth = max;
    }

    public System.Action onDeath; // set = called instead of destroying (outposts fall back to neutral)
    public void Revive() => current = maxHealth;
    public float MaxHealth => maxHealth;

    // Inside a friendly cloak field, the viewer isn't close enough to spot it, and no enemy Uplink Mk II reveals it.
    public bool CloakedFrom(Vector3 viewer) =>
        (transform.position - viewer).sqrMagnitude > CloakField.RevealRange * CloakField.RevealRange && CloakField.Covers(this) && !Revealer.Reveals(this);
    public float Current => current;
    public void Heal(float amount) => current = Mathf.Min(maxHealth, current + amount);

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    // Multiplayer, someone else's object: its owner sends health and death.
    public void SetFromNet(float hp, float max)
    {
        if (max > 0f) maxHealth = max;
        current = hp;
    }

    public void DieRemote()
    {
        enabled = false;
        if (TryGetComponent(out SelectableUnit unit)) unit.HideRing();
        if (TryGetComponent(out UnitDeath death)) death.Die(); // just the look; the owner despawns it
    }

    // from: who shot us, so an idle unit can shoot back (and gets the kill).
    public void TakeDamage(float amount, Health from = null)
    {
        if (net != null && net.IsRemote) { if (!net.IsDead) net.RPC_Damage(amount, NetEntity.IdOf(from)); return; } // owner applies it
        if (IsDead) return;
        current = Mathf.Max(0f, current - amount);
        if (team == 0 && from != null && from.team != team) Alerts.Attacked(this);
        if (IsDead) { Die(from); return; }
        if (from != null && TryGetComponent(out Attacker attacker)) attacker.Retaliate(from);
    }

    void Die(Health killer)
    {
        enabled = false; // leaves Health.All, so nobody keeps targeting the wreck
        if (net != null) net.MarkDead(); // multiplayer: everyone plays the death
        if (killer != null && killer.team != team && killer.TryGetComponent(out SelectableUnit _)) // veterancy: units only
        {
            if (killer.TryGetComponent(out NetEntity kn) && kn.IsRemote) kn.RPC_AddKill(); // another player's unit
            else
            {
                if (!killer.TryGetComponent(out Veterancy vet)) vet = killer.gameObject.AddComponent<Veterancy>();
                vet.AddKill();
            }
        }
        if (onDeath != null) { onDeath(); return; }
        if (team == 0)
        {
            if (TryGetComponent(out Structure s)) Alerts.Show($"Building destroyed: {s.Name}", Alerts.Bad);
            else Alerts.Show("Unit lost", Alerts.Bad, 5f);
        }
        if (TryGetComponent(out SelectableUnit unit)) unit.HideRing(); // or it flies off as debris
        // Pack effect: neon flickers out, the model breaks into physics debris.
        if (TryGetComponent(out UnitDeath death)) death.Die();
        else Net.Destroy(gameObject);
    }
}
