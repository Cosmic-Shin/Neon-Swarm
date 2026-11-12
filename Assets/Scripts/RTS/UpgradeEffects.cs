using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BuildingUpgrade))]
public class UpgradeEffects : MonoBehaviour
{
    [System.Serializable]
    public class Level
    {
        [Range(2, 3)] public int level = 2;
        public string displayName;
        [TextArea] public string description;
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
            foreach (var l in levels) if (l.level == level) { Alerts.Show($"Upgrade complete: {l.displayName}", Alerts.Good); Sfx.Say(Line.Upgrade, 4f); }
    }

    void Rebuild()
    {
        active.Clear();
        foreach (var l in levels) if (Upgrade.level >= l.level) active.AddRange(l.mods);
    }
}
