using UnityEngine;
using UnityEngine.EventSystems;

// Hover a shop button to see what it does. Enter/Exit don't eat scroll or drag, so the strip still scrolls.
public class Tooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string text;

    public void OnPointerEnter(PointerEventData e) { if (TooltipView.Instance != null) TooltipView.Instance.Show(text, (RectTransform)transform); }
    public void OnPointerExit(PointerEventData e) => Hide();
    void OnDisable() => Hide(); // button rebuilt or panel closed while hovered

    void Hide() { if (TooltipView.Instance != null) TooltipView.Instance.Hide(); }

    const string Cyan = "#70FFFF", Grey = "#B8C4D8";

    public static string For(UnitData d)
    {
        string stats = d.damage > 0f
            ? $"HP {d.maxHealth:0}  ·  Damage {d.damage:0} every {d.attackCooldown:0.##}s  ·  Range {d.range:0}m"
            : $"HP {d.maxHealth:0}  ·  Unarmed";
        if (d.moveSpeed > 0f) stats += $"  ·  Speed {d.moveSpeed:0}";
        string traits = "";
        if (d.splash > 0f) traits += $"Splash {d.splash:0}m   ";
        if (d.stunOnHit > 0f) traits += $"Stuns {d.stunOnHit:0.#}s   ";
        if (d.slowOnHit > 0f) traits += $"Slows {d.slowOnHit:0.#}s   ";
        if (d.buildingDamage != 1f) traits += $"x{d.buildingDamage:0.#} vs buildings   ";
        if (d.sight > 0f) traits += $"Sight {d.sight:0}m   ";
        if (traits != "") stats += $"\n<color=#FFD24A>{traits.TrimEnd()}</color>";
        return $"<color={Cyan}><b>{d.displayName}</b></color>   ${d.cost}  ·  {d.buildTime:0}s\n{d.description}\n<color={Grey}>{stats}</color>";
    }

    public static string For(BuildingData d)
    {
        var health = d.prefab.GetComponent<Health>();
        var structure = d.prefab.GetComponent<Structure>();
        int power = structure != null ? structure.power : 0;
        string p = power > 0 ? $"Power +{power}" : power < 0 ? $"Power {power}" : "No power needed";
        return $"<color={Cyan}><b>{d.displayName}</b></color>   ${d.cost}  ·  {d.buildTime:0}s\n{d.description}\n<color={Grey}>HP {health.MaxHealth:0}  ·  {p}</color>";
    }

    public static string For(UpgradeData d) =>
        $"<color={Cyan}><b>{d.displayName}</b></color>   ${d.cost}  ·  {d.researchTime:0}s\n{d.description}\n<color={Grey}>One-time research</color>";
}
