using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

// The only class that talks to Photon Fusion (Shared mode). Menus call this, never the runner.
// A runner can't be restarted after shutdown, so each lobby/room visit gets a fresh one.
public class RoomService : MonoBehaviour
{
    public const int MaxPlayers = 8;
    public static RoomService Instance { get; private set; }
    public static event Action RoomsChanged;
    public static event Action<string> Disconnected; // dropped out of a room without pressing Leave

    public NetworkRunner runnerPrefab; // NetworkRunner + NetworkSceneManagerDefault + NetworkEvents
    public NetworkObject roomPlayerPrefab;
    public NetworkObject roomStatePrefab; // seats, bases and settings; spawned by whoever opens the room
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
    // Test builds only: "GD5.exe -autojoin MyRoom" joins that room without signing in (keeps retrying
    // until it exists), so a second copy of the game can be a test opponent.
    async void Start()
    {
        var args = System.Environment.GetCommandLineArgs();
        if (FriendsHub.DevArg("-quickmatch")) // "-quickmatch": QUICK MATCH once signed in, then ready up
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

    // "-team N": join lobby team N. Then click READY after a few seconds (so the host sees NOT READY first).
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

    // friendsOnly: left off the public room list; friends get in with JOIN or an invite.
    public async Task<string> Create(string room, bool friendsOnly = false)
    {
        if (Rooms.Exists(r => r.Name == room)) return "A room with that name already exists.";
        return await StartRoom(room, true, !friendsOnly);
    }

    public Task<string> Join(string room) => StartRoom(room, false);

    // Joins it if it exists, else creates it (Photon decides atomically, so a group racing here meets up).
    public Task<string> JoinOrCreate(string room, bool visible) => StartRoom(room, true, visible);

    // QUICK MATCH: the fullest open public room with a seat, else a new public one.
    public Task<string> QuickMatch()
    {
        SessionInfo best = null;
        foreach (var r in Rooms)
            if (r.IsOpen && r.IsVisible && r.PlayerCount < r.MaxPlayers && (best == null || r.PlayerCount > best.PlayerCount)) best = r;
        return best != null ? Join(best.Name) : JoinOrCreate("Quick " + UnityEngine.Random.Range(1000, 10000), true);
    }

    public static string HostOf(SessionInfo r) =>
        r.Properties != null && r.Properties.TryGetValue("host", out var h) ? (string)h.PropertyValue : null;

    // PLAY AGAIN: the group's next room ("Name #2", "#3"...) and the settings to bring along.
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

    // Host only: lock the room and send everyone to the map (Fusion loads it on every client).
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
            EnableClientSessionCreation = create, // Join must not silently create a new room
            IsVisible = visible,
            SessionProperties = new System.Collections.Generic.Dictionary<string, SessionProperty> { { "host", AccountService.Username ?? "Player" } },
            SceneManager = Runner.GetComponent<NetworkSceneManagerDefault>(),
        });
        if (!result.Ok)
        {
            await EnterLobby(); // the failed runner is dead; get back on the room list
            return result.ShutdownReason switch
            {
                ShutdownReason.GameNotFound => "No room named \"" + room + "\".",
                ShutdownReason.GameIsFull => "That room is full.",
                ShutdownReason.GameClosed => "That game has already started.",
                _ => "Couldn't enter room: " + result.ShutdownReason,
            };
        }
        inRoom = true;
        FriendsHub.SetRoom(room); // friends see it and can JOIN
        Runner.Spawn(roomPlayerPrefab); // Shared mode: each player spawns and owns their own
        if (Runner.IsSharedModeMasterClient && RoomState.Instance == null) // the room's creator
        {
            var state = Runner.Spawn(roomStatePrefab).GetComponent<RoomState>();
            if (Again is var (againRoom, _, credits, minutes, map) && againRoom == room && credits > 0) { state.Credits = credits; state.Minutes = minutes; state.SetMap(map); }
        }
        Again = null;
        return null;
    }

    NetworkRunner NewRunner()
    {
        // Prefab, not AddComponent: NetworkEvents' UnityEvents are only created when serialized.
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
            if (r != Runner) return; // we shut it down ourselves
            bool wasInRoom = inRoom;
            Runner = null;
            inRoom = false;
            if (FriendsHub.CurrentRoom != null) FriendsHub.SetRoom(null);
            if (wasInRoom) Disconnected?.Invoke(reason.ToString());
        });
        return runner;
    }
}
