using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Esc menu: Resume, Surrender (multiplayer) and Leave. Esc only opens it when nothing else uses Esc
// (build menu, building ghost, chat box). The game keeps running underneath (no pause in a shared match).
[DefaultExecutionOrder(-200)] // before Chat and BuildingPlacer see the same Esc
public class GameMenu : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] Button resume, surrender, leave;
    [SerializeField] MatchManager match;

    public static bool IsOpen { get; private set; }

    void Awake()
    {
        IsOpen = false;
        panel.SetActive(false);
        resume.onClick.AddListener(() => Show(false));
        surrender.onClick.AddListener(() => { Show(false); match.Surrender(); });
        leave.onClick.AddListener(Leave);
        AddSettingsButton();
    }

    void Update()
    {
        var kb = Chat.Keys;
        if (kb != null && kb.f5Key.wasPressedThisFrame) QuickSave();
        if (kb != null && kb.f9Key.wasPressedThisFrame) QuickLoad();
        if (kb == null || !kb.escapeKey.wasPressedThisFrame || match.IsOver || SettingsPanel.IsOpen || SaveSlotsPanel.IsOpen || Hud.HelpOpen) return; // they close themselves on Esc
        if (!IsOpen && (BuildingPlacer.MenuOpen || BuildingPlacer.Placing)) return; // their Esc
        Show(!IsOpen);
    }

    void Show(bool open)
    {
        IsOpen = open;
        panel.SetActive(open);
        surrender.gameObject.SetActive(Net.Online);
        saveButton.gameObject.SetActive(!Net.Online);
        loadButton.gameObject.SetActive(!Net.Online);
        loadButton.interactable = SaveSystem.HasAnySave;
    }

    Button saveButton, loadButton;

    // The scene has Resume, Leave, Surrender stacked by hand: Settings, Save and Load go under Resume, the rest move down.
    void AddSettingsButton()
    {
        Vector2 at = ((RectTransform)leave.transform).anchoredPosition;
        Button Add(string label, UnityEngine.Events.UnityAction action)
        {
            var b = Instantiate(resume, resume.transform.parent);
            b.name = label;
            b.GetComponentInChildren<Text>().text = label.ToUpper();
            b.onClick = new Button.ButtonClickedEvent();
            b.onClick.AddListener(action);
            ((RectTransform)b.transform).anchoredPosition = at;
            at += new Vector2(0f, -80f);
            return b;
        }
        Add("Settings", () => SettingsPanel.Open(panel.transform));
        saveButton = Add("Save game", () => { if (!Offline()) return; SaveSlotsPanel.Open(panel.transform, saving: true, onSaved: () => Show(false)); });
        loadButton = Add("Load game", () => { if (!Offline()) return; SaveSlotsPanel.Open(panel.transform, saving: false); });
        HoloSkin.Button(loadButton, HoloUI.ButtonStyle.Gold);
        float down = at.y - ((RectTransform)leave.transform).anchoredPosition.y;
        foreach (var other in new[] { leave, surrender }) ((RectTransform)other.transform).anchoredPosition += new Vector2(0f, down);    }

    // Saving and loading are offline only: an online match lives on several PCs.
    static bool Offline()
    {
        if (Net.Online) Alerts.Show("Saving and loading are offline only", Alerts.Warn, 2f);
        return !Net.Online;
    }

    // F5: into the last slot used (the Esc menu picks a slot).
    void QuickSave()
    {
        if (!Offline() || match.IsOver) return;
        SaveSystem.Save(SaveSystem.LastSlot);
        Alerts.Show($"Quick saved to slot {SaveSystem.LastSlot}", Alerts.Good);
    }

    // F9: the newest save; restarts the map from it.
    void QuickLoad()
    {
        if (!Offline()) return;
        int slot = SaveSystem.NewestSlot;
        if (slot == 0) Alerts.Show("No saved game yet (F5 saves)", Alerts.Warn, 2f);
        else SaveSystem.LoadGame(slot);
    }

    void Leave()
    {
        Show(false);
        if (Net.Online) match.BackToMenu(); // our units leave with us (DestroyWhenStateAuthorityLeaves)
        else { Skirmish.Active = false; Skirmish.ElapsedBefore = 0f; SceneManager.LoadScene(0); }
    }
}
