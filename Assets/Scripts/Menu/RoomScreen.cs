using UnityEngine;
using UnityEngine.UI;

// Inside a room, C&C skirmish style: numbered, coloured players, the map with the 8 bases, starting
// credits and time limit. Only the host changes things: click a player, then a base (whoever had
// that base swaps with them). Leave is wired by MultiplayerMenu.
public class RoomScreen : MonoBehaviour
{
    public Text title, waiting, hint;
    public Button startGame;
    public Button playerTemplate;   // inactive row: Swatch (Button), Name (Text), Team (Button), Base (Text)
    public RectTransform map;       // the map picture; base circles are placed on it
    public Button baseTemplate;     // inactive circle with a Label (Text)
    public Button credits, minutes; // host cycles through the options
    public InputField customCredits; // host: type any amount, Enter
    public InputField chatInput;     // Enter sends
    public Text chatLog;
    const int ChatLines = 14;
    readonly System.Collections.Generic.List<string> chat = new();

    // Base_1..Base_8 on the RTS map (x, z); the map picture covers -300..300 both ways.
    static readonly Vector2[] BasePositions =
        { new(-90, -218), new(90, -218), new(218, -90), new(218, 90), new(90, 218), new(-90, 218), new(-218, 90), new(-218, -90) };
    const float MapHalf = 300f;
    static readonly Color Empty = new(0.45f, 0.5f, 0.6f, 0.7f);
    static readonly Color Picked = new(1f, 1f, 1f, 0.25f);

    readonly Button[] rows = new Button[RoomService.MaxPlayers];
    readonly Button[] bases = new Button[RoomService.MaxPlayers];
    int picked = -1; // host: the player slot chosen in the list
    bool starting;   // host clicked START

    void Awake()
    {
        // Host: start (once everyone is ready). Everyone else: the same button toggles READY.
        startGame.onClick.AddListener(() =>
        {
            var room = RoomState.Instance;
            if (room == null) return;
            if (!room.IsHost) { room.ToggleReady(); return; }
            starting = true;
            RoomService.Instance.StartMatch();
        });
        credits.onClick.AddListener(() => RoomState.Instance?.NextCredits());
        minutes.onClick.AddListener(() => RoomState.Instance?.NextMinutes());
        customCredits.onSubmit.AddListener(SetCustomCredits);
        chatInput.onSubmit.AddListener(SendChat);
        RoomState.ChatReceived += AddChat;

        for (int i = 0; i < rows.Length; i++)
        {
            int slot = i;
            rows[i] = Instantiate(playerTemplate, playerTemplate.transform.parent);
            rows[i].onClick.AddListener(() => picked = picked == slot ? -1 : slot);
            // Your own row: click the colour swatch or the team to change them (C&C style).
            rows[i].transform.Find("Swatch").GetComponent<Button>().onClick.AddListener(() => RoomState.Instance?.NextColor());
            rows[i].transform.Find("Team").GetComponent<Button>().onClick.AddListener(() => RoomState.Instance?.NextTeam());
        }
        for (int b = 0; b < bases.Length; b++)
        {
            int index = b;
            var btn = Instantiate(baseTemplate, map);
            btn.gameObject.SetActive(true);
            ((RectTransform)btn.transform).anchoredPosition = BasePositions[b] / MapHalf * (map.rect.width * 0.5f);
            btn.onClick.AddListener(() =>
            {
                var room = RoomState.Instance;
                if (room == null || !room.IsHost || picked < 0) return;
                room.MoveToBase(picked, index);
                picked = -1;
            });
            bases[b] = btn;
        }
        // The status line used to share the button's spot (only one showed); now both show, so it sits just above.
        var status = (RectTransform)waiting.transform;
        status.anchoredPosition = ((RectTransform)startGame.transform).anchoredPosition + Vector2.up * 44f;
        status.sizeDelta = new Vector2(status.sizeDelta.x, 28f);
        waiting.resizeTextForBestFit = false;
        waiting.fontSize = Mathf.Min(waiting.fontSize, 20);
        playerTemplate.gameObject.SetActive(false);
        baseTemplate.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        starting = false;
        picked = -1;
        chat.Clear();
        chatLog.text = "";
    }

    void Update()
    {
        var service = RoomService.Instance;
        var runner = service.Runner;
        var room = RoomState.Instance;
        if (!runner || !runner.SessionInfo.IsValid || room == null) return;
        bool host = room.IsHost;
        title.text = runner.SessionInfo.Name.ToUpper() + "   " + RoomPlayer.All.Count + "/" + runner.SessionInfo.MaxPlayers;

        for (int i = 0; i < rows.Length; i++)
        {
            var player = room.Slots[i];
            bool seated = player != Fusion.PlayerRef.None;
            rows[i].gameObject.SetActive(seated);
            if (!seated) { if (picked == i) picked = -1; continue; }
            string tags = (player == runner.LocalPlayer ? "  (you)" : "") + (player == room.Object.StateAuthority ? "  (host)" : "");
            string ready = room.IsHostSlot(i) ? "" : room.IsReady(i) ? "   <color=#60FF90>READY</color>" : "   <color=#FF8080>NOT READY</color>";
            rows[i].transform.Find("Name").GetComponent<Text>().text = $"{i + 1}.  {room.NameOf(i)}{tags}{ready}";
            rows[i].transform.Find("Base").GetComponent<Text>().text = $"BASE {room.Bases[i] + 1}";
            rows[i].transform.Find("Swatch").GetComponent<Image>().color = TeamMember.ColorOf(i);
            bool mine = player == runner.LocalPlayer;
            int team = room.TeamOf(i);
            var teamButton = rows[i].transform.Find("Team").GetComponent<Button>();
            teamButton.GetComponentInChildren<Text>().text = team == 0 ? "NO TEAM" : $"TEAM {team}";
            teamButton.interactable = mine;
            rows[i].transform.Find("Swatch").GetComponent<Button>().interactable = mine;
            rows[i].GetComponent<Image>().color = picked == i ? Picked : new Color(0f, 0f, 0f, 0.4f);
            rows[i].interactable = host;
        }

        for (int b = 0; b < bases.Length; b++)
        {
            int slot = room.SlotAtBase(b);
            bool taken = slot >= 0 && room.Slots[slot] != Fusion.PlayerRef.None;
            bases[b].GetComponent<Image>().color = taken ? TeamMember.ColorOf(slot) : Empty;
            bases[b].GetComponentInChildren<Text>().text = taken ? (slot + 1).ToString() : "";
            bases[b].interactable = host && picked >= 0;
        }

        credits.GetComponentInChildren<Text>().text = $"${room.Credits:N0}";
        minutes.GetComponentInChildren<Text>().text = room.Minutes > 0 ? $"{room.Minutes} MIN" : "NO LIMIT";
        credits.interactable = minutes.interactable = host;
        customCredits.gameObject.SetActive(host);
        hint.text = !host ? "Click your colour or team to change it. The host sets bases and settings."
                  : picked >= 0 ? $"Now click a base for player {picked + 1}."
                  : "Click your colour or team to change it. Click a player, then a base to move them.";

        // The host can change if the creator leaves, so check every frame.
        startGame.gameObject.SetActive(true);
        waiting.gameObject.SetActive(true);
        bool allReady = room.EveryoneReady();
        bool meReady = room.IsReady(room.SlotOf(runner.LocalPlayer));
        startGame.GetComponentInChildren<Text>().text = host ? "START GAME" : meReady ? "NOT READY" : "READY";
        startGame.interactable = host ? allReady && !starting : true;
        waiting.text = host ? (allReady ? "" : "Waiting for everyone to click READY.")
                     : meReady ? "Waiting for the host to start." : "Click READY when you are set.";
    }

    void OnDestroy() => RoomState.ChatReceived -= AddChat;

    // "15000", "$15,000" and "15k" all work.
    void SetCustomCredits(string text)
    {
        text = text.Trim().ToLower().Replace("$", "").Replace(",", "");
        int scale = text.EndsWith("k") ? 1000 : 1;
        if (scale > 1) text = text.TrimEnd('k');
        if (int.TryParse(text, out int amount)) RoomState.Instance?.SetCredits(amount * scale);
        customCredits.text = "";
    }

    void SendChat(string text)
    {
        text = text.Trim();
        if (text.Length > 0 && RoomState.Instance != null) RoomState.Instance.SendChat(text);
        chatInput.text = "";
        chatInput.ActivateInputField(); // keep typing
    }

    void AddChat(int slot, string text, bool toAll)
    {
        string name = RoomState.Instance != null ? RoomState.Instance.NameOf(slot) : "?";
        string colour = ColorUtility.ToHtmlStringRGB(TeamMember.ColorOf(slot));
        chat.Add($"<color=#{colour}><b>{(toAll ? "" : "(Team) ")}{name}:</b></color> {text.Replace("<", "<​")}"); // no rich-text tricks from players
        if (chat.Count > ChatLines) chat.RemoveAt(0);
        chatLog.text = string.Join("\n", chat);
    }
}
