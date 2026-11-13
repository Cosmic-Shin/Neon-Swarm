using UnityEngine;
using UnityEngine.UI;

public class Construction : MonoBehaviour
{
    public float duration = 10f;
    public RectTransform barTemplate;
    public float kitScale = 4f;
    public float startProgress;
    public bool remote;
    public bool intro;

    public float Progress => site != null ? site.Progress : startProgress;
    public Vector3 PlacedAt { get; private set; }

    MonoBehaviour[] features;
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
            site = ConstructionSite.Attach(gameObject, 1e6f, kitScale);
            site.Skip(startProgress);
            SetAbilities(false);
            return;
        }

        bool wall = TryGetComponent(out Structure st) && st.data != null && st.data.dragLine;
        if (!wall && !intro) BuildSlots.Busy.Add(this);
        cam = Camera.main;
        col = GetComponent<Collider>();
        fullTop = col.bounds.max.y;
        features = GetComponents<MonoBehaviour>();
        foreach (var f in features) if (f != this && f is not Health && f is not Fusion.NetworkBehaviour && !(intro && f is Structure)) f.enabled = false;

        if (barTemplate != null && TeamEconomy.TeamOf(this) == 0)
        {
            bar = Instantiate(barTemplate, barTemplate.parent);
            bar.SetSiblingIndex(barTemplate.GetSiblingIndex() + 1);
            bar.gameObject.SetActive(true);
            fill = bar.Find("Fill").GetComponent<Image>();
            fill.fillAmount = 0f;
        }

        site = ConstructionSite.Attach(gameObject, duration, kitScale);
        if (startProgress > 0f) site.Skip(startProgress);
    }

    public void SetRemoteProgress(float p) { if (site != null) site.Skip(p); else startProgress = p; }

    public void FinishRemote()
    {
        if (this == null) return;
        if (site != null) site.Skip(1f);
        SetAbilities(true);
        Juice.ConstructionDone(gameObject);
        Destroy(this);
    }

    void SetAbilities(bool on)
    {
        if (TryGetComponent(out CloakField c)) c.enabled = on;
        if (TryGetComponent(out Revealer r)) r.enabled = on;
    }

    float nextClank;

    void Update()
    {
        if (Time.time >= nextClank && site != null && site.Progress < 1f)
        {
            nextClank = Time.time + Random.Range(0.5f, 1.1f);
            Sfx.At(Snd.Weld, transform.position, 0.3f);
        }
        if (remote) return;
        if (fill != null) fill.fillAmount = site.Progress;
        if (site.Progress < 1f) return;
        Complete();
    }

    public void Complete()
    {
        if (remote) { FinishRemote(); return; }
        if (site != null) site.Skip(1f);
        if (features == null) { Destroy(this); return; }
        foreach (var f in features) if (f != this && f != null && f is not Fusion.NetworkBehaviour) f.enabled = true;
        SendMessage("OnConstructed", SendMessageOptions.DontRequireReceiver);
        Juice.ConstructionDone(gameObject);
        if (TeamEconomy.TeamOf(this) == 0 && !intro)
        {
            if (TryGetComponent(out Structure s)) Alerts.ShowAt($"Construction complete: {s.Name}", Alerts.Good, transform.position);
            Sfx.Say(Line.Built, 5f);
            MatchStats.Add(MatchStats.Kind.BuildingsBuilt);
        }
        Destroy(this);
    }

    void LateUpdate()
    {
        if (remote || bar == null) return;
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
