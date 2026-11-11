using UnityEngine;
using UnityEngine.UI;

// Room browser: create a room by name, join by name or by clicking a listed room.
public class MultiplayerMenu : MonoBehaviour
{
    public GameObject lobbyPanel, roomPanel;
    public InputField roomName;
    public Button create, join, back, leave, friends, friendsOnly;
    public Text status, listHint;
    public Transform roomList;   // ScrollRect content
    public Button roomTemplate;  // inactive child of roomList, cloned per room
    string notice;               // shown after reconnecting, e.g. why we got dropped
    string pendingJoin;          // a friend's room to join once connected
    bool hidden;                 // FRIENDS ONLY: the next room created is off the public list

    void Awake()
    {
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
        RoomService.Disconnected += OnDisconnected;
    }

    void OnDestroy()
    {
        RoomService.RoomsChanged -= Redraw;
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
        if (error == null && pendingJoin != null) { string room = pendingJoin; pendingJoin = null; Enter(false, room); }
    }

    // JOIN on a friend or an invite, from any menu screen: close it, reconnect (leaving any room), join.
    public static void JoinFriendRoom(string room)
    {
        var menu = FindFirstObjectByType<MultiplayerMenu>(FindObjectsInactive.Include);
        if (!menu) return;
        foreach (Transform screen in menu.transform.parent)
        {
            if (screen == menu.transform || screen.name is "MenuBackground" or "Title" or "FriendInvite") continue;
            if (screen.GetComponent<SkirmishPanel>() || screen.GetComponent<SettingsPanel>() || screen.GetComponent<FriendsPanel>()) Destroy(screen.gameObject);
            else screen.gameObject.SetActive(false);
        }
        menu.pendingJoin = room;
        if (menu.gameObject.activeSelf) menu.OnEnable(); // reconnects (leaving any room), then joins
        else menu.gameObject.SetActive(true);
    }

    async void Enter(bool isNew, string room)
    {
        room = room.Trim();
        if (room.Length == 0) { status.text = "Enter a room name."; return; }
        SetBusy(true);
        status.text = isNew ? "Creating room..." : "Joining room...";
        string error;
        try { error = isNew ? await RoomService.Instance.Create(room, hidden) : await RoomService.Instance.Join(room); }
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
        foreach (var room in RoomService.Instance.Rooms)
        {
            if (!room.IsOpen || !room.IsVisible) continue;
            var b = Instantiate(roomTemplate, roomList);
            b.gameObject.SetActive(true);
            b.GetComponentInChildren<Text>().text = room.Name + "    " + room.PlayerCount + "/" + room.MaxPlayers;
            string name = room.Name;
            b.onClick.AddListener(() => Enter(false, name));
            shown++;
        }
        listHint.text = shown == 0 ? "No rooms yet. Create one!" : "";
    }

    void SetBusy(bool busy) => create.interactable = join.interactable = !busy;
}
