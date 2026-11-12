using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class UnitMarker : MonoBehaviour
{
    SelectableUnit unit;
    Health health;
    Attacker attacker;
    GameObject ring;
    SpriteRenderer glow;
    readonly Transform[] corners = new Transform[4];
    readonly SpriteRenderer[] cornerSprites = new SpriteRenderer[4];
    float size;

    static Sprite softDot, corner;
    static int hoverFrame = -1;
    static SelectableUnit hovered;
    public static SelectableUnit Hovered { get { PickHover(); return hovered; } }

    public void Init(GameObject selectionRing)
    {
        unit = GetComponent<SelectableUnit>();
        health = GetComponent<Health>();
        attacker = GetComponent<Attacker>();
        ring = selectionRing;
        size = Flyer.IsAir(this) ? Mathf.Max(2f, Flyer.SizeOf(this) * 0.6f)
             : TryGetComponent(out UnityEngine.AI.NavMeshAgent a) ? Mathf.Max(1.6f, a.radius * transform.lossyScale.x * 2.4f) : 2f;
        softDot ??= MakeSprite(64, (x, y) => { float d = Vector2.Distance(new Vector2(x, y), Vector2.one * 0.5f) * 2f; return Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d); });
        corner ??= MakeSprite(32, (x, y) => (x < 0.22f || y < 0.22f) ? 1f : 0f);

        glow = Flat("TeamGlow", softDot, size * 1.6f, 0.04f);
        for (int i = 0; i < 4; i++)
        {
            cornerSprites[i] = Flat("Bracket", corner, size * 0.35f, 0.08f);
            corners[i] = cornerSprites[i].transform;
            cornerSprites[i].enabled = false;
        }
        Recolor();
    }

    public void Recolor()
    {
        if (glow == null) return;
        Color c = health.team == Creep.Team ? Net.CreepColor : Net.ColorOf(health.team);
        if (c == Color.white) c = new Color(1f, 0.2f, 0.25f);
        c.a = 0.55f;
        glow.color = c;
        foreach (var s in cornerSprites) s.color = new Color(c.r, c.g, c.b, 0.95f);
    }

    public void Remove()
    {
        if (glow != null) Destroy(glow.gameObject);
        foreach (var c in corners) if (c != null) Destroy(c.gameObject);
        Destroy(this);
    }

    SpriteRenderer Flat(string name, Sprite sprite, float worldSize, float y)
    {
        var sr = new GameObject(name).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.transform.localPosition = new Vector3(0f, y, 0f);
        sr.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        sr.transform.localScale = Vector3.one * worldSize / transform.lossyScale.x;
        sr.sprite = sprite;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = false;
        return sr;
    }

    void LateUpdate()
    {
        bool hover = Hovered == unit;
        bool selected = unit.IsSelected;
        if (unit.IsEnemy && ring != null)
        {
            bool threat = attacker != null && attacker.Target != null && Net.Allied(attacker.Target.team, 0);
            ring.SetActive(hover || threat);
        }

        bool brackets = selected || hover;
        float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 6f);
        float half = size * 0.62f * pulse;
        for (int i = 0; i < 4; i++)
        {
            cornerSprites[i].enabled = brackets;
            if (!brackets) continue;
            float a = (45f + 90f * i + Time.time * 20f) * Mathf.Deg2Rad;
            corners[i].localPosition = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * half / transform.lossyScale.x * 1.41f + Vector3.up * 0.08f;
            corners[i].localRotation = Quaternion.Euler(90f, 225f - a * Mathf.Rad2Deg, 0f);
            var c = cornerSprites[i].color; c.a = selected ? 0.95f : 0.5f; cornerSprites[i].color = c;
        }
    }

    static void PickHover()
    {
        if (hoverFrame == Time.frameCount) return;
        hoverFrame = Time.frameCount;
        hovered = null;
        var cam = Camera.main;
        if (cam == null || Mouse.current == null) return;
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
        var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        int mask = cam.cullingMask;
        if (Physics.Raycast(ray, out var hit, 500f, mask, QueryTriggerInteraction.Ignore)) hovered = hit.collider.GetComponentInParent<SelectableUnit>();
    }

    static Sprite MakeSprite(int n, System.Func<float, float, float> alpha)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                px[y * n + x] = new Color32(255, 255, 255, (byte)(alpha((x + 0.5f) / n, (y + 0.5f) / n) * 255));
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }
}
