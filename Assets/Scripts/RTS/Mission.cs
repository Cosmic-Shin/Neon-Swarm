using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class Mission : MonoBehaviour
{
    public class Info
    {
        public string name, tagline, briefing;
        public int map, credits;
        public Skirmish.Difficulty? ai;
        public int aiBase = 4;
        public FactionId faction;   // what the player commands
    }

    public static readonly Info[] All =
    {
        new Info { name = "FIRST LIGHT", tagline = "Build an economy", map = 0, credits = 3000,
            briefing = "Command wants this sector online. Build a Data Refinery, field four harvesters and earn $8,000.\n\nScouts report raiders in the area. Keep your Core Nexus alive." },
        new Info { name = "HOLD THE LINE", tagline = "Survive six minutes", map = 0, credits = 8000,
            briefing = "A swarm is moving on your base in waves, each bigger than the last.\n\nDig in with turrets and units and survive for six minutes. If the Core Nexus falls, the mission fails." },
        new Info { name = "OUTPOST RUN", tagline = "Capture three outposts", map = 0, credits = 6000, ai = Skirmish.Difficulty.Easy, aiBase = 4,
            briefing = "Derelict outposts are scattered across the wasteland. Garrison crews are guarding them, and a rival commander wants them too.\n\nHold three outposts at the same time within 12 minutes." },
        new Info { name = "DECAPITATION", tagline = "Destroy the enemy Core", map = 1, credits = 7000, ai = Skirmish.Difficulty.Normal, aiBase = 1,
            briefing = "One enemy commander holds the far side of the canyon.\n\nYou don't need to wipe out their whole base. Destroy their Core Nexus and their network collapses." },
        // Aegis Corp campaign (unlocked from the start)
        new Info { name = "PERIMETER", tagline = "Fortify and hold", map = 0, credits = 7000, faction = FactionId.Aegis,
            briefing = "Aegis Corp is claiming this sector. The swarm will not give it up quietly.\n\nRaise a Shield Projector and two Sentinel Turrets, then hold your Command Spire for five minutes against the swarm." },
        new Info { name = "HOSTILE TAKEOVER", tagline = "Destroy the Hive Core", map = 1, credits = 8000, ai = Skirmish.Difficulty.Normal, aiBase = 1, faction = FactionId.Aegis,
            briefing = "A Hive commander has dug in across the canyon.\n\nThe board wants a clean acquisition: destroy their Core Nexus. Everything else is a write-off." },
    };

    public static int Current = -1;
    public static bool Active => Current >= 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => Current = -1;

    public static bool Done(int i) => PlayerPrefs.GetInt("mission.done." + i, 0) == 1;
    // each faction's first mission is open; the rest unlock in order
    public static bool Unlocked(int i) => i == 0 || All[i].faction != All[i - 1].faction || Done(i - 1);

    public static void Begin(int i)
    {
        var m = All[i];
        Skirmish.Opponents.Clear();
        if (m.ai is Skirmish.Difficulty d) Skirmish.Opponents.Add(new Skirmish.Opponent { team = 1, difficulty = d, color = 1, baseIndex = m.aiBase });
        Skirmish.PlayerSide = Skirmish.PlayerColor = Skirmish.PlayerBase = 0;
        Skirmish.StartCredits = m.credits;
        Skirmish.Minutes = 0;
        Skirmish.ElapsedBefore = 0f;
        Skirmish.Active = true;
        Tutorial.Active = false;
        Current = i;
        LoadingScreen.Load(GameMaps.At(m.map).scene);
    }

    const int Raiders = 7;

    public MatchManager match;

    class Goal { public Func<string> label; public Func<bool> done; public Text text; }

    readonly List<Goal> goals = new();
    Func<string> failure, countdown;
    readonly List<(float at, string[] units)> waves = new();
    int nextWave;
    Text title, timer;
    RectTransform panel;
    float started;
    bool finished;

    void Start()
    {
        started = Time.time;
        var m = All[Current];
        string hq = Faction.HqName(0);
        failure = () => HQ() == null ? $"Your {hq} was destroyed." : null;
        switch (Current)
        {
            case 0:
                Add(() => "Build a Data Refinery", () => Built("DataRefinery"));
                Add(() => $"Field 4 harvesters  ({Harvesters()}/4)", () => Harvesters() >= 4);
                Add(() => $"Earn $8,000  (${Earned():N0})", () => Earned() >= 8000);
                Wave(150f, "Skitter", "Skitter");
                Wave(300f, "Skitter", "Skitter", "Hackling", "Hackling");
                break;
            case 1:
                Add(() => $"Survive  {Clock(Mathf.Max(0f, 360f - Elapsed))}", () => Elapsed >= 360f);
                Wave(45f, "Skitter", "Skitter", "Skitter");
                Wave(100f, "Hackling", "Hackling", "Hackling", "Skitter", "Skitter");
                Wave(160f, "Chromejaw", "Chromejaw", "Skitter", "Skitter", "Skitter");
                Wave(220f, "Burner", "Burner", "Chromejaw", "Chromejaw", "Strider", "Strider");
                Wave(280f, "MortarScarab", "MortarScarab", "BulwarkTick", "BulwarkTick", "Skitter", "Skitter", "Skitter", "Skitter");
                Wave(330f, "CrusherTarantula", "Chromejaw", "Chromejaw", "Chromejaw", "Burner", "Burner", "Burner");
                break;
            case 2:
                Add(() => $"Hold 3 outposts at once  ({Outposts()}/3)", () => Outposts() >= 3);
                countdown = () => $"Time left {Clock(Mathf.Max(0f, 720f - Elapsed))}";
                failure = () => HQ() == null ? $"Your {hq} was destroyed." : Elapsed > 720f ? "Time ran out." : null;
                break;
            case 3:
                Add(() => "Destroy the enemy Core Nexus", () => Elapsed > 10f && EnemyHQ() == null);
                break;
            case 4:
                Add(() => "Build a Shield Projector", () => Built("ShieldProjector"));
                Add(() => $"Build 2 Sentinel Turrets  ({Count("SentinelTurret")}/2)", () => Count("SentinelTurret") >= 2);
                Add(() => $"Hold for five minutes  {Clock(Mathf.Max(0f, 300f - Elapsed))}", () => Elapsed >= 300f);
                Wave(70f, "Skitter", "Skitter", "Skitter", "Hackling", "Hackling");
                Wave(130f, "Chromejaw", "Chromejaw", "Skitter", "Skitter", "Burner");
                Wave(190f, "Burner", "Burner", "Strider", "Strider", "Hackling", "Hackling", "Hackling");
                Wave(250f, "CrusherTarantula", "Chromejaw", "Chromejaw", "MortarScarab", "Skitter", "Skitter", "Skitter");
                break;
            case 5:
                Add(() => $"Destroy the enemy {Faction.HqName(1)}", () => Elapsed > 10f && EnemyHQ() == null);
                break;
        }
        BuildPanel(m);
        Alerts.Show(m.name + ": " + m.tagline, Alerts.Info, 6f);
    }

    float Elapsed => Time.time - started;

    void Add(Func<string> label, Func<bool> done) => goals.Add(new Goal { label = label, done = done });
    void Wave(float at, params string[] units) => waves.Add((at, units));

    static string Clock(float s) => $"{(int)s / 60}:{(int)s % 60:00}";

    static Structure HQ() => Structure.All.Find(s => s != null && s.isHQ && s.Health.team == 0 && !s.Health.IsDead);
    static Structure EnemyHQ() => Structure.All.Find(s => s != null && s.isHQ && s.Health.team == 1 && !s.Health.IsDead);
    static int Count(string data) => Structure.All.FindAll(s => s != null && s.Health.team == 0 && s.data != null && s.data.name == data && !s.GetComponent<Construction>() && !s.Health.IsDead).Count;
    static bool Built(string data) => Structure.All.Exists(s => s != null && s.Health.team == 0 && s.data != null && s.data.name == data && !s.GetComponent<Construction>());
    static int Earned() => MatchStats.Values[(int)MatchStats.Kind.Earned];

    static int Harvesters()
    {
        int n = 0;
        foreach (var h in Health.All) if (h != null && h.team == 0 && !h.IsDead && h.TryGetComponent(out Harvester _)) n++;
        return n;
    }

    static int Outposts()
    {
        int n = 0;
        foreach (var o in FindObjectsByType<OutpostCapture>(FindObjectsSortMode.None)) if (o.ownerTeam == 0) n++;
        return n;
    }

    void Update()
    {
        if (finished || match == null) return;
        if (!match.IntroDone) { started = Time.time; return; }

        while (nextWave < waves.Count && Elapsed >= waves[nextWave].at) SpawnWave(waves[nextWave++].units);

        bool all = true;
        foreach (var g in goals)
        {
            bool done = g.done();
            all &= done;
            g.text.text = (done ? "<color=#60FF90>■</color>  " : "<color=#6A7690>□</color>  ") + g.label();
            g.text.color = done ? new Color(0.7f, 1f, 0.8f) : Color.white;
        }
        timer.text = nextWave < waves.Count ? $"Next wave in {Clock(waves[nextWave].at - Elapsed)}" : countdown?.Invoke() ?? "";

        if (failure() is string why) Finish(false, why);
        else if (all) Finish(true, All[Current].name + " complete.");
    }

    void Finish(bool won, string detail)
    {
        finished = true;
        panel.gameObject.SetActive(false);
        if (won)
        {
            PlayerPrefs.SetInt("mission.done." + Current, 1);
            PlayerPrefs.Save();
            if (Current + 1 < All.Length && All[Current + 1].faction == All[Current].faction) detail += $"  {All[Current + 1].name} is now unlocked.";
        }
        Sfx.Stinger(won);
        match.Finish(won ? "MISSION COMPLETE" : "MISSION FAILED", detail);
    }

    void SpawnWave(string[] units)
    {
        var hq = HQ();
        if (hq == null) return;
        Vector3 home = hq.transform.position;
        Vector3 dir = Quaternion.Euler(0f, UnityEngine.Random.Range(-35f, 35f), 0f) * (-home).normalized;
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
        Vector3 at = home + dir * 110f;
        for (int i = 0; i < units.Length; i++)
        {
            var prefab = PrefabList.Find(units[i]);
            if (prefab == null) { Debug.LogWarning("[Mission] no prefab " + units[i]); continue; }
            Vector3 p = at + Quaternion.Euler(0f, i * 137f, 0f) * Vector3.forward * (2f + i * 1.2f);
            if (NavMesh.SamplePosition(p, out var hit, 15f, NavMesh.AllAreas)) p = hit.position;
            var go = MatchManager.SpawnFor(Raiders, prefab, p, Quaternion.LookRotation(home - p));
            if (go.TryGetComponent(out Attacker a)) a.AttackMove(home);
        }
        Alerts.ShowAt($"Enemy wave incoming: {units.Length} units", Alerts.Bad, at);
    }

    void BuildPanel(Info m)
    {
        var canvas = new GameObject("MissionCanvas", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvas.transform.SetParent(transform, false);

        float height = 92f + goals.Count * 32f;
        panel = new GameObject("MissionPanel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        panel.SetParent(canvas.transform, false);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 0.5f);
        panel.anchoredPosition = new Vector2(16f, 80f);
        panel.sizeDelta = new Vector2(400f, height);
        var img = panel.GetComponent<Image>();
        img.color = new Color(0.02f, 0.03f, 0.07f, 1f);
        img.raycastTarget = false;
        HoloSkin.Back(panel, "Panel_Frame");
        HoloSkin.FX(panel, HoloUI.Cyan, intro: true, strength: 0.5f);

        title = Label(m.name, 22, true, Alerts.Info, 14f);
        Label("OBJECTIVES", 14, true, new Color(0.6f, 0.68f, 0.8f), 44f);
        for (int i = 0; i < goals.Count; i++) goals[i].text = Label("", 18, false, Color.white, 66f + i * 32f);
        timer = Label("", 16, false, new Color(1f, 0.55f, 0.5f), height - 30f);
    }

    Text Label(string text, int size, bool bold, Color c, float y)
    {
        var t = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
        var rt = t.rectTransform;
        rt.SetParent(panel, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20f, -y);
        rt.sizeDelta = new Vector2(370f, size + 10f);
        t.font = bold ? HoloSkin.BoldFont : HoloSkin.Font;
        t.fontSize = size;
        t.color = c;
        t.text = text;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }
}
