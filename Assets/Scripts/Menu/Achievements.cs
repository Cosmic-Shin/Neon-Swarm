using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class Achievements
{
    public class Def
    {
        public string id, name, unit;
        public int[] tiers;
        public bool lowerIsBetter;
        public Func<PlayerProfile, int> value;
        public string Goal(int t) => lowerIsBetter ? $"Win in under {tiers[t] / 60} min" : $"{tiers[t]:N0} {unit}";
    }

    public static readonly string[] TierNames = { "LOCKED", "BRONZE", "SILVER", "GOLD", "NEON" };
    public static readonly Color[] TierColors =
    {
        new(0.35f, 0.38f, 0.45f), new(0.86f, 0.52f, 0.26f), new(0.78f, 0.86f, 0.95f), new(1f, 0.8f, 0.25f), new(0.3f, 1f, 1f),
    };

    public static readonly Def[] All =
    {
        new() { id = "kills", name = "EXTERMINATOR", unit = "kills", tiers = new[] { 100, 500, 1000, 5000 }, value = p => p.Total(MatchStats.Kind.Kills) },
        new() { id = "wins", name = "CONQUEROR", unit = "wins", tiers = new[] { 1, 10, 50, 150 }, value = p => p.all.wins },
        new() { id = "swarm", name = "SWARM LORD", unit = "units built", tiers = new[] { 100, 500, 2000, 6000 }, value = p => p.Total(MatchStats.Kind.UnitsBuilt) },
        new() { id = "build", name = "ARCHITECT", unit = "buildings built", tiers = new[] { 25, 100, 300, 1000 }, value = p => p.Total(MatchStats.Kind.BuildingsBuilt) },
        new() { id = "credits", name = "DATA TYCOON", unit = "credits earned", tiers = new[] { 100000, 1000000, 5000000, 20000000 }, value = p => p.Total(MatchStats.Kind.Earned) },
        new() { id = "research", name = "MAD SCIENTIST", unit = "research done", tiers = new[] { 10, 50, 150, 400 }, value = p => p.Total(MatchStats.Kind.Research) },
        new() { id = "hard", name = "MACHINE BREAKER", unit = "wins vs Hard AI", tiers = new[] { 1, 10, 30, 100 }, value = p => p.vsAI != null && p.vsAI.Length > 2 ? p.vsAI[2].wins : 0 },
        new() { id = "online", name = "NETRUNNER", unit = "multiplayer wins", tiers = new[] { 1, 10, 50, 150 }, value = p => p.online.wins },
        new() { id = "streak", name = "UNSTOPPABLE", unit = "wins in a row", tiers = new[] { 3, 5, 10, 20 }, value = p => p.bestStreak },
        new() { id = "speed", name = "BLITZ", unit = "", lowerIsBetter = true, tiers = new[] { 1200, 900, 600, 420 }, value = p => p.fastestWin > 0f ? Mathf.CeilToInt(p.fastestWin) : 0 },
        new() { id = "hours", name = "VETERAN", unit = "hours played", tiers = new[] { 1, 10, 50, 200 }, value = p => (int)(p.secondsInGame / 3600f) },
        new() { id = "killer", name = "WAR MACHINE", unit = "kills per match (best avg)", tiers = new[] { 10, 25, 50, 100 }, value = p => p.all.games >= 3 ? p.Total(MatchStats.Kind.Kills) / p.all.games : 0 },
    };

    public static Def Find(string id) => Array.Find(All, d => d.id == id);

    public static int Tier(Def d, PlayerProfile p)
    {
        if (d == null || p == null) return 0;
        int v = d.value(p), t = 0;
        for (int i = 0; i < d.tiers.Length; i++)
            if (d.lowerIsBetter ? v > 0 && v <= d.tiers[i] : v >= d.tiers[i]) t = i + 1;
        return t;
    }

    public static float Progress(Def d, PlayerProfile p, out string text)
    {
        int t = Tier(d, p), v = d.value(p);
        if (t >= d.tiers.Length) { text = "MAXED"; return 1f; }
        int goal = d.tiers[t];
        if (d.lowerIsBetter)
        {
            text = v > 0 ? $"best {v / 60}:{v % 60:00} / {goal / 60}:00" : "no wins yet";
            return v > 0 ? Mathf.Clamp01((float)goal / v) : 0f;
        }
        text = $"{Short(v)} / {Short(goal)}";
        return Mathf.Clamp01((float)v / goal);
    }

    public static string Short(int v) => v >= 1000000 ? $"{v / 1000000f:0.#}M" : v >= 10000 ? $"{v / 1000f:0.#}k" : v.ToString("N0");

    public static int Points(PlayerProfile p) { int s = 0; foreach (var d in All) s += Tier(d, p); return s; }

    public static List<(Def def, int tier)> Snapshot(PlayerProfile p)
    {
        var list = new List<(Def, int)>();
        foreach (var d in All) list.Add((d, Tier(d, p)));
        return list;
    }
}

public class Badge : MonoBehaviour
{
    static readonly Dictionary<string, Sprite> art = new();
    Image frame, icon;
    int tier;
    float phase;

    public static Sprite Art(string name)
    {
        if (art.TryGetValue(name, out var s) && s != null) return s;
        return art[name] = Resources.Load<Sprite>("Badges/" + name);
    }

    public static Badge Make(Transform parent, Vector2 pos, float size)
    {
        var root = new GameObject("Badge", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchoredPosition = pos;
        root.sizeDelta = Vector2.one * size;
        var b = root.gameObject.AddComponent<Badge>();
        b.frame = Layer(root, null, size * 1.6f);
        b.icon = Layer(root, null, size * 0.62f);
        b.icon.rectTransform.anchoredPosition = new Vector2(0f, size * 0.1f);
        b.icon.preserveAspect = true;
        b.phase = UnityEngine.Random.value * 10f;
        return b;
    }

    static Image Layer(Transform parent, Sprite s, float size)
    {
        var img = new GameObject("Layer", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.rectTransform.sizeDelta = Vector2.one * size;
        img.sprite = s;
        img.raycastTarget = false;
        return img;
    }

    public void Show(Achievements.Def d, int t)
    {
        tier = t;
        Color c = Achievements.TierColors[Mathf.Clamp(t, 0, 4)];
        frame.sprite = Art("frame_" + Mathf.Clamp(t, 0, 4));
        frame.color = Color.white;
        icon.sprite = d != null ? Art("icon_" + d.id) : null;
        icon.enabled = icon.sprite != null;
        icon.color = t == 0 ? new Color(0.6f, 0.62f, 0.7f, 0.4f) : Color.Lerp(Color.white, c, 0.2f);
    }

    void Update()
    {
        if (tier < 4) return;
        float t = Time.unscaledTime + phase;
        Color neon = Color.Lerp(new Color(0.3f, 1f, 1f), new Color(1f, 0.3f, 0.95f), 0.5f + 0.5f * Mathf.Sin(t * 1.6f));
        icon.color = Color.Lerp(Color.white, neon, 0.25f);
    }
}

public class AchievementToast : MonoBehaviour
{
    static readonly Queue<(Achievements.Def def, int tier)> pending = new();
    static AchievementToast active;

    public static void Unlocked(Achievements.Def d, int tier)
    {
        pending.Enqueue((d, tier));
        if (active == null) Next();
    }

    static void Next()
    {
        if (pending.Count == 0) return;
        var (d, t) = pending.Dequeue();
        var go = new GameObject("AchievementToast", typeof(Canvas), typeof(CanvasScaler));
        DontDestroyOnLoad(go);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30500;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        active = go.AddComponent<AchievementToast>();
        active.Build(d, t);
    }

    RectTransform box;
    float age;

    void Build(Achievements.Def d, int t)
    {
        box = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        box.SetParent(transform, false);
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 1f);
        box.sizeDelta = new Vector2(560f, 130f);
        box.anchoredPosition = new Vector2(0f, 200f);
        box.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.08f, 0.95f);
        HoloSkin.Modal(box);
        Badge.Make(box, new Vector2(-205f, -4f), 74f).Show(d, t);
        Color c = Achievements.TierColors[t];
        Text(box, "ACHIEVEMENT UNLOCKED", new Vector2(70f, 30f), 16, new Color(0.55f, 0.6f, 0.75f));
        Text(box, $"{d.name}  {new[] { "", "I", "II", "III", "IV" }[t]}", new Vector2(70f, 2f), 30, c, true);
        Text(box, $"{Achievements.TierNames[t]}  ·  {d.Goal(t - 1)}", new Vector2(70f, -30f), 17, Color.white);
        Sfx.Play(Snd.Promote, 0.8f);
    }

    static void Text(Transform parent, string s, Vector2 pos, int size, Color c, bool bold = false)
    {
        var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        t.transform.SetParent(parent, false);
        t.rectTransform.anchoredPosition = pos;
        t.rectTransform.sizeDelta = new Vector2(400f, 36f);
        t.text = s;
        t.supportRichText = true;
        t.font = bold ? HoloSkin.BoldFont : HoloSkin.Font;
        t.fontSize = size;
        t.color = c;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
    }

    void Update()
    {
        age += Time.unscaledDeltaTime;
        float inT = Mathf.Clamp01(age / 0.35f), outT = Mathf.Clamp01((age - 3.6f) / 0.35f);
        float y = Mathf.Lerp(200f, -30f, Mathf.SmoothStep(0f, 1f, inT)) + Mathf.Lerp(0f, 230f, Mathf.SmoothStep(0f, 1f, outT));
        box.anchoredPosition = new Vector2(0f, y);
        if (age >= 4f) { Destroy(gameObject); active = null; Next(); }
    }
}
