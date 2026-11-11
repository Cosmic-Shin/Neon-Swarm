using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// A holo burst under the mouse on every click, in menus and in matches: a ring that expands and fades, a bright
// centre flash and four short sparks. Coloured by the cursor showing at the time (CursorManager.Current):
// cyan move/select, pink attack/invalid, gold build, green harvest... An invalid click also gives a small shake.
// Lives on its own overlay canvas above everything; pooled, so clicking costs nothing. Creates itself.
public class ClickFX : MonoBehaviour
{
    const int Pool = 10;
    const float Life = 0.38f;

    class Burst { public RectTransform root; public RawImage ring, flash; public Image[] sparks; public float born = -10f; public Color c; public bool right, shake; public Vector3 at; }
    static readonly Vector2[] SparkDirs = { Dir(45f), Dir(135f), Dir(225f), Dir(315f) };
    static Vector2 Dir(float deg) => Quaternion.Euler(0f, 0f, deg) * Vector3.up;
    readonly Burst[] bursts = new Burst[Pool];
    int next;
    RectTransform canvasRect;
    static Texture2D ringTex, dotTex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("ClickFX", typeof(Canvas), typeof(CanvasScaler), typeof(ClickFX));
        DontDestroyOnLoad(go);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000; // above every menu and the HUD
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; // bursts are sized in screen pixels
    }

    void Awake()
    {
        canvasRect = (RectTransform)transform;
        Textures();
        for (int i = 0; i < Pool; i++)
        {
            var b = new Burst { root = new GameObject("Burst", typeof(RectTransform)).GetComponent<RectTransform>() };
            b.root.SetParent(transform, false);
            b.ring = Part<RawImage>(b.root, "Ring");
            b.ring.texture = ringTex;
            b.flash = Part<RawImage>(b.root, "Flash");
            b.flash.texture = dotTex;
            b.sparks = new Image[4];
            for (int s = 0; s < 4; s++)
            {
                b.sparks[s] = Part<Image>(b.root, "Spark");
                b.sparks[s].rectTransform.pivot = new Vector2(0.5f, 0f);
                b.sparks[s].rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f + s * 90f);
            }
            b.root.gameObject.SetActive(false);
            bursts[i] = b;
        }
    }

    static T Part<T>(RectTransform parent, string name) where T : Graphic
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(T)).GetComponent<T>();
        g.transform.SetParent(parent, false);
        g.raycastTarget = false;
        return g;
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse != null)
        {
            bool left = mouse.leftButton.wasPressedThisFrame, right = mouse.rightButton.wasPressedThisFrame;
            if (left || right) Spawn(mouse.position.ReadValue(), right);
        }

        float now = Time.unscaledTime;
        foreach (var b in bursts)
        {
            if (!b.root.gameObject.activeSelf) continue;
            float k = (now - b.born) / Life;
            if (k >= 1f) { b.root.gameObject.SetActive(false); continue; }
            float ease = 1f - (1f - k) * (1f - k);
            float fade = 1f - k;
            float size = Mathf.Lerp(10f, b.right ? 58f : 46f, ease);
            b.ring.rectTransform.sizeDelta = new Vector2(size, size);
            b.ring.color = new Color(b.c.r, b.c.g, b.c.b, 0.9f * fade);
            float flash = Mathf.Lerp(22f, 6f, ease);
            b.flash.rectTransform.sizeDelta = new Vector2(flash, flash);
            b.flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - k * 3f));
            for (int i = 0; i < b.sparks.Length; i++)
            {
                var sr = b.sparks[i].rectTransform;
                sr.sizeDelta = new Vector2(2f, Mathf.Lerp(4f, 12f, ease));
                sr.anchoredPosition = SparkDirs[i] * Mathf.Lerp(6f, size * 0.62f, ease); // flying outwards
                b.sparks[i].color = new Color(b.c.r, b.c.g, b.c.b, fade);
            }
            b.root.localPosition = b.at + (b.shake ? new Vector3(Mathf.Sin(now * 90f) * 3f * fade, 0f, 0f) : Vector3.zero); // invalid: a quick "no" shake
        }
    }

    void Spawn(Vector2 screen, bool right)
    {
        var kind = CursorManager.Current;
        var b = bursts[next++ % Pool];
        b.born = Time.unscaledTime;
        b.right = right;
        b.shake = kind == CursorKind.Invalid;
        b.c = ColourOf(kind);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
        b.at = local;
        b.root.localPosition = local;
        b.root.SetAsLastSibling();
        b.root.gameObject.SetActive(true);
    }

    static Color ColourOf(CursorKind k) => k switch
    {
        CursorKind.Attack or CursorKind.Invalid or CursorKind.Sell => HoloUI.Pink,
        CursorKind.Build or CursorKind.Rally or CursorKind.Capture => HoloUI.Yellow,
        CursorKind.Harvest or CursorKind.Repair => HoloUI.Green,
        _ => HoloUI.Cyan,
    };

    static void Textures()
    {
        if (ringTex != null) return;
        const int n = 64;
        ringTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        dotTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f); // 0 centre .. 1 edge
                float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.1f);   // thin bright line
                float halo = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.25f) * 0.35f; // soft glow around it
                ringTex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(ring, halo)));
                dotTex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2f)));
            }
        ringTex.Apply();
        dotTex.Apply();
    }
}
