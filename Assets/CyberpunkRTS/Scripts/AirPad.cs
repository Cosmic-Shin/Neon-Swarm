using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hive Aerie: builds air units and launches them. Produce(i) pays credits, waits the build time,
/// then the hatch doors swing open, the lift raises the new flyer out of the shaft, it takes off
/// to the rally point, and the hatch closes again. Production queues.
/// Upgrades (BuildingUpgrade): Mk II builds 25% faster; Mk III adds the Titan Mooring Mast, where titans
/// (Jellyfish Dreadnought, Moth Mothership, Manta Leviathan) are assembled and released instead of using the hatch.
/// </summary>
public class AirPad : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public GameObject prefab;
        public int cost = 800;
        public float buildTime = 10f;
        [Tooltip("Aerie level needed: 1 = base, 2 = Mk II, 3 = Mk III (titans).")]
        public int requiredLevel = 1;
        [Tooltip("Assembled at the Titan Mooring Mast instead of launched through the hatch.")]
        public bool titan;
        [Tooltip("0 = unlimited. Titans default to 1 alive at a time.")]
        public int maxAlive = 0;
        [System.NonSerialized] public List<GameObject> alive = new List<GameObject>();
    }
    public List<Entry> buildable = new List<Entry>();
    public float doorOpenAngle = 105f;
    public float doorTime = 0.6f;
    public float liftTime = 0.9f;
    [Tooltip("How far the lift rises, in model metres.")]
    public float liftTravel = 0.4f;
    public float titanAssemblyTime = 2.5f;
    public Vector3 rallyOffset = new Vector3(0f, 0f, 4f);

    public bool IsBusy { get; private set; }
    public float Progress { get; private set; }
    public int QueueLength => queue.Count + (IsBusy ? 1 : 0);
    public string LastMessage { get; private set; } = "";
    public int Level { get { var u = GetComponent<BuildingUpgrade>(); return u ? u.level : 1; } }
    public event System.Action<GameObject> OnUnitLaunched;

    class Door { public Transform pivot; public Quaternion closed; public Vector3 axis; public float sign = 1f; }
    readonly List<Door> doors = new List<Door>();
    readonly List<int> queue = new List<int>();
    int building = -1;
    Transform lift, launchPoint, titanDock; Vector3 liftLocalDown;

    void Awake()
    {
        var all = GetComponentsInChildren<Transform>(true);
        Transform Find(string n) { foreach (var t in all) if (t.name == n) return t; return null; }
        foreach (var side in new[] { "L", "R" })
        {
            var piv = Find($"Aerie_Door{side}_Pivot"); var he = Find($"Aerie_Door{side}_HingeEnd"); var ce = Find($"Aerie_Door{side}_Center");
            if (!piv || !he || !ce) continue;
            var d = new Door { pivot = piv, closed = piv.rotation, axis = (he.position - piv.position).normalized };
            Vector3 c = ce.position - piv.position;
            d.sign = (Quaternion.AngleAxis(90f, d.axis) * c).y >= (Quaternion.AngleAxis(-90f, d.axis) * c).y ? 1f : -1f;
            doors.Add(d);
        }
        lift = Find("Aerie_Lift"); launchPoint = Find("Aerie_LaunchPoint"); titanDock = Find("Aerie_TitanDock");
        if (lift) liftLocalDown = lift.localPosition;
        foreach (var e in buildable) if (e.titan && e.maxAlive == 0) e.maxAlive = 1;
    }

    public bool CanProduce(int index, out string why)
    {
        why = "";
        if (index < 0 || index >= buildable.Count || !buildable[index].prefab) { why = "Nothing to build"; return false; }
        var e = buildable[index];
        if (e.requiredLevel > Level) { why = e.requiredLevel >= 3 ? "Needs Aerie Mk III (Titan Mooring Mast)" : $"Needs Aerie Mk {(e.requiredLevel == 2 ? "II" : "III")}"; return false; }
        if (e.maxAlive > 0)
        {
            e.alive.RemoveAll(g => !g);
            int pending = (building == index ? 1 : 0); foreach (var q in queue) if (q == index) pending++;
            if (e.alive.Count + pending >= e.maxAlive) { why = $"Limit: {e.maxAlive} {e.prefab.name} at a time"; return false; }
        }
        var bank = ResourceBank.Instance;
        if (bank && bank.Credits < e.cost) { why = "Not enough credits"; return false; }
        return true;
    }

    public bool Produce(int index)
    {
        if (!CanProduce(index, out var why)) { LastMessage = why; return false; }
        var bank = ResourceBank.Instance;
        if (bank && !bank.Spend(buildable[index].cost)) { LastMessage = "Not enough credits"; return false; }
        queue.Add(index);
        if (!IsBusy) StartCoroutine(Process());
        return true;
    }

    IEnumerator Process()
    {
        IsBusy = true;
        while (queue.Count > 0)
        {
            building = queue[0]; queue.RemoveAt(0);
            var e = buildable[building];
            float time = e.buildTime * (Level >= 2 ? 0.75f : 1f);
            for (float t = 0f; t < time; t += Time.deltaTime) { Progress = t / time; yield return null; }
            Progress = 1f;
            if (e.titan) yield return LaunchTitan(e); else yield return Launch(e);
            building = -1;
        }
        IsBusy = false; Progress = 0f;
    }

    IEnumerator Launch(Entry e)
    {
        for (float t = 0f; t < doorTime; t += Time.deltaTime) { SetDoors(Smooth(t / doorTime)); yield return null; }
        SetDoors(1f);

        Transform lp = launchPoint ? launchPoint : transform;
        var unit = Instantiate(e.prefab, lp.position + Vector3.up * 0.15f, transform.rotation);
        e.alive.Add(unit);
        unit.transform.SetParent(lp, true);
        var fu = unit.GetComponent<FlyingUnit>();
        if (fu) fu.enabled = false;

        for (float t = 0f; t < liftTime; t += Time.deltaTime) { SetLift(Smooth(t / liftTime)); yield return null; }
        SetLift(1f);

        unit.transform.SetParent(null, true);
        Release(unit, fu);
        yield return new WaitForSeconds(0.4f);

        for (float t = 0f; t < liftTime; t += Time.deltaTime) { SetLift(1f - Smooth(t / liftTime)); yield return null; }
        SetLift(0f);
        for (float t = 0f; t < doorTime; t += Time.deltaTime) { SetDoors(1f - Smooth(t / doorTime)); yield return null; }
        SetDoors(0f);
    }

    IEnumerator LaunchTitan(Entry e)
    {
        Transform dock = titanDock ? titanDock : transform;
        var unit = Instantiate(e.prefab, dock.position, transform.rotation);
        e.alive.Add(unit);
        var fu = unit.GetComponent<FlyingUnit>();
        if (fu) fu.enabled = false;
        Vector3 s = unit.transform.localScale;
        for (float t = 0f; t < titanAssemblyTime; t += Time.deltaTime)
        {
            if (!unit) yield break;
            float k = Smooth(t / titanAssemblyTime);
            unit.transform.localScale = s * Mathf.Lerp(0.05f, 1f, k);
            unit.transform.position = dock.position;
            yield return null;
        }
        if (!unit) yield break;
        unit.transform.localScale = s;
        Release(unit, fu);
    }

    void Release(GameObject unit, FlyingUnit fu)
    {
        if (fu)
        {
            fu.enabled = true;
            fu.TakeOff();
            fu.SetDestination(transform.position + transform.rotation * rallyOffset);
        }
        OnUnitLaunched?.Invoke(unit);
    }

    static float Smooth(float k) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));

    void SetDoors(float k)
    {
        foreach (var d in doors) d.pivot.rotation = Quaternion.AngleAxis(doorOpenAngle * k * d.sign, d.axis) * d.closed;
    }

    void SetLift(float k)
    {
        if (!lift) return;
        Vector3 down = lift.parent ? lift.parent.TransformPoint(liftLocalDown) : liftLocalDown;
        lift.position = down + transform.up * liftTravel * transform.lossyScale.y * k;
    }

    void OnGUI()
    {
        var cam = Camera.main; if (!cam) return;
        Vector3 sp = cam.WorldToScreenPoint(transform.position + Vector3.up * 2.2f);
        if (sp.z < 0f) return;
        float w = 120f, x = sp.x - w / 2f, y = Screen.height - sp.y;
        if (IsBusy)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(new Rect(x - 2, y - 2, w + 4, 12), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.83f, 0f); GUI.DrawTexture(new Rect(x, y, w * Progress, 8), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (QueueLength > 1) GUI.Label(new Rect(x + w + 6, y - 6, 60, 20), $"+{QueueLength - 1}");
        }
        if (!string.IsNullOrEmpty(LastMessage)) GUI.Label(new Rect(x - 60, y + 12, 260, 22), LastMessage);
    }
}
