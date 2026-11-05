using Fusion;
using UnityEngine;

// Single-player prefab -> its network variant (NetworkObject + NetEntity, units also NetworkTransform).
// Lives in Resources so Net.Spawn can find it. Rebuilt by Tools > GD5 > Build Network Prefabs.
[CreateAssetMenu(menuName = "RTS/Net Prefabs", fileName = "NetPrefabs")]
public class NetPrefabs : ScriptableObject
{
    [System.Serializable]
    public struct Pair
    {
        public GameObject original;
        public NetworkObject net;
    }

    public Pair[] pairs;

    public NetworkObject Find(GameObject original)
    {
        foreach (var p in pairs) if (p.original == original) return p.net;
        return null;
    }
}
