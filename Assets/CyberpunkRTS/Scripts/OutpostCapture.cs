using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Neutral Derelict Outpost.
///  - Starts RUINED and neutral.
///  - Units of a single team standing in the capture zone raise capture progress (contested = paused).
///  - At 100% the outpost is RESTORED: it repairs itself and glows in the owner's team colour.
///  - Ownership lasts holdDuration seconds. The timer is refreshed while the owner keeps units garrisoned;
///    otherwise it decays back to neutral and ruined.
///  - Owners can garrison up to maxGarrison units inside (hidden, protected). Eject() puts them back out.
/// </summary>
public class OutpostCapture : MonoBehaviour
{
    [Header("Capture")]
    public float captureRadius = 2.2f;
    public float captureTime = 8f;            // seconds for one unit to capture
    public float extraUnitSpeedup = 0.35f;    // each extra unit speeds capture by 35%
    public float holdDuration = 180f;         // seconds owned before decaying (refreshed while garrisoned)
    public float decayTime = 6f;              // seconds to fall from owned back to neutral

    [Header("Garrison")]
    public int maxGarrison = 4;
    [Tooltip("Garrisoned units' weapon range is multiplied by this (read it from your combat code).")]
    public float garrisonRangeBonus = 1.25f;

    [Header("State (read only)")]
    public int ownerTeam = -1;          // -1 = neutral
    public int capturingTeam = -1;
    [Range(0, 1)] public float progress; // 0..1 toward capturingTeam
    public float holdTimeLeft;

    public bool IsOwned => ownerTeam >= 0;
    public int GarrisonCount => garrison.Count;
    public event System.Action<int> OnOwnerChanged;
    public event System.Action<int> OnGarrisonChanged;

    readonly List<TeamMember> garrison = new List<TeamMember>();
    readonly Dictionary<int, int> present = new Dictionary<int, int>();
    GameObject ruined, restored;
    Transform emblem, progressRing, exitPoint;
    readonly List<Transform> slots = new List<Transform>();
    readonly List<(Renderer r, int slot)> glow = new List<(Renderer, int)>();
    MaterialPropertyBlock block;
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    readonly Collider[] hits = new Collider[64];
    float scanTimer;

    void Awake()
    {
        block = new MaterialPropertyBlock();
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Outpost_Ruined") ruined = t.gameObject;
            else if (t.name == "Outpost_Restored") restored = t.gameObject;
            else if (t.name == "Outpost_Emblem") emblem = t;
            else if (t.name == "Out_CaptureProgress") progressRing = t;
            else if (t.name == "Outpost_ExitPoint") exitPoint = t;
            else if (t.name.StartsWith("Outpost_GarrisonSlot")) slots.Add(t);
        }
        slots.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] && mats[i].name.StartsWith("Neutral_Glow")) glow.Add((r, i));
        }
        ApplyState();
    }

    void Update()
    {
        // scan the zone a few times per second (cheap for an RTS with many units)
        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f) { scanTimer = 0.2f; Scan(); }

        int teamsHere = 0, soleTeam = -1, soleCount = 0;
        foreach (var kv in present) if (kv.Value > 0) { teamsHere++; soleTeam = kv.Key; soleCount = kv.Value; }
        int defenders = IsOwned ? garrison.Count + (present.TryGetValue(ownerTeam, out var d) ? d : 0) : 0;

        if (teamsHere == 1 && soleTeam != ownerTeam && defenders == 0)
        {
            // an attacker (or the first team) is capturing
            if (IsOwned) { LoseOwnership(); }
            if (capturingTeam != soleTeam) { capturingTeam = soleTeam; progress = 0f; }
            float speed = 1f + extraUnitSpeedup * (soleCount - 1);
            progress = Mathf.Min(1f, progress + Time.deltaTime * speed / captureTime);
            if (progress >= 1f) TakeOwnership(soleTeam);
        }
        else if (!IsOwned && teamsHere == 0 && progress > 0f)
        {
            progress = Mathf.Max(0f, progress - Time.deltaTime / captureTime);   // abandoned capture drains
            if (progress <= 0f) capturingTeam = -1;
        }

        if (IsOwned)
        {
            if (garrison.Count > 0) holdTimeLeft = holdDuration;   // garrison keeps it held
            else holdTimeLeft -= Time.deltaTime;
            if (holdTimeLeft <= 0f)
            {
                progress = Mathf.Max(0f, progress - Time.deltaTime / decayTime);
                if (progress <= 0f) LoseOwnership();
            }
        }

        if (progressRing) progressRing.localScale = Vector3.one * Mathf.Max(0.01f, progress);
        if (emblem && IsOwned) emblem.Rotate(Vector3.up, 45f * Time.deltaTime, Space.World);
        UpdateGlow();
    }

    void Scan()
    {
        present.Clear();
        int n = Physics.OverlapSphereNonAlloc(transform.position, captureRadius, hits, ~0, QueryTriggerInteraction.Collide);
        var counted = new HashSet<TeamMember>();
        for (int i = 0; i < n; i++)
        {
            var tm = hits[i].GetComponentInParent<TeamMember>();
            if (!tm || !tm.isActiveAndEnabled || !counted.Add(tm)) continue;
            var death = tm.GetComponent<UnitDeath>();
            if (death && death.IsDead) continue;
            present[tm.team] = (present.TryGetValue(tm.team, out var c) ? c : 0) + 1;
        }
    }

    void TakeOwnership(int team)
    {
        ownerTeam = team; capturingTeam = team; progress = 1f; holdTimeLeft = holdDuration;
        ApplyState();
        OnOwnerChanged?.Invoke(team);
    }

    void LoseOwnership()
    {
        EjectAll();
        ownerTeam = -1; capturingTeam = -1; progress = 0f; holdTimeLeft = 0f;
        ApplyState();
        OnOwnerChanged?.Invoke(-1);
    }

    void ApplyState()
    {
        if (ruined) ruined.SetActive(!IsOwned);
        if (restored) restored.SetActive(IsOwned);
    }

    void UpdateGlow()
    {
        Color baseCol = capturingTeam >= 0 ? Color.Lerp(Color.white, TeamMember.ColorOf(capturingTeam), progress) : Color.white;
        float pulse = IsOwned ? 1f : 0.6f + 0.4f * Mathf.Sin(Time.time * 4f);
        foreach (var (r, slot) in glow)
        {
            if (!r) continue;
            r.GetPropertyBlock(block, slot);
            block.SetColor(EmissionId, baseCol * 2.1f * pulse);
            block.SetColor(ColorId, baseCol);
            r.SetPropertyBlock(block, slot);
        }
    }

    // ---------------- garrison ----------------
    public bool CanGarrison(TeamMember unit) => unit && IsOwned && unit.team == ownerTeam && garrison.Count < maxGarrison && !garrison.Contains(unit);

    public bool TryGarrison(TeamMember unit)
    {
        if (!CanGarrison(unit)) return false;
        garrison.Add(unit);
        var slot = slots.Count > 0 ? slots[(garrison.Count - 1) % slots.Count] : transform;
        unit.transform.position = slot.position;
        unit.gameObject.SetActive(false);   // hidden and untargetable while inside
        OnGarrisonChanged?.Invoke(garrison.Count);
        return true;
    }

    public void Eject(TeamMember unit)
    {
        if (!garrison.Remove(unit) || !unit) return;
        Vector3 p = exitPoint ? exitPoint.position : transform.position + transform.forward * 1.6f;
        p += new Vector3(Random.Range(-0.6f, 0.6f), 0f, Random.Range(0f, 0.5f));
        unit.transform.position = p;
        unit.gameObject.SetActive(true);
        OnGarrisonChanged?.Invoke(garrison.Count);
    }

    public void EjectAll()
    {
        for (int i = garrison.Count - 1; i >= 0; i--) Eject(garrison[i]);
    }

    public IReadOnlyList<TeamMember> Garrison => garrison;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, captureRadius);
    }

    void OnGUI()
    {
        var cam = Camera.main;
        if (!cam) return;
        Vector3 sp = cam.WorldToScreenPoint(transform.position + Vector3.up * 2.6f * transform.lossyScale.y);
        if (sp.z < 0) return;
        // Only label it while it's being captured or the mouse is near it (keeps the screen tidy).
        bool capturing = progress > 0f && progress < 1f;
        Vector2 mouse = UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector2.one * -9999f;
        if (!capturing && ((Vector2)sp - mouse).sqrMagnitude > 150f * 150f) return;
        string who = IsOwned ? (ownerTeam == 0 ? "YOURS" : $"TEAM {ownerTeam + 1}") : "NEUTRAL";
        string line = IsOwned
            ? $"{who}  ·  hold {Mathf.CeilToInt(holdTimeLeft)}s  ·  garrison {garrison.Count}/{maxGarrison}"
            : (progress > 0 ? $"CAPTURING {Mathf.RoundToInt(progress * 100)}%" : "DERELICT OUTPOST");
        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        style.normal.textColor = IsOwned ? TeamMember.ColorOf(ownerTeam) : Color.white;
        GUI.Label(new Rect(sp.x - 160, Screen.height - sp.y - 12, 320, 24), line, style);
    }
}
