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

    public bool Unlocked(int team = 0) => Missing(team) == null;

    public string Missing(int team = 0)
    {
        if (requires == null) return null;
        string missing = null;
        foreach (var r in requires)
            if (r != null && !Structure.All.Exists(s => s.data == r && s.Health.team == team))
                missing = missing == null ? r.displayName : missing + "\n" + r.displayName;
        return missing;
    }
}
