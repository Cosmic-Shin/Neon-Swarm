using System.Collections.Generic;
using UnityEngine;

// Where new buildings may go: inside a Power Pylon's ring or near the Core Nexus.
// Pylons read the radius from the model's "*_BuildRadius" rings (Mk II shows a 50% bigger one);
// the Nexus has no ring in its model, so it draws one. Rings only show while placing a building.
// Construction keeps this off until the building is finished, so half-built pylons don't count.
[RequireComponent(typeof(Health))]
public class BuildRadius : MonoBehaviour
{
    public static readonly List<BuildRadius> All = new();

    [SerializeField] float radius; // 0 = use the model's rings
    [SerializeField] Color ringColor = new(0.3f, 1f, 0.6f, 0.6f);

    readonly List<(Renderer r, float radius)> rings = new();
    LineRenderer drawn;
    Health health;
    static bool shown;

    public int Team => health.team;

    // Biggest ring currently switched on (the Mk II ring sits in an upgrade group that starts hidden).
    public float Radius
    {
        get
        {
            float best = radius;
            foreach (var (r, rr) in rings) if (r.gameObject.activeInHierarchy) best = Mathf.Max(best, rr);
            return best;
        }
    }

    void Awake()
    {
        health = GetComponent<Health>();
        foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
            if (mf.name.EndsWith("_BuildRadius"))
            {
                float r = mf.sharedMesh.bounds.extents.x * mf.transform.lossyScale.x;
                rings.Add((mf.GetComponent<Renderer>(), r));
            }
        if (rings.Count == 0) drawn = MakeCircle();
        Show(false);
    }

    void OnEnable() { All.Add(this); Show(shown && Team == 0); }
    void OnDisable() { All.Remove(this); Show(false); }

    void Show(bool on)
    {
        foreach (var (r, _) in rings) r.enabled = on;
        if (drawn) drawn.enabled = on;
    }

    // BuildingPlacer: rings on while placing.
    public static void ShowAll(bool on)
    {
        shown = on;
        foreach (var b in All) b.Show(on && b.Team == 0);
    }

    // Does a footprint of this size (largest side) at p touch one of the team's build circles?
    public static bool Covers(Vector3 p, float footprint, int team)
    {
        foreach (var b in All)
        {
            if (b.Team != team) continue;
            Vector3 d = p - b.transform.position; d.y = 0f;
            float reach = b.Radius + footprint * 0.5f;
            if (d.sqrMagnitude <= reach * reach) return true;
        }
        return false;
    }

    LineRenderer MakeCircle()
    {
        var lr = new GameObject("BuildRing").AddComponent<LineRenderer>();
        lr.transform.SetParent(transform, false);
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = ringColor;
        lr.widthMultiplier = 0.3f;
        lr.loop = true;
        lr.useWorldSpace = true;
        lr.positionCount = 96;
        for (int i = 0; i < 96; i++)
        {
            float a = i / 96f * Mathf.PI * 2f;
            lr.SetPosition(i, transform.position + new Vector3(Mathf.Cos(a) * radius, 0.2f, Mathf.Sin(a) * radius));
        }
        return lr;
    }
}
