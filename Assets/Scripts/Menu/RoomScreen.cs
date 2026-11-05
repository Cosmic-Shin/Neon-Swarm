using UnityEngine;
using UnityEngine.UI;

// Inside a room, C&C skirmish style: numbered, coloured players, the map with the 8 bases, starting
// credits and time limit. Only the host changes things: click a player, then a base (whoever had
// that base swaps with them). Leave is wired by MultiplayerMenu.
public class RoomScreen : MonoBehaviour
{
    public Text title, waiting, hint;
    public Button startGame;
    public Button playerTemplate;   // inactive row: Swatch (Image), Name (Text), Base (Text)
    public RectTransform map;       // the map picture; base circles are placed on it
    public Button baseTemplate;     // inactive circle with a Label (Text)
    public Button credits, minutes; // host cycles through the options

    // Base_1..Base_8 on the RTS map (x, z); the map picture covers -300..300 both ways.
    static readonly Vector2[] BasePositions =
        { new(-90, -218), new(90, -218), new(218, -90), new(218, 90), new(90, 218), new(-90, 218), new(-218, 90), new(-218, -90) };
    const float MapHalf = 300f;
    static readonly Color Empty = new(0.45f, 0.5f, 0.6f, 0.7f);
    static readonly Color Picked = new(1f, 1f, 1f, 0.25f);

    readonly Button[] rows = new Button[RoomService.MaxPlayers];
    readonly Button[] bases = new Button[RoomService.MaxPlayers];
    int picked = -1; // host: the player slot chosen in the list

    void Awake()
    {
        startGame.onClick.AddListener(() =>
        {
            startGame.interactable = false;
            RoomService.Instance.StartMatch();
        });
        credits.onClick.AddListener(() => RoomState.Instance?.NextCredits());
        minutes.onClick.AddListener(() => RoomState.Instance?.NextMinutes());

        for (int i = 0; i < rows.Length; i++)
        {
            int slot = i;
            rows[i] = Instantiate(playerTemplate, playerTemplate.transform.parent);
            rows[i].onClick.AddListener(() => picked = picked == slot ? -1 : slot);
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
        playerTemplate.gameObject.SetActive(false);
        baseTemplate.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        startGame.interactable = true;
        picked = -1;
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
            rows[i].transform.Find("Name").GetComponent<Text>().text = $"{i + 1}.  {room.NameOf(i)}{tags}";
            rows[i].transform.Find("Base").GetComponent<Text>().text = $"BASE {room.Bases[i] + 1}";
            rows[i].transform.Find("Swatch").GetComponent<Image>().color = TeamMember.ColorOf(i);
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

        credits.GetComponentInChildren<Text>().text = $"${room.Credits}";
        minutes.GetComponentInChildren<Text>().text = room.Minutes > 0 ? $"{room.Minutes} MIN" : "NO LIMIT";
        credits.interactable = minutes.interactable = host;
        hint.text = !host ? "Only the host can move players and change the settings."
                  : picked >= 0 ? $"Now click a base for player {picked + 1}."
                  : "Click a player, then a base to move them there (swaps if taken).";

        // The host can change if the creator leaves, so check every frame.
        startGame.gameObject.SetActive(host);
        waiting.gameObject.SetActive(!host);
    }
}
