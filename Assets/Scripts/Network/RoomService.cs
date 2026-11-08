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

    public async Task<string> Create(string room)
    {
        if (Rooms.Exists(r => r.Name == room)) return "A room with that name already exists.";
        return await StartRoom(room, true);
    }

    public Task<string> Join(string room) => StartRoom(room, false);

    public async Task Leave()
    {
        if (!Runner) return;
        var old = Runner;
        Runner = null;
        inRoom = false;
        Rooms = new();
        await old.Shutdown();
    }

    public const string MatchScene = "Assets/Scenes/RTSSelection.unity";
    public bool IsHost => Runner && Runner.IsRunning && Runner.IsSharedModeMasterClient;

    // Host only: lock the room and send everyone to the map (Fusion loads it on every client).
    public void StartMatch()
    {
        if (!IsHost) return;
        Runner.SessionInfo.IsOpen = false;
        int index = UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath(MatchScene);
        Runner.LoadScene(SceneRef.FromIndex(index));
    }

    async Task<string> StartRoom(string room, bool create)
    {
        if (!Runner) Runner = NewRunner();
        var result = await Runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Shared,
            SessionName = room,
            PlayerCount = MaxPlayers,
            EnableClientSessionCreation = create, // Join must not silently create a new room
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
        Runner.Spawn(roomPlayerPrefab); // Shared mode: each player spawns and owns their own
        if (Runner.IsSharedModeMasterClient && RoomState.Instance == null) Runner.Spawn(roomStatePrefab); // the room's creator
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
            if (wasInRoom) Disconnected?.Invoke(reason.ToString());
        });
        return runner;
    }
}
