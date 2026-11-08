using System.Collections.Generic;
using UnityEngine;

/// <summary>Where harvesters dock to unload. One harvester docks at a time; others queue in front of the pad.</summary>
public class Refinery : MonoBehaviour
{
    public static readonly List<Refinery> All = new List<Refinery>();

    public Transform dockPoint;
    public float creditsPerUnit = 1f;
    [Tooltip("Multiplies the queue spacing; match the model's scale.")]
    public float queueScale = 1f;

    readonly List<Harvester> queue = new List<Harvester>();
    // GD5 edit: up to two docks. "Refinery_DockPoint2" sits in the Mk II add-on, so it's only used once that shows.
    readonly List<Transform> docks = new List<Transform>();
    readonly Dictionary<Transform, Harvester> occupant = new Dictionary<Transform, Harvester>();

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Awake()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (!dockPoint && t.name == "Refinery_DockPoint") dockPoint = t;
            if (t.name == "Refinery_DockPoint2") docks.Add(t);
        }
        if (!dockPoint) dockPoint = transform;
        docks.Insert(0, dockPoint);
    }

    public Vector3 DockPosition => dockPoint.position;
    public Vector3 DockPositionOf(Harvester h) => (DockOf(h) ?? dockPoint).position;

    Transform DockOf(Harvester h)
    {
        foreach (var kv in occupant) if (kv.Value == h) return kv.Key;
        return null;
    }

    /// <summary>A docked harvester faces into the refinery.</summary>
    public Quaternion DockFacing
    {
        get
        {
            Vector3 d = transform.position - dockPoint.position; d.y = 0;
            return d.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(d) : transform.rotation;
        }
    }

    Vector3 OutDir
    {
        get { Vector3 d = dockPoint.position - transform.position; d.y = 0; return d.sqrMagnitude > 1e-4f ? d.normalized : transform.forward; }
    }

    // GD5: a harvester is docked and unloading (BuildingLife animates the intake).
    public bool Busy { get { foreach (var o in occupant.Values) if (o && o.isActiveAndEnabled) return true; return false; } }

    public bool TryDock(Harvester h)
    {
        if (DockOf(h) != null) return true;
        queue.RemoveAll(q => !q);
        if (queue.Count > 0 && queue[0] != h) { if (!queue.Contains(h)) queue.Add(h); return false; } // first come, first served
        foreach (var d in docks)
        {
            if (!d.gameObject.activeInHierarchy) continue; // Mk II dock not built yet
            if (occupant.TryGetValue(d, out var o) && o && o.isActiveAndEnabled) continue;
            occupant[d] = h;
            queue.Remove(h);
            return true;
        }
        if (!queue.Contains(h)) queue.Add(h);
        return false;
    }

    public void Release(Harvester h)
    {
        var d = DockOf(h);
        if (d) occupant.Remove(d);
        queue.Remove(h);
    }

    public Vector3 QueuePosition(Harvester h)
    {
        int i = Mathf.Max(0, queue.IndexOf(h));
        Vector3 side = Vector3.Cross(Vector3.up, OutDir);
        return dockPoint.position + (OutDir * (1.7f + 0.9f * (i / 2)) + side * ((i % 2 == 0) ? 0.6f : -0.6f)) * queueScale;
    }

    public static Refinery FindNearest(Vector3 p)
    {
        Refinery best = null; float bd = float.MaxValue;
        foreach (var r in All)
        {
            if (!r) continue;
            float d = (r.transform.position - p).sqrMagnitude;
            if (d < bd) { bd = d; best = r; }
        }
        return best;
    }
}
