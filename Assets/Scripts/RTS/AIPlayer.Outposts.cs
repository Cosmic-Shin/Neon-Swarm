using System.Collections.Generic;
using UnityEngine;

// Outposts and corp sites: a small squad peels off the home army, clears the guards, stands in the zone
// until it flips, leaves one unit garrisoned (stops the hold timer decaying) and moves on to the next.
public partial class AIPlayer
{
    const float ClaimThink = 3f, GuardScan = 28f;

    readonly List<SelectableUnit> claimers = new();
    OutpostCapture claim;
    float nextClaim, claimUntil;

    void Outposts()
    {
        claimers.RemoveAll(u => u == null || !army.Contains(u));
        if (Mode == State.Defending || (claim != null && (claimers.Count == 0 || Time.time > claimUntil))) { Release(); if (Mode == State.Defending) return; }
        if (Time.time < nextClaim) return;
        nextClaim = Time.time + ClaimThink;

        if (claim != null && Mine(claim))
        {
            if (claim.GarrisonCount == 0)
                foreach (var u in claimers)
                    if (Near(u.transform.position, claim.transform.position, claim.captureRadius) && claim.TryGarrison(u.GetComponent<TeamMember>())) break;
            Release();
        }
        if (claim == null && !PickClaim()) return;

        foreach (var u in claimers)
        {
            var a = u.GetComponent<Attacker>();
            if (a.HasTarget) continue;
            if (Near(u.transform.position, claim.transform.position, claim.captureRadius * 0.7f)) { if (a.Busy) a.Stop(); }
            else if (!a.Busy) a.AttackMove(claim.transform.position);
        }
    }

    void Release()
    {
        foreach (var u in claimers) if (u != null && u.TryGetComponent(out Attacker a)) a.Stop();
        claimers.Clear();
        claim = null;
    }

    bool PickClaim()
    {
        if (Time.time - started < profile.firstAttack * 0.35f) return false;
        var idle = new List<SelectableUnit>();
        foreach (var u in army) if (!Away(u) && u.TryGetComponent(out UnityEngine.AI.NavMeshAgent _)) idle.Add(u);

        OutpostCapture best = null;
        int bestNeed = 0;
        float bestScore = float.MaxValue;
        foreach (var o in FindObjectsByType<OutpostCapture>(FindObjectsSortMode.None))
        {
            if (Mine(o) || (o.TryGetComponent(out Health h) && h.IsDead)) continue;
            int need = 3 + 2 * HostilesNear(o.transform.position) + (o.IsOwned ? 2 + o.GarrisonCount : 0);
            if (idle.Count < need + 2) continue;   // keep a couple at home
            float score = Vector3.Distance(o.transform.position, home) * (o.TryGetComponent(out CorpSite c) && c.kind == CorpSite.Kind.DataVault ? 0.8f : 1f) + need * 15f;
            if (score < bestScore) { bestScore = score; best = o; bestNeed = need; }
        }
        if (best == null) return false;
        idle.Sort((x, y) => (x.transform.position - best.transform.position).sqrMagnitude.CompareTo((y.transform.position - best.transform.position).sqrMagnitude));
        for (int i = 0; i < bestNeed; i++) claimers.Add(idle[i]);
        claim = best;
        claimUntil = Time.time + 90f;
        return true;
    }

    bool Mine(OutpostCapture o) => o.ownerTeam >= 0 && Net.Allied(o.ownerTeam, team);

    int HostilesNear(Vector3 at)
    {
        int n = 0;
        foreach (var h in Health.All)
            if (h != null && !h.IsDead && !Net.Allied(h.team, team) && h.TryGetComponent(out Attacker _) && Near(h.transform.position, at, GuardScan)) n++;
        return n;
    }
}
