using UnityEngine;

// One entry in the build menu.
[CreateAssetMenu(menuName = "RTS/Building Data", fileName = "NewBuilding")]
public class BuildingData : ScriptableObject
{
    public string displayName;
    [TextArea] public string description; // shown in the shop tooltip
    public GameObject prefab;       // root needs a BoxCollider (used for the placement overlap check)
    public Sprite icon;
    public int cost = 500;          // credits, paid on Confirm
    public float buildTime = 10f;   // seconds under construction
    public bool dragLine;           // walls: press and drag to lay a line of them
    public BuildingData[] requires; // tech tree: a finished one of each must be standing

    // Every requirement has a finished building of ours (half-built ones aren't in Structure.All).
    public bool Unlocked(int team = 0) => Missing(team) == null;

    // The required buildings a team doesn't have yet, one per line (null = none missing).
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
