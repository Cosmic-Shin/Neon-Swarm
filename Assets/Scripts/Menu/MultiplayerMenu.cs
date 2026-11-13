using UnityEngine;
using UnityEngine.UI;

public class MultiplayerMenu : MonoBehaviour
{
    public GameObject lobbyPanel, roomPanel;
    public InputField roomName;
    public Button create, join, back, leave, friends, friendsOnly, quickMatch;
    public Text status, listHint;
    public Transform roomList;
    public Button roomTemplate;
    string notice;
    (string label, System.Func<System.Threading.Tasks.Task<string>> go)? pending;
    bool hidden;

    void Awake()
    {
        quickMatch.onClick.AddListener(() => EnterWith("Finding a match...", RoomService.Instance.QuickMatch));
        create.onClick.AddListener(() => Enter(true, roomName.text));
        join.onClick.AddListener(() => Enter(false, roomName.text));
        roomName.onSubmit.AddListener(n => Enter(!RoomService.Instance.Rooms.Exists(r => r.Name == n.Trim()), n));
        back.onClick.AddListener(Back);
        leave.onClick.AddListener(LeaveRoom);
        FriendsPanel.Hook(friends);
        var onlyText = friendsOnly.GetComponentInChildren<Text>();
        onlyText.text = "PUBLIC";
        friendsOnly.onClick.AddListener(() => { hidden = !hidden; onlyText.text = hidden ? "FRIENDS ONLY" : "PUBLIC"; HoloSkin.SetOn(friendsOnly, hidden); });
        RoomService.RoomsChanged += Redraw;
        FriendsHub.Changed += Redraw;
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

    public static void JoinFriendRoom(string room) => Open("Joining room...", () => RoomService.Instance.Join(room));

    public static void PlayAgain()
    {
        if (RoomService.Again is var (room, visible, _, _, _)) Open("Joining your group...", () => RoomService.Instance.JoinOrCreate(room, visible));
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
        if (menu.gameObject.activeSelf) menu.OnEnable();
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
        if (!this) return;
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
        gameObject.SetActive(true);
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
        string rejoin = RoomService.RejoinRoom;
        var all = RoomService.Instance.Rooms;
        if (rejoin.Length > 0 && !all.Exists(r => r.Name == rejoin && r.IsOpen && r.IsVisible))
        {
            var rb = Instantiate(roomTemplate, roomList);
            rb.gameObject.SetActive(true);
            rb.GetComponentInChildren<Text>().text = "<color=#FFB040>REJOIN</color>    " + rejoin;
            rb.onClick.AddListener(() => Enter(false, rejoin));
            shown++;
        }
        var rooms = all.FindAll(r => r.IsOpen && r.IsVisible);
        rooms.Sort((a, b) => (b.Name == rejoin).CompareTo(a.Name == rejoin) * 100 + FriendsIn(b.Name).Length.CompareTo(FriendsIn(a.Name).Length));
        foreach (var room in rooms)
        {
            var b = Instantiate(roomTemplate, roomList);
            b.gameObject.SetActive(true);
            string host = RoomService.HostOf(room), pals = FriendsIn(room.Name);
            string state = room.Name == rejoin ? "<color=#FFB040>REJOIN</color>    " : RoomService.IsLive(room) ? "<color=#FFB040>IN GAME · WATCH</color>    " : "";
            b.GetComponentInChildren<Text>().text = state + room.Name + "    " + room.PlayerCount + "/" + room.MaxPlayers
                + (pals.Length > 0 ? "    <color=#60FF90>" + pals + "</color>"
                   : host != null ? "    <color=#8A96B0>host " + host + "</color>" : "");
            string name = room.Name;
            b.onClick.AddListener(() => Enter(false, name));
            shown++;
        }
        listHint.text = shown == 0 ? "No rooms yet. Create one!" : "";
    }

    static string FriendsIn(string room)
    {
        var names = new System.Collections.Generic.List<string>();
        foreach (var f in FriendsHub.Friends) if (FriendsHub.ActivityOf(f.Member)?.room == room) names.Add(FriendsHub.NameOf(f.Member));
        return names.Count > 0 ? "★ " + string.Join(", ", names) : "";
    }

    void SetBusy(bool busy) => create.interactable = join.interactable = quickMatch.interactable = !busy;
}
