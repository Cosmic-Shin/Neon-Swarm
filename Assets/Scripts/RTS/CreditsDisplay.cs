using UnityEngine;
using UnityEngine.UI;

public class CreditsDisplay : MonoBehaviour
{
    [SerializeField] Text label;
    [SerializeField] Text gain;
    [SerializeField] IntEventChannel creditsChanged;

    static readonly Color Earn = new(0.4f, 1f, 0.45f), Spend = new(1f, 0.35f, 0.35f);
    const float FlashTime = 0.5f, PopTime = 0.25f;

    float gainTime = -10f, flashTime = -10f, shown;
    Color baseColor, flash;

    void OnEnable() { creditsChanged.Raised += Changed; instance = this; }
    void OnDisable() => creditsChanged.Raised -= Changed;

    static CreditsDisplay instance;
    float denyTime = -10f;

    public static void Deny()
    {
        if (instance == null || !instance.isActiveAndEnabled) return;
        Sfx.Play(Snd.Bad, 0.5f);
        instance.denyTime = instance.flashTime = Time.unscaledTime;
        instance.flash = Spend;
    }

    void Start()
    {
        baseColor = label.color;
        shown = ResourceBank.GetOrCreate().Credits;
        label.text = $"CREDITS  {shown:N0}";
        gain.text = "";
    }

    void Changed(int delta)
    {
        flash = delta > 0 ? Earn : Spend;
        flashTime = Time.unscaledTime;
        if (delta > 0) { gain.text = $"+{delta:N0}"; gainTime = Time.unscaledTime; }
    }

    void Update()
    {
        float target = ResourceBank.Instance != null ? ResourceBank.Instance.Credits : shown;
        if (shown != target)
        {
            float step = Mathf.Max(Mathf.Abs(target - shown) * 8f, 400f) * Time.unscaledDeltaTime;
            shown = Mathf.MoveTowards(shown, target, step);
            label.text = $"CREDITS  {Mathf.RoundToInt(shown):N0}";
        }

        float f = Time.unscaledTime - flashTime;
        label.color = Color.Lerp(flash, baseColor, Mathf.Clamp01(f / FlashTime));
        bool pop = flash == Earn && f < PopTime;
        float d = Time.unscaledTime - denyTime;
        float throb = d < 0.5f ? 0.15f * Mathf.Abs(Mathf.Sin(d * Mathf.PI * 4f)) * (1f - d / 0.5f) : 0f;
        label.rectTransform.localScale = Vector3.one * ((pop ? 1f + 0.12f * (1f - f / PopTime) : 1f) + throb);

        float age = Time.unscaledTime - gainTime;
        var c = gain.color; c.a = Mathf.Clamp01(1f - age / 1.5f); gain.color = c;
    }
}
