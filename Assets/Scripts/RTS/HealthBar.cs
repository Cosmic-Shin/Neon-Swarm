using UnityEngine;

public class HealthBar : MonoBehaviour
{
    [SerializeField] Health health;
    [SerializeField] SelectableUnit unit;
    [SerializeField] SpriteRenderer back;
    [SerializeField] SpriteRenderer fill;
    [SerializeField] float width = 2f;
    [SerializeField] float height = 0.22f;

    static readonly Color High = new(0.2f, 1f, 1f);
    static readonly Color Mid = new(1f, 0.9f, 0.2f);
    static readonly Color Low = new(1f, 0.2f, 0.6f);

    Camera cam;
    Structure structure;
    SpriteRenderer trail;
    float trailF = 1f, lastF = 1f, drainAt;

    void Awake()
    {
        cam = Camera.main;
        if (unit == null) structure = GetComponentInParent<Structure>();
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
        if (f < lastF) drainAt = Time.time + 0.35f;
        lastF = f;
        trailF = f >= trailF ? f : Time.time < drainAt ? trailF : Mathf.MoveTowards(trailF, f, Time.deltaTime * 0.6f);
        if (!show) return;

        transform.rotation = cam.transform.rotation;

        Place(fill, f, -0.01f);
        Place(trail, trailF, -0.005f);
        fill.color = f > 0.5f ? High : f > 0.25f ? Mid : Low;
    }

    void Place(SpriteRenderer bar, float f, float z)
    {
        bar.transform.localScale = new Vector3(width * f, height, 1f);
        bar.transform.localPosition = new Vector3(-width * (1f - f) * 0.5f, 0f, z);
    }
}
