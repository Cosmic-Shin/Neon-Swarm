using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Building : MonoBehaviour
{
    [SerializeField] SelectableUnit[] unitPrefabs;
    [SerializeField] ResearchData[] upgrades;
    [SerializeField] Transform spawnPoint;
    [SerializeField] float spawnSpread = 1.5f;
    [SerializeField] float exitSpeed = 4f;
    [SerializeField] Gate gate;
    [SerializeField] Transform padEdge;

    public Vector3? Rally { get; set; }

    readonly List<int> queue = new();
    float progress;
    LineRenderer rallyLine;
    Structure structure;
    ProductionFX fx;
    AerieFX aerie;

    void Awake()
    {
        unitPrefabs = unitPrefabs.OrderBy(u => u.Cost).ToArray();
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

    public SelectableUnit CheapestFighter() => unitPrefabs.FirstOrDefault(u => u.TryGetComponent(out Attacker _) && !u.TryGetComponent(out Harvester _) && !u.TryGetComponent(out Repairer _));

    public int OptionCount => unitPrefabs.Length + upgrades.Length;
    bool IsUnit(int i) => i < unitPrefabs.Length;
    ResearchData Upgrade(int i) => upgrades[i - unitPrefabs.Length];

    public string NameOf(int i) => IsUnit(i) ? unitPrefabs[i].DisplayName : Upgrade(i).displayName;
    public int SquadOf(int i) => IsUnit(i) ? Mathf.Max(1, unitPrefabs[i].Data.squadSize) : 1;
    public Sprite IconOf(int i) => IsUnit(i) ? unitPrefabs[i].Icon : Upgrade(i).icon;
    public int CostOf(int i) => IsUnit(i) ? unitPrefabs[i].Cost : Upgrade(i).cost;
    public float TimeOf(int i) => IsUnit(i) ? Stats.Value(Stat.BuildTime, unitPrefabs[i].BuildTime, this) : Upgrade(i).researchTime;
    public string TooltipOf(int i)
    {
        string tip = IsUnit(i) ? Tooltip.For(unitPrefabs[i].Data) : Tooltip.For(Upgrade(i));
        string why = Blocker(i);
        return why == null || queue.Contains(i) ? tip : tip + $"\n<color=#FF5050>{why}</color>";
    }
    public bool IsResearched(int i) => !IsUnit(i) && ResearchManager.IsDone(Upgrade(i), Team);

    public int Producing => queue.Count > 0 ? queue[0] : -1;
    public float Progress01 => queue.Count > 0 ? progress / TimeOf(queue[0]) : 0f;
    public void ShowProduction(int i, float progress01) =>
        fx.Show(i < 0 || i >= OptionCount ? null : IsUnit(i) ? unitPrefabs[i] : Upgrade(i).icon, progress01);

    public string Blocker(int i)
    {
        if (IsUnit(i))
        {
            if (unitPrefabs[i].Data.role != UnitData.Role.Elite) return null;
            if (ResearchManager.CoreLevelOf(Team) < 3) return "Needs Core Nexus Mk III";
            return aerie != null && !aerie.HasMast ? "Needs Hive Aerie Mk III" : null;
        }
        if (queue.Exists(q => !IsUnit(q))) return queue.Contains(i) ? "Researching" : "Lab busy: one project at a time";
        return ResearchManager.Blocker(Upgrade(i), this);
    }

    public string Needs(int i)
    {
        string why = Blocker(i);
        if (why == null) return null;
        if (why.StartsWith("Needs ")) return why.Substring(6);
        if (why.StartsWith("Requires ")) return why.Substring(9);
        return null;
    }

    public bool CanQueue(int i) => TeamEconomy.Credits(Team) >= CostOf(i) && Blocker(i) == null;

    public int Team => structure != null ? structure.Health.team : 0;
    public UnitData UnitOf(int i) => IsUnit(i) ? unitPrefabs[i].Data : null;
    public ResearchData ResearchOf(int i) => IsUnit(i) ? null : Upgrade(i);
    public int QueueLength => queue.Count;

    public void Enqueue(int index)
    {
        if (!CanQueue(index) || !TeamEconomy.Spend(Team, CostOf(index))) return;
        queue.Add(index);
        if (!IsUnit(index)) ResearchManager.Started(Upgrade(index), this);
    }

    public void CancelOne(int index)
    {
        int at = queue.LastIndexOf(index);
        if (at < 0) return;
        queue.RemoveAt(at);
        if (at == 0) progress = 0f;
        if (!IsUnit(index)) ResearchManager.Stopped(Upgrade(index), this);
        TeamEconomy.Add(Team, CostOf(index));
    }

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

    void OnDestroy()
    {
        foreach (int q in queue) if (!IsUnit(q)) ResearchManager.Stopped(Upgrade(q), this);
        if (flag != null) Destroy(flag.gameObject);
    }

    public int QueuedCount(int index)
    {
        int n = 0;
        foreach (int i in queue) if (i == index) n++;
        return n;
    }

    void Update()
    {
        bool showRally = Rally.HasValue && structure != null && structure.IsSelected && unitPrefabs.Length > 0;
        rallyLine.enabled = showRally;
        if (showRally)
        {
            rallyLine.SetPosition(0, spawnPoint.position + Vector3.up * 0.3f);
            rallyLine.SetPosition(1, Rally.Value + Vector3.up * 0.3f);
        }
        RallyFlag(showRally);

        ShowProduction(Producing, Progress01);
        if (queue.Count == 0) return;
        progress += Time.deltaTime * (Structure.LowPowerOf(Team) ? 0.5f : 1f);
        if (progress < TimeOf(queue[0])) return;

        int done = queue[0];
        queue.RemoveAt(0);
        progress = 0f;
        bool ours = Team == 0;
        if (IsUnit(done)) { SummonUnit(done); if (ours) Alerts.Show($"Unit ready: {NameOf(done)}{(SquadOf(done) > 1 ? $" x{SquadOf(done)}" : "")}", Alerts.Info); }
        else { ResearchManager.Complete(Upgrade(done), Team); if (ours) { MatchStats.Add(MatchStats.Kind.Research); Alerts.Show($"Research complete: {NameOf(done)}", Alerts.Good) ; Sfx.Say(Line.Research, 4f); } }
    }

    Transform flag;
    LineRenderer pennant;

    void RallyFlag(bool show)
    {
        if (flag == null)
        {
            if (!show) return;
            Color c = ProductionFX.HoloColor(gameObject);
            flag = new GameObject("RallyFlag").transform;
            var pole = flag.gameObject.AddComponent<LineRenderer>();
            pole.useWorldSpace = false;
            pole.sharedMaterial = Juice.AdditiveLine;
            pole.positionCount = 2;
            pole.SetPosition(0, Vector3.zero);
            pole.SetPosition(1, Vector3.up * 3.2f);
            pole.widthMultiplier = 0.08f;
            pole.startColor = pole.endColor = new Color(c.r, c.g, c.b, 0.9f);
            pennant = new GameObject("Pennant").AddComponent<LineRenderer>();
            pennant.transform.SetParent(flag, false);
            pennant.useWorldSpace = false;
            pennant.sharedMaterial = Juice.AdditiveLine;
            pennant.positionCount = 6;
            pennant.widthCurve = AnimationCurve.Linear(0f, 0.9f, 1f, 0.05f);
            pennant.startColor = pennant.endColor = new Color(c.r, c.g, c.b, 0.55f);
            var ring = new GameObject("Ring").AddComponent<LineRenderer>();
            ring.transform.SetParent(flag, false);
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.sharedMaterial = Juice.AdditiveLine;
            ring.positionCount = 24;
            for (int i = 0; i < 24; i++) { float a = i * Mathf.PI / 12f; ring.SetPosition(i, new Vector3(Mathf.Cos(a), 0.08f, Mathf.Sin(a)) * 1.2f); }
            ring.widthMultiplier = 0.08f;
            ring.startColor = ring.endColor = new Color(c.r, c.g, c.b, 0.7f);
            foreach (var lr in flag.GetComponentsInChildren<LineRenderer>()) lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        flag.gameObject.SetActive(show);
        if (!show) return;
        flag.position = Rally.Value;
        if (Camera.main != null) flag.rotation = Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f);
        for (int i = 0; i < 6; i++)
        {
            float x = i * 0.32f;
            pennant.SetPosition(i, new Vector3(x, 2.85f + Mathf.Sin(Time.time * 6f - i * 0.9f) * 0.08f * i, Mathf.Sin(Time.time * 4f - i) * 0.05f * i));
        }
        float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 4f);
        flag.Find("Ring").localScale = new Vector3(pulse, 1f, pulse);
    }

    void OnDisable() { if (flag != null) flag.gameObject.SetActive(false); }

    const float SquadGap = 0.45f;

    void SummonUnit(int index) => StartCoroutine(SummonSquad(index, SquadOf(index)));

    System.Collections.IEnumerator SummonSquad(int index, int squad)
    {
        for (int n = 0; n < squad; n++)
        {
            Vector3? rally = Rally;
            if (rally.HasValue && squad > 1) rally += Quaternion.Euler(0f, n * 360f / squad, 0f) * Vector3.forward * 3f;
            SummonOne(index, rally);
            if (n + 1 < squad) yield return new WaitForSeconds(SquadGap);
        }
    }

    public SelectableUnit Release(SelectableUnit prefab)
    {
        var (exit, inside, edge) = ExitPath();
        var unit = Net.Spawn(prefab, exit, Quaternion.LookRotation(spawnPoint.position - transform.position));
        if (Team != 0) Attacker.SetTeam(unit, Team);
        Sfx.At(Snd.UnitReady, exit, 0.6f);
        unit.WalkOut(inside, exit, exitSpeed, edge, null);
        float doorTime = Vector3.Distance(inside, exit) / exitSpeed;
        Juice.Materialize(unit.gameObject, doorTime * 0.85f);
        OpenGate(doorTime);
        if (TryGetComponent(out NetEntity net) && net.Online) net.RPC_OpenGate(doorTime);
        return unit;
    }

    (Vector3 exit, Vector3 inside, Vector3? edge) ExitPath()
    {
        Vector2 r = Random.insideUnitCircle * spawnSpread;
        Vector3 exit = spawnPoint.position + new Vector3(r.x, 0f, r.y);
        float floor = padEdge != null ? padEdge.position.y : spawnPoint.position.y;
        Vector3 inside = new(transform.position.x, floor, transform.position.z);
        Vector3? edge = padEdge != null ? padEdge.position + (exit - spawnPoint.position) * 0.5f : null;
        return (exit, inside, edge);
    }

    void SummonOne(int index, Vector3? rally)
    {
        var (exit, inside, edge) = ExitPath();
        var unit = Net.Spawn(unitPrefabs[index], exit, Quaternion.LookRotation(spawnPoint.position - transform.position));
        Sfx.At(Snd.UnitReady, exit, 0.6f);
        if (Team == 0) Sfx.Say(Line.Ready, 8f);
        if (Team != 0) Attacker.SetTeam(unit, Team);
        else MatchStats.Add(MatchStats.Kind.UnitsBuilt);
        if (aerie != null && Flyer.IsAir(unit))
        {
            float launch = aerie.Launch(unit, exit, rally);
            if (TryGetComponent(out NetEntity n) && n.Online) n.RPC_OpenGate(launch);
            Juice.Materialize(unit.gameObject, launch * 0.6f);
            return;
        }
        unit.WalkOut(inside, exit, exitSpeed, edge, rally);
        float doorTime = Vector3.Distance(inside, exit) / exitSpeed;
        Juice.Materialize(unit.gameObject, doorTime * 0.85f);
        OpenGate(doorTime);
        if (TryGetComponent(out NetEntity net) && net.Online) net.RPC_OpenGate(doorTime);
    }

    public void OpenGate(float seconds)
    {
        if (gate != null) gate.Open(seconds);
        if (aerie != null) aerie.OpenHatch(seconds);
    }
}
