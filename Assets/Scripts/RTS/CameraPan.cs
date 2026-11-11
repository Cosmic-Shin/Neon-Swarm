using UnityEngine;
using UnityEngine.InputSystem;

// C&C-style camera: WASD / arrows / screen edge to scroll, mouse wheel to zoom.
public class CameraPan : MonoBehaviour
{
    [SerializeField] float speed = 60f;
    [SerializeField] float edgeSize = 12f;   // px from the screen edge that scrolls
    [SerializeField] float zoomStep = 4f;
    [SerializeField] float minHeight = 25f;
    [SerializeField] float maxHeight = 70f;
    [SerializeField] Vector2 minXZ = new(-100f, -130f);
    [SerializeField] Vector2 maxXZ = new(100f, 70f);

    bool mouseMoved;

    static CameraPan instance;

    void Awake()
    {
        instance = this;
        gameObject.AddComponent<Ambience>(); // rain, ore glints, city life around the view
        gameObject.AddComponent<MapDressing>(); // ground detail, roads, traffic, holo ads, searchlights, ore glow, base pads
    }

    // Camera position p moved so the screen centre looks at 'spot'.
    Vector3 Centered(Vector3 p, Vector3 spot)
    {
        if (transform.forward.y > -0.01f) return p;
        Vector3 focus = p + transform.forward * (-p.y / transform.forward.y);
        p += new Vector3(spot.x - focus.x, 0f, spot.z - focus.z);
        p.x = Mathf.Clamp(p.x, minXZ.x, maxXZ.x);
        p.z = Mathf.Clamp(p.z, minXZ.y, maxXZ.y);
        return p;
    }

    // Jump the camera to look at a spot (idle button, production overview).
    public static void LookAt(Vector3 spot) { if (instance != null) instance.transform.position = instance.Centered(instance.transform.position, spot); }
    // The ground point at the middle of the screen.
    public static Vector3 LookingAt
    {
        get
        {
            if (instance == null) return Vector3.zero;
            var t = instance.transform;
            return t.forward.y < -0.01f ? t.position + t.forward * (-t.position.y / t.forward.y) : t.position;
        }
    }

    void Update()
    {
        var kb = Chat.Keys;
        var mouse = Mouse.current;
        if (kb == null) return;

        // With units selected A/S are orders (attack-move, stop), so the letters stop panning; arrows always pan.
        bool letters = !GoodSelectionManager.UnitsSelected;
        Vector2 dir = Vector2.zero;
        if ((letters && kb.wKey.isPressed) || kb.upArrowKey.isPressed) dir.y += 1f;
        if ((letters && kb.sKey.isPressed) || kb.downArrowKey.isPressed) dir.y -= 1f;
        if ((letters && kb.dKey.isPressed) || kb.rightArrowKey.isPressed) dir.x += 1f;
        if ((letters && kb.aKey.isPressed) || kb.leftArrowKey.isPressed) dir.x -= 1f;

        Vector3 p = transform.position;
        if (mouse != null)
        {
            // Mouse reads (0,0) until it first moves over the game view; don't treat that as the corner.
            if (mouse.delta.ReadValue() != Vector2.zero) mouseMoved = true;
            Vector2 m = mouse.position.ReadValue();
            bool inside = mouseMoved && Application.isFocused && m.x >= 0 && m.y >= 0 && m.x <= Screen.width && m.y <= Screen.height;
            if (inside)
            {
                if (m.x < edgeSize) dir.x -= 1f;
                if (m.x > Screen.width - edgeSize) dir.x += 1f;
                if (m.y < edgeSize) dir.y -= 1f;
                if (m.y > Screen.height - edgeSize) dir.y += 1f;
            }
            float scroll = mouse.scroll.ReadValue().y;
            bool overUI = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            // Wheel over a panel scrolls the panel instead of zooming.
            if (scroll != 0f && !overUI) p.y = Mathf.Clamp(p.y - Mathf.Sign(scroll) * zoomStep, minHeight, maxHeight);
        }

        // Space: look at where the last alert happened (attack, construction done).
        if (kb.spaceKey.wasPressedThisFrame && Alerts.LastSpot is Vector3 spot) p = Centered(p, spot);

        dir = Vector2.ClampMagnitude(dir, 1f);
        p += new Vector3(dir.x, 0f, dir.y) * speed * GameSettings.CameraSpeed * Time.unscaledDeltaTime;
        p.x = Mathf.Clamp(p.x, minXZ.x, maxXZ.x);
        p.z = Mathf.Clamp(p.z, minXZ.y, maxXZ.y);
        transform.position = p;
    }
}
