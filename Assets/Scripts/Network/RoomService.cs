using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

public class RoomService : MonoBehaviour
{
    public const int MaxPlayers = 8;
    public const int MaxSpectators = 4;
    public static RoomService Instance { get; private set; }
    public static event Action RoomsChanged;
    public static event Action<string> Disconnected;

    public NetworkRunner runnerPrefab;
    public NetworkObject roomPlayerPrefab;
    public NetworkObject roomStatePrefab;
    public NetworkRunner Runner { get; private set; }
    public List<SessionInfo> Rooms { get; private set; } = new();
    bool inRoom;

    void Awake()
    {
        if (Instance) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

#if DEVELOPMENT_BUILD
    async void Start()
    {
        var args = System.Environment.GetCommandLineArgs();
        if (FriendsHub.DevArg("-quickmatch"))
        {
            while (this && !AccountService.SignedIn) await Task.Delay(500);
            await Task.Delay(2000);
            string err = await QuickMatch();
            Debug.Log("[GD5] quickmatch: " + (err ?? "in " + Runner.SessionInfo.Name));
            if (err == null) AutoReady(args);
            return;
        }
        int i = System.Array.IndexOf(args, "-autojoin");
        if (i < 0 || i + 1 >= args.Length) return;
        while (this)
        {
            string error = await Join(args[i + 1]);
            if (error == null) { Debug.Log("[GD5] autojoined " + args[i + 1]); AutoReady(args); return; }
            Debug.Log("[GD5] autojoin: " + error);
            await Task.Delay(3000);
        }
    }

    async void AutoReady(string[] args)
    {
        while (this && (RoomState.Instance == null || RoomState.Instance.SlotOf(Runner.LocalPlayer) < 0)) await Task.Delay(500);
        await Task.Delay(8000);
        if (!this || RoomState.Instance == null) return;
        int t = System.Array.IndexOf(args, "-team");
        if (t >= 0 && t + 1 < args.Length && int.TryParse(args[t + 1], out int team))
            for (int n = 0; n < team; n++) { RoomState.Instance.NextTeam(); await Task.Delay(300); }
        RoomState.Instance.ToggleReady();
        Debug.Log("[GD5] auto-ready");
    }
#endif

    public async Task<string> EnterLobby()
    {
        await Leave();
        Runner = NewRunner();
        var result = await Runner.JoinSessionLobby(SessionLobby.Shared);
        return result.Ok ? null : "Can't reach server: " + result.ShutdownReason;
    }

    public async Task<string> Create(string room, bool friendsOnly = false)
    {
        if (Rooms.Exists(r => r.Name == room)) return "A room with that name already exists.";
        return await StartRoom(room, true, !friendsOnly);
    }

    // region: the host's Photon region (from an invite / friend presence); null = our best region
    public Task<string> Join(string room, string region = null) => StartRoom(room, false, region: region);

    static Fusion.Photon.Realtime.FusionAppSettings InRegion(string region)
    {
        var s = Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings.GetCopy();
        s.FixedRegion = region;
        return s;
    }

    public string Region =>Runner && Runner.IsRunning && Runner.SessionInfo != null ? Runner.SessionInfo.Region : null;

    public Task<string> JoinOrCreate(string room, bool visible, string region = null) => StartRoom(room, true, visible, region: region);
    public static string AgainRegion;

    public Task<string> QuickMatch()
    {
        SessionInfo best = null;
        foreach (var r in Rooms)
            if (r.IsOpen && r.IsVisible && !IsLive(r) && r.PlayerCount < r.MaxPlayers && (best == null || r.PlayerCount > best.PlayerCount)) best = r;
        return best != null ? Join(best.Name) : JoinOrCreate("Quick " + UnityEngine.Random.Range(1000, 10000), true);
    }

    public static string HostOf(SessionInfo r) =>
        r.Properties != null && r.Properties.TryGetValue("host", out var h) ? (string)h.PropertyValue : null;

    public static bool IsLive(SessionInfo r) =>
        r.Properties != null && r.Properties.TryGetValue("live", out var l) && (int)l.PropertyValue == 1;

    public const string RejoinKey = "rejoin.room";
    public static string RejoinRoom => PlayerPrefs.GetString(RejoinKey, "");
    public static string RejoinRegion => PlayerPrefs.GetString(RejoinKey + ".region", "") is var r && r.Length > 0 ? r : null;
    public static void ForgetRejoin() { PlayerPrefs.DeleteKey(RejoinKey); PlayerPrefs.DeleteKey(RejoinKey + ".region"); }

    public static (string room, bool visible, int credits, int minutes, int map)? Again;

    public void SetUpPlayAgain()
    {
        var info = Runner ? Runner.SessionInfo : null;
        var state = RoomState.Instance;
        if (info == null || !info.IsValid) return;
        string name = info.Name;
        int hash = name.LastIndexOf(" #");
        int round = hash > 0 && int.TryParse(name[(hash + 2)..], out int n) ? n + 1 : 2;
        if (hash > 0 && round > 2) name = name[..hash];
        AgainRegion = info.Region;
        Again = (name + " #" + round, info.IsVisible, state ? state.Credits : 0, state ? state.Minutes : 0, state ? state.Map : 0);
    }

    public async Task Leave()
    {
        if (!Runner) return;
        var old = Runner;
        Runner = null;
        inRoom = false;
        Rooms = new();
        if (FriendsHub.CurrentRoom != null) FriendsHub.SetRoom(null);
        await old.Shutdown();
    }


    public bool IsHost => Runner && Runner.IsRunning && Runner.IsSharedModeMasterClient;

    public async void Kicked(string why)
    {
        await Leave();
        Disconnected?.Invoke(why);
    }

    public void StartMatch()
    {
        if (!IsHost || RoomState.Instance == null || RoomState.Instance.TooManyPlayers) return;
        int index = UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(GameMaps.PathOf(RoomState.Instance.MapInfo));
        Runner.SessionInfo.UpdateCustomProperties(new Dictionary<string, SessionProperty> { { "host", HostOf(Runner.SessionInfo) ?? AccountService.Username ?? "Player" }, { "live", 1 }, { "scene", index } });
        Runner.LoadScene(SceneRef.FromIndex(index));
    }

    static int LiveScene(SessionInfo r) =>
        r != null && r.IsValid && IsLive(r) && r.Properties.TryGetValue("scene", out var s) ? (int)s.PropertyValue : -1;

    async Task<string> StartRoom(string room, bool create, bool visible = true, int scene = -1, string region = null)
    {
        if (scene < 0) scene = LiveScene(Rooms.Find(r => r.Name == room));
        if (scene >= 0 && UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex != scene)
        {
            LateJoin = true;
            await Leave();
            LoadingScreen.Load(scene);
            while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex != scene) await Task.Yield();
        }
        try { return await EnterRoom(room, create, visible, scene, region); }
        finally { LateJoin = false; }
    }

    public static bool LateJoin { get; private set; }

    async Task<string> EnterRoom(string room, bool create, bool visible, int scene, string region)
    {
        if (inRoom || (region != null && Runner && Region != region)) await Leave();   // e.g. accepting an invite while in another room, or the host is in another region
        if (!Runner) Runner = NewRunner();
        var result = await Runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = room,
            PlayerCount = MaxPlayers + MaxSpectators,
            EnableClientSessionCreation = create,
            IsVisible = visible,
            SessionProperties = new System.Collections.Generic.Dictionary<string, SessionProperty> { { "host", AccountService.Username ?? "Player" }, { "live", 0 }, { "scene", -1 } },
            SceneManager = Runner.GetComponent<NetworkSceneManagerDefault>(),
            CustomPhotonAppSettings = region == null ? null : InRegion(region),
        });
#if DEVELOPMENT_BUILD
        Debug.Log("[GD5] enter room " + room + " -> " + (result.Ok ? "ok, region " + Region : result.ShutdownReason.ToString()));
        if (result.Ok && FriendsHub.DevArg("-autoready")) AutoReady(System.Environment.GetCommandLineArgs());
#endif
        if (!result.Ok)
        {
            if (room == RejoinRoom && result.ShutdownReason is ShutdownReason.GameNotFound or ShutdownReason.GameClosed) ForgetRejoin();
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex != 0) { LoadingScreen.Load(0); return "Couldn't rejoin the match: " + result.ShutdownReason; }
            await EnterLobby();
            return result.ShutdownReason switch
            {
                ShutdownReason.GameNotFound => "No room named \"" + room + "\".",
                ShutdownReason.GameIsFull => "That room is full.",
                ShutdownReason.GameClosed => "That game has already started.",
                _ => "Couldn't enter room: " + result.ShutdownReason,
            };
        }
        if (scene < 0 && LiveScene(Runner.SessionInfo) is int live && live >= 0)
        {
            return await StartRoom(room, false, visible, live, region);
        }
        inRoom = true;
        FriendsHub.SetRoom(room);
        Runner.Spawn(roomPlayerPrefab);
        if (Runner.IsSharedModeMasterClient && RoomState.Instance == null)
        {
            var state = Runner.Spawn(roomStatePrefab).GetComponent<RoomState>();
            if (Again is var (againRoom, _, credits, minutes, map) && againRoom == room && credits > 0) { state.Credits = credits; state.Minutes = minutes; state.SetMap(map); }
        }
        Again = null;
        return null;
    }

    NetworkRunner NewRunner()
    {
        var runner = Instantiate(runnerPrefab);
        DontDestroyOnLoad(runner.gameObject);
        var events = runner.GetComponent<NetworkEvents>();
        events.OnSessionListUpdate.AddListener((r, list) =>
        {
            if (r != Runner) return;
            Rooms = list;
            RoomsChanged?.Invoke();
        });
        events.OnShutdown.AddListener((r, reason) =>
        {
            if (r != Runner) return;
            bool wasInRoom = inRoom;
            Runner = null;
            inRoom = false;
            if (FriendsHub.CurrentRoom != null) FriendsHub.SetRoom(null);
            if (wasInRoom) Disconnected?.Invoke(reason.ToString());
        });
        return runner;
    }
}
