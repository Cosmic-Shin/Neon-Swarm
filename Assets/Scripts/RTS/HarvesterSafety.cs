using UnityEngine;

static class HarvesterSafety
{
    const float Radius = 25f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook() => OreNode.IsDangerous = IsDangerous;

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
