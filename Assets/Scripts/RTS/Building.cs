using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Production building: a C&C queue of units and/or one-time research. Options are units first, then upgrades.
public class Building : MonoBehaviour
{
    [SerializeField] SelectableUnit[] unitPrefabs; // one panel button per entry
    [SerializeField] ResearchData[] upgrades;      // research options (Research Lab)
    [SerializeField] Transform spawnPoint;
    [SerializeField] float spawnSpread = 1.5f;
    [SerializeField] float exitSpeed = 4f; // m/s while a new unit walks out of the building
    [SerializeField] Gate gate;            // optional door that opens for each new unit
    [SerializeField] Transform padEdge;    // optional: front edge of a raised floor; units walk on the floor to here, then step down

    // Set by right-clicking the ground with this building selected: new units walk there.
    public Vector3? Rally { get; set; }

    // Production queue: one at a time, in click order.
    readonly List<int> queue = new();
    float progress; // seconds spent on queue[0]
    LineRenderer rallyLine;
    Structure structure;
    ProductionFX fx;
    AerieFX aerie; // air factory: flyers launch out of its roof hatch, titans from its mast

    void Awake()
    {
        unitPrefabs = unitPrefabs.OrderBy(u => u.Cost).ToArray(); // cheapest first
        upgrades = upgrades.OrderBy(u => u.tier).ThenBy(u => u.branch).ThenBy(u => u.cost).ToArray();
        structure = GetComponent<Structure>();
        aerie = GetComponent<AerieFX>();
        fx = gameObject.AddComponent<ProductionFX>();
        if (aerie != null) fx.titanDock = aerie.TitanDock;
        fx.exit = gate != null ? gate.Lintel : spawnPoint;

        rallyLine = new GameObject("RallyLine").AddComponent<LineRenderer>();
        rallyLine.transform.SetParent(transform, false);
        rallyLine.material = new Material(Shader.Find("Sprites/Default"));
        rallyLine.startColor = rallyLine.endColor = new Color(0.3f, 1f, 1f, 0.8f);
        rallyLine.widthMultiplier = 0.25f;
        rallyLine.positionCount = 2;
        rallyLine.enabled = false;
    }

    public int OptionCount => unitPrefabs.Length + upgrades.Length;
    bool IsUnit(int i) => i < unitPrefabs.Length;
    ResearchData Upgrade(int i) => upgrades[i - unitPrefabs.Length];

    public string NameOf(int i) => IsUnit(i) ? unitPrefabs[i].DisplayName : Upgrade(i).displayName;
    public int SquadOf(int i) => IsUnit(i) ? Mathf.Max(1, unitPrefabs[i].Data.squadSize) : 1; // units per purchase
    public Sprite IconOf(int i) => IsUnit(i) ? unitPrefabs[i].Icon : Upgrade(i).icon;
    public int CostOf(int i) => IsUnit(i) ? unitPrefabs[i].Cost : Upgrade(i).cost;
    public float TimeOf(int i) => IsUnit(i) ? Stats.Value(Stat.BuildTime, unitPrefabs[i].BuildTime, this) : Upgrade(i).researchTime; // Hive Mk II: -20%
    public string TooltipOf(int i)
    {
        string tip = IsUnit(i) ? Tooltip.For(unitPrefabs[i].Data) : Tooltip.For(Upgrade(i));
        string why = Blocker(i);
        return why == null || queue.Contains(i) ? tip : tip + $"\n<color=#FF5050>{why}</color>";
    }
    public bool IsResearched(int i) => !IsUnit(i) && ResearchManager.IsDone(Upgrade(i));

    public int Producing => queue.Count > 0 ? queue[0] : -1;
    public float Progress01 => queue.Count > 0 ? progress / TimeOf(queue[0]) : 0f;
    // The production look for option i (-1 = idle): the unit's model, or the research's icon.
    public void ShowProduction(int i, float progress01) =>
        fx.Show(i < 0 || i >= OptionCount ? null : IsUnit(i) ? unitPrefabs[i] : Upgrade(i).icon, progress01);

    // Why this option can't be queued right now, credits aside (null = it can).
    // Elite units need Core Nexus Mk III; research: one project per lab, plus ResearchManager's rules.
    public string Blocker(int i)
    {
        if (IsUnit(i))
        {
            if (unitPrefabs[i].Data.role != UnitData.Role.Elite) return null;
            if (ResearchManager.CoreLevel < 3) return "Needs Core Nexus Mk III";
            return aerie != null && !aerie.HasMast ? "Needs Hive Aerie Mk III" : null; // titans moor at the mast
        }
        if (queue.Exists(q => !IsUnit(q))) return queue.Contains(i) ? "Researching" : "Lab busy: one project at a time";
        return ResearchManager.Blocker(Upgrade(i), this);
    }

    // What's still needed before option i can be bought, for the button's lock badge (null = nothing).
    // A busy lab or a finished project isn't a requirement, so those don't lock the button.
    public string Needs(int i)
    {
        string why = Blocker(i);
        if (why == null) return null;
        if (why.StartsWith("Needs ")) return why.Substring(6);
        if (why.StartsWith("Requires ")) return why.Substring(9);
        return null;
    }

    public bool CanQueue(int i) => ResourceBank.GetOrCreate().Credits >= CostOf(i) && Blocker(i) == null;

    // Paid up front; refused when short on credits.
    public void Enqueue(int index)
    {
        if (!CanQueue(index) || !ResourceBank.GetOrCreate().Spend(CostOf(index))) return;
        queue.Add(index);
        if (!IsUnit(index)) ResearchManager.Started(Upgrade(index), this);
    }

    // Removes the most recently queued one of this type (C&C right-click) and refunds it.
    public void CancelOne(int index)
    {
        int at = queue.LastIndexOf(index);
        if (at < 0) return;
        queue.RemoveAt(at);
        if (at == 0) progress = 0f; // cancelled the one in production
        if (!IsUnit(index)) ResearchManager.Stopped(Upgrade(index));
        ResourceBank.GetOrCreate().Add(CostOf(index));
    }

    // Saves: the research project running here and its time left.
    public ResearchData CurrentResearch => queue.Count > 0 && !IsUnit(queue[0]) ? Upgrade(queue[0]) : null;
    public float TimeLeft => queue.Count > 0 ? TimeOf(queue[0]) - progress : 0f;

    public void RestoreResearch(ResearchData r, float timeLeft)
    {
        int i = System.Array.IndexOf(upgrades, r);
        if (i < 0) return;
        queue.Insert(0, unitPrefabs.Length + i);
        progress = Mathf.Max(0f, r.researchTime - timeLeft);
        ResearchManager.Started(r, this);
    }

    // Destroyed mid-project: the research is lost (paid for, not refunded), another lab can start it.
    void OnDestroy()
    {
        foreach (int q in queue) if (!IsUnit(q)) ResearchManager.Stopped(Upgrade(q));
    }

    public int QueuedCount(int index)
    {
        int n = 0;
        foreach (int i in queue) if (i == index) n++;
        return n;
    }

    // Runs only while enabled, so a building under construction doesn't produce.
    void Update()
    {
        bool showRally = Rally.HasValue && structure != null && structure.IsSelected && unitPrefabs.Length > 0;
        rallyLine.enabled = showRally;
        if (showRally)
        {
            rallyLine.SetPosition(0, spawnPoint.position + Vector3.up * 0.3f);
            rallyLine.SetPosition(1, Rally.Value + Vector3.up * 0.3f);
        }

        ShowProduction(Producing, Progress01);
        if (queue.Count == 0) return;
        progress += Time.deltaTime * (Structure.LowPower ? 0.5f : 1f); // low power: half speed
        if (progress < TimeOf(queue[0])) return;

        int done = queue[0];
        queue.RemoveAt(0);
        progress = 0f;
        if (IsUnit(done)) { SummonUnit(done); Alerts.Show($"Unit ready: {NameOf(done)}{(SquadOf(done) > 1 ? $" x{SquadOf(done)}" : "")}", Alerts.Info); }
        else { ResearchManager.Complete(Upgrade(done)); MatchStats.Add(MatchStats.Kind.Research); Alerts.Show($"Research complete: {NameOf(done)}", Alerts.Good); }
    }

    // Squad units (C&C style) come out one after another, this far apart.
    const float SquadGap = 0.45f;

    void SummonUnit(int index) => StartCoroutine(SummonSquad(index, SquadOf(index)));

    System.Collections.IEnumerator SummonSquad(int index, int squad)
    {
        for (int n = 0; n < squad; n++)
        {
            // Each one gets its own spot around the rally point, so the squad doesn't fight over one spot.
            Vector3? rally = Rally;
            if (rally.HasValue && squad > 1) rally += Quaternion.Euler(0f, n * 360f / squad, 0f) * Vector3.forward * 3f;
            SummonOne(index, rally);
            if (n + 1 < squad) yield return new WaitForSeconds(SquadGap);
        }
    }

    void SummonOne(int index, Vector3? rally)
    {
        Vector2 r = Random.insideUnitCircle * spawnSpread;
        Vector3 exit = spawnPoint.position + new Vector3(r.x, 0f, r.y);
        float floor = padEdge != null ? padEdge.position.y : spawnPoint.position.y;
        Vector3 inside = new(transform.position.x, floor, transform.position.z);
        Vector3? edge = padEdge != null ? padEdge.position + (exit - spawnPoint.position) * 0.5f : null; // follow the spread, roughly

        // Created at the exit (on the navmesh), then walks there from inside the building, then on to the rally point.
        var unit = Net.Spawn(unitPrefabs[index], exit, Quaternion.LookRotation(spawnPoint.position - transform.position));
        MatchStats.Add(MatchStats.Kind.UnitsBuilt);
        if (aerie != null && Flyer.IsAir(unit))
        {
            float launch = aerie.Launch(unit, exit, rally);
            if (TryGetComponent(out NetEntity n) && n.Online) n.RPC_OpenGate(launch); // others see the hatch open
            return;
        }
        unit.WalkOut(inside, exit, exitSpeed, edge, rally);
        float doorTime = Vector3.Distance(inside, exit) / exitSpeed; // shut once it's through
        OpenGate(doorTime);
        if (TryGetComponent(out NetEntity net) && net.Online) net.RPC_OpenGate(doorTime);
    }

    public void OpenGate(float seconds)
    {
        if (gate != null) gate.Open(seconds);
        if (aerie != null) aerie.OpenHatch(seconds);
    }
}
