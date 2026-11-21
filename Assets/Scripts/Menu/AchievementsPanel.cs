using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AchievementsPanel : MonoBehaviour
{
    public const int MaxPinned = 4;
    static readonly Color Cyan = new(0.3f, 1f, 1f), Dim = new(0.55f, 0.6f, 0.75f), BarBack = new(0.12f, 0.12f, 0.2f, 1f), CardColor = new(1f, 1f, 1f, 0.035f);

    PlayerProfile profile;
    bool own;
    System.Action changed;
    readonly List<(Achievements.Def def, Button pin, Text pinText)> pins = new();
    Text pinnedText;

    public static void Open(Transform parent, PlayerProfile p, bool own, string title, System.Action changed = null)
    {
        var shade = new GameObject("Achievements", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        shade.SetParent(parent, false);
        SettingsPanel.Stretch(shade);
        shade.GetComponent<Image>().color = new Color(0f, 0.01f, 0.04f, 0.97f);
        HoloSkin.Layer(shade, HoloSkin.LayerPopup + 7);
        var panel = shade.gameObject.AddComponent<AchievementsPanel>();
        panel.profile = p ?? new PlayerProfile();
        panel.own = own;
        panel.changed = changed;
        panel.Build(shade, title);
    }

    void Build(RectTransform shade, string title)
    {
        var win = Box(shade, Vector2.zero, new Vector2(1500f, 860f), new Color(0.02f, 0.03f, 0.08f, 1f));
        HoloSkin.Modal(win);
        Box(win, Vector2.zero, new Vector2(1484f, 844f), new Color(0.01f, 0.015f, 0.04f, 1f)).SetSiblingIndex(1);
        Label(win, title, new Vector2(0f, 385f), 34, TextAnchor.MiddleCenter, 1200f, Cyan, true);
        int points = Achievements.Points(profile), max = Achievements.All.Length * 4;
        Label(win, $"{points} / {max} TIERS UNLOCKED", new Vector2(0f, 348f), 17, TextAnchor.MiddleCenter, 800f, Dim);
        pinnedText = Label(win, "", new Vector2(560f, 385f), 16, TextAnchor.MiddleCenter, 300f, Dim);

        const int cols = 4;
        Vector2 card = new(350f, 210f);
        for (int i = 0; i < Achievements.All.Length; i++)
        {
            var d = Achievements.All[i];
            int t = Achievements.Tier(d, profile);
            var pos = new Vector2((i % cols - (cols - 1) / 2f) * (card.x + 16f), 220f - i / cols * (card.y + 16f));
            var c = Box(win, pos, card, CardColor);
            Badge.Make(c, new Vector2(-108f, 16f), 84f).Show(d, t);
            Color tc = Achievements.TierColors[t];
            Label(c, d.name, new Vector2(60f, 70f), 20, TextAnchor.MiddleCenter, 210f, t > 0 ? Color.white : Dim, true);
            Label(c, Achievements.TierNames[t], new Vector2(60f, 44f), 15, TextAnchor.MiddleCenter, 210f, tc, true);
            string goal = t >= d.tiers.Length ? "All tiers complete!" : "Next: " + d.Goal(t);
            var g = Label(c, goal, new Vector2(60f, 8f), 14, TextAnchor.MiddleCenter, 210f, Color.white);
            g.rectTransform.sizeDelta = new Vector2(210f, 40f);
            g.horizontalOverflow = HorizontalWrapMode.Wrap;
            float prog = Achievements.Progress(d, profile, out string text);
            var bar = Box(c, new Vector2(60f, -30f), new Vector2(200f, 10f), BarBack);
            var fill = Box(bar, Vector2.zero, Vector2.zero, t >= 4 ? Cyan : Achievements.TierColors[Mathf.Min(t + 1, 4)]);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(prog, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            Label(c, text, new Vector2(60f, -50f), 13, TextAnchor.MiddleCenter, 210f, Dim);
            var tiersLine = new System.Text.StringBuilder();
            for (int k = 0; k < d.tiers.Length; k++)
                tiersLine.Append($"<color=#{ColorUtility.ToHtmlStringRGB(k < t ? Achievements.TierColors[k + 1] : new Color(0.35f, 0.38f, 0.45f))}>{(d.lowerIsBetter ? $"{d.tiers[k] / 60}m" : Achievements.Short(d.tiers[k]))}</color>   ");
            Label(c, tiersLine.ToString().TrimEnd(), new Vector2(-105f, -78f), 12, TextAnchor.MiddleCenter, 160f, Color.white);
            if (own)
            {
                var pin = SettingsPanel.Button(c, "PIN", new Vector2(60f, -82f), new Vector2(150f, 34f));
                HoloSkin.Button(pin, HoloUI.ButtonStyle.Small);
                pin.interactable = t > 0;
                var def = d;
                pin.onClick.AddListener(() => TogglePin(def));
                pins.Add((d, pin, pin.GetComponentInChildren<Text>()));
            }
        }
        if (!own) pinnedText.text = "";
        Refresh();
        SettingsPanel.Button(win, "CLOSE", new Vector2(0f, -392f), new Vector2(260f, 50f)).onClick.AddListener(() => Destroy(gameObject));
        if (own) Label(win, $"Pin up to {MaxPinned} badges to show them on your profile.", new Vector2(-460f, -392f), 15, TextAnchor.MiddleCenter, 460f, Dim);
    }

    void TogglePin(Achievements.Def d)
    {
        var list = new List<string>(profile.badges ?? new string[0]);
        if (list.Remove(d.id)) { }
        else if (list.Count < MaxPinned) list.Add(d.id);
        else { pinnedText.text = $"<color=#FF6070>Max {MaxPinned} pinned. Unpin one first.</color>"; return; }
        ProfileService.SetBadges(list.ToArray());
        profile = ProfileService.Current;
        Refresh();
        changed?.Invoke();
    }

    void Refresh()
    {
        if (!own) return;
        var pinned = profile.badges ?? new string[0];
        foreach (var (def, pin, text) in pins)
        {
            bool on = System.Array.IndexOf(pinned, def.id) >= 0;
            text.text = on ? "PINNED" : Achievements.Tier(def, profile) > 0 ? "PIN" : "LOCKED";
            HoloSkin.SetOn(pin, on);
        }
        pinnedText.text = $"PINNED  {pinned.Length} / {MaxPinned}";
    }

    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) Destroy(gameObject);
    }

    static RectTransform Box(Transform parent, Vector2 pos, Vector2 size, Color c)
    {
        var r = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        var img = r.GetComponent<Image>();
        img.color = c;
        img.raycastTarget = c.a > 0.5f;
        return r;
    }

    static Text Label(Transform parent, string text, Vector2 pos, int size, TextAnchor align, float width, Color c, bool bold = false)
    {
        var t = SettingsPanel.Label(parent, text, pos, size, align, width);
        t.font = bold ? HoloSkin.BoldFont : HoloSkin.Font;
        t.color = c;
        t.supportRichText = true;
        return t;
    }
}
