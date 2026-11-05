using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Building placement: pick a building (number keys), a hologram ghost follows the mouse on a grid,
/// green = valid / red = blocked, R rotates, left-click places (pays credits and starts a ConstructionSite),
/// right-click or Esc cancels. Hold Shift to keep placing the same building.
/// </summary>
public class BuildPlacer : MonoBehaviour
{
    [System.Serializable] public class Entry { public GameObject prefab; public int cost = 500; public float buildTime = 12f; }
    public List<Entry> buildable = new List<Entry>();
    public float gridSize = 0.5f;
    public bool showMenu = true;

    GameObject ghost; int current = -1; float yaw; bool valid;
    Vector3 boundsOffset, halfExtents; Vector3 placePos;
    Camera cam; string message = ""; float messageUntil;
    static readonly Color Ok = new Color(0.2f, 1f, 0.4f, 0.35f), Bad = new Color(1f, 0.2f, 0.2f, 0.35f);

    void Start() { cam = Camera.main; }

    public void Select(int i)
    {
        Cancel();
        if (i < 0 || i >= buildable.Count || !buildable[i].prefab) return;
        current = i;
        ghost = ConstructionSite.MakeHologram(buildable[i].prefab, Vector3.zero, Quaternion.Euler(0, yaw, 0), Ok);
        var b = ConstructionSite.RendererBounds(ghost);
        boundsOffset = b.center - ghost.transform.position;
        halfExtents = b.extents;
    }

    public void Cancel()
    {
        if (ghost) Destroy(ghost);
        ghost = null; current = -1;
    }

    void Update()
    {
        if (!cam) { cam = Camera.main; if (!cam) return; }
        Vector2 mouse = Vector2.zero; bool place = false, cancel = false, rotate = false, keep = false; int pick = -1;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current; var ms = Mouse.current;
        if (ms != null) { mouse = ms.position.ReadValue(); place = ms.leftButton.wasPressedThisFrame; cancel = ms.rightButton.wasPressedThisFrame; }
        if (kb != null)
        {
            cancel |= kb.escapeKey.wasPressedThisFrame; rotate = kb.rKey.wasPressedThisFrame; keep = kb.leftShiftKey.isPressed;
            Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
            for (int i = 0; i < keys.Length; i++) if (kb[keys[i]].wasPressedThisFrame) pick = i;
        }
#else
        mouse = Input.mousePosition; place = Input.GetMouseButtonDown(0); cancel = Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape);
        rotate = Input.GetKeyDown(KeyCode.R); keep = Input.GetKey(KeyCode.LeftShift);
        for (int i = 0; i < 9; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) pick = i;
#endif
        if (pick >= 0) Select(pick);
        if (!ghost) return;
        if (cancel) { Cancel(); return; }
        if (rotate)
        {
            yaw = (yaw + 90f) % 360f;
            ghost.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var b = ConstructionSite.RendererBounds(ghost);
            boundsOffset = b.center - ghost.transform.position; halfExtents = b.extents;
        }

        var ray = cam.ScreenPointToRay(mouse);
        if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float dist)) return;
        Vector3 p = ray.GetPoint(dist);
        p.x = Mathf.Round(p.x / gridSize) * gridSize; p.z = Mathf.Round(p.z / gridSize) * gridSize; p.y = 0f;
        placePos = p;
        ghost.transform.position = p;
        valid = IsFree(p);
        ConstructionSite.Tint(ghost, valid ? Ok : Bad);

        if (place)
        {
            if (!valid) { Say("Can't build there"); return; }
            var e = buildable[current];
            var bank = ResourceBank.Instance;
            if (bank && !bank.Spend(e.cost)) { Say("Not enough credits"); return; }
            ConstructionSite.Begin(e.prefab, placePos, Quaternion.Euler(0, yaw, 0), e.buildTime);
            if (!keep) Cancel();
        }
    }

    bool IsFree(Vector3 p)
    {
        Vector3 c = p + boundsOffset;
        Vector3 he = new Vector3(halfExtents.x * 0.9f, halfExtents.y * 0.45f, halfExtents.z * 0.9f);
        c.y = he.y + 0.1f;   // keep the test box above the ground plane
        if (Physics.CheckBox(c, he, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) return false;
        var mine = new Bounds(c, he * 2f);
        foreach (var s in ConstructionSite.All) if (s && s.WorldBounds.Intersects(mine)) return false;
        return true;
    }

    void Say(string m) { message = m; messageUntil = Time.time + 1.5f; }

    void OnGUI()
    {
        if (Time.time < messageUntil) GUI.Label(new Rect(Screen.width / 2f - 120, Screen.height - 80, 240, 24), message);
        if (!showMenu) return;
        GUILayout.BeginArea(new Rect(12, 60, 300, 400));
        GUILayout.Label("BUILD  (number key, R rotate, LMB place, RMB cancel, Shift = keep placing)");
        for (int i = 0; i < buildable.Count && i < 9; i++)
            if (buildable[i].prefab)
                GUILayout.Label($"{(i == current ? "> " : "  ")}{i + 1}. {buildable[i].prefab.name}  ·  {buildable[i].cost} cr  ·  {buildable[i].buildTime:0}s");
        GUILayout.EndArea();
    }
}
