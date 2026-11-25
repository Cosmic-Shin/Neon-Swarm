using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SpectatorPanel : MonoBehaviour
{
    const float Refresh = 0.5f, Window = 60f, RowHeight = 104f, Width = 350f;
    const int MaxJobs = 7;

    class Side
    {
        public int team, slot;
        public string name;
        public Color color;
        public RectTransform row;
        public Image strip;
        public Text title, money, army;
        public Image[] jobs = new Image[MaxJobs], jobBars = new Image[MaxJobs];
        public readonly Queue<(float time, int earned)> history = new();
        public Vector3? home;
    }

    MatchManager match;
    RectTransform panel;
    readonly List<Side> sides = new();
    float next;

    public void Build(MatchManager owner, Transform canvas)
    {
        match = owner;
        panel = new GameObject("SpectatorPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
        panel.SetParent(canvas, false);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 0.5f);
        panel.anchoredPosition = new Vector2(16f, 40f);
        panel.sizeDelta = new Vector2(Width, 0f);
        HoloSkin.Modal(panel);
        foreach (Transform c in panel) c.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 6f;
        layout.childControlHeight = layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        HoloSkin.Layer(panel, 15);

        var head = Label(panel, "SPECTATOR  ·  click a player to jump there", 15, new Color(0.6f, 0.68f, 0.8f), true);
        head.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;

        foreach (var (team, slot, name, color) in Teams()) sides.Add(MakeRow(team, slot, name, color));
        next = 0f;
    }

    IEnumerable<(int team, int slot, string name, Color color)> Teams()
    {
        var room = RoomState.Instance;
        if (Net.Online && room != null)
        {
            for (int slot = 0; slot < RoomService.MaxPlayers; slot++)
                if (room.Slots[slot] != Fusion.PlayerRef.None || room.BotLevel(slot) > 0)
                    yield return (Net.ToTeam(slot), slot, room.NameOf(slot), TeamMember.ColorOf(slot));
            yield break;
        }
        yield return (0, -1, "You", TeamMember.ColorOf(0));
        foreach (var ai in Skirmish.Opponents) yield return (ai.team, -1, ai.Name, TeamMember.ColorOf(ai.team));
    }

    Side MakeRow(int team, int slot, string name, Color color)
    {
        var s = new Side { team = team, slot = slot, name = name, color = color };
        var btn = new GameObject("Side " + name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement)).GetComponent<Button>();
        s.row = (RectTransform)btn.transform;
        s.row.SetParent(panel, false);
        btn.GetComponent<LayoutElement>().preferredHeight = RowHeight;
        var bg = btn.GetComponent<Image>();
        bg.color = new Color(0.07f, 0.08f, 0.12f, 1f);
        btn.targetGraphic = bg;
        var colors = btn.colors;
        colors.highlightedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() => { if (s.home is Vector3 p) CameraPan.LookAt(p); });

        s.strip = Make<Image>("Strip", s.row);
        s.strip.rectTransform.anchorMin = new Vector2(0f, 0f);
        s.strip.rectTransform.anchorMax = new Vector2(0f, 1f);
        s.strip.rectTransform.pivot = new Vector2(0f, 0.5f);
        s.strip.rectTransform.sizeDelta = new Vector2(5f, 0f);
        s.strip.color = color;

        s.title = Place(Label(s.row, "", 20, color, true), 6f);
        s.money = Place(Label(s.row, "", 16, Color.white, false), 30f);
        s.army = Place(Label(s.row, "", 16, new Color(0.75f, 0.82f, 0.95f), false), 52f);

        for (int i = 0; i < MaxJobs; i++)
        {
            var icon = Make<Image>("Job", s.row);
            var rt = icon.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(14f + i * 42f, -74f);
            rt.sizeDelta = new Vector2(24f, 24f);
            icon.preserveAspect = true;
            var bar = Make<Image>("Bar", rt);
            bar.rectTransform.anchorMin = new Vector2(0f, 0f);
            bar.rectTransform.anchorMax = new Vector2(1f, 0f);
            bar.rectTransform.pivot = new Vector2(0f, 1f);
            bar.rectTransform.sizeDelta = new Vector2(0f, 3f);
            bar.rectTransform.anchoredPosition = new Vector2(0f, -1f);
            bar.color = color;
            s.jobs[i] = icon;
            s.jobBars[i] = bar;
            icon.gameObject.SetActive(false);
        }
        return s;
    }

    void Update()
    {
        if (panel == null) return;
        bool show = match != null && match.Spectating && !Hud.HelpOpen;
        if (panel.gameObject.activeSelf != show) panel.gameObject.SetActive(show);
        if (!show || Time.unscaledTime < next) return;
        next = Time.unscaledTime + Refresh;
        foreach (var s in sides) Fill(s);
    }

    void Fill(Side s)
    {
        int army = 0, units = 0, workers = 0, buildings = 0;
        foreach (var h in Health.All)
        {
            if (h == null || h.IsDead || h.team != s.team) continue;
            if (h.TryGetComponent(out Harvester _)) workers++;
            else if (h.TryGetComponent(out SelectableUnit u)) { units++; army += u.Cost; }
        }

        int jobs = 0;
        s.home = null;
        foreach (var st in Structure.All)
        {
            if (st == null || st.Health == null || st.Health.team != s.team) continue;
            buildings++;
            if (st.isHQ || s.home == null) s.home = st.transform.position;
            if (jobs < MaxJobs && st.TryGetComponent(out Building b) && b.Job >= 0 && b.Job < b.OptionCount)
            {
                s.jobs[jobs].sprite = b.IconOf(b.Job);
                s.jobBars[jobs].rectTransform.anchorMax = new Vector2(Mathf.Clamp01(b.JobProgress), 0f);
                s.jobs[jobs].gameObject.SetActive(true);
                jobs++;
            }
        }
        for (int i = jobs; i < MaxJobs; i++) s.jobs[i].gameObject.SetActive(false);

        bool alive = buildings > 0;
        s.color = TeamMember.ColorOf(s.slot >= 0 ? s.slot : s.team);
        s.title.color = s.color;
        foreach (var jb in s.jobBars) jb.color = s.color;
        s.title.text = s.name + Faction.Tag(s.team) + (alive ? "" : "  <color=#FF6070><size=15>DESTROYED</size></color>");
        s.strip.color = alive ? s.color : s.color * 0.35f;

        var (credits, earned) = Money(s);
        float now = Time.time;
        if (earned >= 0)
        {
            s.history.Enqueue((now, earned));
            while (s.history.Count > 1 && now - s.history.Peek().time > Window) s.history.Dequeue();
        }
        var first = s.history.Count > 0 ? s.history.Peek() : (time: now, earned);
        float span = Mathf.Max(10f, now - first.time);
        int perMin = earned >= 0 ? Mathf.RoundToInt((earned - first.earned) * 60f / span) : -1;

        s.money.text = (credits >= 0 ? $"<color=#FFD24A>${credits:N0}</color>" : "<color=#6A7690>$ ?</color>")
            + (perMin >= 0 ? $"   <color=#60FF90>+${perMin:N0}/min</color>" : "")
            + $"   <color=#6A7690>{buildings} bldg</color>";
        s.army.text = $"ARMY ${army:N0}  <color=#6A7690>({units} units)</color>   WORKERS {workers}" + (jobs == 0 && alive ? "   <color=#6A7690>idle</color>" : "");
    }

    (int credits, int earned) Money(Side s)
    {
        if (!Net.Online) return (TeamEconomy.Credits(s.team), s.team == 0 ? MatchStats.Values[(int)MatchStats.Kind.Earned] : TeamEconomy.Earned(s.team));
        var room = RoomState.Instance;
        if (room == null || s.slot < 0) return (-1, -1);
        if (s.slot == Net.LocalSlot && !Net.Watching) return (ResourceBank.GetOrCreate().Credits, MatchStats.Values[(int)MatchStats.Kind.Earned]);
        var rp = RoomPlayer.Of(room.Slots[s.slot]);
        if (rp != null) return (rp.Credits, rp.Stats[(int)MatchStats.Kind.Earned]);
        return room.HasStateAuthority ? (TeamEconomy.Credits(s.team), TeamEconomy.Earned(s.team)) : (-1, -1);
    }

    static Text Place(Text t, float y)
    {
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(14f, 0f);
        rt.offsetMax = new Vector2(-6f, 0f);
        rt.anchoredPosition = new Vector2(14f, -y);
        rt.sizeDelta = new Vector2(-20f, t.fontSize + 6f);
        return t;
    }

    static Text Label(Transform parent, string text, int size, Color color, bool bold)
    {
        var t = Make<Text>("Text", parent);
        t.font = bold ? HoloSkin.BoldFont : HoloSkin.Font;
        t.fontSize = size;
        t.color = color;
        t.text = text;
        t.supportRichText = true;
        t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }

    static T Make<T>(string name, Transform parent) where T : Graphic
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(T)).GetComponent<T>();
        g.transform.SetParent(parent, false);
        g.raycastTarget = false;
        return g;
    }
}
