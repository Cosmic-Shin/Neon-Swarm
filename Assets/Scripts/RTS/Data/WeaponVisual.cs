using UnityEngine;

// How a weapon looks. No projectile = the instant laser beam (unless hideBeam, e.g. the flamethrower).
[System.Serializable]
public class WeaponVisual
{
    public GameObject projectile;   // pooled prefab, flies to the target; damage lands on impact
    public float speed = 60f;       // m/s
    public float arc;               // > 0: lobbed this high (artillery)
    public GameObject muzzleFlash;  // Fx at the gun each shot
    public GameObject impact;       // Fx where it lands, sized to the splash radius (or 1)
    public GameObject scorch;       // ground mark left at the impact
    public bool hideBeam;           // no laser line (the flash/impact is the whole visual)
}
