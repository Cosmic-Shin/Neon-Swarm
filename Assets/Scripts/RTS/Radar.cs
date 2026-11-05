using System.Collections.Generic;
using UnityEngine;

// Uplink Tower: while one of ours is built and powered, every enemy unit shows on the minimap, even in fog.
public class Radar : MonoBehaviour
{
    static readonly List<Radar> active = new();
    Health health;

    void Awake() => health = GetComponent<Health>();
    void OnEnable() => active.Add(this);   // off while under construction (Construction disables root scripts)
    void OnDisable() => active.Remove(this);

    public static bool Active => !Structure.LowPower && active.Exists(r => r.health.team == 0); // multiplayer: not other players' towers
}
