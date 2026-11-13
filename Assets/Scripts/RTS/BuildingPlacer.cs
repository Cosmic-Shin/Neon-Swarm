using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BuildingPlacer : MonoBehaviour
{
    [SerializeField] BuildingData[] buildings;
    [SerializeField] GoodSelectionManager selectionManager;
    [SerializeField] LayerMask groundLayer;
    [SerializeField] Material ghostMaterial;
    [SerializeField] Color blockedColor = new(1f, 0.15f, 0.2f, 0.3f);
    [SerializeField] float gridSize = 2f;
    [SerializeField] Renderer gridOverlay;
    [SerializeField] RectTransform progressBarTemplate;

    [Header("UI")]
    [SerializeField] Button buildingsButton;
    [SerializeField] RectTransform menuPanel;
    [SerializeField] GameObject unitPanel;
    [SerializeField] Button optionTemplate;
    [SerializeField] GameObject placementHint;
    [SerializeField] float rotateDeadzone = 1.5f;

    const string PlaceHint = "HOLD LEFT-CLICK + DRAG  ROTATE      RELEASE  BUILD      RIGHT-CLICK / ESC  CANCEL";
    const string WallHint = "PRESS AND DRAG  DRAW A WALL LINE      RELEASE  BUILD      RIGHT-CLICK / ESC  CANCEL";
    Text hintText;
    Color hintColor;

    public static bool MenuOpen { get; private set; }
    public static bool Placing { get; private set; }
    public static bool PlacementOk { get; private set; }
    public BuildingData[] Buildings => buildings;
    public RectTransform ProgressBarTemplate => progressBarTemplate;

    static readonly Key[] Hotkeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6,
        Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0, Key.Minus, Key.Equals, Key.Z, Key.X, Key.C };
    static readonly string[] HotkeyLabels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "Z", "X", "C" };
    bool[] lockedShown;

    Camera cam;
    Quaternion lastRotation = Quaternion.identity;
    Button[] optionButtons;
    readonly System.Collections.Generic.List<GameObject> ghosts = new();
    readonly System.Collections.Generic.List<Material> ghostMats = new();
    GameObject ghost => ghosts.Count > 0 ? ghosts[0] : null;
    int chosen;
    bool anchored;
    Vector3 anchor;

    static readonly int CellId = Shader.PropertyToID("_Cell");
    static readonly int CenterId = Shader.PropertyToID("_Center");
    static readonly int RadiusId = Shader.PropertyToID("_Radius");

    void Awake()
    {
        buildings = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OrderBy(buildings, b => b.cost));
        cam = Camera.main;
        menuPanel.gameObject.SetActive(false);
        optionTemplate.gameObject.SetActive(false);

        gridOverlay.material.SetFloat(CellId, gridSize);
        gridOverlay.gameObject.SetActive(false);

        buildingsButton.onClick.AddListener(ToggleMenu);
        hintText = placementHint.GetComponent<Text>();
        hintColor = hintText.color;
        placementHint.SetActive(false);

        optionButtons = new Button[buildings.Length];
        for (int i = 0; i < buildings.Length; i++)
        {
            int index = i;
            Button btn = Instantiate(optionTemplate, optionTemplate.transform.parent);
            HoloSkin.SquareCard(btn);
            btn.gameObject.SetActive(true);
            btn.transform.Find("Text").GetComponent<Text>().text = buildings[i].displayName;
            btn.transform.Find("Time").GetComponent<Text>().text = $"{buildings[i].buildTime:0}s";
            btn.transform.Find("Cost").GetComponent<Text>().text = $"${buildings[i].cost}";
            if (buildings[i].icon != null) btn.transform.Find("Icon").GetComponent<Image>().sprite = buildings[i].icon;
            btn.onClick.AddListener(() => StartPlacing(index));
            btn.gameObject.AddComponent<Tooltip>().text = Tooltip.For(buildings[i]);
            optionButtons[i] = btn;

            if (i < HotkeyLabels.Length)
            {
                var time = btn.transform.Find("Time").GetComponent<Text>();
                var key = Instantiate(time, btn.transform);
                key.name = "Key";
                key.text = $"[{HotkeyLabels[i]}]";
                key.alignment = TextAnchor.UpperLeft;
                var rt = key.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(8f, -30f);
            }
        }
        lockedShown = new bool[buildings.Length];
        for (int i = 0; i < buildings.Length; i++) lockedShown[i] = !buildings[i].Unlocked();
    }

    bool CanAfford(int index) => ResourceBank.GetOrCreate().Credits >= buildings[index].cost;
    bool CanPick(int index) => CanAfford(index) && buildings[index].Unlocked();

    string Tip(int i, bool locked)
    {
        var d = buildings[i];
        if (d.requires == null || d.requires.Length == 0) return Tooltip.For(d);
        string names = string.Join(", ", System.Linq.Enumerable.Select(d.requires, r => r.displayName));
        return Tooltip.For(d) + $"\n<color={(locked ? "#FF5050" : "#8899AA")}>Requires: {names}</color>";
    }

    void HandleHotkeys()
    {
        var kb = Chat.Keys;
        if (kb == null || ghost != null) return;
        if (kb.bKey.wasPressedThisFrame) ToggleMenu();
        if (!menuPanel.gameObject.activeSelf) return;
        if (kb.escapeKey.wasPressedThisFrame) { menuPanel.gameObject.SetActive(false); return; }
        for (int i = 0; i < Hotkeys.Length && i < buildings.Length; i++)
            if (kb[Hotkeys[i]].wasPressedThisFrame && CanPick(i)) { StartPlacing(i); return; }
    }

    void ToggleMenu()
    {
        if (ghost != null) Cancel();
        bool open = !menuPanel.gameObject.activeSelf;
        menuPanel.gameObject.SetActive(open);
        if (open) unitPanel.SetActive(false);
    }

    void OnEnable() => selectionManager.OnBuildingSelected += CloseMenuOnSelect;
    void OnDisable() => selectionManager.OnBuildingSelected -= CloseMenuOnSelect;
    void CloseMenuOnSelect(Building b) { if (b != null) menuPanel.gameObject.SetActive(false); }

    void StartPlacing(int index)
    {
        menuPanel.gameObject.SetActive(false);
        chosen = index;
        anchored = false;
        MakeGhost();

        selectionManager.enabled = false;
        gridOverlay.gameObject.SetActive(true);
        BuildRadius.ShowAll(true);
        placementHint.SetActive(true);

        Vector3 size = Vector3.Scale(ghost.GetComponent<BoxCollider>().size, ghost.transform.lossyScale);
        gridOverlay.material.SetFloat(RadiusId, Mathf.Max(size.x, size.z) * 0.5f + 8f);
    }

    GameObject MakeGhost()
    {
        var g = Instantiate(buildings[chosen].prefab, Vector3.zero, lastRotation);
        foreach (var c in g.GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (var b in g.GetComponentsInChildren<Behaviour>()) b.enabled = false;
        var mat = new Material(ghostMaterial);
        foreach (var r in g.GetComponentsInChildren<Renderer>())
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        ghosts.Add(g);
        ghostMats.Add(mat);
        return g;
    }

    void RemoveGhost(int i)
    {
        Destroy(ghosts[i]);
        Destroy(ghostMats[i]);
        ghosts.RemoveAt(i);
        ghostMats.RemoveAt(i);
    }

    void LayLine(Vector3 a, Vector3 b)
    {
        Vector3 dir = b - a;
        dir.y = 0f;
        var box = ghost.GetComponent<BoxCollider>();
        float seg = box.size.x * ghost.transform.lossyScale.x;
        int n = dir.magnitude < seg * 0.5f ? 1 : Mathf.FloorToInt(dir.magnitude / seg + 0.5f) + 1;
        if (dir.sqrMagnitude > 0.01f) lastRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 90f, 0f);
        while (ghosts.Count < n) MakeGhost();
        while (ghosts.Count > n) RemoveGhost(ghosts.Count - 1);
        Vector3 step = dir.sqrMagnitude > 0.01f ? dir.normalized * seg : Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            ghosts[i].transform.position = a + step * i;
            ghosts[i].transform.rotation = lastRotation;
        }
    }

    void Update()
    {
        HandleHotkeys();
        MenuOpen = menuPanel.gameObject.activeSelf;
        Placing = ghost != null;
        if (MenuOpen)
            for (int i = 0; i < optionButtons.Length; i++)
            {
                bool locked = !buildings[i].Unlocked();
                optionButtons[i].interactable = !locked && CanAfford(i);
                LockBadge.Set(optionButtons[i], locked ? buildings[i].Missing() : null, CanAfford(i));
                HoloSkin.SetCard(optionButtons[i], locked ? HoloUI.CardState.Locked : Placing && chosen == i ? HoloUI.CardState.Selected : HoloUI.CardState.Normal);
                if (locked == lockedShown[i]) continue;
                lockedShown[i] = locked;
                optionButtons[i].GetComponent<Tooltip>().text = Tip(i, locked);
            }

        if (ghost == null) return;
        var mouse = Mouse.current;
        var kb = Chat.Keys;

        if (mouse.rightButton.wasPressedThisFrame || (kb != null && kb.escapeKey.wasPressedThisFrame)) { Cancel(); return; }

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        bool onGround = Physics.Raycast(cam.ScreenPointToRay(mouse.position.ReadValue()), out RaycastHit hit, 1000f, groundLayer);
        Vector3 p = onGround ? new(Snap(hit.point.x), hit.point.y, Snap(hit.point.z)) : Vector3.zero;

        if (!anchored && mouse.leftButton.wasPressedThisFrame && !overUI && onGround) { anchored = true; anchor = p; }
        if (anchored)
        {
            if (buildings[chosen].dragLine) { if (onGround) LayLine(anchor, p); }
            else if (onGround)
            {
                Vector3 d = hit.point - anchor;
                d.y = 0f;
                if (d.magnitude > rotateDeadzone) lastRotation = Quaternion.LookRotation(d);
                ghost.transform.SetPositionAndRotation(anchor, lastRotation);
            }
        }
        else if (onGround && !overUI)
        {
            while (ghosts.Count > 1) RemoveGhost(ghosts.Count - 1);
            ghost.transform.SetPositionAndRotation(p, lastRotation);
            if (p != lastSnap) { lastSnap = p; snapTime = Time.time; Sfx.Play(Snd.Click, 0.12f, 1.6f); }
        }
        float pop = 1f - Mathf.Clamp01((Time.time - snapTime) / 0.15f);
        ghost.transform.localScale = buildings[chosen].prefab.transform.localScale * (1f + 0.05f * pop * pop);

        gridOverlay.material.SetVector(CenterId, ghosts[ghosts.Count / 2].transform.position);

        int good = 0;
        string reason = "BLOCKED";
        for (int i = 0; i < ghosts.Count; i++)
        {
            bool near = NearBase(ghosts[i]), free = !IsBlocked(ghosts[i]), ok = near && free;
            if (ok) good++;
            else if (!near) reason = "TOO FAR FROM BASE";
            ghostMats[i].color = ok ? ghostMaterial.color : blockedColor;
            ghostMats[i].SetColor("_EmissionColor", ok ? ghostMaterial.GetColor("_EmissionColor") : blockedColor * 0.6f);
        }
        int cost = buildings[chosen].cost;
        bool afford = ResourceBank.GetOrCreate().Credits >= cost * Mathf.Max(1, good);
        bool unlocked = buildings[chosen].Unlocked();
        string problem = !unlocked ? "LOCKED" : !buildings[chosen].dragLine && !BuildSlots.Free ? BuildSlots.FullReason : good == 0 ? reason : !afford ? "NOT ENOUGH CREDITS" : null;
        string price = good > 1 ? $"BUILD {good}  ${cost * good}" : $"${cost}";
        hintText.text = (problem ?? price) + "      " + (buildings[chosen].dragLine ? WallHint : PlaceHint);
        hintText.color = problem == null ? hintColor : blockedColor * 3f;
        PlacementOk = problem == null;

        if (anchored && mouse.leftButton.wasReleasedThisFrame)
        {
            anchored = false;
            if (problem == null) Build();
            else Alerts.Show($"Can't build here: {problem.ToLower()}", Alerts.Warn, 1f);
        }
    }

    float Snap(float v) => Mathf.Round(v / gridSize) * gridSize;
    public int GroundMask => groundLayer;
    Vector3 lastSnap;
    float snapTime = -10f;

    bool IsBlocked(GameObject g)
    {
        var box = g.GetComponent<BoxCollider>();
        Transform t = g.transform;
        Vector3 half = Vector3.Scale(box.size, t.lossyScale) * 0.5f - Vector3.one * 0.05f;
        return Physics.CheckBox(t.TransformPoint(box.center), half, t.rotation, ~groundLayer, QueryTriggerInteraction.Ignore);
    }

    bool NearBase(GameObject g)
    {
        Vector3 size = Vector3.Scale(g.GetComponent<BoxCollider>().size, g.transform.lossyScale);
        return BuildRadius.Covers(g.transform.position, Mathf.Max(size.x, size.z), 0);
    }

    void Build()
    {
        var spots = ghosts.FindAll(g => NearBase(g) && !IsBlocked(g));
        int built = 0;
        foreach (var g in spots)
        {
            if (!ResourceBank.GetOrCreate().Spend(buildings[chosen].cost)) break;
            var b = Net.Spawn(buildings[chosen].prefab, g.transform.position, g.transform.rotation);
            var site = b.AddComponent<Construction>();
            site.duration = buildings[chosen].buildTime;
            site.barTemplate = progressBarTemplate;
            Juice.PlacedRipple(b);
            built++;
        }
        if (built > 0) { Sfx.Play(Snd.Place, 0.7f); Cancel(); }
    }

    void Cancel()
    {
        while (ghosts.Count > 0) RemoveGhost(ghosts.Count - 1);
        placementHint.SetActive(false);
        anchored = false;
        gridOverlay.gameObject.SetActive(false);
        BuildRadius.ShowAll(false);
        selectionManager.enabled = true;
    }
}
