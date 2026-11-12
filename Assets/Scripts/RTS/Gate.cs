using UnityEngine;

public class Gate : MonoBehaviour
{
    [SerializeField] Transform[] slats;
    [SerializeField] Transform lintel;
    public Transform Lintel => lintel;
    [SerializeField] float openTime = 0.5f;

    Vector3[] closed, open;
    float openUntil, t;

    void Awake()
    {
        closed = new Vector3[slats.Length];
        open = new Vector3[slats.Length];
        for (int i = 0; i < slats.Length; i++)
        {
            var s = slats[i];
            closed[i] = s.localPosition;
            Vector3 rise = Vector3.up * (lintel.position.y - s.position.y);
            open[i] = closed[i] + s.parent.InverseTransformVector(rise);
        }
    }

    public void Open(float seconds) => openUntil = Mathf.Max(openUntil, Time.time + seconds);

    void Update()
    {
        float target = Time.time < openUntil ? 1f : 0f;
        if (t == target) return;
        t = Mathf.MoveTowards(t, target, Time.deltaTime / openTime);
        for (int i = 0; i < slats.Length; i++) slats[i].localPosition = Vector3.Lerp(closed[i], open[i], t);
    }
}
