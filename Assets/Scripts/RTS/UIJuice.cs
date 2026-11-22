using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
        HoloUI.SetSliced(glow, "Fill_White");
        glow.raycastTarget = false;
        var rt = glow.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        Juice.Play(glow.gameObject, 0.5f, k => glow.color = new Color(c.r, c.g, c.b, 0.6f * (1f - k)));
    }

    Scaled Get(Transform t)
    {
        if (!scaled.TryGetValue(t, out var s)) scaled[t] = s = new Scaled { rest = restOf.TryGetValue(t, out var r) ? r : t.localScale };
        return s;
    }

    class Anim { public Transform t; public CanvasGroup g; public float start; }
    const float PanelTime = 0.22f, EntryTime = 0.32f;
    readonly List<Anim> anims = new();
    readonly HashSet<Transform> animating = new();
    readonly Dictionary<Transform, Vector3> restOf = new();
    readonly Dictionary<Transform, bool> panels = new();
    readonly HashSet<CanvasGroup> ours = new();
    Canvas[] canvases = new Canvas[0];
    float nextCanvases;

    void Panels()
    {
        if (Time.unscaledTime >= nextCanvases)
        {
            nextCanvases = Time.unscaledTime + 1f;
            canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            settled.Clear();
            foreach (var k in panels.Keys) if (k == null) settled.Add(k);
            foreach (var k in settled) panels.Remove(k);
        }
        bool live = Time.timeSinceLevelLoad > 0.5f;
        foreach (var c in canvases)
        {
            if (c == null || !c.isRootCanvas || !c.gameObject.activeInHierarchy) continue;
            var area = ((RectTransform)c.transform).rect;
            foreach (Transform p in c.transform)
            {
                bool on = p.gameObject.activeSelf;
                bool known = panels.TryGetValue(p, out bool was);
                panels[p] = on;
                if (!on || (known ? was : !live) || p is not RectTransform r) continue;
                if (r.rect.width * r.rect.height < area.width * area.height * 0.1f) continue;
                Open(r);
            }
        }
    }

    void Open(RectTransform panel)
    {
        var g = panel.GetComponent<CanvasGroup>();
        float now = Time.unscaledTime;
        if (g == null) { g = panel.gameObject.AddComponent<CanvasGroup>(); ours.Add(g); }
        if (ours.Contains(g)) Begin(panel, g, now);
        StaggerIn(panel, now + 0.04f);
    }

    public static void Stagger(Transform root) { if (I != null && root != null) I.StaggerIn(root, Time.unscaledTime); }

    void StaggerIn(Transform root, float start)
    {
        int i = 0;
        foreach (var b in root.GetComponentsInChildren<Button>())
        {
            Begin(b.transform, null, start + Mathf.Min(i, 12) * 0.03f);
            if (++i >= 40) break;
        }
    }

    void Begin(Transform t, CanvasGroup g, float start)
    {
        if (!restOf.ContainsKey(t)) restOf[t] = scaled.TryGetValue(t, out var s) ? s.rest : t.localScale;
        if (animating.Add(t)) anims.Add(new Anim { t = t, g = g, start = start });
        else foreach (var a in anims) if (a.t == t) a.start = start;
    }

    void Animate()
    {
        float now = Time.unscaledTime;
        for (int i = anims.Count - 1; i >= 0; i--)
        {
            var a = anims[i];
            if (a.t == null) { anims.RemoveAt(i); continue; }
            Vector3 rest = restOf[a.t];
            bool panel = a.g != null;
            float k = Mathf.Clamp01((now - a.start) / (panel ? PanelTime : EntryTime));
            if (panel)
            {
                float e = 1f - (1f - k) * (1f - k) * (1f - k);
                a.g.alpha = e;
                a.t.localScale = rest * Mathf.Lerp(0.95f, 1f, e);
            }
            else
            {
                float m = k - 1f;
                a.t.localScale = rest * Mathf.Max(0.001f, 1f + 2.7f * m * m * m + 1.7f * m * m);
            }
            if (k < 1f) continue;
            a.t.localScale = rest;
            if (panel) a.g.alpha = 1f;
            animating.Remove(a.t);
            restOf.Remove(a.t);
            anims.RemoveAt(i);
        }
    }

    Transform lastHovered;

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
        Panels();
        Animate();
        SliderTicks(es, mouse);
        if (hovered != null && animating.Contains(hovered)) hovered = null;
        if (hovered != null) Get(hovered);
        if (hovered != lastHovered && hovered != null) Sfx.Play(Snd.Hover, 0.25f);
        if (hovered != null && mouse.leftButton.wasPressedThisFrame) Get(hovered).now = 0.9f;
        lastHovered = hovered;

        settled.Clear();
        float dt = Time.unscaledDeltaTime;
        foreach (var kv in scaled)
        {
            var t = kv.Key;
            if (t == null) { settled.Add(t); continue; }
            var s = kv.Value;
            if (animating.Contains(t)) continue;
            float want = t == hovered ? Hover : 1f;
            s.now = Mathf.Lerp(s.now, want, 1f - Mathf.Exp(-14f * dt));
            t.localScale = s.rest * s.now;
            if (t != hovered && Mathf.Abs(s.now - 1f) < 0.002f) { t.localScale = s.rest; settled.Add(t); }
        }
        foreach (var t in settled) scaled.Remove(t);
    }

    Slider dragging;
    float lastValue, nextTick;

    void SliderTicks(EventSystem es, Mouse mouse)
    {
        var s = mouse != null && mouse.leftButton.isPressed && es != null && es.currentSelectedGameObject != null ? es.currentSelectedGameObject.GetComponent<Slider>() : null;
        if (s != dragging) { dragging = s; if (s != null) lastValue = s.normalizedValue; return; }
        if (s == null || Mathf.Abs(s.normalizedValue - lastValue) < 0.04f || Time.unscaledTime < nextTick) return;
        lastValue = s.normalizedValue;
        nextTick = Time.unscaledTime + 0.04f;
        Sfx.Play(Snd.Hover, 0.2f, 0.8f + s.normalizedValue * 0.6f);
    }

    static bool CantAfford(Button b)
    {
        var cost = b.transform.Find("Cost");
        return cost != null && cost.TryGetComponent(out Text t) && t.color == LockBadge.Red;
    }
}
