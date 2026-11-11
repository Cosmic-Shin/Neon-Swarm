using UnityEngine;
using UnityEngine.UI;

// Main menu > Account: who you are and how you play. Built in code inside the scene's AccountPanel
// (its SIGN OUT / BACK buttons are kept); LobbyMenu calls Show(). Numbers come from ProfileService.
//   top:    avatar, name, rank + progress to the next, member since, player ID
//   cards:  matches, wins, losses, win rate (ring), streak
//   left:   win rate by mode and by AI difficulty, career totals
//   right:  the last 10 matches
public class AccountScreen : MonoBehaviour
{
    static readonly Color Cyan = new(0.3f, 1f, 1f), Pink = new(1f, 0.1f, 0.9f), Win = new(0.35f, 1f, 0.55f), Loss = new(1f, 0.32f, 0.38f),
        Card = new(0.02f, 0.03f, 0.08f, 1f), Dim = new(0.55f, 0.6f, 0.75f), BarBack = new(0.12f, 0.12f, 0.2f, 1f), Gold = new(1f, 0.8f, 0.25f);

    Sprite circle;
    Text avatarLetter, nameText, rankText, sinceText, idText, rankNext, note;
    Image avatar, rankFill, ring;
    Text ringText;
    readonly Text[] cardValues = new Text[5], cardSubs = new Text[5];
    readonly (Text label, Image fill, Text value)[] bars = new (Text, Image, Text)[5];
    readonly Text[] totalValues = new Text[9];
    readonly (GameObject row, Image badge, Text result, Text what, Text when)[] recent = new (GameObject, Image, Text, Text, Text)[PlayerProfile.RecentKept];
    Text noMatches;
    bool built;

    public async void Show()
    {
        if (!built) Build();
        nameText.text = AccountService.Username?.ToUpper() ?? "GUEST";
        avatarLetter.text = string.IsNullOrEmpty(AccountService.Username) ? "?" : AccountService.Username.Substring(0, 1).ToUpper();
        avatar.color = Color.HSVToRGB(Mathf.Abs((AccountService.Username ?? "").GetHashCode() % 360) / 360f, 0.75f, 1f);
        idText.text = "PLAYER ID  " + (AccountService.SignedIn ? AccountService.PlayerId : "-");
        sinceText.text = "Member since ...";
        note.text = "Loading stats...";
        Fill(ProfileService.Current ?? new PlayerProfile());

        var profile = await ProfileService.Load();
        if (this == null) return; // closed meanwhile
        Fill(profile);
        note.text = ProfileService.LastError ?? "Stats are saved to your account (Unity Cloud Save).";
        try
        {
            var since = await AccountService.MemberSince();
            if (this != null) sinceText.text = "Member since " + (since?.ToLocalTime().ToString("MMMM d, yyyy") ?? "unknown");
        }
        catch (System.Exception e) { if (this != null) sinceText.text = "Member since: unavailable"; Debug.LogWarning(e); }
    }

    void Fill(PlayerProfile p)
    {
        int r = p.RankIndex;
        rankText.text = PlayerProfile.Ranks[r].name;
        rankFill.rectTransform.anchorMax = new Vector2(p.RankProgress, 1f);
        rankNext.text = r + 1 < PlayerProfile.Ranks.Length ? $"{PlayerProfile.Ranks[r + 1].wins - p.all.wins} more wins to {PlayerProfile.Ranks[r + 1].name}" : "Top rank reached";

        cardValues[0].text = p.all.games.ToString();
        cardSubs[0].text = Hours(p.secondsPlayed) + " played";
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
        SetBar(2, "VS EASY AI", p.vsAI[0]);
        SetBar(3, "VS NORMAL AI", p.vsAI[1]);
        SetBar(4, "VS HARD AI", p.vsAI[2]);

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
            row.what.text = $"<b>{(m.online ? "MULTIPLAYER" : "SKIRMISH")}</b>  <color=#8C99BF>vs {m.opponents}</color>";
            row.when.text = $"{Clock(m.seconds)}   {m.kills} kills   <color=#8C99BF>{Ago(m.when)}</color>";
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

    // ---- layout ----

    void Build()
    {
        built = true;
        var panel = (RectTransform)transform;
        panel.sizeDelta = new Vector2(1560f, 800f);
        panel.anchoredPosition = new Vector2(0f, -70f); // clear of the game title
        circle = MakeCircle();
        transform.Find("Header").gameObject.SetActive(false);
        transform.Find("Info").gameObject.SetActive(false);
        var signOut = (RectTransform)transform.Find("SIGNOUTButton");
        var back = (RectTransform)transform.Find("BACKButton");
        signOut.anchoredPosition = new Vector2(-210f, -350f);
        back.anchoredPosition = new Vector2(210f, -350f);

        // Top: identity.
        var top = Box(panel, new Vector2(0f, 300f), new Vector2(1480f, 130f), Card);
        var ringBack = Img(top, new Vector2(-660f, 0f), new Vector2(100f, 100f), Pink, circle);
        avatar = Img(ringBack.rectTransform, Vector2.zero, new Vector2(88f, 88f), Cyan, circle);
        avatarLetter = Txt(avatar.rectTransform, "?", Vector2.zero, 52, TextAnchor.MiddleCenter, 88f, Color.black, FontStyle.Bold);
        nameText = Txt(top, "", new Vector2(-370f, 26f), 40, TextAnchor.MiddleLeft, 480f, Color.white, FontStyle.Bold);
        sinceText = Txt(top, "", new Vector2(-370f, -16f), 18, TextAnchor.MiddleLeft, 480f, Dim);
        idText = Txt(top, "", new Vector2(-370f, -42f), 13, TextAnchor.MiddleLeft, 480f, new Color(0.4f, 0.45f, 0.6f));
        Txt(top, "RANK", new Vector2(380f, 38f), 16, TextAnchor.MiddleLeft, 560f, Dim);
        rankText = Txt(top, "", new Vector2(380f, 8f), 34, TextAnchor.MiddleLeft, 560f, Gold, FontStyle.Bold);
        var rankBar = Img(top, new Vector2(380f, -26f), new Vector2(560f, 12f), BarBack, null);
        rankFill = Fill(rankBar.rectTransform, Gold);
        HoloSkin.Bar(rankBar, rankFill, Gold);
        rankNext = Txt(top, "", new Vector2(380f, -48f), 15, TextAnchor.MiddleLeft, 560f, Dim);

        // Cards.
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

        // Left: win rates, then career totals.
        var left = Box(panel, new Vector2(-372f, -112f), new Vector2(736f, 360f), Card);
        Txt(left, "WIN RATE", new Vector2(-52f, 156f), 18, TextAnchor.MiddleLeft, 600f, Pink, FontStyle.Bold);
        for (int i = 0; i < bars.Length; i++)
        {
            float y = 122f - i * 30f;
            var l = Txt(left, "", new Vector2(-268f, y), 15, TextAnchor.MiddleLeft, 170f, Dim);
            var back5 = Img(left, new Vector2(-18f, y), new Vector2(300f, 14f), BarBack, null);
            var v = Txt(left, "", new Vector2(250f, y), 15, TextAnchor.MiddleLeft, 200f, Color.white);
            bars[i] = (l, Fill(back5.rectTransform, Win), v);
            HoloSkin.Bar(back5, bars[i].fill, Win);
        }
        Txt(left, "CAREER", new Vector2(-52f, -36f), 18, TextAnchor.MiddleLeft, 600f, Pink, FontStyle.Bold);
        string[] totalNames = { "KILLS", "UNITS BUILT", "UNITS LOST", "BUILDINGS BUILT", "BUILDINGS LOST", "RESEARCH", "CREDITS EARNED", "AVG MATCH", "KILLS / MATCH" };
        for (int i = 0; i < totalValues.Length; i++)
        {
            var pos = new Vector2(-240f + (i % 3) * 240f, -72f - (i / 3) * 40f);
            totalValues[i] = Txt(left, "0", pos + new Vector2(0f, 8f), 22, TextAnchor.MiddleCenter, 220f, Color.white, FontStyle.Bold);
            Txt(left, totalNames[i], pos + new Vector2(0f, -14f), 12, TextAnchor.MiddleCenter, 220f, Dim);
        }

        // Right: recent matches.
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
            recent[i] = (row.gameObject, badge, res, what, when);
        }

        note = Txt(panel, "", new Vector2(0f, -310f), 14, TextAnchor.MiddleCenter, 1200f, new Color(0.45f, 0.5f, 0.65f));
    }

    // A stat box: Panel_Inset (Holo kit).
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

    // A bar's fill: grows from the left as its anchorMax.x goes 0..1.
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
        t.verticalOverflow = VerticalWrapMode.Overflow; // big numbers are taller than the label box
        return t;
    }

    static Sprite MakeCircle()
    {
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(n / 2f - d) * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    // ---- formatting ----

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
