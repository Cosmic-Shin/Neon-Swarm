using System.Collections.Generic;
using UnityEngine;

// Fog of war: the map starts black, turns dim once seen, and is clear only near your units and buildings.
// Enemies outside your sight are moved to a layer the camera doesn't draw (which also stops clicks on them).
public class FogOfWar : MonoBehaviour
{
    public static FogOfWar Instance { get; private set; }
    public static bool RevealAll; // spectating after defeat: the whole map, every unit

    [SerializeField] Renderer fogPlane;      // map-sized quad with the RTS/FogOfWar shader
    [SerializeField] float mapSize = 600f;   // centred on 0
    [SerializeField] int resolution = 150;   // cells per side (4 m each)
    [SerializeField] float interval = 0.1f;  // seconds between updates
    [SerializeField] string hiddenLayer = "FogHidden";

    [Header("Sight (m)")]
    [SerializeField] float unitSight = 20f;      // at least; long-range units see range + 8
    [SerializeField] float buildingSight = 22f;  // past the building's edge

    public Texture2D MinimapTexture { get; private set; } // fog colour with alpha, for the minimap overlay

    Texture2D tex;
    Color32[] px, miniPx;
    bool[] visible, explored;
    int hidden;
    float next;
    readonly Dictionary<Health, List<(Transform t, int layer)>> hiddenNow = new(); // hidden enemy -> layers to restore
    readonly List<Health> stale = new();

    void Awake()
    {
        Instance = this;
        mapSize = GameMaps.Of(gameObject.scene).size; // the plane covers the whole map
        fogPlane.transform.localScale = new Vector3(mapSize, mapSize, fogPlane.transform.localScale.z);
        RevealAll = false;
        hidden = LayerMask.NameToLayer(hiddenLayer);
        int n = resolution * resolution;
        visible = new bool[n];
        explored = new bool[n];
        px = new Color32[n];
        miniPx = new Color32[n];
        softVisible = new float[n]; softExplored = new float[n]; blurTmp = new float[n]; blurOut = new float[n];
        tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        MinimapTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        fogPlane.material.SetTexture("_FogTex", tex);
        fogPlane.material.SetFloat("_MapSize", mapSize);
        Recompute();
    }

    void LateUpdate()
    {
        if (Time.time < next) return;
        next = Time.time + interval;
        Recompute();
    }

    public bool IsVisible(Vector3 p) => Cell(p, out int i) && visible[i];
    public bool IsExplored(Vector3 p) => Cell(p, out int i) && explored[i];

    bool Cell(Vector3 p, out int index)
    {
        int x = Mathf.FloorToInt((p.x / mapSize + 0.5f) * resolution);
        int z = Mathf.FloorToInt((p.z / mapSize + 0.5f) * resolution);
        index = z * resolution + x;
        return x >= 0 && z >= 0 && x < resolution && z < resolution;
    }

    void Recompute()
    {
        if (RevealAll) { System.Array.Fill(visible, true); System.Array.Fill(explored, true); }
        else
        {
            System.Array.Clear(visible, 0, visible.Length);
            foreach (var h in Health.All)
                if (Net.Allied(h.team, 0)) Stamp(h.transform.position, Sight(h));
        }

        // The screen gets a softened copy: on/off cells blurred so the edge is a smooth curve, not 4 m stair
        // steps, and eased towards the new sight so it glides instead of jumping a cell at a time.
        // (IsVisible and enemy hiding still use the sharp cells.)
        Blur(visible, softVisible, ease: 0.55f);
        Blur(explored, softExplored, ease: 1f);
        Color32 clear = new(0, 0, 0, 0), dim = new(5, 3, 12, 120), dark = new(5, 3, 12, 190); // minimap: the map picture shows faintly through
        for (int i = 0; i < px.Length; i++)
        {
            px[i] = new Color32((byte)(softVisible[i] * 255f), (byte)(softExplored[i] * 255f), 0, 255);
            miniPx[i] = visible[i] ? clear : explored[i] ? dim : dark;
        }
        tex.SetPixels32(px);
        tex.Apply(false);
        MinimapTexture.SetPixels32(miniPx);
        MinimapTexture.Apply(false);

        // Hide enemies we can't see. Outposts are landmarks: always drawn (the fog dims them).
        foreach (var h in Health.All)
            if (!Net.Allied(h.team, 0) && !h.TryGetComponent(out OutpostCapture _)) SetHidden(h, !IsVisible(h.transform.position));

        // Last-seen buildings: gone once we look at the spot again (whether the building is still there or not).
        for (int i = ghosts.Count - 1; i >= 0; i--)
            if (ghosts[i].go == null || IsVisible(ghosts[i].go.transform.position))
            {
                if (ghosts[i].go != null) Destroy(ghosts[i].go);
                ghosts.RemoveAt(i);
            }

        // Forget ones that died or left (they leave Health.All).
        stale.Clear();
        foreach (var h in hiddenNow.Keys) if (h == null || !h.isActiveAndEnabled) stale.Add(h);
        foreach (var h in stale) { hiddenNow.Remove(h); seen.Remove(h); }
    }

    float[] softVisible, softExplored, blurTmp, blurOut;
    const int BlurRadius = 2; // cells (8 m of soft edge)

    // Box blur (rows then columns) of the on/off cells into `soft`, eased by `ease` (1 = jump straight there).
    void Blur(bool[] cells, float[] soft, float ease)
    {
        int r = resolution, w = BlurRadius * 2 + 1;
        for (int z = 0; z < r; z++)
        {
            int sum = 0, row = z * r;
            for (int k = -BlurRadius; k <= BlurRadius; k++) sum += cells[row + Mathf.Clamp(k, 0, r - 1)] ? 1 : 0;
            for (int x = 0; x < r; x++)
            {
                blurTmp[row + x] = sum;
                sum += (cells[row + Mathf.Min(x + BlurRadius + 1, r - 1)] ? 1 : 0) - (cells[row + Mathf.Max(x - BlurRadius, 0)] ? 1 : 0);
            }
        }
        for (int x = 0; x < r; x++)
        {
            float sum = 0f;
            for (int k = -BlurRadius; k <= BlurRadius; k++) sum += blurTmp[Mathf.Clamp(k, 0, r - 1) * r + x];
            for (int z = 0; z < r; z++)
            {
                blurOut[z * r + x] = sum / (w * w);
                sum += blurTmp[Mathf.Min(z + BlurRadius + 1, r - 1) * r + x] - blurTmp[Mathf.Max(z - BlurRadius, 0) * r + x];
            }
        }
        for (int i = 0; i < soft.Length; i++) soft[i] = Mathf.Lerp(soft[i], blurOut[i], ease);
    }

    float Sight(Health h)
    {
        if (h.TryGetComponent(out SelectableUnit u)) return Stats.Value(Stat.Sight, Mathf.Max(unitSight, u.Data.range + 8f, u.Data.sight), u);
        float reach = buildingSight;
        if (h.TryGetComponent(out Turret t)) reach = Mathf.Max(reach, t.Range + 6f);
        if (h.TryGetComponent(out Structure s) && s.sight > 0f && !Structure.LowPower) reach = Mathf.Max(reach, s.sight); // Overwatch Spire
        reach = Stats.Value(Stat.Sight, reach, h); // Uplink Mk II, Hive Mind Uplink
        if (h.TryGetComponent(out Collider c)) reach += Mathf.Max(c.bounds.extents.x, c.bounds.extents.z);
        return reach;
    }

    void Stamp(Vector3 p, float radius)
    {
        float cell = mapSize / resolution;
        int cx = Mathf.FloorToInt((p.x / mapSize + 0.5f) * resolution);
        int cz = Mathf.FloorToInt((p.z / mapSize + 0.5f) * resolution);
        int r = Mathf.CeilToInt(radius / cell);
        float r2 = (radius / cell) * (radius / cell);
        for (int z = Mathf.Max(0, cz - r); z <= Mathf.Min(resolution - 1, cz + r); z++)
            for (int x = Mathf.Max(0, cx - r); x <= Mathf.Min(resolution - 1, cx + r); x++)
            {
                if ((x - cx) * (x - cx) + (z - cz) * (z - cz) > r2) continue;
                int i = z * resolution + x;
                visible[i] = explored[i] = true;
            }
    }

    // ---- enemy buildings remembered under the fog (C&C shroud memory) ----
    readonly HashSet<Health> seen = new();                        // enemies we've had in sight at least once
    readonly List<(GameObject go, int team)> ghosts = new();       // frozen copies where they were last seen
    public IReadOnlyList<(GameObject go, int team)> Ghosts => ghosts; // the minimap draws these

    // A copy of the building's meshes as they look right now: no scripts, no colliders, can't be clicked.
    void MakeGhost(Health h)
    {
        var root = new GameObject("LastSeen_" + h.name);
        root.transform.position = h.transform.position;
        foreach (var mr in h.GetComponentsInChildren<MeshRenderer>())
        {
            if (!mr.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null) continue;
            var part = new GameObject(mr.name);
            part.transform.SetParent(root.transform, false);
            part.transform.SetPositionAndRotation(mr.transform.position, mr.transform.rotation);
            part.transform.localScale = mr.transform.lossyScale;
            part.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = part.AddComponent<MeshRenderer>();
            r.sharedMaterials = mr.sharedMaterials;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        ghosts.Add((root, h.team));
    }

    void SetHidden(Health h, bool hide)
    {
        bool isHidden = hiddenNow.TryGetValue(h, out var saved);
        if (!hide) seen.Add(h);
        if (hide == isHidden) return;
        if (hide)
        {
            if (seen.Contains(h) && h.TryGetComponent(out Structure _)) MakeGhost(h); // walked out of sight of it
            saved = new List<(Transform, int)>();
            foreach (var t in h.GetComponentsInChildren<Transform>(true)) { saved.Add((t, t.gameObject.layer)); t.gameObject.layer = hidden; }
            hiddenNow[h] = saved;
        }
        else
        {
            foreach (var (t, layer) in saved) if (t != null) t.gameObject.layer = layer;
            hiddenNow.Remove(h);
        }
    }
}
