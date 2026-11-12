using UnityEngine;

public enum Stat { Damage, Range, FireRate, MaxHealth, MoveSpeed, Sight, BuildTime, HarvestCapacity, HarvestRate, UnloadTime, RefineryCredits, RepairRate, Splash, StunTime,
    AbilityCooldown, BroodSize, OverheatRate, BurstTime }

[System.Serializable]
public class StatMod
{
    public Stat stat;
    public float percent;
    public float flat;

    [Header("Who (research only; all empty = everything of ours)")]
    public UnitData.Role[] roles;
    public UnitData[] units;
    public BuildingData[] buildings;
    public bool defenses;

    public bool AppliesTo(Component who)
    {
        bool anyFilter = (roles != null && roles.Length > 0) || (units != null && units.Length > 0) || (buildings != null && buildings.Length > 0) || defenses;
        if (!anyFilter) return true;
        if (who.TryGetComponent(out SelectableUnit u) && u.Data != null)
            return Has(units, u.Data) || Has(roles, u.Data.role);
        if (who.TryGetComponent(out Structure s))
            return (s.data != null && Has(buildings, s.data)) || (defenses && who.TryGetComponent(out Turret _));
        return false;
    }

    static bool Has<T>(T[] list, T item) => list != null && System.Array.IndexOf(list, item) >= 0;
}

public static class Stats
{
    public static event System.Action Changed;

    public static void Refresh() => Changed?.Invoke();

    public static float Value(Stat stat, float baseValue, Component who)
    {
        float flat = 0f, percent = 0f;
        void Add(StatMod m) { if (m.stat == stat) { flat += m.flat; percent += m.percent; } }

        if (who.TryGetComponent(out Health h))
        {
            var done = ResearchManager.DoneOf(h.team);
            for (int i = 0; i < done.Count; i++)
                foreach (var m in done[i].effects) if (m.stat == stat && m.AppliesTo(who)) Add(m);
        }
        if (who.TryGetComponent(out UpgradeEffects up)) { var a = up.Active; for (int i = 0; i < a.Count; i++) Add(a[i]); }
        if (who.TryGetComponent(out Veterancy vet)) { var v = vet.Mods; for (int i = 0; i < v.Count; i++) Add(v[i]); }

        return (baseValue + flat) * (1f + percent);
    }
}
