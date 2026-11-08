using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Map pings (multiplayer): Alt+click the ground or the minimap to flash a spot for your allies:
// a pulsing ring and light beam in your colour, a pulse on the minimap and a line in the alerts.
// RoomState sends it (RPC_Ping); allies' PCs call Show.
public class Ping : MonoBehaviour
{
    const float Life = 4f, Cooldown = 1f;
    public static readonly List<(Vector3 at, Color color, float born)> Active = new(); // the minimap draws these

    [SerializeField] LayerMask groundLayer;
    static Material material;
    static float nextPing;

    public static bool AltHeld => Chat.Keys is Keyboard kb && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);

    void Awake() => Active.Clear();

    void Update()
    {
        Active.RemoveAll(p => Time.time - p.born > Life);
        var mouse = Mouse.current;
        if (!Net.Online || mouse == null || !AltHeld || !mouse.leftButton.wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return; // the minimap sends its own
        var ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer)) Send(hit.point);
    }

    public static void Send(Vector3 at)
    {
        if (Time.time < nextPing || RoomState.Instance == null) return;
        nextPing = Time.time + Cooldown;
        RoomState.Instance.SendPing(at);
    }

    // who = null: just the marker (an alert already says why, e.g. an ally under attack).
    public static void Show(Vector3 at, Color color, string who)
    {
        Active.Add((at, color, Time.time));
        Debug.Log($"[GD5] ping from {who} at {at}");
        if (who != null) Alerts.Show($"{who} pinged the map", color, 2f);
        if (material == null) material = Resources.Load<Material>("HarvestFX"); // additive glow
        var go = new GameObject("Ping");
        go.transform.position = at;
        go.AddComponent<PingMarker>().Init(color, material);
        Destroy(go, Life);
    }
}

// The world marker: three expanding rings and a beam of light.
class PingMarker : MonoBehaviour
{
    const int Points = 40;
    LineRenderer ring, beam;
    Color color;
    float born;

    public void Init(Color c, Material m)
    {
        color = c;
        born = Time.time;
        ring = Line("Ring", m, Points, true);
        beam = Line("Beam", m, 2, false);
        beam.SetPosition(0, transform.position);
        beam.SetPosition(1, transform.position + Vector3.up * 25f);
    }

    LineRenderer Line(string n, Material m, int points, bool loop)
    {
        var lr = new GameObject(n).AddComponent<LineRenderer>();
        lr.transform.SetParent(transform, false);
        lr.sharedMaterial = m;
        lr.positionCount = points;
        lr.loop = loop;
        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return lr;
    }

    void Update()
    {
        float age = Time.time - born;
        float pulse = (age % 1f);                    // a new ring every second
        float radius = Mathf.Lerp(1f, 7f, pulse);
        Color c = color; c.a = (1f - pulse) * Mathf.Clamp01(4f - age);
        ring.startColor = ring.endColor = c;
        ring.widthMultiplier = 0.35f;
        for (int i = 0; i < Points; i++)
        {
            float a = i * Mathf.PI * 2f / Points;
            ring.SetPosition(i, transform.position + new Vector3(Mathf.Cos(a) * radius, 0.15f, Mathf.Sin(a) * radius));
        }
        Color b = color; b.a = 0.6f * Mathf.Clamp01(4f - age);
        beam.startColor = b;
        beam.endColor = new Color(b.r, b.g, b.b, 0f);
        beam.widthMultiplier = 0.5f + 0.2f * Mathf.Sin(age * 12f);
    }
}
