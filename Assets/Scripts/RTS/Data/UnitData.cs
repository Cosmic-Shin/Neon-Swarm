using UnityEngine;

[CreateAssetMenu(menuName = "RTS/Unit Data", fileName = "NewUnit")]
public class UnitData : ScriptableObject
{
    public enum Role { Worker, Scout, Infantry, Heavy, Support, Elite }

    public string displayName;
    public Role role = Role.Infantry;
    [TextArea] public string description;
    public Sprite icon;
    public int cost = 300;
    public float buildTime = 5f;
    [Min(1)] public int squadSize = 1;
    public float maxHealth = 100f;

    [Header("Combat")]
    public float damage = 10f;
    public float range = 10f;
    public float attackCooldown = 1f;
    public bool hitsGround = true;
    public bool hitsAir;

    [Header("Role")]
    public float moveSpeed;
    public float sight;
    public float splash;
    public float stunOnHit;
    public float slowOnHit;
    public float buildingDamage = 1f;

    [Header("Counters (DamageTable)")]
    public DamageTable.Armor armor = DamageTable.Armor.Light;
    public DamageTable.Weapon weapon = DamageTable.Weapon.General;

    public enum Ability { None, Brood, Overheat, Charge }
    [Tooltip("Brood: Queen hatches Skitters (extra). Overheat: fires in bursts. Charge: dashes at far targets.")]
    public Ability ability;

    [Header("Look")]
    public WeaponVisual visual = new();
    public enum WeaponStyle { Shot, FlameStream, PodDrop, ShockPulse }
    public WeaponStyle style;
    [Tooltip("Regex for model parts the shots leave from (all of them fire; damage is split). Empty = any part named *Muzzle*.")]
    public string muzzles;
    public GameObject extra;
}
