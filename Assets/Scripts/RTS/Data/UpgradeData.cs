using System.Collections.Generic;
using UnityEngine;

// One research option (Research Lab). Each can be researched once.
[CreateAssetMenu(menuName = "RTS/Upgrade Data", fileName = "NewUpgrade")]
public class UpgradeData : ScriptableObject
{
    public enum Kind { Damage, Armor, Harvest }

    public string displayName;
    [TextArea] public string description; // shown in the shop tooltip
    public Sprite icon;
    public int cost = 1000;
    public float researchTime = 30f;
    public Kind kind;
    public float amount = 0.25f; // +25%
}

// Player-wide bonuses from research. Multipliers start at 1.
public static class Upgrades
{
    public static float Damage = 1f, Armor = 1f, Harvest = 1f;
    static readonly HashSet<UpgradeData> done = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { Damage = Armor = Harvest = 1f; done.Clear(); } // fresh each play session

    public static bool IsDone(UpgradeData u) => done.Contains(u);

    public static void Apply(UpgradeData u)
    {
        if (!done.Add(u)) return;
        switch (u.kind)
        {
            case UpgradeData.Kind.Damage:
                Damage += u.amount; // Attacker/Turret read it per shot
                break;
            case UpgradeData.Kind.Armor:
                float k = (Armor + u.amount) / Armor;
                Armor += u.amount; // new units read it in Health.Awake
                foreach (var h in Health.All)
                    if (h.team == 0 && h.TryGetComponent(out SelectableUnit _)) h.ScaleMax(k);
                break;
            case UpgradeData.Kind.Harvest:
                float m = (Harvest + u.amount) / Harvest;
                Harvest += u.amount; // new harvesters read it in SelectableUnit.Awake
                foreach (var hv in Object.FindObjectsByType<Harvester>(FindObjectsSortMode.None)) hv.harvestRate *= m;
                break;
        }
    }
}
