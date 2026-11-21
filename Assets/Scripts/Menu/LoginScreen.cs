using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class LoginScreen : MonoBehaviour
{
    static readonly Color Cyan = new(0.3f, 1f, 1f);
    static readonly string[] Steps = { "AUTHENTICATING", "SYNCING PROFILE", "LINKING FRIENDS NET", "WAKING THE SWARM" };

    public static bool Booting;

    public InputField username, password;
    public Button signIn, signUp;
    public Text status;
    public GameObject signInPanel, lobbyPanel;

    static Task hub;

    void Start()
    {
        signIn.onClick.AddListener(() => Submit(false));
        signUp.onClick.AddListener(() => Submit(true));
        username.onSubmit.AddListener(_ => { if (password.text.Length == 0) { password.Select(); password.ActivateInputField(); } else Submit(false); });
        password.onSubmit.AddListener(_ => Submit(false));
        Show();
#if DEVELOPMENT_BUILD
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-signin");
        if (i >= 0 && i + 2 < args.Length && !AccountService.SignedIn) { username.text = args[i + 1]; password.text = args[i + 2]; Submit(false); }
#endif
    }

    async void Submit(bool isNew)
    {
        if (!signIn.interactable) return;
        string error = AccountService.Validate(username.text.Trim(), password.text, isNew);
        if (error != null) { status.text = error; return; }

        SetBusy(true);
        status.text = isNew ? "Creating account..." : "Signing in...";
        try
        {
            if (isNew) await AccountService.SignUp(username.text.Trim(), password.text);
            else await AccountService.SignIn(username.text.Trim(), password.text);
            password.text = "";
            status.text = "";
            StartCoroutine(Boot());
        }
        catch (Exception e)
        {
            status.text = e.Message;
            Debug.LogWarning(e);
        }
        SetBusy(false);
    }

    public void Show()
    {
        bool inside = AccountService.SignedIn;
        signInPanel.SetActive(!inside);
        lobbyPanel.SetActive(inside);
        if (inside && (hub == null || hub.IsCompleted)) hub = FriendsHub.Init();
        if (inside && RoomService.Again != null) MultiplayerMenu.PlayAgain();
    }

    void SetBusy(bool busy) => signIn.interactable = signUp.interactable = !busy;

    IEnumerator Boot()
    {
        Booting = true;
        bool calm = GameSettings.ReduceFlashing;
        yield return GlitchPanel(signInPanel, false, calm);
        signInPanel.SetActive(false);
        hub = FriendsHub.Init();

        var root = new GameObject("Uplink", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        var rt = (RectTransform)root.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 150f);
        rt.sizeDelta = new Vector2(520f, 60f);
        var label = Label(rt, new Vector2(0f, 18f), TextAnchor.MiddleLeft);
        var pct = Label(rt, new Vector2(0f, 18f), TextAnchor.MiddleRight);
        Box(rt, new Vector2(0.5f, 0.5f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.15f), 1f);
        var bar = Box(rt, new Vector2(0f, 0.5f), Cyan, 0f);
        var group = root.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        float fill = 0f, shown = 0f, t = 0f;
        while (t < 3f || (!hub.IsCompleted && t < 8f))
        {
            t += Time.unscaledDeltaTime;
            float target = hub.IsCompleted ? t / 3f : Mathf.Min(t / 3f, 0.9f);
            if (UnityEngine.Random.value < 0.06f) fill = Mathf.Max(fill, Mathf.Min(target, fill + UnityEngine.Random.Range(0.04f, 0.14f)));
            shown = Mathf.MoveTowards(shown, Mathf.Clamp01(fill), Time.unscaledDeltaTime * 0.8f);
            bar.rectTransform.sizeDelta = new Vector2(520f * shown, 3f);
            int step = Mathf.Min(Steps.Length - 1, (int)(shown * Steps.Length));
            string dots = new('.', 1 + (int)(t * 3f) % 3);
            label.text = Steps[step] + dots;
            pct.text = $"{Mathf.RoundToInt(shown * 100f)}%";
            group.alpha = Mathf.Min(1f, t * 4f) * (calm || UnityEngine.Random.value > 0.04f ? 1f : 0.3f);
            yield return null;
        }
        label.text = "UPLINK ESTABLISHED";
        pct.text = "100%";
        bar.rectTransform.sizeDelta = new Vector2(520f, 3f);
        yield return new WaitForSecondsRealtime(0.35f);
        Destroy(root);

        Booting = false;
        Show();
        yield return GlitchPanel(lobbyPanel, true, calm);
    }

    IEnumerator GlitchPanel(GameObject panel, bool appear, bool calm)
    {
        if (!panel.TryGetComponent(out CanvasGroup g)) g = panel.AddComponent<CanvasGroup>();
        var rt = (RectTransform)panel.transform;
        Vector2 home = rt.anchoredPosition;
        Sfx.Play(Snd.Zap, calm ? 0.25f : 0.5f);
        const float length = 0.45f;
        for (float t = 0f; t < length; t += Time.unscaledDeltaTime)
        {
            float p = appear ? t / length : 1f - t / length;
            float jitter = calm ? 0f : (1f - p) * 40f;
            rt.anchoredPosition = home + new Vector2(UnityEngine.Random.Range(-jitter, jitter), 0f);
            g.alpha = calm || UnityEngine.Random.value < p ? p : p * 0.2f;
            yield return null;
        }
        rt.anchoredPosition = home;
        g.alpha = 1f;
    }

    Text Label(RectTransform parent, Vector2 pos, TextAnchor align)
    {
        var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.rectTransform.sizeDelta = new Vector2(520f, 28f);
        t.rectTransform.anchoredPosition = pos;
        t.font = status.font;
        t.fontSize = 18;
        t.alignment = align;
        t.color = Cyan;
        t.raycastTarget = false;
        return t;
    }

    Image Box(RectTransform parent, Vector2 pivot, Color color, float width01)
    {
        var img = new GameObject("Bar", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(pivot.x == 0f ? 0f : 0.5f, 0.5f);
        r.pivot = pivot;
        r.anchoredPosition = new Vector2(0f, -4f);
        r.sizeDelta = new Vector2(520f * width01, 3f);
        img.color = color;
        img.raycastTarget = false;
        return img;
    }
}
