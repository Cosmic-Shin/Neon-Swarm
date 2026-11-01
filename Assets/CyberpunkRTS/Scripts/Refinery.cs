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

    public Harvester Occupant { get; private set; }
    readonly List<Harvester> queue = new List<Harvester>();

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Awake()
    {
        if (!dockPoint)
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "Refinery_DockPoint") { dockPoint = t; break; }
        if (!dockPoint) dockPoint = transform;
    }

    public Vector3 DockPosition => dockPoint.position;

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

    public bool TryDock(Harvester h)
    {
        if (Occupant && Occupant != h && Occupant.isActiveAndEnabled) { if (!queue.Contains(h)) queue.Add(h); return false; }
        queue.RemoveAll(q => !q);
        if (queue.Count > 0 && queue[0] != h && Occupant != h) { if (!queue.Contains(h)) queue.Add(h); return false; }
        queue.Remove(h);
        Occupant = h;
        return true;
    }

    public void Release(Harvester h)
    {
        if (Occupant == h) Occupant = null;
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
