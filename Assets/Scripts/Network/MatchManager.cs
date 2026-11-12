using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MatchManager : MonoBehaviour
{
    [Header("Single-player content, removed online")]
    [SerializeField] GameObject[] singlePlayerOnly;
    [SerializeField] Transform baseSpots;
    [SerializeField] Transform layoutNexus, layoutHive;
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

    const float Grace = 15f;
    float started = -1f, nextCheck;
    bool hadOpponent, over;
    bool spectating;
    Button spectateButton, playAgainButton;
    Text spectateBanner, statsNames;
    readonly Text[] statsColumns = new Text[MatchStats.Headers.Length];

    System.Collections.IEnumerator Start()
    {
        ResearchManager.Clear();
        Health.LastBuildingDeath = null;
        endPanel.SetActive(false);
        timerText.transform.parent.gameObject.SetActive(false);
        var room = RoomState.Instance;
        var runner = Net.Runner;
        if (room == null || runner == null || !runner.IsRunning)
        {
            if (Skirmish.Active) StartSkirmish();
            if (Tutorial.Active)
            {
                var tut = gameObject.AddComponent<Tutorial>();
                tut.match = this;
                tut.hive = layoutHive != null ? layoutHive.GetComponent<Building>() : null;
            }
            if (!SaveSystem.Loaded)
            {
                if (layoutNexus != null) introNexus.Add(layoutNexus.gameObject);
                if (layoutHive != null) introHives.Add((layoutHive.GetComponent<Building>(), layoutHarvesters.Length));
                foreach (var h in layoutHarvesters) if (h != null) Destroy(h.gameObject);
                StartCoroutine(Intro());
            }
            else foreach (var b in introBrains) if (b != null) b.enabled = true;
            yield break;
        }

        Net.InMatch = true;
        MatchStats.Reset();
        endButton.onClick.AddListener(BackToMenu);
        BuildEndScreen();
        RoomService.Disconnected += OnDisconnected;

        var bank = ResourceBank.GetOrCreate();
        int diff = room.Credits - bank.Credits;
        if (diff > 0) bank.Add(diff); else if (diff < 0) bank.Spend(-diff);

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
        foreach (var go in singlePlayerOnly) if (go != null) Destroy(go);
        cam.position = mine.position + camOffset;

        while (runner.IsRunning && runner.SceneManager != null && runner.SceneManager.IsBusy) yield return null;
        yield return null;
        if (!runner.IsRunning) yield break;
        var nexus = Net.Spawn(spawns[0].prefab, spawns[0].pos, spawns[0].rot);
        var hive = Net.Spawn(spawns[1].prefab, spawns[1].pos, spawns[1].rot);
        introNexus.Add(nexus);
        introHives.Add((hive.GetComponent<Building>(), layoutHarvesters.Length));
        StartCoroutine(Intro());
        if (room.HasStateAuthority && room.MatchStart <= 0f)
        {
            room.MatchStart = runner.SimulationTime;
            foreach (var c in creeps) if (c.prefab != null) Net.Spawn(c.prefab, c.position, c.rotation, Creep.Team);
        }
        Alerts.Show($"You are Player {Net.LocalSlot + 1}. Destroy every enemy building!", Alerts.Info, 6f);
        Sfx.Say(Line.Start, 30f);
    }

    void OnDestroy() => RoomService.Disconnected -= OnDisconnected;


    void StartSkirmish()
    {
        if (!Tutorial.Active) Sfx.Say(Line.Start, 30f);
        MatchStats.Reset();
        endButton.onClick.AddListener(() => { Skirmish.Active = Tutorial.Active = false; Skirmish.ElapsedBefore = 0f; UnityEngine.SceneManagement.SceneManager.LoadScene(0); });
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
            var aiNexus = SpawnFor(ai.team, nexusPrefab, Place(layoutNexus), turn * layoutNexus.rotation);
            var aiHive = SpawnFor(ai.team, hivePrefab, Place(layoutHive), turn * layoutHive.rotation);
            if (SaveSystem.Loaded) foreach (var h in layoutHarvesters) SpawnFor(ai.team, harvesterPrefab, Place(h), turn * h.rotation);
            else { introNexus.Add(aiNexus); introHives.Add((aiHive.GetComponent<Building>(), layoutHarvesters.Length)); }
            TeamEconomy.Set(ai.team, Skirmish.StartCredits);

            var brain = new GameObject($"AI {ai.team}").AddComponent<AIPlayer>();
            brain.enabled = false;
            introBrains.Add(brain);
            brain.team = ai.team;
            brain.profile = Resources.Load<AIProfile>($"AI/{ai.difficulty}");
            brain.home = Flat(spot.position);
            brain.front = (Flat(centre) - Flat(spot.position)).normalized;
        }
        if (Skirmish.PlayerBase != 0)
        {
            Transform mine = baseSpots.GetChild(Skirmish.PlayerBase);
            Quaternion turn = TurnTo(mine);
            var cam = Camera.main.transform;
            cam.position = mine.position + (cam.position - homeSpot.position);
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
        if (!Tutorial.Active) Alerts.Show("Skirmish: destroy every enemy building!", Alerts.Info, 6f);
    }

    public static GameObject SpawnFor(int team, GameObject prefab, Vector3 pos, Quaternion rot)
    {
        var go = Instantiate(prefab, pos, rot);
        var h = go.GetComponent<Health>();
        h.team = team;
        if (go.TryGetComponent(out TeamMember tm)) tm.team = team;
        if (go.TryGetComponent(out SelectableUnit u)) u.ApplyTeamLook();
        return go;
    }


    const float IntroBuild = 3f, IntroGap = 0.7f;
    readonly List<GameObject> introNexus = new();
    readonly List<(Building hive, int count)> introHives = new();
    readonly List<AIPlayer> introBrains = new();

    System.Collections.IEnumerator Intro()
    {
        foreach (var n in introNexus)
        {
            if (n == null) continue;
            var site = n.AddComponent<Construction>();
            site.duration = IntroBuild;
            site.intro = true;
        }
        yield return new WaitForSeconds(IntroBuild + 0.4f);
        var harvester = harvesterPrefab.GetComponent<SelectableUnit>();
        int most = 0;
        foreach (var (_, count) in introHives) most = Mathf.Max(most, count);
        for (int i = 0; i < most; i++)
        {
            foreach (var (hive, count) in introHives) if (hive != null && i < count) hive.Release(harvester);
            yield return new WaitForSeconds(IntroGap);
        }
        foreach (var b in introBrains) if (b != null) b.enabled = true;
    }

    float skirmishStarted = -1f;

    void UpdateSkirmish()
    {
        if (Tutorial.Active || over || Time.time - skirmishStarted < 5f || Time.time < nextCheck) return;
        nextCheck = Time.time + 0.5f;
        var sides = new HashSet<int>();
        bool mine = false;
        foreach (var s in Structure.All) if (s != null && !s.Health.IsDead && s.Health.team != Creep.Team) { sides.Add(Skirmish.SideOf(s.Health.team)); mine |= s.Health.team == 0; }
        foreach (var c in FindObjectsByType<Construction>(FindObjectsSortMode.None)) { int t = TeamEconomy.TeamOf(c); sides.Add(Skirmish.SideOf(t)); mine |= t == 0; }
        if (!mine) End("DEFEAT", "All your buildings were destroyed.");
        else if (sides.Count == 1) End("VICTORY", "Every enemy base is destroyed.");
        else if (Skirmish.Minutes > 0 && SkirmishTimeLeft <= 0f) SkirmishTimeUp();
    }

    public static float SkirmishTimeLeft => Skirmish.Minutes * 60f - Skirmish.Elapsed;

    void SkirmishTimeUp()
    {
        Sfx.Say(Line.TimeUp, 3600f);
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
    void LogState()
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
        if (room.MatchStart <= 0f) return;
        if (started < 0f) started = Time.time;

        float elapsed = Net.Runner.SimulationTime - room.MatchStart;
        timerText.transform.parent.gameObject.SetActive(true);
        if (room.Minutes > 0)
        {
            float left = Mathf.Max(0f, room.Minutes * 60f - elapsed);
            timerText.text = $"{(int)left / 60:00}:{(int)left % 60:00}";
            timerText.color = left < 60f ? new Color(1f, 0.3f, 0.3f) : Color.white;
            if (left < 60f && left > 0f) Sfx.Say(Line.Hurry, 3600f);
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

    static int Side(int team)
    {
        int t = RoomState.Instance != null ? RoomState.Instance.TeamOf(Net.ToSlot(team)) : 0;
        return t != 0 ? 100 + t : team;
    }

    static HashSet<int> TeamsWithBuildings()
    {
        var teams = new HashSet<int>();
        foreach (var e in NetEntity.All)
            if (e != null && !e.IsDead && e.TryGetComponent(out Structure _)) teams.Add(e.Health.team);
        return teams;
    }

    void TimeUp()
    {
        Sfx.Say(Line.TimeUp, 3600f);
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

    bool friendButtons;
    bool recorded;

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

    public void Finish(string title, string detail) => End(title, detail);

    void End(string title, string detail, bool canSpectate = false)
    {
        Debug.Log($"[GD5] end {title}: {detail}");
        if (!recorded && (title == "VICTORY" || title == "DEFEAT")) { recorded = true; RecordResult(title == "VICTORY"); }
        over = true;
        spectating = false;
        spectateBanner.gameObject.SetActive(false);
        endTitle.text = title;
        endTitle.color = title == "VICTORY" || title == "TUTORIAL COMPLETE" ? new Color(0.3f, 1f, 0.6f) : title == "MATCH OVER" ? Alerts.Info : new Color(1f, 0.3f, 0.35f);
        endDetail.text = detail;
        FillStats(1f);
        spectateButton.gameObject.SetActive(canSpectate);
        bool again = Net.InMatch && title != "DISCONNECTED";
        playAgainButton.gameObject.SetActive(again);
        var row = new System.Collections.Generic.List<Button>();
        if (canSpectate) row.Add(spectateButton);
        if (again) row.Add(playAgainButton);
        row.Add(endButton);
        for (int i = 0; i < row.Count; i++)
            ((RectTransform)row[i].transform).anchoredPosition = new Vector2((i - (row.Count - 1) / 2f) * 420f, -330f);
        if (Net.InMatch && !friendButtons) { friendButtons = true; FriendsPanel.AddButtons(endButton.transform.parent, new Vector2(0f, -262f)); }
        if (title == "DEFEAT" || title == "MATCH OVER") FogOfWar.RevealAll = true;
        StopAllCoroutines();
        if (title == "VICTORY" || title == "DEFEAT") Sfx.Stinger(title == "VICTORY");
        StartCoroutine(Finale(title == "VICTORY" || title == "DEFEAT"));
#if DEVELOPMENT_BUILD
        if (again && FriendsHub.DevArg("-playagain")) Invoke(nameof(DevPlayAgain), 4f);
#endif
    }

#if DEVELOPMENT_BUILD
    void DevPlayAgain() { Debug.Log("[GD5] play again"); playAgainButton.onClick.Invoke(); }
#endif

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
            FillStats(1f - (1f - k) * (1f - k));
            yield return null;
        }
        FillStats(1f);
    }

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

    public void Surrender()
    {
        if (!Net.Online || over) return;
        Defeat("You surrendered.");
    }

    void BuildEndScreen()
    {
        ((RectTransform)endTitle.transform).anchoredPosition = new Vector2(0f, 330f);
        ((RectTransform)endDetail.transform).anchoredPosition = new Vector2(0f, 240f);

        spectateButton = Instantiate(endButton, endButton.transform.parent);
        spectateButton.name = "SpectateButton";
        spectateButton.onClick = new Button.ButtonClickedEvent();
        spectateButton.onClick.AddListener(Spectate);
        spectateButton.GetComponentInChildren<Text>().text = "SPECTATE";

        playAgainButton = Instantiate(endButton, endButton.transform.parent);
        playAgainButton.name = "PlayAgainButton";
        playAgainButton.onClick = new Button.ButtonClickedEvent();
        playAgainButton.onClick.AddListener(() => { playAgainButton.interactable = false; RoomService.Instance.SetUpPlayAgain(); BackToMenu(); });
        playAgainButton.GetComponentInChildren<Text>().text = "PLAY AGAIN";
        playAgainButton.gameObject.SetActive(false);

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
