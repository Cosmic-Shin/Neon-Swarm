using UnityEngine;
using UnityEngine.UI;

// Neon Swarm's use of the Chamfered Holo kit (HoloUI). Our widgets are a dark Image plus an old neon "Frame"
// child; these helpers hide that frame and put the kit sprite on a "HoloBG" child drawn behind the content.
// HoloBG reaches past the rect by the sprite's glow margin, so the visible edge stays where the old frame was
// and padding inside doesn't shrink. Used by the scene bake (Tools > GD5 > Apply Holo Skin) and by every
// screen built in code, so both look the same. Safe to call twice on the same element.
public static class HoloSkin
{
    // Transparent glow outside each sprite's visible edge, in 1x pixels (README_HoloUI.md).
    public static float Glow(string sprite) => sprite switch
    {
        "Panel_Modal" => 12f,
        "Panel_Minimap" => 10f,
        "Panel_Frame" or "Panel_Frame_Pink" or "Panel_BuildBar" => 8f,
        "Btn_Small" or "Btn_Small_Hover" or "Btn_Small_On" => 4f,
        "Panel_Inset" or "Fill_White" => 2f,
        _ when sprite.StartsWith("Btn_") || sprite.StartsWith("Card_") || sprite.StartsWith("Chip_") || sprite == "Tooltip" || sprite == "Outline_White" => 6f,
        _ => 0f,
    };

    // The kit sprite behind rt's content. rt's own Image (if any) turns invisible but still catches clicks.
    public static Image Back(RectTransform rt, string sprite)
    {
        var old = rt.Find("Frame");
        if (old != null) old.gameObject.SetActive(false);
        var bgT = rt.Find("HoloBG") as RectTransform;
        if (bgT == null)
        {
            bgT = new GameObject("HoloBG", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            bgT.SetParent(rt, false);
            bgT.gameObject.AddComponent<LayoutElement>().ignoreLayout = true; // fine inside layout groups
        }
        bgT.SetAsFirstSibling();
        bgT.anchorMin = Vector2.zero;
        bgT.anchorMax = Vector2.one;
        float g = Glow(sprite);
        bgT.offsetMin = new Vector2(-g, -g);
        bgT.offsetMax = new Vector2(g, g);
        var bg = HoloUI.SetSliced(bgT.GetComponent<Image>(), sprite);
        bg.raycastTarget = false;
        if (rt.TryGetComponent(out Image own) && own != bg) own.color = Color.clear;
        return bg;
    }

    // Menu windows and modals: Panel_Modal, two cyan corner brackets and the full HoloFX (opens with a fade).
    public static Image Modal(RectTransform rt)
    {
        var bg = Back(rt, "Panel_Modal");
        Brackets(rt, HoloUI.Cyan);
        var fx = FX(rt, HoloUI.Cyan);
        fx.code = "NS-" + (Mathf.Abs(rt.name.GetHashCode()) % 900 + 100);
        fx.caption = "LINK STABLE";
        return bg;
    }

    // Scanlines, light sweep, breathing glow (HoloFX). HUD panels use a lower strength and no intro.
    public static HoloFX FX(RectTransform rt, Color tint, bool intro = true, float strength = 1f, bool aura = true)
    {
        if (rt == null) return null;
        if (!rt.TryGetComponent(out HoloFX fx)) fx = rt.gameObject.AddComponent<HoloFX>();
        fx.tint = tint; fx.intro = intro; fx.strength = strength; fx.aura = aura;
        return fx;
    }

    // ---- fonts: Oxanium (SIL OFL), the kit's suggested face ----

    static Font regular, bold;
    public static Font Font => regular != null ? regular : regular = Resources.Load<Font>("HoloUI/Oxanium-Medium");
    public static Font BoldFont => bold != null ? bold : bold = Resources.Load<Font>("HoloUI/Oxanium-Bold");

    // Every Text under root: Oxanium; bold text gets the real bold face instead of a faked one.
    public static void ApplyFont(Transform root)
    {
        if (Font == null) return;
        foreach (var t in root.GetComponentsInChildren<Text>(true)) SetFont(t);
    }

    public static void SetFont(Text t)
    {
        if (t == null || Font == null) return;
        bool isBold = t.fontStyle == FontStyle.Bold || t.font == BoldFont;
        t.font = isBold ? BoldFont : Font;
        if (t.fontStyle == FontStyle.Bold) t.fontStyle = FontStyle.Normal;
    }

    public static void Brackets(RectTransform rt, Color c)
    {
        if (rt.Find("Bracket") == null) HoloUI.AddCornerBrackets(rt, c);
    }

    public static string Normal(HoloUI.ButtonStyle style) => style switch
    {
        HoloUI.ButtonStyle.Danger => "Btn_Danger",
        HoloUI.ButtonStyle.Gold => "Btn_Gold",
        HoloUI.ButtonStyle.Small => "Btn_Small",
        _ => "Btn_Normal",
    };

    // Default / Danger / Gold / Small buttons: kit sprite swaps on HoloBG, label colour from HoloLabelColor.
    public static void Button(Selectable b, HoloUI.ButtonStyle style = HoloUI.ButtonStyle.Default)
    {
        if (b == null) return;
        b.targetGraphic = Back((RectTransform)b.transform, Normal(style));
        HoloUI.StyleButton(b, style);
        if (style != HoloUI.ButtonStyle.Small && ((RectTransform)b.transform).rect.height >= 40f) // full-size buttons only
        {
            if (!b.TryGetComponent(out HoloButtonFX fx)) fx = b.gameObject.AddComponent<HoloButtonFX>();
            fx.accent = style switch { HoloUI.ButtonStyle.Danger => HoloUI.Pink, HoloUI.ButtonStyle.Gold => HoloUI.Yellow, _ => HoloUI.Cyan };
        }
        else if (b.TryGetComponent(out HoloButtonFX extra)) Object.DestroyImmediate(extra); // restyled to Small
    }

    // A small toggle that is ON shows Btn_Small_On with dark text.
    public static void SetOn(Selectable b, bool on)
    {
        if (b == null || b.targetGraphic is not Image img) return;
        var want = HoloUI.Get(on ? "Btn_Small_On" : "Btn_Small");
        if (img.sprite == want) return;
        img.sprite = want;
        if (b.TryGetComponent(out HoloLabelColor label)) { label.normal = on ? HoloUI.TextOnBright : HoloUI.Text; label.Refresh(); }
    }

    // List rows and save slots: Panel_Inset, Btn_Hover while hovered.
    public static void Row(Selectable b)
    {
        if (b == null) return;
        var bg = Back((RectTransform)b.transform, "Panel_Inset");
        b.targetGraphic = bg;
        b.transition = Selectable.Transition.SpriteSwap;
        b.spriteState = new SpriteState
        {
            highlightedSprite = HoloUI.Get("Btn_Hover"),
            selectedSprite = HoloUI.Get("Panel_Inset"),
            pressedSprite = HoloUI.Get("Btn_Hover"),
            disabledSprite = HoloUI.Get("Panel_Inset"),
        };
    }

    // InputField: its own Image becomes the field (no glow margin); the old frame goes.
    public static void Input(InputField f)
    {
        if (f == null) return;
        var old = f.transform.Find("Frame");
        if (old != null) old.gameObject.SetActive(false);
        HoloUI.StyleInput(f);
        if (f.textComponent != null) f.textComponent.color = HoloUI.Text;
        if (f.placeholder != null) f.placeholder.color = HoloUI.Muted;
    }

    // Bars: Bar_BG behind, Bar_Fill_White tinted. Callers keep setting fill colour and size as before.
    public static void Bar(Image back, Image fill, Color fillColor)
    {
        if (back == null || fill == null) return;
        HoloUI.StyleBar(back, fill, fillColor);
    }

    // Panel title: a pink tick left of the text and a cyan rule under it (kept in place as the text changes).
    public static void Title(Text t, float ruleWidth)
    {
        if (t == null) return;
        if (!t.TryGetComponent(out HoloTitle title)) title = t.gameObject.AddComponent<HoloTitle>();
        title.ruleWidth = ruleWidth;
        title.Build();
    }

    // ---- build cards ----

    // A shop button whose own Image used to be the unit icon: the icon moves to an "Icon" child, the card frame
    // goes on HoloBG behind it, and the "Progress" child becomes a Card_ProgressFill along the bottom.
    public static void Card(Button b)
    {
        if (b == null) return;
        var rt = (RectTransform)b.transform;
        var own = b.GetComponent<Image>();
        var icon = rt.Find("Icon") as RectTransform;
        if (icon == null)
        {
            var img = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon = img.rectTransform;
            icon.SetParent(rt, false);
            img.sprite = own != null ? own.sprite : null;
            img.raycastTarget = false;
            icon.anchorMin = Vector2.zero;
            icon.anchorMax = Vector2.one;
            icon.offsetMin = new Vector2(8f, 8f);
            icon.offsetMax = new Vector2(-8f, -8f);
        }
        b.targetGraphic = Back(rt, "Card_Normal");
        icon.SetSiblingIndex(1); // just above the card frame, under every label
        b.transition = Selectable.Transition.SpriteSwap;
        SetCard(b, HoloUI.CardState.Normal, true);

        if (rt.Find("Progress") is RectTransform p && p.TryGetComponent(out Image fill))
        {
            HoloUI.SetSliced(fill, "Card_ProgressFill", new Color(HoloUI.Cyan.r, HoloUI.Cyan.g, HoloUI.Cyan.b, 0.45f));
            p.SetSiblingIndex(2);
            p.anchorMin = Vector2.zero;
            p.anchorMax = new Vector2(1f, 0f);
            p.pivot = new Vector2(0.5f, 0f);
            p.anchoredPosition = new Vector2(0f, 6f);
            p.sizeDelta = new Vector2(-12f, 0f);
        }
    }

    // The card's sprite for its state. Disabled (can't afford) keeps the state's sprite; only Normal glows on hover.
    public static void SetCard(Button b, HoloUI.CardState state, bool force = false)
    {
        if (b == null || b.targetGraphic is not Image img) return;
        var sprite = HoloUI.Get("Card_" + state);
        if (!force && img.sprite == sprite) return;
        img.sprite = sprite;
        bool normal = state == HoloUI.CardState.Normal;
        b.spriteState = new SpriteState
        {
            highlightedSprite = normal ? HoloUI.Get("Card_Hover") : sprite,
            selectedSprite = sprite,
            pressedSprite = normal ? HoloUI.Get("Card_Selected") : sprite,
            disabledSprite = sprite,
        };
        var stripes = b.transform.Find("LockBadge");
        if (stripes != null && state != HoloUI.CardState.Locked) stripes.gameObject.SetActive(false);
    }

    // Production progress 0..1 as the height of the card's Card_ProgressFill.
    public static void SetProgress(Image fill, float progress)
    {
        if (fill == null) return;
        var r = fill.rectTransform;
        float h = ((RectTransform)r.parent).rect.height - 12f;
        r.sizeDelta = new Vector2(r.sizeDelta.x, Mathf.Max(0f, h * Mathf.Clamp01(progress)));
        fill.enabled = progress > 0f;
    }
}
