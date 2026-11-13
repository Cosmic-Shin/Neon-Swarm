using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static SettingsPanel;

public class FriendsPanel : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    static readonly Color Online = new(0.35f, 1f, 0.55f), Offline = new(0.4f, 0.42f, 0.5f), Dim = HoloUI.Muted, Pink = new(1f, 0.1f, 0.9f);
    const float RowH = 64f, Width = 700f;

    RectTransform content;
    int strangersShown = -1;
    Text myTag, status;
    InputField addBox;
    RectTransform box, card;
    string cardId, cardName;
    GameObject[] underCard;
    Text cardChat, cardStatus;

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
        HoloSkin.Layer(root, HoloSkin.LayerPopup);
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
        box = Box(root, new Vector2(Width + 60f, 900f), "Panel_Modal");
        HoloSkin.Brackets(box, HoloUI.Cyan);
        HoloSkin.FX(box, HoloUI.Cyan);
        HoloSkin.Title(Label(box, "FRIENDS", new Vector2(0f, 405f), 34, TextAnchor.MiddleCenter, Width), Width - 40f);

        myTag = Label(box, "", new Vector2(-90f, 345f), 22, TextAnchor.MiddleLeft, Width - 180f);
        Small(Button(box, "COPY", new Vector2(Width / 2f - 70f, 345f), new Vector2(140f, 44f))).onClick.AddListener(() =>
        {
            GUIUtility.systemCopyBuffer = FriendsHub.MyTag ?? "";
            status.text = "Tag copied. Send it to a friend so they can add you.";
        });

        addBox = Input(box, "Friend's tag, e.g. name#1234", new Vector2(-90f, 285f), new Vector2(Width - 180f, 50f), myTag.font);
        addBox.onSubmit.AddListener(_ => AddFriend());
        var add = Small(Button(box, "ADD", new Vector2(Width / 2f - 70f, 285f), new Vector2(140f, 50f)));
        add.onClick.AddListener(AddFriend);
        status = Label(box, "", new Vector2(0f, 235f), 20, TextAnchor.MiddleCenter, Width);
        status.color = Dim;

        content = List(box, new Vector2(0f, -40f), new Vector2(Width, 500f));
        underCard = new[] { addBox.gameObject, add.gameObject, status.gameObject, content.GetComponentInParent<ScrollRect>().gameObject };
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
        myTag.text = "YOUR TAG   " + (FriendsHub.MyTag ?? "-");
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

    void Row(ref float y, string name, string sub, Color dot, params (string label, Func<Task<string>> act)[] buttons)
    {
        var row = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        row.SetParent(content, false);
        Top(row);
        row.sizeDelta = new Vector2(Width - 8f, RowH - 6f);
        row.anchoredPosition = new Vector2(4f, y - RowH / 2f);
        HoloSkin.Back(row, "Panel_Inset");

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
            var b = Small(Button(row, label, new Vector2(Width / 2f - 70f - (buttons.Length - 1 - i) * 120f, 0f), new Vector2(110f, 42f)));
            if (label is "DECLINE" or "BLOCK" or "CANCEL") HoloSkin.Button(b, HoloUI.ButtonStyle.Danger);
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


    async void OpenCard(Unity.Services.Friends.Models.Member m)
    {
        if (card) Destroy(card.gameObject);
        cardId = m.Id;
        cardName = FriendsHub.NameOf(m);
        card = Box(box, new Vector2(Width + 20f, 700f), "Panel_Frame");
        foreach (var go in underCard) go.SetActive(false);
        card.anchoredPosition = new Vector2(0f, -40f);
        string id = m.Id, name = FriendsHub.NameOf(m);

        Small(Button(card, "< BACK", new Vector2(-Width / 2f + 70f, 315f), new Vector2(120f, 44f))).onClick.AddListener(CloseCard);
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
        var chatBox = new GameObject("ChatBox", typeof(RectTransform)).GetComponent<RectTransform>();
        chatBox.SetParent(card, false);
        chatBox.anchoredPosition = cardChat.rectTransform.anchoredPosition;
        chatBox.sizeDelta = cardChat.rectTransform.sizeDelta + new Vector2(24f, 16f);
        chatBox.SetSiblingIndex(cardChat.transform.GetSiblingIndex());
        HoloSkin.Back(chatBox, "Panel_Inset");
        var input = Input(card, "Message (Enter to send)", new Vector2(-75f, -160f), new Vector2(Width - 190f, 48f), myTag.font);
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

        Risky(Danger(Button(card, "REMOVE FRIEND", new Vector2(-150f, -290f), new Vector2(250f, 48f))), async () => { await FriendsHub.Remove(id); CloseCard(); });
        Risky(Danger(Button(card, "BLOCK", new Vector2(150f, -290f), new Vector2(250f, 48f))), async () => { await FriendsHub.Block(id); CloseCard(); });

        FriendsHub.MarkRead(id);
        RefreshChat();
        var p = await ProfileService.LoadPublic(id);
        if (!stats) return;
        stats.text = p == null ? "No stats yet." : StatsText(p);
        AvatarList.Make(card, new Vector2(Width / 2f - 60f, 300f), 76f, HoloUI.Cyan).Show(p?.avatar, name);
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
        foreach (var go in underCard) if (go) go.SetActive(true);
        card = null;
        cardId = null;
    }

    static void Risky(Button b, Action act)
    {
        var text = b.GetComponentInChildren<Text>();
        b.onClick.AddListener(() => { if (text.text != "SURE?") { text.text = "SURE?"; return; } b.interactable = false; act(); });
    }

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


    static RectTransform Box(Transform parent, Vector2 size, string sprite)
    {
        var box = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        box.SetParent(parent, false);
        box.sizeDelta = size;
        HoloSkin.Back(box, sprite);
        return box;
    }

    static Button Small(Button b) { HoloSkin.Button(b, HoloUI.ButtonStyle.Small); return b; }
    static Button Danger(Button b) { HoloSkin.Button(b, HoloUI.ButtonStyle.Danger); return b; }

    static InputField Input(Transform parent, string hint, Vector2 pos, Vector2 size, Font font)
    {
        var f = DefaultControls.CreateInputField(new DefaultControls.Resources()).GetComponent<InputField>();
        f.transform.SetParent(parent, false);
        var rt = (RectTransform)f.transform;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        f.textComponent.fontSize = 22;
        var ph = (Text)f.placeholder;
        f.textComponent.font = ph.font = font;
        ph.text = hint; ph.fontSize = 20;
        HoloSkin.Input(f);
        return f;
    }

    static RectTransform List(Transform parent, Vector2 pos, Vector2 size)
    {
        var view = DefaultControls.CreateScrollView(new DefaultControls.Resources()).GetComponent<ScrollRect>();
        view.transform.SetParent(parent, false);
        var rt = (RectTransform)view.transform;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        HoloSkin.Back(rt, "Panel_Inset");
        view.horizontal = false;
        view.scrollSensitivity = 30f;
        Destroy(view.horizontalScrollbar.gameObject);
        view.horizontalScrollbar = null;
        HoloUI.SetSliced(view.verticalScrollbar.GetComponent<Image>(), "Slider_Track");
        HoloUI.SetSliced(view.verticalScrollbar.handleRect.GetComponent<Image>(), "Bar_Fill_White", HoloUI.Cyan);
        return view.content;
    }


    static (string from, string room)? waiting;

    [RuntimeInitializeOnLoadMethod]
    static void HookInvites()
    {
        FriendsHub.Invited += ShowInvite;
        FriendsHub.Messaged += (from, text) =>
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex != 0) Alerts.Show(from + ": " + text, Alerts.Info);
            else Sfx.Play(Snd.Info);
        };
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged += (old, scene) =>
        {
            if (scene.buildIndex != 0 || waiting is not var (from, room)) return;
            waiting = null;
            ShowInvite(from, room);
        };
    }

    static void ShowInvite(string from, string room)
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex != 0)
        {
            waiting = (from, room);
            Alerts.Show(from + " invited you to " + room + " (join from the menu)", Alerts.Info);
            return;
        }
        Debug.Log($"[GD5] invite from {from} to {room}");
        Sfx.Play(Snd.Info);
        var canvas = FindFirstObjectByType<Canvas>()?.rootCanvas;
        if (!canvas) return;
        var box = Box(canvas.transform, new Vector2(520f, 150f), "Tooltip");
        box.name = "FriendInvite";
        box.anchorMin = box.anchorMax = Vector2.one;
        box.anchoredPosition = new Vector2(-280f, -95f);
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
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-acceptinvites") >= 0)
            box.GetComponentsInChildren<Button>()[0].onClick.Invoke();
#endif
    }
}
