using UnityEngine;

// Every unit and building prefab, so a save can turn "Skitter" back into a Skitter.
// One asset: Resources/PrefabList (refill it when adding units or buildings).
[CreateAssetMenu(menuName = "RTS/Prefab List", fileName = "PrefabList")]
public class PrefabList : ScriptableObject
{
    public GameObject[] prefabs;

    public static GameObject Find(string name)
    {
        var list = Resources.Load<PrefabList>("PrefabList");
        if (list == null) return null;
        foreach (var p in list.prefabs) if (p != null && p.name == name) return p;
        return null;
    }
}
