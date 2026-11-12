using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

public class RoomService : MonoBehaviour
{
    public const int MaxPlayers = 8;
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

    public Task<string> Join(string room) => StartRoom(room, false);

    public Task<string> JoinOrCreate(string room, bool visible) => StartRoom(room, true, visible);

    public Task<string> QuickMatch()
    {
        SessionInfo best = null;
        foreach (var r in Rooms)
            if (r.IsOpen && r.IsVisible && r.PlayerCount < r.MaxPlayers && (best == null || r.PlayerCount > best.PlayerCount)) best = r;
        return best != null ? Join(best.Name) : JoinOrCreate("Quick " + UnityEngine.Random.Range(1000, 10000), true);
    }

    public static string HostOf(SessionInfo r) =>
        r.Properties != null && r.Properties.TryGetValue("host", out var h) ? (string)h.PropertyValue : null;

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

    public void StartMatch()
    {
        if (!IsHost || RoomState.Instance == null || RoomState.Instance.TooManyPlayers) return;
        Runner.SessionInfo.IsOpen = false;
        int index = UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(GameMaps.PathOf(RoomState.Instance.MapInfo));
        Runner.LoadScene(SceneRef.FromIndex(index));
    }

    async Task<string> StartRoom(string room, bool create, bool visible = true)
    {
        if (!Runner) Runner = NewRunner();
        var result = await Runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = room,
            PlayerCount = MaxPlayers,
            EnableClientSessionCreation = create,
            IsVisible = visible,
            SessionProperties = new System.Collections.Generic.Dictionary<string, SessionProperty> { { "host", AccountService.Username ?? "Player" } },
            SceneManager = Runner.GetComponent<NetworkSceneManagerDefault>(),
        });
        if (!result.Ok)
        {
            await EnterLobby();
            return result.ShutdownReason switch
            {
                ShutdownReason.GameNotFound => "No room named \"" + room + "\".",
                ShutdownReason.GameIsFull => "That room is full.",
                ShutdownReason.GameClosed => "That game has already started.",
                _ => "Couldn't enter room: " + result.ShutdownReason,
            };
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
