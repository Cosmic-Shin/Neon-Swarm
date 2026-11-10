using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Player options, saved in PlayerPrefs: volumes, graphics quality, fullscreen, camera speed, screen shake.
public static class GameSettings
{
    public static float Master = Get("master", 1f), Music = Get("music", 0.6f), Effects = Get("effects", 0.8f), CameraSpeed = Get("camSpeed", 1f);
    public static bool Shake = Get("shake", 1f) > 0.5f, Rain = Get("rain", 1f) > 0.5f, DamageNumbers = Get("dmgNumbers", 0f) > 0.5f;
    public static int Quality = (int)Get("quality", -1f); // -1: the project's default level
    public static event System.Action Changed;

    static float Get(string key, float fallback) => PlayerPrefs.GetFloat("settings." + key, fallback);
    static void Set(string key, float v) => PlayerPrefs.SetFloat("settings." + key, v);

    public static void Save()
    {
        Set("master", Master); Set("music", Music); Set("effects", Effects); Set("camSpeed", CameraSpeed);
        Set("shake", Shake ? 1f : 0f); Set("quality", Quality);
        Set("rain", Rain ? 1f : 0f); Set("dmgNumbers", DamageNumbers ? 1f : 0f);
        PlayerPrefs.Save();
        Apply();
    }

    public static void Apply()
    {
        AudioListener.volume = Master;
        if (Quality >= 0 && Quality < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != Quality) QualitySettings.SetQualityLevel(Quality, true);
        Changed?.Invoke();
    }
}

// The Settings screen, built in code so the main menu and the in-game Esc menu share it.
public class SettingsPanel : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    // Shared with the Skirmish screen.
    public static readonly Color Back = new(0.01f, 0.01f, 0.03f, 0.92f), Box = new(0.02f, 0.03f, 0.07f, 0.95f), Cyan = new(0.44f, 1f, 1f);
    static Font cachedFont;
    static Font font => cachedFont != null ? cachedFont : cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    public static void Open(Component anyUI)
    {
        var canvas = anyUI.GetComponentInParent<Canvas>().rootCanvas;
        var root = new GameObject("Settings", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        root.SetParent(canvas.transform, false);
        Stretch(root);
        root.GetComponent<Image>().color = Back; // also blocks clicks to the game underneath
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
        box.sizeDelta = new Vector2(620f, 740f);
        box.GetComponent<Image>().color = Box;

        Label(box, "SETTINGS", new Vector2(0f, 325f), 34, TextAnchor.MiddleCenter, 560f);
        float y = 245f;
        Slider(box, "Master volume", ref y, 0f, 1f, GameSettings.Master, v => { GameSettings.Master = v; GameSettings.Apply(); }, v => $"{v:P0}");
        Slider(box, "Music", ref y, 0f, 1f, GameSettings.Music, v => { GameSettings.Music = v; GameSettings.Apply(); }, v => $"{v:P0}");
        Slider(box, "Effects", ref y, 0f, 1f, GameSettings.Effects, v => { GameSettings.Effects = v; Sfx.Play(Snd.Laser, 0.8f); }, v => $"{v:P0}");
        Slider(box, "Camera speed", ref y, 0.5f, 2f, GameSettings.CameraSpeed, v => GameSettings.CameraSpeed = v, v => $"{v:0.0}x");

        string[] q = QualitySettings.names;
        Cycle(box, "Graphics", ref y, () => q[QualitySettings.GetQualityLevel()],
            () => { GameSettings.Quality = (QualitySettings.GetQualityLevel() + 1) % q.Length; GameSettings.Apply(); });
        Cycle(box, "Fullscreen", ref y, () => Screen.fullScreen ? "ON" : "OFF", () => Screen.fullScreen = !Screen.fullScreen);
        Cycle(box, "Screen shake", ref y, () => GameSettings.Shake ? "ON" : "OFF", () => GameSettings.Shake = !GameSettings.Shake);
        Cycle(box, "Rain", ref y, () => GameSettings.Rain ? "ON" : "OFF", () => GameSettings.Rain = !GameSettings.Rain);
        Cycle(box, "Damage numbers", ref y, () => GameSettings.DamageNumbers ? "ON" : "OFF", () => GameSettings.DamageNumbers = !GameSettings.DamageNumbers);

        var back = Button(box, "BACK", new Vector2(0f, -330f), new Vector2(300f, 56f));
        back.onClick.AddListener(Close);
    }

    // ---- widgets ----

    public static Text Label(Transform parent, string text, Vector2 pos, int size, TextAnchor align, float width)
    {
        var t = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = new Vector2(width, 44f);
        t.font = font; t.fontSize = size; t.alignment = align; t.color = Cyan; t.text = text;
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
        foreach (var img in s.GetComponentsInChildren<Image>()) img.color = new Color(0.1f, 0.15f, 0.25f);
        s.fillRect.GetComponent<Image>().color = Cyan * 0.8f;
        s.handleRect.GetComponent<Image>().color = Cyan;
        s.minValue = min; s.maxValue = max; s.value = value;
        s.onValueChanged.AddListener(v => { set(v); readout.text = show(v); });
        y -= 62f;
    }

    public static void Cycle(Transform box, string name, ref float y, System.Func<string> show, System.Action next)
    {
        Label(box, name, new Vector2(-145f, y), 22, TextAnchor.MiddleLeft, 260f);
        var b = Button(box, show(), new Vector2(140f, y), new Vector2(230f, 44f));
        var text = b.GetComponentInChildren<Text>();
        b.onClick.AddListener(() => { next(); text.text = show(); });
        y -= 62f;
    }

    public static Button Button(Transform parent, string text, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        go.GetComponent<Image>().color = new Color(0.06f, 0.1f, 0.18f, 1f);
        var t = Label(go.transform, text, Vector2.zero, 22, TextAnchor.MiddleCenter, size.x);
        t.rectTransform.sizeDelta = size;
        return go.GetComponent<Button>();
    }

    public static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
}
