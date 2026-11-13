using System.Collections.Generic;
using System.IO;
using Fusion;
using UnityEditor;
using UnityEngine;

public static class NetPrefabBuilder
{
    const string NetDir = "Assets/Prefabs/Net";
    static readonly string[] Skip = { "DerelictOutpost" };

    [MenuItem("Tools/GD5/Build Network Prefabs")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(NetDir)) AssetDatabase.CreateFolder("Assets/Prefabs", "Net");
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

        var pairs = new List<NetPrefabs.Pair>();
        foreach (var folder in new[] { "Assets/Prefabs/Units", "Assets/Prefabs/Buildings" })
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var original = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (System.Array.IndexOf(Skip, original.name) >= 0 || !original.GetComponent<Health>()) continue;
                pairs.Add(new NetPrefabs.Pair { original = original, net = MakeVariant(original, folder.EndsWith("Units")) });
            }

        var table = AssetDatabase.LoadAssetAtPath<NetPrefabs>("Assets/Resources/NetPrefabs.asset");
        if (table == null)
        {
            table = ScriptableObject.CreateInstance<NetPrefabs>();
            AssetDatabase.CreateAsset(table, "Assets/Resources/NetPrefabs.asset");
        }
        table.pairs = pairs.ToArray();
        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        EditorApplication.ExecuteMenuItem("Tools/Fusion/Rebuild Prefab Table");
        Debug.Log($"[GD5] {pairs.Count} network prefabs built.");
    }

    static NetworkObject MakeVariant(GameObject original, bool isUnit)
    {
        string path = $"{NetDir}/{original.name}_Net.prefab";
        GameObject root = File.Exists(path)
            ? PrefabUtility.LoadPrefabContents(path)
            : (GameObject)PrefabUtility.InstantiatePrefab(original);

        var no = root.GetComponent<NetworkObject>();
        if (no == null) no = root.AddComponent<NetworkObject>();
        no.Flags = (no.Flags & ~NetworkObjectFlags.DestroyWhenStateAuthorityLeaves) | NetworkObjectFlags.AllowStateAuthorityOverride;
        if (root.GetComponent<NetEntity>() == null) root.AddComponent<NetEntity>();
        if (isUnit && root.GetComponent<NetworkTransform>() == null) root.AddComponent<NetworkTransform>();

        GameObject saved;
        if (File.Exists(path))
        {
            saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
        else
        {
            saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
        return saved.GetComponent<NetworkObject>();
    }
}
