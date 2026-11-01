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

    readonly List<SelectableUnit> selected = new();
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
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 mousePos = mouse.position.ReadValue();

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (mouse.leftButton.wasPressedThisFrame && !overUI)
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

        if (mouse.rightButton.wasPressedThisFrame && !overUI) IssueMoveCommand(mousePos);

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

        OnMoveCommand.Invoke(GetCircleFormation(hit.point, selected.Count));

        // Clicked on ore? Workers switch from the move to mining it.
        if (Physics.Raycast(ray, out RaycastHit any, maxRayDistance) && any.collider.GetComponentInParent<OreNode>() is OreNode ore)
            OnHarvestCommand?.Invoke(ore);

        ShowMarker(hit.point);
    }

    // Plain move to a ground point (also used by the minimap): units go there, or it becomes the building's rally point.
    public void MoveSelectedTo(Vector3 point)
    {
        if (selected.Count > 0) OnMoveCommand?.Invoke(GetCircleFormation(point, selected.Count));
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
        if (kb == null) return;
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
