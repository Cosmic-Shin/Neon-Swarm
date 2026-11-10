using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Every button in every scene, including ones made at runtime (lives on the Sfx "Audio" object):
//  - grows a little while hovered, springs back when left
//  - Flash(): a glow sweep and a pop, e.g. a unit button when that unit is done
//  - clicking a shop button you can't afford pulses the credits counter red
public class UIJuice : MonoBehaviour
{
    const float Hover = 1.06f;
    static UIJuice I;

    class Scaled { public Vector3 rest; public float now = 1f; }
    readonly Dictionary<Transform, Scaled> scaled = new();
    readonly List<RaycastResult> hits = new();
    readonly List<Transform> settled = new();

    void Awake() => I = this;

    public static void Flash(Button button, Color c)
    {
        if (I == null || button == null) return;
        I.Get(button.transform).now = 1.18f;
        var glow = new GameObject("Flash", typeof(RectTransform)).AddComponent<Image>();
        glow.transform.SetParent(button.transform, false);
        glow.raycastTarget = false;
        var rt = glow.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        Juice.Play(glow.gameObject, 0.5f, k => glow.color = new Color(c.r, c.g, c.b, 0.6f * (1f - k)));
    }

    Scaled Get(Transform t)
    {
        if (!scaled.TryGetValue(t, out var s)) scaled[t] = s = new Scaled { rest = t.localScale };
        return s;
    }

    void Update()
    {
        Transform hovered = null;
        var mouse = Mouse.current;
        var es = EventSystem.current;
        if (mouse != null && es != null)
        {
            hits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = mouse.position.ReadValue() }, hits);
            var button = hits.Count > 0 ? hits[0].gameObject.GetComponentInParent<Button>() : null;
            if (button != null && button.IsInteractable()) hovered = button.transform;
            if (button != null && !button.IsInteractable() && mouse.leftButton.wasPressedThisFrame && CantAfford(button)) CreditsDisplay.Deny();
        }
        if (hovered != null) Get(hovered);

        settled.Clear();
        float dt = Time.unscaledDeltaTime;
        foreach (var kv in scaled)
        {
            var t = kv.Key;
            if (t == null) { settled.Add(t); continue; }
            var s = kv.Value;
            float want = t == hovered ? Hover : 1f;
            s.now = Mathf.Lerp(s.now, want, 1f - Mathf.Exp(-14f * dt));
            t.localScale = s.rest * s.now;
            if (t != hovered && Mathf.Abs(s.now - 1f) < 0.002f) { t.localScale = s.rest; settled.Add(t); }
        }
        foreach (var t in settled) scaled.Remove(t);
    }

    // Its price is shown in red (LockBadge does that while you're short of credits).
    static bool CantAfford(Button b)
    {
        var cost = b.transform.Find("Cost");
        return cost != null && cost.TryGetComponent(out Text t) && t.color == LockBadge.Red;
    }
}
