using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(menuName = "RTS/Avatar List", fileName = "Avatars")]
public class AvatarList : ScriptableObject
{
    public Sprite[] sprites;

    static AvatarList list;
    static Sprite circle;

    public static Sprite[] All
    {
        get
        {
            if (list == null) list = Resources.Load<AvatarList>("Avatars");
            return list != null ? list.sprites : new Sprite[0];
        }
    }

    public static Sprite Find(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var s in All) if (s != null && s.name == name) return s;
        return null;
    }

    public static string Title(string name)
    {
        if (Find(name) == null) return "INITIAL";
        string n = name.Replace("icon_unit_", "");
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < n.Length; i++)
        {
            if (i > 0 && char.IsUpper(n[i]) && !char.IsUpper(n[i - 1])) sb.Append(' ');
            sb.Append(char.ToUpper(n[i]));
        }
        return sb.ToString();
    }

    public static Color ColorOf(string username) => Color.HSVToRGB(Mathf.Abs((username ?? "").GetHashCode() % 360) / 360f, 0.75f, 1f);

    public static Sprite Circle
    {
        get
        {
            if (circle != null) return circle;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(n / 2f - d) * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    public class View
    {
        public Image ring, back, icon;
        public Text letter;

        public void Show(string avatar, string username)
        {
            var s = Find(avatar);
            icon.sprite = s;
            icon.enabled = s != null;
            letter.enabled = s == null;
            letter.text = string.IsNullOrEmpty(username) ? "?" : username.Substring(0, 1).ToUpper();
            back.color = s != null ? new Color(0.05f, 0.07f, 0.15f) : ColorOf(username);
        }
    }

    public static View Make(Transform parent, Vector2 pos, float size, Color ringColor)
    {
        var v = new View();
        v.ring = Disc(parent, pos, size, ringColor);
        v.back = Disc(v.ring.transform, Vector2.zero, size * 0.88f, Color.white);
        v.icon = Disc(v.back.transform, Vector2.zero, size * 0.84f, Color.white);
        v.icon.preserveAspect = true;
        v.letter = new GameObject("Letter", typeof(RectTransform)).AddComponent<Text>();
        v.letter.transform.SetParent(v.back.transform, false);
        v.letter.rectTransform.sizeDelta = Vector2.one * size;
        v.letter.font = HoloSkin.BoldFont;
        v.letter.fontSize = Mathf.RoundToInt(size * 0.5f);
        v.letter.alignment = TextAnchor.MiddleCenter;
        v.letter.color = Color.black;
        v.letter.raycastTarget = false;
        return v;
    }

    static Image Disc(Transform parent, Vector2 pos, float size, Color c)
    {
        var img = new GameObject("Avatar", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.rectTransform.anchoredPosition = pos;
        img.rectTransform.sizeDelta = Vector2.one * size;
        img.sprite = Circle;
        img.color = c;
        img.raycastTarget = false;
        return img;
    }
}
