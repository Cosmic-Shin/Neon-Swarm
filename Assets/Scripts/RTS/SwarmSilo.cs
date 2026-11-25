using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(Health))]
public class SwarmSilo : MonoBehaviour
{
    public const float ChargeTime = 420f, StrikeRadius = 22f, EmpRadius = 15f;
    const float ArmTime = 40f / 24f;

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    public static readonly Color Pink = new(1f, 0.05f, 0.6f);
    public static readonly List<SwarmSilo> All = new();

    public GameObject podPrefab, shockBig, shockPulse, shockRing, bolt, locustPrefab;
    public bool orbital;
    public GameObject beamPrefab, impactPrefab;
    public string Label => orbital ? "Orbital Lance" : "Swarm Silo";
    public Color Tint => orbital ? OrbitalStrike.Hot : Pink;
    float Arm => orbital ? 25f / 24f : ArmTime;

    Health health;
    NetEntity net;
    Animator anim;
    BuildingLife life;
    Transform launchPoint, chargeE, chargeW;
    Renderer[] pod = System.Array.Empty<Renderer>();
    readonly List<Renderer>[] steps = { new(), new(), new() };
    readonly List<Renderer> readyRing = new();
    MaterialPropertyBlock block;
    Light beacon;
    LineRenderer beam;
    float seconds, nextSpark, nextArc, nextVent;
    bool firing, podGone, announced, readySaid, remoteReady;
    int shownStep = -1;
    Color lit = Color.cyan * 6f;

    public int Team => health.team;
    public float Seconds => seconds;
    public float Left => Mathf.Max(0f, ChargeTime - seconds);
    public float Charge01 => Mathf.Clamp01(seconds / ChargeTime);
    public bool Built => !TryGetComponent(out Construction _);
    bool Remote => net != null && net.IsRemote;
    public bool Paused => !Ready && !firing && Structure.LowPowerOf(health.team);
    public bool Firing => firing;
    public bool PodGone => podGone;
    public bool Ready => Built && !health.IsDead && (Remote ? remoteReady : seconds >= ChargeTime && !firing);
    Vector3 LaunchAt => launchPoint != null ? launchPoint.position : transform.position + Vector3.up * 12f;

    void Awake()
    {
        health = GetComponent<Health>();
        net = GetComponent<NetEntity>();
        anim = GetComponentInChildren<Animator>();
        block = new MaterialPropertyBlock();
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            switch (t.name)
            {
                case "Silo_LaunchPoint": launchPoint = t; break;
                case "OL_BeamPoint": launchPoint = t; break;
                case "Silo_ChargeFX_E": chargeE = t; break;
                case "Silo_ChargeFX_W": chargeW = t; break;
                case "Silo_Pod": pod = t.GetComponentsInChildren<Renderer>(true); break;
                case "Silo_ReadyRing": readyRing.AddRange(t.GetComponentsInChildren<Renderer>(true)); break;
            }
            for (int side = 0; side < 2; side++)
                for (int step = 0; step < 3; step++)
                    if (t.name == $"Silo_Charge{side}_{step}" && t.TryGetComponent(out Renderer r)) steps[step].Add(r);
        }
    }

    void Start() => life = GetComponent<BuildingLife>();
    void OnEnable() => All.Add(this);
    void OnDisable() { All.Remove(this); if (beacon != null) beacon.enabled = false; if (beam != null) beam.enabled = false; }

    public void Restore(float saved, bool gone)
    {
        seconds = Mathf.Clamp(saved, 0f, ChargeTime);
        announced = true;
        readySaid = seconds >= ChargeTime;
        podGone = gone && seconds < ChargeTime;
        SetPod(!podGone);
        shownStep = -1;
    }

    public void ShowRemote(float netSeconds, bool ready, bool gone)
    {
        seconds = netSeconds;
        remoteReady = ready;
        if (gone && !podGone && !firing) { podGone = true; SetPod(false); }
    }

    void Update()
    {
        if (!Built || health.IsDead) { ShowBeacon(false); return; }
        Announce();
        bool stunned = Structure.Stunned(this);
        if (!Remote && !firing && seconds < ChargeTime && !stunned && !Structure.LowPowerOf(health.team)) seconds += Time.deltaTime;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!Remote && health.team == 0 && Chat.Keys is Keyboard kb && kb.f8Key.wasPressedThisFrame) seconds = ChargeTime;
#endif
        if (Ready && podGone) { podGone = false; SetPod(true); ReloadFx(); }
        if (Ready && !readySaid) SayReady();
        ShowCharge();
        ShowBeacon(Ready && !stunned);
    }

    void SayReady()
    {
        readySaid = true;
        if (health.team == 0)
        {
            Alerts.ShowAt($"{Label} ready", Alerts.Good, transform.position);
            Sfx.Say(Line.Ready, 1f);
        }
        else if (!Net.Allied(health.team, 0))
        {
            Alerts.ShowAt($"Enemy {Label} is ready to fire", Alerts.Bad, transform.position, 10f);
            Sfx.Cue("error_004", 0.6f, 0.75f);
        }
    }

    void Announce()
    {
        if (announced) return;
        announced = true;
        if (Time.timeSinceLevelLoad < 3f || Net.Allied(health.team, 0)) return;
        Alerts.ShowAt($"{Label} detected", Alerts.Bad, transform.position);
        Sfx.Cue("error_006", 0.7f, 0.8f);
    }

    void ReloadFx()
    {
        if (!Juice.Seen(transform.position)) return;
        Juice.Ring(transform.position + Vector3.up * 0.3f, Tint, 4f, 18f, 0.8f, 0.4f);
        Juice.Burst(LaunchAt, Tint, 24, 8f, 0.15f, 0.6f, 1f);
        if (life != null) life.Kick(0.8f);
        Sfx.CueAt("doorClose_000", transform.position, 0.7f, 0.7f, 0f);
    }

    void ShowCharge()
    {
        float c = Charge01;
        int step = c >= 1f ? 3 : c >= 0.66f ? 2 : c >= 0.33f ? 1 : 0;
        if (step != shownStep)
        {
            if (step > shownStep && shownStep >= 0 && step > 0) StepFx(step);
            shownStep = step;
            for (int i = 0; i < steps.Length; i++)
                foreach (var r in steps[i]) Glow(r, i < step ? lit : Color.black);
        }
        float pulse = Ready ? 1.5f + 3.5f * (0.5f + 0.5f * Mathf.Sin(Time.time * 4f)) : 0f;
        foreach (var r in readyRing) Glow(r, Tint * pulse);
    }

    void StepFx(int step)
    {
        if (!Juice.Seen(transform.position)) return;
        foreach (var p in new[] { chargeE, chargeW })
        {
            if (p == null) continue;
            Juice.Burst(p.position, Color.cyan, 10 + step * 6, 6f, 0.12f, 0.5f, 1f);
            Juice.Flash(p.position, Color.cyan, 3f + step, 12f, 0.35f);
        }
        Juice.Ring(transform.position + Vector3.up * 0.3f, Color.cyan, 5f, 12f + step * 3f, 0.6f, 0.2f);
        if (life != null) life.Kick(0.35f + step * 0.15f);
        Sfx.CueAt("forceField_002", transform.position, 0.65f, 0.7f + step * 0.18f, 0f);
    }

    void ShowBeacon(bool on)
    {
        if (on && beacon == null) MakeBeacon();
        if (beacon == null) return;
        bool seen = on && Juice.Seen(transform.position);
        beacon.enabled = beam.enabled = seen;
        if (!seen) return;
        float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
        beacon.intensity = (1.5f + 3f * k) * GameSettings.FlashScale;
        Vector3 from = LaunchAt;
        beam.SetPosition(0, from);
        beam.SetPosition(1, from + Vector3.up * 34f);
        beam.widthMultiplier = 0.14f + 0.2f * k;
        beam.startColor = new Color(Tint.r, Tint.g, Tint.b, 0.18f + 0.2f * k);
        beam.endColor = new Color(Tint.r, Tint.g, Tint.b, 0f);
        if (Time.time >= nextArc && chargeE != null && chargeW != null && bolt != null)
        {
            nextArc = Time.time + Random.Range(0.5f, 1.4f);
            Vector3 d = chargeW.position - chargeE.position;
            Fx.Spawn(bolt, chargeE.position, Quaternion.LookRotation(d), new Vector3(0.6f, 0.6f, d.magnitude));
            Sfx.At(Snd.Zap, chargeE.position, 0.16f);
        }
        if (Time.time >= nextVent)
        {
            nextVent = Time.time + 0.35f;
            Vector2 c = Random.insideUnitCircle.normalized * 9f;
            Particles.Emit("Steam", transform.position + new Vector3(c.x, 0.5f, c.y), Vector3.up * 2.2f, 2.6f, 1.5f, new Color(1f, 0.55f, 0.85f, 0.2f));
        }
    }

    void MakeBeacon()
    {
        var go = new GameObject("SiloBeacon");
        go.transform.SetParent(transform, false);
        go.transform.position = LaunchAt + Vector3.up * 2f;
        beacon = go.AddComponent<Light>();
        beacon.type = LightType.Point;
        beacon.color = Tint;
        beacon.range = 34f;
        beacon.shadows = LightShadows.None;
        beam = go.AddComponent<LineRenderer>();
        beam.sharedMaterial = Juice.AdditiveLine;
        beam.positionCount = 2;
        beam.useWorldSpace = true;
        beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void Glow(Renderer r, Color c)
    {
        r.GetPropertyBlock(block);
        block.SetColor(EmissionId, c);
        r.SetPropertyBlock(block);
    }

    void SetPod(bool on) { foreach (var r in pod) r.enabled = on; }

    public void Fire(Vector3 at)
    {
        if (!Ready || Remote) return;
        if (net != null && net.Online) net.RPC_SiloLaunch(at);
        else Launch(at, true);
    }

    public void Launch(Vector3 at, bool authority)
    {
        if (firing) return;
        firing = true;
        remoteReady = false;
        if (anim != null && !orbital) { anim.ResetTrigger("Close"); anim.SetTrigger("Arm"); }
        if (anim != null && orbital) anim.SetTrigger("Fire");
        if (orbital && TryGetComponent(out AimPivot dish)) dish.LookAt(at, 16f);
        Sfx.CueAt("doorOpen_000", transform.position, 1f, 0.55f, 0.1f);
        Sfx.CueAt("computerNoise_000", transform.position, 0.7f, 0.7f, 0f);
        if (life != null) life.Kick(-0.6f);
        StartCoroutine(Sequence(at, authority));
    }

    IEnumerator Sequence(Vector3 at, bool authority)
    {
        float nextAlarm = 0f;
        for (float t = 0f; t < Arm; t += Time.deltaTime)
        {
            bool seen = Juice.Seen(transform.position);
            if (t >= nextAlarm)
            {
                nextAlarm += 0.42f;
                Sfx.CueAt("error_006", transform.position, 0.45f, 0.75f, 0f);
                if (seen)
                {
                    Juice.Flash(LaunchAt + Vector3.up * 3f, Juice.Danger, 5f, 30f, 0.3f);
                    Juice.Shake(transform.position, 0.12f);
                }
            }
            if (seen && Time.time >= nextSpark)
            {
                nextSpark = Time.time + 0.07f;
                foreach (var p in new[] { chargeE, chargeW })
                    if (p != null) Juice.Burst(p.position, Tint, 5, 6f, 0.11f, 0.3f, 0.6f);
                Vector2 c = Random.insideUnitCircle.normalized * Random.Range(6f, 10f);
                Particles.Emit("Steam", transform.position + new Vector3(c.x, 0.6f, c.y), new Vector3(c.x * 0.3f, 2.5f, c.y * 0.3f), 4f, 1.2f, new Color(0.85f, 0.8f, 0.9f, 0.35f));
                Particles.Emit("Print", LaunchAt, Vector3.up * Random.Range(10f, 22f), 0.5f, 0.5f, Tint);
            }
            yield return null;
        }
        if (health.IsDead) yield break;
        SetPod(false);
        podGone = true;
        readySaid = false;
        remoteReady = false;
        seconds = 0f;
        firing = false;
        shownStep = -1;
        if (orbital) { OrbitalStrike.Begin(this, LaunchAt, at, authority); yield break; }
        LaunchFx();
        SwarmStrike.Begin(this, LaunchAt, at, authority);
        yield return new WaitForSeconds(0.6f);
        if (anim != null) anim.SetTrigger("Close");
        Sfx.CueAt("doorClose_001", transform.position, 0.8f, 0.6f, 0f);
    }

    void LaunchFx()
    {
        Vector3 from = LaunchAt, ground = new(transform.position.x, 0.3f, transform.position.z);
        Sfx.CueAt("lowFrequency_explosion_000", from, 0.85f, 1.45f, 0.2f);
        Sfx.CueAt("thrusterFire_002", from, 1f, 0.5f, 0.15f);
        if (Net.Allied(health.team, 0)) Sfx.Say(Line.Launch, 1f);
        if (!Juice.Seen(from)) return;
        Juice.Flash(from + Vector3.up * 2f, Juice.Fire, 12f, 45f, 0.7f);
        Juice.Ring(ground, Juice.Fire, 4f, 32f, 0.9f, 0.7f);
        Juice.Ring(ground, Color.white, 3f, 18f, 0.4f, 0.3f);
        Juice.Burst(from, Juice.Fire, 90, 24f, 0.24f, 0.8f, 1.6f, true);
        Juice.Shake(from, 0.85f);
        if (life != null) life.Kick(-2f);
        for (int i = 0; i < 28; i++)
        {
            float a = i * Mathf.PI * 2f / 28f;
            Vector3 dir = new(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Particles.Emit("Smoke", ground + dir * 8f, dir * Random.Range(7f, 13f) + Vector3.up * 0.8f, Random.Range(5f, 8f), Random.Range(1.6f, 2.8f), new Color(0.42f, 0.38f, 0.44f, 0.55f));
        }
    }

    public static LineRenderer MakeRing(string name, float radius, Color color, float width)
    {
        var lr = new GameObject(name).AddComponent<LineRenderer>();
        lr.sharedMaterial = new Material(Juice.AdditiveLine) { renderQueue = 3300 };
        lr.loop = true;
        lr.useWorldSpace = false;
        lr.alignment = LineAlignment.TransformZ;
        lr.widthMultiplier = width;
        lr.startColor = lr.endColor = color;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.positionCount = 72;
        for (int i = 0; i < 72; i++)
        {
            float a = i * Mathf.PI * 2f / 72f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius);
        }
        lr.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        return lr;
    }

    public static void Remove(LineRenderer lr)
    {
        if (lr == null) return;
        Destroy(lr.sharedMaterial);
        Destroy(lr.gameObject);
    }

    public static string Clock(float s)
    {
        int total = Mathf.CeilToInt(s);
        return $"{total / 60}:{total % 60:00}";
    }
}

public class SiloTargeting : MonoBehaviour
{
    public static bool Active { get; private set; }

    SwarmSilo silo;
    GoodSelectionManager manager;
    LineRenderer outer, inner;
    Text readout;
    bool armed;
    float nextCount;

    public static void Begin(SwarmSilo silo, GoodSelectionManager manager)
    {
        if (Active || silo == null || !silo.Ready || BuildingPlacer.Placing) return;
        var t = new GameObject("SiloTargeting").AddComponent<SiloTargeting>();
        t.silo = silo;
        t.manager = manager;
        t.outer = SwarmSilo.MakeRing("SiloPreview", SwarmSilo.StrikeRadius, new Color(1f, 0.1f, 0.15f), 0.7f);
        t.inner = SwarmSilo.MakeRing("SiloPreviewEmp", SwarmSilo.EmpRadius, new Color(1f, 0.15f, 0.55f), 0.3f);
        t.readout = SwarmStrike.MakeLabel("SiloPreviewText", 24);
        manager.enabled = false;
        Active = true;
        Sfx.Cue("forceField_002", 0.5f, 1.3f);
        Alerts.Show("Click a target, right-click cancels", Alerts.Warn, 2f, true);
    }

    void Update()
    {
        var mouse = Mouse.current;
        bool over = MatchManager.Instance != null && MatchManager.Instance.IsOver;
        if (silo == null || !silo.Ready || mouse == null || over) { Destroy(gameObject); return; }
        if (Chat.Keys is Keyboard kb && kb.escapeKey.wasPressedThisFrame) { Destroy(gameObject); return; }
        if (mouse.rightButton.wasReleasedThisFrame) { Destroy(gameObject); return; }

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        bool ground = Physics.Raycast(manager.Cam.ScreenPointToRay(mouse.position.ReadValue()), out RaycastHit hit, manager.MaxRayDistance, manager.GroundLayer);
        bool show = ground && !overUI;
        outer.enabled = inner.enabled = show;
        readout.gameObject.SetActive(show);
        if (show)
        {
            Vector3 at = hit.point + Vector3.up * 0.25f;
            outer.transform.position = inner.transform.position = at;
            outer.widthMultiplier = 0.6f + 0.2f * Mathf.Sin(Time.unscaledTime * 6f);
            inner.transform.Rotate(0f, 0f, 40f * Time.unscaledDeltaTime, Space.Self);
            readout.transform.position = mouse.position.ReadValue() + Vector2.up * 54f;
            if (Time.unscaledTime >= nextCount)
            {
                nextCount = Time.unscaledTime + 0.12f;
                SwarmStrike.Count(hit.point, silo.Team, out int buildings, out int units);
                readout.text = buildings + units == 0 ? "NO KNOWN TARGETS" : $"{buildings} BUILDINGS  ·  {units} UNITS";
                readout.color = buildings + units == 0 ? new Color(0.75f, 0.8f, 0.9f) : Color.white;
            }
        }
        if (mouse.leftButton.wasPressedThisFrame && show) armed = true;
        if (armed && mouse.leftButton.wasReleasedThisFrame)
        {
            armed = false;
            if (show) { silo.Fire(hit.point); Destroy(gameObject); }
        }
    }

    void OnDestroy()
    {
        Active = false;
        SwarmSilo.Remove(outer);
        SwarmSilo.Remove(inner);
        if (readout != null) Destroy(readout.transform.parent.gameObject);
        if (manager != null) manager.enabled = true;
    }
}

public class SiloHud : MonoBehaviour
{
    const float Right = -292f, Top = -80f, Step = 48f;

    class Chip { public RectTransform box, bar; public Image back, fill; public Text text; public string sprite; public SwarmSilo silo; }

    readonly List<Chip> chips = new();
    readonly List<SwarmSilo> shown = new();
    GoodSelectionManager manager;

    void LateUpdate()
    {
        shown.Clear();
        foreach (var s in SwarmSilo.All) if (s.Built && !s.GetComponent<Health>().IsDead) shown.Add(s);
        shown.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
        while (chips.Count < shown.Count) chips.Add(Make());
        for (int i = 0; i < chips.Count; i++)
        {
            bool on = i < shown.Count;
            if (chips[i].box.gameObject.activeSelf != on) chips[i].box.gameObject.SetActive(on);
            if (on) Fill(chips[i], shown[i]);
        }
    }

    static int Rank(SwarmSilo s) => s.Team == 0 ? 0 : Net.Allied(s.Team, 0) ? 1 : 2;

    void Fill(Chip chip, SwarmSilo s)
    {
        chip.silo = s;
        int rank = Rank(s);
        string kind = s.orbital ? "LANCE" : "SILO";
        string who = rank == 0 ? $"YOUR {kind}" : rank == 1 ? $"ALLY {kind}" : $"ENEMY {kind}";
        string state = s.Ready ? "READY" : s.Firing ? "LAUNCH" : rank == 0 && s.Paused ? "PAUSED" : SwarmSilo.Clock(s.Left);
        chip.text.text = $"{who}  <b>{state}</b>";
        string sprite = rank == 2 ? "Chip_Pink" : s.Ready ? "Chip_Green" : "Chip_Cyan";
        if (sprite != chip.sprite) { HoloUI.SetSliced(chip.back, sprite); chip.sprite = sprite; }
        bool urgent = s.Ready || s.Left <= 30f;
        float blink = 0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * (s.Ready ? 5f : 3f));
        chip.text.color = urgent ? new Color(1f, 1f, 1f, blink) : Color.white;
        chip.bar.anchorMax = new Vector2(s.Charge01, 1f);
        chip.fill.color = rank == 2 ? HoloUI.Pink : s.Paused ? HoloUI.Yellow : s.Ready ? HoloUI.Green : HoloUI.Cyan;
    }

    void Click(Chip chip)
    {
        var s = chip.silo;
        if (s == null) return;
        if (manager == null) manager = FindFirstObjectByType<GoodSelectionManager>();
        CameraPan.LookAt(s.transform.position);
        if (s.Team != 0 || manager == null || !manager.enabled) return;
        manager.SelectStructure(s.GetComponent<Structure>());
        if (s.Ready) SiloTargeting.Begin(s, manager);
    }

    Chip Make()
    {
        int i = chips.Count;
        var chip = new Chip { sprite = "Chip_Cyan" };
        chip.box = new GameObject("SiloChip", typeof(RectTransform)).GetComponent<RectTransform>();
        chip.box.SetParent(transform, false);
        chip.box.anchorMin = chip.box.anchorMax = chip.box.pivot = Vector2.one;
        chip.box.anchoredPosition = new Vector2(Right, Top - i * Step);
        chip.box.sizeDelta = new Vector2(250f, 42f);
        chip.back = HoloSkin.Back(chip.box, "Chip_Cyan");
        chip.back.raycastTarget = true;
        var button = chip.box.gameObject.AddComponent<Button>();
        button.targetGraphic = chip.back;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => Click(chip));

        var track = new GameObject("Track", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        track.SetParent(chip.box, false);
        track.anchorMin = new Vector2(0f, 0f);
        track.anchorMax = new Vector2(1f, 0f);
        track.offsetMin = new Vector2(14f, 5f);
        track.offsetMax = new Vector2(-14f, 8f);
        var trackImage = track.GetComponent<Image>();
        trackImage.color = new Color(1f, 1f, 1f, 0.12f);
        trackImage.raycastTarget = false;
        chip.bar = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        chip.bar.SetParent(track, false);
        chip.bar.anchorMin = Vector2.zero;
        chip.bar.anchorMax = Vector2.one;
        chip.bar.offsetMin = chip.bar.offsetMax = Vector2.zero;
        chip.fill = chip.bar.GetComponent<Image>();
        chip.fill.raycastTarget = false;

        chip.text = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        chip.text.transform.SetParent(chip.box, false);
        chip.text.rectTransform.anchorMin = Vector2.zero;
        chip.text.rectTransform.anchorMax = Vector2.one;
        chip.text.rectTransform.offsetMin = new Vector2(0f, 4f);
        chip.text.rectTransform.offsetMax = Vector2.zero;
        chip.text.font = HoloSkin.Font;
        chip.text.fontSize = 16;
        chip.text.alignment = TextAnchor.MiddleCenter;
        chip.text.supportRichText = true;
        chip.text.raycastTarget = false;
        return chip;
    }
}
