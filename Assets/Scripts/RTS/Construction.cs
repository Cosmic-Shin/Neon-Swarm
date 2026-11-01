using UnityEngine;
using UnityEngine.UI;

// Added to a freshly placed building: it rises out of the ground over `duration` seconds with a
// progress bar above it, and its scripts (unit production, refinery docking) stay off until it's finished.
public class Construction : MonoBehaviour
{
    public float duration = 10f;
    public RectTransform barTemplate; // disabled UI template with a "Fill" child (Image, Filled)

    float elapsed;
    Vector3 fullScale;
    float fullTop;
    MonoBehaviour[] features; // the building's own scripts, paused while under construction
    Collider col;
    RectTransform bar;
    Image fill;
    Camera cam;

    void Start()
    {
        cam = Camera.main;
        fullScale = transform.localScale;
        col = GetComponent<Collider>();
        fullTop = col.bounds.max.y; // finished height, measured before it's squashed
        features = GetComponents<MonoBehaviour>();
        foreach (var f in features) if (f != this && f is not Health) f.enabled = false; // can be shot while building

        bar = Instantiate(barTemplate, barTemplate.parent);
        bar.SetSiblingIndex(barTemplate.GetSiblingIndex() + 1); // stays behind the HUD panels
        bar.gameObject.SetActive(true);
        fill = bar.Find("Fill").GetComponent<Image>();
        Apply(0f);
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float k = Mathf.Clamp01(elapsed / duration);
        Apply(k);
        if (k < 1f) return;

        foreach (var f in features) if (f != this && f != null) f.enabled = true;
        SendMessage("OnConstructed", SendMessageOptions.DontRequireReceiver); // e.g. FreeHarvester
        if (TryGetComponent(out Structure s)) Alerts.Show($"Construction complete: {s.Name}", Alerts.Good);
        Destroy(this);
    }

    void LateUpdate()
    {
        // Bar floats just above where the finished roof will be.
        Bounds b = col.bounds;
        Vector3 screen = cam.WorldToScreenPoint(new Vector3(b.center.x, fullTop, b.center.z));
        bar.gameObject.SetActive(screen.z > 0f);
        bar.position = screen + Vector3.up * 16f;
    }

    void Apply(float k)
    {
        transform.localScale = new Vector3(fullScale.x, fullScale.y * Mathf.Max(k, 0.02f), fullScale.z);
        fill.fillAmount = k;
    }

    void OnDestroy()
    {
        if (bar != null) Destroy(bar.gameObject);
    }
}
