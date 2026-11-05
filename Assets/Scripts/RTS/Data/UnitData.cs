using UnityEngine;

// Tunable stats for one unit type, shared by every copy of it (like the course's ItemData).
[CreateAssetMenu(menuName = "RTS/Unit Data", fileName = "NewUnit")]
public class UnitData : ScriptableObject
{
    public enum Role { Worker, Scout, Infantry, Heavy, Support, Elite } // research targets roles; Elite needs Core Nexus Mk III

    public string displayName;
    public Role role = Role.Infantry;
    [TextArea] public string description; // shown in the shop tooltip
    public Sprite icon;
    public int cost = 300;          // credits, paid when queued
    public float buildTime = 5f;    // seconds to produce
    public float maxHealth = 100f;

    [Header("Combat")]
    public float damage = 10f;         // per shot; 0 = can't attack
    public float range = 10f;          // metres
    public float attackCooldown = 1f;  // seconds between shots

    [Header("Role")]
    public float moveSpeed;            // 0 = the prefab's agent speed
    public float sight;                // fog-of-war sight; 0 = default
    public float splash;               // > 0: every enemy this close to the target is hit too
    public float stunOnHit;            // seconds the target can't move or shoot
    public float slowOnHit;            // seconds the target moves at half speed
    public float buildingDamage = 1f;  // damage multiplier against buildings

    [Header("Look")]
    public WeaponVisual visual = new();
}
