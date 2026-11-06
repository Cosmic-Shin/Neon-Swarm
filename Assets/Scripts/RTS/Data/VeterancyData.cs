using UnityEngine;

// The veterancy ranks, kills needed and bonuses. One asset in Resources, shared by every unit and turret.
[CreateAssetMenu(menuName = "RTS/Veterancy Data", fileName = "Veterancy")]
public class VeterancyData : ScriptableObject
{
    [System.Serializable]
    public class Rank
    {
        public string name;
        public int kills;                 // total kills to reach this rank
        public float selfRepair;          // fraction of max HP healed per second
        public StatMod[] bonuses;         // only stat/percent/flat are used
    }

    public Rank[] ranks;
    public float glowPerRank = 0.4f;      // neon emission x(1 + this * rank)
    public GameObjectEventChannel promoted; // raised with the unit when one of ours ranks up

    static VeterancyData instance;
    public static VeterancyData Get => instance ? instance : instance = Resources.Load<VeterancyData>("Veterancy");
}
