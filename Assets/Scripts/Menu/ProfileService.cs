using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

public static class ProfileService
{
    const string Key = "profile";

    public static PlayerProfile Current { get; private set; }
    public static string LastError { get; private set; }

    static string LocalKey => "profile." + AccountService.PlayerId;

    public static async Task<PlayerProfile> Load()
    {
        if (!AccountService.SignedIn) return Current = new PlayerProfile();
        Current ??= FromJson(PlayerPrefs.GetString(LocalKey, ""));
        try
        {
            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { Key });
            var cloud = data.TryGetValue(Key, out var item) ? FromJson(item.Value.GetAsString()) : null;
            if (cloud != null && cloud.all.games >= Current.all.games) { cloud.secondsInGame = Mathf.Max(cloud.secondsInGame, Current.secondsInGame); Current = cloud; await PushPublic(); }
            else if (Current.all.games > 0) await Push();
            if (Current.all.wins > 0 && !submitted) { submitted = true; LeaderboardPanel.Submit(Current.all.wins); }
            KeepLocal();
            LastError = null;
        }
        catch (System.Exception e)
        {
            LastError = "Cloud Save unavailable, showing this PC's copy.";
            Debug.LogWarning(e);
        }
        return Current;
    }

    public static async void Record(PlayerProfile.MatchRecord match, int[] stats)
    {
        if (!AccountService.SignedIn) return;
        try
        {
            if (Current == null) await Load();
            var before = Achievements.Snapshot(Current);
            Current.Add(match, (int[])stats.Clone());
            foreach (var (def, tier) in before) { int now = Achievements.Tier(def, Current); for (int t = tier + 1; t <= now; t++) AchievementToast.Unlocked(def, t); }
            KeepLocal();
            await Push();
            LeaderboardPanel.Submit(Current.all.wins);
            LastError = null;
        }
        catch (System.Exception e)
        {
            LastError = "Couldn't reach Cloud Save; the result is kept on this PC.";
            Debug.LogWarning(e);
        }
    }

    public static async void SetBadges(string[] badges)
    {
        Current ??= new PlayerProfile();
        Current.badges = badges;
        if (!AccountService.SignedIn) return;
        KeepLocal();
        try { publicGames = -1; await Push(); LastError = null; }
        catch (System.Exception e) { LastError = "Couldn't reach Cloud Save; your badges are kept on this PC."; Debug.LogWarning(e); }
    }

    public static async void SetAvatar(string avatar)
    {
        Current ??= new PlayerProfile();
        Current.avatar = avatar;
        if (!AccountService.SignedIn) return;
        KeepLocal();
        try
        {
            publicGames = -1;
            await Push();
            LastError = null;
        }
        catch (System.Exception e)
        {
            LastError = "Couldn't reach Cloud Save; your avatar is kept on this PC.";
            Debug.LogWarning(e);
        }
    }

    static async Task Push()
    {
        await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { Key, JsonUtility.ToJson(Current) } });
        await PushPublic();
    }

    static async Task PushPublic()
    {
        if (publicGames == Current.all.games) return;
        await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { Key, JsonUtility.ToJson(Current) } },
            new Unity.Services.CloudSave.Models.Data.Player.SaveOptions(new Unity.Services.CloudSave.Models.Data.Player.PublicWriteAccessClassOptions()));
        publicGames = Current.all.games;
    }
    static int publicGames = -1;

    public static async Task<PlayerProfile> LoadPublic(string playerId)
    {
        try
        {
            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { Key },
                new Unity.Services.CloudSave.Models.Data.Player.LoadOptions(new Unity.Services.CloudSave.Models.Data.Player.PublicReadAccessClassOptions(playerId)));
            return data.TryGetValue(Key, out var item) ? FromJson(item.Value.GetAsString()) : null;
        }
        catch (System.Exception e) { Debug.LogWarning(e); return null; }
    }

    // DMs go through Cloud Code (Assets/CloudCode/SendMail.js): the server writes the recipient's PROTECTED "inbox"
    // and the sender's PROTECTED "sent" key. Protected = only the server writes, only the owner reads.
    [System.Serializable] public class Mail { public string from, to, text; public long at; }
    [System.Serializable] public class MailBox { public List<Mail> items = new(); }
    [System.Serializable] class SendResult { public long at; }

    public static async Task<long> SendMail(string to, string text) =>
        (await Unity.Services.CloudCode.CloudCodeService.Instance.CallEndpointAsync<SendResult>("SendMail",
            new Dictionary<string, object> { { "to", to }, { "text", text } })).at;

    public static async Task<(MailBox inbox, MailBox sent)> LoadMail()
    {
        var data = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { "inbox", "sent" },
            new Unity.Services.CloudSave.Models.Data.Player.LoadOptions(new Unity.Services.CloudSave.Models.Data.Player.ProtectedReadAccessClassOptions()));
        MailBox Get(string key) => data.TryGetValue(key, out var item) ? item.Value.GetAs<MailBox>() ?? new MailBox() : new MailBox();
        return (Get("inbox"), Get("sent"));
    }

    // the first version kept a PUBLIC outbox under "mail" (readable by any player); wipe it
    public static async Task DropPublicMail()
    {
        try { await CloudSaveService.Instance.Data.Player.DeleteAsync("mail", new Unity.Services.CloudSave.Models.Data.Player.DeleteOptions(new Unity.Services.CloudSave.Models.Data.Player.PublicWriteAccessClassOptions())); }
        catch (System.Exception e) { if (!e.Message.Contains("not found", System.StringComparison.OrdinalIgnoreCase)) Debug.LogWarning(e); }
    }

    static bool submitted;
    public static void Forget() { Current = null; publicGames = -1; submitted = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void StartTimer() => Object.DontDestroyOnLoad(new GameObject("PlayTimer", typeof(PlayTimer)) { hideFlags = HideFlags.HideInHierarchy });

    class PlayTimer : MonoBehaviour
    {
        float saveAt = 60f, pushAt = 300f;

        void Update()
        {
            if (Current == null || !AccountService.SignedIn) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f);
            Current.secondsInGame += dt;
            if ((saveAt -= dt) <= 0f) { saveAt = 60f; KeepLocal(); }
            if ((pushAt -= dt) <= 0f) { pushAt = 300f; Save(); }
        }

        void OnApplicationQuit() { if (Current != null && AccountService.SignedIn) KeepLocal(); }

        static async void Save()
        {
            try { await Push(); } catch (System.Exception e) { Debug.LogWarning(e); }
        }
    }

    static void KeepLocal()
    {
        PlayerPrefs.SetString(LocalKey, JsonUtility.ToJson(Current));
        PlayerPrefs.Save();
    }

    static PlayerProfile FromJson(string json) =>
        string.IsNullOrEmpty(json) ? new PlayerProfile() : JsonUtility.FromJson<PlayerProfile>(json) ?? new PlayerProfile();
}
