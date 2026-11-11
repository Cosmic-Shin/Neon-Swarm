using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// A computer opponent (single player). It plays by your rules: its own credits from its own harvesters,
// its own power, the same tech tree, buildings placed inside its build radius and built over time,
// units bought from its factories. What it does is set by its AIProfile (difficulty).
//
// Every think it runs the economy and base (harvesters, build order, rebuilding, factories), then its army
// state machine:
//   Gathering - units collect at the front of the base until the army is worth enough to attack
//   Attacking - the wave attack-moves to the nearest enemy building, then the next one
//   Defending - enemies near the base: everything at home turns on them, then back to Gathering
// Past the build order it upgrades (Core Nexus first) and researches; it buys counters to what enemies
// field, builds flak against air, pylons out to new ore when its own runs low, and raids enemy harvesters.
public class AIPlayer : MonoBehaviour
{
    public enum State { Gathering, Attacking, Defending }

    public int team;
    public AIProfile profile;
    public Vector3 home;  // its base spot
    public Vector3 front; // flat direction from home towards the middle of the map

    public State Mode { get; private set; } = State.Gathering;

    BuildingPlacer placer;
    float nextThink, started;
    int waveValue, lateIndex;

    // Refreshed every think.
    readonly List<Structure> buildings = new();
    readonly List<Construction> sites = new();
    readonly List<SelectableUnit> army = new(), workers = new();
    readonly List<SelectableUnit> wave = new();

    public static readonly List<AIPlayer> All = new();
    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
        placer = FindFirstObjectByType<BuildingPlacer>();
        started = Time.time - Skirmish.ElapsedBefore; // a loaded save: the match is already this old
        waveValue = profile.waveValue;
        TeamEconomy.SetIncomeBonus(team, profile.incomeBonus);
        nextThink = Time.time + 2f + team * 0.3f; // AIs don't all think on the same frame
        armyFund = Mathf.RoundToInt(TeamEconomy.Credits(team) * profile.armyShare);
        lastEarned = TeamEconomy.Earned(team);
    }

    // Budget: armyShare of all income goes to units (armyFund), the rest to buildings and harvesters.
    int armyFund, lastEarned;
    int BuildFunds => Mathf.Max(0, TeamEconomy.Credits(team) - armyFund);

    void Update()
    {
        if (Time.time < nextThink) return;
        nextThink = Time.time + profile.thinkInterval;
        int earned = TeamEconomy.Earned(team);
        armyFund += Mathf.RoundToInt((earned - lastEarned) * profile.armyShare);
        lastEarned = earned;
        armyFund = Mathf.Min(armyFund, TeamEconomy.Credits(team)); // never more than it has
        Survey();
        if (buildings.Count == 0 && sites.Count == 0) { enabled = false; return; } // defeated
        Harvesters();
        Build();
        Produce();
        Army();
        Harass();
    }

    // ---- what it has ----
    void Survey()
    {
        buildings.Clear(); sites.Clear(); army.Clear(); workers.Clear();
        foreach (var s in Structure.All) if (s != null && s.Health.team == team && !s.Health.IsDead) buildings.Add(s);
        foreach (var c in FindObjectsByType<Construction>(FindObjectsSortMode.None)) if (TeamEconomy.TeamOf(c) == team) sites.Add(c);
        foreach (var h in Health.All)
        {
            if (h.team != team || !h.TryGetComponent(out SelectableUnit u) || u.Data == null) continue;
            if (u.Data.role == UnitData.Role.Worker) workers.Add(u);
            else if (h.TryGetComponent(out Attacker _)) army.Add(u);
        }
        wave.RemoveAll(u => u == null || !army.Contains(u));
        harass.RemoveAll(u => u == null || !army.Contains(u));
        SurveyEnemies();
        PickExpansion();
    }

    // What enemies field, by credits: light vs heavy armour, and how much of it flies. Drives counter-picking.
    // ponytail: sees every enemy unit through fog (a mild cheat); scout-based memory if it ever matters.
    float enemyLight, enemyHeavy, enemyAir;

    void SurveyEnemies()
    {
        enemyLight = enemyHeavy = enemyAir = 0f;
        foreach (var h in Health.All)
        {
            if (Net.Allied(h.team, team) || h.team == Creep.Team || !h.TryGetComponent(out SelectableUnit u) || u.Data == null) continue;
            if (u.Data.role == UnitData.Role.Worker) continue;
            float worth = u.Data.cost / Mathf.Max(1f, u.Data.squadSize);
            if (u.Data.armor == DamageTable.Armor.Heavy) enemyHeavy += worth; else enemyLight += worth;
            if (Flyer.IsAir(u)) enemyAir += worth;
        }
        ownAntiAir = 0f;
        foreach (var u in army) if (u.Data.hitsAir) ownAntiAir += u.Data.cost / Mathf.Max(1f, u.Data.squadSize);
    }

    float ownAntiAir;
    float AirShare => enemyAir / Mathf.Max(1f, enemyLight + enemyHeavy);
    // Air still unanswered: once its anti-air is worth 1.5x their flyers, it stops favouring more.
    float AirNeed => Mathf.Clamp01((enemyAir * 1.5f - ownAntiAir) / Mathf.Max(1f, enemyLight + enemyHeavy));

    // How well a unit answers that army: its weapon against their armour mix, and whether it can hit their flyers.
    float Counter(UnitData d)
    {
        float total = enemyLight + enemyHeavy;
        if (total < 1f) return 1f;
        float f = (enemyLight * DamageTable.Factor(d.weapon, DamageTable.Armor.Light) + enemyHeavy * DamageTable.Factor(d.weapon, DamageTable.Armor.Heavy)) / total;
        f *= d.hitsAir ? 1f + AirNeed * 2f : 1f - AirShare * 0.5f; // can't shoot what flies
        return f * f; // sharpen: 1.25 -> 1.56, 0.65 -> 0.42
    }

    int Owned(BuildingData d)
    {
        int n = 0;
        foreach (var s in buildings) if (s.data == d) n++;
        foreach (var c in sites) if (c.TryGetComponent(out Structure s) && s.data == d) n++;
        return n;
    }

    IEnumerable<Building> Factories()
    {
        foreach (var s in buildings) if (s.TryGetComponent(out Building b) && b.enabled) yield return b;
    }

    // ---- economy ----
    void Harvesters()
    {
        int refineries = 0;
        foreach (var s in buildings) if (s.TryGetComponent(out Refinery _)) refineries++;
        int want = Mathf.Min(profile.harvesters, 1 + refineries * 2);
        int have = workers.Count;
        foreach (var b in Factories())
            for (int i = 0; i < b.OptionCount; i++)
                if (b.UnitOf(i) is UnitData d && d.role == UnitData.Role.Worker) have += b.QueuedCount(i);
        if (have >= want) return;
        foreach (var b in Factories())
            for (int i = 0; i < b.OptionCount; i++)
                if (b.UnitOf(i) is UnitData d && d.role == UnitData.Role.Worker && b.CanQueue(i) && b.CostOf(i) <= BuildFunds) { b.Enqueue(i); return; }
    }

    // ---- base ----
    BuildingData nextBuilding;

    void Build()
    {
        nextBuilding = PickBuilding();
        if (nextBuilding == null) { if (!teching) armyFund = TeamEconomy.Credits(team); return; } // nothing to build or tech: everything goes on the army
        if (!BuildSlots.FreeFor(team)) return;

        // Would it run the base out of power? A pylon first.
        int spare = Structure.PowerMadeOf(team) - Structure.PowerUsedOf(team);
        int need = nextBuilding.prefab.TryGetComponent(out Structure st) ? st.power : 0;
        if (need < 0 && spare + need < 0) nextBuilding = Pylon() ?? nextBuilding;

        if (BuildFunds < nextBuilding.cost) return; // saving up
        if (!FindSpot(nextBuilding, out Vector3 pos, out Quaternion rot, buildToward))
        {
            if (buildToward.HasValue) return; // nowhere to put the next step right now
            // Base full: a pylon on the outer edge grows the build area (once the last site is done, so its ring counts).
            if (sites.Count > 0 || Pylon() is not BuildingData pylon || pylon == nextBuilding || BuildFunds < pylon.cost) return;
            nextBuilding = pylon;
            if (!FindSpot(pylon, out pos, out rot, null, outward: true)) return;
        }
        if (!TeamEconomy.Spend(team, nextBuilding.cost)) return;

        var go = Net.Spawn(nextBuilding.prefab, pos, rot);
        var h = go.GetComponent<Health>();
        h.team = team; // before Start: colours, power and build slots follow the team
        if (go.TryGetComponent(out TeamMember tm)) tm.team = team;
        var site = go.AddComponent<Construction>();
        site.duration = nextBuilding.buildTime;
        MoveOff(pos);
        if (nextBuilding == lateGamePick) lateIndex++;
        if (buildToward.HasValue && ++expandSteps > 8) GiveUp(expandTo); // pylon chain going nowhere
    }

    // Its units standing on the spot FindSpot just picked.
    readonly List<SelectableUnit> inTheWay = new();

    // Units on a new building's spot walk off it, away from it.
    void MoveOff(Vector3 spot)
    {
        foreach (var u in inTheWay)
        {
            if (u == null || !u.TryGetComponent(out Attacker a) || a.HasTarget) continue; // harvesters drive off on their own
            Vector3 away = u.transform.position - spot; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = front;
            a.AttackMove(spot + away.normalized * 16f);
        }
        inTheWay.Clear();
    }

    BuildingData lateGamePick;
    bool teching; // Tech() is spending on or saving for an upgrade or research
    Vector3? buildToward; // set when the pick is an expansion step: place it as close to the new field as possible

    // The first build-order step it hasn't got (lost buildings count as missing, so it rebuilds them),
    // skipping ones whose tech isn't there yet. Flak when enemies fly, an expansion when ore runs low,
    // then the late-game list, if it can spare the credits.
    BuildingData PickBuilding()
    {
        buildToward = null;
        teching = false;
        if (profile.counterPick > 0f && AirShare > 0.2f && AntiAir() is BuildingData flak && Owned(flak) < 2 && flak.Unlocked(team)) return flak;
        var seen = new Dictionary<BuildingData, int>();
        foreach (var d in profile.buildOrder)
        {
            if (d == null) continue;
            seen.TryGetValue(d, out int n);
            seen[d] = ++n;
            if (Owned(d) >= n) continue;
            if (d.Unlocked(team)) return d;
        }
        if (Expansion() is BuildingData step) return step;
        if (teching = Tech()) return null; // upgrades and research before more of the same buildings
        lateGamePick = null;
        if (profile.lateGame == null || profile.lateGame.Length == 0) return null;
        for (int k = 0; k < profile.lateGame.Length; k++)
        {
            var d = profile.lateGame[(lateIndex + k) % profile.lateGame.Length];
            if (d == null || !d.Unlocked(team)) continue;
            if (BuildFunds < d.cost) return null; // not yet: the army can have the money meanwhile
            lateIndex += k;
            return lateGamePick = d;
        }
        return null;
    }

    BuildingData Pylon() => FirstBuilding(d => d.prefab.TryGetComponent(out Structure s) && s.power > 0);
    BuildingData AntiAir() => FirstBuilding(d => d.prefab.TryGetComponent(out Turret t) && t.HitsAir && !t.HitsGround);
    BuildingData RefineryData() => FirstBuilding(d => d.prefab.TryGetComponent(out Refinery _) && !d.prefab.GetComponent<Structure>().isHQ);

    BuildingData FirstBuilding(System.Predicate<BuildingData> match)
    {
        foreach (var d in placer.Buildings) if (d.prefab != null && match(d) && d.Unlocked(team)) return d;
        return null;
    }

    // After the build order. Core Nexus upgrades first (they unlock research tiers and elite units), then a
    // research project for each idle lab, then the other buildings' upgrades, cheapest first.
    // True while it's spending on (or saving up for) something.
    bool Tech()
    {
        int funds = BuildFunds;
        if (profile.upgrades)
            foreach (var s in buildings)
                if (s.isHQ && s.TryGetComponent(out BuildingUpgrade core) && core.CanUpgrade)
                {
                    if (core.NextCost <= funds) core.Upgrade();
                    return true;
                }

        if (profile.research)
            foreach (var lab in Factories())
            {
                if (lab.QueueLength > 0) continue;
                for (int i = 0; i < lab.OptionCount; i++)
                {
                    var r = lab.ResearchOf(i);
                    if (r == null || r.effects == null || r.effects.Length == 0 || lab.Blocker(i) != null) continue;
                    if (lab.CostOf(i) <= funds) lab.Enqueue(i);
                    return true;
                }
            }

        if (profile.upgrades)
        {
            BuildingUpgrade cheapest = null;
            foreach (var s in buildings)
                if (s.TryGetComponent(out BuildingUpgrade up) && up.CanUpgrade && (cheapest == null || up.NextCost < cheapest.NextCost)) cheapest = up;
            if (cheapest != null)
            {
                if (cheapest.NextCost <= funds) cheapest.Upgrade();
                return true;
            }
        }
        return false;
    }

    // ---- expansion ----
    // When the ore near its refineries runs low, the nearest field still worth mining (safe, not given up on)
    // becomes the target: pylons chain out towards it until a refinery fits beside it.
    OreNode expandTo;
    int expandSteps;
    readonly HashSet<OreNode> giveUp = new();

    // Writes off the whole field round that node, not just the node.
    void GiveUp(OreNode node)
    {
        if (node == null) return;
        foreach (var o in OreNode.All) if (o != null && Near(o.transform.position, node.transform.position, 25f)) giveUp.Add(o);
    }
    const float RefineryReach = 40f;

    void PickExpansion()
    {
        var old = expandTo;
        expandTo = null;
        if (profile.expandBelow <= 0f) return;
        float reachable = 0f;
        foreach (var o in OreNode.All)
            if (o != null && NearRefinery(o.transform.position)) reachable += o.amount * o.valueMultiplier;
        if (reachable >= profile.expandBelow) return;

        float best = float.MaxValue;
        foreach (var o in OreNode.All)
        {
            if (o == null || o.amount < 300f || giveUp.Contains(o) || NearRefinery(o.transform.position)) continue;
            Vector3 p = o.transform.position;
            if (OreNode.IsDangerous != null && OreNode.IsDangerous(p, team)) continue;
            if (EnemyBuildingNear(p, 60f)) continue; // someone else's base field
            float d = (p - home).sqrMagnitude;
            if (d < best) { best = d; expandTo = o; }
        }
        if (expandTo != null && (old == null || !Near(old.transform.position, expandTo.transform.position, 25f))) expandSteps = 0; // a new field
    }

    bool NearRefinery(Vector3 p)
    {
        foreach (var s in buildings) if (s.TryGetComponent(out Refinery _) && Near(p, s.transform.position, RefineryReach)) return true;
        return false;
    }

    bool EnemyBuildingNear(Vector3 p, float r)
    {
        foreach (var s in Structure.All) if (s != null && !Net.Allied(s.Health.team, team) && Near(p, s.transform.position, r)) return true;
        return false;
    }

    // The next step out to the target field: a refinery if one fits near it, else a pylon as close as it'll go.
    BuildingData Expansion()
    {
        if (expandTo == null || RefineryData() is not BuildingData refinery) return null;
        if (sites.Count > 0) return null; // wait for the last step to finish: its build radius isn't on yet
        Vector3 target = expandTo.transform.position;
        buildToward = target;
        if (FindSpot(refinery, out Vector3 p, out _, target) && Near(p, target, RefineryReach * 0.6f)) return refinery;
        return Pylon();
    }

    // A free spot inside its build radius. Normally rings around home, nearest first: defences prefer the
    // front, everything else the back. With a target (expansion): anywhere it may build, closest to the target.
    // A few metres of room are kept round each building so units can get past.
    bool FindSpot(BuildingData d, out Vector3 pos, out Quaternion rot, Vector3? toward = null, bool outward = false)
    {
        var box = d.prefab.GetComponent<BoxCollider>();
        Vector3 scale = d.prefab.transform.localScale;
        Vector3 size = Vector3.Scale(box.size, scale);
        float footprint = Mathf.Max(size.x, size.z);
        bool defence = d.prefab.TryGetComponent(out Turret _);
        rot = Quaternion.LookRotation(front);
        pos = default;

        var spots = new List<(Vector3 p, float score)>();
        void Rings(Vector3 centre, float from, float to, float step)
        {
            int ring = 0;
            for (float r = from; r <= to; r += step, ring++)
            {
                int steps = Mathf.Max(8, Mathf.RoundToInt(r * 0.6f));
                for (int i = 0; i < steps; i++)
                {
                    float a = (i + (ring % 2) * 0.5f) * Mathf.PI * 2f / steps; // every other ring staggered
                    Vector3 p = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    p.x = Mathf.Round(p.x / 2f) * 2f;
                    p.z = Mathf.Round(p.z / 2f) * 2f;
                    Vector3 off = p - home; off.y = 0f;
                    float dist = off.magnitude;
                    float facing = dist > 0.1f ? Vector3.Dot(off / dist, front) : 0f;
                    float score = toward.HasValue ? Vector3.Distance(p, toward.Value) : outward ? -dist : defence ? dist - facing * 40f : dist + facing * 10f;
                    spots.Add((p, score));
                }
            }
        }
        // Close around each of its build circles (pylon circles are small; a building only has to touch one)...
        foreach (var b in BuildRadius.All) if (b.Team == team) Rings(b.transform.position, 4f, b.Radius + footprint * 0.5f, 3f);
        if (!toward.HasValue) Rings(home, 16f, 90f, 6f); // ...and the wider base around home
        spots.Sort((x, y) => x.score.CompareTo(y.score));

        int ground = placer.GroundMask;
        Vector3 half = size * 0.5f + new Vector3(3f, -0.1f, 3f); // room to walk round it
        foreach (var (p, _) in spots)
        {
            if (!BuildRadius.Covers(p, footprint, team)) continue;
            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas)) continue;
            Vector3 at = new(p.x, hit.position.y, p.z);
            Vector3 centre = at + rot * Vector3.Scale(box.center, scale);
            // Its own units don't count: they get told to clear the spot once it's built (MoveOff).
            inTheWay.Clear();
            bool blocked = false;
            foreach (var c in Physics.OverlapBox(centre, half, rot, ~ground, QueryTriggerInteraction.Ignore))
            {
                if (c.GetComponentInParent<SelectableUnit>() is SelectableUnit u && TeamEconomy.TeamOf(u) == team) { inTheWay.Add(u); continue; }
                blocked = true;
                break;
            }
            if (blocked) continue;
            pos = at;
            return true;
        }
        return false;
    }

    // ---- factories ----
    void Produce()
    {
        Vector3 rally = home + front * 24f;
        foreach (var b in Factories()) b.Rally = rally;
        // One purchase per think, weighted over every factory's options together: a factory with a single
        // unit (the drone hangar) would otherwise buy one every think and flood the army with it.
        // The pick sticks until it's bought, so it saves up for expensive ones (elites) instead of
        // spending everything on whatever is cheap right now.
        if (wanted.b == null || wanted.b.QueueLength >= 2 || wanted.b.Blocker(wanted.i) != null) wanted = RollUnit();
        if (wanted.b == null || wanted.b.CostOf(wanted.i) > Mathf.Min(armyFund, TeamEconomy.Credits(team))) return; // saving up
        int before = TeamEconomy.Credits(team);
        wanted.b.Enqueue(wanted.i);
        armyFund -= before - TeamEconomy.Credits(team); // paid from the army's share
        wanted = default;
    }

    (Building b, int i) wanted;

    (Building, int) RollUnit()
    {
        float total = 0f;
        var options = new List<(Building b, int i, float w)>();
        foreach (var b in Factories())
        {
            if (b.QueueLength >= 2) continue;
            for (int i = 0; i < b.OptionCount; i++)
            {
                var d = b.UnitOf(i);
                if (d == null || d.role == UnitData.Role.Worker || b.Blocker(i) != null) continue;
                float w = Weight(d);
                if (w <= 0f) continue;
                options.Add((b, i, w));
                total += w;
            }
        }
        float roll = Random.value * total;
        foreach (var (b, i, w) in options) { roll -= w; if (roll <= 0f) return (b, i); }
        return default;
    }

    public int ArmyFund => armyFund;

    int HomeArmyWorth()
    {
        int worth = 0;
        foreach (var u in army) if (!Away(u)) worth += u.Data.cost / Mathf.Max(1, u.Data.squadSize);
        return worth;
    }

    bool Away(SelectableUnit u) => wave.Contains(u) || harass.Contains(u);

    float Weight(UnitData d)
    {
        if (profile.units != null) foreach (var p in profile.units) if (p.unit == d) return p.weight * Mathf.Lerp(1f, Counter(d), profile.counterPick);
        return 0f;
    }

    // ---- army ----
    void Army()
    {
        var threat = Threat();
        if (threat != null)
        {
            Mode = State.Defending;
            foreach (var u in army) if (!wave.Contains(u) || Near(u.transform.position, home, profile.defendRadius * 1.5f)) Order(u, threat.transform.position);
            return;
        }
        if (Mode == State.Defending) Mode = wave.Count > 0 ? State.Attacking : State.Gathering;

        // Units at home that aren't in the wave wait at the rally point (after a defence they walk back there).
        Vector3 rally = home + front * 24f;
        int worth = HomeArmyWorth();
        foreach (var u in army)
        {
            if (Away(u)) continue;
            var a = u.GetComponent<Attacker>();
            if (!a.Busy && !Near(u.transform.position, rally, 18f) && u.TryGetComponent(out NavMeshAgent ag) && ag.enabled && !ag.hasPath) a.AttackMove(rally);
        }

        if (Time.time - started >= profile.firstAttack && worth >= waveValue)
        {
            foreach (var u in army) if (!Away(u)) wave.Add(u);
            waveValue = Mathf.RoundToInt(waveValue * profile.waveGrowth);
            Mode = State.Attacking;
            foreach (var u in wave) u.GetComponent<Attacker>().Stop(); // forget the rally point
        }

        if (wave.Count == 0) { if (Mode == State.Attacking) Mode = State.Gathering; return; }
        // The wave works through enemy buildings, nearest first; units that finished their attack-move get the next one.
        Vector3 centre = Vector3.zero;
        foreach (var u in wave) centre += u.transform.position;
        centre /= wave.Count;
        var target = NearestEnemyBuilding(centre);
        if (target == null) return;
        // Buildings cut a hole in the navmesh: walk to the reachable ground beside it, then attack it outright.
        Vector3 edge = target.TryGetComponent(out Collider col) ? col.ClosestPoint(centre) : target.transform.position;
        Vector3 stand = NavMesh.SamplePosition(edge, out NavMeshHit hit, 12f, NavMesh.AllAreas) ? hit.position : edge;
        foreach (var u in wave)
        {
            var a = u.GetComponent<Attacker>();
            if (a.HasTarget) continue; // busy fighting
            if (Near(u.transform.position, stand, 30f)) a.Attack(target);
            else if (!a.Busy) a.AttackMove(stand);
        }
    }

    // ---- harass ----
    // Every harassEvery seconds its fastest units at home raid enemy harvesters, nearest first,
    // until none are left or the base needs them; then they walk home and rejoin the army.
    readonly List<SelectableUnit> harass = new();
    float nextHarass;

    void Harass()
    {
        if (Mode == State.Defending) { harass.Clear(); return; }
        if (harass.Count > 0)
        {
            Vector3 centre = Vector3.zero;
            foreach (var u in harass) centre += u.transform.position;
            var prey = NearestEnemyHarvester(centre / harass.Count);
            if (prey == null) { harass.Clear(); return; } // back to the rally with the others
            foreach (var u in harass) { var a = u.GetComponent<Attacker>(); if (!a.HasTarget) a.Attack(prey); }
            return;
        }
        if (profile.harassEvery <= 0f || Time.time < nextHarass || Time.time - started < profile.firstAttack * 0.6f) return;
        nextHarass = Time.time + profile.harassEvery;
        var idle = new List<SelectableUnit>();
        foreach (var u in army) if (!Away(u) && !Flyer.IsAir(u) && u.TryGetComponent(out NavMeshAgent _)) idle.Add(u);
        if (idle.Count < profile.harassSize * 2) return; // keep some at home
        idle.Sort((x, y) => y.GetComponent<NavMeshAgent>().speed.CompareTo(x.GetComponent<NavMeshAgent>().speed));
        for (int i = 0; i < profile.harassSize; i++) harass.Add(idle[i]);
    }

    Health NearestEnemyHarvester(Vector3 from)
    {
        Health best = null;
        float bestSqr = float.MaxValue;
        foreach (var h in Health.All)
        {
            if (Net.Allied(h.team, team) || h.team == Creep.Team || !h.TryGetComponent(out Harvester _)) continue;
            float d = (h.transform.position - from).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = h; }
        }
        return best;
    }

    void Order(SelectableUnit u, Vector3 to) { if (u.TryGetComponent(out Attacker a) && !a.HasTarget) a.AttackMove(to); }

    static bool Near(Vector3 a, Vector3 b, float r) { a.y = b.y = 0f; return (a - b).sqrMagnitude <= r * r; }

    // The closest enemy unit near its base or any of its buildings.
    Health Threat()
    {
        Health best = null;
        float bestSqr = float.MaxValue;
        foreach (var h in Health.All)
        {
            if (Net.Allied(h.team, team) || h.team == Creep.Team || !h.TryGetComponent(out SelectableUnit _)) continue;
            Vector3 p = h.transform.position;
            bool close = Near(p, home, profile.defendRadius);
            if (!close) foreach (var s in buildings) if (Near(p, s.transform.position, 22f)) { close = true; break; }
            if (!close) continue;
            float d = (p - home).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = h; }
        }
        return best;
    }

    Health NearestEnemyBuilding(Vector3 from)
    {
        Health best = null;
        float bestSqr = float.MaxValue;
        foreach (var h in Health.All)
        {
            if (Net.Allied(h.team, team) || h.team == Creep.Team || !h.TryGetComponent(out Structure _)) continue;
            float d = (h.transform.position - from).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = h; }
        }
        return best;
    }
}
