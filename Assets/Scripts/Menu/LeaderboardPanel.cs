using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Leaderboards;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardPanel : MonoBehaviour
{
    public const string BoardId = "Neon_Swarm_Global_Leaderboard";
    const int Rows = 10, MaxPlayers = 50;

    static readonly Color Cyan = new(0.3f, 1f, 1f), Gold = new(1f, 0.8f, 0.25f), Dim = new(0.55f, 0.6f, 0.75f),
        Card = new(0.02f, 0.03f, 0.08f, 1f), Me = new(0.3f, 1f, 1f, 0.12f), Odd = new(1f, 1f, 1f, 0.03f), Even = new(1f, 1f, 1f, 0.001f);

    readonly (Image back, Text rank, Text name, Text wins, Text extra)[] rows = new (Image, Text, Text, Text, Text)[Rows];
    readonly string[] rowIds = new string[Rows];
    readonly string[] rowNames = new string[Rows];
    Text status, extraHeader, pageText, mine;
    Button globalTab, friendsTab, prev, next;
    bool friends;
    int request, page, pages = 1;
    List<Entry> friendList;

    public static void Open(Transform parent)
    {
        var shade = new GameObject("Leaderboard", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        shade.SetParent(parent, false);
        SettingsPanel.Stretch(shade);
        shade.GetComponent<Image>().color = new Color(0f, 0.01f, 0.04f, 0.88f);
        HoloSkin.Layer(shade, HoloSkin.LayerPopup + 3);
        shade.gameObject.AddComponent<LeaderboardPanel>().Build(shade);
    }

    public static async void Submit(int wins)
    {
        if (!AccountService.SignedIn || wins <= 0) return;
        try { await LeaderboardsService.Instance.AddPlayerScoreAsync(BoardId, wins); }
        catch (System.Exception e) { Debug.LogWarning("[Leaderboard] " + e.Message); }
    }

    void Build(RectTransform shade)
    {
        var win = Box(shade, Vector2.zero, new Vector2(900f, 780f), Card);
        HoloSkin.Modal(win);
        Box(win, Vector2.zero, new Vector2(884f, 764f), new Color(0.01f, 0.015f, 0.04f, 0.94f)).SetSiblingIndex(1);
        Label(win, "LEADERBOARD", new Vector2(0f, 340f), 36, TextAnchor.MiddleCenter, 800f, Cyan, FontStyle.Bold);
        globalTab = SettingsPanel.Button(win, "GLOBAL", new Vector2(-110f, 282f), new Vector2(200f, 46f));
        friendsTab = SettingsPanel.Button(win, "FRIENDS", new Vector2(110f, 282f), new Vector2(200f, 46f));
        globalTab.onClick.AddListener(() => { page = 0; Show(false); });
        friendsTab.onClick.AddListener(() => { page = 0; Show(true); });

        Label(win, "#", new Vector2(-380f, 232f), 15, TextAnchor.MiddleLeft, 60f, Dim, FontStyle.Bold);
        Label(win, "PLAYER", new Vector2(-300f, 232f), 15, TextAnchor.MiddleLeft, 300f, Dim, FontStyle.Bold);
        Label(win, "WINS", new Vector2(150f, 232f), 15, TextAnchor.MiddleCenter, 120f, Dim, FontStyle.Bold);
        extraHeader = Label(win, "", new Vector2(310f, 232f), 15, TextAnchor.MiddleCenter, 180f, Dim, FontStyle.Bold);
        for (int i = 0; i < Rows; i++)
        {
            float y = 192f - i * 42f;
            var back = Box(win, new Vector2(0f, y), new Vector2(820f, 38f), i % 2 == 0 ? Odd : Even).GetComponent<Image>();
            back.raycastTarget = true;
            var b = back.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.ColorTint;
            b.colors = new ColorBlock { normalColor = Color.white, highlightedColor = new Color(3f, 3f, 4f, 6f), pressedColor = new Color(2f, 2f, 3f, 4f), selectedColor = Color.white, disabledColor = Color.white, colorMultiplier = 1f, fadeDuration = 0.06f };
            int index = i;
            b.onClick.AddListener(() => OpenProfile(index));
            rows[i] = (back,
                Label(back.transform, "", new Vector2(-380f, 0f), 19, TextAnchor.MiddleLeft, 60f, Gold, FontStyle.Bold),
                Label(back.transform, "", new Vector2(-300f, 0f), 19, TextAnchor.MiddleLeft, 380f, Color.white),
                Label(back.transform, "", new Vector2(150f, 0f), 20, TextAnchor.MiddleCenter, 120f, Color.white, FontStyle.Bold),
                Label(back.transform, "", new Vector2(310f, 0f), 17, TextAnchor.MiddleCenter, 180f, Dim));
        }

        prev = SettingsPanel.Button(win, "<  PREV", new Vector2(-250f, -248f), new Vector2(170f, 44f));
        next = SettingsPanel.Button(win, "NEXT  >", new Vector2(250f, -248f), new Vector2(170f, 44f));
        HoloSkin.Button(prev, HoloUI.ButtonStyle.Small);
        HoloSkin.Button(next, HoloUI.ButtonStyle.Small);
        prev.onClick.AddListener(() => { if (page > 0) { page--; Show(friends); } });
        next.onClick.AddListener(() => { if (page + 1 < pages) { page++; Show(friends); } });
        pageText = Label(win, "", new Vector2(0f, -248f), 18, TextAnchor.MiddleCenter, 300f, Color.white, FontStyle.Bold);
        mine = Label(win, "", new Vector2(0f, -292f), 17, TextAnchor.MiddleCenter, 820f, Cyan);
        status = Label(win, "", new Vector2(0f, -322f), 14, TextAnchor.MiddleCenter, 820f, Dim);
        status.rectTransform.sizeDelta = new Vector2(820f, 40f);
        SettingsPanel.Button(win, "CLOSE", new Vector2(0f, -362f), new Vector2(240f, 46f)).onClick.AddListener(() => Destroy(gameObject));
        Show(false);
    }

    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && transform.parent.Find("ProfileView") == null) Destroy(gameObject);
    }

    void OpenProfile(int i)
    {
        if (string.IsNullOrEmpty(rowIds[i])) return;
        if (rowIds[i] == AccountService.PlayerId) return;
        AccountScreen.View(transform.parent, rowIds[i], rowNames[i]);
    }

    async void Show(bool showFriends)
    {
        if (friends != showFriends) friendList = null;
        friends = showFriends;
        HoloSkin.SetOn(globalTab, !friends);
        HoloSkin.SetOn(friendsTab, friends);
        extraHeader.text = friends ? "WIN RATE" : "";
        foreach (var r in rows) r.back.gameObject.SetActive(false);
        status.text = "Loading...";
        mine.text = "";
        prev.interactable = next.interactable = false;
        int ticket = ++request;
        var list = friends ? await LoadFriends() : await LoadGlobal();
        if (this == null || ticket != request) return;
        for (int i = 0; i < Rows; i++)
        {
            bool used = i < list.Count;
            rows[i].back.gameObject.SetActive(used);
            rowIds[i] = used ? list[i].id : null;
            rowNames[i] = used ? list[i].name : null;
            if (!used) continue;
            var e = list[i];
            rows[i].back.color = e.me ? Me : i % 2 == 0 ? Odd : Even;
            rows[i].rank.text = e.rank > 0 ? e.rank.ToString() : "-";
            rows[i].rank.color = e.rank == 1 ? Gold : e.rank == 2 ? new Color(0.8f, 0.85f, 0.95f) : e.rank == 3 ? new Color(0.85f, 0.55f, 0.3f) : Dim;
            rows[i].name.text = e.name + (e.me ? "  <color=#4DFFFF>(you)</color>" : "");
            rows[i].wins.text = e.wins.ToString("N0");
            rows[i].extra.text = e.extra;
        }
        pageText.text = $"PAGE {page + 1} / {pages}";
        prev.interactable = page > 0;
        next.interactable = page + 1 < pages;
    }

    struct Entry { public int rank, wins; public string id, name, extra; public bool me; }

    async Task<List<Entry>> LoadGlobal()
    {
        var list = new List<Entry>();
        pages = 1;
        if (!AccountService.SignedIn) { status.text = "Sign in to see the leaderboard."; return list; }
        try
        {
            var result = await LeaderboardsService.Instance.GetScoresAsync(BoardId, new GetScoresOptions { Offset = page * Rows, Limit = Rows });
            int total = Mathf.Min(result.Total, MaxPlayers);
            pages = Mathf.Max(1, Mathf.CeilToInt(total / (float)Rows));
            foreach (var e in result.Results)
            {
                if (e.Rank >= MaxPlayers) break;
                list.Add(new Entry { rank = e.Rank + 1, wins = (int)e.Score, id = e.PlayerId, name = Short(e.PlayerName), me = e.PlayerId == AccountService.PlayerId });
            }
            try
            {
                var me = await LeaderboardsService.Instance.GetPlayerScoreAsync(BoardId);
                mine.text = me.Rank < MaxPlayers ? $"You are #{me.Rank + 1} with {me.Score:N0} wins." : $"Your rank: #{me.Rank + 1} ({me.Score:N0} wins). Top {MaxPlayers} are shown.";
            }
            catch { mine.text = "Win a match to get on the board."; }
            status.text = list.Count == 0 ? "No scores yet. Win a match to get on the board!" : $"Top {MaxPlayers} players by total wins. Click a player to see their profile.";
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Leaderboard] " + e.Message);
            status.text = "Couldn't reach the global leaderboard. Check your connection and try again.";
        }
        return list;
    }

    async Task<List<Entry>> LoadFriends()
    {
        if (!AccountService.SignedIn) { status.text = "Sign in to compare with your friends."; pages = 1; return new List<Entry>(); }
        if (friendList == null)
        {
            var all = new List<Entry>();
            var me = ProfileService.Current ?? await ProfileService.Load();
            all.Add(new Entry { wins = me.all.wins, id = AccountService.PlayerId, name = AccountService.Username, me = true, extra = Rate(me) });
            var jobs = new List<(string id, string name, Task<PlayerProfile> task)>();
            foreach (var r in FriendsHub.Friends) jobs.Add((r.Member.Id, FriendsHub.NameOf(r.Member), ProfileService.LoadPublic(r.Member.Id)));
            foreach (var (id, name, task) in jobs)
            {
                var p = await task;
                all.Add(new Entry { wins = p != null ? p.all.wins : 0, id = id, name = name, extra = p != null ? Rate(p) : "no games" });
            }
            all.Sort((a, b) => b.wins.CompareTo(a.wins));
            for (int i = 0; i < all.Count; i++) { var e = all[i]; e.rank = i + 1; all[i] = e; }
            if (all.Count > MaxPlayers) all.RemoveRange(MaxPlayers, all.Count - MaxPlayers);
            friendList = all;
            status.text = jobs.Count == 0 ? "Add friends (FRIENDS on the main menu) to compare wins here." : "You and your friends by total wins. Click a friend to see their profile.";
            int you = all.FindIndex(e => e.me);
            mine.text = you >= 0 ? $"You are #{you + 1} of {all.Count}." : "";
        }
        pages = Mathf.Max(1, Mathf.CeilToInt(friendList.Count / (float)Rows));
        page = Mathf.Clamp(page, 0, pages - 1);
        return friendList.GetRange(page * Rows, Mathf.Min(Rows, friendList.Count - page * Rows));
    }

    static string Rate(PlayerProfile p) => p.all.games > 0 ? $"{p.all.Rate * 100f:0}%  <size=13>({p.all.games} games)</size>" : "no games";
    static string Short(string n) { if (string.IsNullOrEmpty(n)) return "Player"; int h = n.LastIndexOf('#'); return h > 0 ? n.Substring(0, h) : n; }

    static RectTransform Box(Transform parent, Vector2 pos, Vector2 size, Color c)
    {
        var r = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        var img = r.GetComponent<Image>();
        img.color = c;
        img.raycastTarget = c.a > 0.5f;
        return r;
    }

    static Text Label(Transform parent, string text, Vector2 pos, int size, TextAnchor align, float width, Color c, FontStyle style = FontStyle.Normal)
    {
        var t = SettingsPanel.Label(parent, text, pos, size, align, width);
        t.font = style == FontStyle.Bold ? HoloSkin.BoldFont : HoloSkin.Font;
        t.color = c;
        t.supportRichText = true;
        if (align == TextAnchor.MiddleLeft) t.rectTransform.pivot = new Vector2(0f, 0.5f);
        return t;
    }
}
