using System.Collections.Generic;
using UnityEngine;

/// <summary>A data-crystal ore patch. Shrinks through Full/Half/Low/Depleted stages as it's mined and slowly regrows.</summary>
public class OreNode : MonoBehaviour
{
    public static readonly List<OreNode> All = new List<OreNode>();
    /// <summary>Optional game hook: true when a spot is too dangerous to mine (enemies nearby). FindNearest skips those.</summary>
    public static System.Func<Vector3, bool> IsDangerous;

    public float maxAmount = 600f;
    public float amount = 600f;
    [Tooltip("Ore regained per second once nobody has mined it for regrowDelay seconds (0 = never regrows).")]
    public float regrowPerSecond = 3f;
    public float regrowDelay = 8f;
    [Tooltip("How close a harvester parks to mine it.")]
    public float harvestRadius = 0.85f;
    [Tooltip("Credits multiplier for ore mined here (red ore = 2.5).")]
    public float valueMultiplier = 1f;

    public int ActiveHarvesters { get; set; }
    public bool IsDepleted => amount <= 0.5f;

    GameObject full, half, low, depleted;
    float lastTaken = -999f;
    int stage = -1;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Awake()
    {
        full = Child("OreStage_Full"); half = Child("OreStage_Half"); low = Child("OreStage_Low"); depleted = Child("OreStage_Depleted");
        UpdateVisual();
    }

    GameObject Child(string n)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t.gameObject;
        return null;
    }

    /// <summary>Removes up to 'want' ore and returns how much was actually taken.</summary>
    public float Take(float want)
    {
        float got = Mathf.Min(want, amount);
        amount -= got;
        lastTaken = Time.time;
        UpdateVisual();
        if (got > 0f) TakenHook?.Invoke(this, got);
        return got;
    }

    /// <summary>Optional game hook (multiplayer): told about every Take, so the patch's owner can subtract it too.</summary>
    public static System.Action<OreNode, float> TakenHook;

    /// <summary>Multiplayer: another PC owns this patch; show its amount and don't regrow here.</summary>
    public void SetFromNet(float value)
    {
        amount = value;
        lastTaken = Time.time;
        UpdateVisual();
    }

    void Update()
    {
        if (regrowPerSecond > 0f && amount < maxAmount && Time.time - lastTaken > regrowDelay)
        {
            amount = Mathf.Min(maxAmount, amount + regrowPerSecond * Time.deltaTime);
            UpdateVisual();
        }
    }

    void UpdateVisual()
    {
        float f = amount / maxAmount;
        int st = f > 0.66f ? 3 : f > 0.33f ? 2 : f > 0.001f ? 1 : 0;
        if (st == stage) return;
        stage = st;
        if (full) full.SetActive(st == 3);
        if (half) half.SetActive(st == 2);
        if (low) low.SetActive(st == 1);
        if (depleted) depleted.SetActive(st == 0);
    }

    /// <summary>Nearest non-depleted ore, lightly preferring patches nobody else is mining.</summary>
    public static OreNode FindNearest(Vector3 from)
    {
        OreNode best = null; float bd = float.MaxValue;
        foreach (var o in All)
        {
            if (!o || o.IsDepleted) continue;
            if (IsDangerous != null && IsDangerous(o.transform.position)) continue;
            float d = (o.transform.position - from).sqrMagnitude + o.ActiveHarvesters * 16f;
            if (d < bd) { bd = d; best = o; }
        }
        return best;
    }
}
