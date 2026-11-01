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
}
