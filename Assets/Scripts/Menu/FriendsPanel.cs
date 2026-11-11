using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static SettingsPanel; // Label, Button, Stretch, colours

// Friends overlay (opened from the main menu, room browser and room screen), built in code:
//   your tag + COPY, add by tag, incoming requests, friends (online first) with JOIN / INVITE / VIEW, sent requests.
//   VIEW opens a friend's card: their public stats, a chat with them, REMOVE and BLOCK.
// Also shows the invite pop-up when a friend invites you while you're in the menus.
public class FriendsPanel : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    static readonly Color Online = new(0.35f, 1f, 0.55f), Offline = new(0.4f, 0.42f, 0.5f), Dim = new(0.55f, 0.6f, 0.75f), Pink = new(1f, 0.1f, 0.9f);
    const float RowH = 64f, Width = 700f;

    RectTransform content;
    int strangersShown = -1; // redraw when someone joins or leaves the room
    Text tag, status;
    InputField addBox;
    RectTransform box, card;  // card: one friend's stats + chat, over the list
    string cardId, cardName;  // whose card is open
    Text cardChat, cardStatus;

    // A menu button that opens the panel; its label shows pending requests.
    public static void Hook(Button b)
    {
        var label = b.GetComponentInChildren<Text>();
        b.onClick.AddListener(() => Open(b));
        Action update = null;
        update = () =>
        {
            if (!b) { FriendsHub.Changed -= update; return; }
            int n = FriendsHub.Incoming.Count + FriendsHub.UnreadTotal;
            label.text = n > 0 ? $"FRIENDS ({n})" : "FRIENDS";
        };
        FriendsHub.Changed += update;
        update();
    }

    public static void Open(Component anyUI)
    {
        if (IsOpen) return;
        var canvas = anyUI.GetComponentInParent<Canvas>().rootCanvas;
        var root = new GameObject("Friends", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        root.SetParent(canvas.transform, false);
        Stretch(root);
        root.GetComponent<Image>().color = Back;
        root.gameObject.AddComponent<FriendsPanel>().Build(root);
        IsOpen = true;
    }

    void OnDestroy() { IsOpen = false; FriendsHub.Changed -= Redraw; }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { if (card) CloseCard(); else Destroy(gameObject); }
        if (FriendsHub.CurrentRoom != null && Time.frameCount % 30 == 0 && RoomPlayer.Strangers().Count != strangersShown) Redraw();
    }

    void Build(RectTransform root)
    {
        box = Box(root, new Vector2(Width + 60f, 900f));
        Label(box, "FRIENDS", new Vector2(0f, 405f), 34, TextAnchor.MiddleCenter, Width);

        tag = Label(box, "", new Vector2(-90f, 345f), 22, TextAnchor.MiddleLeft, Width - 180f);
        Button(box, "COPY", new Vector2(Width / 2f - 70f, 345f), new Vector2(140f, 44f)).onClick.AddListener(() =>
        {
            GUIUtility.systemCopyBuffer = FriendsHub.MyTag ?? "";
            status.text = "Tag copied. Send it to a friend so they can add you.";
        });

        addBox = Input(box, "Friend's tag, e.g. name#1234", new Vector2(-90f, 285f), new Vector2(Width - 180f, 50f), tag.font);
        addBox.onSubmit.AddListener(_ => AddFriend());
        Button(box, "ADD", new Vector2(Width / 2f - 70f, 285f), new Vector2(140f, 50f)).onClick.AddListener(AddFriend);
        status = Label(box, "", new Vector2(0f, 235f), 20, TextAnchor.MiddleCenter, Width);
        status.color = Dim;

        content = List(box, new Vector2(0f, -40f), new Vector2(Width, 500f));
        Button(box, "CLOSE", new Vector2(0f, -400f), new Vector2(300f, 56f)).onClick.AddListener(() => Destroy(gameObject));

        FriendsHub.Changed += Redraw;
        Redraw();
        if (!FriendsHub.Ready) { status.text = "Connecting..."; _ = FriendsHub.Init(); }
    }

    async void AddFriend()
    {
        string t = addBox.text.Trim();
        if (t.Length == 0) { status.text = "Type your friend's tag first."; return; }
        status.text = "Sending request...";
        string error = await FriendsHub.Add(t);
        if (!this) return;
        status.text = error ?? "Request sent to " + t + ".";
        if (error == null) addBox.text = "";
    }

    void Redraw()
    {
        if (!this) return;
        if (card) RefreshChat();
        tag.text = "YOUR TAG   " + (FriendsHub.MyTag ?? "-");
        if (!FriendsHub.Ready && FriendsHub.LastError != null) status.text = FriendsHub.LastError;
        foreach (Transform c in content) Destroy(c.gameObject);
        float y = 0f;

        var incoming = FriendsHub.Incoming;
        if (incoming.Count > 0) Header(ref y, "REQUESTS");
        foreach (var r in incoming)
        {
            string id = r.Member.Id;
            Row(ref y, FriendsHub.NameOf(r.Member), "wants to be friends", Pink,
                ("ACCEPT", () => FriendsHub.Accept(id)), ("DECLINE", () => FriendsHub.Decline(id)), ("BLOCK", () => FriendsHub.Block(id)));
        }

        // People in your room you aren't friends with yet: one click sends a request.
        var strangers = FriendsHub.CurrentRoom != null ? RoomPlayer.Strangers() : new();
        strangersShown = strangers.Count;
        if (strangers.Count > 0) Header(ref y, "IN THIS ROOM");
        foreach (var (who, id) in strangers)
            Row(ref y, who, "playing with you", Online, ("ADD", async () => { var e = await FriendsHub.AddById(id); return e ?? "Request sent to " + who + "."; }));

        var friends = new System.Collections.Generic.List<Unity.Services.Friends.Models.Relationship>(FriendsHub.Friends);
        friends.Sort((a, b) => FriendsHub.IsOnline(b.Member).CompareTo(FriendsHub.IsOnline(a.Member)));
        Header(ref y, $"FRIENDS  {friends.FindAll(f => FriendsHub.IsOnline(f.Member)).Count}/{friends.Count} ONLINE");
        if (friends.Count == 0) Note(ref y, "No friends yet. Add one by tag above.");
        foreach (var r in friends)
        {
            var m = r.Member;
            string id = m.Id;
            var act = FriendsHub.ActivityOf(m);
            string sub = act == null ? "Offline" : act.room != null ? act.status + ": " + act.room : act.status;
            int unread = FriendsHub.Unread(id);
            if (unread > 0) sub = $"<color=#FF40E0>{unread} new message{(unread == 1 ? "" : "s")}</color>   " + sub;
            bool canJoin = act?.room != null && act.room != FriendsHub.CurrentRoom && UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex == 0;
            bool canInvite = act != null && FriendsHub.CurrentRoom != null && act.room != FriendsHub.CurrentRoom;
            var buttons = new System.Collections.Generic.List<(string, Func<Task<string>>)>();
            if (canJoin) { string room = act.room; buttons.Add(("JOIN", () => { Destroy(gameObject); MultiplayerMenu.JoinFriendRoom(room); return Task.FromResult<string>(null); })); }
            if (canInvite) buttons.Add(("INVITE", async () => { var e = await FriendsHub.SendInvite(id); return e ?? "Invite sent."; }));
            var member = m;
            buttons.Add((unread > 0 ? $"VIEW ({unread})" : "VIEW", () => { OpenCard(member); return Task.FromResult<string>(null); }));
            Row(ref y, FriendsHub.NameOf(m), sub, act != null ? Online : Offline, buttons.ToArray());
        }

        var outgoing = FriendsHub.Outgoing;
        if (outgoing.Count > 0) Header(ref y, "SENT");
        foreach (var r in outgoing)
        {
            string id = r.Member.Id;
            Row(ref y, FriendsHub.NameOf(r.Member), "waiting for them to accept", Offline, ("CANCEL", () => FriendsHub.Cancel(id)));
        }

        var blocked = FriendsHub.Blocked;
        if (blocked.Count > 0) Header(ref y, "BLOCKED");
        foreach (var r in blocked)
        {
            string id = r.Member.Id;
            Row(ref y, FriendsHub.NameOf(r.Member), "can't send you requests or invites", Offline, ("UNBLOCK", () => FriendsHub.Unblock(id)));
        }
        content.sizeDelta = new Vector2(0f, -y);
    }

    // ---- list pieces ----

    void Header(ref float y, string text)
    {
        var t = Label(content, text, new Vector2(10f, y - 22f), 20, TextAnchor.MiddleLeft, Width - 20f);
        Top(t.rectTransform);
        y -= 44f;
    }

    void Note(ref float y, string text)
    {
        var t = Label(content, text, new Vector2(10f, y - 22f), 20, TextAnchor.MiddleLeft, Width - 20f);
        t.color = Dim;
        Top(t.rectTransform);
        y -= 44f;
    }

    // name + one line of detail, a status dot, and buttons on the right. REMOVE/DECLINE/CANCEL need a 2nd click.
    void Row(ref float y, string name, string sub, Color dot, params (string label, Func<Task<string>> act)[] buttons)
    {
        var row = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        row.SetParent(content, false);
        Top(row);
        row.sizeDelta = new Vector2(Width - 8f, RowH - 6f);
        row.anchoredPosition = new Vector2(4f, y - RowH / 2f);
        row.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.12f, 1f);

        var d = new GameObject("Dot", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        d.SetParent(row, false);
        d.anchoredPosition = new Vector2(-Width / 2f + 22f, 0f);
        d.sizeDelta = new Vector2(12f, 12f);
        d.GetComponent<Image>().color = dot;

        float textW = Width - 50f - buttons.Length * 120f;
        float left = -Width / 2f + 40f + textW / 2f;
        Label(row, name.ToUpper(), new Vector2(left, 11f), 22, TextAnchor.MiddleLeft, textW).color = Color.white;
        Label(row, sub, new Vector2(left, -14f), 17, TextAnchor.MiddleLeft, textW).color = Dim;

        for (int i = 0; i < buttons.Length; i++)
        {
            var (label, act) = buttons[i];
            var b = Button(row, label, new Vector2(Width / 2f - 70f - (buttons.Length - 1 - i) * 120f, 0f), new Vector2(110f, 42f));
            var text = b.GetComponentInChildren<Text>();
            text.fontSize = 18;
            bool risky = label is "REMOVE" or "DECLINE" or "CANCEL" or "BLOCK";
            b.onClick.AddListener(async () =>
            {
                if (risky && text.text != "SURE?") { text.text = "SURE?"; return; }
                b.interactable = false;
                string result = await act();
                if (!this) return;
                if (result != null) status.text = result;
                if (b) b.interactable = true;
            });
        }
        y -= RowH;
    }

    // ---- a friend's card: stats, chat, REMOVE / BLOCK ----

    async void OpenCard(Unity.Services.Friends.Models.Member m)
    {
        if (card) Destroy(card.gameObject);
        cardId = m.Id;
        cardName = FriendsHub.NameOf(m);
        card = Box(box, new Vector2(Width + 20f, 700f));
        card.anchoredPosition = new Vector2(0f, -40f);
        string id = m.Id, name = FriendsHub.NameOf(m);

        Button(card, "< BACK", new Vector2(-Width / 2f + 70f, 315f), new Vector2(120f, 44f)).onClick.AddListener(CloseCard);
        Label(card, name.ToUpper(), new Vector2(0f, 315f), 30, TextAnchor.MiddleCenter, 400f).color = Color.white;
        var act = FriendsHub.ActivityOf(m);
        Label(card, act == null ? "Offline" : act.room != null ? act.status + ": " + act.room : act.status, new Vector2(0f, 278f), 18, TextAnchor.MiddleCenter, Width).color = Dim;
        var stats = Label(card, "Loading stats...", new Vector2(0f, 205f), 20, TextAnchor.UpperCenter, Width - 40f);
        stats.rectTransform.sizeDelta = new Vector2(Width - 40f, 110f);
        stats.color = Color.white;

        Label(card, "CHAT", new Vector2(-Width / 2f + 130f, 135f), 20, TextAnchor.MiddleLeft, 200f);
        cardChat = Label(card, "", new Vector2(0f, 0f), 19, TextAnchor.LowerLeft, Width - 60f);
        cardChat.rectTransform.sizeDelta = new Vector2(Width - 60f, 240f);
        cardChat.color = Color.white;
        var input = Input(card, "Message (Enter to send)", new Vector2(-75f, -160f), new Vector2(Width - 190f, 48f), tag.font);
        input.characterLimit = FriendsHub.MaxMessage;
        async void Send()
        {
            string text = input.text;
            input.text = "";
            string e = await FriendsHub.SendText(id, text);
            if (cardStatus) cardStatus.text = e ?? "";
            if (input) input.ActivateInputField();
        }
        input.onSubmit.AddListener(_ => Send());
        Button(card, "SEND", new Vector2(Width / 2f - 70f, -160f), new Vector2(120f, 48f)).onClick.AddListener(Send);
        cardStatus = Label(card, "", new Vector2(0f, -205f), 18, TextAnchor.MiddleCenter, Width);
        cardStatus.color = Dim;

        // Two clicks each, like the list.
        Risky(Button(card, "REMOVE FRIEND", new Vector2(-150f, -290f), new Vector2(250f, 48f)), async () => { await FriendsHub.Remove(id); CloseCard(); });
        Risky(Button(card, "BLOCK", new Vector2(150f, -290f), new Vector2(250f, 48f)), async () => { await FriendsHub.Block(id); CloseCard(); });

        FriendsHub.MarkRead(id);
        RefreshChat();
        var p = await ProfileService.LoadPublic(id);
        if (!stats) return;
        stats.text = p == null ? "No stats yet." : StatsText(p);
    }

    static string StatsText(PlayerProfile p)
    {
        string streak = p.streak > 0 ? $"W{p.streak}" : p.streak < 0 ? $"L{-p.streak}" : "-";
        var lines = $"<color=#FFCC40>{PlayerProfile.Ranks[p.RankIndex].name}</color>     {Plural(p.all.games, "game")}     {Plural(p.all.wins, "win")}     " +
                    $"{(p.all.games > 0 ? $"{p.all.Rate * 100f:0}%" : "-")} win rate     streak {streak}\n<color=#8A96B0>";
        for (int i = 0; i < Mathf.Min(3, p.recent.Count); i++)
        {
            var m = p.recent[i];
            lines += (m.won ? "<color=#60FF90>WIN</color>" : "<color=#FF6070>LOSS</color>") + $" vs {m.opponents}   ";
        }
        return lines + "</color>";
    }

    static string Plural(int n, string word) => n + " " + word + (n == 1 ? "" : "s");

    void RefreshChat()
    {
        if (cardId == null || !cardChat) return;
        FriendsHub.MarkRead(cardId);
        var lines = new System.Text.StringBuilder();
        if (FriendsHub.Chats.TryGetValue(cardId, out var chat))
            for (int i = Mathf.Max(0, chat.Count - 9); i < chat.Count; i++)
                lines.AppendLine((chat[i].mine ? "<color=#4CFFFF>You:</color> " : $"<color=#FF40E0>{cardName}:</color> ") + chat[i].text);
        cardChat.text = lines.Length > 0 ? lines.ToString() : "<color=#8A96B0>No messages yet. Messages last until you close the game.</color>";
    }

    void CloseCard()
    {
        if (card) Destroy(card.gameObject);
        card = null;
        cardId = null;
    }

    static void Risky(Button b, Action act)
    {
        var text = b.GetComponentInChildren<Text>();
        b.onClick.AddListener(() => { if (text.text != "SURE?") { text.text = "SURE?"; return; } b.interactable = false; act(); });
    }

    // Results screen: "+ ADD NAME" for each player from the match you aren't friends with yet.
    public static void AddButtons(Transform parent, Vector2 center)
    {
        var list = RoomPlayer.Strangers();
        for (int i = 0; i < list.Count; i++)
        {
            var (who, id) = list[i];
            var b = Button(parent, "+ ADD " + who.ToUpper(), center + new Vector2((i - (list.Count - 1) / 2f) * 250f, 0f), new Vector2(240f, 44f));
            var text = b.GetComponentInChildren<Text>();
            text.fontSize = 18;
            b.onClick.AddListener(async () =>
            {
                b.interactable = false;
                string e = await FriendsHub.AddById(id);
                if (text) text.text = e == null ? "REQUEST SENT" : "COULDN'T ADD";
            });
        }
    }

    static void Top(RectTransform r) { r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f); }

    // ---- widgets ----

    static RectTransform Box(Transform parent, Vector2 size)
    {
        var box = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        box.SetParent(parent, false);
        box.sizeDelta = size;
        box.GetComponent<Image>().color = new Color(SettingsPanel.Box.r, SettingsPanel.Box.g, SettingsPanel.Box.b, 1f); // solid: the menu behind is busy
        return box;
    }

    static InputField Input(Transform parent, string hint, Vector2 pos, Vector2 size, Font font)
    {
        var f = DefaultControls.CreateInputField(new DefaultControls.Resources()).GetComponent<InputField>();
        f.transform.SetParent(parent, false);
        var rt = (RectTransform)f.transform;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        f.GetComponent<Image>().color = new Color(0.06f, 0.1f, 0.18f, 1f);
        f.textComponent.color = Color.white;
        f.textComponent.fontSize = 22;
        var ph = (Text)f.placeholder;
        f.textComponent.font = ph.font = font; // the default control has no font in Unity 6
        ph.text = hint; ph.fontSize = 20; ph.color = Dim;
        return f;
    }

    static RectTransform List(Transform parent, Vector2 pos, Vector2 size)
    {
        var view = DefaultControls.CreateScrollView(new DefaultControls.Resources()).GetComponent<ScrollRect>();
        view.transform.SetParent(parent, false);
        var rt = (RectTransform)view.transform;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        view.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.3f);
        view.horizontal = false;
        view.scrollSensitivity = 30f;
        Destroy(view.horizontalScrollbar.gameObject);
        view.horizontalScrollbar = null;
        foreach (var img in view.verticalScrollbar.GetComponentsInChildren<Image>()) img.color = new Color(0.1f, 0.15f, 0.25f);
        view.verticalScrollbar.handleRect.GetComponent<Image>().color = Cyan * 0.8f;
        return view.content;
    }

    // ---- invite pop-up ----

    static (string from, string room)? waiting; // an invite that came during a match

    [RuntimeInitializeOnLoadMethod]
    static void HookInvites()
    {
        FriendsHub.Invited += ShowInvite;
        FriendsHub.Messaged += (from, text) =>
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex != 0) Alerts.Show(from + ": " + text, Alerts.Info); // mid-match
            else Sfx.Play(Snd.Info); // menus: the FRIENDS button counts it
        };
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged += (old, scene) =>
        {
            if (scene.buildIndex != 0 || waiting is not var (from, room)) return;
            waiting = null;
            ShowInvite(from, room); // back in the menus: offer it now
        };
    }

    static void ShowInvite(string from, string room)
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex != 0)
        {
            waiting = (from, room); // mid-match: say so, offer JOIN once back in the menus
            Alerts.Show(from + " invited you to " + room + " (join from the menu)", Alerts.Info);
            return;
        }
        Debug.Log($"[GD5] invite from {from} to {room}");
        Sfx.Play(Snd.Info);
        var canvas = FindFirstObjectByType<Canvas>()?.rootCanvas;
        if (!canvas) return;
        var box = Box(canvas.transform, new Vector2(520f, 150f));
        box.name = "FriendInvite";
        box.anchorMin = box.anchorMax = Vector2.one;
        box.anchoredPosition = new Vector2(-280f, -95f);
        box.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.12f, 0.98f);
        Label(box, from.ToUpper() + " INVITED YOU", new Vector2(0f, 42f), 24, TextAnchor.MiddleCenter, 480f).color = Color.white;
        Label(box, "to room \"" + room + "\"", new Vector2(0f, 8f), 20, TextAnchor.MiddleCenter, 480f).color = Dim;
        var go = box.gameObject;
        Button(box, "JOIN", new Vector2(-110f, -45f), new Vector2(200f, 46f)).onClick.AddListener(() =>
        {
            Destroy(go);
            var open = FindFirstObjectByType<FriendsPanel>();
            if (open) Destroy(open.gameObject);
            MultiplayerMenu.JoinFriendRoom(room);
        });
        Button(box, "IGNORE", new Vector2(110f, -45f), new Vector2(200f, 46f)).onClick.AddListener(() => Destroy(go));
        box.SetAsLastSibling();
        Destroy(go, 30f);
#if DEVELOPMENT_BUILD
        // Test builds: "-acceptinvites" clicks JOIN by itself.
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-acceptinvites") >= 0)
            box.GetComponentsInChildren<Button>()[0].onClick.Invoke();
#endif
    }
}
