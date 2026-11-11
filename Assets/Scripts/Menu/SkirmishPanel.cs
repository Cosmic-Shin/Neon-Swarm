using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Main menu > Skirmish, laid out like a multiplayer room (it is a copy of the RoomPanel, driven offline):
// 8 numbered seats (you + up to 7 AIs) with colour, team and difficulty, the map with the 8 bases
// (click a seat, then a base), starting credits, time limit, quick setups and START.
// The choices stay for the next visit; START hands them to Skirmish and loads the map.
public class SkirmishPanel : MonoBehaviour
{
    const int Seats = 8;

    class Seat
    {
        public bool used;
        public Skirmish.Difficulty difficulty = Skirmish.Difficulty.Normal;
        public int side;      // lobby team: 0 none, 1-4
        public int color;     // TeamMember.TeamColors index
        public int baseIndex; // 0..7
    }

    // Seat 0 is you. Kept between visits (reset when play mode restarts).
    static Seat[] seats;
    static int credits = 5000, minutes, map; // map: GameMaps.All index
    static GameMap M => GameMaps.At(map);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { seats = null; credits = 5000; minutes = 0; map = 0; }

    static readonly Color Empty = new(0.45f, 0.5f, 0.6f, 0.7f), Picked = new(1f, 1f, 1f, 0.25f), OpenSeat = new(0f, 0f, 0f, 0.2f);

    GameObject caller;
    Text title, waiting, hint;
    Button start;
    InputField customCredits;
    Button creditsButton, minutesButton, loadButton, mapButton;
    RectTransform mapRect;
    readonly System.Collections.Generic.List<(Button button, int players)> presets = new();
    readonly Button[] rows = new Button[Seats], diffs = new Button[Seats], bases = new Button[Seats];
    int picked = -1;

    // The lobby's RoomPanel is copied (inactive, so its RoomScreen never wakes) and its RoomScreen swapped for this.
    public static void Open(Component anyUI)
    {
        var room = System.Array.Find(Resources.FindObjectsOfTypeAll<RoomScreen>(), r => r.gameObject.scene.IsValid());
        if (room == null) { Debug.LogError("Skirmish: no RoomPanel in the scene to copy."); return; }
        var src = room.gameObject;
        bool was = src.activeSelf;
        src.SetActive(false);
        var copy = Instantiate(src, src.transform.parent);
        src.SetActive(was);
        copy.name = "SkirmishPanel";
        var invite = copy.transform.Find("FRIENDSButton");
        if (invite) DestroyImmediate(invite.gameObject); // online only
        // What RoomScreen added at runtime (if the room was opened before): this panel makes its own.
        foreach (var t in copy.GetComponentsInChildren<Transform>(true))
            if (t != null && (t.name == "MapButton" || t.name.EndsWith("(Clone)"))) DestroyImmediate(t.gameObject);
        foreach (var mb in copy.GetComponents<MonoBehaviour>()) if (!(mb is TabNavigation)) DestroyImmediate(mb);
        var panel = copy.AddComponent<SkirmishPanel>();
        panel.caller = anyUI.gameObject;
        anyUI.gameObject.SetActive(false);
        copy.SetActive(true);
    }

    void Awake()
    {
        if (seats == null)
        {
            seats = new Seat[Seats];
            for (int i = 0; i < Seats; i++) seats[i] = new Seat { color = i };
            seats[0].used = true;
            Add(); // one Normal enemy across the map
        }

        T Get<T>(string path) where T : Component => transform.Find(path).GetComponent<T>();
        title = Get<Text>("Title");
        title.text = "SKIRMISH";
        waiting = Get<Text>("Waiting");
        hint = Get<Text>("Hint");
        start = Get<Button>("StartGameButton");
        start.onClick.AddListener(StartMatch);
        var back = Get<Button>("LEAVEROOMButton");
        back.GetComponentInChildren<Text>().text = "BACK";
        HoloSkin.Button(back); // the lobby's LEAVE ROOM is Danger; Back is Default
        back.onClick.AddListener(Close);
        // Same spot as the room's status line: just above START.
        var status = (RectTransform)waiting.transform;
        status.anchoredPosition = ((RectTransform)start.transform).anchoredPosition + Vector2.up * 44f;
        status.sizeDelta = new Vector2(status.sizeDelta.x, 28f);
        waiting.resizeTextForBestFit = false;
        waiting.fontSize = 20;
        waiting.color = new Color(1f, 0.45f, 0.45f);

        creditsButton = Get<Button>("CreditsButton");
        creditsButton.onClick.AddListener(() => credits = Next(RoomState.CreditOptions, credits));
        minutesButton = Get<Button>("TimeButton");
        minutesButton.onClick.AddListener(() => minutes = Next(RoomState.MinuteOptions, minutes));
        customCredits = Get<InputField>("CustomCredits");
        customCredits.onSubmit.AddListener(SetCustomCredits);
        Get<Text>("TeamsNote").text = "Same team = allies. No team = everyone for themselves.";

        // Seats: the room's row, plus a difficulty button (a copy of the team button) where the name ended.
        var template = transform.Find("PlayerRows/PlayerRow").GetComponent<Button>();
        for (int i = 0; i < Seats; i++)
        {
            int seat = i;
            var row = Instantiate(template, template.transform.parent);
            row.gameObject.SetActive(true);
            row.onClick.AddListener(() => ClickRow(seat));
            row.transform.Find("Swatch").GetComponent<Button>().onClick.AddListener(() => NextColor(seat));
            var team = row.transform.Find("Team").GetComponent<Button>();
            team.onClick.AddListener(() => { var s = seats[seat]; s.side = (s.side + 1) % 5; });
            var diff = Instantiate(team, row.transform);
            diff.name = "Difficulty";
            ((RectTransform)diff.transform).anchoredPosition += Vector2.left * 120f;
            diff.onClick.AddListener(() => NextDifficulty(seat));
            ((RectTransform)row.transform.Find("Name")).sizeDelta = new Vector2(170f, 36f);
            rows[i] = row;
            diffs[i] = diff;
        }
        template.gameObject.SetActive(false);

        mapRect = (RectTransform)transform.Find("Map");
        var baseTemplate = mapRect.Find("Base").GetComponent<Button>();
        for (int b = 0; b < Seats; b++)
        {
            int index = b;
            var btn = Instantiate(baseTemplate, mapRect);
            btn.onClick.AddListener(() => { if (picked >= 0) { MoveToBase(picked, index); picked = -1; } });
            bases[b] = btn;
        }
        baseTemplate.gameObject.SetActive(false);
        mapButton = RoomScreen.MapButton(transform, minutesButton);
        mapButton.onClick.AddListener(NextMap);
        RoomScreen.ShowMap(mapRect, bases, M);

        // The chat column becomes quick setups; LOAD GAME takes the chat box's spot.
        Get<Text>("ChatHeader").text = "QUICK SETUP";
        var chatInput = (RectTransform)transform.Find("ChatInput");
        chatInput.gameObject.SetActive(false);
        var load = Instantiate(start, start.transform.parent);
        load.name = "LoadGameButton";
        load.onClick = new Button.ButtonClickedEvent();
        load.onClick.AddListener(() => SaveSlotsPanel.Open(this, saving: false));
        ((RectTransform)load.transform).anchoredPosition = chatInput.anchoredPosition;
        load.GetComponentInChildren<Text>().text = "LOAD GAME";
        loadButton = load;
        var box = transform.Find("ChatLog");
        box.Find("Text").gameObject.SetActive(false);
        var style = minutesButton;
        void Preset(string label, float y, System.Action setup, int players)
        {
            var b = Instantiate(style, box);
            presets.Add((b, players));
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => { setup(); picked = -1; });
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(420f, 50f);
            var t = b.GetComponentInChildren<Text>();
            t.text = label;
            t.rectTransform.sizeDelta = rt.sizeDelta;
            HoloSkin.Button(b, label == "CLEAR ALL AI" ? HoloUI.ButtonStyle.Danger : HoloUI.ButtonStyle.Default); // full-width, not a small toggle
        }
        Preset("1 VS 1", 180f, () => Setup(new[] { 0 }, 1), 2);
        Preset("1 VS 3  ·  FREE FOR ALL", 110f, () => Setup(new[] { 0, 0, 0 }, 0), 4);
        Preset("2 VS 2  ·  YOU + ALLY", 40f, () => Setup(new[] { 1, 2, 2 }, 1), 4);
        Preset("3 VS 3  ·  YOU + 2 ALLIES", -30f, () => Setup(new[] { 1, 1, 2, 2, 2 }, 1), 6);
        Preset("4 VS 4  ·  YOU + 3 ALLIES", -30f, () => Setup(new[] { 1, 1, 1, 2, 2, 2, 2 }, 1), 8);
        Preset("8 PLAYER FREE FOR ALL", -100f, () => Setup(new[] { 0, 0, 0, 0, 0, 0, 0 }, 0), 8);
        Preset("CLEAR ALL AI", -170f, () => Setup(new int[0], 0), 1);
    }

    // Next map; the seats start over with the setup it is made for (GameMap.startAi).
    void NextMap()
    {
        map = (map + 1) % GameMaps.All.Length;
        picked = -1;
        RoomScreen.ShowMap(mapRect, bases, M);
        Setup(M.startAi, M.startYou);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !customCredits.isFocused && !SaveSlotsPanel.IsOpen && SaveSlotsPanel.ClosedAt != Time.frameCount) { Close(); return; }

        for (int i = 0; i < Seats; i++)
        {
            var s = seats[i];
            var row = rows[i];
            row.gameObject.SetActive(i < M.Players); // a seat per base
            bool used = s.used;
            string name = i == 0 ? "You" : used ? "AI" : "<color=#7A8699>Open  ·  click to add AI</color>";
            var label = row.transform.Find("Name").GetComponent<Text>();
            label.text = $"{i + 1}.  {name}";
            label.rectTransform.sizeDelta = new Vector2(used && i > 0 ? 170f : 480f, 36f); // AI rows make room for the difficulty button
            row.transform.Find("Base").GetComponent<Text>().text = used ? $"BASE {s.baseIndex + 1}" : "";
            var swatch = row.transform.Find("Swatch");
            swatch.gameObject.SetActive(used);
            swatch.GetComponent<Image>().color = TeamMember.TeamColors[s.color];
            var team = row.transform.Find("Team");
            team.gameObject.SetActive(used);
            team.GetComponentInChildren<Text>().text = s.side == 0 ? "NO TEAM" : $"TEAM {s.side}";
            diffs[i].gameObject.SetActive(used && i > 0);
            diffs[i].GetComponentInChildren<Text>().text = s.difficulty.ToString().ToUpper();
            row.GetComponent<Image>().color = picked == i ? Picked : used ? new Color(0f, 0f, 0f, 0.4f) : OpenSeat;
        }

        mapButton.GetComponentInChildren<Text>().text = RoomScreen.MapLabel(M, true);
        float y = 180f; // the setups that fit this map, stacked from the top
        foreach (var (button, players) in presets)
        {
            bool fits = players <= M.Players;
            button.gameObject.SetActive(fits);
            if (fits) { ((RectTransform)button.transform).anchoredPosition = new Vector2(0f, y); y -= 65f; }
        }
        for (int b = 0; b < M.Players; b++)
        {
            int seat = SeatAt(b);
            bases[b].GetComponent<Image>().color = seat >= 0 ? TeamMember.TeamColors[seats[seat].color] : Empty;
            bases[b].GetComponentInChildren<Text>().text = seat >= 0 ? (seat + 1).ToString() : "";
            bases[b].interactable = picked >= 0;
        }

        creditsButton.GetComponentInChildren<Text>().text = $"${credits:N0}";
        minutesButton.GetComponentInChildren<Text>().text = minutes > 0 ? $"{minutes} MIN" : "NO LIMIT";
        hint.text = picked >= 0 ? $"Now click a base for player {picked + 1}."
                  : "Click a colour, team or difficulty to change it. Click a player, then a base to move them.";

        string problem = Problem();
        waiting.text = problem ?? "";
        start.interactable = problem == null;
        start.GetComponentInChildren<Text>().text = "START GAME";
        loadButton.interactable = SaveSystem.HasAnySave;
    }

    // Why START is off (null = good to go).
    static string Problem()
    {
        int enemies = 0;
        for (int i = 1; i < Seats; i++)
            if (seats[i].used && (seats[0].side == 0 || seats[i].side != seats[0].side)) enemies++;
        return enemies == 0 ? "Add at least one AI that isn't on your team." : null;
    }

    // ---- seats ----

    void ClickRow(int seat)
    {
        if (!seats[seat].used) { Use(seat); return; }
        picked = picked == seat ? -1 : seat;
    }

    static void Add()
    {
        for (int i = 1; i < M.Players; i++) if (!seats[i].used) { Use(i); return; }
    }

    static void Use(int seat)
    {
        var s = seats[seat];
        s.used = true;
        s.difficulty = Skirmish.Difficulty.Normal;
        s.side = 0;
        s.color = FreeColor(seat, seat);
        s.baseIndex = FreeBase();
    }

    static void NextDifficulty(int seat)
    {
        var s = seats[seat];
        if (s.difficulty == Skirmish.Difficulty.Hard) s.used = false; // past Hard = remove the AI
        else s.difficulty++;
    }

    static void NextColor(int seat) => seats[seat].color = FreeColor(seat, seats[seat].color + 1);

    // The first colour from `from` on that no other seat uses.
    static int FreeColor(int seat, int from)
    {
        int n = TeamMember.TeamColors.Length;
        for (int k = 0; k < n; k++)
        {
            int c = (from + k) % n;
            bool taken = false;
            for (int i = 0; i < Seats; i++) if (i != seat && seats[i].used && seats[i].color == c) taken = true;
            if (!taken) return c;
        }
        return from % n;
    }

    static int FreeBase()
    {
        if (SeatAt(0) < 0) return 0;
        foreach (int b in M.fill) if (SeatAt(b) < 0) return b;
        return 0;
    }

    static int SeatAt(int b)
    {
        for (int i = 0; i < Seats; i++) if (seats[i].used && seats[i].baseIndex == b) return i;
        return -1;
    }

    // Whoever had that base swaps with the seat.
    static void MoveToBase(int seat, int b)
    {
        int other = SeatAt(b);
        if (other >= 0) seats[other].baseIndex = seats[seat].baseIndex;
        seats[seat].baseIndex = b;
    }

    // Quick setups: one entry per AI = its team (0 none); `you` = your team. Bases and colours reset.
    static void Setup(int[] aiTeams, int you)
    {
        for (int i = 1; i < Seats; i++) seats[i].used = false;
        seats[0].side = you;
        seats[0].baseIndex = 0;
        // Allies sit next to you, enemies across the map.
        int[] allyBases = M.allies.Length > 0 ? M.allies : M.fill, enemyBases = M.enemies;
        int nextAlly = 0, nextEnemy = 0;
        for (int k = 0; k < aiTeams.Length && k < M.Players - 1; k++)
        {
            int seat = k + 1;
            Use(seat);
            seats[seat].side = aiTeams[k];
            bool ally = you != 0 && aiTeams[k] == you;
            int want = ally ? allyBases[nextAlly++ % allyBases.Length] : enemyBases[nextEnemy++ % enemyBases.Length];
            if (SeatAt(want) < 0) seats[seat].baseIndex = want;
        }
    }

    // ---- settings ----

    static int Next(int[] options, int current)
    {
        int i = System.Array.IndexOf(options, current);
        return options[(i + 1) % options.Length];
    }

    // "15000", "$15,000" and "15k" all work (same as the room).
    void SetCustomCredits(string text)
    {
        text = text.Trim().ToLower().Replace("$", "").Replace(",", "");
        int scale = text.EndsWith("k") ? 1000 : 1;
        if (scale > 1) text = text.TrimEnd('k');
        if (int.TryParse(text, out int amount)) credits = Mathf.Clamp(amount * scale, 500, 1000000);
        customCredits.text = "";
    }

    void Close()
    {
        if (caller != null) caller.SetActive(true);
        Destroy(gameObject);
    }

    void StartMatch()
    {
        if (Problem() != null) return;
        Skirmish.Opponents.Clear();
        for (int i = 1; i < Seats; i++)
        {
            var s = seats[i];
            if (s.used) Skirmish.Opponents.Add(new Skirmish.Opponent { team = i, difficulty = s.difficulty, side = s.side, color = s.color, baseIndex = s.baseIndex });
        }
        Skirmish.PlayerSide = seats[0].side;
        Skirmish.PlayerColor = seats[0].color;
        Skirmish.PlayerBase = seats[0].baseIndex;
        Skirmish.StartCredits = credits;
        Skirmish.Minutes = minutes;
        Skirmish.ElapsedBefore = 0f; // a new match, not a loaded one
        Skirmish.Active = true;
        UnityEngine.SceneManagement.SceneManager.LoadScene(M.scene);
    }
}
