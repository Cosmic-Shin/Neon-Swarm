using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

public static class GameSettings
{
    public static float Master = Get("master", 1f), Music = Get("music", 0.6f), Effects = Get("effects", 0.8f), CameraSpeed = Get("camSpeed", 1f);
    public static bool Shake = Get("shake", 1f) > 0.5f, Rain = Get("rain", 1f) > 0.5f, DamageNumbers = Get("dmgNumbers", 0f) > 0.5f;
    public static int Quality = (int)Get("quality", -1f);
    public static float BloomLevel = Get("bloom", 1f);
    public static bool PostFX = Get("postfx", 1f) > 0.5f;
    public static float NeonColours = Get("neon", 1f);
    public static bool ReduceFlashing = Get("calm", 0f) > 0.5f;
    public static float FlashScale => ReduceFlashing ? 0.3f : 1f;
    public static event System.Action Changed;

    static float Get(string key, float fallback) => PlayerPrefs.GetFloat("settings." + key, fallback);
    static void Set(string key, float v) => PlayerPrefs.SetFloat("settings." + key, v);

    public static void Save()
    {
        Set("master", Master); Set("music", Music); Set("effects", Effects); Set("camSpeed", CameraSpeed);
        Set("shake", Shake ? 1f : 0f); Set("quality", Quality);
        Set("rain", Rain ? 1f : 0f); Set("dmgNumbers", DamageNumbers ? 1f : 0f);
        Set("bloom", BloomLevel); Set("postfx", PostFX ? 1f : 0f); Set("neon", NeonColours); Set("calm", ReduceFlashing ? 1f : 0f);
        PlayerPrefs.Save();
        Apply();
    }

    public static void Apply()
    {
        AudioListener.volume = Master;
        if (Quality >= 0 && Quality < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != Quality) QualitySettings.SetQualityLevel(Quality, true);
        ApplyPostFX();
        Changed?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod]
    static void HookScenes() => UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => ApplyPostFX();

    public static void ApplyPostFX()
    {
        foreach (var layer in Object.FindObjectsByType<PostProcessLayer>(FindObjectsSortMode.None)) layer.enabled = PostFX;
        foreach (var vol in Object.FindObjectsByType<PostProcessVolume>(FindObjectsSortMode.None))
        {
            if (vol.sharedProfile == null) continue;
            if (vol.sharedProfile.TryGetSettings(out Bloom baseBloom) && vol.profile.TryGetSettings(out Bloom bloom))
            {
                bloom.active = BloomLevel > 0.01f;
                bloom.intensity.value = baseBloom.intensity.value * BloomLevel;
            }
            if (vol.sharedProfile.TryGetSettings(out ColorGrading baseGrade) && vol.profile.TryGetSettings(out ColorGrading grade))
            {
                float dim = 1f - NeonColours;
                grade.saturation.value = baseGrade.saturation.value - dim * 100f;
                grade.postExposure.value = baseGrade.postExposure.value - dim * 0.6f;
            }
        }
    }
}

public class SettingsPanel : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    public static readonly Color Back = new(0.01f, 0.01f, 0.03f, 0.92f), Box = new(0.02f, 0.03f, 0.07f, 0.95f), Cyan = new(0.44f, 1f, 1f);
    static Font cachedFont;
    static Font font => cachedFont != null ? cachedFont : cachedFont = HoloSkin.Font != null ? HoloSkin.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    public static void Open(Component anyUI)
    {
        var canvas = anyUI.GetComponentInParent<Canvas>().rootCanvas;
        var root = new GameObject("Settings", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        root.SetParent(canvas.transform, false);
        Stretch(root);
        root.GetComponent<Image>().color = Back;
        root.gameObject.AddComponent<SettingsPanel>().Build(root);
        IsOpen = true;
    }

    void OnDestroy() => IsOpen = false;

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }

    void Close() { GameSettings.Save(); Destroy(gameObject); }

    void Build(RectTransform root)
    {
        var box = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        box.SetParent(root, false);
        box.sizeDelta = new Vector2(620f, 1000f);
        HoloSkin.Modal(box);

        HoloSkin.Title(Label(box, "SETTINGS", new Vector2(0f, 455f), 34, TextAnchor.MiddleCenter, 560f), 540f);
        float y = 380f;
        Slider(box, "Master volume", ref y, 0f, 1f, GameSettings.Master, v => { GameSettings.Master = v; GameSettings.Apply(); }, v => $"{v:P0}");
        Slider(box, "Music", ref y, 0f, 1f, GameSettings.Music, v => { GameSettings.Music = v; GameSettings.Apply(); }, v => $"{v:P0}");
        Slider(box, "Effects", ref y, 0f, 1f, GameSettings.Effects, v => { GameSettings.Effects = v; Sfx.Play(Snd.Laser, 0.8f); }, v => $"{v:P0}");
        Slider(box, "Camera speed", ref y, 0.5f, 2f, GameSettings.CameraSpeed, v => GameSettings.CameraSpeed = v, v => $"{v:0.0}x");
        int[] cursorPx = { 0, 32, 48, 64 };
        string[] cursorNames = { "AUTO", "SMALL 32", "MEDIUM 48", "LARGE 64" };
        int CursorIndex() => Mathf.Max(0, System.Array.IndexOf(cursorPx, PlayerPrefs.GetInt(CursorManager.PrefKey, 0)));
        Cycle(box, "Cursor size", ref y, () => cursorNames[CursorIndex()],
            () => CursorManager.SetSizePreference(cursorPx[(CursorIndex() + 1) % cursorPx.Length]));

        string[] q = QualitySettings.names;
        Text postLabel = null;
        Cycle(box, "Graphics", ref y, () => q[QualitySettings.GetQualityLevel()], () =>
        {
            GameSettings.Quality = (QualitySettings.GetQualityLevel() + 1) % q.Length;
            GameSettings.PostFX = GameSettings.Quality > 1;
            GameSettings.Apply();
            postLabel.text = GameSettings.PostFX ? "ON" : "OFF";
            HoloSkin.SetOn(postLabel.GetComponentInParent<Button>(), GameSettings.PostFX);
        });
        postLabel = Cycle(box, "Post effects", ref y, () => GameSettings.PostFX ? "ON" : "OFF", () => { GameSettings.PostFX = !GameSettings.PostFX; GameSettings.ApplyPostFX(); });
        Slider(box, "Bloom (neon glow)", ref y, 0f, 1.5f, GameSettings.BloomLevel, v => { GameSettings.BloomLevel = v; GameSettings.ApplyPostFX(); }, v => v < 0.01f ? "OFF" : $"{v:P0}");
        Slider(box, "Neon colours", ref y, 0.3f, 1f, GameSettings.NeonColours, v => { GameSettings.NeonColours = v; GameSettings.ApplyPostFX(); }, v => $"{v:P0}");
        Cycle(box, "Reduce flashing", ref y, () => GameSettings.ReduceFlashing ? "ON" : "OFF", () => GameSettings.ReduceFlashing = !GameSettings.ReduceFlashing);
        Cycle(box, "Fullscreen", ref y, () => Screen.fullScreen ? "ON" : "OFF", () => Screen.fullScreen = !Screen.fullScreen);
        Cycle(box, "Screen shake", ref y, () => GameSettings.Shake ? "ON" : "OFF", () => GameSettings.Shake = !GameSettings.Shake);
        Cycle(box, "Rain", ref y, () => GameSettings.Rain ? "ON" : "OFF", () => GameSettings.Rain = !GameSettings.Rain);
        Cycle(box, "Damage numbers", ref y, () => GameSettings.DamageNumbers ? "ON" : "OFF", () => GameSettings.DamageNumbers = !GameSettings.DamageNumbers);

        var back = Button(box, "BACK", new Vector2(-150f, -450f), new Vector2(270f, 56f));
        back.onClick.AddListener(Close);
        Button(box, "CREDITS", new Vector2(150f, -450f), new Vector2(270f, 56f)).onClick.AddListener(() => ShowCredits(root));
    }

    static void ShowCredits(RectTransform root)
    {
        var cover = new GameObject("Credits", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        cover.SetParent(root, false);
        Stretch(cover);
        cover.GetComponent<Image>().color = new Color(0f, 0f, 0.01f, 0.97f);
        var box = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        box.SetParent(cover, false);
        box.sizeDelta = new Vector2(760f, 1000f);
        HoloSkin.Modal(box);
        HoloSkin.Title(Label(box, "CREDITS", new Vector2(0f, 455f), 34, TextAnchor.MiddleCenter, 700f), 680f);
        Label(box, "MUSIC", new Vector2(0f, 395f), 24, TextAnchor.MiddleCenter, 700f).color = Cyan;
        var lines = new System.Text.StringBuilder();
        foreach (var title in Sfx.Credits)
            lines.Append($"{title} Kevin MacLeod (incompetech.com)\nLicensed under Creative Commons: By Attribution 4.0\nhttps://creativecommons.org/licenses/by/4.0/\n\n");
        var text = Label(box, lines.ToString().TrimEnd(), new Vector2(0f, -20f), 19, TextAnchor.UpperCenter, 700f);
        text.rectTransform.sizeDelta = new Vector2(700f, 760f);
        Button(box, "CLOSE", new Vector2(0f, -450f), new Vector2(300f, 56f)).onClick.AddListener(() => Destroy(cover.gameObject));
    }


    public static Text Label(Transform parent, string text, Vector2 pos, int size, TextAnchor align, float width)
    {
        var t = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = new Vector2(width, 44f);
        t.font = font; t.fontSize = size; t.alignment = align; t.color = HoloUI.Text; t.text = text;
        t.raycastTarget = false;
        return t;
    }

    static void Slider(Transform box, string name, ref float y, float min, float max, float value, System.Action<float> set, System.Func<float, string> show)
    {
        Label(box, name, new Vector2(-145f, y), 22, TextAnchor.MiddleLeft, 260f);
        var readout = Label(box, show(value), new Vector2(255f, y), 22, TextAnchor.MiddleRight, 90f);
        var s = DefaultControls.CreateSlider(new DefaultControls.Resources()).GetComponent<Slider>();
        s.transform.SetParent(box, false);
        var rt = (RectTransform)s.transform;
        rt.anchoredPosition = new Vector2(95f, y);
        rt.sizeDelta = new Vector2(220f, 20f);
        HoloUI.StyleSlider(s);
        foreach (var part in new[] { "Background", "Fill Area" })
        {
            var pr = (RectTransform)s.transform.Find(part);
            pr.anchorMin = new Vector2(0f, 0.5f); pr.anchorMax = new Vector2(1f, 0.5f);
            pr.sizeDelta = new Vector2(pr.sizeDelta.x, 6f);
        }
        s.fillRect.offsetMin = new Vector2(s.fillRect.offsetMin.x, 0f); s.fillRect.offsetMax = new Vector2(s.fillRect.offsetMax.x, 0f);
        s.handleRect.sizeDelta = new Vector2(20f, 0f);
        s.minValue = min; s.maxValue = max; s.value = value;
        s.onValueChanged.AddListener(v => { set(v); readout.text = show(v); });
        y -= RowStep;
    }

    const float RowStep = 58f;

    public static Text Cycle(Transform box, string name, ref float y, System.Func<string> show, System.Action next)
    {
        Label(box, name, new Vector2(-145f, y), 22, TextAnchor.MiddleLeft, 260f);
        var b = Button(box, show(), new Vector2(140f, y), new Vector2(230f, 44f));
        HoloSkin.Button(b, HoloUI.ButtonStyle.Small);
        var text = b.GetComponentInChildren<Text>();
        HoloSkin.SetOn(b, text.text == "ON");
        b.onClick.AddListener(() => { next(); text.text = show(); HoloSkin.SetOn(b, text.text == "ON"); });
        y -= RowStep;
        return text;
    }

    public static Button Button(Transform parent, string text, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var t = Label(go.transform, text, Vector2.zero, 22, TextAnchor.MiddleCenter, size.x);
        t.rectTransform.sizeDelta = size;
        var b = go.GetComponent<Button>();
        HoloSkin.Button(b);
        return b;
    }

    public static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
}
