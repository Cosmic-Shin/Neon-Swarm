using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Minimap : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [SerializeField] RectTransform map;
    [SerializeField] RectTransform dots;
    [SerializeField] RectTransform viewFrame;
    [SerializeField] Sprite dotSprite;
    [SerializeField] RawImage fogOverlay;
    [SerializeField] float worldSize = 600f;

    static readonly Color GreenOre = new(0.4f, 1f, 0.3f, 0.6f);
    static readonly Color RedOre = new(1f, 0.15f, 0.1f, 0.8f);

    readonly List<Image> pool = new();
    int used;
    Camera cam;
    OutpostCapture[] outposts;
    static readonly Plane Ground = new(Vector3.up, Vector3.zero);

    GoodSelectionManager selection;

    void Awake()
    {
        cam = Camera.main;
        worldSize = GameMaps.Of(gameObject.scene).size;
        selection = FindFirstObjectByType<GoodSelectionManager>();

        var terrain = new GameObject("Terrain", typeof(RectTransform)).AddComponent<RawImage>();
        terrain.transform.SetParent(map, false);
        terrain.transform.SetAsFirstSibling();
        terrain.texture = Resources.Load<Texture>(GameMaps.Of(gameObject.scene).preview);
        terrain.color = new Color(0.55f, 0.55f, 0.65f, 1f);
        terrain.raycastTarget = false;
        var r = terrain.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        outposts = FindObjectsByType<OutpostCapture>(FindObjectsSortMode.None);
    }

    void LateUpdate()
    {
        var fog = FogOfWar.Instance;
        if (fog != null && fogOverlay.texture == null) fogOverlay.texture = fog.MinimapTexture;
        bool Explored(Vector3 p) => fog == null || fog.IsExplored(p);

        used = 0;
        foreach (var o in OreNode.All)
            if (!o.IsDepleted && Explored(o.transform.position)) Dot(o.transform.position, 3f, o.valueMultiplier > 1f ? RedOre : GreenOre);
        foreach (var h in Health.All)
            if (Net.Allied(h.team, 0) || fog == null || fog.IsVisible(h.transform.position) || (Radar.Active && h.TryGetComponent(out SelectableUnit _)))
                Dot(h.transform.position, h.TryGetComponent(out SelectableUnit _) ? 5f : 10f, Net.ColorOf(h.team));
        if (fog != null)
            foreach (var (go, team) in fog.Ghosts)
                if (go != null) { Color c = Net.ColorOf(team); c.a = 0.45f; Dot(go.transform.position, 8f, c); }
        foreach (var o in outposts)
            if (o != null && Explored(o.transform.position)) Dot(o.transform.position, 10f, o.IsOwned ? Net.ColorOf(o.ownerTeam) : Color.white);
        for (int i = fights.Count - 1; i >= 0; i--)
        {
            float age = Time.time - fights[i].at;
            if (age > FightTime) { fights.RemoveAt(i); continue; }
            float throb = 0.5f + 0.5f * Mathf.Sin(age * 14f);
            Color c = fights[i].ours ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.65f, 0.1f);
            c.a = (1f - age / FightTime) * (0.45f + 0.55f * throb);
            Dot(fights[i].p, 10f + 8f * throb, c);
        }
        foreach (var p in Ping.Active)
        {
            float age = Time.time - p.born, pulse = age % 1f;
            Color c = p.color; c.a = 1f - pulse;
            Dot(p.at, 8f + 26f * pulse, c);
        }
        DrawAttacks();
        for (int i = used; i < pool.Count; i++) pool[i].enabled = false;

        Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
        foreach (var v in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
        {
            Vector2 p = ToMap(GroundPoint(cam.ViewportPointToRay(v)));
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        viewFrame.anchoredPosition = min;
        viewFrame.sizeDelta = max - min;
    }

    const float AttackLife = 5f, AttackPatch = 35f, AttackGap = 4f, FlashTime = 1.2f;
    static readonly List<(Vector3 p, float at)> attacks = new();
    static float flashAt = -10f;
    Image[] edges;

    public static void UnderAttack(Vector3 p)
    {
        attacks.RemoveAll(a => Time.time - a.at > AttackLife);
        foreach (var a in attacks)
        {
            Vector3 d = a.p - p;
            if (d.x * d.x + d.z * d.z <= AttackPatch * AttackPatch && Time.time - a.at < AttackGap) return;
        }
        attacks.Add((p, Time.time));
        flashAt = Time.time;
        Alerts.LastSpot = p;
    }

    void DrawAttacks()
    {
        foreach (var (p, at) in attacks)
        {
            float age = Time.time - at;
            if (age > AttackLife) continue;
            float fade = 1f - age / AttackLife;
            for (int ring = 0; ring < 2; ring++)
            {
                float k = (age * 1.4f + ring * 0.5f) % 1f;
                Dot(p, 6f + 34f * k, new Color(1f, 0.15f, 0.15f, (1f - k) * 0.7f * fade));
            }
            bool blink = Mathf.Repeat(age * 4f, 1f) < 0.6f;
            Dot(p, 9f, new Color(1f, 0.2f, 0.2f, blink ? fade : fade * 0.3f));
        }
        if (edges == null) edges = MakeEdges();
        float flash = Mathf.Clamp01(1f - (Time.time - flashAt) / FlashTime);
        float on = flash * (Mathf.Repeat((Time.time - flashAt) * 5f, 1f) < 0.5f ? 1f : 0.4f);
        foreach (var e in edges) { e.enabled = on > 0.01f; e.color = new Color(1f, 0.15f, 0.15f, on); }
    }

    Image[] MakeEdges()
    {
        var list = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            var img = new GameObject("AttackEdge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(map, false);
            img.raycastTarget = false;
            var r = img.rectTransform;
            bool side = i >= 2;
            r.anchorMin = side ? new Vector2(i == 2 ? 0f : 1f, 0f) : new Vector2(0f, i == 0 ? 0f : 1f);
            r.anchorMax = side ? new Vector2(i == 2 ? 0f : 1f, 1f) : new Vector2(1f, i == 0 ? 0f : 1f);
            r.sizeDelta = side ? new Vector2(4f, 0f) : new Vector2(0f, 4f);
            r.anchoredPosition = Vector2.zero;
            list[i] = img;
        }
        return list;
    }

    const float FightTime = 2.5f, FightPatch = 24f;
    static readonly List<(Vector3 p, float at, bool ours)> fights = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetFights() { fights.Clear(); attacks.Clear(); flashAt = -10f; }

    public static void Fight(Vector3 p, bool ours)
    {
        if (!ours && FogOfWar.Instance != null && !FogOfWar.Instance.IsVisible(p)) return;
        for (int i = 0; i < fights.Count; i++)
        {
            Vector3 d = fights[i].p - p;
            if (d.x * d.x + d.z * d.z > FightPatch * FightPatch) continue;
            if (Time.time - fights[i].at > 0.5f) fights[i] = (fights[i].p, Time.time, fights[i].ours || ours);
            return;
        }
        if (fights.Count < 24) fights.Add((p, Time.time, ours));
    }

    void Dot(Vector3 world, float size, Color color)
    {
        if (used == pool.Count)
        {
            var img = new GameObject("Dot", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(dots, false);
            img.sprite = dotSprite;
            img.raycastTarget = false;
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = Vector2.zero;
            pool.Add(img);
        }
        var d = pool[used++];
        d.enabled = true;
        d.color = color;
        d.rectTransform.sizeDelta = new Vector2(size, size);
        d.rectTransform.anchoredPosition = ToMap(world);
    }

    Vector2 ToMap(Vector3 world) => new Vector2(world.x / worldSize + 0.5f, world.z / worldSize + 0.5f) * map.rect.size;

    static Vector3 GroundPoint(Ray ray) => Ground.Raycast(ray, out float d) ? ray.GetPoint(d) : ray.origin;

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left && Net.Online && Ping.AltHeld) Ping.Send(ToWorld(e));
        else if (e.button == PointerEventData.InputButton.Left) MoveCamera(e);
        else if (e.button == PointerEventData.InputButton.Right && selection != null) selection.MoveSelectedTo(ToWorld(e));
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left) MoveCamera(e);
    }

    Vector3 ToWorld(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(map, e.position, e.pressEventCamera, out Vector2 local);
        Rect r = map.rect;
        float x = Mathf.Clamp01((local.x - r.xMin) / r.width), z = Mathf.Clamp01((local.y - r.yMin) / r.height);
        return new Vector3((x - 0.5f) * worldSize, 0f, (z - 0.5f) * worldSize);
    }

    void MoveCamera(PointerEventData e)
    {
        Vector3 shift = ToWorld(e) - GroundPoint(cam.ViewportPointToRay(new Vector3(0.5f, 0.5f)));
        shift.y = 0f;
        cam.transform.position += shift;
    }
}
