using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Chamfered Holo UI kit (Option A) for Neon Swarm.
/// Sprites live in Assets/CyberpunkRTS/UI/Resources/HoloUI and load with HoloUI.Get("Btn_Normal").
/// They are 2x sprites (PPU 200), so on a Canvas with Reference Pixels Per Unit = 100 they render at 1x.
/// Most sprites include a soft glow margin (6-12 px) outside the visible edge; see HoloUI_manifest.json.
/// </summary>
public static class HoloUI
{
    // Palette
    public static readonly Color Cyan   = new Color32(0, 229, 212, 255);
    public static readonly Color Pink   = new Color32(255, 46, 151, 255);
    public static readonly Color Yellow = new Color32(255, 212, 0, 255);
    public static readonly Color Green  = new Color32(140, 255, 26, 255);
    public static readonly Color Text   = new Color32(237, 239, 247, 255);
    public static readonly Color Muted  = new Color32(154, 163, 184, 255);
    public static readonly Color TextOnBright = new Color32(4, 18, 26, 255);
    public static readonly Color DangerText = new Color32(255, 122, 184, 255);

    public enum ButtonStyle { Default, Danger, Gold, Small }

    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite Get(string name)
    {
        if (cache.TryGetValue(name, out var s) && s) return s;
        s = Resources.Load<Sprite>("HoloUI/" + name);
        if (!s) Debug.LogWarning("[HoloUI] Missing sprite HoloUI/" + name);
        cache[name] = s;
        return s;
    }

    /// <summary>Sets a sliced sprite on an Image.</summary>
    public static Image SetSliced(Image img, string sprite, Color? tint = null)
    {
        if (!img) return null;
        img.sprite = Get(sprite);
        img.type = Image.Type.Sliced;
        img.fillCenter = true;
        img.pixelsPerUnitMultiplier = 1f;
        img.color = tint ?? Color.white;
        return img;
    }

    /// <summary>Panel backgrounds: Panel_Modal, Panel_Frame, Panel_Frame_Pink, Panel_BuildBar, Panel_Minimap, Panel_Inset, Chip_*, Tooltip, Toast_*.</summary>
    public static Image StylePanel(Image img, string sprite = "Panel_Frame") => SetSliced(img, sprite);

    /// <summary>Applies sprite-swap states to a Button (or Toggle) and its Image.</summary>
    public static void StyleButton(Selectable b, ButtonStyle style = ButtonStyle.Default)
    {
        if (!b) return;
        string n, h, p, d = "Btn_Disabled";
        Color label = Text;
        switch (style)
        {
            case ButtonStyle.Danger: n = "Btn_Danger"; h = "Btn_Danger_Hover"; p = "Btn_Danger_Hover"; label = DangerText; break;
            case ButtonStyle.Gold:   n = "Btn_Gold";   h = "Btn_Gold_Hover";   p = "Btn_Gold_Hover";   label = Yellow; break;
            case ButtonStyle.Small:  n = "Btn_Small";  h = "Btn_Small_Hover";  p = "Btn_Small_On";     break;
            default:                 n = "Btn_Normal"; h = "Btn_Hover";        p = "Btn_Pressed";      break;
        }
        var img = b.targetGraphic as Image ?? b.GetComponent<Image>();
        if (img) { SetSliced(img, n); b.targetGraphic = img; }
        b.transition = Selectable.Transition.SpriteSwap;
        b.spriteState = new SpriteState
        {
            highlightedSprite = Get(h),
            pressedSprite = Get(p),
            selectedSprite = Get(h),
            disabledSprite = Get(d),
        };
        var swap = b.GetComponent<HoloLabelColor>() ?? b.gameObject.AddComponent<HoloLabelColor>();
        swap.normal = label;
        swap.pressed = (style == ButtonStyle.Default || style == ButtonStyle.Small) ? TextOnBright : label;
        swap.Refresh();
    }

    /// <summary>Input field background (InputField or TMP_InputField: pass the Selectable).</summary>
    public static void StyleInput(Selectable field)
    {
        if (!field) return;
        var img = field.targetGraphic as Image ?? field.GetComponent<Image>();
        if (img) { SetSliced(img, "Input_Field"); field.targetGraphic = img; }
        field.transition = Selectable.Transition.SpriteSwap;
        field.spriteState = new SpriteState
        {
            highlightedSprite = Get("Input_Field_Focus"),
            selectedSprite = Get("Input_Field_Focus"),
            pressedSprite = Get("Input_Field_Focus"),
            disabledSprite = Get("Input_Field"),
        };
    }

    /// <summary>Slider: dark track, tinted fill, glowing diamond handle.</summary>
    public static void StyleSlider(Slider s, Color? fill = null)
    {
        if (!s) return;
        var bg = s.transform.Find("Background")?.GetComponent<Image>();
        if (bg) SetSliced(bg, "Slider_Track");
        if (s.fillRect) SetSliced(s.fillRect.GetComponent<Image>(), "Bar_Fill_White", fill ?? Cyan);
        if (s.handleRect)
        {
            var hi = s.handleRect.GetComponent<Image>();
            if (hi) { hi.sprite = Get("Slider_Handle"); hi.type = Image.Type.Simple; hi.color = Color.white; hi.preserveAspect = true; }
        }
        s.transition = Selectable.Transition.None;
    }

    /// <summary>Health / power / progress bar: background + tinted fill.</summary>
    public static void StyleBar(Image background, Image fill, Color fillColor)
    {
        SetSliced(background, "Bar_BG");
        SetSliced(fill, "Bar_Fill_White", fillColor);
    }

    public enum CardState { Normal, Hover, Selected, Building, Locked, Empty }

    /// <summary>Build bar card background for a state. Locked cards should also show Card_LockedStripes (Tiled).</summary>
    public static void StyleCard(Image img, CardState state) => SetSliced(img, "Card_" + state);

    /// <summary>Adds a tiled stripe overlay (for locked cards). Returns the overlay Image.</summary>
    public static Image AddLockedStripes(RectTransform card)
    {
        var go = new GameObject("LockedStripes", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(card, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8, 8); rt.offsetMax = new Vector2(-8, -8);
        var img = go.GetComponent<Image>();
        img.sprite = Get("Card_LockedStripes"); img.type = Image.Type.Tiled; img.raycastTarget = false;
        return img;
    }

    /// <summary>Adds the two corner brackets (top-right + bottom-left) used on big panels.</summary>
    public static void AddCornerBrackets(RectTransform panel, Color color, float size = 28f, float outset = 2f)
    {
        MakeBracket(panel, color, size, new Vector2(1, 1), new Vector2(outset, outset), -90f);
        MakeBracket(panel, color, size, new Vector2(0, 0), new Vector2(-outset, -outset), 90f);
    }

    static void MakeBracket(RectTransform parent, Color c, float size, Vector2 anchor, Vector2 offset, float rot)
    {
        var go = new GameObject("Bracket", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
        float full = size * (64f / 48f);           // sprite has an 8px glow margin around a 48px L
        rt.sizeDelta = new Vector2(full, full);
        float half = full * 0.5f - size * (8f / 48f);
        rt.anchoredPosition = new Vector2(anchor.x > 0.5f ? -half : half, anchor.y > 0.5f ? -half : half) + offset;
        rt.localEulerAngles = new Vector3(0, 0, rot);
        var img = go.GetComponent<Image>();
        img.sprite = Get("Deco_CornerBracket"); img.color = c; img.raycastTarget = false;
    }
}
