using UnityEngine;

// Neon bar over a unit or building. Shown while selected or hurt (C&C style).
public class HealthBar : MonoBehaviour
{
    [SerializeField] Health health;
    [SerializeField] SelectableUnit unit; // empty on buildings: bar shows only when hurt
    [SerializeField] SpriteRenderer back;
    [SerializeField] SpriteRenderer fill; // 1x1 unit sprite, scaled to the current health
    [SerializeField] float width = 2f;
    [SerializeField] float height = 0.22f;

    static readonly Color High = new(0.2f, 1f, 1f);    // cyan
    static readonly Color Mid = new(1f, 0.9f, 0.2f);   // yellow
    static readonly Color Low = new(1f, 0.2f, 0.6f);   // magenta

    Camera cam;
    Structure structure;
    SpriteRenderer trail; // white chunk of health just lost: waits a moment, then drains down to the fill
    float trailF = 1f, lastF = 1f, drainAt;

    void Awake()
    {
        cam = Camera.main;
        if (unit == null) structure = GetComponentInParent<Structure>(); // buildings: show while selected too
        back.transform.localScale = new Vector3(width + 0.16f, height + 0.16f, 1f);
        trail = Instantiate(fill, fill.transform.parent);
        trail.name = "Trail";
        trail.color = new Color(1f, 1f, 1f, 0.85f);
        trail.sortingOrder = fill.sortingOrder;
    }

    void LateUpdate()
    {
        bool show = (unit != null && unit.IsSelected) || (structure != null && structure.IsSelected) || health.IsDamaged;
        back.enabled = fill.enabled = trail.enabled = show;
        float f = health.Fraction;
        if (f < lastF) drainAt = Time.time + 0.35f; // each hit restarts the wait
        lastF = f;
        trailF = f >= trailF ? f : Time.time < drainAt ? trailF : Mathf.MoveTowards(trailF, f, Time.deltaTime * 0.6f);
        if (!show) return;

        transform.rotation = cam.transform.rotation; // always face the camera

        Place(fill, f, -0.01f);
        Place(trail, trailF, -0.005f); // just behind the fill
        fill.color = f > 0.5f ? High : f > 0.25f ? Mid : Low;
    }

    // Shrinks toward the left.
    void Place(SpriteRenderer bar, float f, float z)
    {
        bar.transform.localScale = new Vector3(width * f, height, 1f);
        bar.transform.localPosition = new Vector3(-width * (1f - f) * 0.5f, 0f, z);
    }
}
