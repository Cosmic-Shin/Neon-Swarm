using UnityEngine;
using UnityEngine.UI;

public class RoomScreen : MonoBehaviour
{
    public Text title, waiting, hint;
    public Button startGame;
    public Button playerTemplate;
    public RectTransform map;
    public Button baseTemplate;
    public Button credits, minutes;
    public InputField customCredits;
    public InputField chatInput;
    public Text chatLog;
    public Button friends;
    const int ChatLines = 14;
    readonly System.Collections.Generic.List<string> chat = new();

    Button mapButton;
    GameMap shownMap;
    static readonly Color Empty = new(0.45f, 0.5f, 0.6f, 0.7f);
    static readonly Color Picked = new(1f, 1f, 1f, 0.25f);

    readonly Button[] rows = new Button[RoomService.MaxPlayers];
    readonly Button[] bases = new Button[RoomService.MaxPlayers];
    int picked = -1;
    bool starting;

    void Awake()
    {
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
        FriendsPanel.Hook(friends);
        RoomState.ChatReceived += AddChat;

        for (int i = 0; i < rows.Length; i++)
        {
            int slot = i;
            rows[i] = Instantiate(playerTemplate, playerTemplate.transform.parent);
            rows[i].onClick.AddListener(() => picked = picked == slot ? -1 : slot);
            rows[i].transform.Find("Swatch").GetComponent<Button>().onClick.AddListener(() => RoomState.Instance?.NextColor());
            rows[i].transform.Find("Team").GetComponent<Button>().onClick.AddListener(() => RoomState.Instance?.NextTeam());
        }
        for (int b = 0; b < bases.Length; b++)
        {
            int index = b;
            var btn = Instantiate(baseTemplate, map);
            btn.onClick.AddListener(() =>
            {
                var room = RoomState.Instance;
                if (room == null || !room.IsHost || picked < 0) return;
                room.MoveToBase(picked, index);
                picked = -1;
            });
            bases[b] = btn;
        }
        var status = (RectTransform)waiting.transform;
        status.anchoredPosition = ((RectTransform)startGame.transform).anchoredPosition + Vector2.up * 44f;
        status.sizeDelta = new Vector2(status.sizeDelta.x, 28f);
        waiting.resizeTextForBestFit = false;
        waiting.fontSize = Mathf.Min(waiting.fontSize, 20);
        playerTemplate.gameObject.SetActive(false);
        baseTemplate.gameObject.SetActive(false);
        mapButton = MapButton(transform, minutes);
        mapButton.onClick.AddListener(() => RoomState.Instance?.NextMap());
    }


    public static Button MapButton(Transform panel, Button style)
    {
        var header = panel.Find("MapHeader");
        var b = Instantiate(style, panel);
        b.name = "MapButton";
        b.onClick = new Button.ButtonClickedEvent();
        var rt = (RectTransform)b.transform;
        var hr = (RectTransform)header;
        rt.anchoredPosition = hr.anchoredPosition;
        rt.sizeDelta = new Vector2(hr.sizeDelta.x, 40f);
        b.GetComponentInChildren<Text>().rectTransform.sizeDelta = rt.sizeDelta;
        header.gameObject.SetActive(false);
        HoloSkin.Button(b);
        return b;
    }

    public static void ShowMap(RectTransform map, Button[] bases, GameMap m)
    {
        if (map.TryGetComponent(out Image picture)) picture.sprite = GameMaps.Preview(m);
        for (int b = 0; b < bases.Length; b++)
        {
            bool has = b < m.Players;
            bases[b].gameObject.SetActive(has);
            if (has) ((RectTransform)bases[b].transform).anchoredPosition = m.bases[b] / m.Half * (map.rect.width * 0.5f);
        }
    }

    public static string MapLabel(GameMap m, bool canChange) => canChange ? $"MAP:  {m.name}   >>" : $"MAP:  {m.name}";

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
        title.text = runner.SessionInfo.Name.ToUpper() + "   " + RoomPlayer.All.Count + "/" + runner.SessionInfo.MaxPlayers
            + (runner.SessionInfo.IsVisible ? "" : "   <size=20>FRIENDS ONLY</size>");

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

        var m = room.MapInfo;
        if (m != shownMap) { shownMap = m; ShowMap(map, bases, m); picked = -1; }
        mapButton.GetComponentInChildren<Text>().text = MapLabel(m, host);
        mapButton.interactable = host;
        for (int b = 0; b < m.Players; b++)
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

        startGame.gameObject.SetActive(true);
        waiting.gameObject.SetActive(true);
        bool allReady = room.EveryoneReady();
        bool meReady = room.IsReady(room.SlotOf(runner.LocalPlayer));
        startGame.GetComponentInChildren<Text>().text = host ? "START GAME" : meReady ? "NOT READY" : "READY";
        bool crowded = room.TooManyPlayers;
        startGame.interactable = host ? allReady && !starting && !crowded : true;
        waiting.text = crowded ? $"Too many players for {m.name.Split(' ')[0]}. The host picks a bigger map."
                     : host ? (allReady ? "" : "Waiting for everyone to click READY.")
                     : meReady ? "Waiting for the host to start." : "Click READY when you are set.";
    }

    void OnDestroy() => RoomState.ChatReceived -= AddChat;

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
        chatInput.ActivateInputField();
    }

    void AddChat(int slot, string text, bool toAll)
    {
        string name = RoomState.Instance != null ? RoomState.Instance.NameOf(slot) : "?";
        string colour = ColorUtility.ToHtmlStringRGB(TeamMember.ColorOf(slot));
        chat.Add($"<color=#{colour}><b>{(toAll ? "" : "(Team) ")}{name}:</b></color> {text.Replace("<", "<​")}");
        if (chat.Count > ChatLines) chat.RemoveAt(0);
        chatLog.text = string.Join("\n", chat);
    }
}
