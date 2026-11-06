using System.Collections.Generic;
using UnityEngine;

// Cloak Field Generator: friendly units and buildings inside the radius can't be targeted by enemies
// unless an enemy gets within revealRange. Offline while the player is low on power.
[RequireComponent(typeof(Health))]
public class CloakField : MonoBehaviour
{
    static readonly List<CloakField> active = new();

    [SerializeField] float radius = 28f;
    [SerializeField] Color ringColor = new(0.6f, 0.4f, 1f, 0.5f);
    public const float RevealRange = 8f;

    Health self;
    LineRenderer ring;

    void Awake()
    {
        self = GetComponent<Health>();
        // Faint ring on the ground showing the field.
        ring = new GameObject("CloakRing").AddComponent<LineRenderer>();
        ring.transform.SetParent(transform, false);
        ring.material = new Material(Shader.Find("Sprites/Default"));
        ring.startColor = ring.endColor = ringColor;
        ring.widthMultiplier = 0.3f;
        ring.loop = true;
        ring.useWorldSpace = true;
        ring.positionCount = 64;
        for (int i = 0; i < 64; i++)
        {
            float a = i / 64f * Mathf.PI * 2f;
            ring.SetPosition(i, transform.position + new Vector3(Mathf.Cos(a) * radius, 0.2f, Mathf.Sin(a) * radius));
        }
    }

    void OnEnable() { active.Add(this); ring.enabled = true; }
    void OnDisable() { active.Remove(this); ring.enabled = false; }

    bool Online => !Net.LowPower(self.team); // other players' power comes over the network

    void Update() => ring.enabled = Online;

    // Is h inside a working cloak field of its own side (allies' fields count)?
    public static bool Covers(Health h)
    {
        foreach (var f in active)
            if (Net.Allied(f.self.team, h.team) && f.Online && (h.transform.position - f.transform.position).sqrMagnitude <= f.radius * f.radius) return true;
        return false;
    }
}
