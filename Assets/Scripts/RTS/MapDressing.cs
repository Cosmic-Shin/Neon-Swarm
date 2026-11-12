using System.Collections.Generic;
using UnityEngine;

public class MapDressing : MonoBehaviour
{
    static float MapHalf => GameMaps.Current.Half;
    static readonly Color[] Neon = { new(0.2f, 0.9f, 1f), new(1f, 0.2f, 0.85f), new(1f, 0.75f, 0.2f), new(0.4f, 1f, 0.5f) };

    Shader additive;
    readonly List<(Material m, float speed)> flowing = new();
    readonly List<Car> cars = new();
    readonly List<(Transform t, Material m, float phase, Color c)> billboards = new();
    readonly List<(Transform t, float speed)> searchlights = new();
    readonly List<(Vector3 centre, List<OreNode> nodes, float max, Material m, Color c)> orePools = new();
    readonly List<(Vector3 at, GameObject pad, Transform hex)> pads = new();
    Transform root;
    Camera cam;
    float nextSlow;

    class Car { public Transform t; public Vector3 dir; public float speed; public Material m; public Color c; }

    void Start()
    {
        cam = GetComponent<Camera>();
        additive = Shader.Find("Legacy Shaders/Particles/Additive");
        root = new GameObject("MapDressing").transform;
        Ground();
        Roads();
        Traffic();
        Billboards();
        Searchlights();
        OrePools();
        BasePads();
    }


    Material Glow(Texture tex, Color c, float hdr)
    {
        var m = new Material(additive) { mainTexture = tex };
        m.SetColor("_TintColor", new Color(c.r * hdr, c.g * hdr, c.b * hdr, c.a));
        return m;
    }

    Transform FlatQuad(string name, Vector3 at, float size, Material m, float yaw = 0f)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.name = name;
        q.transform.SetParent(root, false);
        q.transform.SetPositionAndRotation(at, Quaternion.Euler(90f, yaw, 0f));
        q.transform.localScale = Vector3.one * size;
        var r = q.GetComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return q.transform;
    }

    static Texture2D Tex(int w, int h, System.Func<float, float, float> alpha, TextureWrapMode wrap = TextureWrapMode.Clamp)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = wrap };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte a = (byte)(Mathf.Clamp01(alpha((x + 0.5f) / w, (y + 0.5f) / h)) * 255);
                px[y * w + x] = new Color32(a, a, a, a);
            }
        t.SetPixels32(px);
        t.Apply(true);
        return t;
    }

    static float Soft(float x, float y) { float d = Vector2.Distance(new Vector2(x, y), Vector2.one * 0.5f) * 2f; return Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d); }

    LineRenderer Line(string name, Material m, float width, params Vector3[] points)
    {
        var lr = new GameObject(name).AddComponent<LineRenderer>();
        lr.transform.SetParent(root, false);
        lr.sharedMaterial = m;
        lr.widthMultiplier = width;
        lr.positionCount = points.Length;
        lr.SetPositions(points);
        lr.alignment = LineAlignment.TransformZ;
        lr.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        lr.textureMode = LineTextureMode.Tile;
        lr.textureScale = new Vector2(width / 6f, 1f);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }


    void Ground()
    {
        var shader = Shader.Find("GD5/GroundDetail");
        if (shader == null) return;
        var q = FlatQuad("GroundDetail", new Vector3(0f, 0.06f, 0f), MapHalf * 2f, new Material(shader));
        q.GetComponent<MeshRenderer>().sortingOrder = -10;
    }

    void Roads()
    {
        var spots = GameObject.Find("BaseSpots");
        if (spots == null || spots.transform.childCount == 0) return;
        var glowTex = Tex(8, 32, (x, y) => Mathf.Pow(1f - Mathf.Abs(y - 0.5f) * 2f, 2f));
        var dashTex = Tex(128, 16, (x, y) => (x < 0.12f ? 1f : 0f) * (1f - Mathf.Abs(y - 0.5f) * 2f), TextureWrapMode.Repeat);
        var bed = Glow(glowTex, new Color(0.35f, 0.1f, 0.7f, 0.5f), 1f);
        var dash = Glow(dashTex, new Color(0.3f, 0.85f, 1f, 0.5f), 1.1f);
        flowing.Add((dash, -1.2f));

        float near = float.MaxValue;
        foreach (Transform s in spots.transform) near = Mathf.Min(near, new Vector2(s.position.x, s.position.z).magnitude);
        float RingRadius = near * 0.83f, Inner = Mathf.Min(40f, near * 0.3f);
        var dirs = new List<Vector3>();
        foreach (Transform s in spots.transform) dirs.Add(new Vector3(s.position.x, 0f, s.position.z).normalized);
        dirs.Sort((a, b) => Mathf.Atan2(a.z, a.x).CompareTo(Mathf.Atan2(b.z, b.x)));
        var ring = new List<Vector3>();
        foreach (var d in dirs) ring.Add(d * RingRadius + Vector3.up * 0.08f);
        ring.Add(ring[0]);
        bool ringClear = true;
        for (int i = 1; i < ring.Count; i++) if (ChasmAlong(ring[i - 1], ring[i]) < float.MaxValue) ringClear = false;
        if (dirs.Count > 2 && ringClear)
        {
            Line("RingRoad", bed, 7f, ring.ToArray());
            Line("RingRoadLights", dash, 0.35f, ring.ToArray());
        }
        foreach (var d in dirs)
        {
            Vector3 outer = d * RingRadius + Vector3.up * 0.08f, inner = d * Inner + Vector3.up * 0.08f;
            float stop = ChasmAlong(outer, inner);
            if (stop < float.MaxValue) inner = outer + (inner - outer).normalized * Mathf.Max(0f, stop - 3f);
            if ((inner - outer).sqrMagnitude < 25f) continue;
            Line("Spoke", bed, 5f, outer, inner);
            Line("SpokeLights", dash, 0.3f, outer, inner);
        }
    }

    static float ChasmAlong(Vector3 a, Vector3 b)
    {
        Vector3 from = new(a.x, 0.1f, a.z), d = new(b.x - a.x, 0f, b.z - a.z);
        float best = float.MaxValue;
        foreach (var hit in Physics.RaycastAll(from, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
            if ((hit.collider.name == "Chasm" || hit.collider.name == "Deck") && hit.distance < best) best = hit.distance;
        return best;
    }


    void Traffic()
    {
        var dot = Tex(32, 32, Soft);
        var rng = new System.Random(7);
        for (int lane = 0; lane < 7; lane++)
        {
            bool alongX = lane % 2 == 0;
            float offset = (float)(rng.NextDouble() * 2 - 1) * MapHalf * 0.8f;
            float height = 20f + (float)rng.NextDouble() * 8f;
            int dirSign = rng.Next(2) == 0 ? -1 : 1;
            Vector3 dir = (alongX ? Vector3.right : Vector3.forward) * dirSign;
            for (int k = 0; k < 6; k++)
            {
                Color c = Neon[rng.Next(Neon.Length)];
                var m = Glow(dot, c, 2.5f);
                var car = new GameObject("Car").transform;
                car.SetParent(root, false);
                float along = (float)(rng.NextDouble() * 2 - 1) * MapHalf;
                car.position = alongX ? new Vector3(along, height, offset) : new Vector3(offset, height, along);
                var body = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(body.GetComponent<Collider>());
                body.transform.SetParent(car, false);
                body.transform.localScale = Vector3.one * 1.4f;
                body.GetComponent<MeshRenderer>().sharedMaterial = m;
                var trail = car.gameObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial = m;
                trail.time = 0.35f;
                trail.widthMultiplier = 0.5f;
                trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cars.Add(new Car { t = car, dir = dir, speed = 30f + (float)rng.NextDouble() * 25f, m = m, c = c });
            }
        }
    }

    void Billboards()
    {
        var city = GameObject.Find("City");
        if (city == null) return;
        var tall = new List<(Renderer r, float h)>();
        foreach (var r in city.GetComponentsInChildren<MeshRenderer>())
        {
            var b = r.bounds;
            if (b.size.y > 7f && b.size.x < 30f) tall.Add((r, b.max.y));
        }
        tall.Sort((a, b) => b.h.CompareTo(a.h));
        var rng = new System.Random(11);
        var used = new List<Vector3>();
        var textures = new Texture2D[4];
        for (int i = 0; i < textures.Length; i++) textures[i] = AdTexture(rng);
        float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;
        foreach (var (r, h) in tall)
        {
            if (billboards.Count >= 14) break;
            Vector3 c = r.bounds.center;
            if (used.Exists(u => (u - c).sqrMagnitude < 40f * 40f)) continue;
            used.Add(c);
            Color col = Neon[rng.Next(Neon.Length)];
            var m = Glow(textures[rng.Next(textures.Length)], col, 1.8f);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "HoloAd";
            q.transform.SetParent(root, false);
            float w = Mathf.Clamp(r.bounds.size.x * 0.8f, 4f, 9f);
            q.transform.position = new Vector3(c.x, h + w * 0.35f, c.z);
            q.transform.rotation = Quaternion.Euler(20f, yaw, 0f);
            q.transform.localScale = new Vector3(w, w * 0.55f, 1f);
            q.GetComponent<MeshRenderer>().sharedMaterial = m;
            q.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            billboards.Add((q.transform, m, (float)rng.NextDouble() * 10f, col));
        }
    }

    static Texture2D AdTexture(System.Random rng)
    {
        const int W = 128, H = 72;
        var rows = new float[6];
        for (int i = 0; i < rows.Length; i++) rows[i] = 0.3f + (float)rng.NextDouble() * 0.65f;
        int seed = rng.Next();
        return Tex(W, H, (x, y) =>
        {
            bool frame = x < 0.03f || x > 0.97f || y < 0.05f || y > 0.95f;
            if (frame) return 0.9f;
            int row = Mathf.FloorToInt((1f - y) * 7f) - 1;
            if (row < 0) return (Mathf.Sin(x * 40f + seed) > 0.2f && y < 0.9f && y > 0.78f) ? 1f : 0.08f;
            if (row >= rows.Length) return 0.08f;
            float inRow = (1f - y) * 7f % 1f;
            bool glyph = inRow > 0.25f && inRow < 0.75f && x > 0.08f && x < 0.08f + rows[row] * 0.84f && Mathf.Sin(x * 90f + row * 7f + seed) > -0.4f;
            float scan = 0.85f + 0.15f * Mathf.Sin(y * H * 3.14f);
            return (glyph ? 0.75f : 0.06f) * scan;
        });
    }

    void Searchlights()
    {
        var beamTex = Tex(16, 64, (x, y) => (1f - Mathf.Abs(x - 0.5f) * 2f) * Mathf.Pow(1f - y, 1.5f));
        var m = Glow(beamTex, new Color(0.6f, 0.8f, 1f, 0.07f), 1f);
        Vector3[] at = { new(-280, 0, 280), new(280, 0, 280), new(-280, 0, -280), new(280, 0, -280), new(0, 0, 290), new(-290, 0, 0), new(290, 0, 0) };
        var rng = new System.Random(3);
        foreach (var p in at)
        {
            var pivot = new GameObject("Searchlight").transform;
            pivot.SetParent(root, false);
            pivot.position = p;
            pivot.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            var beam = new GameObject("Beam").AddComponent<LineRenderer>();
            beam.transform.SetParent(pivot, false);
            beam.useWorldSpace = false;
            beam.sharedMaterial = m;
            beam.positionCount = 2;
            beam.SetPosition(0, Vector3.zero);
            beam.SetPosition(1, new Vector3(0f, 140f, 90f));
            beam.startWidth = 1.5f; beam.endWidth = 14f;
            beam.textureMode = LineTextureMode.Stretch;
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            searchlights.Add((pivot, (rng.Next(2) == 0 ? -1f : 1f) * (8f + (float)rng.NextDouble() * 10f)));
        }
    }


    void OrePools()
    {
        var soft = Tex(64, 64, Soft);
        var left = new List<OreNode>();
        foreach (var o in OreNode.All) if (o != null) left.Add(o);
        while (left.Count > 0)
        {
            var seed = left[0];
            var field = left.FindAll(o => (o.transform.position - seed.transform.position).sqrMagnitude < 30f * 30f);
            left.RemoveAll(field.Contains);
            Vector3 centre = Vector3.zero; float max = 0f, reach = 0f;
            foreach (var o in field) { centre += o.transform.position; max += o.maxAmount; }
            centre /= field.Count;
            foreach (var o in field) reach = Mathf.Max(reach, Vector3.Distance(o.transform.position, centre));
            bool red = seed.valueMultiplier > 1.01f;
            Color c = red ? new Color(1f, 0.25f, 0.3f) : new Color(0.3f, 1f, 0.5f);
            var m = Glow(soft, c, 1f);
            FlatQuad(red ? "OreGlowRed" : "OreGlow", new Vector3(centre.x, 0.07f, centre.z), reach * 2f + 14f, m);
            orePools.Add((centre, field, max, m, c));
        }
    }

    void BasePads()
    {
        var spots = GameObject.Find("BaseSpots");
        if (spots == null) return;
        var padTex = Tex(128, 128, (x, y) =>
        {
            Vector2 p = new(x - 0.5f, y - 0.5f);
            float r = p.magnitude * 2f;
            float hex = Mathf.Max(Mathf.Abs(p.x) * 1.732f + Mathf.Abs(p.y), Mathf.Abs(p.y) * 2f) * 1.15f;
            float edge = Mathf.Clamp01(1f - Mathf.Abs(hex - 0.95f) * 30f);
            float inner = Mathf.Clamp01(1f - Mathf.Abs(r - 0.55f) * 40f) * (Mathf.Sin(Mathf.Atan2(p.y, p.x) * 12f) > 0f ? 1f : 0f);
            float fill = hex < 0.95f ? 0.06f : 0f;
            return Mathf.Max(edge, inner * 0.8f, fill);
        });
        int n = 0;
        foreach (Transform s in spots.transform)
        {
            n++;
            var m = Glow(padTex, new Color(0.3f, 0.9f, 1f, 0.6f), 1.4f);
            var pad = new GameObject("BasePad").transform;
            pad.SetParent(root, false);
            var hex = FlatQuad("Hex", new Vector3(s.position.x, 0.09f, s.position.z), 26f, m);
            hex.SetParent(pad, true);
            var label = new GameObject("Label").AddComponent<TextMesh>();
            label.transform.SetParent(root, false);
            label.transform.SetPositionAndRotation(new Vector3(s.position.x, 0.1f, s.position.z - 15f), Quaternion.Euler(90f, 0f, 0f));
            label.text = $"BASE {n}";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            label.fontSize = 64; label.characterSize = 0.18f;
            label.anchor = TextAnchor.MiddleCenter;
            label.color = new Color(0.4f, 0.95f, 1f, 0.7f);
            label.transform.SetParent(pad, true);
            pads.Add((new Vector3(s.position.x, 0f, s.position.z), pad.gameObject, hex));
        }
    }


    void Update()
    {
        float t = Time.time, dt = Time.deltaTime;
        foreach (var (m, speed) in flowing) m.mainTextureOffset = new Vector2(t * speed, 0f);

        Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
        Quaternion face = cam != null ? cam.transform.rotation : Quaternion.identity;
        foreach (var car in cars)
        {
            car.t.position += car.dir * car.speed * dt;
            Vector3 p = car.t.position;
            if (Mathf.Abs(p.x) > MapHalf + 20f || Mathf.Abs(p.z) > MapHalf + 20f)
            {
                if (Mathf.Abs(p.x) > MapHalf + 20f) p.x = -Mathf.Sign(p.x) * (MapHalf + 15f);
                if (Mathf.Abs(p.z) > MapHalf + 20f) p.z = -Mathf.Sign(p.z) * (MapHalf + 15f);
                car.t.position = p;
                car.t.GetComponent<TrailRenderer>().Clear();
            }
            car.t.GetChild(0).rotation = face;
        }

        foreach (var (tr, m, phase, c) in billboards)
        {
            float flick = Mathf.PerlinNoise(t * 3f + phase, phase) > 0.78f ? Random.Range(0.1f, 0.6f) : 1f;
            m.SetColor("_TintColor", new Color(c.r * 1.8f, c.g * 1.8f, c.b * 1.8f, 0.55f * flick));
        }

        foreach (var (pivot, speed) in searchlights) pivot.Rotate(0f, speed * dt, 0f, Space.World);

        if (t < nextSlow) return;
        nextSlow = t + 0.5f;
        foreach (var (centre, nodes, max, m, c) in orePools)
        {
            float amount = 0f;
            foreach (var o in nodes) if (o != null) amount += o.amount;
            float k = max > 0f ? amount / max : 0f;
            float pulse = 0.85f + 0.15f * Mathf.Sin(t * 1.3f + centre.x);
            m.SetColor("_TintColor", new Color(c.r, c.g, c.b, (0.05f + 0.25f * k) * pulse));
        }
        foreach (var (at, pad, hex) in pads)
        {
            bool taken = false;
            foreach (var s in Structure.All) if (s != null && (s.transform.position - at).sqrMagnitude < 45f * 45f) { taken = true; break; }
            pad.SetActive(!taken);
            if (!taken) hex.rotation = Quaternion.Euler(90f, t * 6f, 0f);
        }
    }
}
