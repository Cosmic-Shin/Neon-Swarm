using UnityEngine;
using UnityEngine.UI;

public class AccountScreen : MonoBehaviour
{
    static readonly Color Cyan = new(0.3f, 1f, 1f), Pink = new(1f, 0.1f, 0.9f), Win = new(0.35f, 1f, 0.55f), Loss = new(1f, 0.32f, 0.38f),
        Card = new(0.02f, 0.03f, 0.08f, 1f), Dim = new(0.55f, 0.6f, 0.75f), BarBack = new(0.12f, 0.12f, 0.2f, 1f), Gold = new(1f, 0.8f, 0.25f);

    AvatarList.View avatarView;
    Text nameText, rankText, sinceText, idText, rankNext, note;
    Image rankFill, ring;
    Text ringText;
    readonly Text[] cardValues = new Text[5], cardSubs = new Text[5];
    readonly (Text label, Image fill, Text value)[] bars = new (Text, Image, Text)[7];
    Text factionText;
    Image factionEmblem;

    static Faction Fac(int id) { foreach (var f in Faction.All) if ((int)f.id == id) return f; return null; }
    static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    static string Tag(int id) { var f = Fac(id); return f == null ? "" : $"<color={Hex(f.color)}>{f.id.ToString().ToUpper()}</color>  "; }
    readonly Text[] totalValues = new Text[9];
    readonly (GameObject row, Image badge, Text result, Text what, Text when)[] recent = new (GameObject, Image, Text, Text, Text)[PlayerProfile.RecentKept];
    Text noMatches;
    bool built;
    bool viewer;
    PlayerProfile shown;
    readonly Badge[] showcase = new Badge[AchievementsPanel.MaxPinned];
    readonly Text[] showcaseEmpty = new Text[AchievementsPanel.MaxPinned];
    readonly Image[] showcaseSlot = new Image[AchievementsPanel.MaxPinned];
    readonly Tooltip[] showcaseTip = new Tooltip[AchievementsPanel.MaxPinned];
    Text showcaseLabel;
    string shownName;

    void OpenAchievements() =>
        AchievementsPanel.Open(transform.parent, shown, !viewer, viewer ? (shownName ?? "PLAYER").ToUpper() + "'S ACHIEVEMENTS" : "ACHIEVEMENTS", () => { if (this != null) ShowBadges(shown); });

    void ShowBadges(PlayerProfile p)
    {
        var pinned = p?.badges ?? new string[0];
        int n = 0;
        for (int i = 0; i < showcase.Length; i++)
        {
            var d = i < pinned.Length ? Achievements.Find(pinned[i]) : null;
            int t = d != null ? Achievements.Tier(d, p) : 0;
            bool has = d != null && t > 0;
            showcase[i].gameObject.SetActive(has);
            showcaseEmpty[i].text = has || viewer ? "" : "+";
            showcaseSlot[i].color = new Color(1f, 1f, 1f, has ? 0f : 0.35f);
            if (has) { showcase[i].Show(d, t); n++; }
            showcaseTip[i].text = has ? $"{d.name}  ·  {Achievements.TierNames[t]}" : viewer ? "Empty slot" : "Pin a badge in ACHIEVEMENTS";
        }
        int pts = p != null ? Achievements.Points(p) : 0;
        showcaseLabel.text = n == 0 && !viewer ? "PIN BADGES  ·  click a slot" : $"BADGES  ·  {pts} / {Achievements.All.Length * 4} tiers";
    }

    public static void View(Transform parent, string playerId, string name)
    {
        var shade = new GameObject("ProfileView", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        shade.SetParent(parent, false);
        SettingsPanel.Stretch(shade);
        shade.GetComponent<Image>().color = new Color(0f, 0.01f, 0.04f, 0.9f);
        HoloSkin.Layer(shade, HoloSkin.LayerPopup + 4);
        var panel = new GameObject("Profile", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        panel.SetParent(shade, false);
        HoloSkin.Modal(panel);
        var screen = panel.gameObject.AddComponent<AccountScreen>();
        screen.viewer = true;
        screen.ShowOther(playerId, name);
    }

    async void ShowOther(string playerId, string name)
    {
        Build();
        nameText.text = (name ?? "PLAYER").ToUpper();
        idText.text = "PLAYER ID  " + playerId;
        memberSince = "Friend";
        noMatches.text = "No matches yet.";
        note.text = "Loading stats...";
        shown = new PlayerProfile();
        Fill(shown, name);
        var p = await ProfileService.LoadPublic(playerId);
        if (this == null) return;
        shown = p ?? new PlayerProfile();
        Fill(shown, name);
        note.text = p == null ? "This player has no stats yet." : "Public stats from their account.";
    }

    public async void Show()
    {
        if (!built) Build();
        nameText.text = AccountService.Username?.ToUpper() ?? "GUEST";
        idText.text = "PLAYER ID  " + (AccountService.SignedIn ? AccountService.PlayerId : "-");
        memberSince = "Member since ...";
        nextClock = 0f;
        note.text = "Loading stats...";
        Fill(ProfileService.Current ?? new PlayerProfile());

        var profile = await ProfileService.Load();
        if (this == null) return;
        Fill(profile);
        note.text = ProfileService.LastError ?? "Stats are saved to your account (Unity Cloud Save).";
        try
        {
            var since = await AccountService.MemberSince();
            memberSince = "Member since " + (since?.ToLocalTime().ToString("MMMM d, yyyy") ?? "unknown");
        }
        catch (System.Exception e) { memberSince = "Member since: unavailable"; Debug.LogWarning(e); }
    }

    string memberSince = "Member since ...";
    float nextClock;

    void Update()
    {
        if (viewer && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame && matchView == null) { Destroy(transform.parent.gameObject); return; }
        if (!built || Time.unscaledTime < nextClock) return;
        nextClock = Time.unscaledTime + 1f;
        var p = viewer ? shown : ProfileService.Current;
        sinceText.text = memberSince + (p != null ? $"   <color=#FFFFFF>·   {Played(p.secondsInGame)} played</color>" : "");
    }

    static string Played(float s)
    {
        int m = (int)(s / 60f);
        return m >= 60 ? $"{m / 60}h {m % 60:00}m" : $"{m}m";
    }

    void Fill(PlayerProfile p) => Fill(p, AccountService.Username);

    void Fill(PlayerProfile p, string user)
    {
        if (!viewer) shown = p;
        avatarView.Show(p.avatar, user);
        shownName = user;
        ShowBadges(p);
        int r = p.RankIndex;
        rankText.text = PlayerProfile.Ranks[r].name;
        rankFill.rectTransform.anchorMax = new Vector2(p.RankProgress, 1f);
        rankNext.text = r + 1 < PlayerProfile.Ranks.Length ? $"{PlayerProfile.Ranks[r + 1].wins - p.all.wins} more wins to {PlayerProfile.Ranks[r + 1].name}" : "Top rank reached";

        cardValues[0].text = p.all.games.ToString();
        cardSubs[0].text = Hours(p.secondsPlayed) + " in matches";
        cardValues[1].text = p.all.wins.ToString();
        cardSubs[1].text = p.fastestWin > 0f ? "fastest " + Clock(p.fastestWin) : "no wins yet";
        cardValues[2].text = p.all.Losses.ToString();
        cardSubs[2].text = $"K/D  {(p.Total(MatchStats.Kind.UnitsLost) > 0 ? (float)p.Total(MatchStats.Kind.Kills) / p.Total(MatchStats.Kind.UnitsLost) : p.Total(MatchStats.Kind.Kills)):0.00}";
        cardValues[3].text = "";
        ring.fillAmount = p.all.Rate;
        ring.color = Color.Lerp(Loss, Win, p.all.Rate);
        ringText.text = p.all.games > 0 ? $"{p.all.Rate * 100f:0}%" : "-";
        cardSubs[3].text = "win rate";
        cardValues[4].text = p.streak > 0 ? $"W{p.streak}" : p.streak < 0 ? $"L{-p.streak}" : "-";
        cardValues[4].color = p.streak > 0 ? Win : p.streak < 0 ? Loss : Color.white;
        cardSubs[4].text = $"best  W{p.bestStreak}";

        SetBar(0, "SKIRMISH", p.skirmish);
        SetBar(1, "MULTIPLAYER", p.online);
        int bar = 2;
        foreach (var f in Faction.All)
            if (bar < 4) SetBar(bar++, $"<color={Hex(f.color)}>AS {f.displayName.ToUpper()}</color>", p.As(f.id));
        SetBar(4, "VS EASY AI", p.vsAI[0]);
        SetBar(5, "VS NORMAL AI", p.vsAI[1]);
        SetBar(6, "VS HARD AI", p.vsAI[2]);

        var main = p.Main.HasValue ? Fac((int)p.Main.Value) : null;
        factionText.text = main != null
            ? $"MAIN FACTION   <color={Hex(main.color)}><b>{main.displayName.ToUpper()}</b></color>   <color=#8C99BF>{p.As(main.id).games} of {p.all.games} games</color>"
            : "MAIN FACTION   <color=#8C99BF>play a match to find out</color>";
        factionEmblem.sprite = main != null ? main.emblem : null;
        factionEmblem.enabled = factionEmblem.sprite != null;
        factionText.rectTransform.anchoredPosition = new Vector2(factionEmblem.enabled ? -302f : -330f, factionText.rectTransform.anchoredPosition.y);

        int[] t =
        {
            p.Total(MatchStats.Kind.Kills), p.Total(MatchStats.Kind.UnitsBuilt), p.Total(MatchStats.Kind.UnitsLost),
            p.Total(MatchStats.Kind.BuildingsBuilt), p.Total(MatchStats.Kind.BuildingsLost), p.Total(MatchStats.Kind.Research),
        };
        for (int i = 0; i < t.Length; i++) totalValues[i].text = t[i].ToString("N0");
        totalValues[6].text = "$" + Short(p.Total(MatchStats.Kind.Earned));
        totalValues[7].text = p.all.games > 0 ? Clock(p.secondsPlayed / p.all.games) : "-";
        totalValues[8].text = p.all.games > 0 ? $"{(float)p.Total(MatchStats.Kind.Kills) / p.all.games:0.0}" : "-";

        noMatches.gameObject.SetActive(p.recent.Count == 0);
        for (int i = 0; i < recent.Length; i++)
        {
            var row = recent[i];
            bool used = i < p.recent.Count;
            row.row.SetActive(used);
            if (!used) continue;
            var m = p.recent[i];
            row.badge.color = m.won ? Win : Loss;
            row.result.text = m.won ? "W" : "L";
            row.what.text = $"{Tag(m.faction)}<b>{(m.online ? "MULTIPLAYER" : "SKIRMISH")}</b>  <color=#8C99BF>vs {m.opponents}</color>";
            row.when.text = $"{Clock(m.seconds)}   {m.kills} kills   <color=#8C99BF>{Ago(m.when)}  ></color>";
        }
    }

    void SetBar(int i, string label, PlayerProfile.Tally t)
    {
        var (l, fill, value) = bars[i];
        l.text = label;
        fill.rectTransform.anchorMax = new Vector2(t.games > 0 ? Mathf.Max(0.02f, t.Rate) : 0f, 1f);
        fill.color = Color.Lerp(Loss, Win, t.Rate);
        value.text = t.games > 0 ? $"{t.Rate * 100f:0}%   <color=#8C99BF>{t.wins}W {t.Losses}L</color>" : "<color=#8C99BF>no games</color>";
    }


    void Build()
    {
        built = true;
        var panel = (RectTransform)transform;
        panel.sizeDelta = new Vector2(1560f, 800f);
        panel.anchoredPosition = new Vector2(0f, -112f);
        panel.localScale = Vector3.one * 0.92f;
        var circle = AvatarList.Circle;
        if (viewer)
        {
            var close = SettingsPanel.Button(panel, "BACK", new Vector2(220f, -350f), new Vector2(360f, 64f));
            close.onClick.AddListener(() => Destroy(transform.parent.gameObject));
            SettingsPanel.Button(panel, "ACHIEVEMENTS", new Vector2(-220f, -350f), new Vector2(360f, 64f)).onClick.AddListener(OpenAchievements);
        }
        else
        {
            transform.Find("Header").gameObject.SetActive(false);
            transform.Find("Info").gameObject.SetActive(false);
            var signOut = (RectTransform)transform.Find("SIGNOUTButton");
            var back = (RectTransform)transform.Find("BACKButton");
            signOut.anchoredPosition = new Vector2(-420f, -350f);
            back.anchoredPosition = new Vector2(420f, -350f);
            var ach = SettingsPanel.Button(panel, "ACHIEVEMENTS", new Vector2(0f, -350f), back.sizeDelta);
            HoloSkin.Button(ach, HoloUI.ButtonStyle.Gold);
            ach.onClick.AddListener(OpenAchievements);
        }

        var top = Box(panel, new Vector2(0f, 296f), new Vector2(1480f, 140f), Card);
        avatarView = AvatarList.Make(top, new Vector2(-655f, 0f), 116f, Pink);
        if (!viewer)
        {
            var avatarButton = avatarView.ring.gameObject.AddComponent<Button>();
            avatarView.ring.raycastTarget = true;
            avatarButton.transition = Selectable.Transition.None;
            avatarButton.onClick.AddListener(OpenPicker);
            var edit = Img(avatarView.ring.rectTransform, new Vector2(40f, -40f), new Vector2(34f, 34f), Cyan, circle);
            Txt(edit.rectTransform, "EDIT", Vector2.zero, 11, TextAnchor.MiddleCenter, 34f, Color.black, FontStyle.Bold);
        }
        nameText = Txt(top, "", new Vector2(-330f, 36f), 40, TextAnchor.MiddleLeft, 480f, Color.white, FontStyle.Bold);
        sinceText = Txt(top, "", new Vector2(-330f, 2f), 17, TextAnchor.MiddleLeft, 480f, Dim);
        factionText = Txt(top, "", new Vector2(-330f, -24f), 15, TextAnchor.MiddleLeft, 480f, Dim);
        factionEmblem = Img(top, new Vector2(-556f, -24f), new Vector2(24f, 24f), Color.white, null);
        factionEmblem.preserveAspect = true;
        idText = Txt(top, "", new Vector2(-330f, -48f), 12, TextAnchor.MiddleLeft, 480f, new Color(0.4f, 0.45f, 0.6f));
        for (int i = 0; i < showcase.Length; i++)
        {
            var pos = new Vector2(22f + (i - 1.5f) * 76f, 4f);
            var slot = Img(top, pos, new Vector2(90f, 90f), new Color(1f, 1f, 1f, 0.35f), Badge.Art("frame_0"));
            slot.raycastTarget = true;
            slot.preserveAspect = true;
            showcaseSlot[i] = slot;
            slot.gameObject.AddComponent<Button>().onClick.AddListener(OpenAchievements);
            showcaseEmpty[i] = Txt(slot.transform, "+", Vector2.zero, 26, TextAnchor.MiddleCenter, 70f, Dim);
            showcase[i] = Badge.Make(top, pos, 56f);
            showcaseTip[i] = slot.gameObject.AddComponent<Tooltip>();
        }
        showcaseLabel = Txt(top, "", new Vector2(22f, -50f), 12, TextAnchor.MiddleCenter, 320f, Dim);
        Img(top, new Vector2(180f, 0f), new Vector2(2f, 100f), new Color(1f, 1f, 1f, 0.08f), null);
        Txt(top, "RANK", new Vector2(450f, 40f), 16, TextAnchor.MiddleLeft, 500f, Dim);
        rankText = Txt(top, "", new Vector2(450f, 10f), 34, TextAnchor.MiddleLeft, 500f, Gold, FontStyle.Bold);
        var rankBar = Img(top, new Vector2(450f, -24f), new Vector2(500f, 12f), BarBack, null);
        rankFill = Fill(rankBar.rectTransform, Gold);
        HoloSkin.Bar(rankBar, rankFill, Gold);
        rankNext = Txt(top, "", new Vector2(450f, -46f), 15, TextAnchor.MiddleLeft, 500f, Dim);

        string[] titles = { "MATCHES", "WINS", "LOSSES", "", "STREAK" };
        Color[] tints = { Cyan, Win, Loss, Cyan, Color.white };
        for (int i = 0; i < 5; i++)
        {
            var c = Box(panel, new Vector2(-592f + i * 296f, 150f), new Vector2(276f, 140f), Card);
            Txt(c, titles[i], new Vector2(0f, 46f), 16, TextAnchor.MiddleCenter, 260f, Dim);
            cardValues[i] = Txt(c, "0", new Vector2(0f, 4f), 48, TextAnchor.MiddleCenter, 260f, tints[i], FontStyle.Bold);
            cardSubs[i] = Txt(c, "", new Vector2(0f, -46f), 15, TextAnchor.MiddleCenter, 260f, Dim);
            if (i == 3)
            {
                var back3 = Img(c, new Vector2(0f, 10f), new Vector2(92f, 92f), BarBack, circle);
                ring = Img(back3.rectTransform, Vector2.zero, new Vector2(92f, 92f), Win, circle);
                ring.type = Image.Type.Filled; ring.fillMethod = Image.FillMethod.Radial360; ring.fillOrigin = (int)Image.Origin360.Top; ring.fillClockwise = true;
                var hole = Img(back3.rectTransform, Vector2.zero, new Vector2(70f, 70f), Card, circle);
                ringText = Txt(hole.rectTransform, "-", Vector2.zero, 22, TextAnchor.MiddleCenter, 70f, Color.white, FontStyle.Bold);
                cardSubs[i].rectTransform.anchoredPosition = new Vector2(0f, -54f);
            }
        }

        var left = Box(panel, new Vector2(-372f, -112f), new Vector2(736f, 360f), Card);
        Txt(left, "WIN RATE", new Vector2(-52f, 156f), 18, TextAnchor.MiddleLeft, 600f, Pink, FontStyle.Bold);
        for (int i = 0; i < bars.Length; i++)
        {
            float y = 128f - i * 21f;
            var l = Txt(left, "", new Vector2(-268f, y), 15, TextAnchor.MiddleLeft, 170f, Dim);
            var back5 = Img(left, new Vector2(-18f, y), new Vector2(300f, 14f), BarBack, null);
            var v = Txt(left, "", new Vector2(250f, y), 15, TextAnchor.MiddleLeft, 200f, Color.white);
            bars[i] = (l, Fill(back5.rectTransform, Win), v);
            HoloSkin.Bar(back5, bars[i].fill, Win);
        }
        Txt(left, "CAREER", new Vector2(-52f, -22f), 18, TextAnchor.MiddleLeft, 600f, Pink, FontStyle.Bold);
        string[] totalNames = { "KILLS", "UNITS BUILT", "UNITS LOST", "BUILDINGS BUILT", "BUILDINGS LOST", "RESEARCH", "CREDITS EARNED", "AVG MATCH", "KILLS / MATCH" };
        for (int i = 0; i < totalValues.Length; i++)
        {
            var pos = new Vector2(-240f + (i % 3) * 240f, -56f - (i / 3) * 42f);
            totalValues[i] = Txt(left, "0", pos + new Vector2(0f, 7f), 20, TextAnchor.MiddleCenter, 220f, Color.white, FontStyle.Bold);
            Txt(left, totalNames[i], pos + new Vector2(0f, -13f), 12, TextAnchor.MiddleCenter, 220f, Dim);
        }

        var right = Box(panel, new Vector2(372f, -112f), new Vector2(736f, 360f), Card);
        Txt(right, "RECENT MATCHES", new Vector2(-52f, 156f), 18, TextAnchor.MiddleLeft, 600f, Pink, FontStyle.Bold);
        noMatches = Txt(right, "No matches yet.\nFinish a skirmish or multiplayer game and it shows up here.", Vector2.zero, 18, TextAnchor.MiddleCenter, 600f, Dim);
        noMatches.rectTransform.sizeDelta = new Vector2(600f, 80f);
        for (int i = 0; i < recent.Length; i++)
        {
            float y = 124f - i * 30f;
            var row = Img(right, new Vector2(0f, y), new Vector2(700f, 27f), i % 2 == 0 ? new Color(1f, 1f, 1f, 0.03f) : new Color(0f, 0f, 0f, 0f), null);
            var badge = Img(row.rectTransform, new Vector2(-334f, 0f), new Vector2(24f, 22f), Win, null);
            var res = Txt(badge.rectTransform, "W", Vector2.zero, 15, TextAnchor.MiddleCenter, 24f, Color.black, FontStyle.Bold);
            var what = Txt(row.rectTransform, "", new Vector2(-90f, 0f), 15, TextAnchor.MiddleLeft, 450f, Color.white);
            var when = Txt(row.rectTransform, "", new Vector2(222f, 0f), 14, TextAnchor.MiddleRight, 240f, Color.white);
            row.raycastTarget = true;
            var rowButton = row.gameObject.AddComponent<Button>();
            rowButton.transition = Selectable.Transition.ColorTint;
            rowButton.colors = new ColorBlock { normalColor = Color.white, highlightedColor = new Color(1.6f, 1.6f, 2.2f), pressedColor = new Color(0.8f, 0.8f, 0.9f), selectedColor = Color.white, disabledColor = Color.white, colorMultiplier = 1f, fadeDuration = 0.08f };
            int index = i;
            rowButton.onClick.AddListener(() => { var p = shown; if (p != null && index < p.recent.Count) ShowMatch(p.recent[index]); });
            recent[i] = (row.gameObject, badge, res, what, when);
        }

        note = Txt(panel, "", new Vector2(0f, -304f), 14, TextAnchor.MiddleCenter, 1200f, new Color(0.45f, 0.5f, 0.65f));
    }

    RectTransform Box(Transform parent, Vector2 pos, Vector2 size, Color fill)
    {
        var img = Img(parent, pos, size, fill, null);
        HoloSkin.Back(img.rectTransform, "Panel_Inset");
        return img.rectTransform;
    }

    static Image Img(Transform parent, Vector2 pos, Vector2 size, Color color, Sprite sprite)
    {
        var img = new GameObject("Image", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.sizeDelta = size;
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Image Fill(RectTransform bar, Color color)
    {
        var f = Img(bar, Vector2.zero, Vector2.zero, color, null);
        f.rectTransform.anchorMin = Vector2.zero; f.rectTransform.anchorMax = new Vector2(0f, 1f);
        f.rectTransform.offsetMin = f.rectTransform.offsetMax = Vector2.zero;
        return f;
    }

    static Text Txt(Transform parent, string text, Vector2 pos, int size, TextAnchor align, float width, Color color, FontStyle style = FontStyle.Normal)
    {
        var t = SettingsPanel.Label(parent, text, pos, size, align, width);
        t.color = color;
        t.fontStyle = style;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    GameObject picker;
    Text pickedName;
    readonly System.Collections.Generic.List<(string name, Image glow)> cells = new();

    void OpenPicker()
    {
        if (picker == null) BuildPicker();
        picker.SetActive(true);
        picker.transform.SetAsLastSibling();
        MarkPicked();
    }

    void BuildPicker()
    {
        var shade = Img(transform.parent, Vector2.zero, Vector2.zero, new Color(0f, 0.01f, 0.04f, 0.85f), null);
        SettingsPanel.Stretch(shade.rectTransform);
        shade.raycastTarget = true;
        picker = shade.gameObject;
        picker.name = "AvatarPicker";
        HoloSkin.Layer(shade, HoloSkin.LayerPopup + 5);
        var win = Img(shade.transform, new Vector2(0f, -30f), new Vector2(1080f, 720f), Card, null);
        HoloSkin.Modal(win.rectTransform);
        Img(win.transform, Vector2.zero, new Vector2(1064f, 704f), new Color(0.01f, 0.015f, 0.04f, 0.96f), null).transform.SetSiblingIndex(1);   // account screen showed through
        Txt(win.transform, "CHOOSE YOUR AVATAR", new Vector2(0f, 312f), 34, TextAnchor.MiddleCenter, 900f, Cyan, FontStyle.Bold);
        Txt(win.transform, "Shown on your account and to your friends.", new Vector2(0f, 274f), 17, TextAnchor.MiddleCenter, 900f, Dim);

        // one page of avatars per faction, switched by tabs (the "initial" option sits first on every page)
        var groups = AvatarList.Groups;
        var pages = new GameObject[groups.Length];
        var tabs = new Button[groups.Length];
        void ShowPage(int k)
        {
            for (int j = 0; j < pages.Length; j++)
            {
                pages[j].SetActive(j == k);
                tabs[j].GetComponentInChildren<Text>().color = j == k ? Color.white : Dim;
                tabs[j].transform.localScale = Vector3.one * (j == k ? 1f : 0.92f);
            }
        }
        string current = ProfileService.Current?.avatar ?? "";
        int start = 0;
        for (int g = 0; g < groups.Length; g++)
        {
            var f = Fac((int)groups[g].faction);
            int k = g;
            tabs[g] = SettingsPanel.Button(win.transform, f != null ? f.displayName.ToUpper() : groups[g].faction.ToString().ToUpper(), new Vector2((g - (groups.Length - 1) / 2f) * 250f, 232f), new Vector2(230f, 44f));
            tabs[g].onClick.AddListener(() => ShowPage(k));
            var page = new GameObject("Page_" + groups[g].faction, typeof(RectTransform));
            page.transform.SetParent(win.transform, false);
            pages[g] = page;
            var options = new System.Collections.Generic.List<string> { "" };
            foreach (var s in groups[g].sprites) if (s != null) { options.Add(s.name); if (s.name == current) start = g; }
            BuildCells(page.transform, options);
        }
        if (groups.Length > 0) ShowPage(start);

        pickedName = Txt(win.transform, "", new Vector2(0f, -246f), 24, TextAnchor.MiddleCenter, 900f, Gold, FontStyle.Bold);
        SettingsPanel.Button(win.transform, "DONE", new Vector2(160f, -296f), new Vector2(280f, 60f)).onClick.AddListener(() => picker.SetActive(false));
        var upload = SettingsPanel.Button(win.transform, "UPLOAD FROM PC", new Vector2(-160f, -296f), new Vector2(280f, 60f));
        HoloSkin.Button(upload, HoloUI.ButtonStyle.Gold);
        upload.interactable = CustomAvatar.CanPick;
        upload.onClick.AddListener(Upload);
    }

    void BuildCells(Transform page, System.Collections.Generic.List<string> options)
    {
        const int cols = 8;
        const float cell = 96f, gap = 12f;
        for (int i = 0; i < options.Count; i++)
        {
            string name = options[i];
            var pos = new Vector2((i % cols - (cols - 1) / 2f) * (cell + gap), 150f - i / cols * (cell + gap));
            var glow = Img(page, pos, Vector2.one * (cell + 10f), Cyan, null);
            var bg = Box(page, pos, Vector2.one * cell, new Color(0.05f, 0.07f, 0.15f, 1f));
            var bgImg = bg.GetComponent<Image>();
            bgImg.raycastTarget = true;
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bgImg;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.6f, 1.6f, 1.6f);
            b.colors = colors;
            b.onClick.AddListener(() => Pick(name));
            if (name == "") AvatarList.Make(bg, Vector2.zero, 76f, Dim).Show("", AccountService.Username);
            else Img(bg, Vector2.zero, Vector2.one * 84f, Color.white, AvatarList.Find(name)).preserveAspect = true;
            cells.Add((name, glow));
        }
    }

    void Upload()
    {
        string path = CustomAvatar.PickFile();
        if (string.IsNullOrEmpty(path)) return;
        string data = CustomAvatar.FromFile(path, out string error);
        if (data == null) { pickedName.text = error; return; }
        Pick(data);
    }

    void Pick(string name)
    {
        ProfileService.SetAvatar(name);
        avatarView.Show(name, AccountService.Username);
        MarkPicked();
    }

    void MarkPicked()
    {
        string current = ProfileService.Current?.avatar ?? "";
        foreach (var (name, glow) in cells) glow.enabled = name == current;
        pickedName.text = AvatarList.Title(current);
    }


    GameObject matchView;

    void ShowMatch(PlayerProfile.MatchRecord m)
    {
        if (matchView != null) Destroy(matchView);
        var shade = Img(transform.parent, Vector2.zero, Vector2.zero, new Color(0f, 0.01f, 0.04f, 0.85f), null);
        SettingsPanel.Stretch(shade.rectTransform);
        shade.raycastTarget = true;
        matchView = shade.gameObject;
        matchView.name = "MatchDetail";
        HoloSkin.Layer(shade, HoloSkin.LayerPopup + 5);
        shade.gameObject.AddComponent<Button>().onClick.AddListener(() => Destroy(matchView));

        var win = Img(shade.transform, Vector2.zero, new Vector2(900f, 560f), Card, null);
        win.raycastTarget = true;
        HoloSkin.Modal(win.rectTransform);
        Img(win.transform, Vector2.zero, new Vector2(884f, 544f), new Color(0.01f, 0.015f, 0.04f, 0.94f), null).transform.SetSiblingIndex(1);
        Color result = m.won ? Win : Loss;
        Txt(win.transform, m.won ? "VICTORY" : "DEFEAT", new Vector2(0f, 222f), 44, TextAnchor.MiddleCenter, 800f, result, FontStyle.Bold);
        var when = new System.DateTime(m.when, System.DateTimeKind.Utc).ToLocalTime();
        var played = Fac(m.faction);
        Txt(win.transform, $"{(played != null ? $"<color={Hex(played.color)}>{played.displayName.ToUpper()}</color>   ·   " : "")}{(m.online ? "MULTIPLAYER" : "SKIRMISH")}   ·   {(string.IsNullOrEmpty(m.map) ? "Unknown map" : m.map)}   ·   {when:MMM d, yyyy  h:mm tt}",
            new Vector2(0f, 176f), 18, TextAnchor.MiddleCenter, 840f, Dim);
        Txt(win.transform, $"vs {m.opponents}", new Vector2(0f, 146f), 20, TextAnchor.MiddleCenter, 840f, Color.white);

        string credits = m.startCredits > 0 ? $"${m.startCredits:N0}" : "-";
        var top = new[] { ("DURATION", Clock(m.seconds)), ("KILLS", m.kills.ToString()), ("UNITS LOST", m.unitsLost.ToString()), ("START CREDITS", credits) };
        for (int i = 0; i < top.Length; i++)
        {
            var box = Img(win.transform, new Vector2(-315f + i * 210f, 70f), new Vector2(190f, 92f), new Color(1f, 1f, 1f, 0.04f), null);
            Txt(box.transform, top[i].Item2, new Vector2(0f, 12f), 32, TextAnchor.MiddleCenter, 180f, i == 0 ? Cyan : Color.white, FontStyle.Bold);
            Txt(box.transform, top[i].Item1, new Vector2(0f, -26f), 13, TextAnchor.MiddleCenter, 180f, Dim);
        }

        if (m.stats != null && m.stats.Length >= MatchStats.Headers.Length)
        {
            Txt(win.transform, "MATCH STATS", new Vector2(-220f, -24f), 16, TextAnchor.MiddleLeft, 300f, Pink, FontStyle.Bold);
            int n = MatchStats.Headers.Length;
            for (int i = 0; i < n; i++)
            {
                float x = -360f + i * (720f / (n - 1));
                string v = i == (int)MatchStats.Kind.Earned ? "$" + Short(m.stats[i]) : m.stats[i].ToString("N0");
                Txt(win.transform, v, new Vector2(x, -70f), 26, TextAnchor.MiddleCenter, 120f, Color.white, FontStyle.Bold);
                var label = Txt(win.transform, MatchStats.Headers[i], new Vector2(x, -110f), 12, TextAnchor.MiddleCenter, 120f, Dim);
                label.rectTransform.sizeDelta = new Vector2(120f, 34f);
            }
            int built = m.stats[(int)MatchStats.Kind.UnitsBuilt], lost = m.stats[(int)MatchStats.Kind.UnitsLost];
            float kd = lost > 0 ? (float)m.kills / lost : m.kills;
            Txt(win.transform, $"K/D {kd:0.00}   ·   {(built > 0 ? $"{100f * (built - Mathf.Min(lost, built)) / built:0}% of units survived" : "no units built")}   ·   ${(m.seconds > 0f ? m.stats[(int)MatchStats.Kind.Earned] / (m.seconds / 60f) : 0f):N0} earned per minute",
                new Vector2(0f, -160f), 16, TextAnchor.MiddleCenter, 840f, Gold);
        }
        else Txt(win.transform, "Detailed stats weren't recorded for this match.\nMatches played from now on keep the full breakdown.", new Vector2(0f, -90f), 17, TextAnchor.MiddleCenter, 800f, Dim)
            .rectTransform.sizeDelta = new Vector2(800f, 60f);

        var close = SettingsPanel.Button(win.transform, "CLOSE", new Vector2(0f, -232f), new Vector2(260f, 52f));
        close.onClick.AddListener(() => Destroy(matchView));
    }

    void OnDisable() { if (matchView != null) Destroy(matchView); }

    static string Clock(float s) => $"{(int)s / 60}:{(int)s % 60:00}";
    static string Hours(float s) => s >= 3600f ? $"{s / 3600f:0.0}h" : $"{Mathf.RoundToInt(s / 60f)}m";
    static string Short(int v) => v >= 1000000 ? $"{v / 1000000f:0.#}M" : v >= 1000 ? $"{v / 1000f:0.#}k" : v.ToString();

    static string Ago(long ticks)
    {
        var span = System.DateTime.UtcNow - new System.DateTime(ticks, System.DateTimeKind.Utc);
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return new System.DateTime(ticks, System.DateTimeKind.Utc).ToLocalTime().ToString("MMM d");
    }
}
