using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Friends;
using Unity.Services.Friends.Models;
using Unity.Services.Friends.Options;
using UnityEngine;
using UnityEngine.SceneManagement;

// The only class that talks to Unity Friends. Friends are added by tag ("name#1234", Unity's player
// name). Your presence says what you're doing and which room you're in, so friends can JOIN it;
// invites are Friends messages that pop up an InviteToast.
public static class FriendsHub
{
    [Serializable] public class Activity { public string status = "In menus"; public string room; public long at; } // at: unix seconds, refreshed each minute
    [Serializable] public class Invite { public string room; }

    public static bool Ready { get; private set; }
    public static string MyTag { get; private set; }
    public static string LastError { get; private set; }
    public static string CurrentRoom => me.room;
    public static event Action Changed;
    public static event Action<string, string> Invited; // friend name, room

    static readonly Activity me = new();
    static bool hooked;
    static IFriendsService Service => FriendsService.Instance;

    public static IReadOnlyList<Relationship> Friends => Ready ? Service.Friends : Array.Empty<Relationship>();
    public static IReadOnlyList<Relationship> Incoming => Ready ? Service.IncomingFriendRequests : Array.Empty<Relationship>();
    public static IReadOnlyList<Relationship> Outgoing => Ready ? Service.OutgoingFriendRequests : Array.Empty<Relationship>();
    public static IReadOnlyList<Relationship> Blocked => Ready ? Service.Blocks : Array.Empty<Relationship>();
    static int lastIncoming; // a new request plays a sound

    // After sign-in. Makes the Unity player name match the username so the tag is recognisable.
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
                    // ponytail: Friends 1.4 bug: after a sign-out its cleanup times out on the closed socket, and that
                    // failed task blocks every later InitializeAsync (or the socket is still reconnecting). Clear it
                    // and retry; drop this when the SDK fixes it.
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
                Service.MessageReceived += e => Invited?.Invoke(NameOf(e.UserId), e.GetAs<Invite>().room);
                Heartbeat();
            }
            Ready = true;
            lastIncoming = Incoming.Count;
            LastError = null;
            await Push();
        }
        catch (Exception e) { LastError = "Friends unavailable: " + e.Message; Debug.LogWarning(e); }
        Changed?.Invoke();
    }

    static void OnChanged()
    {
        if (Incoming.Count > lastIncoming) Sfx.Play(Snd.Info);
        lastIncoming = Incoming.Count;
        Changed?.Invoke();
    }

    // Presence stays "online" after a crash (its LastSeen is only filled in on going offline), so each game
    // stamps its activity every minute and anyone not stamped within Stale counts as offline. Stale is
    // generous so a few minutes of clock difference between PCs don't matter.
    const long Stale = 200;
    static async void Heartbeat()
    {
        while (Application.isPlaying)
        {
            await Task.Delay(60000);
            if (Ready) await Push();
            Changed?.Invoke(); // friends who went quiet turn offline
        }
    }

    // Signing out: the next player starts fresh (events are re-hooked by the next Init).
    public static void SignedOut()
    {
        if (!Ready) return;
        _ = Service.SetPresenceAvailabilityAsync(Availability.Offline);
        Ready = false;
        MyTag = null;
        me.room = null;
        Changed?.Invoke();
    }

    // ---- what I'm doing (shown to friends) ----

    [RuntimeInitializeOnLoadMethod]
    static void Hook()
    {
        SceneManager.activeSceneChanged += (old, scene) =>
        {
            me.status = scene.buildIndex == 0 ? "In menus" : RoomService.Instance && RoomService.Instance.Runner ? "In an online match" : "Playing skirmish";
            if (scene.buildIndex != 0) me.room = null; // the room closes when the match starts
            _ = Push();
        };
        Application.quitting += () => { if (Ready) _ = Service.SetPresenceAvailabilityAsync(Availability.Offline); };
    }

    public static void SetRoom(string room)
    {
        me.room = room;
        me.status = room != null ? "In a lobby" : "In menus";
        _ = Push();
        Changed?.Invoke(); // INVITE buttons appear/disappear
    }

    static async Task Push()
    {
        if (!Ready) return;
        me.at = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try { await Service.SetPresenceAsync(Availability.Online, me); }
        catch (Exception e) { Debug.LogWarning(e); }
    }

    // ---- actions; each returns an error message or null ----

    public static Task<string> Add(string tag) => Run(() => Service.AddFriendByNameAsync(tag.Trim()));
    public static Task<string> AddById(string id) => Run(() => Service.AddFriendAsync(id)); // someone from your room / match
    public static Task<string> Block(string id) => Run(() => Service.AddBlockAsync(id));
    public static Task<string> Unblock(string id) => Run(() => Service.DeleteBlockAsync(id));
    public static Task<string> Accept(string id) => Run(() => Service.AddFriendAsync(id));
    public static Task<string> Decline(string id) => Run(() => Service.DeleteIncomingFriendRequestAsync(id));
    public static Task<string> Cancel(string id) => Run(() => Service.DeleteOutgoingFriendRequestAsync(id));
    public static Task<string> Remove(string id) => Run(() => Service.DeleteFriendAsync(id));
    public static Task<string> SendInvite(string id) =>
        me.room == null ? Task.FromResult("You're not in a room.") : Run(() => Service.MessageAsync(id, new Invite { room = me.room }));

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

    // ---- reading a friend ----

    public static string NameOf(Member m) => Short(m.Profile?.Name ?? m.Id);
    static string NameOf(string id)
    {
        foreach (var r in Friends) if (r.Member.Id == id) return NameOf(r.Member);
        return "A friend";
    }
    static string Short(string tag) { int h = tag.IndexOf('#'); return h > 0 ? tag[..h] : tag; }

    public static bool IsOnline(Member m) => m.Presence != null && m.Presence.Availability is Availability.Online or Availability.Busy or Availability.Away
        && Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (m.Presence.GetActivity<Activity>()?.at ?? 0)) < Stale;

    // Already a friend, asked, asking or blocked: no ADD button for them.
    public static bool IsKnown(string id)
    {
        if (!Ready || id == AccountService.PlayerId) return true;
        foreach (var r in Service.Relationships) if (r.Member.Id == id) return true;
        return false;
    }
    public static Activity ActivityOf(Member m) => IsOnline(m) ? m.Presence.GetActivity<Activity>() ?? new Activity() : null;
}
