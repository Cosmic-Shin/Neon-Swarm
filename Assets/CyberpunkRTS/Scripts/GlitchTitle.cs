using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class GlitchTitle : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("Overall size multiplier (1 = 474 x 196 px on a 1080p reference canvas).")]
    public float scale = 1f;
    public bool showSubtitle = true;
    public float subtitleGap = -18f;

    [Header("Idle")]
    public Vector2 idleSplit = new Vector2(4f, 2f);
    public float glowPulseSpeed = 1.2f;

    [Header("Glitch bursts")]
    public Vector2 burstInterval = new Vector2(2.5f, 6f);
    public Vector2 burstLength = new Vector2(0.15f, 0.35f);
    public int slices = 8;
    public float maxSliceShift = 28f;
    public float maxSplit = 14f;

    [Header("Intro")]
    public bool playIntro = true;
    public float introTime = 0.8f;
    public float subtitleTypeTime = 0.6f;

    RectTransform root;
    Image glow, cyan, pink, sub;
    CanvasGroup group;
    readonly List<RectTransform> sliceImgs = new List<RectTransform>();
    readonly List<Image> sliceImages = new List<Image>();
    Vector2 size;
    bool built, bursting;
    Coroutine loop;

    static Sprite S(string n) => Resources.Load<Sprite>("HoloUI/Title/" + n);

    void Build()
    {
        if (built) return;
        built = true;
        root = (RectTransform)transform;
        var white = S("Title_White");
        if (!white) { Debug.LogWarning("[GlitchTitle] Missing Resources/HoloUI/Title sprites"); enabled = false; return; }
        size = white.rect.size / (white.pixelsPerUnit / 100f) * scale;
        root.sizeDelta = size;
        if (!TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false; group.interactable = false;

        glow = Layer("Glow", S("Title_Glow"), root);
        cyan = Layer("Cyan", S("Title_Cyan"), root);
        pink = Layer("Pink", S("Title_Pink"), root);
        cyan.color = new Color(1, 1, 1, 0.85f);
        pink.color = new Color(1, 1, 1, 0.85f);

        var whiteRoot = new GameObject("White", typeof(RectTransform)).GetComponent<RectTransform>();
        whiteRoot.SetParent(root, false); Fill(whiteRoot);
        float h = size.y / Mathf.Max(1, slices);
        for (int i = 0; i < slices; i++)
        {
            var strip = new GameObject("Slice" + i, typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            strip.SetParent(whiteRoot, false);
            strip.anchorMin = new Vector2(0, 1); strip.anchorMax = new Vector2(1, 1); strip.pivot = new Vector2(0.5f, 1);
            strip.sizeDelta = new Vector2(0, h + 0.5f);
            strip.anchoredPosition = new Vector2(0, -i * h);
            var img = Layer("Img", white, strip);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(0, i * h);
            sliceImgs.Add(rt); sliceImages.Add(img);
        }

        if (showSubtitle && S("Title_Subtitle"))
        {
            var ss = S("Title_Subtitle");
            sub = Layer("Subtitle", ss, root);
            var rt = sub.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0); rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = ss.rect.size / (ss.pixelsPerUnit / 100f) * scale;
            rt.anchoredPosition = new Vector2(26f * scale, -subtitleGap * scale);
            sub.type = Image.Type.Filled; sub.fillMethod = Image.FillMethod.Horizontal; sub.fillOrigin = 0;
        }
        SetSplit(idleSplit);
    }

    Image Layer(string name, Sprite s, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false); Fill(rt);
        var img = go.GetComponent<Image>();
        img.sprite = s; img.raycastTarget = false; img.preserveAspect = false;
        return img;
    }

    static void Fill(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

    void SetSplit(Vector2 v)
    {
        cyan.rectTransform.anchoredPosition = new Vector2(-v.x, -v.y);
        pink.rectTransform.anchoredPosition = new Vector2(v.x, v.y);
    }

    void OnEnable()
    {
        Build();
        if (!built || !enabled) return;
        if (loop != null) StopCoroutine(loop);
        loop = StartCoroutine(Run());
    }

    void Update()
    {
        if (!built || glow == null) return;
        float k = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * glowPulseSpeed * Mathf.PI * 2f * 0.5f);
        glow.color = new Color(1, 1, 1, k);
    }

    IEnumerator Run()
    {
        bool calm = GameSettings.ReduceFlashing;
        if (sub) sub.fillAmount = playIntro ? 0f : 1f;
        if (playIntro)
        {
            float t = 0f;
            while (t < introTime)
            {
                t += Time.unscaledDeltaTime;
                float p = t / introTime;
                group.alpha = calm ? p : (Random.value < 0.3f + 0.7f * p ? Mathf.Lerp(0.3f, 1f, p) : 0.1f);
                float amt = (1f - p) * (calm ? 0.3f : 1f);
                SetSplit(idleSplit + Random.insideUnitCircle * maxSplit * amt);
                ShiftSlices(amt);
                yield return null;
            }
            group.alpha = 1f; ResetSlices(); SetSplit(idleSplit);
            if (sub)
                for (float s = 0; s < subtitleTypeTime; s += Time.unscaledDeltaTime)
                { sub.fillAmount = Mathf.Floor(s / subtitleTypeTime * 18f) / 18f; yield return null; }
            if (sub) sub.fillAmount = 1f;
            playIntro = false;
        }
        else group.alpha = 1f;

        while (true)
        {
            calm = GameSettings.ReduceFlashing;
            float wait = Random.Range(burstInterval.x, burstInterval.y) * (calm ? 2.5f : 1f);
            yield return new WaitForSecondsRealtime(wait);
            float len = Random.Range(burstLength.x, burstLength.y);
            float amt = calm ? 0.25f : 1f;
            bursting = true;
            for (float t = 0; t < len;)
            {
                SetSplit(idleSplit + Random.insideUnitCircle * maxSplit * amt);
                ShiftSlices(amt);
                if (!calm) group.alpha = Random.value < 0.15f ? 0.55f : 1f;
                yield return new WaitForSecondsRealtime(0.04f);
                t += 0.04f;
            }
            bursting = false;
            group.alpha = 1f; ResetSlices(); SetSplit(idleSplit);
        }
    }

    void ShiftSlices(float amount)
    {
        float h = size.y / Mathf.Max(1, slices);
        for (int i = 0; i < sliceImgs.Count; i++)
        {
            float dx = Random.value < 0.35f ? Random.Range(-maxSliceShift, maxSliceShift) * amount : 0f;
            sliceImgs[i].anchoredPosition = new Vector2(dx, i * h);
            sliceImages[i].color = Random.value < 0.08f * amount ? new Color(0.6f, 1f, 0.95f, 1f) : Color.white;
        }
    }

    void ResetSlices()
    {
        float h = size.y / Mathf.Max(1, slices);
        for (int i = 0; i < sliceImgs.Count; i++) { sliceImgs[i].anchoredPosition = new Vector2(0, i * h); sliceImages[i].color = Color.white; }
    }

    public void Glitch()
    {
        if (!built || bursting || !isActiveAndEnabled) return;
        StartCoroutine(OneBurst());
    }

    IEnumerator OneBurst()
    {
        bursting = true;
        float amt = GameSettings.ReduceFlashing ? 0.25f : 1f;
        for (int i = 0; i < 5; i++) { SetSplit(idleSplit + Random.insideUnitCircle * maxSplit * amt); ShiftSlices(amt); yield return new WaitForSecondsRealtime(0.04f); }
        ResetSlices(); SetSplit(idleSplit); bursting = false;
    }
}
