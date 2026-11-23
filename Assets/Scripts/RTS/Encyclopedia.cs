using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Encyclopedia : MonoBehaviour
{
    const string Cyan = "#70FFFF";
    static readonly string[] TabNames = { "UNITS", "BUILDINGS", "RESEARCH" };

    public static bool IsOpen { get; private set; }

    class Entry
    {
        public string name, group, head, body;
        public Sprite icon;
        public int rank, cost;
    }

    readonly List<Entry>[] pages = { new(), new(), new() };
    readonly Button[] tabs = new Button[3];
    readonly List<Button> rows = new();
    RectTransform content;
    ScrollRect scroll;
    Image icon;
    Text head, body;

    public static void Open(Transform parent)
    {
        var shade = new GameObject("Encyclopedia", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        shade.SetParent(parent, false);
        SettingsPanel.Stretch(shade);
        shade.GetComponent<Image>().color = new Color(0f, 0.01f, 0.04f, 0.9f);
        HoloSkin.Layer(shade, HoloSkin.LayerPopup + 3);
        shade.gameObject.AddComponent<Encyclopedia>().Build(shade);
    }

    void Awake() => IsOpen = true;
    void OnDestroy() => IsOpen = false;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) Destroy(gameObject);
    }

    void Build(RectTransform shade)
    {
        Collect();
        var win = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        win.SetParent(shade, false);
        win.sizeDelta = new Vector2(1320f, 780f);
        win.GetComponent<Image>().color = new Color(0.01f, 0.015f, 0.04f, 1f);
        HoloSkin.Modal(win);
        var inner = new GameObject("Inner", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        inner.SetParent(win, false);
        inner.sizeDelta = win.sizeDelta - new Vector2(16f, 16f);
        inner.GetComponent<Image>().color = new Color(0.01f, 0.015f, 0.04f, 1f);
        inner.GetComponent<Image>().raycastTarget = false;
        inner.SetSiblingIndex(1);
        HoloSkin.Title(Label(win, "DATABASE", new Vector2(0f, 345f), 36, 1000f, true, TextAnchor.MiddleCenter), 1120f);

        for (int i = 0; i < tabs.Length; i++)
        {
            int page = i;
            tabs[i] = SettingsPanel.Button(win, TabNames[i], new Vector2(-445f + (i - 1) * 134f, 278f), new Vector2(128f, 44f));
            HoloSkin.Button(tabs[i], HoloUI.ButtonStyle.Small);
            var t = tabs[i].GetComponentInChildren<Text>();
            t.fontSize = 15;
            tabs[i].onClick.AddListener(() => ShowPage(page));
        }

        BuildList(win);

        icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        icon.transform.SetParent(win, false);
        icon.rectTransform.anchoredPosition = new Vector2(-130f, 190f);
        icon.rectTransform.sizeDelta = new Vector2(150f, 150f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        HoloSkin.Brackets(icon.rectTransform, HoloUI.Cyan);

        head = Label(win, "", new Vector2(285f, 190f), 30, 650f, true, TextAnchor.MiddleLeft);
        head.rectTransform.sizeDelta = new Vector2(650f, 130f);
        head.horizontalOverflow = HorizontalWrapMode.Wrap;

        body = Label(win, "", new Vector2(205f, -130f), 21, 850f, false, TextAnchor.UpperLeft);
        body.rectTransform.sizeDelta = new Vector2(850f, 470f);
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.verticalOverflow = VerticalWrapMode.Truncate;
        body.lineSpacing = 1.12f;

        SettingsPanel.Button(win, "BACK", new Vector2(-445f, -340f), new Vector2(200f, 50f)).onClick.AddListener(() => Destroy(gameObject));
        ShowPage(0);
    }

    void BuildList(RectTransform win)
    {
        var view = new GameObject("List", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect)).GetComponent<RectTransform>();
        view.SetParent(win, false);
        view.anchoredPosition = new Vector2(-445f, -25f);
        view.sizeDelta = new Vector2(400f, 545f);
        view.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);

        content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
        content.SetParent(view, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(8, 12, 6, 6);
        layout.childControlWidth = layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll = view.GetComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = view;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
    }

    void ShowPage(int page)
    {
        for (int i = 0; i < tabs.Length; i++) HoloSkin.SetOn(tabs[i], i == page);
        foreach (Transform c in content) Destroy(c.gameObject);
        rows.Clear();
        var list = pages[page];
        for (int i = 0; i < list.Count; i++)
        {
            int index = i;
            var e = list[i];
            var b = SettingsPanel.Button(content, "", Vector2.zero, new Vector2(370f, 66f));
            HoloSkin.Row(b);
            b.gameObject.AddComponent<LayoutElement>().preferredHeight = 66f;
            var label = b.GetComponentInChildren<Text>();
            label.supportRichText = true;
            label.alignment = TextAnchor.MiddleLeft;
            label.fontSize = 19;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(16f, 2f);
            label.rectTransform.offsetMax = new Vector2(-70f, -2f);
            label.text = $"<b>{e.name}</b>\n<size=14>{e.group}</size>";
            var pic = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            pic.transform.SetParent(b.transform, false);
            pic.rectTransform.anchorMin = pic.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            pic.rectTransform.anchoredPosition = new Vector2(-37f, 0f);
            pic.rectTransform.sizeDelta = new Vector2(52f, 52f);
            pic.sprite = e.icon;
            pic.enabled = e.icon != null;
            pic.preserveAspect = true;
            pic.raycastTarget = false;
            b.onClick.AddListener(() => Pick(list, index));
            rows.Add(b);
        }
        scroll.verticalNormalizedPosition = 1f;
        if (list.Count > 0) Pick(list, 0);
    }

    void Pick(List<Entry> list, int index)
    {
        for (int i = 0; i < rows.Count; i++) HoloSkin.SetOn(rows[i], i == index);
        var e = list[index];
        icon.sprite = e.icon;
        icon.enabled = e.icon != null;
        head.text = e.head;
        body.text = e.body;
    }

    void Collect()
    {
        var prefabs = Resources.Load<PrefabList>("PrefabList").prefabs;
        var units = new List<SelectableUnit>();
        var trainedAt = new Dictionary<UnitData, string>();
        var seen = new HashSet<Object>();
        var labs = new List<(string name, ResearchData r)>();
        var makers = new List<(BuildingData data, Building factory)>();

        foreach (var p in prefabs)
        {
            if (p == null) continue;
            if (p.TryGetComponent(out SelectableUnit u) && u.Data != null && seen.Add(u.Data)) units.Add(u);
            if (!p.TryGetComponent(out Structure s)) continue;
            if (s.data == null)
            {
                if (s.isHQ && seen.Add(s)) pages[1].Add(CoreEntry(s));
                continue;
            }
            p.TryGetComponent(out Building f);
            if (!seen.Add(s.data)) continue;
            makers.Add((s.data, f));
            if (f == null) continue;
            foreach (var t in f.Trains) if (t != null && t.Data != null) trainedAt[t.Data] = s.data.displayName;
            foreach (var r in f.Researches) if (r != null && seen.Add(r)) labs.Add((s.data.displayName, r));
        }

        foreach (var u in units) pages[0].Add(UnitEntry(u, units, trainedAt));
        foreach (var (d, f) in makers) pages[1].Add(BuildingEntry(d, f));
        foreach (var (lab, r) in labs) pages[2].Add(ResearchEntry(r, lab));
        foreach (var page in pages) page.Sort((a, b) => a.rank != b.rank ? a.rank.CompareTo(b.rank) : a.cost != b.cost ? a.cost.CompareTo(b.cost) : string.CompareOrdinal(a.name, b.name));
    }

    static Entry Make(string name, string group, Sprite sprite, int rank, int cost, string tip, string extra)
    {
        int cut = tip.IndexOf('\n');
        return new Entry { name = name, group = group, icon = sprite, rank = rank, cost = cost, head = tip.Substring(0, cut), body = tip.Substring(cut + 1) + extra };
    }

    static bool CanHit(UnitData attacker, SelectableUnit target) =>
        attacker.damage > 0f && (target.GetComponent<Flyer>() != null ? attacker.hitsAir : attacker.hitsGround);

    static string Names(List<(float score, string name)> list)
    {
        if (list.Count == 0) return "None in particular";
        list.Sort((a, b) => b.score.CompareTo(a.score));
        var names = new List<string>();
        for (int i = 0; i < list.Count && i < 5; i++) names.Add(list[i].name);
        return string.Join(", ", names);
    }

    static string Section(string title, string text) => $"\n\n<color={Cyan}><b>{title}</b></color>\n{text}";

    static Entry UnitEntry(SelectableUnit unit, List<SelectableUnit> all, Dictionary<UnitData, string> trainedAt)
    {
        var d = unit.Data;
        var strong = new List<(float, string)>();
        var weak = new List<(float, string)>();
        foreach (var other in all)
        {
            var o = other.Data;
            if (o == d) continue;
            float mine = DamageTable.Factor(d.weapon, o.armor), theirs = DamageTable.Factor(o.weapon, d.armor);
            if (o.role != UnitData.Role.Worker && CanHit(d, other) && mine > 1.01f) strong.Add((mine * o.cost, o.displayName));
            if (CanHit(o, unit) && theirs > 1.01f) weak.Add((theirs * o.cost, o.displayName));
        }
        string extra = trainedAt.TryGetValue(d, out var at) ? Section("TRAINED AT", at) : "";
        if (d.damage > 0f) extra += Section("STRONG AGAINST", Names(strong));
        extra += Section("COUNTERED BY", Names(weak));
        return Make(d.displayName, $"{d.role}  ·  ${d.cost}", d.icon, (int)d.role, d.cost, Tooltip.For(d), extra);
    }

    static Entry BuildingEntry(BuildingData d, Building factory)
    {
        var needs = new List<string>();
        if (d.coreLevel > 1) needs.Add(d.CoreName);
        if (d.requires != null) foreach (var r in d.requires) if (r != null) needs.Add(r.displayName);
        string extra = Section("REQUIRES", needs.Count > 0 ? string.Join(", ", needs) : "Nothing");
        if (factory != null)
        {
            var trains = new List<string>();
            foreach (var t in factory.Trains) if (t != null && t.Data != null) trains.Add(t.Data.displayName);
            if (trains.Count > 0) extra += Section("TRAINS", string.Join(", ", trains));
            var research = new List<string>();
            foreach (var r in factory.Researches) if (r != null) research.Add(r.displayName);
            if (research.Count > 0) extra += Section("RESEARCH", string.Join(", ", research));
        }
        return Make(d.displayName, $"${d.cost}", d.icon, needs.Count, d.cost, Tooltip.For(d), extra);
    }

    static Entry CoreEntry(Structure s)
    {
        float hp = s.TryGetComponent(out Health h) ? h.MaxHealth : 0f;
        string tip = $"<color={Cyan}><b>Core Nexus</b></color>   Headquarters\nYour command centre. It sets your build radius, accepts ore at a slower rate than a refinery, and its Mk II and Mk III upgrades unlock higher research tiers and elite units. Lose every building and you lose the match.\nHP {hp:0}  ·  Power +{s.power}";
        return Make("Core Nexus", "Headquarters", null, -1, 0, tip, Section("REQUIRES", "Nothing. Every base starts with one and it cannot be built or sold."));
    }

    static Entry ResearchEntry(ResearchData r, string lab)
    {
        string core = r.tier == 1 ? "" : $"Core Nexus Mk {(r.tier == 2 ? "II" : "III")}";
        string needs = r.requires != null ? r.requires.displayName + (core != "" ? ", " + core : "") : core != "" ? core : "Nothing";
        return Make(r.displayName, $"{r.branch}  ·  Tier {r.tier}", r.icon, (int)r.branch * 10 + r.tier, r.cost, Tooltip.For(r), Section("RESEARCHED AT", lab) + Section("REQUIRES", needs));
    }

    static Text Label(Transform parent, string text, Vector2 pos, int size, float width, bool bold, TextAnchor align)
    {
        var t = SettingsPanel.Label(parent, text, pos, size, align, width);
        t.font = bold ? HoloSkin.BoldFont : HoloSkin.Font;
        t.supportRichText = true;
        return t;
    }
}
