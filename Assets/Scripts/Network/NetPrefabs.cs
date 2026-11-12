using Fusion;
using UnityEngine;

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
