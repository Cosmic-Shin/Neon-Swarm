using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class GoodSelectionManager : MonoBehaviour
{
    public event Action<List<Vector3>> OnMoveCommand; // one slot per selected unit
    public event Action<Building> OnBuildingSelected;
    public event Action<Structure> OnStructureSelected; // any of our buildings (null = none)
    public event Action<OreNode> OnHarvestCommand; // right-clicked ore: workers go mine it
    public event Action<Health> OnAttackCommand;   // right-clicked an enemy: armed units attack it
    public event Action<OutpostCapture> OnGarrisonCommand; // right-clicked our outpost: armed units go inside
    public event Action<List<Vector3>> OnAttackMoveCommand; // A + click: walk there, fighting anything met on the way
    public event Action OnStopCommand; // S
    public event Action OnHoldCommand; // H: stay put, shoot only what's in range

    public enum Formation { Circle, Line, Box }


    [Header("Raycasting")]
    [SerializeField] Camera cam;
    [SerializeField] LayerMask selectableLayer;
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float maxRayDistance = 1000f;

    [Header("Box selection")]
    [SerializeField] RectTransform selectionBox;
    [SerializeField] float dragThreshold = 10f;

    [Header("Optional target marker")]
    [SerializeField] GameObject targetMarker;
    [SerializeField] float markerDuration = 1f;

    [Header("Formation")]
    [SerializeField] float formationSpacing = 2f; // gap between rings
    [SerializeField] float minRadius = 1.5f;      // first ring radius
    [SerializeField] float slotSpacing = 2.5f;    // line/box: gap between units
    [SerializeField] int lineWidth = 10;          // line: units per row
    [SerializeField] Formation formation = Formation.Circle; // F cycles it

    readonly List<SelectableUnit> selected = new();
    public static bool UnitsSelected { get; private set; } // A/S/H are orders then, so the camera ignores WASD
    public Vector3 Facing { get; private set; } // which way the last move's formation faces (zero = don't turn)
    bool attackMoveArmed; // A pressed: next left-click is an attack-move
    bool rightPressing, rightDragging;
    Vector2 rightStart;
    LineRenderer dragLine; // preview of a right-drag formation line
    public IReadOnlyList<SelectableUnit> SelectedUnits => selected;
    Vector2 dragStart;
    bool isPressing;
    bool isDragging;
    float markerHideTime;
    Building selectedBuilding;
    Structure selectedStructure;
    public Structure SelectedStructure => selectedStructure;

    readonly List<SelectableUnit>[] groups = new List<SelectableUnit>[10]; // Ctrl+1..9 saves, 1..9 recalls
    float lastClickTime = -1f;
    UnitData lastClickType;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (selectionBox != null) selectionBox.gameObject.SetActive(false);
        if (targetMarker != null) targetMarker.SetActive(false);

        dragLine = new GameObject("FormationLine").AddComponent<LineRenderer>();
        dragLine.material = new Material(Shader.Find("Sprites/Default"));
        dragLine.startColor = dragLine.endColor = new Color(0.3f, 1f, 1f, 0.8f);
        dragLine.widthMultiplier = 0.3f;
        dragLine.positionCount = 2;
        dragLine.enabled = false;
    }

    // Disabled while a building is being placed.
    void OnDisable()
    {
        UnitsSelected = rightPressing = rightDragging = attackMoveArmed = false;
        if (dragLine != null) dragLine.enabled = false;
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 mousePos = mouse.position.ReadValue();

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        UnitsSelected = selected.Exists(u => u != null); // keep dead slots: units remember their index

        OrderKeys();

        // Attack-move armed: this click is the order, not a selection.
        if (attackMoveArmed && mouse.leftButton.wasPressedThisFrame && !overUI)
        {
            attackMoveArmed = false;
            IssueAttackMove(mousePos);
        }
        else if (mouse.leftButton.wasPressedThisFrame && !overUI)
        {
            dragStart = mousePos;
            isPressing = true;
            isDragging = false;
        }

        if (isPressing && mouse.leftButton.isPressed)
        {
            if (!isDragging && (mousePos - dragStart).sqrMagnitude > dragThreshold * dragThreshold)
            {
                isDragging = true;
                if (selectionBox != null) selectionBox.gameObject.SetActive(true);
            }
            if (isDragging) UpdateSelectionBox(dragStart, mousePos);
        }

        if (isPressing && mouse.leftButton.wasReleasedThisFrame)
        {
            isPressing = false;
            if (isDragging)
            {
                if (selectionBox != null) selectionBox.gameObject.SetActive(false);
                BoxSelect(dragStart, mousePos);
            }
            else
            {
                ClickSelect(mousePos);
            }
            isDragging = false;
        }

        // Right-click: move. Right-drag: units line up along the dragged line, facing forward.
        if (mouse.rightButton.wasPressedThisFrame && !overUI)
        {
            if (attackMoveArmed) attackMoveArmed = false; // right-click cancels A
            else { rightPressing = true; rightDragging = false; rightStart = mousePos; }
        }
        if (rightPressing && mouse.rightButton.isPressed && selected.Count > 0 &&
            (mousePos - rightStart).sqrMagnitude > dragThreshold * dragThreshold * 4f &&
            GroundAt(rightStart, out Vector3 a) && GroundAt(mousePos, out Vector3 b))
        {
            rightDragging = true;
            dragLine.SetPosition(0, a + Vector3.up * 0.3f);
            dragLine.SetPosition(1, b + Vector3.up * 0.3f);
            dragLine.enabled = true;
        }
        if (rightPressing && mouse.rightButton.wasReleasedThisFrame)
        {
            rightPressing = false;
            dragLine.enabled = false;
            if (rightDragging && GroundAt(rightStart, out Vector3 a2) && GroundAt(mousePos, out Vector3 b2)) LineMove(a2, b2);
            else IssueMoveCommand(rightStart);
            rightDragging = false;
        }

        ControlGroups();

        if (targetMarker != null && targetMarker.activeSelf && Time.time >= markerHideTime)
            targetMarker.SetActive(false);
    }

    void ClickSelect(Vector2 screenPos)
    {
        DeselectAll();

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, selectableLayer))
        {
            if (hit.collider.TryGetComponent(out SelectableUnit unit))
            {
                if (unit.IsEnemy) return;
                // Double-click: every unit of this type on screen.
                bool doubleClick = Time.unscaledTime - lastClickTime < 0.35f && lastClickType == unit.Data;
                lastClickTime = Time.unscaledTime;
                lastClickType = unit.Data;
                if (!doubleClick) { SelectUnit(unit); return; }
                foreach (var u in FindObjectsByType<SelectableUnit>(FindObjectsSortMode.None))
                {
                    Vector3 sp = cam.WorldToScreenPoint(u.transform.position);
                    bool onScreen = sp.z > 0f && sp.x >= 0f && sp.y >= 0f && sp.x <= Screen.width && sp.y <= Screen.height;
                    if (onScreen && !u.IsEnemy && u.Data == unit.Data) SelectUnit(u);
                }
            }
            else if (hit.collider.TryGetComponent(out OutpostCapture outpost) && outpost.ownerTeam == 0)
            {
                // Click our outpost: everyone inside comes out, selected.
                var inside = new List<TeamMember>(outpost.Garrison);
                outpost.EjectAll();
                foreach (var t in inside) if (t != null && t.TryGetComponent(out SelectableUnit u)) SelectUnit(u);
            }
            else if (hit.collider.TryGetComponent(out Structure structure) && structure.enabled && structure.Health.team == 0) // disabled = still under construction
            {
                selectedStructure = structure;
                structure.IsSelected = true;
                OnStructureSelected?.Invoke(structure);
                if (structure.TryGetComponent(out Building building) && building.enabled)
                {
                    selectedBuilding = building;
                    OnBuildingSelected?.Invoke(building);
                }
            }
        }
    }

    void BoxSelect(Vector2 a, Vector2 b)
    {
        DeselectAll();

        Rect screenRect = Rect.MinMaxRect(
            Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
            Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

        foreach (var unit in FindObjectsByType<SelectableUnit>(FindObjectsSortMode.None))
        {
            Vector3 sp = cam.WorldToScreenPoint(unit.transform.position);
            if (sp.z > 0f && screenRect.Contains(sp) && !unit.IsEnemy) // z > 0: ignore units behind the camera
                SelectUnit(unit);
        }
    }

    void UpdateSelectionBox(Vector2 a, Vector2 b)
    {
        if (selectionBox == null) return;
        var parent = (RectTransform)selectionBox.parent;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, a, null, out Vector2 la);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, b, null, out Vector2 lb);

        selectionBox.anchorMin = selectionBox.anchorMax = parent.pivot;
        selectionBox.pivot = Vector2.zero;
        selectionBox.anchoredPosition = Vector2.Min(la, lb);
        selectionBox.sizeDelta = new Vector2(Mathf.Abs(la.x - lb.x), Mathf.Abs(la.y - lb.y));
    }

    bool GroundAt(Vector2 screenPos, out Vector3 point)
    {
        bool hit = Physics.Raycast(cam.ScreenPointToRay(screenPos), out RaycastHit h, maxRayDistance, groundLayer);
        point = h.point;
        return hit;
    }

    // A = attack-move (then click), S = stop, H = hold position, F = next formation.
    void OrderKeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.fKey.wasPressedThisFrame)
        {
            formation = (Formation)(((int)formation + 1) % 3);
            Alerts.Show($"Formation: {formation}", Alerts.Info);
        }
        if (kb.escapeKey.wasPressedThisFrame) attackMoveArmed = false;
        if (!UnitsSelected) { attackMoveArmed = false; return; }
        if (kb.aKey.wasPressedThisFrame) { attackMoveArmed = true; Alerts.Show("Attack-move: left-click a spot", Alerts.Warn); }
        if (kb.sKey.wasPressedThisFrame) { attackMoveArmed = false; OnStopCommand?.Invoke(); }
        if (kb.hKey.wasPressedThisFrame) { attackMoveArmed = false; OnHoldCommand?.Invoke(); }
    }

    void IssueAttackMove(Vector2 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit target, maxRayDistance, selectableLayer) &&
            target.collider.TryGetComponent(out Health enemy) && enemy.enabled && enemy.team != 0)
        {
            OnAttackCommand?.Invoke(enemy); // clicked right on an enemy: plain attack
            return;
        }
        if (!GroundAt(screenPos, out Vector3 point)) return;
        OnAttackMoveCommand?.Invoke(GetFormation(point));
        ShowMarker(point);
    }

    // Right-drag: units spread along a..b (extra rows behind), facing away from where they stand now.
    void LineMove(Vector3 a, Vector3 b)
    {
        Vector3 along = b - a; along.y = 0f;
        Vector3 mid = (a + b) * 0.5f;
        Vector3 facing = Vector3.Cross(along.normalized, Vector3.up);
        if (Vector3.Dot(facing, mid - Centroid()) < 0f) facing = -facing;
        int cols = Mathf.Clamp(Mathf.FloorToInt(along.magnitude / slotSpacing) + 1, 1, selected.Count);
        Facing = facing;
        OnMoveCommand?.Invoke(Rows(mid, facing, cols, cols > 1 ? along.magnitude / (cols - 1) : 0f));
        ShowMarker(mid);
    }

    Vector3 Centroid()
    {
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (var u in selected) if (u != null) { sum += u.transform.position; n++; }
        return n > 0 ? sum / n : Vector3.zero;
    }

    // Slots for the current formation around center, facing from the group toward it. Sets Facing.
    List<Vector3> GetFormation(Vector3 center)
    {
        Vector3 facing = center - Centroid(); facing.y = 0f;
        facing = facing.sqrMagnitude > 0.01f ? facing.normalized : Vector3.forward;
        Facing = facing;
        int count = selected.Count;
        return formation switch
        {
            Formation.Line => Rows(center, facing, Mathf.Min(count, lineWidth), slotSpacing),
            Formation.Box => Rows(center, facing, Mathf.CeilToInt(Mathf.Sqrt(count)), slotSpacing),
            _ => GetCircleFormation(center, count),
        };
    }

    // Rows of `cols` units centred on center, front row first. Units are matched to slots so paths don't cross:
    // the front-most units take the front row, and go left-to-right within each row.
    List<Vector3> Rows(Vector3 center, Vector3 facing, int cols, float colSpacing)
    {
        int count = selected.Count;
        Vector3 right = Vector3.Cross(Vector3.up, facing);
        int rows = Mathf.CeilToInt(count / (float)cols);
        float Along(int i, Vector3 axis) => selected[i] != null ? Vector3.Dot(selected[i].transform.position, axis) : 0f;
        var order = new List<int>(count);
        for (int i = 0; i < count; i++) order.Add(i);
        order.Sort((x, y) => Along(y, facing).CompareTo(Along(x, facing)));

        var slots = new Vector3[count];
        for (int r = 0, start = 0; r < rows; r++, start += cols)
        {
            int n = Mathf.Min(cols, count - start);
            var row = order.GetRange(start, n);
            row.Sort((x, y) => Along(x, right).CompareTo(Along(y, right)));
            float back = (r - (rows - 1) * 0.5f) * slotSpacing;
            for (int c = 0; c < n; c++)
                slots[row[c]] = center + right * ((c - (n - 1) * 0.5f) * colSpacing) - facing * back;
        }
        return new List<Vector3>(slots);
    }

    void IssueMoveCommand(Vector2 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);

        // Only a production building selected: right-click sets where its new units go.
        if (selected.Count == 0)
        {
            if (selectedBuilding != null && Physics.Raycast(ray, out RaycastHit ground, maxRayDistance, groundLayer)) MoveSelectedTo(ground.point);
            return;
        }
        if (OnMoveCommand == null) return;

        if (Physics.Raycast(ray, out RaycastHit target, maxRayDistance, selectableLayer))
        {
            // Our outpost? Walk over and garrison it.
            if (target.collider.TryGetComponent(out OutpostCapture outpost) && outpost.ownerTeam == 0)
            {
                Facing = Vector3.zero;
                OnMoveCommand.Invoke(GetCircleFormation(outpost.transform.position, selected.Count));
                OnGarrisonCommand?.Invoke(outpost);
                return;
            }
            // An enemy (disabled Health = neutral outpost, just walk there)? Attack it instead of moving.
            if (target.collider.TryGetComponent(out Health enemy) && enemy.enabled && enemy.team != 0)
            {
                OnAttackCommand?.Invoke(enemy); // unarmed units (harvesters) ignore it
                return;
            }
        }

        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, groundLayer)) return;

        OnMoveCommand.Invoke(GetFormation(hit.point));

        // Clicked on ore? Workers switch from the move to mining it.
        if (Physics.Raycast(ray, out RaycastHit any, maxRayDistance) && any.collider.GetComponentInParent<OreNode>() is OreNode ore)
            OnHarvestCommand?.Invoke(ore);

        ShowMarker(hit.point);
    }

    // Plain move to a ground point (also used by the minimap): units go there, or it becomes the building's rally point.
    public void MoveSelectedTo(Vector3 point)
    {
        if (selected.Count > 0) OnMoveCommand?.Invoke(GetFormation(point));
        else if (selectedBuilding != null) selectedBuilding.Rally = point;
        else return;
        ShowMarker(point);
    }

    void ShowMarker(Vector3 point)
    {
        if (targetMarker == null) return;
        targetMarker.transform.position = point + Vector3.up * 0.05f; // avoid z-fighting
        targetMarker.SetActive(true);
        markerHideTime = Time.time + markerDuration;
    }

    // Ctrl+1..9 saves the selection; 1..9 brings it back.
    void ControlGroups()
    {
        var kb = Keyboard.current;
        if (kb == null || BuildingPlacer.MenuOpen) return; // digits pick buildings then
        bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
        for (int i = 1; i <= 9; i++)
        {
            if (!kb[Key.Digit0 + i].wasPressedThisFrame) continue;
            if (ctrl) groups[i] = selected.FindAll(u => u != null);
            else if (groups[i] != null) SelectOnly(groups[i]);
        }
    }

    // Replace the selection with these units (skips dead and garrisoned ones).
    public void SelectOnly(List<SelectableUnit> units)
    {
        var keep = units.FindAll(u => u != null && u.isActiveAndEnabled);
        DeselectAll();
        foreach (var u in keep) SelectUnit(u);
    }

    // Selection panel: click a unit type to keep only those.
    public void SelectOnly(UnitData type) => SelectOnly(selected.FindAll(u => u != null && u.Data == type));

    // One unit in the center, then rings of 6, 12, 18... growing outward.
    List<Vector3> GetCircleFormation(Vector3 center, int count)
    {
        var slots = new List<Vector3>(count);
        int remaining = count;
        int ring = 0;

        while (remaining > 0)
        {
            int onThisRing = Mathf.Min(ring == 0 ? 1 : Mathf.Max(6, ring * 6), remaining);
            float radius = ring == 0 ? 0f : minRadius + (ring - 1) * formationSpacing;

            for (int i = 0; i < onThisRing; i++)
            {
                float angle = i / (float)onThisRing * Mathf.PI * 2f;
                slots.Add(center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }

            remaining -= onThisRing;
            ring++;
        }
        return slots;
    }

    void SelectUnit(SelectableUnit unit)
    {
        if (selected.Contains(unit)) return;
        unit.Select(this, selected.Count); // its slot index in the formation
        selected.Add(unit);
    }

    void DeselectAll()
    {
        foreach (var unit in selected)
            if (unit != null) unit.Deselect(); // Unit unsubscribes itself. Skip destroyed units.
        selected.Clear();

        if (selectedBuilding != null)
        {
            selectedBuilding = null;
            OnBuildingSelected?.Invoke(null);
        }
        if (selectedStructure != null) selectedStructure.IsSelected = false;
        if (!ReferenceEquals(selectedStructure, null))
        {
            selectedStructure = null;
            OnStructureSelected?.Invoke(null);
        }
    }
}
