using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Options;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FriendsHub
{
    [Serializable] public class Activity { public string status = "In menus"; public string room, region; public long at; }
    [Serializable] public class Note { public string room, region, text; public long at; }

    public static bool Ready { get; private set; }
    public static string MyTag { get; private set; }
    public static string LastError { get; private set; }
    public static string CurrentRoom => me.room;
    public static event Action Changed;
    public static event Action<string, string, string> Invited;   // from, room, host region
    public static event Action<string, string> Messaged;

    // History = my protected inbox + sent lists, written by the SendMail Cloud Code script, so offline messages arrive on next sync
    public static readonly Dictionary<string, List<(bool mine, string text, long at)>> Chats = new();
    public static event Action<string> MissedMail;   // once after sign-in: "name sent you 2 messages"
    public static int Unread(string id)
    {
        if (!Chats.TryGetValue(id, out var c)) return 0;
        long seen = LastRead(id); int n = 0;
        foreach (var m in c) if (!m.mine && m.at > seen) n++;
        return n;
    }
    public static int UnreadTotal { get { int n = 0; foreach (var id in Chats.Keys) n += Unread(id); return n; } }
    public static void MarkRead(string id)
    {
        if (Unread(id) == 0) return;
        long last = 0;
        foreach (var m in Chats[id]) if (!m.mine) last = Math.Max(last, m.at);
        PlayerPrefs.SetString(ReadKey(id), last.ToString());
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
    static string ReadKey(string id) => "mail.read." + AccountService.PlayerId + "." + id;
    static long LastRead(string id) => long.TryParse(PlayerPrefs.GetString(ReadKey(id), "0"), out long v) ? v : 0;
    static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    static void AddLine(string id, bool mine, string text, long at)
    {
        if (!Chats.TryGetValue(id, out var c)) Chats[id] = c = new();
        foreach (var m in c) if (Math.Abs(m.at - at) < 5000 && m.mine == mine && m.text == text) return;   // live copy vs stored copy
        c.Add((mine, text, at));
        c.Sort((a, b) => a.at.CompareTo(b.at));
    }

    static bool syncing, synced;
    public static async Task SyncMail()
    {
        if (!Ready || syncing) return;
        syncing = true;
        int before = UnreadTotal;
        try
        {
            if (!synced) _ = ProfileService.DropPublicMail();
            var friends = new HashSet<string>();
            foreach (var r in Friends) friends.Add(r.Member.Id);
            var (inbox, sent) = await ProfileService.LoadMail();
            foreach (var m in sent.items) if (friends.Contains(m.to)) AddLine(m.to, true, m.text, m.at);
            foreach (var m in inbox.items) if (friends.Contains(m.from)) AddLine(m.from, false, m.text, m.at);
            if (!synced)
            {
                synced = true;
                var who = new List<string>();
                foreach (var r in Friends) if (Unread(r.Member.Id) > 0) who.Add(NameOf(r.Member));
                int n = UnreadTotal;
                if (n > 0) MissedMail?.Invoke((who.Count == 1 ? who[0] : who.Count + " friends") + " sent you " + n + (n == 1 ? " message" : " messages"));
                before = n;
            }
        }
        catch (Exception e) { Debug.LogWarning(e); }
        finally { syncing = false; }
        if (UnreadTotal > before) Sfx.Play(Snd.Info);
        Changed?.Invoke();
    }

    static readonly Activity me = new();
    static bool hooked;
    static IFriendsService Service => FriendsService.Instance;

    public static IReadOnlyList<Relationship> Friends => Ready ? Service.Friends : Array.Empty<Relationship>();
    public static IReadOnlyList<Relationship> Incoming => Ready ? Service.IncomingFriendRequests : Array.Empty<Relationship>();
    public static IReadOnlyList<Relationship> Outgoing => Ready ? Service.OutgoingFriendRequests : Array.Empty<Relationship>();
    public static IReadOnlyList<Relationship> Blocked => Ready ? Service.Blocks : Array.Empty<Relationship>();
    static int lastIncoming;

    public static async Task Init()
    {
        if (Ready || !AccountService.SignedIn) return;
        try
        {
            var auth = AuthenticationService.Instance;
            MyTag = await auth.GetPlayerNameAsync();
            string user = AccountService.Username;
            if (user != null && (MyTag == null || !MyTag.StartsWith(user + "#", StringComparison.OrdinalIgnoreCase)))
                MyTag = await auth.UpdatePlayerNameAsync(user);

            var options = new InitializeOptions().WithEvents(true).WithMemberPresence(true).WithMemberProfile(true);
            for (int attempt = 1; ; attempt++)
            {
                try { await Service.InitializeAsync(options); break; }
                catch (Exception e) when (attempt < 3 && AccountService.SignedIn)
                {
                    Debug.Log("Friends init retry: " + e.Message);
                    Service.GetType().GetField("m_CleanupTask", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.SetValue(Service, Task.CompletedTask);
                    await Task.Delay(1000);
                }
            }
            if (!hooked)
            {
                hooked = true;
                Service.RelationshipAdded += _ => OnChanged();
                Service.RelationshipDeleted += _ => OnChanged();
                Service.PresenceUpdated += _ => OnChanged();
                Service.MessageReceived += OnNote;
                Heartbeat();
            }
            Ready = true;
            lastIncoming = Incoming.Count;
            _ = ProfileService.Load();
            LastError = null;
            await Push();
            _ = SyncMail();
        }
        catch (Exception e) { LastError = "Friends unavailable: " + e.Message; Debug.LogWarning(e); }
        Changed?.Invoke();
    }

    static void OnNote(Unity.Services.Friends.Notifications.IMessageReceivedEvent e)
    {
        var note = e.GetAs<Note>();
        if (string.IsNullOrEmpty(note.text)) { Invited?.Invoke(NameOf(e.UserId), note.room, note.region); return; }
        AddLine(e.UserId, false, note.text, note.at > 0 ? note.at : Now);
        Debug.Log("[GD5] message from " + NameOf(e.UserId) + ": " + note.text);
#if DEVELOPMENT_BUILD
        if (DevArg("-echomessages")) _ = SendText(e.UserId, "echo: " + note.text);
#endif
        Messaged?.Invoke(NameOf(e.UserId), note.text);
        Changed?.Invoke();
    }

    static void OnChanged()
    {
        if (Incoming.Count > lastIncoming) Sfx.Play(Snd.Info);
#if DEVELOPMENT_BUILD
        if (DevArg("-acceptfriends")) foreach (var r in Incoming) _ = Accept(r.Member.Id);
#endif
        lastIncoming = Incoming.Count;
        Changed?.Invoke();
    }

    const long Stale = 200;
    static async void Heartbeat()
    {
        while (Application.isPlaying)
        {
            await Task.Delay(60000);
            if (Ready) { await Push(); await SyncMail(); }
            Changed?.Invoke();
        }
    }

    public static void SignedOut()
    {
        if (!Ready) return;
        _ = Service.SetPresenceAvailabilityAsync(Availability.Offline);
        Ready = false;
        MyTag = null;
        me.room = null;
        Chats.Clear();
        synced = false;
        Changed?.Invoke();
    }


    [RuntimeInitializeOnLoadMethod]
    static void Hook()
    {
        SceneManager.activeSceneChanged += (old, scene) =>
        {
            me.status = scene.buildIndex == 0 ? "In menus" : RoomService.Instance && RoomService.Instance.Runner ? "In an online match" : "Playing skirmish";
            if (scene.buildIndex != 0) me.room = null;
            _ = Push();
        };
        Application.quitting += () => { if (Ready) _ = Service.SetPresenceAvailabilityAsync(Availability.Offline); };
    }

    public static void SetRoom(string room)
    {
        me.room = room;
        me.region = room != null && RoomService.Instance ? RoomService.Instance.Region : null;
        me.status = room != null ? "In a lobby" : "In menus";
        _ = Push();
        Changed?.Invoke();
    }

    static async Task Push()
    {
        if (!Ready) return;
        me.at = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try { await Service.SetPresenceAsync(Availability.Online, me); }
        catch (Exception e) { Debug.LogWarning(e); }
    }


    public static Task<string> Add(string tag) => Run(() => Service.AddFriendByNameAsync(tag.Trim()));
    public static Task<string> AddById(string id) => Run(() => Service.AddFriendAsync(id));
    public static Task<string> Block(string id) => Run(() => Service.AddBlockAsync(id));
    public static Task<string> Unblock(string id) => Run(() => Service.DeleteBlockAsync(id));
    public static Task<string> Accept(string id) => Run(() => Service.AddFriendAsync(id));
    public static Task<string> Decline(string id) => Run(() => Service.DeleteIncomingFriendRequestAsync(id));
    public static Task<string> Cancel(string id) => Run(() => Service.DeleteOutgoingFriendRequestAsync(id));
    public static Task<string> Remove(string id) => Run(() => Service.DeleteFriendAsync(id));
    public static Task<string> SendInvite(string id) =>
        me.room == null ? Task.FromResult("You're not in a room.") : Run(() => Service.MessageAsync(id, new Note { room = me.room, region = me.region }));

    public const int MaxMessage = 200;
    public static async Task<string> SendText(string id, string text)
    {
        text = text.Trim();
        if (text.Length == 0) return null;
        if (text.Length > MaxMessage) text = text[..MaxMessage];
        if (!Ready) return LastError ?? "Friends aren't connected.";
        long at;
        try { at = await ProfileService.SendMail(id, text); }
        catch (Exception e) { Debug.LogWarning(e); return "Couldn't send, try again."; }
        AddLine(id, true, text, at);
        Changed?.Invoke();
        // live push is just a fast path; an offline friend gets it from their inbox on their next sync
        try { await Service.MessageAsync(id, new Note { text = text, at = at }); }
        catch (Exception e) { Debug.Log("live message not delivered (friend offline?): " + e.Message); }
        return null;
    }

    static async Task<string> Run(Func<Task> call)
    {
        if (!Ready) return LastError ?? "Friends aren't connected.";
        try { await call(); }
        catch (Exception e) { Debug.LogWarning(e); return Friendly(e.Message); }
        Changed?.Invoke();
        return null;
    }

    static string Friendly(string msg) =>
        msg.Contains("does not refer", StringComparison.OrdinalIgnoreCase) || msg.Contains("not found", StringComparison.OrdinalIgnoreCase)
            ? "No player with that tag. Tags look like name#1234." :
        msg.Contains("self", StringComparison.OrdinalIgnoreCase) ? "That's your own tag." :
        msg.Contains("block", StringComparison.OrdinalIgnoreCase) ? "Can't add that player (blocked)." :
        msg.Contains("exist", StringComparison.OrdinalIgnoreCase) ? "Already friends or already requested." : msg;

#if DEVELOPMENT_BUILD
    public static bool DevArg(string flag) => Array.IndexOf(Environment.GetCommandLineArgs(), flag) >= 0;
#endif


    public static string NameOf(Member m) => Short(m.Profile?.Name ?? m.Id);
    static string NameOf(string id)
    {
        foreach (var r in Friends) if (r.Member.Id == id) return NameOf(r.Member);
        return "A friend";
    }
    static string Short(string tag) { int h = tag.IndexOf('#'); return h > 0 ? tag[..h] : tag; }

    public static bool IsOnline(Member m) => m.Presence != null && m.Presence.Availability is Availability.Online or Availability.Busy or Availability.Away
        && Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (m.Presence.GetActivity<Activity>()?.at ?? 0)) < Stale;

    public static bool IsKnown(string id)
    {
        if (!Ready || id == AccountService.PlayerId) return true;
        foreach (var r in Service.Relationships) if (r.Member.Id == id) return true;
        return false;
    }
    public static Activity ActivityOf(Member m) => IsOnline(m) ? m.Presence.GetActivity<Activity>() ?? new Activity() : null;
}
