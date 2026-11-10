using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Main menu > Skirmish: up to 3 AI opponents (off / difficulty, enemy or ally) and the starting credits,
// then START loads the map with Skirmish.Active. Built in code with the Settings screen's widgets.
public class SkirmishPanel : MonoBehaviour
{
    // Per AI slot: 0 = off, 1-3 = enemy Easy/Normal/Hard, 4-6 = ally Easy/Normal/Hard.
    static readonly int[] choice = { 2, 0, 0 };
    static readonly int[] creditOptions = { 3000, 5000, 10000, 20000 };
    static int creditIndex = 1;

    Text status;

    public static void Open(Component anyUI)
    {
        var canvas = anyUI.GetComponentInParent<Canvas>().rootCanvas;
        var root = new GameObject("Skirmish", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        root.SetParent(canvas.transform, false);
        SettingsPanel.Stretch(root);
        root.GetComponent<Image>().color = SettingsPanel.Back;
        root.gameObject.AddComponent<SkirmishPanel>().Build(root);
    }

    static string Describe(int c) => c == 0 ? "OFF" : $"{(Skirmish.Difficulty)((c - 1) % 3)}  {(c >= 4 ? "ALLY" : "ENEMY")}".ToUpper();

    void Build(RectTransform root)
    {
        var box = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        box.SetParent(root, false);
        box.sizeDelta = new Vector2(640f, 560f);
        box.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.07f, 1f);

        SettingsPanel.Label(box, "SKIRMISH", new Vector2(0f, 225f), 34, TextAnchor.MiddleCenter, 560f);
        SettingsPanel.Label(box, "You vs. AI opponents. Destroy every enemy building to win.", new Vector2(0f, 180f), 17, TextAnchor.MiddleCenter, 580f);
        float y = 110f;
        for (int i = 0; i < 3; i++)
        {
            int slot = i;
            SettingsPanel.Cycle(box, $"AI player {i + 1}", ref y, () => Describe(choice[slot]), () => choice[slot] = (choice[slot] + 1) % 7);
        }
        SettingsPanel.Cycle(box, "Starting credits", ref y, () => $"${creditOptions[creditIndex]:N0}", () => creditIndex = (creditIndex + 1) % creditOptions.Length);

        status = SettingsPanel.Label(box, "", new Vector2(0f, -150f), 18, TextAnchor.MiddleCenter, 580f);
        status.color = new Color(1f, 0.4f, 0.4f);
        SettingsPanel.Button(box, "START", new Vector2(-120f, -220f), new Vector2(220f, 60f)).onClick.AddListener(StartMatch);
        SettingsPanel.Button(box, "BACK", new Vector2(120f, -220f), new Vector2(220f, 60f)).onClick.AddListener(() => Destroy(gameObject));
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Destroy(gameObject);
    }

    void StartMatch()
    {
        Skirmish.Opponents.Clear();
        for (int i = 0; i < 3; i++)
            if (choice[i] > 0)
                Skirmish.Opponents.Add(new Skirmish.Opponent { team = i + 1, difficulty = (Skirmish.Difficulty)((choice[i] - 1) % 3), ally = choice[i] >= 4 });
        if (!Skirmish.Opponents.Exists(o => !o.ally)) { status.text = "Add at least one enemy AI."; return; }
        Skirmish.StartCredits = creditOptions[creditIndex];
        Skirmish.Active = true;
        UnityEngine.SceneManagement.SceneManager.LoadScene(1); // the RTS map
    }
}
