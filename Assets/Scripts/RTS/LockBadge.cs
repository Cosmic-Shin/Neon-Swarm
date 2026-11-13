using UnityEngine;
using UnityEngine.UI;

public static class LockBadge
{
    public static readonly Color Red =new(1f, 0.35f, 0.35f);

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
        shade.color = new Color(0.02f, 0f, 0.05f, 0.78f);
        shade.raycastTarget = false;
        var rt = shade.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(6f, 6f);
        rt.offsetMax = new Vector2(-6f, -6f);
        HoloUI.AddLockedStripes(rt);

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
        var outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        var trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(4f, 4f);
        trt.offsetMax = new Vector2(-4f, -4f);
        return shade.transform;
    }
}

public class CostColour : MonoBehaviour
{
    Color? original;
    public Color Original(Text t) => original ??= t.color;
}
