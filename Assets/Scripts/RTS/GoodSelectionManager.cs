using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class GoodSelectionManager : MonoBehaviour
{
    public event Action<List<Vector3>> OnMoveCommand;
    public event Action<Building> OnBuildingSelected;
    public event Action<Structure> OnStructureSelected;
    public event Action<OreNode> OnHarvestCommand;
    public event Action<Health> OnAttackCommand;
    public event Action<OutpostCapture> OnGarrisonCommand;
    public event Action<Transport> OnBoardCommand;
    public event Action<List<Vector3>> OnAttackMoveCommand;
    public event Action<List<Vector3>> OnPatrolCommand;
    public event Action OnStopCommand;
    public event Action OnHoldCommand;

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
    [SerializeField] float formationSpacing = 2f;
    [SerializeField] float minRadius = 1.5f;
    [SerializeField] float slotSpacing = 2.5f;
    [SerializeField] int lineWidth = 10;
    [SerializeField] Formation formation = Formation.Circle;

    readonly List<SelectableUnit> selected = new();
    public static bool UnitsSelected { get; private set; }
    public Vector3 Facing { get; private set; }
    bool attackMoveArmed;
    bool patrolArmed;
    public bool Queueing => Chat.Keys is Keyboard kb && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
    bool rightPressing, rightDragging;
    Vector2 rightStart;
    LineRenderer dragLine;
    public IReadOnlyList<SelectableUnit> SelectedUnits => selected;
    public Camera Cam => cam;
    public LayerMask SelectableLayer => selectableLayer;
    public LayerMask GroundLayer => groundLayer;
    public float MaxRayDistance => maxRayDistance;
    public Building SelectedBuilding => selectedBuilding;
    Vector2 dragStart;
    bool isPressing;
    bool isDragging;
    float markerHideTime;
    Building selectedBuilding;
    Structure selectedStructure;
    public Structure SelectedStructure => selectedStructure;

    readonly List<SelectableUnit>[] groups = new List<SelectableUnit>[10];
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
        if (!TryGetComponent(out CursorContext _)) gameObject.AddComponent<CursorContext>();
    }

    void OnDisable()
    {
        UnitsSelected = rightPressing = rightDragging = attackMoveArmed = patrolArmed = false;
        if (dragLine != null) dragLine.enabled = false;
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;
        Vector2 mousePos = mouse.position.ReadValue();

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        UnitsSelected = selected.Exists(u => u != null);

        OrderKeys();

        if (attackMoveArmed && mouse.leftButton.wasPressedThisFrame && !overUI)
        {
            attackMoveArmed = Queueing;
            IssueAttackMove(mousePos);
        }
        else if (patrolArmed && mouse.leftButton.wasPressedThisFrame && !overUI)
        {
            patrolArmed = false;
            if (GroundAt(mousePos, out Vector3 p)) { OnPatrolCommand?.Invoke(GetFormation(p)); ShowMarker(p, Juice.Gold); }
        }
        else if (mouse.leftButton.wasPressedThisFrame && !overUI && !(Net.Online && Ping.AltHeld))
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
            if (selected.Exists(u => u != null) || selectedStructure != null) Sfx.Play(Snd.Select, 0.45f);
        }

        if (mouse.rightButton.wasPressedThisFrame && !overUI)
        {
            if (attackMoveArmed || patrolArmed) attackMoveArmed = patrolArmed = false;
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
                bool doubleClick = Time.unscaledTime - lastClickTime < 0.35f && lastClickType == unit.Data;
                lastClickTime = Time.unscaledTime;
                lastClickType = unit.Data;
                if (!doubleClick) { SelectUnit(unit); return; }
                foreach (var u in FindObjectsByType<SelectableUnit>(FindObjectsSortMode.None))
                {
                    Vector3 sp = cam.WorldToScreenPoint(Flyer.Center(u));
                    bool onScreen = sp.z > 0f && sp.x >= 0f && sp.y >= 0f && sp.x <= Screen.width && sp.y <= Screen.height;
                    if (onScreen && !u.IsEnemy && u.Data == unit.Data) SelectUnit(u);
                }
            }
            else if (hit.collider.TryGetComponent(out OutpostCapture outpost) && outpost.ownerTeam == 0)
            {
                var inside = new List<TeamMember>(outpost.Garrison);
                outpost.EjectAll();
                foreach (var t in inside) if (t != null && t.TryGetComponent(out SelectableUnit u)) SelectUnit(u);
            }
            else if (hit.collider.TryGetComponent(out Structure structure) && structure.enabled && structure.Health.team == 0)
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
            Vector3 sp = cam.WorldToScreenPoint(Flyer.Center(unit));
            if (sp.z > 0f && screenRect.Contains(sp) && !unit.IsEnemy)
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

    void OrderKeys()
    {
        var kb = Chat.Keys;
        if (kb == null) return;
        if (kb.fKey.wasPressedThisFrame) NextFormation();
        if (kb.escapeKey.wasPressedThisFrame) attackMoveArmed = patrolArmed = false;
        if (kb.periodKey.wasPressedThisFrame) NextIdle();
        if (!UnitsSelected) { attackMoveArmed = patrolArmed = false; return; }
        if (kb.aKey.wasPressedThisFrame) ArmAttackMove();
        if (kb.pKey.wasPressedThisFrame) ArmPatrol();
        if (kb.sKey.wasPressedThisFrame) StopOrder();
        if (kb.hKey.wasPressedThisFrame) HoldOrder();
    }

    public Formation CurrentFormation => formation;
    public bool AttackMoveArmed => attackMoveArmed;
    public bool PatrolArmed => patrolArmed;

    public void NextFormation()
    {
        formation = (Formation)(((int)formation + 1) % 3);
        Alerts.Show($"Formation: {formation}", Alerts.Info);
    }
    public void ArmAttackMove() { attackMoveArmed = true; patrolArmed = false; Alerts.Show("Attack-move: left-click a spot", Alerts.Warn); }
    public void ArmPatrol() { patrolArmed = true; attackMoveArmed = false; Alerts.Show("Patrol: left-click the far end", Alerts.Warn); }
    public void StopOrder() { attackMoveArmed = patrolArmed = false; OnStopCommand?.Invoke(); }
    public void HoldOrder() { attackMoveArmed = patrolArmed = false; OnHoldCommand?.Invoke(); }

    public List<SelectableUnit> Group(int i) => groups[i];
    public void SelectGroup(int i) { if (groups[i] != null) SelectOnly(groups[i]); }

    public void SelectOne(SelectableUnit u) { DeselectAll(); if (u != null) SelectUnit(u); }

    public void SelectStructure(Structure structure)
    {
        DeselectAll();
        if (structure == null || !structure.enabled) return;
        selectedStructure = structure;
        structure.IsSelected = true;
        OnStructureSelected?.Invoke(structure);
        if (structure.TryGetComponent(out Building building) && building.enabled) { selectedBuilding = building; OnBuildingSelected?.Invoke(building); }
    }

    public static bool IsIdle(SelectableUnit u)
    {
        if (u == null || !u.isActiveAndEnabled || u.IsEnemy || u.IsRemote || !u.TryGetComponent(out Health h) || !h.enabled) return false;
        if (u.TryGetComponent(out Harvester hv)) return hv.enabled && hv.state == Harvester.State.Idle;
        if (!u.TryGetComponent(out UnityEngine.AI.NavMeshAgent a) || !a.enabled || a.pathPending || a.hasPath) return false;
        return !u.TryGetComponent(out Attacker at) || !at.Busy;
    }

    int idleCursor;

    public void NextIdle()
    {
        var idle = new List<SelectableUnit>();
        foreach (var u in FindObjectsByType<SelectableUnit>(FindObjectsSortMode.InstanceID)) if (IsIdle(u)) idle.Add(u);
        if (idle.Count == 0) { Alerts.Show("No idle units", Alerts.Info, 1f); return; }
        var pick = idle[idleCursor++ % idle.Count];
        SelectOne(pick);
        CameraPan.LookAt(pick.transform.position);
    }

    void IssueAttackMove(Vector2 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit target, maxRayDistance, selectableLayer) &&
            target.collider.TryGetComponent(out Health enemy) && enemy.enabled && !Net.Allied(enemy.team, 0))
        {
            OnAttackCommand?.Invoke(enemy);
            Juice.TargetLock(enemy);
            return;
        }
        if (!GroundAt(screenPos, out Vector3 point)) return;
        OnAttackMoveCommand?.Invoke(GetFormation(point));
        ShowMarker(point, Juice.Danger);
    }

    void LineMove(Vector3 a, Vector3 b)
    {
        Vector3 along = b - a; along.y = 0f;
        Vector3 mid = (a + b) * 0.5f;
        Vector3 facing = Vector3.Cross(along.normalized, Vector3.up);
        if (Vector3.Dot(facing, mid - Centroid()) < 0f) facing = -facing;
        int cols = Mathf.Clamp(Mathf.FloorToInt(along.magnitude / Gap()) + 1, 1, selected.Count);
        Facing = facing;
        OnMoveCommand?.Invoke(Rows(mid, facing, cols, cols > 1 ? along.magnitude / (cols - 1) : 0f));
        ShowMarker(mid);
    }

    float Gap()
    {
        float widest = 0f;
        foreach (var u in selected)
            if (u != null && u.TryGetComponent(out UnityEngine.AI.NavMeshAgent a)) widest = Mathf.Max(widest, Flyer.IsAir(u) ? Flyer.SizeOf(u) * 1.6f : a.radius * 2f);
        return Mathf.Max(slotSpacing, widest + 0.8f);
    }

    Vector3 Centroid()
    {
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (var u in selected) if (u != null) { sum += u.transform.position; n++; }
        return n > 0 ? sum / n : Vector3.zero;
    }

    List<Vector3> GetFormation(Vector3 center)
    {
        Vector3 facing = center - Centroid(); facing.y = 0f;
        facing = facing.sqrMagnitude > 0.01f ? facing.normalized : Vector3.forward;
        Facing = facing;
        int count = selected.Count;
        return formation switch
        {
            Formation.Line => Rows(center, facing, Mathf.Min(count, lineWidth), Gap()),
            Formation.Box => Rows(center, facing, Mathf.CeilToInt(Mathf.Sqrt(count)), Gap()),
            _ => GetCircleFormation(center, count),
        };
    }

    List<Vector3> Rows(Vector3 center, Vector3 facing, int cols, float colSpacing)
    {
        int count = selected.Count;
        Vector3 right = Vector3.Cross(Vector3.up, facing);
        int rows = Mathf.CeilToInt(count / (float)cols);
        float rowGap = Gap();
        float Along(int i, Vector3 axis) => selected[i] != null ? Vector3.Dot(selected[i].transform.position, axis) : 0f;
        var order = ByKind();
        order.Sort((x, y) =>
        {
            int k = KindOrder(x, y);
            return k != 0 ? k : Along(y, facing).CompareTo(Along(x, facing));
        });

        var slots = new Vector3[count];
        for (int r = 0, start = 0; r < rows; r++, start += cols)
        {
            int n = Mathf.Min(cols, count - start);
            var row = order.GetRange(start, n);
            row.Sort((x, y) => { int k = KindOrder(x, y); return k != 0 ? k : Along(x, right).CompareTo(Along(y, right)); });
            float back = (r - (rows - 1) * 0.5f) * rowGap;
            for (int c = 0; c < n; c++)
                slots[row[c]] = center + right * ((c - (n - 1) * 0.5f) * colSpacing) - facing * back;
        }
        return new List<Vector3>(slots);
    }

    void IssueMoveCommand(Vector2 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (selected.Count == 0)
        {
            if (selectedBuilding != null && Physics.Raycast(ray, out RaycastHit ground, maxRayDistance, groundLayer)) MoveSelectedTo(ground.point);
            return;
        }
        if (OnMoveCommand == null) return;

        if (Physics.Raycast(ray, out RaycastHit target, maxRayDistance, selectableLayer))
        {
            if (target.collider.TryGetComponent(out OutpostCapture outpost) && outpost.ownerTeam == 0)
            {
                Facing = Vector3.zero;
                OnMoveCommand.Invoke(GetCircleFormation(outpost.transform.position, selected.Count));
                if (!Net.Online) OnGarrisonCommand?.Invoke(outpost);
                return;
            }
            if (target.collider.TryGetComponent(out Transport wasp) && target.collider.TryGetComponent(out SelectableUnit waspUnit) && !waspUnit.IsEnemy)
            {
                Facing = Vector3.zero;
                OnMoveCommand.Invoke(GetCircleFormation(wasp.transform.position, selected.Count));
                OnBoardCommand?.Invoke(wasp);
                return;
            }
            if (target.collider.TryGetComponent(out Health enemy) && enemy.enabled && !Net.Allied(enemy.team, 0))
            {
                OnAttackCommand?.Invoke(enemy);
                Juice.TargetLock(enemy);
                return;
            }
        }

        if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, groundLayer)) return;

        OnMoveCommand.Invoke(GetFormation(hit.point));

        if (Physics.Raycast(ray, out RaycastHit any, maxRayDistance) && any.collider.GetComponentInParent<OreNode>() is OreNode ore)
            OnHarvestCommand?.Invoke(ore);

        ShowMarker(hit.point);
    }

    public void MoveSelectedTo(Vector3 point)
    {
        if (selected.Count > 0) OnMoveCommand?.Invoke(GetFormation(point));
        else if (selectedBuilding != null) selectedBuilding.Rally = point;
        else return;
        ShowMarker(point);
    }

    static readonly Color MoveColor = new(0.3f, 1f, 1f);

    void ShowMarker(Vector3 point, Color? color = null)
    {
        Sfx.Play(Snd.Ack, 0.5f);
        Color c = color ?? MoveColor;
        Juice.MoveMarker(point, c);
        foreach (var u in selected) if (u != null) Juice.OrderLine(Flyer.Center(u), point, c);
        if (targetMarker == null) return;
        targetMarker.transform.position = point + Vector3.up * 0.05f;
        targetMarker.SetActive(true);
        markerHideTime = Time.time + markerDuration;
    }

    void ControlGroups()
    {
        var kb = Chat.Keys;
        if (kb == null || BuildingPlacer.MenuOpen) return;
        bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
        for (int i = 1; i <= 9; i++)
        {
            if (!kb[Key.Digit0 + i].wasPressedThisFrame) continue;
            if (ctrl) groups[i] = selected.FindAll(u => u != null);
            else if (groups[i] != null) SelectOnly(groups[i]);
        }
    }

    public void SelectOnly(List<SelectableUnit> units)
    {
        var keep = units.FindAll(u => u != null && u.isActiveAndEnabled);
        DeselectAll();
        foreach (var u in keep) SelectUnit(u);
    }

    public void SelectOnly(UnitData type) => SelectOnly(selected.FindAll(u => u != null && u.Data == type));

    static int Rank(UnitData d) => d == null ? 9 : d.role switch
    {
        UnitData.Role.Heavy => 0, UnitData.Role.Elite => 1, UnitData.Role.Infantry => 2,
        UnitData.Role.Scout => 3, UnitData.Role.Support => 4, _ => 5,
    };

    List<int> ByKind()
    {
        var order = new List<int>(selected.Count);
        for (int i = 0; i < selected.Count; i++) order.Add(i);
        return order;
    }

    int KindOrder(int x, int y)
    {
        UnitData a = selected[x] != null ? selected[x].Data : null, b = selected[y] != null ? selected[y].Data : null;
        int r = Rank(a).CompareTo(Rank(b));
        return r != 0 ? r : string.CompareOrdinal(a != null ? a.name : "", b != null ? b.name : "");
    }

    List<Vector3> GetCircleFormation(Vector3 center, int count)
    {
        var raw = RingSlots(center, count);
        if (count != selected.Count) return raw;
        float SlotAngle(Vector3 p) => (p - center).sqrMagnitude < 0.01f ? -10f : Mathf.Atan2(p.z - center.z, p.x - center.x);
        raw.Sort((p, q) => SlotAngle(p).CompareTo(SlotAngle(q)));
        var order = ByKind();
        float Angle(int i) => selected[i] != null ? Mathf.Atan2(selected[i].transform.position.z - center.z, selected[i].transform.position.x - center.x) : 0f;
        order.Sort((x, y) => { int k = KindOrder(x, y); return k != 0 ? k : Angle(x).CompareTo(Angle(y)); });
        var slots = new Vector3[count];
        for (int k = 0; k < count; k++) slots[order[k]] = raw[k];
        return new List<Vector3>(slots);
    }

    List<Vector3> RingSlots(Vector3 center, int count)
    {
        var slots = new List<Vector3>(count);
        int remaining = count;
        int ring = 0;
        float gap = Gap();

        while (remaining > 0)
        {
            float radius = ring == 0 ? 0f : Mathf.Max(minRadius, gap) + (ring - 1) * Mathf.Max(formationSpacing, gap);
            int fits = ring == 0 ? 1 : Mathf.Max(1, Mathf.FloorToInt(2f * Mathf.PI * radius / gap));
            int onThisRing = Mathf.Min(fits, remaining);

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
        if (unit.TryGetComponent(out Health h) && !h.enabled) return;
        unit.Select(this, selected.Count);
        selected.Add(unit);
    }

    void DeselectAll()
    {
        foreach (var unit in selected)
            if (unit != null) unit.Deselect();
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
