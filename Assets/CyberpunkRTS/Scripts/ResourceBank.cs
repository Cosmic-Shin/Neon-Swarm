using UnityEngine;

/// <summary>Player credits. Harvesters add to it when they unload at a refinery. Shows a simple on-screen counter.</summary>
public class ResourceBank : MonoBehaviour
{
    public static ResourceBank Instance { get; private set; }
    public int startingCredits = 1000;
    public bool showOnScreen = true;
    public int Credits { get; private set; }
    [Tooltip("Raised with the change (+income / -spent) every time credits change.")]
    public IntEventChannel creditsChanged;

    float flashTime = -10f; int lastDelta;
    GUIStyle big, small;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(this); return; }
        Instance = this;
        Credits = startingCredits;
    }

    public static ResourceBank GetOrCreate()
    {
        if (Instance) return Instance;
        return new GameObject("ResourceBank").AddComponent<ResourceBank>();
    }

    public void Add(int amount)
    {
        if (amount == 0) return;
        Credits += amount; lastDelta = amount; flashTime = Time.time;
        if (creditsChanged) creditsChanged.Raise(amount);
    }

    public bool Spend(int amount)
    {
        if (Credits < amount) return false;
        Credits -= amount;
        if (creditsChanged) creditsChanged.Raise(-amount);
        return true;
    }

    void OnGUI()
    {
        if (!showOnScreen) return;
        if (big == null)
        {
            big = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
            big.normal.textColor = new Color(0.55f, 1f, 0.15f);
            small = new GUIStyle(big) { fontSize = 18 };
        }
        GUI.Label(new Rect(Screen.width - 340, 8, 320, 32), $"CREDITS  {Credits:N0}", big);
        float age = Time.time - flashTime;
        if (age < 1.5f && lastDelta > 0)
        {
            small.normal.textColor = new Color(1f, 0.9f, 0.2f, 1f - age / 1.5f);
            GUI.Label(new Rect(Screen.width - 340, 40 - age * 14f, 320, 26), $"+{lastDelta:N0}", small);
        }
    }
}
