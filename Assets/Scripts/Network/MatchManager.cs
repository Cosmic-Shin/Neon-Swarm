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

    System.Collections.IEnumerator Start()
    {
        endPanel.SetActive(false);
        timerText.transform.parent.gameObject.SetActive(false);
        var room = RoomState.Instance;
        var runner = Net.Runner;
        if (room == null || runner == null || !runner.IsRunning) yield break; // single player

        Net.InMatch = true;
        endButton.onClick.AddListener(BackToMenu);
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
        if (!Net.InMatch || over) return;
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
        if (!standing.Contains(0)) { End("DEFEAT", "All your buildings were destroyed."); return; }
        var sides = new HashSet<int>();
        foreach (int t in standing) sides.Add(Side(t));
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
        End(best == us ? "VICTORY" : "DEFEAT",
            $"Time's up. {who} army and base are worth the most (${bestWorth}; yours ${mine}).");
    }

    void End(string title, string detail)
    {
        Debug.Log($"[GD5] end {title}: {detail}");
        over = true;
        endTitle.text = title;
        endTitle.color = title == "VICTORY" ? new Color(0.3f, 1f, 0.6f) : new Color(1f, 0.3f, 0.35f);
        endDetail.text = detail;
        endPanel.SetActive(true);
    }

    public bool IsOver => over;

    // Esc menu: remove all our units and buildings (everyone else sees us lose) and show the defeat screen.
    public void Surrender()
    {
        if (!Net.Online || over) return;
        foreach (var e in NetEntity.All.ToArray())
            if (e != null && !e.IsRemote) e.Despawn(0f);
        End("DEFEAT", "You surrendered.");
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
