using UnityEditor;
using UnityEngine;

/// <summary>
/// Imported models carry optional add-on groups that must start switched off:
///   *_Upgrade_Mk2 / *_Upgrade_Mk3  (shown by BuildingUpgrade.cs)
///   *_Damage, *_Damage_<Part>      (mid-health look, shown below ~50% health)
/// This turns those GameObjects off on import so prefabs and the Scene view show the clean base building.
/// Scripts find them with GetComponentsInChildren<Transform>(true) and SetActive(true) when needed.
/// </summary>
public class HiddenGroupsImporter : AssetPostprocessor
{
    // bump to force Unity to reimport every model with this rule
    public override uint GetVersion() => 1;

    void OnPostprocessModel(GameObject root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            if (n.EndsWith("_Upgrade_Mk2") || n.EndsWith("_Upgrade_Mk3") || n.EndsWith("_Damage") || n.Contains("_Damage_"))
                t.gameObject.SetActive(false);
        }
    }
}
