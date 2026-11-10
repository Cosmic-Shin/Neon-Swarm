using UnityEngine;

// How an AI opponent plays: its build order, the units it likes, when and how big it attacks, how fast it thinks.
// One asset per difficulty in Resources/AI (Easy, Normal, Hard). Tune the AI here, not in code.
[CreateAssetMenu(menuName = "RTS/AI Profile", fileName = "AIProfile")]
public class AIProfile : ScriptableObject
{
    [System.Serializable]
    public class UnitPick
    {
        public UnitData unit;
        [Min(0f)] public float weight = 1f; // how often it's picked compared to the others
    }

    [Header("Thinking")]
    [Tooltip("Seconds between decisions. Lower = reacts faster.")]
    public float thinkInterval = 1.5f;
    [Tooltip("Extra income from harvesting (0.15 = +15%). The only thing an AI gets that you don't.")]
    public float incomeBonus;

    [Header("Base")]
    [Tooltip("Built in this order; a building listed twice means two of them. Lost ones are rebuilt.")]
    public BuildingData[] buildOrder;
    [Tooltip("After the build order: these, round and round, while credits allow.")]
    public BuildingData[] lateGame;
    [Tooltip("Share of its income (and starting credits) kept for units; the rest pays for buildings and harvesters.")]
    [Range(0f, 1f)] public float armyShare = 0.5f;
    public int harvesters = 4;

    [Header("Army")]
    public UnitPick[] units;
    [Tooltip("Seconds into the match before the first attack.")]
    public float firstAttack = 240f;
    [Tooltip("Army worth (credits) before it attacks; grows after each wave.")]
    public int waveValue = 3000;
    public float waveGrowth = 1.25f;
    [Tooltip("Enemies this close to its base make it pull its army home.")]
    public float defendRadius = 55f;

    [Header("Tech")]
    [Tooltip("After the build order: upgrade the Core Nexus (research tiers, elite units), then other buildings.")]
    public bool upgrades = true;
    [Tooltip("After the build order: research in its labs.")]
    public bool research = true;

    [Header("Smarts")]
    [Tooltip("0 = ignores what enemies field; 1 = buys hard counters to their armour and air.")]
    [Range(0f, 1f)] public float counterPick = 0.5f;
    [Tooltip("Ore its refineries can reach below this: pylons out to the nearest safe field and a refinery there. 0 = never.")]
    public float expandBelow = 2000f;
    [Tooltip("Seconds between raids on enemy harvesters with its fastest units. 0 = never.")]
    public float harassEvery;
    public int harassSize = 3;
}
