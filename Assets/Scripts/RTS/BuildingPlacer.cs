using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// "Buildings" button -> pick a building -> ghost follows the mouse (click/drag to move it) -> Confirm to build.
public class BuildingPlacer : MonoBehaviour
{
    [SerializeField] BuildingData[] buildings; // build menu entries, in order
    [SerializeField] GoodSelectionManager selectionManager; // disabled while placing so clicks don't select
    [SerializeField] LayerMask groundLayer;
    [SerializeField] Material ghostMaterial; // see-through hologram; tinted red when blocked
    [SerializeField] Color blockedColor = new(1f, 0.15f, 0.2f, 0.3f);
    [SerializeField] float gridSize = 2f; // metres; ghost snaps to this while placing
    [SerializeField] float buildRadius = 30f; // new buildings must be this close to one you own
    [SerializeField] Renderer gridOverlay; // ground-sized quad, shown only while placing
    [SerializeField] RectTransform progressBarTemplate; // cloned above each building under construction

    [Header("UI")]
    [SerializeField] Button buildingsButton;
    [SerializeField] RectTransform menuPanel;
    [SerializeField] GameObject unitPanel; // shares the bottom strip with the build menu; only one shows
    [SerializeField] Button optionTemplate; // disabled template, cloned per building
    [SerializeField] GameObject confirmBar;
    [SerializeField] Button confirmButton;
    [SerializeField] Button cancelButton;
    [SerializeField] Button rotateButton; // hold to rotate
    [SerializeField] float rotateSpeed = 120f; // degrees per second while Q/E or the button is held
    [SerializeField] GameObject placementHint; // "Q/E rotate..." shown while placing

    public static bool MenuOpen { get; private set; } // number keys pick buildings while it's open

    // B opens the menu; then 1-9, 0, -, =, Z, X, C pick the buttons left to right.
    static readonly Key[] Hotkeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6,
        Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0, Key.Minus, Key.Equals, Key.Z, Key.X, Key.C };
    static readonly string[] HotkeyLabels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "Z", "X", "C" };
    bool[] lockedShown; // tooltip state per button, rebuilt when it changes

    Camera cam;
    Quaternion lastRotation = Quaternion.identity; // next building starts facing the same way
    bool rotateHeld; // ROTATE button is being pressed
    Button[] optionButtons;
    readonly System.Collections.Generic.List<GameObject> ghosts = new();   // one, or a line of wall segments
    readonly System.Collections.Generic.List<Material> ghostMats = new();  // per ghost, so each tints on its own
    GameObject ghost => ghosts.Count > 0 ? ghosts[0] : null;
    Vector3 dragStart; // walls: where the line starts
    bool dragging;
    int chosen;
    bool parked; // false: ghost follows the mouse; true: it stays put unless dragged

    static readonly int CellId = Shader.PropertyToID("_Cell");
    static readonly int CenterId = Shader.PropertyToID("_Center");
    static readonly int RadiusId = Shader.PropertyToID("_Radius");

    void Awake()
    {
        buildings = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OrderBy(buildings, b => b.cost)); // cheapest first (ties keep inspector order)
        cam = Camera.main;
        menuPanel.gameObject.SetActive(false);
        confirmBar.SetActive(false);
        optionTemplate.gameObject.SetActive(false);

        gridOverlay.material.SetFloat(CellId, gridSize); // lines match where buildings snap
        gridOverlay.gameObject.SetActive(false);

        buildingsButton.onClick.AddListener(ToggleMenu);
        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(Cancel);
        // Hold-to-rotate: a click event only fires once, so track press/release instead.
        var trigger = rotateButton.gameObject.AddComponent<EventTrigger>();
        var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(_ => rotateHeld = true);
        var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener(_ => rotateHeld = false);
        trigger.triggers.Add(down);
        trigger.triggers.Add(up);
        placementHint.SetActive(false);

        optionButtons = new Button[buildings.Length];
        for (int i = 0; i < buildings.Length; i++)
        {
            int index = i;
            Button btn = Instantiate(optionTemplate, optionTemplate.transform.parent);
            btn.gameObject.SetActive(true);
            btn.transform.Find("Text").GetComponent<Text>().text = buildings[i].displayName;
            btn.transform.Find("Time").GetComponent<Text>().text = $"{buildings[i].buildTime:0}s";
            btn.transform.Find("Cost").GetComponent<Text>().text = $"${buildings[i].cost}";
            if (buildings[i].icon != null) btn.GetComponent<Image>().sprite = buildings[i].icon;
            btn.onClick.AddListener(() => StartPlacing(index));
            btn.gameObject.AddComponent<Tooltip>().text = Tooltip.For(buildings[i]);
            optionButtons[i] = btn;

            if (i < HotkeyLabels.Length) // key badge under the price, styled like the time badge
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
        for (int i = 0; i < buildings.Length; i++) lockedShown[i] = !buildings[i].Unlocked(); // force a first tooltip update
    }

    bool CanAfford(int index) => ResourceBank.GetOrCreate().Credits >= buildings[index].cost;
    bool CanPick(int index) => CanAfford(index) && buildings[index].Unlocked();

    // Shop tooltip plus the tech requirement line (red while missing).
    string Tip(int i, bool locked)
    {
        var d = buildings[i];
        if (d.requires == null || d.requires.Length == 0) return Tooltip.For(d);
        string names = string.Join(", ", System.Linq.Enumerable.Select(d.requires, r => r.displayName));
        return Tooltip.For(d) + $"\n<color={(locked ? "#FF5050" : "#8899AA")}>Requires: {names}</color>";
    }

    // B toggles the menu, Esc closes it, number keys pick a building.
    void HandleHotkeys()
    {
        var kb = Keyboard.current;
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
        parked = false;
        dragging = false;
        MakeGhost();

        selectionManager.enabled = false;
        gridOverlay.gameObject.SetActive(true);
        placementHint.SetActive(true);
        var hint = placementHint.GetComponent<Text>();
        if (hint != null)
            hint.text = buildings[index].dragLine
                ? "PRESS AND DRAG  DRAW A WALL LINE      RIGHT-CLICK / ESC  CANCEL"
                : "HOLD Q / E  ROTATE      LEFT-CLICK  PLACE      RIGHT-CLICK / ESC  CANCEL";

        // Grid circle a bit bigger than the building's footprint.
        Vector3 size = Vector3.Scale(ghost.GetComponent<BoxCollider>().size, ghost.transform.lossyScale);
        gridOverlay.material.SetFloat(RadiusId, Mathf.Max(size.x, size.z) * 0.5f + 8f);
    }

    // Ghost = a copy with physics, carving and scripts turned off, drawn in the hologram material.
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

    // Walls: segments end to end from a to b, all facing along the line.
    void LayLine(Vector3 a, Vector3 b)
    {
        Vector3 dir = b - a;
        dir.y = 0f;
        var box = ghost.GetComponent<BoxCollider>();
        float seg = box.size.x * ghost.transform.lossyScale.x; // walls are long along local X
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
        if (MenuOpen)
            for (int i = 0; i < optionButtons.Length; i++)
            {
                bool locked = !buildings[i].Unlocked();
                optionButtons[i].interactable = !locked && CanAfford(i); // red when short or not unlocked yet
                if (locked == lockedShown[i]) continue;
                lockedShown[i] = locked;
                optionButtons[i].GetComponent<Tooltip>().text = Tip(i, locked);
            }

        if (ghost == null) return;
        var mouse = Mouse.current;
        var kb = Keyboard.current;

        if (mouse.rightButton.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) { Cancel(); return; }
        float turn = 0f;
        if (kb.eKey.isPressed || kb.rKey.isPressed || rotateHeld) turn += 1f;
        if (kb.qKey.isPressed) turn -= 1f;
        if (turn != 0f) Rotate(turn * rotateSpeed * Time.unscaledDeltaTime);

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (!overUI && (!parked || mouse.leftButton.isPressed))
        {
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
            {
                Vector3 p = new(Snap(hit.point.x), hit.point.y, Snap(hit.point.z));
                if (buildings[chosen].dragLine && mouse.leftButton.wasPressedThisFrame) { dragStart = p; dragging = true; }
                if (dragging && mouse.leftButton.isPressed) LayLine(dragStart, p);
                else
                {
                    while (ghosts.Count > 1) RemoveGhost(ghosts.Count - 1);
                    ghost.transform.position = p;
                }
            }
        }
        if (!overUI && mouse.leftButton.wasReleasedThisFrame)
        {
            parked = true;
            dragging = false;
            confirmBar.SetActive(true);
        }

        gridOverlay.material.SetVector(CenterId, ghosts[ghosts.Count / 2].transform.position);

        // Each ghost is checked and tinted on its own; Confirm builds the good ones.
        int good = 0;
        string reason = "BLOCKED";
        for (int i = 0; i < ghosts.Count; i++)
        {
            bool near = NearBase(ghosts[i]), free = !IsBlocked(ghosts[i]), ok = near && free;
            if (ok) good++;
            else if (!near) reason = "TOO FAR";
            ghostMats[i].color = ok ? ghostMaterial.color : blockedColor;
            ghostMats[i].SetColor("_EmissionColor", ok ? ghostMaterial.GetColor("_EmissionColor") : blockedColor * 0.6f);
        }
        int cost = buildings[chosen].cost;
        bool afford = CanAfford(chosen); // credits can drop while placing (units queued)
        bool unlocked = buildings[chosen].Unlocked(); // the required building can die while placing
        confirmButton.interactable = good > 0 && afford && unlocked;
        confirmButton.GetComponentInChildren<Text>().text =
            !unlocked ? "LOCKED" : good == 0 ? reason : !afford ? "NO CREDITS" : good > 1 ? $"BUILD {good}  ${cost * good}" : $"CONFIRM ${cost}";
    }

    void Rotate(float degrees)
    {
        if (ghosts.Count != 1) return; // a wall line faces along its drag
        ghost.transform.Rotate(0f, degrees, 0f);
        lastRotation = ghost.transform.rotation;
    }

    float Snap(float v) => Mathf.Round(v / gridSize) * gridSize;

    // Anything but the ground inside the building's box blocks it (other buildings, cubes, units).
    bool IsBlocked(GameObject g)
    {
        var box = g.GetComponent<BoxCollider>();
        Transform t = g.transform;
        Vector3 half = Vector3.Scale(box.size, t.lossyScale) * 0.5f - Vector3.one * 0.05f; // a touching wall segment isn't "inside"
        return Physics.CheckBox(t.TransformPoint(box.center), half, t.rotation, ~groundLayer, QueryTriggerInteraction.Ignore);
    }

    // C&C build radius: the ghost's footprint must come within buildRadius of a finished building of ours.
    bool NearBase(GameObject g)
    {
        Vector3 p = g.transform.position;
        var box = g.GetComponent<BoxCollider>();
        Vector3 size = Vector3.Scale(box.size, g.transform.lossyScale);
        float reach = buildRadius + Mathf.Max(size.x, size.z) * 0.5f;
        foreach (var s in Structure.All)
            if (s.Health.team == 0 && s.TryGetComponent(out Collider c) && (c.ClosestPoint(p) - p).sqrMagnitude <= reach * reach) return true;
        return false;
    }

    // Builds every valid ghost (check them all first: new buildings would block their neighbours' checks).
    void Confirm()
    {
        var spots = ghosts.FindAll(g => NearBase(g) && !IsBlocked(g));
        int built = 0;
        foreach (var g in spots)
        {
            if (!ResourceBank.GetOrCreate().Spend(buildings[chosen].cost)) break;
            var b = Instantiate(buildings[chosen].prefab, g.transform.position, g.transform.rotation);
            var site = b.AddComponent<Construction>();
            site.duration = buildings[chosen].buildTime;
            site.barTemplate = progressBarTemplate;
            built++;
        }
        if (built > 0) Cancel();
    }

    void Cancel()
    {
        while (ghosts.Count > 0) RemoveGhost(ghosts.Count - 1);
        confirmBar.SetActive(false);
        placementHint.SetActive(false);
        rotateHeld = false;
        gridOverlay.gameObject.SetActive(false);
        selectionManager.enabled = true;
    }
}
