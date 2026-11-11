using System.Collections.Generic;
using UnityEngine;

public enum CursorKind
{
        Default,
        Select,
        Move,
        Attack,
        Invalid,
        Build,
        Repair,
        Sell,
        Harvest,
        Rally,
        Capture,
        Load,
        ScrollLeft,
        ScrollUpLeft,
        ScrollUp,
        ScrollUpRight,
        ScrollRight,
        ScrollDownRight,
        ScrollDown,
        ScrollDownLeft
}

/// <summary>
/// Neon Swarm cursor set ("Anime Shard"). Textures load from Resources/HoloCursors/Cursor_[Kind]_[32|48|64].
/// Call CursorManager.Set(CursorKind.Attack) whenever the context under the mouse changes (cheap if unchanged).
/// Size: 0 = auto (32 below 1440p, 48 up to 4K, 64 at 4K+), or 32 / 48 / 64. Saved in PlayerPrefs "CursorSize".
/// </summary>
public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    static readonly int[] Sizes = { 32, 48, 64 };
    static readonly Dictionary<CursorKind, Vector2[]> Hotspots = new Dictionary<CursorKind, Vector2[]>
    {
        { CursorKind.Default, new[] { new Vector2(4, 4), new Vector2(6, 6), new Vector2(9, 9) } },
        { CursorKind.Select, new[] { new Vector2(4, 4), new Vector2(6, 6), new Vector2(9, 9) } },
        { CursorKind.Move, new[] { new Vector2(4, 4), new Vector2(6, 6), new Vector2(9, 9) } },
        { CursorKind.Attack, new[] { new Vector2(4, 4), new Vector2(6, 6), new Vector2(9, 9) } },
        { CursorKind.Invalid, new[] { new Vector2(4, 4), new Vector2(6, 6), new Vector2(9, 9) } },
        { CursorKind.Build, new[] { new Vector2(4, 4), new Vector2(6, 6), new Vector2(9, 9) } },
        { CursorKind.Repair, new[] { new Vector2(4, 4), new Vector2(6, 6), new Vector2(9, 9) } },
        { CursorKind.Sell, new[] { new Vector2(4, 4), new Vector2(6, 6), new Vector2(9, 9) } },
        { CursorKind.Harvest, new[] { new Vector2(4, 4), new Vector2(6, 6), new Vector2(9, 9) } },
        { CursorKind.Rally, new[] { new Vector2(4, 4), new Vector2(6, 6), new Vector2(9, 9) } },
        { CursorKind.Capture, new[] { new Vector2(4, 4), new Vector2(6, 6), new Vector2(9, 9) } },
        { CursorKind.Load, new[] { new Vector2(4, 4), new Vector2(6, 6), new Vector2(9, 9) } },
        { CursorKind.ScrollLeft, new[] { new Vector2(4, 16), new Vector2(6, 24), new Vector2(9, 32) } },
        { CursorKind.ScrollUpLeft, new[] { new Vector2(8, 8), new Vector2(12, 12), new Vector2(15, 15) } },
        { CursorKind.ScrollUp, new[] { new Vector2(16, 4), new Vector2(24, 6), new Vector2(32, 9) } },
        { CursorKind.ScrollUpRight, new[] { new Vector2(24, 8), new Vector2(36, 12), new Vector2(49, 15) } },
        { CursorKind.ScrollRight, new[] { new Vector2(28, 16), new Vector2(42, 24), new Vector2(55, 32) } },
        { CursorKind.ScrollDownRight, new[] { new Vector2(24, 24), new Vector2(36, 36), new Vector2(49, 49) } },
        { CursorKind.ScrollDown, new[] { new Vector2(16, 28), new Vector2(24, 42), new Vector2(32, 55) } },
        { CursorKind.ScrollDownLeft, new[] { new Vector2(8, 24), new Vector2(12, 36), new Vector2(15, 49) } },
    };

    readonly Dictionary<(CursorKind, int), Texture2D> cache = new Dictionary<(CursorKind, int), Texture2D>();
    CursorKind current = CursorKind.Default;
    int currentSize = -1;
    bool hasCurrent;

    [Tooltip("Hardware cursor (Auto) has no lag. ForceSoftware only if a platform scales cursors badly.")]
    public CursorMode mode = CursorMode.Auto;

    public const string PrefKey = "CursorSize";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance) return;
        var go = new GameObject("CursorManager");
        DontDestroyOnLoad(go);
        go.AddComponent<CursorManager>();
    }

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Apply(CursorKind.Default, true);
    }

    /// <summary>Change the cursor. Safe to call every frame.</summary>
    public static void Set(CursorKind kind)
    {
        if (!Instance) Boot();
        Instance.Apply(kind, false);
    }

    /// <summary>Edge-scroll cursor from a scroll direction (x = right, y = up). Zero vector = Default.</summary>
    public static void SetScroll(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.01f) { Set(CursorKind.Default); return; }
        int x = dir.x > 0.3f ? 1 : dir.x < -0.3f ? -1 : 0;
        int y = dir.y > 0.3f ? 1 : dir.y < -0.3f ? -1 : 0;
        CursorKind k =
            y > 0 ? (x < 0 ? CursorKind.ScrollUpLeft : x > 0 ? CursorKind.ScrollUpRight : CursorKind.ScrollUp) :
            y < 0 ? (x < 0 ? CursorKind.ScrollDownLeft : x > 0 ? CursorKind.ScrollDownRight : CursorKind.ScrollDown) :
            (x < 0 ? CursorKind.ScrollLeft : CursorKind.ScrollRight);
        Set(k);
    }

    /// <summary>0 = auto, otherwise 32 / 48 / 64. Persists and re-applies immediately.</summary>
    public static void SetSizePreference(int px)
    {
        PlayerPrefs.SetInt(PrefKey, px);
        if (Instance) Instance.Apply(Instance.current, true);
    }

    public static CursorKind Current => Instance ? Instance.current : CursorKind.Default;

    int PickSize()
    {
        int pref = PlayerPrefs.GetInt(PrefKey, 0);
        if (pref == 32 || pref == 48 || pref == 64) return pref;
        int h = Screen.height;
        return h >= 2000 ? 64 : h >= 1400 ? 48 : 32;
    }

    void Apply(CursorKind kind, bool force)
    {
        int size = PickSize();
        if (!force && hasCurrent && kind == current && size == currentSize) return;
        var tex = Load(kind, size);
        if (!tex) { tex = Load(CursorKind.Default, size); kind = CursorKind.Default; }
        if (!tex) return;
        int si = System.Array.IndexOf(Sizes, size);
        Vector2 hot = Hotspots.TryGetValue(kind, out var hs) ? hs[si] : Vector2.zero;
        Cursor.SetCursor(tex, hot, mode);
        current = kind; currentSize = size; hasCurrent = true;
    }

    Texture2D Load(CursorKind kind, int size)
    {
        if (cache.TryGetValue((kind, size), out var t) && t) return t;
        t = Resources.Load<Texture2D>($"HoloCursors/Cursor_{kind}_{size}");
        if (!t) Debug.LogWarning($"[CursorManager] Missing HoloCursors/Cursor_{kind}_{size}");
        cache[(kind, size)] = t;
        return t;
    }

    void OnApplicationFocus(bool focus) { if (focus) Apply(current, true); }

    int lastHeight;
    void Update()
    {
        if (Screen.height != lastHeight) { lastHeight = Screen.height; Apply(current, true); }
    }
}
