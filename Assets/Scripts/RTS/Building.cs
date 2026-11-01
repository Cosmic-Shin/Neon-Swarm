using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Production building: a C&C queue of units and/or one-time research. Options are units first, then upgrades.
public class Building : MonoBehaviour
{
    [SerializeField] SelectableUnit[] unitPrefabs; // one panel button per entry
    [SerializeField] UpgradeData[] upgrades;       // research options (Research Lab)
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

    void Awake()
    {
        unitPrefabs = unitPrefabs.OrderBy(u => u.Cost).ToArray(); // cheapest first
        upgrades = upgrades.OrderBy(u => u.cost).ToArray();
        structure = GetComponent<Structure>();

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
    UpgradeData Upgrade(int i) => upgrades[i - unitPrefabs.Length];

    public string NameOf(int i) => IsUnit(i) ? unitPrefabs[i].DisplayName : Upgrade(i).displayName;
    public Sprite IconOf(int i) => IsUnit(i) ? unitPrefabs[i].Icon : Upgrade(i).icon;
    public int CostOf(int i) => IsUnit(i) ? unitPrefabs[i].Cost : Upgrade(i).cost;
    public float TimeOf(int i) => IsUnit(i) ? unitPrefabs[i].BuildTime : Upgrade(i).researchTime;
    public string TooltipOf(int i) => IsUnit(i) ? Tooltip.For(unitPrefabs[i].Data) : Tooltip.For(Upgrade(i));
    public bool IsResearched(int i) => !IsUnit(i) && Upgrades.IsDone(Upgrade(i));

    public int Producing => queue.Count > 0 ? queue[0] : -1;
    public float Progress01 => queue.Count > 0 ? progress / TimeOf(queue[0]) : 0f;

    // Research: only once, so not while it's already queued or done.
    public bool CanQueue(int i) =>
        ResourceBank.GetOrCreate().Credits >= CostOf(i) && (IsUnit(i) || (!IsResearched(i) && !queue.Contains(i)));

    // Paid up front; refused when short on credits.
    public void Enqueue(int index)
    {
        if (CanQueue(index) && ResourceBank.GetOrCreate().Spend(CostOf(index))) queue.Add(index);
    }

    // Removes the most recently queued one of this type (C&C right-click) and refunds it.
    public void CancelOne(int index)
    {
        int at = queue.LastIndexOf(index);
        if (at < 0) return;
        queue.RemoveAt(at);
        if (at == 0) progress = 0f; // cancelled the one in production
        ResourceBank.GetOrCreate().Add(CostOf(index));
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

        if (queue.Count == 0) return;
        progress += Time.deltaTime * (Structure.LowPower ? 0.5f : 1f); // low power: half speed
        if (progress < TimeOf(queue[0])) return;

        int done = queue[0];
        queue.RemoveAt(0);
        progress = 0f;
        if (IsUnit(done)) { SummonUnit(done); Alerts.Show($"Unit ready: {NameOf(done)}", Alerts.Info); }
        else { Upgrades.Apply(Upgrade(done)); Alerts.Show($"Research complete: {NameOf(done)}", Alerts.Good); }
    }

    void SummonUnit(int index)
    {
        Vector2 r = Random.insideUnitCircle * spawnSpread;
        Vector3 exit = spawnPoint.position + new Vector3(r.x, 0f, r.y);
        float floor = padEdge != null ? padEdge.position.y : spawnPoint.position.y;
        Vector3 inside = new(transform.position.x, floor, transform.position.z);
        Vector3? edge = padEdge != null ? padEdge.position + (exit - spawnPoint.position) * 0.5f : null; // follow the spread, roughly

        // Created at the exit (on the navmesh), then walks there from inside the building, then on to the rally point.
        var unit = Instantiate(unitPrefabs[index], exit, Quaternion.LookRotation(spawnPoint.position - transform.position));
        unit.WalkOut(inside, exit, exitSpeed, edge, Rally);
        if (gate != null) gate.Open(Vector3.Distance(inside, exit) / exitSpeed); // shut once it's through
    }
}
