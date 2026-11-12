using UnityEngine;

[CreateAssetMenu(menuName = "RTS/Veterancy Data", fileName = "Veterancy")]
public class VeterancyData : ScriptableObject
{
    [System.Serializable]
    public class Rank
    {
        public string name;
        public int kills;
        public float selfRepair;
        public StatMod[] bonuses;
    }

    public Rank[] ranks;
    public float glowPerRank = 0.4f;
    public GameObjectEventChannel promoted;

    static VeterancyData instance;
    public static VeterancyData Get => instance ? instance : instance = Resources.Load<VeterancyData>("Veterancy");
}
