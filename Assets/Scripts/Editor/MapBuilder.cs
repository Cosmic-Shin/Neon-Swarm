using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MapBuilder
{
    const string Source = "Assets/Scenes/RTSSelection.unity";
    const string FeatureArt = "Assets/Art/MapFeatures";

    [MenuItem("Tools/GD5/Build Maps")]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildSource();
        foreach (var m in GameMaps.All.Skip(1)) Build(m);
        EditorSceneManager.OpenScene(Source);
    }

    public static void BuildSource()
    {
        var m = GameMaps.All[0];
        var scene = EditorSceneManager.OpenScene(Source, OpenSceneMode.Single);
        RemoveFeatures(scene);
        var clear = new List<(Vector3 at, float r)>();
        BuildFeatures(m, clear);
        ClearRuins(scene, clear, m.Half);
        var surface = Object.FindFirstObjectByType<NavMeshSurface>();
        string asset = AssetDatabase.GetAssetPath(surface.navMeshData);
        Bake(surface, string.IsNullOrEmpty(asset) ? "Assets/Scenes/RTSSelection/NavMesh-NavMesh.asset" : asset);
        RenderPreview(m, scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[MapBuilder] {m.scene}: features rebuilt.");
    }

    static void RemoveFeatures(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (var g in scene.GetRootGameObjects())
            if (g.name == "MapFeatures" || g.name == "ChasmNav") Object.DestroyImmediate(g);
    }

    static void ClearRuins(UnityEngine.SceneManagement.Scene scene, List<(Vector3 at, float r)> clear, float half)
    {
        var city = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "City");
        if (city == null) return;
        foreach (Transform group in city.transform)
            foreach (Transform c in group.Cast<Transform>().ToList())
            {
                Vector3 p = c.position;
                if (Mathf.Abs(p.x) > half - 6f || Mathf.Abs(p.z) > half - 6f || clear.Any(z => Vector3.Distance(Flat(z.at), Flat(p)) < z.r))
                    Object.DestroyImmediate(c.gameObject);
            }
    }

    static void Bake(NavMeshSurface surface, string assetPath)
    {
        surface.RemoveData();
        surface.navMeshData = null;
        surface.BuildNavMesh();
        AssetDatabase.DeleteAsset(assetPath);
        string dir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Path.GetDirectoryName(dir).Replace('\\', '/'), Path.GetFileName(dir));
        AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
        surface.AddData();
        EditorUtility.SetDirty(surface);
    }

    public static void Build(GameMap m)
    {
        string path = GameMaps.PathOf(m), folder = $"Assets/Scenes/{m.scene}";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.DeleteAsset(folder);
        AssetDatabase.CopyAsset(Source, path);
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        RemoveFeatures(scene);
        GameObject Root(string n) => scene.GetRootGameObjects().FirstOrDefault(g => g.name == n);
        string[] keep = m.keep;
        float half = m.Half;

        var spots = Root("BaseSpots").transform;
        Vector3 oldHome = spots.GetChild(0).position, home = V3(m.bases[0]);
        for (int i = spots.childCount - 1; i >= m.Players; i--) Object.DestroyImmediate(spots.GetChild(i).gameObject);
        for (int i = 0; i < m.Players; i++) spots.GetChild(i).position = V3(m.bases[i]);

        var ore = Root("OreFields").transform;
        var starter = ore.Find("OreField_Base1_Starter");
        var back = ore.Find("OreField_Base1_Back");
        var moving = new List<Transform> { Root("CoreNexus").transform, Root("AssemblyHive").transform, starter, back };
        foreach (Transform u in Root("Units").transform) moving.Add(u);
        Quaternion turn = Turn(oldHome, home);
        foreach (var t in moving) t.SetPositionAndRotation(home + turn * (t.position - oldHome), turn * t.rotation);
        var cam = Root("Main Camera").transform;
        cam.position = home + (cam.position - oldHome);

        foreach (Transform f in ore.Cast<Transform>().ToList())
            if (f != starter && f != back && !keep.Any(k => f.name == "OreField_Red_" + k)) Object.DestroyImmediate(f.gameObject);
        for (int i = 1; i < m.Players; i++)
        {
            Vector3 spot = V3(m.bases[i]);
            Quaternion r = Turn(home, spot);
            foreach (var src in new[] { starter, back })
            {
                var copy = Object.Instantiate(src.gameObject, ore).transform;
                copy.name = src.name.Replace("Base1", "Base" + (i + 1));
                copy.SetPositionAndRotation(spot + r * (src.position - home), r * src.rotation);
            }
        }

        foreach (Transform o in Root("Outposts").transform.Cast<Transform>().ToList())
            if (!keep.Any(k => o.name == "Outpost_" + k)) Object.DestroyImmediate(o.gameObject);
        foreach (Transform g in Root("Creeps").transform.Cast<Transform>().ToList())
            if (!keep.Any(k => g.name == "Guards_" + k)) Object.DestroyImmediate(g.gameObject);
        Object.DestroyImmediate(Root("Enemies"));
        var outposts = Root("Outposts").transform.Cast<Transform>().Select(t => t.position).ToList();
        var mm = Object.FindFirstObjectByType<MatchManager>();
        var so = new SerializedObject(mm);
        var creeps = so.FindProperty("creeps");
        for (int i = creeps.arraySize - 1; i >= 0; i--)
        {
            Vector3 p = creeps.GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value;
            if (!outposts.Any(o => Vector3.Distance(Flat(o), Flat(p)) < 30f)) creeps.DeleteArrayElementAtIndex(i);
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        var clear = new List<(Vector3 at, float r)>();
        foreach (Transform s in spots) clear.Add((s.position, 50f));
        foreach (Transform f in ore) clear.Add((f.position, 18f));
        foreach (var o in outposts) clear.Add((o, 18f));
        BuildFeatures(m, clear);
        ClearRuins(scene, clear, half);

        Root("Ground").transform.localScale = new Vector3(m.size / 10f, 1f, m.size / 10f);
        var grid = Root("City").transform.Find("CityGrid");
        if (grid) grid.localScale = new Vector3(m.size, m.size, 1f);
        var fog = Root("FogOfWar").transform;
        fog.localScale = new Vector3(m.size, m.size, 1f);

        Bake(Object.FindFirstObjectByType<NavMeshSurface>(), $"{folder}/NavMesh.asset");

        RenderPreview(m, scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuild(path);
        Debug.Log($"[MapBuilder] {m.scene}: {m.Players} bases, {ore.childCount} ore fields, {outposts.Count} outposts.");
    }


    static void BuildFeatures(GameMap m, List<(Vector3 at, float r)> clear)
    {
        if (m.chasms.Length == 0) return;
        var root = new GameObject("MapFeatures");
        root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        var nav = new GameObject("ChasmNav");
        var gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Buildings/FirewallGate.prefab");
        float piece = gatePrefab.GetComponent<BoxCollider>().size.x;
        var voidMat = Mat("ChasmVoid", new Color(0.01f, 0f, 0.025f), Color.black, unlit: true);
        var depthMat = Mat("ChasmDepth", new Color(0.06f, 0.02f, 0.12f), Color.black, unlit: true);
        var edgeMat = Mat("ChasmEdge", Color.black, new Color(0.2f, 0.9f, 1f) * 2.2f);
        var streamMat = Mat("ChasmStream", Color.black, new Color(1f, 0.2f, 0.8f) * 0.9f);
        var deckMat = Mat("BridgeDeck", new Color(0.13f, 0.12f, 0.18f), Color.black);
        var railMat = Mat("BridgeRail", Color.black, new Color(0.2f, 0.9f, 1f) * 1.6f);
        var postMat = Mat("BridgePost", new Color(0.1f, 0.1f, 0.14f), new Color(1f, 0.25f, 0.75f) * 1.5f);
        int walls = 0;

        foreach (var c in m.chasms)
        {
            var pts = c.path.Select(V3).ToArray();
            int edges = c.loop ? pts.Length : pts.Length - 1;
            var gaps = c.bridges.Select(g => (at: V3(g), size: Chasm.BridgeGap, wall: false))
                .Concat(c.walls.Select(g => (at: V3(g), size: piece * Chasm.WallPieces, wall: true))).ToList();

            for (int e = 0; e < edges; e++)
            {
                Vector3 a = pts[e], b = pts[(e + 1) % pts.Length], dir = (b - a).normalized;
                float len = Vector3.Distance(a, b);
                var cuts = new List<(float from, float to)>();
                foreach (var g in gaps)
                {
                    float t = Vector3.Dot(g.at - a, dir);
                    float off = Vector3.Distance(a + dir * t, g.at);
                    if (off < c.width && t > -g.size && t < len + g.size) cuts.Add((t - g.size / 2f, t + g.size / 2f));
                }
                cuts.Sort((x, y) => x.from.CompareTo(y.from));
                float start = 0f;
                foreach (var (from, to) in cuts.Append((len, len)))
                {
                    float end = Mathf.Min(from, len);
                    float s = start <= 0f ? -0.6f : start, f = end >= len && from >= len ? len + 0.6f : end;
                    if (f - s > 0.5f) Piece(root.transform, nav.transform, a + dir * s, a + dir * f, c.width, voidMat, depthMat, edgeMat, streamMat, clear);
                    start = Mathf.Max(start, to);
                }
            }

            foreach (var g in gaps)
            {
                Vector3 along = Vector3.right; float best = float.MaxValue;
                for (int e = 0; e < edges; e++)
                {
                    Vector3 a = pts[e], b = pts[(e + 1) % pts.Length];
                    Vector3 p = a + Vector3.Project(g.at - a, b - a);
                    float d = Vector3.Distance(p, g.at);
                    if (d < best) { best = d; along = (b - a).normalized; }
                }
                var bridge = Bridge(root.transform, g.at, along, g.size, c.width, deckMat, railMat, postMat, g.wall);
                clear.Add((g.at, g.size / 2f + c.width / 2f + 6f));
                if (!g.wall) continue;
                for (int k = 0; k < Chasm.WallPieces; k++)
                {
                    var w = (GameObject)Object.Instantiate(gatePrefab, bridge);
                    w.name = $"Barrier_{++walls}";
                    w.transform.SetPositionAndRotation(g.at + along * (k - (Chasm.WallPieces - 1) / 2f) * piece, Quaternion.LookRotation(Vector3.Cross(along, Vector3.up)));
                    MakeBarrier(w);
                }
            }
        }
    }

    static void Piece(Transform root, Transform nav, Vector3 a, Vector3 b, float w, Material voidMat, Material depthMat, Material edgeMat, Material streamMat, List<(Vector3 at, float r)> clear)
    {
        float len = Vector3.Distance(a, b);
        var rot = Quaternion.LookRotation(b - a);
        var p = new GameObject("Chasm").transform;
        p.SetParent(root, false);
        p.SetPositionAndRotation((a + b) / 2f, rot);
        Flat(p, "Void", 0f, w, len, 0.09f, voidMat);
        Flat(p, "DepthL", -(w / 2f - 1.6f), 2.2f, len, 0.095f, depthMat);
        Flat(p, "DepthR", w / 2f - 1.6f, 2.2f, len, 0.095f, depthMat);
        Flat(p, "EdgeL", -(w / 2f - 0.25f), 0.5f, len, 0.1f, edgeMat);
        Flat(p, "EdgeR", w / 2f - 0.25f, 0.5f, len, 0.1f, edgeMat);
        Flat(p, "Stream", 0f, 0.3f, len, 0.1f, streamMat);
        var col = p.gameObject.AddComponent<BoxCollider>();
        col.size = new Vector3(w, 0.2f, len);
        col.center = new Vector3(0f, 0.1f, 0f);

        var v = new GameObject("NotWalkable").AddComponent<NavMeshModifierVolume>();
        v.transform.SetParent(nav, false);
        v.transform.SetPositionAndRotation(p.position, rot);
        v.size = new Vector3(w, 6f, len);
        v.center = Vector3.zero;
        v.area = 1;

        for (float t = 0f; t <= len; t += 8f) clear.Add((a + (b - a).normalized * t, w / 2f + 5f));
    }

    static Transform Bridge(Transform root, Vector3 at, Vector3 along, float gap, float w, Material deckMat, Material railMat, Material postMat, bool sealedOff)
    {
        var br = new GameObject(sealedOff ? "SealedBridge" : "Bridge").transform;
        br.SetParent(root, false);
        br.SetPositionAndRotation(at, Quaternion.LookRotation(Vector3.Cross(along, Vector3.up)));
        float span = w + 8f;
        Box(br, "Deck", new Vector3(0f, 0.1f, 0f), new Vector3(gap - 1f, 0.1f, span), deckMat, keepCollider: true);
        for (int s = -1; s <= 1; s += 2)
        {
            float x = s * (gap / 2f - 0.8f);
            Box(br, "Rail", new Vector3(x, 0.45f, 0f), new Vector3(0.3f, 0.6f, span), railMat);
            for (int e = -1; e <= 1; e += 2) Box(br, "Post", new Vector3(x, 1.1f, e * (w / 2f + 4f)), new Vector3(0.8f, 2.2f, 0.8f), postMat);
        }
        return br;
    }

    static void MakeBarrier(GameObject w)
    {
        Object.DestroyImmediate(w.GetComponent<Structure>());
        var so = new SerializedObject(w.GetComponent<Health>());
        so.FindProperty("team").intValue = Creep.Team;
        so.FindProperty("scenery").boolValue = true;
        so.FindProperty("maxHealth").floatValue = 2500f;
        so.ApplyModifiedPropertiesWithoutUndo();
        w.AddComponent<Barrier>();
        var ob = w.GetComponent<UnityEngine.AI.NavMeshObstacle>();
        ob.carving = true;
        ob.carveOnlyStationary = true;
    }

    static void Flat(Transform parent, string name, float x, float width, float len, float y, Material mat)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.name = name;
        q.transform.SetParent(parent, false);
        q.transform.localPosition = new Vector3(x, y, 0f);
        q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        q.transform.localScale = new Vector3(width, len, 1f);
        var r = q.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static void Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool keepCollider = false)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        if (!keepCollider) Object.DestroyImmediate(c.GetComponent<Collider>());
        c.name = name;
        c.transform.SetParent(parent, false);
        c.transform.localPosition = pos;
        c.transform.localScale = size;
        c.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static Material Mat(string name, Color color, Color emission, bool unlit = false)
    {
        string path = $"{FeatureArt}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        if (!AssetDatabase.IsValidFolder(FeatureArt)) AssetDatabase.CreateFolder("Assets/Art", "MapFeatures");
        mat = new Material(Shader.Find(unlit ? "Unlit/Color" : "Standard")) { color = color };
        if (!unlit && emission != Color.black)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void RenderPreview(GameMap m, UnityEngine.SceneManagement.Scene scene)
    {
        var hide = new[] { "Units", "CoreNexus", "AssemblyHive", "FogOfWar", "Creeps" }
            .Select(n => scene.GetRootGameObjects().FirstOrDefault(g => g.name == n)).Where(g => g && g.activeSelf).ToList();
        foreach (var g in hide) g.SetActive(false);

        var go = new GameObject("PreviewCam");
        var cam = go.AddComponent<Camera>();
        var main = Camera.main;
        cam.orthographic = true;
        cam.orthographicSize = m.Half;
        cam.transform.SetPositionAndRotation(new Vector3(0f, 3000f, 0f), Quaternion.Euler(90f, 0f, 0f));
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = main ? main.backgroundColor : Color.black;
        cam.nearClipPlane = 2800f; cam.farClipPlane = 3100f;
        const int Px = 1024;
        var rt = new RenderTexture(Px, Px, 24);
        cam.targetTexture = rt;
        bool fog = RenderSettings.fog;
        RenderSettings.fog = false;
        cam.Render();
        RenderSettings.fog = fog;
        RenderTexture.active = rt;
        var tex = new Texture2D(Px, Px, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Px, Px), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(rt);
        foreach (var g in hide) g.SetActive(true);

        string file = $"Assets/Resources/{m.preview}.png";
        File.WriteAllBytes(file, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(file);
        var ti = (TextureImporter)AssetImporter.GetAtPath(file);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.mipmapEnabled = false;
        ti.maxTextureSize = Px;
        ti.SaveAndReimport();
    }

    static void AddToBuild(string path)
    {
        var list = EditorBuildSettings.scenes.ToList();
        if (list.Any(s => s.path == path)) return;
        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    static Vector3 V3(Vector2 v) => new(v.x, 0f, v.y);
    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    static Quaternion Turn(Vector3 from, Vector3 to) => Quaternion.Euler(0f, Vector3.SignedAngle(Flat(from), Flat(to), Vector3.up), 0f);
}
