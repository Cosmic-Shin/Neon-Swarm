using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Picks the mouse cursor (CursorManager) each frame from what a click would do right now, mirroring the orders in
// GoodSelectionManager (it adds this next to itself and lends its camera, layers and reach). Priority:
//   1. over UI -> Default            2. at a screen edge -> scroll arrow (the camera's own edge direction)
//   3. a mode: placing a building (Build / Invalid), attack-move armed (Attack over anything), patrol armed (Move)
//   4. units selected: enemy -> Attack (Invalid if none can hit it), ore + harvester -> Harvest,
//      neutral / enemy-held outpost -> Capture, own outpost or own Wasp with passengers that fit -> Load,
//      own unit -> Select, walkable ground -> Move, else Invalid
//   5. only a production building selected: ground -> Rally   6. otherwise own unit/building -> Select, else Default
// CursorManager caches, so Set every frame is cheap. Hovering needs its own raycasts: the selection code only
// casts on clicks.
public class CursorContext : MonoBehaviour
{
    GoodSelectionManager manager;

    void Awake() => manager = GetComponent<GoodSelectionManager>();

    void OnDisable() { if (CursorManager.Instance) CursorManager.Set(CursorKind.Default); } // menus, results screen, scene change (not while quitting: that would make a new manager)

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || manager == null || manager.Cam == null) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) { CursorManager.Set(CursorKind.Default); return; }
        if (CameraPan.EdgeDir != Vector2.zero) { CursorManager.SetScroll(CameraPan.EdgeDir); return; }
        CursorManager.Set(Pick(mouse.position.ReadValue()));
    }

    CursorKind Pick(Vector2 screen)
    {
        if (BuildingPlacer.Placing) return BuildingPlacer.PlacementOk ? CursorKind.Build : CursorKind.Invalid;

        Ray ray = manager.Cam.ScreenPointToRay(screen);
        Physics.Raycast(ray, out RaycastHit target, manager.MaxRayDistance, manager.SelectableLayer);
        var col = target.collider;
        var units = manager.SelectedUnits;
        bool haveUnits = GoodSelectionManager.UnitsSelected;

        if (manager.AttackMoveArmed) return CursorKind.Attack; // A: the click attacks whatever is there
        if (manager.PatrolArmed) return OnGround(ray, out _) ? CursorKind.Move : CursorKind.Invalid;

        if (haveUnits)
        {
            if (col != null)
            {
                if (col.TryGetComponent(out OutpostCapture outpost))
                {
                    if (outpost.ownerTeam == 0) return CursorKind.Load; // garrison it
                    bool defended = col.TryGetComponent(out Health oh) && oh.enabled;
                    if (!defended) return AnyGround(units) ? CursorKind.Capture : CursorKind.Invalid; // stand in it to take it
                }
                if (col.TryGetComponent(out Transport wasp) && col.TryGetComponent(out SelectableUnit waspUnit) && !waspUnit.IsEnemy)
                    return AnyBoards(units, waspUnit) ? CursorKind.Load : CursorKind.Select;
                if (col.TryGetComponent(out Health enemy) && enemy.enabled && !Net.Allied(enemy.team, 0))
                    return AnyCanHit(units, enemy) ? CursorKind.Attack : CursorKind.Invalid;
                if (col.TryGetComponent(out SelectableUnit own) && !own.IsEnemy) return CursorKind.Select;
                if (col.GetComponentInParent<Structure>() is Structure s && TeamEconomy.TeamOf(s) == 0) return CursorKind.Select;
            }
            if (AnyHarvester(units) && OreUnder(ray)) return CursorKind.Harvest;
            if (!OnGround(ray, out Vector3 p)) return CursorKind.Invalid; // off the map
            return Walkable(p) || AllFly(units) ? CursorKind.Move : CursorKind.Invalid;
        }

        if (col != null && Own(col)) return CursorKind.Select;
        if (manager.SelectedBuilding != null) return OnGround(ray, out _) ? CursorKind.Rally : CursorKind.Default; // right-click sets the rally point
        return CursorKind.Default;
    }

    static bool Own(Collider c) =>
        (c.TryGetComponent(out SelectableUnit u) && !u.IsEnemy) ||
        (c.GetComponentInParent<Structure>() is Structure s && TeamEconomy.TeamOf(s) == 0);

    bool OnGround(Ray ray, out Vector3 point)
    {
        bool hit = Physics.Raycast(ray, out RaycastHit h, manager.MaxRayDistance, manager.GroundLayer);
        point = h.point;
        return hit;
    }

    static bool Walkable(Vector3 p) => NavMesh.SamplePosition(p, out _, 2f, NavMesh.AllAreas);

    bool OreUnder(Ray ray) =>
        Physics.Raycast(ray, out RaycastHit any, manager.MaxRayDistance) && any.collider.GetComponentInParent<OreNode>() != null;

    static bool AnyCanHit(System.Collections.Generic.IReadOnlyList<SelectableUnit> units, Health target)
    {
        foreach (var u in units) if (u != null && u.TryGetComponent(out Attacker a) && a.CanHit(target)) return true;
        return false;
    }

    static bool AnyHarvester(System.Collections.Generic.IReadOnlyList<SelectableUnit> units)
    {
        foreach (var u in units) if (u != null && u.TryGetComponent(out Harvester _)) return true;
        return false;
    }

    static bool AnyBoards(System.Collections.Generic.IReadOnlyList<SelectableUnit> units, SelectableUnit wasp)
    {
        foreach (var u in units) if (u != null && u != wasp && Transport.CanCarry(u)) return true;
        return false;
    }

    static bool AnyGround(System.Collections.Generic.IReadOnlyList<SelectableUnit> units)
    {
        foreach (var u in units) if (u != null && !Flyer.IsAir(u)) return true;
        return false;
    }

    static bool AllFly(System.Collections.Generic.IReadOnlyList<SelectableUnit> units)
    {
        foreach (var u in units) if (u != null && !Flyer.IsAir(u)) return false;
        return true;
    }
}
