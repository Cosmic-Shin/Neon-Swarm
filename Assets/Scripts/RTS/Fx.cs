using System.Collections.Generic;
using UnityEngine;

// Short-lived effect (shockwave, flash, scorch, bolt...): scales from scaleFrom to scaleTo over its lifetime,
// fades its glow near the end, then goes back to the pool. Spawn through Fx.Spawn so effects are reused.
public class Fx : MonoBehaviour
{
    [SerializeField] float lifetime = 0.5f;
    [SerializeField] float scaleFrom = 1f;
    [SerializeField] float scaleTo = 1f;
    [SerializeField, Range(0f, 1f)] float fadeStart = 0.5f; // fraction of the lifetime when fading begins
    [SerializeField] bool randomRoll;                         // spin a random amount around its forward axis (flashes)

    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    Vector3 baseScale, size = Vector3.one;
    float age;
    Renderer[] renderers;
    Color[] baseColor, baseEmission;
    MaterialPropertyBlock block;

    void Awake()
    {
        baseScale = transform.localScale;
        renderers = GetComponentsInChildren<Renderer>(true);
        baseColor = new Color[renderers.Length];
        baseEmission = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i].sharedMaterial;
            baseColor[i] = m != null && m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white;
            baseEmission[i] = m != null && m.HasProperty(EmissionId) ? m.GetColor(EmissionId) : Color.black;
        }
        block = new MaterialPropertyBlock();
    }

    // size: multiplies the prefab's scale (e.g. blast radius, or (1,1,length) for a bolt).
    public static Fx Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 size)
    {
        if (prefab == null) return null;
        var fx = FxPool.Spawn(prefab, position, rotation).GetComponent<Fx>();
        if (fx == null) return null;
        fx.size = size;
        fx.age = 0f;
        if (fx.randomRoll) fx.transform.rotation = rotation * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        fx.Apply(0f);
        return fx;
    }

    public static Fx Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float size = 1f) =>
        Spawn(prefab, position, rotation, Vector3.one * size);

    void Update()
    {
        age += Time.deltaTime;
        float k = age / lifetime;
        if (k >= 1f) { FxPool.Despawn(gameObject); return; }
        Apply(k);
    }

    void Apply(float k)
    {
        transform.localScale = Vector3.Scale(baseScale, size) * Mathf.Lerp(scaleFrom, scaleTo, k);
        float fade = k < fadeStart ? 1f : 1f - (k - fadeStart) / (1f - fadeStart);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is ParticleSystemRenderer) continue;
            renderers[i].GetPropertyBlock(block);
            var c = baseColor[i]; c.a *= fade;
            block.SetColor(ColorId, c);
            block.SetColor(EmissionId, baseEmission[i] * fade);
            renderers[i].SetPropertyBlock(block);
        }
    }
}

// Reuses effect and projectile objects instead of creating and destroying them all the time.
public static class FxPool
{
    static readonly Dictionary<GameObject, Stack<GameObject>> free = new(); // prefab -> parked copies
    static readonly Dictionary<GameObject, GameObject> prefabOf = new();    // copy -> prefab
    static Transform root;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { free.Clear(); prefabOf.Clear(); root = null; }

    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (root == null) root = new GameObject("FxPool").transform;
        GameObject go = null;
        if (free.TryGetValue(prefab, out var stack))
            while (stack.Count > 0 && go == null) go = stack.Pop(); // skip any destroyed with the scene
        if (go == null)
        {
            go = Object.Instantiate(prefab, root);
            prefabOf[go] = prefab;
        }
        go.transform.SetPositionAndRotation(position, rotation);
        go.SetActive(true);
        return go;
    }

    public static void Despawn(GameObject go)
    {
        if (!prefabOf.TryGetValue(go, out var prefab)) { Object.Destroy(go); return; } // not from the pool
        go.SetActive(false);
        if (!free.TryGetValue(prefab, out var stack)) free[prefab] = stack = new Stack<GameObject>();
        stack.Push(go);
    }
}
