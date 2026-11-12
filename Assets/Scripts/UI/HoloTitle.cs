using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class HoloTitle : MonoBehaviour
{
    public float ruleWidth = 400f;
    [SerializeField] RectTransform tick, rule;
    Text text;
    string shown;

    public void Build()
    {
        text = GetComponent<Text>();
        if (HoloSkin.BoldFont != null) { text.font = HoloSkin.BoldFont; text.fontStyle = FontStyle.Normal; }
        tick = Part(tick, "TitleTick", "Deco_HeaderTick", HoloUI.Pink, Image.Type.Simple);
        rule = Part(rule, "TitleRule", "Deco_Divider", new Color(HoloUI.Cyan.r, HoloUI.Cyan.g, HoloUI.Cyan.b, 0.55f), Image.Type.Sliced);
        tick.sizeDelta = new Vector2(6f, 22f);
        rule.sizeDelta = new Vector2(ruleWidth, 2f);
        shown = null;
        Place();
    }

    RectTransform Part(RectTransform have, string name, string sprite, Color tint, Image.Type type)
    {
        if (have == null)
        {
            have = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            have.SetParent(transform, false);
        }
        var img = have.GetComponent<Image>();
        HoloUI.SetSliced(img, sprite, tint);
        img.type = type;
        img.raycastTarget = false;
        have.anchorMin = have.anchorMax = new Vector2(0.5f, 0.5f);
        return have;
    }

    static Texture2D fade;

    void Start()
    {
        if (text == null) text = GetComponent<Text>();
        if (!TryGetComponent(out Outline glow)) glow = gameObject.AddComponent<Outline>();
        glow.effectColor = new Color(HoloUI.Pink.r, HoloUI.Pink.g, HoloUI.Pink.b, 0.28f);
        glow.effectDistance = new Vector2(1.5f, -1.5f);

        if (fade == null)
        {
            fade = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int i = 0; i < 64; i++) fade.SetPixel(i, 0, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Sin(i / 63f * Mathf.PI), 1.5f)));
            fade.Apply();
        }
        var parent = transform.parent;
        if (parent.Find(name + "Band") != null) return;
        var band = new GameObject(name + "Band", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement)).GetComponent<RawImage>();
        band.GetComponent<LayoutElement>().ignoreLayout = true;
        band.transform.SetParent(parent, false);
        band.transform.SetSiblingIndex(transform.GetSiblingIndex());
        band.texture = fade;
        band.color = new Color(HoloUI.Pink.r, HoloUI.Pink.g, HoloUI.Pink.b, 0.16f);
        band.raycastTarget = false;
        var br = band.rectTransform;
        var tr = text.rectTransform;
        br.anchorMin = tr.anchorMin; br.anchorMax = tr.anchorMax; br.pivot = tr.pivot;
        br.anchoredPosition = tr.anchoredPosition;
        br.sizeDelta = new Vector2(ruleWidth, Mathf.Min(tr.rect.height, text.fontSize * 1.6f));
        if (tr.anchorMin != tr.anchorMax) br.sizeDelta = new Vector2(ruleWidth - tr.rect.width + tr.sizeDelta.x, br.sizeDelta.y);
    }

    void LateUpdate()
    {
        if (text == null) text = GetComponent<Text>();
        if (text.text != shown) Place();
    }

    void Place()
    {
        if (text == null) text = GetComponent<Text>();
        if (tick == null || rule == null) return;
        shown = text.text;
        var box = text.rectTransform.rect;
        float words = Mathf.Min(text.preferredWidth, box.width), lines = Mathf.Min(text.preferredHeight, box.height);
        float x = text.alignment switch
        {
            TextAnchor.UpperLeft or TextAnchor.MiddleLeft or TextAnchor.LowerLeft => box.xMin + words / 2f,
            TextAnchor.UpperRight or TextAnchor.MiddleRight or TextAnchor.LowerRight => box.xMax - words / 2f,
            _ => box.center.x,
        };
        float y = text.alignment switch
        {
            TextAnchor.UpperLeft or TextAnchor.UpperCenter or TextAnchor.UpperRight => box.yMax - lines / 2f,
            TextAnchor.LowerLeft or TextAnchor.LowerCenter or TextAnchor.LowerRight => box.yMin + lines / 2f,
            _ => box.center.y,
        };
        x -= box.center.x;
        y -= box.center.y;
        tick.gameObject.SetActive(shown.Length > 0);
        tick.anchoredPosition = new Vector2(x - words / 2f - 14f, y);
        rule.anchoredPosition = new Vector2(x, y - lines / 2f - 6f);
    }
}
