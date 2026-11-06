using System.Collections.Generic;
using UnityEngine;

// Uplink Tower Mk II: enemies inside its reveal ring can't hide in a cloak field.
// The radius comes from the model's "*_RevealRadius" ring, which only shows once the Mk II add-on is on.
[RequireComponent(typeof(Health))]
public class Revealer : MonoBehaviour
{
    static readonly List<Revealer> active = new();

    Health self;
    Transform ring;
    float radius;

    void Awake()
    {
        self = GetComponent<Health>();
        foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
            if (mf.name.EndsWith("_RevealRadius"))
            {
                ring = mf.transform;
                radius = mf.sharedMesh.bounds.extents.x * mf.transform.lossyScale.x;
            }
    }

    void OnEnable() => active.Add(this);   // off while under construction
    void OnDisable() => active.Remove(this);

    bool On => ring != null && ring.gameObject.activeInHierarchy && !Net.LowPower(self.team);

    public static bool Reveals(Health h)
    {
        foreach (var r in active)
            if (!Net.Allied(r.self.team, h.team) && r.On && (h.transform.position - r.transform.position).sqrMagnitude <= r.radius * r.radius) return true;
        return false;
    }
}
