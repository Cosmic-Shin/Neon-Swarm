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
        if (room.HasStateAuthority && room.MatchStart <= 0f) room.MatchStart = runner.SimulationTime;
        Alerts.Show($"You are Player {Net.LocalSlot + 1}. Destroy every enemy building!", Alerts.Info, 6f);
    }

    void OnDestroy() => RoomService.Disconnected -= OnDisconnected;

    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
    float nextLog;
    void LogState() // test builds: what this PC sees, to compare players in Player.log
    {
        if (Time.time < nextLog) return;
        nextLog = Time.time + 5f;
        var sb = new System.Text.StringBuilder($"[GD5] slot {Net.LocalSlot} sees:");
        foreach (var e in NetEntity.All)
            if (e != null) sb.Append($" {e.name.Replace("_Net(Clone)", "")}/t{e.Health.team}/{(e.IsRemote ? "R" : "own")}@{e.transform.position.x:0},{e.transform.position.z:0} hp{e.Health.Current:0};");
        Debug.Log(sb.ToString());
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
        if (standing.Count > 1) hadOpponent = true;
        else if (hadOpponent) End("VICTORY", "Every enemy base is destroyed.");
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
            if (e == null || e.IsDead) continue;
            int value = e.TryGetComponent(out SelectableUnit u) ? u.Cost
                      : e.TryGetComponent(out Structure s) ? (s.data != null ? s.data.cost : 3000) : 0;
            worth.TryGetValue(e.Health.team, out int sum);
            worth[e.Health.team] = sum + value;
        }
        int best = -1, bestWorth = -1;
        foreach (var kv in worth) if (kv.Value > bestWorth) { best = kv.Key; bestWorth = kv.Value; }
        worth.TryGetValue(0, out int mine);
        End(best == 0 ? "VICTORY" : "DEFEAT",
            $"Time's up. {(best == 0 ? "Your" : Net.NameOf(best) + "'s")} army and base are worth the most (${bestWorth}; yours ${mine}).");
    }

    void End(string title, string detail)
    {
        over = true;
        endTitle.text = title;
        endTitle.color = title == "VICTORY" ? new Color(0.3f, 1f, 0.6f) : new Color(1f, 0.3f, 0.35f);
        endDetail.text = detail;
        endPanel.SetActive(true);
    }

    void OnDisconnected(string reason) { if (!over) End("DISCONNECTED", reason); }

    async void BackToMenu()
    {
        endButton.interactable = false;
        Net.InMatch = false;
        await RoomService.Instance.Leave();
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }
}
