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
            if (cloud != null && cloud.all.games >= Current.all.games) { Current = cloud; await PushPublic(); }
            else if (Current.all.games > 0) await Push();
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
            Current.Add(match, (int[])stats.Clone());
            KeepLocal();
            await Push();
            LastError = null;
        }
        catch (System.Exception e)
        {
            LastError = "Couldn't reach Cloud Save; the result is kept on this PC.";
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

    public static void Forget() { Current = null; publicGames = -1; }

    static void KeepLocal()
    {
        PlayerPrefs.SetString(LocalKey, JsonUtility.ToJson(Current));
        PlayerPrefs.Save();
    }

    static PlayerProfile FromJson(string json) =>
        string.IsNullOrEmpty(json) ? new PlayerProfile() : JsonUtility.FromJson<PlayerProfile>(json) ?? new PlayerProfile();
}
