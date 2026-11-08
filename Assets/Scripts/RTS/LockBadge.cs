using UnityEngine;
using UnityEngine.UI;

// On a shop button: what's still needed to buy it. Locked = darkened, with "LOCKED" and the missing
// requirement written on it ("NEEDS Research Lab"). The price turns red while you can't afford it.
// Used by the unit/research panel (BuildingPanel) and the build menu (BuildingPlacer).
public static class LockBadge
{
    static readonly Color Red = new(1f, 0.35f, 0.35f);

    // needs: what's missing (null = nothing, unlocked). canAfford: colours the price.
    public static void Set(Button button, string needs, bool canAfford)
    {
        var costT = button.transform.Find("Cost");
        if (costT != null && costT.TryGetComponent(out Text cost))
        {
            if (!costT.TryGetComponent(out CostColour normal)) normal = costT.gameObject.AddComponent<CostColour>();
            cost.color = canAfford ? normal.Original(cost) : Red;
        }

        var badge = button.transform.Find("LockBadge");
        if (needs == null) { if (badge != null) badge.gameObject.SetActive(false); return; }
        if (badge == null) badge = Make(button);
        badge.gameObject.SetActive(true);
        badge.GetComponentInChildren<Text>().text = $"<color=#FF5A5A><b>LOCKED</b></color>\n<size=12>NEEDS</size>\n{needs}";
    }

    static Transform Make(Button button)
    {
        var shade = new GameObject("LockBadge", typeof(RectTransform)).AddComponent<Image>();
        shade.transform.SetParent(button.transform, false);
        shade.color = new Color(0.02f, 0f, 0.05f, 0.85f);
        shade.raycastTarget = false; // hover still reaches the button (tooltip)
        var rt = shade.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(3f, 3f);
        rt.offsetMax = new Vector2(-3f, -3f);

        var font = button.GetComponentInChildren<Text>(true);
        var text = new GameObject("Needs", typeof(RectTransform)).AddComponent<Text>();
        text.transform.SetParent(shade.transform, false);
        text.font = font != null ? font.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 15;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        var trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(4f, 4f);
        trt.offsetMax = new Vector2(-4f, -4f);
        return shade.transform;
    }
}

// Remembers a price label's normal colour, so it can go back from red.
public class CostColour : MonoBehaviour
{
    Color? original;
    public Color Original(Text t) => original ??= t.color;
}
