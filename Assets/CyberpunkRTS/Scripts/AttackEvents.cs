using UnityEngine;

/// <summary>
/// Receives the "Fire" Animation Event from a unit's Attack clip (on the same GameObject as the Animator)
/// and forwards it to combat code. Subscribe to OnFire to spawn the projectile / muzzle flash / hit
/// at the exact frame of the punch, recoil or bomb drop.
/// </summary>
public class AttackEvents : MonoBehaviour
{
    public event System.Action OnFire;
    static readonly int AttackId = Animator.StringToHash("Attack");
    Animator anim;

    void Awake() { anim = GetComponent<Animator>(); }

    /// <summary>Play the Attack clip (if this unit has one). The OnFire event follows on the fire frame.</summary>
    public void PlayAttack()
    {
        if (anim && anim.runtimeAnimatorController) anim.SetTrigger(AttackId);
        else OnFire?.Invoke();   // no clip: fire immediately
    }

    // Called by the Animation Event named "Fire" in the Attack clip.
    public void Fire() { OnFire?.Invoke(); }
}
