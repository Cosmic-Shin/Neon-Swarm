using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Multiplayer match on the RTS map (loaded from a room). Single player: does nothing.
// Clears the single-player setup, gives everyone their starting credits and base (a copy of the
// single-player Base_1 layout, turned to face the centre from their own base spot), runs the time
// limit and decides who won. Lose = all your buildings gone; last player with buildings wins;
// time up = the biggest army + base (by cost) wins.
public class MatchManager : MonoBehaviour
{
    [Header("Single-player content, removed online")]
    [SerializeField] GameObject[] singlePlayerOnly; // starting base, harvesters, enemy squad, creeps, outposts
    [SerializeField] Transform baseSpots;           // Base_1..Base_8
    [SerializeField] Transform layoutNexus, layoutHive; // the single-player base: copied for every player
    [SerializeField] Transform[] layoutHarvesters;
    [SerializeField] GameObject nexusPrefab, hivePrefab, harvesterPrefab;

    [System.Serializable] public struct CreepSpawn { public GameObject prefab; public Vector3 position; public Quaternion rotation; }
    [Tooltip("The single-player creeps (baked from the scene): online the host spawns these as network units.")]
    [SerializeField] CreepSpawn[] creeps;

    [Header("UI")]
    [SerializeField] Text timerText;
    [SerializeField] GameObject endPanel;
    [SerializeField] Text endTitle, endDetail;
    [SerializeField] Button endButton;

    const float Grace = 15f; // seconds before anyone can lose (bases are still spawning)
    float started = -1f, nextCheck;
    bool hadOpponent, over;
    bool spectating; // lost, still watching (fog lifted) until the match is decided
    Button spectateButton;
    Text spectateBanner, statsNames;
    readonly Text[] statsColumns = new Text[MatchStats.Headers.Length];

    System.Collections.IEnumerator Start()
    {
        endPanel.SetActive(false);
        timerText.transform.parent.gameObject.SetActive(false);
        var room = RoomState.Instance;
        var runner = Net.Runner;
        if (room == null || runner == null || !runner.IsRunning) yield break; // single player

        Net.InMatch = true;
        MatchStats.Reset();
        endButton.onClick.AddListener(BackToMenu);
        BuildEndScreen();
        RoomService.Disconnected += OnDisconnected;

        // Starting credits.
        var bank = ResourceBank.GetOrCreate();
        int diff = room.Credits - bank.Credits;
        if (diff > 0) bank.Add(diff); else if (diff < 0) bank.Spend(-diff);

        // Our base: the single-player layout around Base_1, turned to our base spot.
        Transform home = baseSpots.GetChild(0), mine = baseSpots.GetChild(room.Bases[Net.LocalSlot]);
        Quaternion turn = Quaternion.Euler(0f, Vector3.SignedAngle(Flat(home.position), Flat(mine.position), Vector3.up), 0f);
        Vector3 Place(Transform t) => mine.position + turn * (t.position - home.position);
        var cam = Camera.main.transform;
        Vector3 camOffset = cam.position - home.position;

        var spawns = new List<(GameObject prefab, Vector3 pos, Quaternion rot)>
        {
            (nexusPrefab, Place(layoutNexus), turn * layoutNexus.rotation),
            (hivePrefab, Place(layoutHive), turn * layoutHive.rotation),
        };
        foreach (var h in layoutHarvesters) spawns.Add((harvesterPrefab, Place(h), turn * h.rotation));
        foreach (var go in singlePlayerOnly) if (go != null) Destroy(go);
        cam.position = mine.position + camOffset; // CameraPan keeps its angle

        // Fusion can't spawn while it is still loading this scene (Start runs in the middle of that).
        while (runner.IsRunning && runner.SceneManager != null && runner.SceneManager.IsBusy) yield return null;
        yield return null;
        if (!runner.IsRunning) yield break;
        foreach (var s in spawns) Net.Spawn(s.prefab, s.pos, s.rot);
        if (room.HasStateAuthority && room.MatchStart <= 0f)
        {
            room.MatchStart = runner.SimulationTime;
            foreach (var c in creeps) if (c.prefab != null) Net.Spawn(c.prefab, c.position, c.rotation, Creep.Team);
        }
        Alerts.Show($"You are Player {Net.LocalSlot + 1}. Destroy every enemy building!", Alerts.Info, 6f);
    }

    void OnDestroy() => RoomService.Disconnected -= OnDisconnected;

    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    float nextLog;
    bool chatSent, pingSent;
    void LogState() // test builds: what this PC sees, to compare players in Player.log
    {
        if (Time.time < nextLog) return;
        nextLog = Time.time + 5f;
        var sb = new System.Text.StringBuilder($"[GD5] slot {Net.LocalSlot} sees:");
        foreach (var e in NetEntity.All)
            if (e != null) sb.Append($" {e.name.Replace("_Net(Clone)", "")}/t{e.Health.team}/{(e.IsRemote ? "R" : "own")}@{e.transform.position.x:0},{e.transform.position.z:0} hp{e.Health.Current:0};");
        float ore = 0f; foreach (var o in OreNode.All) ore += o.amount;
        int creepCount = 0; foreach (var e in NetEntity.All) if (e != null && e.Health.team == Creep.Team && !e.IsDead) creepCount++;
        sb.Append($" creeps={creepCount} teams={RoomState.Instance.TeamOf(0)},{RoomState.Instance.TeamOf(1)} colours={RoomState.Instance.ColorIndexOf(0)},{RoomState.Instance.ColorIndexOf(1)} lowPowerOther={Net.LowPower(1)}");
        foreach (var op in FindObjectsByType<OutpostCapture>(FindObjectsSortMode.None)) if (op.ownerTeam >= 0 || op.progress > 0f) sb.Append($" {op.name}:own{op.ownerTeam}/cap{op.capturingTeam}/{op.progress:0.00}/hp{op.GetComponent<Health>().Current:0}");
        sb.Append($" ore={ore:0}");
        foreach (var e in NetEntity.All)
            if (e != null && e.IsRemote && e.TryGetComponent(out Harvester hv)) sb.Append($" harv{hv.AnimIndex}/{hv.CargoFraction:0.00}");
        Debug.Log(sb.ToString());
#if DEVELOPMENT_BUILD
        if (!chatSent && started > 0f && Time.time - started > 20f && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autojoin") >= 0)
        { chatSent = true; RoomState.Instance.SendChat($"hello from slot {Net.LocalSlot}"); }
        if (!pingSent && started > 0f && Time.time - started > 25f && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-autojoin") >= 0)
        { pingSent = true; Ping.Send(Camera.main.transform.position + Camera.main.transform.forward * 20f); }
#endif
    }
#endif

    void Update()
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (Net.InMatch) LogState();
#endif
        if (!Net.InMatch || (over && !spectating)) return;
        var room = RoomState.Instance;
        if (room == null || !Net.Online) { End("DISCONNECTED", "Lost the connection to the match."); return; }
        if (room.MatchStart <= 0f) return; // the host hasn't stamped the start yet
        if (started < 0f) started = Time.time;

        float elapsed = Net.Runner.SimulationTime - room.MatchStart;
        timerText.transform.parent.gameObject.SetActive(true);
        if (room.Minutes > 0)
        {
            float left = Mathf.Max(0f, room.Minutes * 60f - elapsed);
            timerText.text = $"{(int)left / 60:00}:{(int)left % 60:00}";
            timerText.color = left < 60f ? new Color(1f, 0.3f, 0.3f) : Color.white;
            if (left <= 0f) { TimeUp(); return; }
        }
        else timerText.text = $"{(int)elapsed / 60:00}:{(int)elapsed % 60:00}";

        if (Time.time < nextCheck || Time.time - started < Grace) return;
        nextCheck = Time.time + 0.5f;
        var standing = TeamsWithBuildings();
        var sides = new HashSet<int>();
        foreach (int t in standing) sides.Add(Side(t));
        if (spectating) { if (sides.Count <= 1) End("MATCH OVER", Winner(sides)); return; }
        if (!standing.Contains(0)) { Defeat("All your buildings were destroyed."); return; }
        if (sides.Count > 1) hadOpponent = true;
        else if (hadOpponent) End("VICTORY", "Every enemy base is destroyed.");
    }

    // Allies share a side (their lobby team); without a team every player is their own side.
    static int Side(int team)
    {
        int t = RoomState.Instance != null ? RoomState.Instance.TeamOf(Net.ToSlot(team)) : 0;
        return t != 0 ? 100 + t : team;
    }

    // Teams (0 = us) with at least one building standing or being built.
    static HashSet<int> TeamsWithBuildings()
    {
        var teams = new HashSet<int>();
        foreach (var e in NetEntity.All)
            if (e != null && !e.IsDead && e.TryGetComponent(out Structure _)) teams.Add(e.Health.team);
        return teams;
    }

    void TimeUp()
    {
        var worth = new Dictionary<int, int>();
        foreach (var e in NetEntity.All)
        {
            if (e == null || e.IsDead || e.Health.team == Creep.Team) continue;
            int value = e.TryGetComponent(out SelectableUnit u) ? u.Cost
                      : e.TryGetComponent(out Structure s) ? (s.data != null ? s.data.cost : 3000) : 0;
            int side = Side(e.Health.team);
            worth.TryGetValue(side, out int sum);
            worth[side] = sum + value;
        }
        int best = -1, bestWorth = -1;
        foreach (var kv in worth) if (kv.Value > bestWorth) { best = kv.Key; bestWorth = kv.Value; }
        int us = Side(0);
        worth.TryGetValue(us, out int mine);
        string who = best == us ? (us >= 100 ? "Your team's" : "Your") : best >= 100 ? $"Team {best - 100}'s" : Net.NameOf(best) + "'s";
        if (spectating) End("MATCH OVER", $"Time's up. {who} army and base are worth the most (${bestWorth}).");
        else End(best == us ? "VICTORY" : "DEFEAT",
            $"Time's up. {who} army and base are worth the most (${bestWorth}; yours ${mine}).");
    }

    // canSpectate: we lost but the others play on, so we may stay and watch.
    void End(string title, string detail, bool canSpectate = false)
    {
        Debug.Log($"[GD5] end {title}: {detail}");
        over = true;
        spectating = false;
        spectateBanner.gameObject.SetActive(false);
        endTitle.text = title;
        endTitle.color = title == "VICTORY" ? new Color(0.3f, 1f, 0.6f) : title == "MATCH OVER" ? Alerts.Info : new Color(1f, 0.3f, 0.35f);
        endDetail.text = detail;
        FillStats(1f);
        spectateButton.gameObject.SetActive(canSpectate);
        float x = canSpectate ? 210f : 0f;
        ((RectTransform)endButton.transform).anchoredPosition = new Vector2(x, -330f);
        ((RectTransform)spectateButton.transform).anchoredPosition = new Vector2(-x, -330f);
        endPanel.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(RevealEnd());
    }

    // The title slams in, then everyone's stats count up.
    System.Collections.IEnumerator RevealEnd()
    {
        Color title = endTitle.color, detail = endDetail.color;
        const float slam = 0.35f, count = 1.4f;
        FillStats(0f);
        for (float t = 0f; t < slam; t += Time.unscaledDeltaTime)
        {
            float k = t / slam;
            endTitle.rectTransform.localScale = Vector3.one * Mathf.Lerp(3f, 1f, k * k);
            endTitle.color = new Color(title.r, title.g, title.b, k);
            endDetail.color = new Color(detail.r, detail.g, detail.b, k);
            yield return null;
        }
        endTitle.rectTransform.localScale = Vector3.one;
        endTitle.color = title;
        endDetail.color = detail;
        var cam = Camera.main;
        if (cam != null) Juice.Shake(cam.transform.position + cam.transform.forward * 30f, 0.4f);
        for (float t = 0f; t < count; t += Time.unscaledDeltaTime)
        {
            float k = t / count;
            FillStats(1f - (1f - k) * (1f - k)); // fast, then settling
            yield return null;
        }
        FillStats(1f);
    }

    // Lost: our leftover units go too (as on surrender), then the defeat screen, with SPECTATE
    // if two or more other sides still fight it out.
    void Defeat(string why)
    {
        foreach (var e in NetEntity.All.ToArray())
            if (e != null && !e.IsRemote) e.Despawn(0f);
        var others = new HashSet<int>();
        foreach (int t in TeamsWithBuildings()) if (t != 0) others.Add(Side(t));
        End("DEFEAT", why, others.Count > 1);
    }

    void Spectate()
    {
        spectating = true;
        endPanel.SetActive(false);
        FogOfWar.RevealAll = true;
        spectateBanner.gameObject.SetActive(true);
    }

    static string Winner(HashSet<int> sides)
    {
        foreach (int s in sides) return s >= 100 ? $"Team {s - 100} wins!" : $"{Net.NameOf(s)} wins!";
        return "Nobody is left standing.";
    }

    public bool IsOver => over && !spectating;

    // Esc menu: remove all our units and buildings (everyone else sees us lose) and show the defeat screen.
    public void Surrender()
    {
        if (!Net.Online || over) return;
        Defeat("You surrendered.");
    }

    // ---- end screen: title, detail, everyone's stats, SPECTATE / BACK TO MENU ----
    void BuildEndScreen()
    {
        ((RectTransform)endTitle.transform).anchoredPosition = new Vector2(0f, 330f);
        ((RectTransform)endDetail.transform).anchoredPosition = new Vector2(0f, 240f);

        spectateButton = Instantiate(endButton, endButton.transform.parent);
        spectateButton.name = "SpectateButton";
        spectateButton.onClick = new Button.ButtonClickedEvent();
        spectateButton.onClick.AddListener(Spectate);
        spectateButton.GetComponentInChildren<Text>().text = "SPECTATE";

        // Columns: player names, then one per stat; a header on top, one line per player below.
        const float nameWidth = 340f, statWidth = 170f;
        float left = -(nameWidth + statWidth * statsColumns.Length) / 2f;
        statsNames = Column(left + nameWidth / 2f, nameWidth, "PLAYER", TextAnchor.UpperLeft);
        for (int k = 0; k < statsColumns.Length; k++)
            statsColumns[k] = Column(left + nameWidth + statWidth * (k + 0.5f), statWidth, MatchStats.Headers[k], TextAnchor.UpperCenter);

        spectateBanner = MakeText(endPanel.transform.parent, "SPECTATING   ·   the fog is lifted   ·   Esc to leave", 26, TextAnchor.MiddleCenter, Alerts.Info);
        var rt = spectateBanner.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 60f);
        rt.sizeDelta = new Vector2(1000f, 40f);
        spectateBanner.gameObject.SetActive(false);
    }

    Text Column(float x, float width, string header, TextAnchor align)
    {
        var head = MakeText(endPanel.transform, header, 20, align == TextAnchor.UpperLeft ? TextAnchor.LowerLeft : TextAnchor.LowerCenter, new Color(0.6f, 0.68f, 0.8f));
        head.rectTransform.anchoredPosition = new Vector2(x, 160f);
        head.rectTransform.sizeDelta = new Vector2(width, 56f);
        var body = MakeText(endPanel.transform, "", 26, align, Color.white);
        body.rectTransform.pivot = new Vector2(0.5f, 1f);
        body.rectTransform.anchoredPosition = new Vector2(x, 120f);
        body.rectTransform.sizeDelta = new Vector2(width, 360f);
        body.lineSpacing = 1.25f;
        return body;
    }

    Text MakeText(Transform parent, string text, int size, TextAnchor align, Color color)
    {
        var t = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = endDetail.font;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.text = text;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }

    // amount: 0..1 of each number, for the count-up.
    void FillStats(float amount)
    {
        var room = RoomState.Instance;
        if (room == null) return;
        var names = new System.Text.StringBuilder();
        var cols = new System.Text.StringBuilder[statsColumns.Length];
        for (int k = 0; k < cols.Length; k++) cols[k] = new System.Text.StringBuilder();
        for (int slot = 0; slot < RoomService.MaxPlayers; slot++)
        {
            var player = room.Slots[slot];
            if (player == Fusion.PlayerRef.None) continue;
            bool me = slot == Net.LocalSlot;
            var rp = RoomPlayer.Of(player);
            int team = room.TeamOf(slot);
            names.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGB(TeamMember.ColorOf(slot))}>{room.NameOf(slot)}</color>"
                + (team > 0 ? $"  <size=18>T{team}</size>" : "") + (me ? "  <size=18>(you)</size>" : ""));
            for (int k = 0; k < cols.Length; k++)
            {
                int v = Mathf.RoundToInt((me ? MatchStats.Values[k] : rp != null ? rp.Stats[k] : 0) * amount);
                cols[k].AppendLine(k == (int)MatchStats.Kind.Earned ? $"${v:N0}" : v.ToString());
            }
        }
        statsNames.text = names.ToString();
        for (int k = 0; k < cols.Length; k++) statsColumns[k].text = cols[k].ToString();
    }

    void OnDisconnected(string reason) { if (!over) End("DISCONNECTED", reason); }

    public async void BackToMenu()
    {
        endButton.interactable = false;
        Net.InMatch = false;
        await RoomService.Instance.Leave();
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }
}
