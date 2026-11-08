using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Test controls for the air demo: 1-6 build a flyer at the Aerie, right-click sends all flyers, L releases Locust drones.</summary>
public class AirDemo : MonoBehaviour
{
    public AirPad pad;
    Camera cam;
    void Start() { cam = Camera.main; if (!pad) pad = FindFirstObjectByType<AirPad>(); }
    void Update()
    {
        if (!cam) { cam = Camera.main; if (!cam) return; }
        int pick = -1; bool send = false, release = false; Vector2 mouse = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current; var ms = Mouse.current;
        if (kb != null)
        {
            Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
            for (int i = 0; i < keys.Length; i++) if (kb[keys[i]].wasPressedThisFrame) pick = i;
            release = kb.lKey.wasPressedThisFrame;
        }
        if (ms != null) { send = ms.rightButton.wasPressedThisFrame; mouse = ms.position.ReadValue(); }
#else
        for (int i = 0; i < 9; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) pick = i;
        release = Input.GetKeyDown(KeyCode.L); send = Input.GetMouseButtonDown(1); mouse = Input.mousePosition;
#endif
        if (pick >= 0 && pad) pad.Produce(pick);
        if (send)
        {
            var ray = cam.ScreenPointToRay(mouse);
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float d))
            {
                Vector3 p = ray.GetPoint(d); int i = 0;
                foreach (var f in FindObjectsByType<FlyingUnit>(FindObjectsSortMode.None))
                    if (f.enabled) f.SetDestination(p + Quaternion.Euler(0f, i * 47f, 0f) * Vector3.forward * (0.6f * Mathf.Sqrt(i++)));
            }
        }
        if (release) foreach (var c in FindObjectsByType<SwarmCarrier>(FindObjectsSortMode.None)) c.Release();
    }
    void OnGUI()
    {
        GUI.Label(new Rect(12, 34, 1100, 24), "1 Dragonfly  2 Hornet  3 Beetle  4 Locust  5 Firefly  6 Wasp  ·  Titans (Aerie Mk III): 7 Jellyfish  8 Mothership  9 Manta  ·  U: upgrade Aerie  ·  RMB: send flyers  ·  L: Locust drones");
    }
}
