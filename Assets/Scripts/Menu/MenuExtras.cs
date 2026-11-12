using UnityEngine;
using UnityEngine.UI;

public class MenuExtras : MonoBehaviour
{
    static readonly string[] Tips =
    {
        "Hold <b>Shift</b> while giving orders to queue them up.",
        "<b>Ctrl + 1-9</b> saves a control group. Tap the number to select it again.",
        "Press <b>A</b> and click the ground to attack-move: units fight anything they meet.",
        "Harvesters mine on their own. Build a second Data Refinery near far ore fields.",
        "Low power halves production speed. Watch the POWER meter at the top of the screen.",
        "Units rank up from kills. Veterans hit harder and glow brighter.",
        "Capture neutral outposts to hold key ground on the map.",
        "Press <b>Space</b> to jump the camera to the last alert.",
        "<b>Alt + click</b> pings the map for your allies.",
        "<b>F5</b> quick-saves a skirmish. CONTINUE loads your newest save.",
        "Press <b>F1</b> in a match to see every hotkey.",
        "Hard AI scouts early and counters your army. Bring anti-air.",
    };
    const float TipTime = 7f;

    AvatarList.View avatar;
    Text playerName, rank, tip;
    int tipIndex;
    float tipStarted;

    public static MenuExtras Build(Transform canvas, System.Action openAccount)
    {
        var root = new GameObject("MenuExtras", typeof(RectTransform)).GetComponent<RectTransform>();
        root.gameObject.SetActive(false);
        root.SetParent(canvas, false);
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        var x = root.gameObject.AddComponent<MenuExtras>();

        var card = new GameObject("PlayerCard", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
        card.SetParent(root, false);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(1f, 1f);
        card.anchoredPosition = new Vector2(-36f, -36f);
        card.sizeDelta = new Vector2(380f, 104f);
        card.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.08f, 0.85f);
        HoloSkin.Back(card, "Panel_Inset");
        var cardButton = card.GetComponent<Button>();
        cardButton.transition = Selectable.Transition.None;
        cardButton.onClick.AddListener(() => openAccount());
        x.avatar = AvatarList.Make(card, new Vector2(-130f, 0f), 76f, HoloUI.Pink);
        x.playerName = Label(card, "", new Vector2(-82f, 18f), 26, TextAnchor.MiddleLeft, 260f, Color.white, true);
        x.rank = Label(card, "", new Vector2(-82f, -10f), 17, TextAnchor.MiddleLeft, 260f, new Color(1f, 0.8f, 0.25f), true);
        Label(card, "VIEW PROFILE  >", new Vector2(-82f, -34f), 13, TextAnchor.MiddleLeft, 260f, new Color(0.55f, 0.6f, 0.75f), false);

        x.tip = Label(root, "", Vector2.zero, 19, TextAnchor.MiddleLeft, 900f, new Color(0.8f, 0.88f, 1f), false);
        var tr = x.tip.rectTransform;
        tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0f, 0f);
        tr.anchoredPosition = new Vector2(110f, 26f);
        tr.sizeDelta = new Vector2(900f, 30f);

        var music = Label(root, $"NOW PLAYING  <color=#FFFFFF>{Sfx.Credits[0]}</color>  ·  Kevin MacLeod      <color=#5A6680>v{Application.version}</color>",
            Vector2.zero, 15, TextAnchor.MiddleRight, 700f, new Color(0.55f, 0.6f, 0.75f), false);
        var mr = music.rectTransform;
        mr.anchorMin = mr.anchorMax = mr.pivot = new Vector2(1f, 0f);
        mr.anchoredPosition = new Vector2(-36f, 26f);
        mr.sizeDelta = new Vector2(700f, 24f);
        music.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
        x.tip.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);

        x.tipIndex = Random.Range(0, Tips.Length);
        return x;
    }

    static Text Label(Transform parent, string text, Vector2 pos, int size, TextAnchor align, float width, Color color, bool bold)
    {
        var t = SettingsPanel.Label(parent, text, pos, size, align, width);
        t.rectTransform.pivot = new Vector2(0f, 0.5f);
        t.font = bold ? HoloSkin.BoldFont : HoloSkin.Font;
        t.color = color;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }

    async void OnEnable()
    {
        transform.SetSiblingIndex(Mathf.Max(0, transform.parent.Find("Title") is Transform title ? title.GetSiblingIndex() : 0));
        tipStarted = Time.unscaledTime;
        ShowTip();
        Fill(ProfileService.Current);
        var p = await ProfileService.Load();
        if (this != null) Fill(p);
    }

    void Fill(PlayerProfile p)
    {
        string user = AccountService.Username;
        avatar.Show(p?.avatar, user);
        playerName.text = string.IsNullOrEmpty(user) ? "GUEST" : user.ToUpper();
        rank.text = p == null ? "" : PlayerProfile.Ranks[p.RankIndex].name + $"   <color=#8C99BF>{p.all.wins}W  {p.all.Losses}L</color>";
    }

    void ShowTip() => tip.text = "<color=#FF40E0><b>TIP</b></color>   " + Tips[tipIndex];

    void Update()
    {
        float t = Time.unscaledTime - tipStarted;
        if (t > TipTime)
        {
            tipIndex = (tipIndex + 1) % Tips.Length;
            tipStarted = Time.unscaledTime;
            ShowTip();
            t = 0f;
        }
        float a = Mathf.Clamp01(t / 0.4f) * Mathf.Clamp01((TipTime - t) / 0.4f);
        var c = tip.color;
        tip.color = new Color(c.r, c.g, c.b, a);
    }
}
