using System.Collections.Generic;
using UnityEngine;

static class HarvesterSafety
{
    const float Radius = 25f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook() => OreNode.IsDangerous = IsDangerous;

    static readonly List<(Vector3 p, int team)> armed = new();
    static int builtFrame = -1;

    static bool IsDangerous(Vector3 p, int team)
    {
        if (builtFrame != Time.frameCount)
        {
            builtFrame = Time.frameCount;
            armed.Clear();
            foreach (var h in Health.All)
                if (h != null && (h.TryGetComponent(out Attacker _) || h.TryGetComponent(out Turret _) || h.TryGetComponent(out Outpost _)))
                    armed.Add((h.transform.position, h.team));
        }
        foreach (var (a, t) in armed)
            if (!Net.Allied(t, team) && (a - p).sqrMagnitude <= Radius * Radius) return true;
        return false;
    }
}
