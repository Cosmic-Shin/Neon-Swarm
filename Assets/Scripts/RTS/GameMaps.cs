using UnityEngine;
using UnityEngine.SceneManagement;

public class GameMap
{
    public string name, scene, preview;
    public float size;
    public Vector2[] bases;
    public int[] fill;
    public int[] allies;
    public int[] enemies;
    public string[] keep = { "Centre" };
    public int[] startAi = { 0 };
    public int startYou = 1;
    public Chasm[] chasms = new Chasm[0];
    public int Players => bases.Length;
    public float Half => size * 0.5f;
}

public class Chasm
{
    public Vector2[] path;
    public bool loop;
    public float width = 14f;
    public Vector2[] bridges = new Vector2[0], walls = new Vector2[0];
    public const float BridgeGap = 20f;
    public const int WallPieces = 3;

    public static Chasm Ring(float radius, float[] bridgeAngles, float[] wallAngles)
    {
        const int Sides = 32;
        var p = new Vector2[Sides];
        for (int i = 0; i < Sides; i++) p[i] = At(radius, 360f * i / Sides);
        return new Chasm
        {
            path = p, loop = true,
            bridges = System.Array.ConvertAll(bridgeAngles, a => At(radius, a)),
            walls = System.Array.ConvertAll(wallAngles, a => At(radius, a)),
        };
    }

    static Vector2 At(float r, float deg) => new(r * Mathf.Cos(deg * Mathf.Deg2Rad), r * Mathf.Sin(deg * Mathf.Deg2Rad));
}

public static class GameMaps
{
    public static readonly GameMap[] All =
    {
        new GameMap
        {
            name = "WASTELAND  ·  8", scene = "RTSSelection", preview = "MapPreview", size = 600f,
            bases = new Vector2[] { new(-90, -218), new(90, -218), new(218, -90), new(218, 90), new(90, 218), new(-90, 218), new(-218, 90), new(-218, -90) },
            fill = new[] { 4, 2, 6, 1, 7, 3, 5 }, allies = new[] { 1, 7, 2, 6 }, enemies = new[] { 4, 3, 5, 2, 6, 1, 7 },
            chasms = new[] { Chasm.Ring(95f, new[] { 0f, 90f, 180f, 270f }, new[] { 22.5f, 67.5f, 112.5f, 157.5f, 202.5f, 247.5f, 292.5f, 337.5f }) },
        },
        new GameMap
        {
            name = "DUEL  ·  1V1", scene = "Map_Duel", preview = "MapPreview_Duel", size = 320f,
            bases = new Vector2[] { new(0, -118), new(0, 118) },
            fill = new[] { 1 }, allies = new int[0], enemies = new[] { 1 },
            chasms = new[]
            {
                new Chasm { path = new Vector2[] { new(-180, -60), new(180, -60) }, bridges = new Vector2[] { new(-80, -60), new(80, -60) }, walls = new Vector2[] { new(0, -60) } },
                new Chasm { path = new Vector2[] { new(-180, 60), new(180, 60) }, bridges = new Vector2[] { new(-80, 60), new(80, 60) }, walls = new Vector2[] { new(0, 60) } },
            },
        },
        new GameMap
        {
            name = "TWIN FRONT  ·  2V2", scene = "Map_TwinFront", preview = "MapPreview_TwinFront", size = 440f,
            bases = new Vector2[] { new(-75, -168), new(75, -168), new(75, 168), new(-75, 168) },
            fill = new[] { 3, 1, 2 }, allies = new[] { 1 }, enemies = new[] { 3, 2, 1 },
            keep = new[] { "Centre", "1", "5" }, startAi = new[] { 1, 2, 2 },
            chasms = new[]
            {
                new Chasm { path = new Vector2[] { new(-240, -80), new(240, -80) }, bridges = new Vector2[] { new(-100, -80), new(100, -80) }, walls = new Vector2[] { new(-190, -80), new(0, -80), new(190, -80) } },
                new Chasm { path = new Vector2[] { new(-240, 80), new(240, 80) }, bridges = new Vector2[] { new(-100, 80), new(100, 80) }, walls = new Vector2[] { new(-190, 80), new(0, 80), new(190, 80) } },
            },
        },
        new GameMap
        {
            name = "BREACH LINE  ·  3V3", scene = "Map_BreachLine", preview = "MapPreview_BreachLine", size = 560f,
            bases = new Vector2[] { new(-150, -215), new(0, -228), new(150, -215), new(150, 215), new(0, 228), new(-150, 215) },
            fill = new[] { 3, 1, 4, 2, 5 }, allies = new[] { 1, 2 }, enemies = new[] { 5, 4, 3 },
            keep = new[] { "Centre", "1", "5" }, startAi = new[] { 1, 1, 2, 2, 2 },
            chasms = new[]
            {
                new Chasm { path = new Vector2[] { new(-300, -70), new(300, -70) }, bridges = new Vector2[] { new(-95, -70), new(95, -70) }, walls = new Vector2[] { new(-230, -70), new(0, -70), new(230, -70) } },
                new Chasm { path = new Vector2[] { new(-300, 70), new(300, 70) }, bridges = new Vector2[] { new(-95, 70), new(95, 70) }, walls = new Vector2[] { new(-230, 70), new(0, 70), new(230, 70) } },
            },
        },
        new GameMap
        {
            name = "QUAD CORE  ·  FFA", scene = "Map_QuadCore", preview = "MapPreview_QuadCore", size = 500f,
            bases = new Vector2[] { new(-165, -165), new(165, -165), new(165, 165), new(-165, 165) },
            fill = new[] { 2, 1, 3 }, allies = new[] { 1, 3 }, enemies = new[] { 2, 1, 3 },
            keep = new[] { "Centre", "1", "3", "5", "7" }, startAi = new[] { 0, 0, 0 }, startYou = 0,
            chasms = new[] { Chasm.Ring(90f, new[] { 0f, 90f, 180f, 270f }, new[] { 45f, 135f, 225f, 315f }) },
        },
    };

    public static GameMap Current => Of(SceneManager.GetActiveScene());

    public static GameMap Of(Scene scene)
    {
        foreach (var m in All) if (m.scene == scene.name) return m;
        return All[0];
    }

    public static int IndexOf(GameMap m) => System.Array.IndexOf(All, m);
    public static GameMap At(int i) => All[Mathf.Clamp(i, 0, All.Length - 1)];
    public static string PathOf(GameMap m) => $"Assets/Scenes/{m.scene}.unity";
    public static Sprite Preview(GameMap m) => Resources.Load<Sprite>(m.preview);

    public static int[] DefaultBases(GameMap m, int slots)
    {
        var r = new int[slots];
        for (int i = 0; i < slots; i++) r[i] = i == 0 ? 0 : i - 1 < m.fill.Length ? m.fill[i - 1] : i;
        return r;
    }
}
