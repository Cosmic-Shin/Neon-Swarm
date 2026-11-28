using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Full faction picker for your own seat (Skirmish setup and the multiplayer room): a card per faction with its emblem,
// tagline, four signature units and its superweapon. AI seats keep the quick cycle button.
public static class FactionPicker
{
    static readonly Color Shade = new(0f, 0.01f, 0.04f, 0.88f), Window = new(0.02f, 0.03f, 0.07f, 0.97f), Dim = new(0.6f, 0.66f, 0.78f);

    public static void Show(Transform anyUi, int current, System.Action<int> pick)
    {
        var canvas = anyUi.GetComponentInParent<Canvas>().rootCanvas.transform;
        var shade = Img(canvas, Vector2.zero, Vector2.zero, Shade);
        SettingsPanel.Stretch(shade.rectTransform);
        shade.raycastTarget = true;
        shade.name = "FactionPicker";
        HoloSkin.Layer(shade, HoloSkin.LayerPopup + 5);
        void Close() => Object.Destroy(shade.gameObject);

        var factions = Faction.All;
        float width = Mathf.Max(1, factions.Length) * 520f + 60f;
        var win = Img(shade.transform, new Vector2(0f, -10f), new Vector2(width, 680f), Window);
        HoloSkin.Modal(win.rectTransform);
        HoloSkin.FX(win.rectTransform, HoloUI.Cyan);
        Label(win.transform, "CHOOSE YOUR FACTION", new Vector2(0f, 296f), 34, HoloUI.Cyan, 900f, true);
        Label(win.transform, "Both sides share the same tech tree shape: the difference is in how they fight.", new Vector2(0f, 258f), 17, Dim, 900f);

        for (int i = 0; i < factions.Length; i++)
        {
            var f = factions[i];
            int index = i;
            Vector2 at = new((i - (factions.Length - 1) / 2f) * 520f, -30f);
            var card = SettingsPanel.Button(win.transform, "", at, new Vector2(490f, 470f));
            HoloSkin.Card(card);
            var frame = Img(card.transform, Vector2.zero, new Vector2(500f, 480f), new Color(f.color.r, f.color.g, f.color.b, i == current ? 0.9f : 0.25f));
            frame.sprite = HoloUI.Get("Outline_White");
            frame.type = Image.Type.Sliced;
            frame.transform.SetAsFirstSibling();

            if (f.emblem != null)
            {
                var emblem = Img(card.transform, new Vector2(0f, 162f), new Vector2(110f, 110f), Color.white);
                emblem.sprite = f.emblem;
                emblem.preserveAspect = true;
            }
            Label(card.transform, f.displayName.ToUpper(), new Vector2(0f, 82f), 32, f.color, 460f, true);
            var tag = Label(card.transform, f.tagline, new Vector2(0f, 42f), 17, Dim, 430f);
            tag.rectTransform.sizeDelta = new Vector2(430f, 48f);

            Label(card.transform, "SIGNATURE UNITS", new Vector2(0f, -4f), 15, Dim, 430f, true);
            var units = Signature(f);
            for (int u = 0; u < units.Count; u++)
            {
                var d = units[u];
                Vector2 slot = new((u - (units.Count - 1) / 2f) * 104f, -78f);
                var back = Img(card.transform, slot, new Vector2(92f, 92f), new Color(f.color.r, f.color.g, f.color.b, 0.18f));
                var pic = Img(card.transform, slot, new Vector2(86f, 86f), Color.white);
                pic.sprite = d.portrait != null ? d.portrait : d.icon;
                pic.preserveAspect = true;
                back.raycastTarget = pic.raycastTarget = false;
                Label(card.transform, d.displayName, slot + new Vector2(0f, -58f), 13, HoloUI.Text, 100f);
            }

            var (count, superweapon) = Roster(f);
            Label(card.transform, $"{count} units  ·  superweapon: {superweapon}", new Vector2(0f, -170f), 16, HoloUI.Text, 440f);
            var state = Label(card.transform, i == current ? "SELECTED" : "CLICK TO PLAY AS", new Vector2(0f, -206f), 18, i == current ? HoloUI.Yellow : Dim, 440f, true);

            card.onClick.AddListener(() =>
            {
                Sfx.Play(Snd.Select, 0.6f);
                Juice.EdgeFlash(f.color, 0.35f);
                pick(index);
                Close();
            });
        }

        var cancel = SettingsPanel.Button(win.transform, "CANCEL", new Vector2(0f, -300f), new Vector2(240f, 50f));
        HoloSkin.Button(cancel, HoloUI.ButtonStyle.Small);
        cancel.onClick.AddListener(Close);
    }

    // four units spread across the price range (not just the elites), cheapest-first
    static List<UnitData> Signature(Faction f)
    {
        var all = new List<UnitData>();
        foreach (var b in f.buildings)
            if (b != null && b.prefab != null && b.prefab.TryGetComponent(out Building factory))
                foreach (var u in factory.Trains)
                    if (u != null && u.Data != null && u.Data.role != UnitData.Role.Worker && !all.Contains(u.Data)) all.Add(u.Data);
        all.Sort((a, b) => a.cost.CompareTo(b.cost));
        var picks = new List<UnitData>();
        if (all.Count == 0) return picks;
        foreach (float k in new[] { 0.15f, 0.45f, 0.75f, 1f })
        {
            var d = all[Mathf.Clamp(Mathf.RoundToInt(k * (all.Count - 1)), 0, all.Count - 1)];
            if (!picks.Contains(d)) picks.Add(d);
        }
        return picks;
    }

    static (int units, string superweapon) Roster(Faction f)
    {
        var seen = new HashSet<UnitData>();
        string super = "none";
        foreach (var b in f.buildings)
        {
            if (b == null || b.prefab == null) continue;
            if (b.prefab.TryGetComponent(out SwarmSilo _)) super = b.displayName;
            if (b.prefab.TryGetComponent(out Building factory)) foreach (var u in factory.Trains) if (u != null && u.Data != null) seen.Add(u.Data);
        }
        return (seen.Count, super);
    }

    static Image Img(Transform parent, Vector2 pos, Vector2 size, Color c)
    {
        var img = new GameObject("Img", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.sizeDelta = size;
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    static Text Label(Transform parent, string text, Vector2 pos, int size, Color c, float width, bool bold = false)
    {
        var t = SettingsPanel.Label(parent, text, pos, size, TextAnchor.MiddleCenter, width);
        t.color = c;
        t.font = bold ? HoloSkin.BoldFont : HoloSkin.Font;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        return t;
    }
}
