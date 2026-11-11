using UnityEngine;
using UnityEngine.UI;

// Life for a Holo panel (added by HoloSkin): faint scanlines, a light band sweeping down every few seconds, a
// slowly breathing outer glow, pulsing corner brackets and (menus) a quick fade/scale-in when it opens.
// Everything is built at runtime from two tiny generated textures, so nothing extra is saved in the scenes.
// Decoration only: no raycasts, ignored by layout groups, drawn between the panel sprite and the content.
public class HoloFX : MonoBehaviour
{
    public Color tint = new Color32(0, 229, 212, 255);
    public bool intro = true, aura = true;
    [Range(0f, 1f)] public float strength = 1f; // HUD panels run quieter than menus
    public string code, caption; // menus: a panel code top-left, a status caption with a blinking light bottom-right

    const float IntroTime = 0.22f, SweepEvery = 6f, SweepTime = 1.8f, BandHeight = 140f;
    static Texture2D scanTex, bandTex;

    RectTransform rt, sweep;
    RawImage scan, band;
    Image glow;
    Image[] brackets;
    Image led;
    CanvasGroup group;
    float openedAt, phase;
    bool built, settled;

    void Awake()
    {
        if (!Application.isPlaying) return;
        rt = (RectTransform)transform;
        phase = Random.value * SweepEvery; // panels don't all sweep together
        Build();
    }

    void OnEnable()
    {
        openedAt = Time.unscaledTime;
        settled = false;
    }

    void Build()
    {
        if (built) return;
        built = true;
        Textures();
        var bg = transform.Find("HoloBG");
        int above = bg != null ? bg.GetSiblingIndex() + 1 : 0;

        scan = Child<RawImage>("HoloScan", above);
        Stretch(scan.rectTransform, 10f);
        scan.texture = scanTex;
        scan.color = new Color(tint.r, tint.g, tint.b, 0.05f * strength);

        band = Child<RawImage>("HoloSweep", above + 1);
        sweep = band.rectTransform;
        sweep.anchorMin = new Vector2(0f, 1f); sweep.anchorMax = new Vector2(1f, 1f);
        sweep.pivot = new Vector2(0.5f, 0.5f);
        sweep.offsetMin = new Vector2(10f, 0f); sweep.offsetMax = new Vector2(-10f, 0f);
        sweep.sizeDelta = new Vector2(sweep.sizeDelta.x, BandHeight);
        band.texture = bandTex;
        band.color = new Color(tint.r, tint.g, tint.b, 0f);

        if (aura)
        {
            glow = Child<Image>("HoloAura", 0); // behind the panel sprite
            HoloUI.SetSliced(glow, "Outline_White", new Color(tint.r, tint.g, tint.b, 0f));
            glow.raycastTarget = false;
            Stretch(glow.rectTransform, -16f);
        }

        var found = new System.Collections.Generic.List<Image>();
        foreach (Transform c in transform) if (c.name == "Bracket" && c.TryGetComponent(out Image b)) found.Add(b);
        brackets = found.ToArray();

        if (!string.IsNullOrEmpty(code)) Caption("HoloCode", code, new Vector2(0f, 1f), new Vector2(40f, -16f), TextAnchor.UpperLeft); // clear of the chamfered corner
        if (!string.IsNullOrEmpty(caption))
        {
            Caption("HoloCaption", caption, new Vector2(1f, 0f), new Vector2(-48f, 14f), TextAnchor.LowerRight);
            led = Child<Image>("HoloLed", transform.childCount);
            HoloUI.SetSliced(led, "Fill_White", HoloUI.Green);
            var lr = led.rectTransform;
            lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(1f, 0f);
            lr.anchoredPosition = new Vector2(-34f, 18f);
            lr.sizeDelta = new Vector2(6f, 6f);
        }

        if (intro && !TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
    }

    void Update()
    {
        if (!built) return;
        float t = Time.unscaledTime;

        if (intro && !settled)
        {
            float k = Mathf.Clamp01((t - openedAt) / IntroTime);
            float ease = 1f - (1f - k) * (1f - k) * (1f - k);
            group.alpha = ease;
            rt.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, ease);
            if (k >= 1f) { settled = true; rt.localScale = Vector3.one; }
        }

        // The sweep: a soft band runs from the top to the bottom edge, then rests.
        float s = (t + phase) % SweepEvery / SweepTime;
        float h = rt.rect.height;
        if (s < 1f)
        {
            sweep.anchoredPosition = new Vector2(0f, Mathf.Lerp(BandHeight * 0.5f, -h - BandHeight * 0.5f, s));
            band.color = new Color(tint.r, tint.g, tint.b, 0.09f * strength * Mathf.Sin(s * Mathf.PI));
        }
        else if (band.color.a != 0f) band.color = new Color(tint.r, tint.g, tint.b, 0f);

        float breathe = 0.5f + 0.5f * Mathf.Sin(t * 1.6f + phase);
        if (glow != null) glow.color = new Color(tint.r, tint.g, tint.b, (0.10f + 0.12f * breathe) * strength);
        foreach (var b in brackets) if (b != null) b.color = new Color(b.color.r, b.color.g, b.color.b, 0.55f + 0.45f * breathe);
        if (led != null) led.color = new Color(HoloUI.Green.r, HoloUI.Green.g, HoloUI.Green.b, (t + phase) % 1.4f < 1.1f ? 1f : 0.2f);
    }

    // Reuses a child left by a copy of this panel (the skirmish screen is a runtime copy of the lobby).
    T Child<T>(string name, int index) where T : Graphic
    {
        if (transform.Find(name) is Transform have && have.TryGetComponent(out T existing)) return existing;
        var go = new GameObject(name, typeof(RectTransform), typeof(T), typeof(LayoutElement));
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        go.transform.SetParent(transform, false);
        go.transform.SetSiblingIndex(Mathf.Min(index, transform.childCount - 1));
        var g = go.GetComponent<T>();
        g.raycastTarget = false;
        return g;
    }

    void Caption(string name, string text, Vector2 corner, Vector2 offset, TextAnchor align)
    {
        var t = Child<Text>(name, transform.childCount);
        t.text = text;
        t.font = HoloSkin.Font != null ? HoloSkin.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 11;
        t.alignment = align;
        t.color = new Color(HoloUI.Muted.r, HoloUI.Muted.g, HoloUI.Muted.b, 0.7f);
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        var r = t.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = corner;
        r.anchoredPosition = offset;
        r.sizeDelta = new Vector2(220f, 16f);
    }

    static void Stretch(RectTransform r, float inset)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
    }

    void LateUpdate()
    {
        // Scanlines: one bright row in three, kept 1:1 with screen pixels whatever the panel size.
        if (scan != null) scan.uvRect = new Rect(0f, 0f, 1f, scan.rectTransform.rect.height / 3f);
    }

    static void Textures()
    {
        if (scanTex != null) return;
        scanTex = new Texture2D(1, 3, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
        scanTex.SetPixels(new[] { Color.white, new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0f) });
        scanTex.Apply();
        const int n = 64;
        bandTex = new Texture2D(1, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        for (int i = 0; i < n; i++)
        {
            float y = i / (n - 1f);
            float a = Mathf.Pow(Mathf.Sin(y * Mathf.PI), 2f) * (1f - 0.6f * y); // brighter leading (lower) edge
            bandTex.SetPixel(0, i, new Color(1f, 1f, 1f, a));
        }
        bandTex.Apply();
    }
}
