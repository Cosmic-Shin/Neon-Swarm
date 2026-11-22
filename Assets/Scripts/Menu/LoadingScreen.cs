using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingScreen : MonoBehaviour
{
    const float MinTime = 1.2f, Fade = 0.4f;

    static LoadingScreen instance;

    CanvasGroup group;
    Image preview, bar;
    RectTransform info;
    Text mapName, status, tip;
    bool showing;
    float shownAt;

    public static bool Visible => instance != null && instance.showing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        instance = new GameObject("LoadingScreen").AddComponent<LoadingScreen>();
        DontDestroyOnLoad(instance.gameObject);
        instance.Build();
    }

    public static void Load(int buildIndex) => Load(SceneUtility.GetScenePathByBuildIndex(buildIndex));

    public static void Load(string scene)
    {
        Boot();
        instance.StopAllCoroutines();
        instance.StartCoroutine(instance.Run(scene));
    }

    IEnumerator Run(string scene)
    {
        Show(System.IO.Path.GetFileNameWithoutExtension(scene));
        yield return null;
        yield return null;
        var load = SceneManager.LoadSceneAsync(scene);
        for (float fake = 0f; !load.isDone; fake = Mathf.MoveTowards(fake, 0.8f, Time.unscaledDeltaTime * 0.4f))
        {
            SetProgress(Mathf.Max(load.progress, fake));
            yield return null;
        }
        yield return Finish();
    }

    void Update()
    {
        if (showing) bar.fillAmount = Mathf.MoveTowards(bar.fillAmount, target, Time.unscaledDeltaTime * 1.2f);
        var runner = Net.Runner;
        if (!showing && runner != null && runner.IsRunning && runner.SceneManager != null && runner.SceneManager.IsBusy)
        {
            Show(null);
            StartCoroutine(WaitForNetwork());
        }
    }

    IEnumerator WaitForNetwork()
    {
        float fake = 0f;
        while (Net.Runner != null && Net.Runner.SceneManager != null && Net.Runner.SceneManager.IsBusy)
        {
            fake = Mathf.MoveTowards(fake, 0.85f, Time.unscaledDeltaTime * 0.3f);
            SetProgress(fake);
            yield return null;
        }
        mapName.text = MapTitle(SceneManager.GetActiveScene().name);
        yield return Finish();
    }

    IEnumerator Finish()
    {
        status.text = "SYNCING";
        SetProgress(1f);
        while (bar.fillAmount < 1f) yield return null;
        while (Time.unscaledTime - shownAt < MinTime) yield return null;
        yield return null;
        for (float t = 0f; t < Fade; t += Time.unscaledDeltaTime) { group.alpha = 1f - t / Fade; yield return null; }
        group.alpha = 0f;
        group.gameObject.SetActive(false);
        showing = false;
    }

    static GameMap MapOf(string sceneName) => System.Array.Find(GameMaps.All, m => m.scene == sceneName);

    static string MapTitle(string sceneName) =>
        MapOf(sceneName) is GameMap m ? m.name
        : sceneName == System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(0)) ? "MAIN MENU" : "";

    void Show(string sceneName)
    {
        showing = true;
        shownAt = Time.unscaledTime;
        var map = sceneName != null ? MapOf(sceneName) : null;
        if (sceneName == null && RoomState.Instance != null) map = RoomState.Instance.MapInfo;
        var sprite = map != null ? GameMaps.Preview(map) : null;
        preview.sprite = sprite;
        preview.gameObject.SetActive(sprite != null);
        info.anchoredPosition = new Vector2(0f, sprite != null ? 0f : 330f);
        mapName.text = map != null ? map.name : sceneName != null ? MapTitle(sceneName) : "";
        status.text = map != null ? $"DEPLOYING TO BATTLEFIELD   ·   CONDITIONS: {map.weather.name}" : "LOADING";
        tip.text = "<color=#FF40E0><b>TIP</b></color>   " + MenuExtras.Tips[Random.Range(0, MenuExtras.Tips.Length)];
        SetProgress(0f);
        group.alpha = 1f;
        group.gameObject.SetActive(true);
    }

    float target;
    void SetProgress(float p) { target = Mathf.Clamp01(p); if (p <= 0f) bar.fillAmount = 0f; }

    void Build()
    {
        var root = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        var c = root.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 900;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        group = root.GetComponent<CanvasGroup>();

        var back = Make<Image>("Back", root.transform);
        Stretch(back.rectTransform);
        back.color = new Color(0.03f, 0.03f, 0.06f, 1f);
        back.raycastTarget = true;

        preview = Make<Image>("Preview", root.transform);
        preview.rectTransform.sizeDelta = new Vector2(560f, 560f);
        preview.rectTransform.anchoredPosition = new Vector2(0f, 40f);
        preview.preserveAspect = true;
        preview.color = new Color(0.75f, 0.85f, 1f, 0.9f);
        HoloSkin.Brackets(preview.rectTransform, new Color(0.1f, 1f, 0.95f));

        var logo = Make<Image>("Logo", root.transform);
        logo.sprite = Resources.Load<Sprite>("HoloUI/Title/Title_Full");
        logo.preserveAspect = true;
        logo.rectTransform.anchorMin = logo.rectTransform.anchorMax = new Vector2(0f, 1f);
        logo.rectTransform.pivot = new Vector2(0f, 1f);
        logo.rectTransform.anchoredPosition = new Vector2(60f, -40f);
        logo.rectTransform.sizeDelta = new Vector2(520f, 170f);
        logo.gameObject.SetActive(logo.sprite != null);

        info = new GameObject("Info", typeof(RectTransform)).GetComponent<RectTransform>();
        info.SetParent(root.transform, false);
        mapName = Label("Map", info, 44, new Vector2(0f, -290f), new Color(0.1f, 1f, 0.95f), true);
        status = Label("Status", info, 22, new Vector2(0f, -345f), new Color(0.6f, 0.68f, 0.8f), false);

        var barBack = Make<Image>("BarBack", info);
        barBack.rectTransform.anchoredPosition = new Vector2(0f, -385f);
        barBack.rectTransform.sizeDelta = new Vector2(760f, 8f);
        barBack.color = new Color(1f, 1f, 1f, 0.08f);
        bar = Make<Image>("Bar", barBack.transform);
        Stretch(bar.rectTransform);
        bar.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), Vector2.one * 0.5f);
        bar.type = Image.Type.Filled;
        bar.fillMethod = Image.FillMethod.Horizontal;
        bar.color = new Color(1f, 0.25f, 0.85f);

        tip = Label("Tip", info, 22, new Vector2(0f, -450f), new Color(0.85f, 0.9f, 1f), false);
        tip.rectTransform.sizeDelta = new Vector2(1400f, 40f);

        root.SetActive(false);
    }

    static Text Label(string name, Transform parent, int size, Vector2 pos, Color color, bool bold)
    {
        var t = Make<Text>(name, parent);
        t.font = bold ? HoloSkin.BoldFont : HoloSkin.Font;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = new Vector2(1200f, size + 16f);
        return t;
    }

    static T Make<T>(string name, Transform parent) where T : Graphic
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(T)).GetComponent<T>();
        g.transform.SetParent(parent, false);
        g.raycastTarget = false;
        return g;
    }

    static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
}
