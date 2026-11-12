using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class GlitchSplash : MonoBehaviour
{
    public Sprite image;
    public float hold = 2.5f;
    public int slices = 14;
    public float maxSliceShift = 70f, maxSplit = 18f;
    public float contentOffset = 0.185f;

    static bool shown;

    GameObject canvas, title;
    CanvasGroup group;
    RectTransform cyan, red;
    readonly List<RectTransform> strips = new();
    readonly List<Image> stripImages = new();
    bool skip;

    void Awake()
    {
        if (shown || image == null) { enabled = false; return; }
        shown = true;
        if (transform.Find("Title") is Transform t) { title = t.gameObject; title.SetActive(false); }
        Build();
        StartCoroutine(Run());
    }

    void Build()
    {
        canvas = new GameObject("GlitchSplash", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = canvas.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 1000;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var back = Make<Image>("Back", canvas.transform);
        Stretch(back.rectTransform);
        back.color = new Color(0.05f, 0.05f, 0.05f, 1f);

        var vignette = Make<RawImage>("Vignette", back.transform);
        Stretch(vignette.rectTransform);
        var v = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float d = new Vector2(x / 63f - 0.5f, y / 63f - 0.5f).magnitude * 1.6f;
                v.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.Clamp01(d * d) * 0.8f));
            }
        v.Apply();
        vignette.texture = v;

        var frame = new GameObject("Frame", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        frame.SetParent(canvas.transform, false);
        frame.sizeDelta = new Vector2(1860f, 1860f * image.rect.height / image.rect.width);
        frame.anchoredPosition = new Vector2(1860f * contentOffset, 0f);
        group = frame.GetComponent<CanvasGroup>();
        group.alpha = 0f;

        red = Ghost(frame, new Color(1f, 0.15f, 0.2f, 0.85f));
        cyan = Ghost(frame, new Color(0.1f, 1f, 0.95f, 0.85f));

        float h = 1f / slices;
        for (int i = 0; i < slices; i++)
        {
            var strip = new GameObject("Slice" + i, typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            strip.SetParent(frame, false);
            strip.anchorMin = new Vector2(0f, 1f - (i + 1) * h);
            strip.anchorMax = new Vector2(1f, 1f - i * h);
            strip.offsetMin = new Vector2(0f, -0.5f);
            strip.offsetMax = new Vector2(0f, 0.5f);
            var img = Make<Image>("Img", strip);
            img.sprite = image;
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, -(slices - 1 - i));
            rt.anchorMax = new Vector2(1f, i + 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            strips.Add(rt);
            stripImages.Add(img);
        }

        var scan = Make<RawImage>("Scanlines", canvas.transform);
        Stretch(scan.rectTransform);
        var tex = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        tex.SetPixels(new[] { new Color(0f, 0f, 0f, 0.35f), Color.clear, Color.clear, Color.clear });
        tex.Apply();
        scan.texture = tex;
        scan.uvRect = new Rect(0f, 0f, 1f, 1080f / 4f);
    }

    RectTransform Ghost(RectTransform parent, Color c)
    {
        var img = Make<Image>("Ghost", parent);
        img.sprite = image;
        img.color = c;
        Stretch(img.rectTransform);
        return img.rectTransform;
    }

    static T Make<T>(string name, Transform parent) where T : Graphic
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(T)).GetComponent<T>();
        g.transform.SetParent(parent, false);
        g.raycastTarget = false;
        return g;
    }

    static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if ((kb != null && kb.anyKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame)) skip = true;
    }

    void Glitch(float amount)
    {
        Vector2 split = Random.insideUnitCircle * maxSplit * amount;
        cyan.anchoredPosition = -split;
        red.anchoredPosition = split;
        for (int i = 0; i < strips.Count; i++)
        {
            float dx = Random.value < 0.35f * Mathf.Max(0.3f, amount) ? Random.Range(-maxSliceShift, maxSliceShift) * amount : 0f;
            strips[i].anchoredPosition = new Vector2(dx, 0f);
            stripImages[i].color = Random.value < 0.08f * amount ? new Color(0.6f, 1f, 0.95f) : Color.white;
        }
    }

    void Settle()
    {
        cyan.anchoredPosition = new Vector2(-3f, 1f);
        red.anchoredPosition = new Vector2(3f, -1f);
        foreach (var s in strips) s.anchoredPosition = Vector2.zero;
        foreach (var i in stripImages) i.color = Color.white;
    }

    IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds && !skip; t += Time.unscaledDeltaTime) yield return null;
    }

    IEnumerator Burst(float length, float amount, bool calm)
    {
        Sfx.Play(Snd.Zap, calm ? 0.25f : 0.5f);
        for (float t = 0f; t < length; t += 0.04f)
        {
            Glitch(amount);
            if (!calm) group.alpha = Random.value < 0.2f ? 0.6f : 1f;
            yield return new WaitForSecondsRealtime(0.04f);
        }
        group.alpha = 1f;
        Settle();
    }

    IEnumerator Run()
    {
        bool calm = GameSettings.ReduceFlashing;
        float scale = calm ? 0.25f : 1f;
        yield return Wait(0.35f);

        if (!skip)
        {
            Sfx.Play(Snd.Zap, calm ? 0.3f : 0.6f);
            const float intro = 0.9f;
            for (float t = 0f; t < intro && !skip; t += Time.unscaledDeltaTime)
            {
                float p = t / intro;
                group.alpha = calm ? p : (Random.value < 0.3f + 0.7f * p ? Mathf.Lerp(0.3f, 1f, p) : 0.05f);
                Glitch((1f - p) * scale);
                yield return null;
            }
            group.alpha = 1f;
            Settle();
        }

        for (float t = 0f; t < hold && !skip;)
        {
            float gap = Random.Range(0.7f, 1.3f) * (calm ? 2f : 1f);
            yield return Wait(gap);
            t += gap;
            if (skip || t >= hold) break;
            float len = Random.Range(0.12f, 0.25f);
            yield return Burst(len, scale * 0.7f, calm);
            t += len;
        }

        Sfx.Play(Snd.Zap, calm ? 0.3f : 0.6f);
        const float outro = 0.45f;
        float start = group.alpha;
        for (float t = 0f; t < outro; t += Time.unscaledDeltaTime)
        {
            float p = t / outro;
            Glitch(p * scale);
            group.alpha = Mathf.Lerp(start, 0f, p) * (calm || Random.value > 0.25f ? 1f : 0.4f);
            yield return null;
        }

        var whole = canvas.AddComponent<CanvasGroup>();
        if (title != null) title.SetActive(true);
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            whole.alpha = 1f - t / 0.35f;
            yield return null;
        }
        Destroy(canvas);
        enabled = false;
    }
}
