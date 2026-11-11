using UnityEngine;
using UnityEngine.UI;

// Room browser: QUICK MATCH, create a room by name, join by name or by clicking a listed room (each shows
// its host and which friends are in it; friends' rooms first).
public class MultiplayerMenu : MonoBehaviour
{
    public GameObject lobbyPanel, roomPanel;
    public InputField roomName;
    public Button create, join, back, leave, friends, friendsOnly, quickMatch;
    public Text status, listHint;
    public Transform roomList;   // ScrollRect content
    public Button roomTemplate;  // inactive child of roomList, cloned per room
    string notice;               // shown after reconnecting, e.g. why we got dropped
    (string label, System.Func<System.Threading.Tasks.Task<string>> go)? pending; // a room to enter once connected (friend's, play again)
    bool hidden;                 // FRIENDS ONLY: the next room created is off the public list

    void Awake()
    {
        quickMatch.onClick.AddListener(() => EnterWith("Finding a match...", RoomService.Instance.QuickMatch));
        create.onClick.AddListener(() => Enter(true, roomName.text));
        join.onClick.AddListener(() => Enter(false, roomName.text));
        // Enter in the name box: join it if it exists, otherwise create it.
        roomName.onSubmit.AddListener(n => Enter(!RoomService.Instance.Rooms.Exists(r => r.Name == n.Trim()), n));
        back.onClick.AddListener(Back);
        leave.onClick.AddListener(LeaveRoom);
        FriendsPanel.Hook(friends);
        var onlyText = friendsOnly.GetComponentInChildren<Text>();
        onlyText.text = "PUBLIC";
        friendsOnly.onClick.AddListener(() => { hidden = !hidden; onlyText.text = hidden ? "FRIENDS ONLY" : "PUBLIC"; });
        RoomService.RoomsChanged += Redraw;
        FriendsHub.Changed += Redraw; // friends moving between rooms
        RoomService.Disconnected += OnDisconnected;
    }

    void OnDestroy()
    {
        RoomService.RoomsChanged -= Redraw;
        FriendsHub.Changed -= Redraw;
        RoomService.Disconnected -= OnDisconnected;
    }

    async void OnEnable()
    {
        Redraw();
        SetBusy(true);
        status.text = "Connecting...";
        string error = await RoomService.Instance.EnterLobby();
        if (!this) return;
        status.text = error ?? notice ?? "";
        notice = null;
        SetBusy(false);
        if (error == null && pending is var (label, go)) { pending = null; EnterWith(label, go); }
    }

    // JOIN on a friend or an invite, from any menu screen: close it, reconnect (leaving any room), join.
    public static void JoinFriendRoom(string room) => Open("Joining room...", () => RoomService.Instance.Join(room));

    // Back from an online match with PLAY AGAIN: straight into the group's next room.
    public static void PlayAgain()
    {
        if (RoomService.Again is var (room, visible, _, _)) Open("Joining your group...", () => RoomService.Instance.JoinOrCreate(room, visible));
    }

    static void Open(string label, System.Func<System.Threading.Tasks.Task<string>> go)
    {
        var menu = FindFirstObjectByType<MultiplayerMenu>(FindObjectsInactive.Include);
        if (!menu) return;
        foreach (Transform screen in menu.transform.parent)
        {
            if (screen == menu.transform || screen.name is "MenuBackground" or "Title" or "FriendInvite") continue;
            if (screen.GetComponent<SkirmishPanel>() || screen.GetComponent<SettingsPanel>() || screen.GetComponent<FriendsPanel>()) Destroy(screen.gameObject);
            else screen.gameObject.SetActive(false);
        }
        menu.pending = (label, go);
        if (menu.gameObject.activeSelf) menu.OnEnable(); // reconnects (leaving any room), then joins
        else menu.gameObject.SetActive(true);
    }

    void Enter(bool isNew, string room)
    {
        room = room.Trim();
        if (room.Length == 0) { status.text = "Enter a room name."; return; }
        EnterWith(isNew ? "Creating room..." : "Joining room...", () => isNew ? RoomService.Instance.Create(room, hidden) : RoomService.Instance.Join(room));
    }

    async void EnterWith(string label, System.Func<System.Threading.Tasks.Task<string>> go)
    {
        SetBusy(true);
        status.text = label;
        string error;
        try { error = await go(); }
        catch (System.Exception e) { Debug.LogException(e); error = "Something went wrong. Try again."; await RoomService.Instance.EnterLobby(); }
        SetBusy(false);
        if (error != null) { status.text = error; return; }
        gameObject.SetActive(false);
        roomPanel.SetActive(true);
    }

    async void Back()
    {
        await RoomService.Instance.Leave();
        gameObject.SetActive(false);
        lobbyPanel.SetActive(true);
    }

    async void LeaveRoom()
    {
        leave.interactable = false;
        await RoomService.Instance.Leave();
        leave.interactable = true;
        roomPanel.SetActive(false);
        gameObject.SetActive(true); // OnEnable reconnects to the room list
    }

    void OnDisconnected(string reason)
    {
        notice = "Disconnected from room: " + reason;
        roomPanel.SetActive(false);
        gameObject.SetActive(true);
    }

    void Redraw()
    {
        foreach (Transform child in roomList)
            if (child != roomTemplate.transform) Destroy(child.gameObject);

        int shown = 0;
        var rooms = RoomService.Instance.Rooms.FindAll(r => r.IsOpen && r.IsVisible);
        rooms.Sort((a, b) => FriendsIn(b.Name).Length.CompareTo(FriendsIn(a.Name).Length)); // friends' rooms first
        foreach (var room in rooms)
        {
            var b = Instantiate(roomTemplate, roomList);
            b.gameObject.SetActive(true);
            string host = RoomService.HostOf(room), pals = FriendsIn(room.Name);
            b.GetComponentInChildren<Text>().text = room.Name + "    " + room.PlayerCount + "/" + room.MaxPlayers
                + (pals.Length > 0 ? "    <color=#60FF90>" + pals + "</color>" // friends matter more than who hosts
                   : host != null ? "    <color=#8A96B0>host " + host + "</color>" : "");
            string name = room.Name;
            b.onClick.AddListener(() => Enter(false, name));
            shown++;
        }
        listHint.text = shown == 0 ? "No rooms yet. Create one!" : "";
    }

    // "★ ann, bob" for friends whose status says they're in this room.
    static string FriendsIn(string room)
    {
        var names = new System.Collections.Generic.List<string>();
        foreach (var f in FriendsHub.Friends) if (FriendsHub.ActivityOf(f.Member)?.room == room) names.Add(FriendsHub.NameOf(f.Member));
        return names.Count > 0 ? "★ " + string.Join(", ", names) : "";
    }

    void SetBusy(bool busy) => create.interactable = join.interactable = quickMatch.interactable = !busy;
}
