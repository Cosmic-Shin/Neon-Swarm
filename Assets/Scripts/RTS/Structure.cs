using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class Structure : MonoBehaviour
{
    public static readonly List<Structure> All = new();

    public BuildingData data;
    public bool isHQ;
    [SerializeField] string fallbackName = "Building";
    public int power;
    public float sight;
    [SerializeField] float repairSpeed = 0.05f;

    public bool IsSelected { get; set; }
    public bool Repairing { get; private set; }
    public bool RemoteRepairing { get; set; }
    public Health Health { get; private set; }
    public string Name => data != null ? data.displayName : fallbackName;
    public bool CanSell => data != null;
    public int SellValue => data != null ? Mathf.RoundToInt(data.cost * 0.5f * Health.Fraction) : 0;
    int RepairCost => data != null ? data.cost / 2 : 1000;

    float owed;

    public static int PowerMade => PowerMadeOf(0);
    public static int PowerUsed => PowerUsedOf(0);
    public static bool LowPower => LowPowerOf(0);
    public static int PowerMadeOf(int team) { int n = 0; foreach (var s in All) if (s.power > 0 && s.Health.team == team) n += s.power; return n; }
    public static int PowerUsedOf(int team) { int n = 0; foreach (var s in All) if (s.power < 0 && s.Health.team == team) n -= s.power; return n; }
    public static bool LowPowerOf(int team) => PowerUsedOf(team) > PowerMadeOf(team);

    Refinery refinery;
    float baseCredits;

    void Awake()
    {
        Health = GetComponent<Health>();
        gameObject.AddComponent<BuildingFX>();
        gameObject.AddComponent<BuildingLife>();
        if (TryGetComponent(out refinery)) { baseCredits = refinery.creditsPerUnit; Stats.Changed += RefreshStats; }
    }

    void OnDestroy() => Stats.Changed -= RefreshStats;
    void RefreshStats() => refinery.creditsPerUnit = Stats.Value(Stat.RefineryCredits, baseCredits, this);
    void OnEnable() => All.Add(this);
    void OnDisable() { All.Remove(this); Repairing = false; }

    public void Sell()
    {
        if (!CanSell) return;
        if (Health.team == 0) Alerts.Show($"Sold {Name}: +${SellValue}", Alerts.Good);
        TeamEconomy.Add(Health.team, SellValue);
        Juice.Sell(gameObject, SellValue);
        Net.Destroy(gameObject);
    }

    public void ToggleRepair() => Repairing = !Repairing && Health.IsDamaged;

    void Update()
    {
        if (!Repairing) return;
        if (!Health.IsDamaged) { Repairing = false; return; }
        float heal = Mathf.Min(repairSpeed * Time.deltaTime, 1f - Health.Fraction);
        owed += heal * RepairCost;
        int pay = Mathf.FloorToInt(owed);
        if (pay > 0)
        {
            if (!TeamEconomy.Spend(Health.team, pay)) { Repairing = false; return; }
            owed -= pay;
        }
        Health.Heal(heal * Health.MaxHealth);
    }
}
