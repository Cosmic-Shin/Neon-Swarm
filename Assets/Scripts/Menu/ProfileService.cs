using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using UnityEngine;

// The only class that talks to Unity Cloud Save. Keeps the signed-in player's PlayerProfile as one JSON
// value under the key "profile" in their cloud data, plus a copy on this PC (PlayerPrefs, per player ID)
// for when the cloud can't be reached; the cloud wins when it answers. A public copy of the same JSON lets
// friends view your stats (LoadPublic).
public static class ProfileService
{
    const string Key = "profile";

    public static PlayerProfile Current { get; private set; }
    public static string LastError { get; private set; } // why the cloud failed last time (null = fine)

    static string LocalKey => "profile." + AccountService.PlayerId;

    public static async Task<PlayerProfile> Load()
    {
        if (!AccountService.SignedIn) return Current = new PlayerProfile();
        Current ??= FromJson(PlayerPrefs.GetString(LocalKey, ""));
        try
        {
            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { Key });
            // The copy with more games wins: a result saved here while the cloud was down isn't lost.
            var cloud = data.TryGetValue(Key, out var item) ? FromJson(item.Value.GetAsString()) : null;
            if (cloud != null && cloud.all.games >= Current.all.games) { Current = cloud; await PushPublic(); }
            else if (Current.all.games > 0) await Push(); // this PC is ahead: catch the cloud up
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

    // A match ended (MatchManager). Loads first so a fresh session never overwrites the cloud copy.
    public static async void Record(PlayerProfile.MatchRecord match, int[] stats)
    {
        if (!AccountService.SignedIn) return; // played straight from the editor without signing in
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

    // Readable by any signed-in player (stats only, nothing private). Copied when it's missing or behind.
    static async Task PushPublic()
    {
        if (publicGames == Current.all.games) return;
        await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { Key, JsonUtility.ToJson(Current) } },
            new Unity.Services.CloudSave.Models.Data.Player.SaveOptions(new Unity.Services.CloudSave.Models.Data.Player.PublicWriteAccessClassOptions()));
        publicGames = Current.all.games;
    }
    static int publicGames = -1;

    // Another player's stats (a friend's card). Null if they have none yet or the cloud can't be reached.
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

    // Signing out: the next player starts from their own data.
    public static void Forget() { Current = null; publicGames = -1; }

    static void KeepLocal()
    {
        PlayerPrefs.SetString(LocalKey, JsonUtility.ToJson(Current));
        PlayerPrefs.Save();
    }

    static PlayerProfile FromJson(string json) =>
        string.IsNullOrEmpty(json) ? new PlayerProfile() : JsonUtility.FromJson<PlayerProfile>(json) ?? new PlayerProfile();
}
