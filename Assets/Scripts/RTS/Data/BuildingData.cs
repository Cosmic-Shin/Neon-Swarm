using UnityEngine;

[CreateAssetMenu(menuName = "RTS/Building Data", fileName = "NewBuilding")]
public class BuildingData : ScriptableObject
{
    public string displayName;
    [TextArea] public string description;
    public GameObject prefab;
    public Sprite icon;
    public int cost = 500;
    public float buildTime = 10f;
    public bool dragLine;
    public BuildingData[] requires;
    [Tooltip("Core Nexus level needed (1 = none).")]
    [Min(1)] public int coreLevel = 1;
    public bool onePerPlayer;

    public bool Unlocked(int team = 0) => Missing(team) == null;

    bool Owned(int team)
    {
        foreach (var s in Structure.All) if (s.data == this && s.Health.team == team) return true;
        foreach (var c in Construction.All)
            if (TeamEconomy.TeamOf(c) == team && c.TryGetComponent(out Structure s) && s.data == this) return true;
        return false;
    }

    public string CoreName => coreLevel >= 3 ? "Core Nexus Mk III" : "Core Nexus Mk II";

    public string Missing(int team = 0)
    {
        if (onePerPlayer && Owned(team)) return "Limit: one per player";
        string missing = coreLevel > 1 && ResearchManager.CoreLevelOf(team) < coreLevel ? CoreName : null;
        if (requires == null) return missing;
        foreach (var r in requires)
            if (r != null && !Structure.All.Exists(s => s.data == r && s.Health.team == team))
                missing = missing == null ? r.displayName : missing + "\n" + r.displayName;
        return missing;
    }
}
