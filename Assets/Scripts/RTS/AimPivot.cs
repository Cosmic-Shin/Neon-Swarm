using UnityEngine;

public class AimPivot : MonoBehaviour
{
    public string pivotName;
    public float turnSpeed = 180f;
    public float idleSweep = 25f;

    Transform pivot;
    Turret turret;
    Attacker attacker;
    Quaternion restRelative;
    Vector3 restDir;
    float yaw, seed;
    Animator anim;
    bool hasFire;
    float lastShot = -99f, lookUntil;
    Vector3 lookPoint;

    public void LookAt(Vector3 point, float seconds) { lookPoint = point; lookUntil = Time.time + seconds; }

    void Awake()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == pivotName) { pivot = t; break; }
        if (pivot == null) { enabled = false; return; }
        turret = GetComponent<Turret>();
        attacker = GetComponent<Attacker>();
        restRelative = Quaternion.Inverse(transform.rotation) * pivot.rotation;
        Vector3 muzzle = pivot.position + transform.forward;
        foreach (var t in pivot.GetComponentsInChildren<Transform>(true))
            if (t.name.Contains("Muzzle") || t.name.Contains("BeamPoint")) { muzzle = t.position; break; }
        Vector3 d = muzzle - pivot.position;
        d.y = 0f;
        restDir = transform.InverseTransformDirection(d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward);
        seed = Random.value * 10f;
        anim = GetComponentInChildren<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null) foreach (var p in anim.parameters) if (p.name == "Fire") hasFire = true;
    }

    Health Target => turret != null ? turret.Aim : attacker != null ? attacker.Target : null;

    void LateUpdate()
    {
        float want = yaw;
        if (hasFire && turret != null && turret.LastShot > lastShot) { lastShot = turret.LastShot; anim.SetTrigger("Fire"); }
        var target = Target;
        bool looking = Time.time < lookUntil;
        if (looking || (target != null && !target.IsDead))
        {
            Vector3 to = (looking ? lookPoint : target.transform.position) - pivot.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) want = Vector3.SignedAngle(transform.TransformDirection(restDir), to, Vector3.up);
        }
        else if (turret != null && turret.Online) want = Mathf.Sin(Time.time * 0.35f + seed) * idleSweep;
        yaw = Mathf.MoveTowardsAngle(yaw, want, turnSpeed * Time.deltaTime);
        pivot.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * transform.rotation * restRelative;
    }
}
