using UnityEngine;

public class CorpSite : MonoBehaviour
{
    public enum Kind { CapturePoint, RepairBay, DataVault, Outpost }

    public Kind kind;
    public float sight = 70f;
    public int income = 30;
    public float interval = 5f;
    public float repairRadius = 12f;
    public float repairPerSecond = 15f;

    public static readonly Color Neutral = new(0.55f, 0.57f, 0.62f);

    OutpostCapture capture;
    Transform repairPoint;
    int shownOwner = -99;
    float nextPay, nextSpark;

    public int Owner => capture != null ? capture.ownerTeam : -1;
    public float Sight => kind == Kind.Outpost && Owner >= 0 ? sight : 0f;

    void Awake()
    {
        capture = GetComponent<OutpostCapture>();
        foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == "NRB_RepairPoint") repairPoint = t;
        if (!TryGetComponent(out life)) life = gameObject.AddComponent<BuildingLife>();
    }

    BuildingLife life;

    void Captured(int owner)
    {
        if (!Juice.Seen(transform.position)) return;
        // Outpost.CaptureFX already does the rings, burst and sky beam
        Juice.Flash(transform.position + Vector3.up * 6f, owner < 0 ? Neutral : Net.ColorOf(owner), 4f, 30f, 0.5f);
        life.Kick(owner < 0 ? -0.8f : 1f);
    }

    void Update()
    {
        int owner = Owner;
        if (owner != shownOwner)
        {
            if (shownOwner != -99) Captured(owner);
            shownOwner = owner;
            TeamLook.ApplyColor(gameObject, owner < 0 ? Neutral : Net.ColorOf(owner));
        }
        if (owner < 0) return;
        if (kind == Kind.DataVault && Time.time >= nextPay)
        {
            nextPay = Time.time + interval;
            if (!Net.Online || owner == 0)
            {
                TeamEconomy.Earn(owner, income);
                if (owner == 0 && Juice.Seen(transform.position)) Juice.Burst(transform.position + Vector3.up * 6f, Juice.Heal, 8, 4f, 0.12f, 0.5f, 1f);
            }
        }
        if (kind == Kind.RepairBay) Repair(owner);
    }

    void Repair(int owner)
    {
        Vector3 at = repairPoint != null ? repairPoint.position : transform.position;
        float r2 = repairRadius * repairRadius;
        bool spark = Time.time >= nextSpark;
        if (spark) nextSpark = Time.time + 0.4f;
        foreach (var h in Health.All)
        {
            if (h.IsDead || !h.IsDamaged || !Net.Allied(h.team, owner) || !h.TryGetComponent(out SelectableUnit u) || u.Data == null || u.Data.role != UnitData.Role.Heavy) continue;
            if (h.TryGetComponent(out NetEntity n) && n.IsRemote) continue;
            if ((h.transform.position - at).sqrMagnitude > r2) continue;
            h.Heal(repairPerSecond * Time.deltaTime);
            if (spark && Juice.Seen(h.transform.position)) Juice.Burst(h.transform.position + Vector3.up, Juice.Heal, 3, 2f, 0.12f, 0.4f, 1f);
        }
    }
}
