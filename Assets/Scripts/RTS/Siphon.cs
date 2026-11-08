using UnityEngine;

// Data Siphon: a slow, steady trickle of credits. Half rate while low on power. Each payout pulses and pops "+$N".
public class Siphon : MonoBehaviour
{
    [SerializeField] int amount = 20;
    [SerializeField] float interval = 4f;

    float next;
    public float Progress01 => 1f - Mathf.Clamp01((next - Time.time) / interval); // towards the next payout (BuildingLife fills the capsules)
    public float LastPayout { get; private set; } = -99f;

    void OnEnable() => next = Time.time + interval; // starts counting once construction finishes

    void Update()
    {
        if (Time.time < next) return;
        next = Time.time + interval;
        LastPayout = Time.time;
        int earned = Structure.LowPower ? amount / 2 : amount;
        ResourceBank.GetOrCreate().Add(earned);
        MatchStats.Add(MatchStats.Kind.Earned, earned);

        // The payout: a pulse from the roof and "+$N" floating up.
        var b = ConstructionSite.RendererBounds(gameObject);
        Vector3 roof = new(b.center.x, b.max.y, b.center.z);
        Juice.Ring(roof, Juice.Gold, 0.5f, Mathf.Max(b.extents.x, b.extents.z) + 1.5f, 0.6f, 0.15f);
        Juice.Burst(roof, Juice.Gold, 14, 3f, 0.2f, 0.6f, 1.5f);
        CreditsPopup.Show(roof + Vector3.up * 1.5f, $"+${earned}", Juice.Gold);
    }
}
