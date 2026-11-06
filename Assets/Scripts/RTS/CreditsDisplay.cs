using UnityEngine;
using UnityEngine.UI;

// Neon credits counter (replaces ResourceBank's built-in OnGUI label). Flashes "+N" on income.
public class CreditsDisplay : MonoBehaviour
{
    [SerializeField] Text label;
    [SerializeField] Text gain; // "+200", fades out
    [SerializeField] IntEventChannel creditsChanged; // same asset as ResourceBank's

    float gainTime = -10f;

    void OnEnable() => creditsChanged.Raised += Changed;
    void OnDisable() => creditsChanged.Raised -= Changed;

    void Start()
    {
        label.text = $"CREDITS  {ResourceBank.GetOrCreate().Credits:N0}";
        gain.text = "";
    }

    void Changed(int delta)
    {
        label.text = $"CREDITS  {ResourceBank.Instance.Credits:N0}";
        if (delta > 0) { gain.text = $"+{delta:N0}"; gainTime = Time.time; }
    }

    void Update()
    {
        float age = Time.time - gainTime;
        var c = gain.color; c.a = Mathf.Clamp01(1f - age / 1.5f); gain.color = c;
    }
}
