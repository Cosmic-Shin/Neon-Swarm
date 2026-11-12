using System.Collections;
using UnityEngine;

public class RecoilKick : MonoBehaviour
{
    public Transform target;
    public float distance = 0.15f;
    public float angle = 6f;
    public float returnTime = 0.12f;
    public bool shake;

    float k;
    Vector3 dir = Vector3.forward;
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
        float e = k * k;
        if (shake) target.position += Random.insideUnitSphere * distance * e;
        else
        {
            target.position -= dir * distance * e;
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            target.rotation = Quaternion.AngleAxis(-angle * e, right) * target.rotation;
        }
        k = Mathf.Max(0f, k - Time.deltaTime / Mathf.Max(0.01f, returnTime));
    }

    IEnumerator RestoreEachFrame()
    {
        restoring = true;
        var endOfFrame = new WaitForEndOfFrame();
        do
        {
            yield return endOfFrame;
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
