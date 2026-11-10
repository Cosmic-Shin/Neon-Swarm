using UnityEngine;
using UnityEngine.UI;

// "POWER used/made" next to the credits, with a meter along the bottom of the box: how much of the power made
// is used. Green with room to spare, yellow near the limit, flashing red when low (towers offline, production at half speed).
public class PowerDisplay : MonoBehaviour
{
    [SerializeField] Text label;
    [SerializeField] Color normal = new(0.55f, 1f, 0.2f);
    [SerializeField] Color low = new(1f, 0.2f, 0.25f);

    static readonly Color Tight = new(1f, 0.85f, 0.2f);
    RectTransform fill;
    Image fillImage;

    void Awake()
    {
        var back = new GameObject("Meter", typeof(RectTransform)).AddComponent<Image>();
        back.transform.SetParent(transform, false);
        back.color = new Color(0.08f, 0.1f, 0.16f, 1f);
        back.raycastTarget = false;
        var r = back.rectTransform;
        r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(1f, 0f);
        r.offsetMin = new Vector2(8f, 6f); r.offsetMax = new Vector2(-8f, 12f);
        fillImage = new GameObject("Fill", typeof(RectTransform)).AddComponent<Image>();
        fillImage.transform.SetParent(back.transform, false);
        fillImage.raycastTarget = false;
        fill = fillImage.rectTransform;
        fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
    }

    void LateUpdate()
    {
        int used = Structure.PowerUsed, made = Structure.PowerMade;
        bool isLow = used > made;
        float use = made > 0 ? (float)used / made : used > 0 ? 1f : 0f;
        bool blink = isLow && Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.4f;
        label.text = isLow ? $"LOW POWER  {used}/{made}" : $"POWER  {used}/{made}";
        label.color = isLow ? (blink ? Color.white : low) : normal;
        fill.anchorMax = new Vector2(Mathf.Clamp01(use), 1f);
        fillImage.color = isLow ? (blink ? Color.white : low) : use > 0.8f ? Tight : normal;
    }
}
