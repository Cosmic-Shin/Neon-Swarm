using UnityEngine;

// Counters, C&C style: how hard each kind of weapon hits each kind of armour.
// One asset, Resources/DamageTable; edit the numbers there.
[CreateAssetMenu(menuName = "RTS/Damage Table", fileName = "DamageTable")]
public class DamageTable : ScriptableObject
{
    public enum Armor { Light, Heavy, Structure }
    public enum Weapon { General, Gun, Cannon, Explosive }

    [System.Serializable]
    public class Row
    {
        public Weapon weapon;
        public float vsLight = 1f, vsHeavy = 1f, vsStructure = 1f;
    }

    public Row[] rows;

    static DamageTable instance;
    static DamageTable Instance => instance != null ? instance : instance = Resources.Load<DamageTable>("DamageTable");

    public static float Factor(Weapon w, Health target)
    {
        if (target == null) return 1f;
        return Factor(w, ArmorOf(target));
    }

    public static float Factor(Weapon w, Armor a)
    {
        var table = Instance;
        if (table == null) return 1f;
        foreach (var r in table.rows)
            if (r.weapon == w) return a == Armor.Light ? r.vsLight : a == Armor.Heavy ? r.vsHeavy : r.vsStructure;
        return 1f;
    }

    public static Armor ArmorOf(Health h) =>
        h.TryGetComponent(out SelectableUnit u) && u.Data != null ? u.Data.armor : Armor.Structure;

    // Tooltip line: "Gun: strong vs Light, weak vs Heavy, Structure".
    public static string Describe(Weapon w)
    {
        var table = Instance;
        if (table == null || w == Weapon.General) return null;
        foreach (var r in table.rows)
        {
            if (r.weapon != w) continue;
            string strong = "", weak = "";
            void Sort(string name, float f) { if (f > 1.01f) strong += (strong == "" ? "" : ", ") + name; else if (f < 0.99f) weak += (weak == "" ? "" : ", ") + name; }
            Sort("Light", r.vsLight); Sort("Heavy", r.vsHeavy); Sort("Buildings", r.vsStructure);
            return $"{w}: " + (strong != "" ? $"strong vs {strong}" : "") + (strong != "" && weak != "" ? ", " : "") + (weak != "" ? $"weak vs {weak}" : "");
        }
        return null;
    }
}
