using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HoloButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Color accent = new Color32(0, 229, 212, 255);
    public string index;

    const float ShineTime = 0.45f, Nudge = 6f;
    static Texture2D fade;

    Selectable button;
    RectTransform shine, bar, label;
    RawImage shineImg;
    Image barImg;
    Text number;
    Vector2 labelRest;
    float hoverAt = -10f, hover;
    bool over, built;

    void Awake()
    {
        if (!Application.isPlaying) return;
        button = GetComponent<Selectable>();
        Build();
    }

    void Build()
    {
        built = true;
        if (fade == null)
        {
            fade = new Texture2D(32, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int i = 0; i < 32; i++) fade.SetPixel(i, 0, new Color(1f, 1f, 1f, Mathf.Sin(i / 31f * Mathf.PI)));
            fade.Apply();
        }
        foreach (var t in GetComponentsInChildren<Text>(true)) if (t.name != "HoloIndex") { label = t.rectTransform; break; }
        if (label != null) labelRest = label.anchoredPosition;
        int behindLabel = label != null ? label.GetSiblingIndex() : transform.childCount;

        barImg = Part<Image>("HoloAccent", behindLabel);
        HoloUI.SetSliced(barImg, "Fill_White", accent);
        bar = barImg.rectTransform;
        bar.anchorMin = new Vector2(0f, 0.5f); bar.anchorMax = new Vector2(0f, 0.5f);
        bar.anchoredPosition = new Vector2(10f, 0f);

        shineImg = Part<RawImage>("HoloShine", transform.childCount);
        shineImg.texture = fade;
        shine = shineImg.rectTransform;
        shine.anchorMin = new Vector2(0f, 0f); shine.anchorMax = new Vector2(0f, 1f);
        shine.sizeDelta = new Vector2(70f, -10f);
        shineImg.color = Color.clear;

        if (!string.IsNullOrEmpty(index))
        {
            number = Part<Text>("HoloIndex", transform.childCount);
            number.text = index;
            number.font = HoloSkin.Font != null ? HoloSkin.Font : label != null ? label.GetComponent<Text>().font : null;
            number.fontSize = 13;
            number.alignment = TextAnchor.MiddleLeft;
            number.color = HoloUI.Muted;
            var nr = number.rectTransform;
            nr.anchorMin = nr.anchorMax = new Vector2(0f, 0.5f);
            nr.pivot = new Vector2(0f, 0.5f);
            nr.anchoredPosition = new Vector2(22f, 0f);
            nr.sizeDelta = new Vector2(40f, 20f);
        }
        Apply(0f);
    }

    T Part<T>(string name, int index) where T : Graphic
    {
        if (transform.Find(name) is Transform have && have.TryGetComponent(out T existing)) return existing;
        var go = new GameObject(name, typeof(RectTransform), typeof(T), typeof(LayoutElement));
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        go.transform.SetParent(transform, false);
        go.transform.SetSiblingIndex(Mathf.Clamp(index, 0, transform.childCount - 1));
        var g = go.GetComponent<T>();
        g.raycastTarget = false;
        return g;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (button != null && !button.IsInteractable()) return;
        over = true;
        hoverAt = Time.unscaledTime;
    }

    public void OnPointerExit(PointerEventData e) => over = false;

    void OnDisable() { over = false; hover = 0f; if (built) Apply(0f); }

    void OnDestroy()
    {
        if (!built) return;
        if (label != null) label.anchoredPosition = labelRest;
        foreach (var part in new Component[] { bar, shine, number })
            if (part != null) { if (Application.isPlaying) Destroy(part.gameObject); else DestroyImmediate(part.gameObject); }
    }

    void Update()
    {
        if (!built) return;
        float want = over ? 1f : 0f;
        hover = Mathf.MoveTowards(hover, want, Time.unscaledDeltaTime * 7f);
        Apply(hover * hover * (3f - 2f * hover));

        float s = (Time.unscaledTime - hoverAt) / ShineTime;
        if (s <= 1f)
        {
            float w = ((RectTransform)transform).rect.width;
            shine.anchoredPosition = new Vector2(Mathf.Lerp(-35f, w + 35f, s), 0f);
            shineImg.color = new Color(1f, 1f, 1f, 0.22f * Mathf.Sin(s * Mathf.PI));
        }
        else if (shineImg.color.a != 0f) shineImg.color = Color.clear;
    }

    void Apply(float k)
    {
        bool live = button == null || button.IsInteractable();
        float h = ((RectTransform)transform).rect.height;
        bar.sizeDelta = new Vector2(Mathf.Lerp(3f, 4f, k), Mathf.Max(8f, (h - 22f) * Mathf.Lerp(0.55f, 0.8f, k)));
        barImg.color = new Color(accent.r, accent.g, accent.b, live ? Mathf.Lerp(0.55f, 1f, k) : 0.15f);
        if (label != null) label.anchoredPosition = labelRest + new Vector2(Nudge * k, 0f);
        if (number != null) number.color = Color.Lerp(HoloUI.Muted, accent, k);
    }
}
