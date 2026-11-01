using UnityEngine;
using UnityEngine.UI;

// Neon credits counter (replaces ResourceBank's built-in OnGUI label). Flashes "+N" on income.
public class CreditsDisplay : MonoBehaviour
{
    [SerializeField] Text label;
    [SerializeField] Text gain; // "+200", fades out

    float gainTime = -10f;
    ResourceBank bank;

    void Start()
    {
        bank = ResourceBank.GetOrCreate();
        bank.OnCreditsChanged += Changed;
        label.text = $"CREDITS  {bank.Credits:N0}";
        gain.text = "";
    }

    void OnDestroy()
    {
        if (bank != null) bank.OnCreditsChanged -= Changed;
    }

    void Changed(int total, int delta)
    {
        label.text = $"CREDITS  {total:N0}";
        if (delta > 0) { gain.text = $"+{delta:N0}"; gainTime = Time.time; }
    }

    void Update()
    {
        float age = Time.time - gainTime;
        var c = gain.color; c.a = Mathf.Clamp01(1f - age / 1.5f); gain.color = c;
    }
}
