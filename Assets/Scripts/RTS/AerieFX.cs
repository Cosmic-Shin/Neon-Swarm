using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class AerieFX : MonoBehaviour
{
    const float DoorAngle = 105f, DoorTime = 0.7f, LiftTime = 0.8f, LiftTravel = 1.6f;

    class Door { public Transform pivot; public Quaternion closed; public Vector3 axis; public float sign; }
    readonly List<Door> doors = new();
    readonly List<(Transform t, Vector3 home, Vector3 outward)> clamps = new();
    readonly List<Transform> clampTips = new();
    Transform lift, launchPoint, beacon, dock;
    Vector3 liftDown;
    float hatchUntil, hatchK, liftK, clampK, clampOpenUntil, beaconBoost;
    Color color;

    public Transform TitanDock => dock != null ? dock : dock = Find("Aerie_TitanDock");
    public bool HasMast => TitanDock != null && dock.gameObject.activeInHierarchy;

    Transform Find(string n)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
        return null;
    }

    void Start()
    {
        foreach (var side in new[] { "L", "R" })
        {
            Transform piv = Find($"Aerie_Door{side}_Pivot"), hinge = Find($"Aerie_Door{side}_HingeEnd"), mid = Find($"Aerie_Door{side}_Center");
            if (piv == null || hinge == null || mid == null) continue;
            Vector3 axis = (hinge.position - piv.position).normalized, c = mid.position - piv.position;
            float sign = (Quaternion.AngleAxis(90f, axis) * c).y >= (Quaternion.AngleAxis(-90f, axis) * c).y ? 1f : -1f;
            doors.Add(new Door { pivot = piv, closed = piv.localRotation, axis = piv.parent.InverseTransformDirection(axis), sign = sign });
        }
        lift = Find("Aerie_Lift");
        launchPoint = Find("Aerie_LaunchPoint");
        beacon = Find("Aerie_BeaconSpin");
        if (lift != null) liftDown = lift.localPosition;
        if (TitanDock != null)
            foreach (Transform t in dock.parent)
                if (t.name.StartsWith("AerMk3_Clamp"))
                {
                    Vector3 out_ = t.position - dock.position; out_.y = 0f;
                    clamps.Add((t, t.localPosition, t.parent.InverseTransformDirection(out_.normalized)));
                    if (t.name.StartsWith("AerMk3_ClampTip")) clampTips.Add(t);
                }
        color = ProductionFX.HoloColor(gameObject);
    }

    public void OpenHatch(float seconds) => hatchUntil = Mathf.Max(hatchUntil, Time.time + seconds);

    void Update()
    {
        float dt = Time.deltaTime;
        bool open = Time.time < hatchUntil;
        hatchK = Mathf.MoveTowards(hatchK, open ? 1f : 0f, dt / DoorTime);
        liftK = Mathf.MoveTowards(liftK, open && hatchK >= 1f ? 1f : 0f, dt / LiftTime);
        if (liftK > 0f && hatchK < 1f) hatchK = 1f;
        float d = Mathf.SmoothStep(0f, 1f, hatchK);
        foreach (var door in doors) door.pivot.localRotation = Quaternion.AngleAxis(DoorAngle * d * door.sign, door.axis) * door.closed;
        if (lift != null) lift.position = lift.parent.TransformPoint(liftDown) + Vector3.up * LiftTravel * Mathf.SmoothStep(0f, 1f, liftK);

        clampK = Mathf.MoveTowards(clampK, Time.time < clampOpenUntil ? 1f : 0f, dt / 0.6f);
        float ck = Mathf.SmoothStep(0f, 1f, clampK) * 2.2f;
        foreach (var (t, home, outward) in clamps) t.localPosition = home + outward * ck / Mathf.Max(0.01f, t.parent.lossyScale.x);

        beaconBoost = Mathf.MoveTowards(beaconBoost, open ? 1f : 0f, dt);
        if (beacon != null) beacon.Rotate(Vector3.up, (90f + 540f * beaconBoost) * dt, Space.World);
    }

    public float Launch(SelectableUnit unit, Vector3 exit, Vector3? rally)
    {
        if (unit.Data.role == UnitData.Role.Elite && HasMast) { StartCoroutine(Titan(unit, exit, rally)); return 0f; }
        float total = DoorTime + LiftTime + 1.8f;
        OpenHatch(total);
        StartCoroutine(Hatch(unit, exit, rally));
        return total;
    }

    IEnumerator Hatch(SelectableUnit unit, Vector3 exit, Vector3? rally)
    {
        var agent = unit.GetComponent<NavMeshAgent>();
        agent.enabled = false;
        Transform u = unit.transform;
        float hover = Flyer.HeightOf(unit);
        Vector3 Pad() => (launchPoint != null ? launchPoint.position : transform.position) - Vector3.up * hover;
        u.position = Pad();

        for (float t = 0f; t < DoorTime + LiftTime && unit != null; t += Time.deltaTime) { u.position = Pad(); yield return null; }
        if (unit == null) yield break;

        Vector3 start = u.position, top = new(start.x, 0f, start.z);
        Juice.Flash(start + Vector3.up * hover, color, 6f, 12f, 0.4f);
        const float climb = 1.1f;
        for (float t = 0f; t < climb && unit != null; t += Time.deltaTime)
        {
            float k = t / climb;
            u.position = Vector3.Lerp(start, top, 1f - (1f - k) * (1f - k));
            if (Random.value < 0.6f) Juice.Burst(u.position + Vector3.up * (hover - 0.5f), color, 2, 3f, 0.12f, 0.4f, -1f);
            yield return null;
        }
        if (unit == null) yield break;
        Juice.Ring(new Vector3(top.x, 2.6f, top.z), color, 1f, 6f, 0.5f, 0.2f);
        Vector3 flyTo = exit + (exit - transform.position).normalized * Flyer.SizeOf(unit);
        unit.WalkOut(top, flyTo, Mathf.Max(6f, agent.speed * 0.7f), null, rally);
    }

    IEnumerator Titan(SelectableUnit unit, Vector3 exit, Vector3? rally)
    {
        var agent = unit.GetComponent<NavMeshAgent>();
        agent.enabled = false;
        Transform u = unit.transform;
        float hover = Flyer.HeightOf(unit), size = Flyer.SizeOf(unit);
        u.SetPositionAndRotation(dock.position - Vector3.up * hover, transform.rotation);
        Vector3 core = dock.position;

        Juice.Flash(core, Color.white, 14f, size * 4f, 0.6f);
        Juice.Ring(core, color, size * 0.5f, size * 2.5f, 0.7f, 0.5f);
        Juice.Ring(new Vector3(core.x, 0.3f, core.z), color, 1f, size * 2f, 1f, 0.4f);
        Juice.Burst(core, color, 60, 14f, 0.25f, 1f, 0.2f);
        Juice.Shake(core, 0.6f);
        if (TeamEconomy.TeamOf(this) == 0) Alerts.Show($"Titan online: {unit.DisplayName}", Alerts.Good);

        var arcs = new List<LineRenderer>();
        foreach (var tip in clampTips)
        {
            var lr = new GameObject("TitanArc").AddComponent<LineRenderer>();
            lr.sharedMaterial = Resources.Load<Material>("HarvestFX");
            lr.positionCount = 8;
            lr.widthMultiplier = 0.25f;
            lr.startColor = lr.endColor = color * 3f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            arcs.Add(lr);
        }
        for (float t = 0f; t < 1.8f && unit != null; t += Time.deltaTime)
        {
            for (int i = 0; i < arcs.Count; i++)
            {
                Vector3 a = clampTips[i].position, b = core + Random.insideUnitSphere * size * 0.4f;
                for (int p = 0; p < 8; p++)
                    arcs[i].SetPosition(p, Vector3.Lerp(a, b, p / 7f) + (p is 0 or 7 ? Vector3.zero : Random.insideUnitSphere * 0.6f));
                arcs[i].enabled = Random.value > 0.15f;
            }
            if (Random.value < 0.5f) Juice.Burst(core + Random.insideUnitSphere * size * 0.5f, color, 3, 5f, 0.12f, 0.5f, 0f, true);
            yield return null;
        }
        foreach (var lr in arcs) Destroy(lr.gameObject);
        if (unit == null) yield break;

        clampOpenUntil = Time.time + 4f;
        Juice.Ring(core, Color.white, size * 0.8f, size * 3f, 0.6f, 0.6f);
        Juice.Flash(core, color, 8f, size * 3f, 0.4f);
        Juice.Shake(core, 0.4f);
        yield return new WaitForSeconds(0.5f);
        if (unit == null) yield break;

        Vector3 flyTo = exit + (exit - transform.position).normalized * size;
        unit.WalkOut(u.position, flyTo, Mathf.Max(4f, agent.speed * 0.6f), null, rally);
    }
}
