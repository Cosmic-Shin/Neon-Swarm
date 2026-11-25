using UnityEngine;
using UnityEngine.UI;

public class MissionsPanel : MonoBehaviour
{
    static readonly Color Dim = new(0.6f, 0.68f, 0.8f), Green = new(0.4f, 1f, 0.6f), Locked = new(1f, 0.4f, 0.45f);

    readonly Button[] rows = new Button[Mission.All.Length];
    Text heading, tagline, briefing, map;
    Image preview;
    Button start;
    int picked;

    public static void Open(Transform parent)
    {
        var shade = new GameObject("Missions", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        shade.SetParent(parent, false);
        SettingsPanel.Stretch(shade);
        shade.GetComponent<Image>().color = new Color(0f, 0.01f, 0.04f, 0.88f);
        HoloSkin.Layer(shade, HoloSkin.LayerPopup + 3);
        shade.gameObject.AddComponent<MissionsPanel>().Build(shade);
    }

    void Build(RectTransform shade)
    {
        var win = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        win.SetParent(shade, false);
        win.sizeDelta = new Vector2(1180f, 720f);
        win.GetComponent<Image>().color = new Color(0.01f, 0.015f, 0.04f, 1f);
        HoloSkin.Modal(win);
        var inner = new GameObject("Inner", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        inner.SetParent(win, false);
        inner.sizeDelta = win.sizeDelta - new Vector2(16f, 16f);
        inner.GetComponent<Image>().color = new Color(0.01f, 0.015f, 0.04f, 1f);
        inner.GetComponent<Image>().raycastTarget = false;
        inner.SetSiblingIndex(1);
        HoloSkin.Title(Text(win, "MISSIONS", new Vector2(0f, 310f), 36, TextAnchor.MiddleCenter, 1000f, true), 980f);

        for (int i = 0; i < rows.Length; i++)
        {
            int index = i;
            rows[i] = SettingsPanel.Button(win, "", new Vector2(-370f, 222f - i * 76f), new Vector2(380f, 70f));   // 6 missions (Hive + Aegis)
            HoloSkin.Row(rows[i]);
            var label = rows[i].GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.supportRichText = true;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(22f, 4f);
            label.rectTransform.offsetMax = new Vector2(-10f, -4f);
            rows[i].onClick.AddListener(() => Pick(index));
        }

        preview = new GameObject("Preview", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        preview.transform.SetParent(win, false);
        preview.rectTransform.anchoredPosition = new Vector2(440f, 168f);
        preview.rectTransform.sizeDelta = new Vector2(190f, 190f);
        preview.preserveAspect = true;
        HoloSkin.Brackets(preview.rectTransform, HoloUI.Cyan);
        map = Text(win, "", new Vector2(440f, 58f), 16, TextAnchor.MiddleCenter, 240f, false);
        map.color = Dim;

        heading = Text(win, "", new Vector2(100f, 226f), 34, TextAnchor.MiddleLeft, 440f, true);
        heading.color = HoloUI.Cyan;
        tagline = Text(win, "", new Vector2(100f, 168f), 18, TextAnchor.UpperLeft, 440f, false);
        tagline.horizontalOverflow = HorizontalWrapMode.Wrap;
        tagline.rectTransform.sizeDelta = new Vector2(440f, 60f);
        tagline.color = Dim;
        briefing = Text(win, "", new Vector2(160f, -60f), 21, TextAnchor.UpperLeft, 560f, false);
        briefing.rectTransform.sizeDelta = new Vector2(700f, 270f);
        briefing.horizontalOverflow = HorizontalWrapMode.Wrap;
        briefing.rectTransform.anchoredPosition = new Vector2(200f, -100f);

        start = SettingsPanel.Button(win, "START MISSION", new Vector2(200f, -280f), new Vector2(300f, 56f));
        HoloSkin.Button(start, HoloUI.ButtonStyle.Gold);
        start.onClick.AddListener(() => { if (Mission.Unlocked(picked)) Mission.Begin(picked); });
        SettingsPanel.Button(win, "BACK", new Vector2(-370f, -280f), new Vector2(200f, 56f)).onClick.AddListener(() => Destroy(gameObject));

        int first = 0;
        while (first + 1 < rows.Length && Mission.Done(first) && Mission.Unlocked(first + 1)) first++;
        Pick(first);
    }

    void Pick(int i)
    {
        picked = i;
        for (int r = 0; r < rows.Length; r++)
        {
            var m = Mission.All[r];
            string state = Mission.Done(r) ? $"<color=#{Hex(Green)}>COMPLETE</color>" : Mission.Unlocked(r) ? $"<color=#{(r == i ? "0A2F3A" : Hex(Dim))}>{m.tagline}</color>" : $"<color=#{(r == i ? "8A1424" : Hex(Locked))}>LOCKED</color>";
            var f = System.Array.Find(Faction.All, x => x.id == m.faction);
            string side = f != null ? $"<color=#{Hex(f.color)}>{f.id.ToString().ToUpper()}</color>  " : "";
            rows[r].GetComponentInChildren<Text>().text = $"<size=16><color=#{Hex(Dim)}>{r + 1:00}</color></size>   <b>{m.name}</b>\n<size=16>       {side}{state}</size>";
            HoloSkin.SetOn(rows[r], r == i);
        }
        var info = Mission.All[i];
        bool open = Mission.Unlocked(i);
        heading.text = info.name;
        tagline.text = info.tagline.ToUpper() + "\n" + (info.ai is Skirmish.Difficulty d ? $"ENEMY AI: {d.ToString().ToUpper()}   ·   " : "") + $"STARTING CREDITS: ${info.credits:N0}";
        briefing.text = open ? info.briefing : $"<color=#{Hex(Locked)}>Complete {Mission.All[i - 1].name} to unlock this mission.</color>";
        var gm = GameMaps.At(info.map);
        preview.sprite = GameMaps.Preview(gm);
        map.text = gm.name;
        start.interactable = open;
    }

    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) Destroy(gameObject);
    }

    static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

    static Text Text(Transform parent, string text, Vector2 pos, int size, TextAnchor align, float width, bool bold)
    {
        var t = SettingsPanel.Label(parent, text, pos, size, align, width);
        t.font = bold ? HoloSkin.BoldFont : HoloSkin.Font;
        t.supportRichText = true;
        return t;
    }
}
