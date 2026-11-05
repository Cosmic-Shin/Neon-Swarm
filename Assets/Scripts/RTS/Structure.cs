using System.Collections.Generic;
using UnityEngine;

// Every player building: power it makes (+) or uses (-), selling and repairing.
// Construction turns root scripts off, so a half-built building neither counts for power nor can be sold.
[RequireComponent(typeof(Health))]
public class Structure : MonoBehaviour
{
    public static readonly List<Structure> All = new();

    public BuildingData data;          // name, icon, cost; empty = can't be sold (the HQ)
    public bool isHQ;                  // Core Nexus: its Mk level gates research tiers and elite units
    [SerializeField] string fallbackName = "Building";
    public int power;                  // + produces, - consumes
    public float sight;                // fog-of-war sight past its edge while powered; 0 = default
    [SerializeField] float repairSpeed = 0.05f; // fraction of max health per second

    public bool IsSelected { get; set; }
    public bool Repairing { get; private set; }
    public Health Health { get; private set; }
    public string Name => data != null ? data.displayName : fallbackName;
    public bool CanSell => data != null;
    public int SellValue => data != null ? Mathf.RoundToInt(data.cost * 0.5f * Health.Fraction) : 0;
    int RepairCost => data != null ? data.cost / 2 : 1000; // credits for a full 0-to-max repair

    float owed; // repair cost not yet charged (credits are whole numbers)

    // Ours only (multiplayer: other players' buildings are in All too).
    public static int PowerMade { get { int n = 0; foreach (var s in All) if (s.power > 0 && s.Health.team == 0) n += s.power; return n; } }
    public static int PowerUsed { get { int n = 0; foreach (var s in All) if (s.power < 0 && s.Health.team == 0) n -= s.power; return n; } }
    public static bool LowPower => PowerUsed > PowerMade;

    Refinery refinery; // Rapid Unload research: more credits per load
    float baseCredits;

    void Awake()
    {
        Health = GetComponent<Health>();
        if (TryGetComponent(out refinery)) { baseCredits = refinery.creditsPerUnit; Stats.Changed += RefreshStats; }
    }

    void OnDestroy() => Stats.Changed -= RefreshStats;
    void RefreshStats() => refinery.creditsPerUnit = Stats.Value(Stat.RefineryCredits, baseCredits, this);
    void OnEnable() => All.Add(this);
    void OnDisable() { All.Remove(this); Repairing = false; }

    public void Sell()
    {
        if (!CanSell) return;
        Alerts.Show($"Sold {Name}: +${SellValue}", Alerts.Good);
        ResourceBank.GetOrCreate().Add(SellValue);
        Net.Destroy(gameObject);
    }

    public void ToggleRepair() => Repairing = !Repairing && Health.IsDamaged;

    // Pays for the repair as it heals; stops when full or out of credits.
    void Update()
    {
        if (!Repairing) return;
        if (!Health.IsDamaged) { Repairing = false; return; }
        float heal = Mathf.Min(repairSpeed * Time.deltaTime, 1f - Health.Fraction);
        owed += heal * RepairCost;
        int pay = Mathf.FloorToInt(owed);
        if (pay > 0)
        {
            if (!ResourceBank.GetOrCreate().Spend(pay)) { Repairing = false; return; }
            owed -= pay;
        }
        Health.Heal(heal * Health.MaxHealth);
    }
}
