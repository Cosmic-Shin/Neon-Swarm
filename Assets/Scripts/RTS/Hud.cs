using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// In-game HUD, built in code on the Canvas (cloned from the BUILDINGS button for the neon look):
//  - bottom: selection panel (one unit: portrait, health, rank, counters, ability; several: a grid per type)
//    and command buttons (attack-move, patrol, stop, hold, formation, unload) with their hotkeys
//  - top-left: control groups 1-9, then everything being built / researched / upgraded (click to jump there)
//  - IDLE button next to BUILDINGS, the match clock offline, "F1 HOTKEYS" under the minimap and the F1 sheet
public class Hud : MonoBehaviour
{
    // Panel is opaque: the HDR neon behind would bleed through any alpha.
    static readonly Color Panel = new(0.02f, 0.03f, 0.07f, 1f), Cyan = new(0.44f, 1f, 1f), Grey = new(0.72f, 0.77f, 0.85f),
        Gold = new(1f, 0.82f, 0.29f), Pink = new(1f, 0.6f, 0.88f), BarBack = new(0.08f, 0.1f, 0.16f),
        High = new(0.2f, 1f, 1f), Mid = new(1f, 0.9f, 0.2f), Low = new(1f, 0.2f, 0.6f);

    public static bool HelpOpen => help != null && help.activeSelf;
    static GameObject help;

    GoodSelectionManager manager;
    Button template;
    Font font;
    Sprite frame;

    void Start()
    {
        manager = FindFirstObjectByType<GoodSelectionManager>();
        template = transform.Find("BuildingsButton").GetComponent<Button>();
        font = template.GetComponentInChildren<Text>().font;
        frame = template.transform.Find("Frame") is Transform f && f.TryGetComponent(out Image fi) ? fi.sprite : null;
        if (transform.Find("SelectedUnits") is Transform old) old.gameObject.SetActive(false); // replaced by the bottom panel

        BuildUnitPanel();
        BuildGroups();
        BuildProduction();
        BuildIdle();
        BuildHelp();
        clock = transform.Find("MatchTimer")?.GetComponentInChildren<Text>();
    }

    void LateUpdate()
    {
        UpdateUnitPanel();
        UpdateGroups();
        UpdateProduction();
        UpdateIdle();
        if (!Net.Online && clock != null) // offline: the online timer box shows the time played
        {
            clock.transform.parent.gameObject.SetActive(true);
            float t = Time.timeSinceLevelLoad;
            clock.text = $"{(int)t / 60:00}:{(int)t % 60:00}";
        }
        var kb = Chat.Keys;
        if (kb != null && kb.f1Key.wasPressedThisFrame) help.SetActive(!help.activeSelf);
        if (kb != null && kb.escapeKey.wasPressedThisFrame && help.activeSelf) help.SetActive(false);
    }

    Text clock;

    // ================= selection panel + commands =================

    RectTransform unitPanel, single, grid;
    Image portrait;
    Text nameText, roleText, hpText, rankText, statText, abilityText;
    RectTransform hpFill, abilityFill;
    GameObject abilityBar;
    readonly List<(Button b, Image icon, Text count, RectTransform hp, Image hpImg)> cells = new();
    Button attackBtn, patrolBtn, stopBtn, holdBtn, formBtn, unloadBtn;

    void BuildUnitPanel()
    {
        unitPanel = Box(transform, "UnitHud", new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(1180f, 180f), Panel, true);

        // One unit: a card.
        single = Box(unitPanel, "Single", new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(800f, 156f), null, false, new Vector2(0f, 0.5f));
        portrait = new GameObject("Portrait", typeof(RectTransform)).AddComponent<Image>();
        portrait.transform.SetParent(single, false);
        Place(portrait.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(150f, 150f), new Vector2(0f, 0.5f));
        portrait.preserveAspect = true;
        nameText = Label(single, "", 28, TextAnchor.UpperLeft, Cyan, new Vector2(170f, -6f), new Vector2(600f, 36f), FontStyle.Bold);
        roleText = Label(single, "", 17, TextAnchor.UpperLeft, Pink, new Vector2(170f, -42f), new Vector2(620f, 24f));
        hpFill = Bar(single, new Vector2(170f, -72f), new Vector2(340f, 16f));
        hpText = Label(single, "", 16, TextAnchor.UpperLeft, Grey, new Vector2(520f, -69f), new Vector2(260f, 22f));
        rankText = Label(single, "", 17, TextAnchor.UpperLeft, Gold, new Vector2(170f, -96f), new Vector2(620f, 24f));
        statText = Label(single, "", 17, TextAnchor.UpperLeft, Grey, new Vector2(170f, -120f), new Vector2(620f, 24f));
        abilityFill = Bar(single, new Vector2(520f, -100f), new Vector2(160f, 12f));
        abilityBar = abilityFill.parent.gameObject;
        abilityText = Label(single, "", 17, TextAnchor.UpperLeft, Gold, new Vector2(520f, -120f), new Vector2(280f, 24f), FontStyle.Bold);

        // Several: one cell per unit type.
        grid = Box(unitPanel, "Grid", new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(800f, 156f), null, false, new Vector2(0f, 0.5f));
        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(72f, 72f);
        layout.spacing = new Vector2(6f, 6f);

        // Orders.
        var cmd = Box(unitPanel, "Commands", new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(342f, 156f), null, false, new Vector2(1f, 0.5f));
        Vector2 size = new(108f, 74f);
        Button At(int col, int row, string label, System.Action a) => MakeButton(cmd, label, new Vector2(col * 117f, -row * 82f), size, a, new Vector2(0f, 1f));
        attackBtn = At(0, 0, "ATTACK\n<size=14>[A]</size>", manager.ArmAttackMove);
        patrolBtn = At(1, 0, "PATROL\n<size=14>[P]</size>", manager.ArmPatrol);
        stopBtn = At(2, 0, "STOP\n<size=14>[S]</size>", manager.StopOrder);
        holdBtn = At(0, 1, "HOLD\n<size=14>[H]</size>", manager.HoldOrder);
        formBtn = At(1, 1, "", manager.NextFormation);
        unloadBtn = At(2, 1, "UNLOAD\n<size=14>[U]</size>", () => { foreach (var t in Selected<Transport>()) t.Unload(); });
        Tip(attackBtn, "Attack-move: walk there and fight anything met on the way.\nShift: add to the unit's queue of orders.");
        Tip(patrolBtn, "Patrol: walk back and forth between here and the clicked spot, fighting anything met.");
        Tip(stopBtn, "Stop: drop every order.");
        Tip(holdBtn, "Hold position: never move, only shoot what comes in range.");
        Tip(formBtn, "Next formation: circle, line or box. Right-drag draws a line formation.");
        Tip(unloadBtn, "Wasp Transport: drop off everyone aboard.");
    }

    readonly List<SelectableUnit> alive = new();

    void UpdateUnitPanel()
    {
        alive.Clear();
        foreach (var u in manager.SelectedUnits)
            if (u != null && u.isActiveAndEnabled && u.TryGetComponent(out Health h) && h.enabled) alive.Add(u);
        bool show = alive.Count > 0 && !BuildingPlacer.MenuOpen && !BuildingPlacer.Placing;
        unitPanel.gameObject.SetActive(show);
        if (!show) return;

        single.gameObject.SetActive(alive.Count == 1);
        grid.gameObject.SetActive(alive.Count > 1);
        if (alive.Count == 1) Card(alive[0]); else Grid();

        Text(attackBtn).color = manager.AttackMoveArmed ? Gold : Cyan;
        Text(patrolBtn).color = manager.PatrolArmed ? Gold : Cyan;
        Text(formBtn).text = $"{manager.CurrentFormation.ToString().ToUpper()}\n<size=14>[F]</size>";
        bool wasp = false, cargo = false;
        foreach (var t in Selected<Transport>()) { wasp = true; cargo |= t.Carried > 0; }
        unloadBtn.gameObject.SetActive(wasp);
        unloadBtn.interactable = cargo;
    }

    void Card(SelectableUnit u)
    {
        var d = u.Data;
        var h = u.GetComponent<Health>();
        portrait.sprite = d.icon;
        nameText.text = d.displayName.ToUpper();
        roleText.text = $"{d.role}  ·  {d.armor} armour" + (d.damage > 0f ? $"  ·  {d.weapon} weapon" : "");
        SetBar(hpFill, h.Fraction, h.Fraction > 0.5f ? High : h.Fraction > 0.25f ? Mid : Low);
        hpText.text = $"{h.Current:0} / {h.MaxHealth:0}";
        rankText.text = u.TryGetComponent(out Veterancy v) ? $"★ {v.RankName.ToUpper()}  ·  {v.Kills} kills" : "ROOKIE  ·  0 kills";
        var agent = u.GetComponent<UnityEngine.AI.NavMeshAgent>();
        statText.text = d.damage > 0f
            ? $"DMG {Stats.Value(Stat.Damage, d.damage, u):0}  ·  RANGE {Stats.Value(Stat.Range, d.range, u):0}m  ·  SPEED {agent.speed:0}"
            : $"Unarmed  ·  SPEED {agent.speed:0}";

        // Ability / cargo, with a bar when it has one.
        string ability = "";
        float bar = -1f;
        if (u.TryGetComponent(out Charge c)) { ability = c.Charging ? "CHARGING!" : c.CooldownLeft > 0f ? $"CHARGE  {c.CooldownLeft:0}s" : "CHARGE READY"; bar = 1f - c.CooldownLeft / c.CooldownTotal; }
        else if (u.TryGetComponent(out Overheat o)) { ability = o.Ready ? $"HEAT  {o.Heat * 100f:0}%" : $"OVERHEATED  {o.VentLeft:0.0}s"; bar = o.Ready ? o.Heat : o.VentLeft / Overheat.Vent; }
        else if (u.TryGetComponent(out Brood b)) { ability = $"BROOD  {b.Count}/{b.Max}  ·  next {b.NextIn:0}s"; bar = (float)b.Count / Mathf.Max(1, b.Max); }
        else if (u.TryGetComponent(out Transport t)) { ability = $"ABOARD  {t.Carried}/{t.capacity}"; bar = (float)t.Carried / t.capacity; }
        else if (u.TryGetComponent(out Harvester hv)) { ability = $"CARGO  {hv.cargo:0}/{hv.capacity:0}  ·  {hv.state}"; bar = hv.cargo / Mathf.Max(1f, hv.capacity); }
        abilityText.text = ability;
        abilityBar.SetActive(bar >= 0f);
        if (bar >= 0f) SetBar(abilityFill, bar, Gold);
    }

    readonly List<UnitData> order = new();
    readonly Dictionary<UnitData, (int count, float hp)> types = new();

    void Grid()
    {
        order.Clear();
        types.Clear();
        foreach (var u in alive)
        {
            if (!types.TryGetValue(u.Data, out var g)) { order.Add(u.Data); g = (0, 0f); }
            types[u.Data] = (g.count + 1, g.hp + u.GetComponent<Health>().Fraction);
        }
        while (cells.Count < order.Count) cells.Add(MakeCell());
        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            bool used = i < order.Count;
            cell.b.gameObject.SetActive(used);
            if (!used) continue;
            var type = order[i];
            var (count, hp) = types[type];
            float avg = hp / count;
            cell.icon.sprite = type.icon;
            cell.count.text = count > 1 ? $"x{count}" : "";
            SetBar(cell.hp, avg, avg > 0.5f ? High : avg > 0.25f ? Mid : Low);
            cell.b.onClick.RemoveAllListeners();
            cell.b.onClick.AddListener(() => manager.SelectOnly(type));
        }
    }

    (Button, Image, Text, RectTransform, Image) MakeCell()
    {
        var go = new GameObject("Cell", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(grid, false);
        go.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.14f, 1f);
        var icon = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
        icon.transform.SetParent(go.transform, false);
        Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(60f, 60f));
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        var count = Label(go.transform, "", 16, TextAnchor.UpperRight, Color.white, new Vector2(-4f, -2f), new Vector2(60f, 20f), FontStyle.Bold, new Vector2(1f, 1f));
        var hp = Bar(go.transform, new Vector2(4f, 6f), new Vector2(64f, 6f), new Vector2(0f, 0f));
        return (go.GetComponent<Button>(), icon, count, hp, hp.GetComponent<Image>());
    }

    IEnumerable<T> Selected<T>() where T : Component
    {
        foreach (var u in alive) if (u.TryGetComponent(out T t)) yield return t;
    }

    // ================= control groups =================

    readonly Button[] groupTabs = new Button[10];

    void BuildGroups()
    {
        for (int i = 1; i <= 9; i++)
        {
            int g = i;
            groupTabs[i] = MakeButton(transform, "", new Vector2(12f + (i - 1) * 66f, -12f), new Vector2(60f, 46f), () => manager.SelectGroup(g), new Vector2(0f, 1f));
            Tip(groupTabs[i], $"Group {i}: click or press {i} to select. Ctrl+{i} saves the selection here.");
        }
    }

    void UpdateGroups()
    {
        float x = 12f;
        for (int i = 1; i <= 9; i++)
        {
            int n = 0;
            var list = manager.Group(i);
            if (list != null) foreach (var u in list) if (u != null && u.isActiveAndEnabled) n++;
            var tab = groupTabs[i];
            tab.gameObject.SetActive(n > 0);
            if (n == 0) continue;
            ((RectTransform)tab.transform).anchoredPosition = new Vector2(x, -12f); // packed to the left
            x += 66f;
            Text(tab).text = $"<b>{i}</b>\n<size=13>x{n}</size>";
        }
        groupsWidth = x;
    }

    float groupsWidth;

    // ================= production overview =================

    readonly List<(Button b, Image icon, RectTransform fill, Text label)> prodCells = new();
    float nextProdScan;
    readonly List<(Transform where, Structure s, Sprite icon, float progress, string label)> jobs = new();

    void BuildProduction() { }

    void UpdateProduction()
    {
        if (Time.time >= nextProdScan) { nextProdScan = Time.time + 0.2f; ScanJobs(); }
        while (prodCells.Count < jobs.Count) prodCells.Add(MakeProdCell());
        bool groupsShown = groupsWidth > 12f;
        for (int i = 0; i < prodCells.Count; i++)
        {
            var c = prodCells[i];
            bool used = i < jobs.Count;
            c.b.gameObject.SetActive(used);
            if (!used) continue;
            var j = jobs[i];
            ((RectTransform)c.b.transform).anchoredPosition = new Vector2(12f + i * 62f, groupsShown ? -66f : -12f);
            c.icon.sprite = j.icon;
            c.icon.enabled = j.icon != null;
            SetBar(c.fill, j.progress, Cyan);
            c.label.text = j.label;
            var where = j.where;
            var s = j.s;
            c.b.onClick.RemoveAllListeners();
            c.b.onClick.AddListener(() => { if (s != null) manager.SelectStructure(s); if (where != null) CameraPan.LookAt(where.position); });
        }
    }

    // Everything of ours in progress: units / research in each building, construction sites, upgrades.
    void ScanJobs()
    {
        jobs.Clear();
        foreach (var s in Structure.All)
        {
            if (s == null || s.Health.team != 0) continue;
            if (s.TryGetComponent(out Building b) && b.enabled && b.Producing >= 0)
            {
                int queued = 0;
                for (int i = 0; i < b.OptionCount; i++) queued += b.QueuedCount(i);
                jobs.Add((s.transform, s, b.IconOf(b.Producing), b.Progress01, queued > 1 ? $"x{queued}" : ""));
            }
            if (s.TryGetComponent(out BuildingUpgrade up) && up.IsUpgrading)
                jobs.Add((s.transform, s, s.data != null ? s.data.icon : null, up.Progress, "MK+"));
        }
        foreach (var c in FindObjectsByType<Construction>(FindObjectsSortMode.None))
        {
            if (c.remote) continue;
            var st = c.GetComponent<Structure>();
            jobs.Add((c.transform, null, st != null && st.data != null ? st.data.icon : null, c.Progress, "BUILD"));
        }
    }

    (Button, Image, RectTransform, Text) MakeProdCell()
    {
        var go = new GameObject("Job", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(transform, false);
        Place((RectTransform)go.transform, new Vector2(0f, 1f), Vector2.zero, new Vector2(56f, 56f), new Vector2(0f, 1f));
        go.GetComponent<Image>().color = Panel;
        AddFrame(go.transform, Cyan * 0.8f);
        var icon = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
        icon.transform.SetParent(go.transform, false);
        Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(44f, 44f));
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        var fill = Bar(go.transform, new Vector2(4f, 4f), new Vector2(48f, 5f), new Vector2(0f, 0f));
        var label = Label(go.transform, "", 13, TextAnchor.UpperRight, Gold, new Vector2(-3f, -1f), new Vector2(52f, 18f), FontStyle.Bold, new Vector2(1f, 1f));
        go.AddComponent<Tooltip>().text = "In progress. Click to select the building and look at it.";
        return (go.GetComponent<Button>(), icon, fill, label);
    }

    // ================= idle units =================

    Button idleBtn;
    float nextIdleScan;

    void BuildIdle()
    {
        idleBtn = MakeButton(transform, "", new Vector2(180f, 216f), new Vector2(130f, 60f), manager.NextIdle, new Vector2(0f, 0f));
        Tip(idleBtn, "Idle units (no orders, harvesters not mining). Click or press . (full stop) to cycle through them.");
    }

    void UpdateIdle()
    {
        if (Time.time < nextIdleScan) return;
        nextIdleScan = Time.time + 0.5f;
        int n = 0;
        foreach (var u in FindObjectsByType<SelectableUnit>(FindObjectsSortMode.None)) if (GoodSelectionManager.IsIdle(u)) n++;
        Text(idleBtn).text = $"IDLE  {n}";
        idleBtn.interactable = n > 0;
    }

    // ================= hotkey sheet (F1) =================

    void BuildHelp()
    {
        var hint = Label(transform, "F1  HOTKEYS", 15, TextAnchor.UpperRight, Grey, new Vector2(-16f, -348f), new Vector2(260f, 20f), FontStyle.Normal, new Vector2(1f, 1f));
        hint.raycastTarget = false;

        var root = Box(transform, "Hotkeys", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 640f), Panel, true, new Vector2(0.5f, 0.5f));
        help = root.gameObject;
        Label(root, "HOTKEYS", 34, TextAnchor.UpperCenter, Cyan, new Vector2(0f, -18f), new Vector2(900f, 44f), FontStyle.Bold, new Vector2(0.5f, 1f));
        string Col(params (string key, string what)[] rows)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var (k, w) in rows) sb.Append(k == "" ? $"\n<color=#70FFFF><b>{w}</b></color>\n" : $"<color=#FFD24A><b>{k}</b></color>   {w}\n");
            return sb.ToString();
        }
        Label(root, Col(
            ("", "UNITS"),
            ("Left-click / drag", "select / box select"),
            ("Double-click", "all of that type on screen"),
            ("Right-click", "move, attack, mine, board, garrison"),
            ("Right-drag", "line formation along the drag"),
            ("Shift + order", "add to the queue of orders"),
            ("A + click", "attack-move"),
            ("P + click", "patrol"),
            ("S  /  H", "stop  /  hold position"),
            ("F", "next formation"),
            ("U", "unload a Wasp Transport"),
            ("Ctrl+1-9  /  1-9", "save / select a group"),
            (".", "next idle unit")),
            18, TextAnchor.UpperLeft, Color.white, new Vector2(40f, -80f), new Vector2(440f, 520f), FontStyle.Normal, new Vector2(0f, 1f));
        Label(root, Col(
            ("", "BASE"),
            ("B", "build menu"),
            ("1-0, -, =, Z, X, C", "pick a building (menu open)"),
            ("Click, drag, release", "place and turn a building"),
            ("Right-click a unit button", "cancel one from the queue"),
            ("", "CAMERA & GAME"),
            ("WASD / arrows / edge", "scroll"),
            ("Mouse wheel", "zoom"),
            ("Space", "jump to the last alert"),
            ("F5  /  F9", "quick save  /  load (offline)"),
            ("Enter", "chat (online)"),
            ("Esc", "menu")),
            18, TextAnchor.UpperLeft, Color.white, new Vector2(510f, -80f), new Vector2(440f, 520f), FontStyle.Normal, new Vector2(0f, 1f));
        Label(root, "F1 or Esc to close", 15, TextAnchor.LowerCenter, Grey, new Vector2(0f, 14f), new Vector2(900f, 22f), FontStyle.Normal, new Vector2(0.5f, 0f));
        help.SetActive(false);
    }

    // ================= building blocks =================

    RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color? fill, bool framed, Vector2? pivot = null)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Place(rt, anchor, pos, size, pivot ?? anchor);
        if (fill is Color c) rt.gameObject.AddComponent<Image>().color = c;
        if (framed) AddFrame(rt, Cyan);
        return rt;
    }

    void AddFrame(Transform parent, Color c)
    {
        if (frame == null) return;
        var img = new GameObject("Frame", typeof(RectTransform)).AddComponent<Image>();
        img.transform.SetParent(parent, false);
        img.sprite = frame;
        img.type = Image.Type.Sliced;
        img.color = c;
        img.raycastTarget = false;
        var r = img.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot ?? anchor;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    // Default: positioned from the parent's top-left corner.
    Text Label(Transform parent, string s, int size, TextAnchor align, Color c, Vector2 pos, Vector2 box, FontStyle style = FontStyle.Normal, Vector2? anchor = null)
    {
        var t = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
        t.transform.SetParent(parent, false);
        Place(t.rectTransform, anchor ?? new Vector2(0f, 1f), pos, box);
        t.font = font; t.fontSize = size; t.alignment = align; t.color = c; t.fontStyle = style; t.text = s;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    // A bar from the top-left (or 'anchor') corner; returns the fill, sized with SetBar.
    RectTransform Bar(Transform parent, Vector2 pos, Vector2 size, Vector2? anchor = null)
    {
        var back = new GameObject("Bar", typeof(RectTransform)).AddComponent<Image>();
        back.transform.SetParent(parent, false);
        Place(back.rectTransform, anchor ?? new Vector2(0f, 1f), pos, size);
        back.color = BarBack;
        back.raycastTarget = false;
        var fill = new GameObject("Fill", typeof(RectTransform)).AddComponent<Image>();
        fill.transform.SetParent(back.transform, false);
        fill.raycastTarget = false;
        var r = fill.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        return r;
    }

    static void SetBar(RectTransform fill, float f, Color c)
    {
        fill.anchorMax = new Vector2(Mathf.Clamp01(f), 1f);
        fill.GetComponent<Image>().color = c;
    }

    Button MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, System.Action onClick, Vector2 anchor)
    {
        var b = Instantiate(template, parent);
        b.name = "HudButton";
        b.gameObject.SetActive(true);
        b.onClick = new Button.ButtonClickedEvent();
        b.onClick.AddListener(() => onClick());
        Place((RectTransform)b.transform, anchor, pos, size);
        var t = Text(b);
        t.text = label;
        t.supportRichText = true;
        t.fontSize = 17;
        t.alignment = TextAnchor.MiddleCenter;
        return b;
    }

    static Text Text(Button b) => b.GetComponentInChildren<Text>();
    static void Tip(Button b, string text)
    {
        if (!b.TryGetComponent(out Tooltip tip)) tip = b.gameObject.AddComponent<Tooltip>();
        tip.text = text;
    }
}
