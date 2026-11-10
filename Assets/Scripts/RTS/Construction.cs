using UnityEngine;
using UnityEngine.UI;

// Added to a freshly placed building. The pack's ConstructionSite does the look (pad, scaffold,
// hologram, scan frame, welding drones, building rising out of the ground); this script keeps the
// building's own scripts off until it's done and shows our neon progress bar.
// Multiplayer, another player's building (remote): just the look, following the progress its owner sends.
public class Construction : MonoBehaviour
{
    public float duration = 10f;
    public RectTransform barTemplate; // disabled UI template with a "Fill" child (Image, Filled)
    public float kitScale = 4f;       // our buildings are 4x the pack's size
    public float startProgress;       // 0..1, loading a save
    public bool remote;               // set by NetEntity on other players' buildings

    public float Progress => site != null ? site.Progress : startProgress;
    public Vector3 PlacedAt { get; private set; } // where it was put down (it sinks while being built)

    MonoBehaviour[] features; // the building's own scripts, paused while under construction
    ConstructionSite site;
    Collider col;
    float fullTop;
    RectTransform bar;
    Image fill;
    Camera cam;

    void Awake() => PlacedAt = transform.position;

    void Start()
    {
        if (remote)
        {
            site = ConstructionSite.Attach(gameObject, 1e6f, kitScale); // never finishes on its own
            site.Skip(startProgress);
            SetAbilities(false); // a half-built cloak field or uplink does nothing yet
            return;
        }

        bool wall = TryGetComponent(out Structure st) && st.data != null && st.data.dragLine;
        if (!wall) BuildSlots.Busy.Add(this); // takes one of the 2 build slots until done
        cam = Camera.main;
        col = GetComponent<Collider>();
        fullTop = col.bounds.max.y; // finished height, measured before it sinks
        features = GetComponents<MonoBehaviour>();
        foreach (var f in features) if (f != this && f is not Health && f is not Fusion.NetworkBehaviour) f.enabled = false; // can be shot while building

        if (barTemplate != null && TeamEconomy.TeamOf(this) == 0) // AI sites: no bar
        {
            bar = Instantiate(barTemplate, barTemplate.parent);
            bar.SetSiblingIndex(barTemplate.GetSiblingIndex() + 1); // stays behind the HUD panels
            bar.gameObject.SetActive(true);
            fill = bar.Find("Fill").GetComponent<Image>();
            fill.fillAmount = 0f;
        }

        site = ConstructionSite.Attach(gameObject, duration, kitScale);
        if (startProgress > 0f) site.Skip(startProgress);
    }

    // Remote: the owner's progress; done when the owner says so.
    public void SetRemoteProgress(float p) { if (site != null) site.Skip(p); else startProgress = p; }

    public void FinishRemote()
    {
        if (site != null) site.Skip(1f); // the site completes itself next frame
        SetAbilities(true);
        Juice.ConstructionDone(gameObject);
        Destroy(this);
    }

    void SetAbilities(bool on)
    {
        if (TryGetComponent(out CloakField c)) c.enabled = on;
        if (TryGetComponent(out Revealer r)) r.enabled = on;
    }

    void Update()
    {
        if (remote) return;
        if (fill != null) fill.fillAmount = site.Progress;
        if (site.Progress < 1f) return;

        foreach (var f in features) if (f != this && f != null && f is not Fusion.NetworkBehaviour) f.enabled = true;
        SendMessage("OnConstructed", SendMessageOptions.DontRequireReceiver); // e.g. FreeHarvester
        Juice.ConstructionDone(gameObject);
        if (TeamEconomy.TeamOf(this) == 0)
        {
            if (TryGetComponent(out Structure s)) Alerts.ShowAt($"Construction complete: {s.Name}", Alerts.Good, transform.position);
            MatchStats.Add(MatchStats.Kind.BuildingsBuilt);
        }
        Destroy(this);
    }

    void LateUpdate()
    {
        if (remote || bar == null) return;
        // Bar floats just above where the finished roof will be.
        Bounds b = col.bounds;
        Vector3 screen = cam.WorldToScreenPoint(new Vector3(b.center.x, fullTop, b.center.z));
        bar.gameObject.SetActive(screen.z > 0f);
        bar.position = screen + Vector3.up * 16f;
    }

    void OnDestroy()
    {
        BuildSlots.Busy.Remove(this);
        if (bar != null) Destroy(bar.gameObject);
    }
}
