using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The 5 save slots, for saving (Esc menu in a match) or loading (Esc menu, or the skirmish lobby).
// Each row: slot number, what the save is (mode, opponents), play time and date, then SAVE / LOAD and
// DELETE (click twice). Built in code with the Settings screen's widgets; Esc or BACK closes it.
public class SaveSlotsPanel : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    public static int ClosedAt = -1; // frame it closed on Esc, so the screen under it ignores that Esc

    bool saving;
    System.Action onSaved;
    readonly Text[] info = new Text[SaveSystem.Slots + 1];
    readonly Button[] act = new Button[SaveSystem.Slots + 1], del = new Button[SaveSystem.Slots + 1];
    int confirmDelete, confirmOverwrite; // slot waiting for its second click (0 = none)

    public static void Open(Component anyUI, bool saving, System.Action onSaved = null)
    {
        var canvas = anyUI.GetComponentInParent<Canvas>().rootCanvas;
        var root = new GameObject("SaveSlots", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        root.SetParent(canvas.transform, false);
        root.SetAsLastSibling();
        SettingsPanel.Stretch(root);
        root.GetComponent<Image>().color = SettingsPanel.Back;
        var panel = root.gameObject.AddComponent<SaveSlotsPanel>();
        panel.saving = saving;
        panel.onSaved = onSaved;
        panel.Build(root);
    }

    void Build(RectTransform root)
    {
        IsOpen = true;
        var box = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        box.SetParent(root, false);
        box.sizeDelta = new Vector2(980f, 660f);
        HoloSkin.Modal(box);

        HoloSkin.Title(SettingsPanel.Label(box, saving ? "SAVE GAME" : "LOAD GAME", new Vector2(0f, 280f), 34, TextAnchor.MiddleCenter, 900f), 900f);
        SettingsPanel.Label(box, saving ? "Pick a slot. F5 quick-saves to the last slot you used." : "Pick a save. F9 in a match loads the newest one.",
            new Vector2(0f, 238f), 17, TextAnchor.MiddleCenter, 900f).color = HoloUI.Muted;

        for (int s = 1; s <= SaveSystem.Slots; s++)
        {
            int slot = s;
            float y = 165f - (s - 1) * 88f;
            var row = new GameObject($"Slot{s}", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<RectTransform>();
            row.SetParent(box, false);
            row.anchoredPosition = new Vector2(0f, y);
            row.sizeDelta = new Vector2(920f, 78f);
            HoloSkin.Row(row.GetComponent<Button>()); // Panel_Inset, Btn_Hover while the mouse is over the slot
            var number = SettingsPanel.Label(row, s.ToString(), new Vector2(-418f, 0f), 34, TextAnchor.MiddleCenter, 60f);
            number.color = new Color(1f, 0.1f, 0.9f);
            info[s] = SettingsPanel.Label(row, "", new Vector2(-120f, 0f), 18, TextAnchor.MiddleLeft, 520f);
            info[s].rectTransform.sizeDelta = new Vector2(520f, 72f);
            info[s].supportRichText = true;
            act[s] = SettingsPanel.Button(row, "", new Vector2(270f, 0f), new Vector2(190f, 52f));
            if (!saving) HoloSkin.Button(act[s], HoloUI.ButtonStyle.Gold);
            act[s].onClick.AddListener(() => Act(slot));
            del[s] = SettingsPanel.Button(row, "DELETE", new Vector2(400f, 0f), new Vector2(100f, 52f));
            HoloSkin.Button(del[s], HoloUI.ButtonStyle.Danger);
            del[s].onClick.AddListener(() => Delete(slot));
            del[s].GetComponentInChildren<Text>().fontSize = 16;
        }
        SettingsPanel.Button(box, "BACK", new Vector2(0f, -280f), new Vector2(260f, 56f)).onClick.AddListener(Close);
        Refresh();
    }

    void Refresh()
    {
        for (int s = 1; s <= SaveSystem.Slots; s++)
        {
            var d = SaveSystem.Info(s);
            if (d == null)
            {
                info[s].text = SaveSystem.HasSave(s) ? "<color=#FF6060>Unreadable save</color>" : "<color=#6A7690>Empty slot</color>";
            }
            else
            {
                var when = new System.DateTime(d.savedAt, System.DateTimeKind.Utc).ToLocalTime();
                string time = $"{(int)d.elapsed / 60}:{(int)d.elapsed % 60:00}";
                string limit = d.skirmish && d.minutes > 0 ? $" / {d.minutes}:00" : "";
                string date = d.savedAt > 0 ? when.ToString("MMM d, HH:mm") : "older save"; // saves from before slots had no date
                info[s].text = $"<b>{d.summary ?? "SAVED GAME"}</b>\n<color=#8C99BF>Played {time}{limit}   ·   ${Mathf.Max(0, d.credits):N0}   ·   {date}</color>";
            }
            bool has = SaveSystem.HasSave(s);
            act[s].GetComponentInChildren<Text>().text = saving ? (confirmOverwrite == s ? "SURE?" : has ? "OVERWRITE" : "SAVE") : "LOAD";
            act[s].gameObject.SetActive(saving || d != null);
            del[s].gameObject.SetActive(has);
            del[s].GetComponentInChildren<Text>().text = confirmDelete == s ? "SURE?" : "DELETE";
        }
    }

    void Act(int slot)
    {
        if (!saving) { Close(); SaveSystem.LoadGame(slot); return; }
        if (SaveSystem.HasSave(slot) && confirmOverwrite != slot) { confirmOverwrite = slot; confirmDelete = 0; Refresh(); return; }
        SaveSystem.Save(slot);
        Alerts.Show($"Game saved to slot {slot}", Alerts.Good);
        Close();
        onSaved?.Invoke();
    }

    void Delete(int slot)
    {
        if (confirmDelete != slot) { confirmDelete = slot; confirmOverwrite = 0; Refresh(); return; }
        SaveSystem.Delete(slot);
        confirmDelete = 0;
        Refresh();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }

    void Close()
    {
        IsOpen = false;
        ClosedAt = Time.frameCount;
        Destroy(gameObject);
    }

    void OnDestroy() => IsOpen = false;
}
