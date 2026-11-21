using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BadgeArtBaker
{
    const int N = 256;
    const string Folder = "Assets/Resources/Badges";
    delegate float Sdf(Vector2 p);

    [MenuItem("Tools/GD5/Bake Badge Art")]
    public static void Bake()
    {
        Directory.CreateDirectory(Folder);
        var icons = new Dictionary<string, Sdf>
        {
            ["kills"] = Crosshair, ["wins"] = Crown, ["swarm"] = Swarm, ["build"] = Tower, ["credits"] = Crystal, ["research"] = Flask,
            ["hard"] = RobotSkull, ["online"] = Network, ["streak"] = Flame, ["speed"] = Bolt, ["hours"] = Hourglass, ["killer"] = Burst,
        };
        foreach (var kv in icons) Save("icon_" + kv.Key, Render(kv.Value, 0.035f, true));
        if (File.Exists(Folder + "/plate.png")) AssetDatabase.DeleteAsset(Folder + "/plate.png");
        AssetDatabase.Refresh();
        foreach (var path in Directory.GetFiles(Folder, "*.png"))
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        Debug.Log("[Badges] baked " + icons.Count + " sprites to " + Folder);
    }

    static void Save(string name, Texture2D tex) => File.WriteAllBytes($"{Folder}/{name}.png", tex.EncodeToPNG());

    static Texture2D Render(Sdf f, float bevel, bool icon, bool plate = false)
    {
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        var px = new Color32[N * N];
        Vector2 light = new Vector2(-0.6f, 0.8f).normalized;
        const float e = 1.5f / N;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                var p = new Vector2((x + 0.5f) / N * 2f - 1f, (y + 0.5f) / N * 2f - 1f);
                float d = f(p);
                float aa = 2f / N;
                float a = Mathf.Clamp01(0.5f - d / aa);
                if (a <= 0f) { px[y * N + x] = new Color32(0, 0, 0, 0); continue; }
                Vector2 g = new(f(p + new Vector2(e, 0f)) - f(p - new Vector2(e, 0f)), f(p + new Vector2(0f, e)) - f(p - new Vector2(0f, e)));
                g = g.sqrMagnitude > 1e-9f ? g.normalized : Vector2.zero;
                float depth = -d;
                float v;
                if (plate)
                {
                    v = Mathf.Lerp(0.22f, 0.08f, (p.y + 1f) * 0.5f);
                    if (depth < bevel) v += 0.25f * Mathf.Clamp01(1f - depth / bevel) * Mathf.Max(0f, Vector2.Dot(-g, light));
                }
                else if (depth < bevel)
                {
                    float k = depth / bevel;
                    float lit = Vector2.Dot(-g, light);
                    v = Mathf.Lerp(0.55f + 0.45f * lit, 0.9f, k * k);
                    if (depth < aa * 1.2f) v *= 0.55f;
                }
                else
                {
                    v = Mathf.Lerp(0.98f, 0.72f, (p.y + 1f) * 0.5f * 0.8f + 0.1f);
                    if (!icon) v *= 0.9f + 0.1f * Mathf.Sin((p.x + p.y) * 18f);
                }
                v = Mathf.Clamp01(v);
                byte c = (byte)(v * 255f);
                px[y * N + x] = new Color32(c, c, c, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
    static float Ring(Vector2 p, Vector2 c, float r, float w) => Mathf.Abs((p - c).magnitude - r) - w;

    static float Box(Vector2 p, Vector2 c, Vector2 half, float rot = 0f, float round = 0f)
    {
        p -= c;
        if (rot != 0f) { float s = Mathf.Sin(-rot), co = Mathf.Cos(-rot); p = new Vector2(p.x * co - p.y * s, p.x * s + p.y * co); }
        Vector2 q = new(Mathf.Abs(p.x) - half.x + round, Mathf.Abs(p.y) - half.y + round);
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - round;
    }

    static float Seg(Vector2 p, Vector2 a, Vector2 b, float r)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude - r;
    }

    static float Poly(Vector2 p, params Vector2[] v)
    {
        float d = Vector2.Dot(p - v[0], p - v[0]);
        float s = 1f;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
        {
            Vector2 e = v[j] - v[i], w = p - v[i];
            Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
            d = Mathf.Min(d, Vector2.Dot(b, b));
            bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
            if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
        }
        return s * Mathf.Sqrt(d);
    }

    static float Hex(Vector2 p, float r)
    {
        p = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y));
        return Mathf.Max(p.x * 0.866f + p.y * 0.5f, p.y) - r;
    }

    static float Star(Vector2 p, int n, float outer, float inner, float rot = 0f)
    {
        var v = new Vector2[n * 2];
        for (int i = 0; i < n * 2; i++)
        {
            float a = rot + Mathf.PI / 2f + i * Mathf.PI / n;
            float r = i % 2 == 0 ? outer : inner;
            v[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        return Poly(p, v);
    }

    static float U(params float[] d) { float m = d[0]; for (int i = 1; i < d.Length; i++) m = Mathf.Min(m, d[i]); return m; }
    static float Sub(float a, float b) => Mathf.Max(a, -b);
    static Vector2 V(float x, float y) => new(x, y);

    static float Crosshair(Vector2 p) => U(Ring(p, Vector2.zero, 0.5f, 0.06f), Circle(p, Vector2.zero, 0.09f),
        Seg(p, V(0f, 0.3f), V(0f, 0.8f), 0.055f), Seg(p, V(0f, -0.3f), V(0f, -0.8f), 0.055f),
        Seg(p, V(0.3f, 0f), V(0.8f, 0f), 0.055f), Seg(p, V(-0.3f, 0f), V(-0.8f, 0f), 0.055f));

    static float Crown(Vector2 p)
    {
        float body = Poly(p, V(-0.7f, -0.45f), V(0.7f, -0.45f), V(0.78f, 0.45f), V(0.38f, 0.05f), V(0f, 0.6f), V(-0.38f, 0.05f), V(-0.78f, 0.45f));
        float band = Box(p, V(0f, -0.56f), V(0.72f, 0.1f), 0f, 0.04f);
        float gems = U(Circle(p, V(0f, 0.72f), 0.1f), Circle(p, V(0.82f, 0.56f), 0.09f), Circle(p, V(-0.82f, 0.56f), 0.09f));
        return U(Sub(body, Circle(p, V(0f, -0.15f), 0.12f)), band, gems);
    }

    static float Swarm(Vector2 p) => U(Hex(p - V(0f, 0.32f), 0.28f), Hex(p - V(-0.36f, -0.3f), 0.28f), Hex(p - V(0.36f, -0.3f), 0.28f),
        Circle(p, V(0.72f, 0.42f), 0.08f), Circle(p, V(-0.72f, 0.42f), 0.06f), Circle(p, V(0f, -0.8f), 0.07f));

    static float Tower(Vector2 p)
    {
        float body = U(Box(p, V(0f, -0.15f), V(0.32f, 0.62f), 0f, 0.03f), Box(p, V(-0.5f, -0.45f), V(0.2f, 0.32f), 0f, 0.03f), Box(p, V(0.5f, -0.4f), V(0.2f, 0.37f), 0f, 0.03f));
        float windows = U(Box(p, V(-0.12f, 0.2f), V(0.07f, 0.08f)), Box(p, V(0.12f, 0.2f), V(0.07f, 0.08f)), Box(p, V(-0.12f, -0.05f), V(0.07f, 0.08f)),
            Box(p, V(0.12f, -0.05f), V(0.07f, 0.08f)), Box(p, V(0f, -0.55f), V(0.1f, 0.18f)));
        return U(Sub(body, windows), Seg(p, V(0f, 0.47f), V(0f, 0.82f), 0.04f), Circle(p, V(0f, 0.86f), 0.07f));
    }

    static float Crystal(Vector2 p)
    {
        float gem = Poly(p, V(0f, -0.85f), V(0.62f, 0.15f), V(0.38f, 0.55f), V(-0.38f, 0.55f), V(-0.62f, 0.15f));
        float facets = U(Seg(p, V(-0.62f, 0.15f), V(0.62f, 0.15f), 0.025f), Seg(p, V(-0.2f, 0.15f), V(0f, -0.8f), 0.025f), Seg(p, V(0.2f, 0.15f), V(0f, -0.8f), 0.025f),
            Seg(p, V(-0.2f, 0.15f), V(-0.1f, 0.55f), 0.025f), Seg(p, V(0.2f, 0.15f), V(0.1f, 0.55f), 0.025f));
        return U(Sub(gem, facets), Circle(p, V(0.72f, 0.7f), 0.06f), Circle(p, V(-0.75f, -0.45f), 0.05f));
    }

    static float Flask(Vector2 p)
    {
        float glass = Poly(p, V(-0.16f, 0.6f), V(0.16f, 0.6f), V(0.16f, 0.15f), V(0.62f, -0.62f), V(0.52f, -0.78f), V(-0.52f, -0.78f), V(-0.62f, -0.62f), V(-0.16f, 0.15f));
        float lip = Box(p, V(0f, 0.66f), V(0.26f, 0.06f), 0f, 0.03f);
        float liquid = Poly(p, V(-0.42f, -0.3f), V(0.42f, -0.3f), V(0.56f, -0.62f), V(0.48f, -0.72f), V(-0.48f, -0.72f), V(-0.56f, -0.62f));
        float shell = Sub(glass, Sub(Poly(p, V(-0.08f, 0.55f), V(0.08f, 0.55f), V(0.08f, 0.12f), V(0.5f, -0.6f), V(0.45f, -0.68f), V(-0.45f, -0.68f), V(-0.5f, -0.6f), V(-0.08f, 0.12f)), liquid));
        return U(shell, lip, Circle(p, V(0.25f, 0.42f), 0.07f), Circle(p, V(0.4f, 0.68f), 0.05f), Circle(p, V(0.15f, 0.85f), 0.04f));
    }

    static float RobotSkull(Vector2 p)
    {
        float head = Box(p, V(0f, 0.12f), V(0.58f, 0.52f), 0f, 0.18f);
        float jaw = Box(p, V(0f, -0.5f), V(0.38f, 0.2f), 0f, 0.06f);
        float eyes = U(Poly(p, V(-0.42f, 0.25f), V(-0.1f, 0.12f), V(-0.12f, -0.02f), V(-0.42f, 0.02f)), Poly(p, V(0.42f, 0.25f), V(0.1f, 0.12f), V(0.12f, -0.02f), V(0.42f, 0.02f)));
        float teeth = U(Box(p, V(-0.19f, -0.5f), V(0.035f, 0.13f)), Box(p, V(0f, -0.5f), V(0.035f, 0.13f)), Box(p, V(0.19f, -0.5f), V(0.035f, 0.13f)));
        float nose = Poly(p, V(0f, -0.08f), V(0.08f, -0.22f), V(-0.08f, -0.22f));
        float antenna = U(Seg(p, V(-0.35f, 0.62f), V(-0.5f, 0.85f), 0.035f), Circle(p, V(-0.5f, 0.86f), 0.06f));
        return U(Sub(Sub(Sub(U(head, jaw), eyes), teeth), nose), antenna);
    }

    static float Network(Vector2 p)
    {
        Vector2 a = V(0f, 0.6f), b = V(-0.6f, -0.35f), c = V(0.6f, -0.35f), m = V(0f, 0f);
        return U(Circle(p, a, 0.17f), Circle(p, b, 0.17f), Circle(p, c, 0.17f), Circle(p, m, 0.13f),
            Seg(p, a, b, 0.04f), Seg(p, b, c, 0.04f), Seg(p, c, a, 0.04f), Seg(p, m, a, 0.035f), Seg(p, m, b, 0.035f), Seg(p, m, c, 0.035f),
            Ring(p, Vector2.zero, 0.86f, 0.025f));
    }

    static float Flame(Vector2 p)
    {
        float outer = Poly(p, V(0f, 0.85f), V(0.22f, 0.45f), V(0.42f, 0.55f), V(0.55f, 0.05f), V(0.48f, -0.45f), V(0.2f, -0.75f), V(-0.2f, -0.75f), V(-0.48f, -0.45f), V(-0.55f, 0.0f), V(-0.35f, 0.35f), V(-0.2f, 0.2f));
        float inner = Poly(p, V(0f, 0.25f), V(0.22f, -0.1f), V(0.25f, -0.45f), V(0f, -0.62f), V(-0.25f, -0.45f), V(-0.2f, -0.15f));
        return U(Sub(outer, Sub(inner, Circle(p, V(0f, -0.4f), 0.16f))), Circle(p, V(0f, -0.4f), 0.14f));
    }

    static float Bolt(Vector2 p) => Poly(p, V(0.18f, 0.88f), V(-0.48f, -0.05f), V(-0.06f, -0.05f), V(-0.22f, -0.88f), V(0.5f, 0.12f), V(0.08f, 0.12f), V(0.28f, 0.88f));

    static float Hourglass(Vector2 p)
    {
        float frame = U(Box(p, V(0f, 0.74f), V(0.5f, 0.07f), 0f, 0.03f), Box(p, V(0f, -0.74f), V(0.5f, 0.07f), 0f, 0.03f),
            Seg(p, V(-0.44f, 0.7f), V(-0.44f, -0.7f), 0.035f), Seg(p, V(0.44f, 0.7f), V(0.44f, -0.7f), 0.035f));
        float glass = U(Poly(p, V(-0.32f, 0.62f), V(0.32f, 0.62f), V(0.04f, 0.02f), V(-0.04f, 0.02f)), Poly(p, V(-0.04f, -0.02f), V(0.04f, -0.02f), V(0.32f, -0.62f), V(-0.32f, -0.62f)));
        float sand = U(Poly(p, V(-0.16f, 0.32f), V(0.16f, 0.32f), V(0.03f, 0.04f), V(-0.03f, 0.04f)), Poly(p, V(0f, -0.3f), V(0.28f, -0.6f), V(-0.28f, -0.6f)));
        return U(frame, Sub(glass, Sub(Poly(p, V(-0.25f, 0.56f), V(0.25f, 0.56f), V(0.03f, 0.06f), V(-0.03f, 0.06f)), sand)), Seg(p, V(0f, 0.04f), V(0f, -0.3f), 0.015f));
    }

    static float Burst(Vector2 p) => U(Sub(Star(p, 8, 0.88f, 0.45f, 0.2f), Circle(p, Vector2.zero, 0.3f)), Circle(p, Vector2.zero, 0.2f));
}
