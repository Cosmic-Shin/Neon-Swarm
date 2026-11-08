using System.Collections;
using UnityEngine;

// Procedural recoil for shooters without an authored Attack clip. Kick() shoves `target` back from the shot
// and tilts it up (or shakes it), easing back to rest over `returnTime`. The offset is added in LateUpdate on
// top of whatever the Animator or aiming code wrote, and taken off again at the end of the frame, so it never
// builds up or fights them.
public class RecoilKick : MonoBehaviour
{
    public Transform target;          // the part that kicks (body, torso, tower head)
    public float distance = 0.15f;    // metres back at full kick
    public float angle = 6f;          // degrees of muzzle climb
    public float returnTime = 0.12f;  // seconds to settle
    public bool shake;                // ArcCoil: a jitter instead of a kick

    float k;                          // 1 = just fired, 0 = at rest
    Vector3 dir = Vector3.forward;    // shot direction, flat
    Vector3 restPos;
    Quaternion restRot;
    bool applied, restoring;

    public void Kick(Vector3 shotDirection, float strength = 1f)
    {
        if (target == null) return;
        shotDirection.y = 0f;
        dir = shotDirection.sqrMagnitude > 0.001f ? shotDirection.normalized : transform.forward;
        k = Mathf.Max(k, strength);
        if (!restoring && isActiveAndEnabled) StartCoroutine(RestoreEachFrame());
    }

    void LateUpdate()
    {
        if (k <= 0f || target == null) return;
        restPos = target.localPosition;
        restRot = target.localRotation;
        applied = true;
        float e = k * k; // snaps back fast, settles gently
        if (shake) target.position += Random.insideUnitSphere * distance * e;
        else
        {
            target.position -= dir * distance * e;
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            target.rotation = Quaternion.AngleAxis(-angle * e, right) * target.rotation; // muzzle climbs
        }
        k = Mathf.Max(0f, k - Time.deltaTime / Mathf.Max(0.01f, returnTime));
    }

    IEnumerator RestoreEachFrame()
    {
        restoring = true;
        var endOfFrame = new WaitForEndOfFrame();
        do
        {
            yield return endOfFrame; // after rendering: put it back before the Animator / aiming run again
            if (applied && target != null) { target.localPosition = restPos; target.localRotation = restRot; }
            applied = false;
        } while (k > 0f);
        restoring = false;
    }

    void OnDisable()
    {
        if (applied && target != null) { target.localPosition = restPos; target.localRotation = restRot; }
        applied = restoring = false;
        k = 0f;
    }
}
