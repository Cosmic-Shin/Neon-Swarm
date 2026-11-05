using System.Collections.Generic;
using UnityEngine;

// What a building's Mk II / Mk III upgrade does. The pack's BuildingUpgrade pays, waits and shows the
// add-on models; this holds the bonuses for the reached levels, which Stats adds to this building.
// Effects that aren't plain numbers (second refinery dock, bigger pylon ring, reveal ring, research
// tiers) read the level or the shown add-on models directly.
[RequireComponent(typeof(BuildingUpgrade))]
public class UpgradeEffects : MonoBehaviour
{
    [System.Serializable]
    public class Level
    {
        [Range(2, 3)] public int level = 2;
        public string displayName;              // "Core Nexus Mk II"
        [TextArea] public string description;   // the effect, shown in the tooltip
        public StatMod[] mods;
    }

    public Level[] levels;

    readonly List<StatMod> active = new();
    public IReadOnlyList<StatMod> Active => active;
    public BuildingUpgrade Upgrade { get; private set; }

    public Level Next
    {
        get { foreach (var l in levels) if (l.level == Upgrade.level + 1) return l; return null; }
    }

    void Awake()
    {
        Upgrade = GetComponent<BuildingUpgrade>();
        Upgrade.OnLevelChanged += LevelChanged;
        Rebuild();
    }

    void OnDestroy() => Upgrade.OnLevelChanged -= LevelChanged;

    void LevelChanged(int level)
    {
        Rebuild();
        Stats.Refresh();
        if (TryGetComponent(out Health h) && h.team == 0)
            foreach (var l in levels) if (l.level == level) Alerts.Show($"Upgrade complete: {l.displayName}", Alerts.Good);
    }

    void Rebuild()
    {
        active.Clear();
        foreach (var l in levels) if (Upgrade.level >= l.level) active.AddRange(l.mods);
    }
}
