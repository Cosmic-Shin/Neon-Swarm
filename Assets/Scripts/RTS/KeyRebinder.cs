using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class KeyRebinder : MonoBehaviour
{
    public static bool Open { get; private set; }

    readonly List<(Act act, Text key)> rows = new();
    Act? waiting;
    Text note;
    bool skipFrame;

    public static void Show(RectTransform root)
    {
        var cover = new GameObject("Controls", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        cover.SetParent(root, false);
        SettingsPanel.Stretch(cover);
        cover.GetComponent<Image>().color = new Color(0f, 0f, 0.01f, 0.97f);
        cover.gameObject.AddComponent<KeyRebinder>().Build(cover);
    }

    void Build(RectTransform cover)
    {
        Open = true;
        var box = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        box.SetParent(cover, false);
        box.sizeDelta = new Vector2(640f, 1000f);
        HoloSkin.Modal(box);
        HoloSkin.Title(SettingsPanel.Label(box, "CONTROLS", new Vector2(0f, 455f), 34, TextAnchor.MiddleCenter, 560f), 540f);

        float y = 385f;
        foreach (var (act, name, _) in Controls.All)
        {
            SettingsPanel.Label(box, name, new Vector2(-110f, y), 21, TextAnchor.MiddleLeft, 300f);
            var b = SettingsPanel.Button(box, "", new Vector2(160f, y), new Vector2(180f, 40f));
            HoloSkin.Button(b, HoloUI.ButtonStyle.Small);
            var a = act;
            b.onClick.AddListener(() => { waiting = a; skipFrame = true; note.text = $"Press a key for <b>{name.ToUpper()}</b>   (Esc cancels)"; Refresh(); });
            rows.Add((act, b.GetComponentInChildren<Text>()));
            y -= 46f;
        }
        note = SettingsPanel.Label(box, "", new Vector2(0f, -385f), 18, TextAnchor.MiddleCenter, 580f);
        note.supportRichText = true;
        note.rectTransform.sizeDelta = new Vector2(580f, 50f);
        SettingsPanel.Button(box, "RESET", new Vector2(-150f, -450f), new Vector2(240f, 56f)).onClick.AddListener(() =>
        {
            Controls.ResetAll();
            waiting = null;
            note.text = "Keys reset to defaults.";
            Refresh();
        });
        SettingsPanel.Button(box, "CLOSE", new Vector2(150f, -450f), new Vector2(240f, 56f)).onClick.AddListener(Close);
        note.text = "Click a key to change it. Camera keys only scroll while nothing is selected.";
        Refresh();
    }

    void Refresh()
    {
        foreach (var (act, key) in rows) key.text = waiting == act ? "..." : Controls.Label(act);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (waiting == null)
        {
            if (kb.escapeKey.wasPressedThisFrame) Close();
            return;
        }
        if (skipFrame) { skipFrame = false; return; }
        if (kb.escapeKey.wasPressedThisFrame) { waiting = null; note.text = "Cancelled."; Refresh(); return; }
        foreach (var k in kb.allKeys)
        {
            if (k == null || !k.wasPressedThisFrame) continue;
            if (Controls.Reserved(k.keyCode)) { note.text = $"{Controls.Label(k.keyCode)} is reserved (groups, chat, modifiers)."; return; }
            var act = waiting.Value;
            string clash = Controls.Conflict(act, k.keyCode);
            Controls.Set(act, k.keyCode);
            note.text = clash != null ? $"Set to {Controls.Label(k.keyCode)}. Swapped with <b>{clash}</b>." : $"Set to {Controls.Label(k.keyCode)}.";
            waiting = null;
            Refresh();
            Sfx.Play(Snd.Click, 0.6f);
            return;
        }
    }

    void Close() => Destroy(gameObject);

    void OnDestroy() => Open = false;
}
