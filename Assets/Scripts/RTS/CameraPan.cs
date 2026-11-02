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

    void Update()
    {
        var kb = Keyboard.current;
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

        dir = Vector2.ClampMagnitude(dir, 1f);
        p += new Vector3(dir.x, 0f, dir.y) * speed * Time.unscaledDeltaTime;
        p.x = Mathf.Clamp(p.x, minXZ.x, maxXZ.x);
        p.z = Mathf.Clamp(p.z, minXZ.y, maxXZ.y);
        transform.position = p;
    }
}
