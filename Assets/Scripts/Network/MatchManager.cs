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
        ResearchManager.Clear(); // research belongs to one match
        Health.LastBuildingDeath = null;
        endPanel.SetActive(false);
        timerText.transform.parent.gameObject.SetActive(false);
        var room = RoomState.Instance;
        var runner = Net.Runner;
        if (room == null || runner == null || !runner.IsRunning) // single player
        {
            if (Skirmish.Active) StartSkirmish();
            yield break;
        }

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

    // ================= skirmish (single player vs AI) =================

    // The scene's enemy squad goes; creeps and outposts stay. Your base (the scene's, at Base_1) moves to the
    // base you picked in the lobby; each AI gets a copy of the Base_1 layout on its own base spot.
    void StartSkirmish()
    {
        MatchStats.Reset();
        endButton.onClick.AddListener(() => { Skirmish.Active = false; Skirmish.ElapsedBefore = 0f; UnityEngine.SceneManagement.SceneManager.LoadScene(0); });
        BuildEndScreen();
        if (GameObject.Find("Enemies") is GameObject squad) Destroy(squad);
        TeamEconomy.Set(0, Skirmish.StartCredits);

        Transform homeSpot = baseSpots.GetChild(0);
        Quaternion TurnTo(Transform spot) => Quaternion.Euler(0f, Vector3.SignedAngle(Flat(homeSpot.position), Flat(spot.position), Vector3.up), 0f);
        Vector3 centre = Vector3.zero;
        foreach (var ai in Skirmish.Opponents)
        {
            Transform spot = baseSpots.GetChild(ai.baseIndex);
            Quaternion turn = TurnTo(spot);
            Vector3 Place(Transform t) => spot.position + turn * (t.position - homeSpot.position);
            SpawnFor(ai.team, nexusPrefab, Place(layoutNexus), turn * layoutNexus.rotation);
            SpawnFor(ai.team, hivePrefab, Place(layoutHive), turn * layoutHive.rotation);
            foreach (var h in layoutHarvesters) SpawnFor(ai.team, harvesterPrefab, Place(h), turn * h.rotation);
            TeamEconomy.Set(ai.team, Skirmish.StartCredits);

            var brain = new GameObject($"AI {ai.team}").AddComponent<AIPlayer>();
            brain.team = ai.team;
            brain.profile = Resources.Load<AIProfile>($"AI/{ai.difficulty}");
            brain.home = Flat(spot.position);
            brain.front = (Flat(centre) - Flat(spot.position)).normalized;
        }
        // Yours last: the AIs copy the layout from where it stood.
        if (Skirmish.PlayerBase != 0)
        {
            Transform mine = baseSpots.GetChild(Skirmish.PlayerBase);
            Quaternion turn = TurnTo(mine);
            var cam = Camera.main.transform;
            cam.position = mine.position + (cam.position - homeSpot.position); // CameraPan keeps its angle
            var moving = new List<Transform> { layoutNexus, layoutHive };
            moving.AddRange(layoutHarvesters);
            foreach (var t in moving)
            {
                if (t == null) continue;
                Vector3 to = mine.position + turn * (t.position - homeSpot.position);
                t.SetPositionAndRotation(to, turn * t.rotation);
                if (t.TryGetComponent(out UnityEngine.AI.NavMeshAgent agent) && agent.enabled) agent.Warp(to);
            }
        }

        skirmishStarted = Time.time;
        Alerts.Show("Skirmish: destroy every enemy building!", Alerts.Info, 6f);
    }

    static void SpawnFor(int team, GameObject prefab, Vector3 pos, Quaternion rot)
    {
        var go = Instantiate(prefab, pos, rot);
        var h = go.GetComponent<Health>();
        h.team = team; // before Start: colours and power follow the team
        if (go.TryGetComponent(out TeamMember tm)) tm.team = team;
        if (go.TryGetComponent(out SelectableUnit u)) u.ApplyTeamLook();
    }

    float skirmishStarted = -1f;

    // Same rules as online: lose when all your buildings are gone, win when your side is the only one left.
    void UpdateSkirmish()
    {
        if (over || Time.time - skirmishStarted < 5f || Time.time < nextCheck) return;
        nextCheck = Time.time + 0.5f;
        var sides = new HashSet<int>();
        bool mine = false;
        foreach (var s in Structure.All) if (s != null && !s.Health.IsDead && s.Health.team != Creep.Team) { sides.Add(Skirmish.SideOf(s.Health.team)); mine |= s.Health.team == 0; }
        foreach (var c in FindObjectsByType<Construction>(FindObjectsSortMode.None)) { int t = TeamEconomy.TeamOf(c); sides.Add(Skirmish.SideOf(t)); mine |= t == 0; }
        if (!mine) End("DEFEAT", "All your buildings were destroyed.");
        else if (sides.Count == 1) End("VICTORY", "Every enemy base is destroyed.");
        else if (Skirmish.Minutes > 0 && SkirmishTimeLeft <= 0f) SkirmishTimeUp();
    }

    // Seconds left of the lobby's time limit (Hud shows it as a countdown).
    public static float SkirmishTimeLeft => Skirmish.Minutes * 60f - Skirmish.Elapsed;

    // Like online: the side whose army and base are worth the most (by cost) wins.
    void SkirmishTimeUp()
    {
        var worth = new Dictionary<int, int>();
        foreach (var h in Health.All)
        {
            if (h == null || h.IsDead || h.team == Creep.Team) continue;
            int value = h.TryGetComponent(out SelectableUnit u) ? u.Cost
                      : h.TryGetComponent(out Structure s) ? (s.data != null ? s.data.cost : 3000) : 0;
            int side = Skirmish.SideOf(h.team);
            worth.TryGetValue(side, out int sum);
            worth[side] = sum + value;
        }
        int best = -1, bestWorth = -1;
        foreach (var kv in worth) if (kv.Value > bestWorth) { best = kv.Key; bestWorth = kv.Value; }
        int us = Skirmish.SideOf(0);
        worth.TryGetValue(us, out int mine);
        string who = best == us ? (us >= 100 ? "Your team's" : "Your") : best >= 100 ? $"Team {best - 100}'s" : (Skirmish.Find(best)?.Name ?? "An AI") + "'s";
        End(best == us ? "VICTORY" : "DEFEAT", $"Time's up. {who} army and base are worth the most (${bestWorth:N0}; yours ${mine:N0}).");
    }

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
        if (Skirmish.Active && !Net.InMatch) { UpdateSkirmish(); return; }
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

    bool recorded; // the result went into the player's profile (a defeat followed by spectating counts once)

    // Into the signed-in player's career stats (Account screen).
    void RecordResult(bool won)
    {
        var m = new PlayerProfile.MatchRecord { won = won, online = Net.InMatch, difficulty = -1, when = System.DateTime.UtcNow.Ticks,
            seconds = Net.InMatch ? Time.timeSinceLevelLoad : Skirmish.Elapsed, kills = MatchStats.Values[(int)MatchStats.Kind.Kills], unitsLost = MatchStats.Values[(int)MatchStats.Kind.UnitsLost] };
        if (m.online)
        {
            int others = RoomState.Instance != null ? RoomPlayer.All.Count - 1 : 0;
            m.opponents = $"{others} player{(others == 1 ? "" : "s")}";
        }
        else
        {
            var enemies = Skirmish.Opponents.FindAll(o => !o.ally);
            foreach (var o in enemies) m.difficulty = Mathf.Max(m.difficulty, (int)o.difficulty);
            m.opponents = $"{enemies.Count} AI: " + string.Join(", ", enemies.ConvertAll(o => o.difficulty.ToString()));
            int allies = Skirmish.Opponents.Count - enemies.Count;
            if (allies > 0) m.opponents += $"  (+{allies} all{(allies == 1 ? "y" : "ies")})";
        }
        ProfileService.Record(m, MatchStats.Values);
    }

    // canSpectate: we lost but the others play on, so we may stay and watch.
    void End(string title, string detail, bool canSpectate = false)
    {
        Debug.Log($"[GD5] end {title}: {detail}");
        if (!recorded && (title == "VICTORY" || title == "DEFEAT")) { recorded = true; RecordResult(title == "VICTORY"); }
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
        if (title == "DEFEAT" || title == "MATCH OVER") FogOfWar.RevealAll = true; // nothing of yours left to see with: show the whole map behind the results
        StopAllCoroutines();
        StartCoroutine(Finale(title == "VICTORY" || title == "DEFEAT"));
    }

    // The match was just decided by a building going up: the camera flies to it in slow motion (offline;
    // online the clock can't slow down) for a moment before the results.
    System.Collections.IEnumerator Finale(bool decided)
    {
        if (decided && Health.LastBuildingDeath is Vector3 boom && Time.time - Health.LastBuildingDeathAt < 3f)
        {
            Vector3 from = CameraPan.LookingAt;
            float slow = Net.InMatch ? 1f : 0.3f, before = Time.timeScale;
            Time.timeScale = slow;
            for (float t = 0f; t < 1.6f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / 0.6f);
                CameraPan.LookAt(Vector3.Lerp(from, boom, k * k * (3f - 2f * k)));
                yield return null;
            }
            Time.timeScale = before == 0f ? 1f : before;
        }
        endPanel.SetActive(true);
        yield return RevealEnd();
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
        if (room == null) { FillSkirmishStats(amount); return; }
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

    // Skirmish: your numbers; AIs are listed with whether they still stand (they don't keep stats).
    void FillSkirmishStats(float amount)
    {
        if (statsNames == null) return;
        var names = new System.Text.StringBuilder($"<color=#{ColorUtility.ToHtmlStringRGB(TeamMember.ColorOf(0))}>You</color>\n");
        var cols = new System.Text.StringBuilder[statsColumns.Length];
        for (int k = 0; k < cols.Length; k++)
        {
            int v = Mathf.RoundToInt(MatchStats.Values[k] * amount);
            cols[k] = new System.Text.StringBuilder((k == (int)MatchStats.Kind.Earned ? $"${v:N0}" : v.ToString()) + "\n");
        }
        foreach (var ai in Skirmish.Opponents)
        {
            bool alive = Structure.All.Exists(s => s != null && s.Health.team == ai.team);
            // AIs keep no stats: their status goes after the name, the columns show a dash.
            names.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGB(TeamMember.ColorOf(ai.team))}>{ai.Name}</color>  <color=#{(alive ? "60FF90" : "FF6070")}>{(alive ? "standing" : "destroyed")}</color>");
            for (int k = 0; k < cols.Length; k++) cols[k].AppendLine("<color=#6A7690>-</color>");
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
