using UnityEngine;

// Tells the pack's harvesters which ore is unsafe: armed enemies (units, towers, enemy outposts) within 25 m.
static class HarvesterSafety
{
    const float Radius = 25f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook() => OreNode.IsDangerous = IsDangerous;

    // team: whose harvester is asking (single-player AIs mine too).
    static bool IsDangerous(Vector3 p, int team)
    {
        foreach (var h in Health.All)
        {
            if (Net.Allied(h.team, team) || (h.transform.position - p).sqrMagnitude > Radius * Radius) continue;
            if (h.TryGetComponent(out Attacker _) || h.TryGetComponent(out Turret _) || h.TryGetComponent(out Outpost _)) return true;
        }
        return false;
    }
}
