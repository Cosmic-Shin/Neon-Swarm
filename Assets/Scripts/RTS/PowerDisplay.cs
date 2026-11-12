using UnityEngine;
using UnityEngine.UI;

public class PowerDisplay : MonoBehaviour
{
    [SerializeField] Text label;
    static readonly Color normal = HoloUI.Green, low = HoloUI.Pink, Tight = HoloUI.Yellow;
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
        HoloSkin.Bar(back, fillImage, normal);
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
