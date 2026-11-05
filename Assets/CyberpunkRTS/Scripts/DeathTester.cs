using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Test controls for the Cyberpunk RTS test scene: pan/zoom camera, click to kill, K kills nearest.</summary>
public class DeathTester : MonoBehaviour
{
    public float panSpeed = 14f;
    public float zoomStep = 2.5f;
    public bool clickToKill = true;
    Camera cam;
    bool walking;
    static readonly int SpeedId = Animator.StringToHash("Speed");

    void Start() { cam = Camera.main; }

    void Update()
    {
        if (!cam) { cam = Camera.main; if (!cam) return; }
        Vector2 move = Vector2.zero; float zoom = 0f; bool click = false, kill = false; Vector2 mouse = Vector2.zero; bool fast = false;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current; var ms = Mouse.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1;
            kill = kb.kKey.wasPressedThisFrame;
            if (kb.spaceKey.wasPressedThisFrame) ToggleWalk();
            if (kb.uKey.wasPressedThisFrame) UpgradeNearest();
            fast = kb.leftShiftKey.isPressed;
        }
        if (ms != null)
        {
            float s = ms.scroll.ReadValue().y;
            zoom = Mathf.Abs(s) > 0.01f ? Mathf.Sign(s) : 0f;
            click = ms.leftButton.wasPressedThisFrame;
            mouse = ms.position.ReadValue();
        }
#else
        move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        zoom = Mathf.Sign(Input.mouseScrollDelta.y) * (Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f ? 1 : 0);
        click = Input.GetMouseButtonDown(0); kill = Input.GetKeyDown(KeyCode.K);
        mouse = Input.mousePosition; fast = Input.GetKey(KeyCode.LeftShift);
        if (Input.GetKeyDown(KeyCode.Space)) ToggleWalk();
        if (Input.GetKeyDown(KeyCode.U)) UpgradeNearest();
#endif
        var t = cam.transform;
        Vector3 fwd = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
        t.position += (t.right * move.x + fwd * move.y) * panSpeed * (fast ? 3f : 1f) * Time.deltaTime;
        if (zoom != 0f) t.position += t.forward * zoom * zoomStep;

        if (clickToKill && click && Physics.Raycast(cam.ScreenPointToRay(mouse), out var hit, 1000f))
        {
            var d = hit.collider.GetComponentInParent<UnitDeath>();
            if (d) d.Die();
        }
        if (kill)
        {
            var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            var ground = new Plane(Vector3.up, Vector3.zero);
            Vector3 p = ground.Raycast(ray, out float dist) ? ray.GetPoint(dist) : t.position;
            UnitDeath best = null; float bestD = float.MaxValue;
            foreach (var d in FindObjectsByType<UnitDeath>(FindObjectsSortMode.None))
            {
                if (d.IsDead) continue;
                float dd = (d.transform.position - p).sqrMagnitude;
                if (dd < bestD) { bestD = dd; best = d; }
            }
            if (best) best.Die();
        }
    }

    void UpgradeNearest()
    {
        var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        var ground = new Plane(Vector3.up, Vector3.zero);
        Vector3 p = ground.Raycast(ray, out float dist) ? ray.GetPoint(dist) : cam.transform.position;
        BuildingUpgrade best = null; float bestD = float.MaxValue;
        foreach (var b in FindObjectsByType<BuildingUpgrade>(FindObjectsSortMode.None))
        {
            if (!b.CanUpgrade) continue;
            float d = (b.transform.position - p).sqrMagnitude;
            if (d < bestD) { bestD = d; best = b; }
        }
        if (best) best.SetLevel(best.level + 1, true);   // test key: instant, free
    }

    void ToggleWalk()
    {
        walking = !walking;
        foreach (var a in FindObjectsByType<Animator>(FindObjectsSortMode.None))
            if (a && a.runtimeAnimatorController) a.SetFloat(SpeedId, walking ? 1f : 0f);
    }

    void OnGUI()
    {
        GUI.Label(new Rect(12, 10, 900, 24), "WASD / arrows: pan (Shift = fast)    Scroll: zoom    Space: toggle walk / idle    U: upgrade nearest building    Click a unit or building: kill it    K: kill nearest");
    }
}
