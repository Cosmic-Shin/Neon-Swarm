using UnityEngine;

// Uplink Tower: while one is built and powered, every enemy unit shows on the minimap, even in fog.
public class Radar : MonoBehaviour
{
    static int count;

    public static bool Active => count > 0 && !Structure.LowPower;

    void OnEnable() => count++;   // off while under construction (Construction disables root scripts)
    void OnDisable() => count--;
}
