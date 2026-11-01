using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Death handling for the Cyberpunk RTS kit.
///  BreakApart        - small/medium units: child meshes become physics debris.
///  AnimateThenWreck  - heavy/elite units: plays the "Die" trigger, then swaps to the wreck prefab.
///  SwapToWreck       - buildings: short shake + sink, then swaps to the wreck prefab.
/// All modes flicker and fade the neon emission to black. Call Die() when HP hits 0.
/// </summary>
public class UnitDeath : MonoBehaviour
{
    public enum DeathMode { BreakApart, AnimateThenWreck, SwapToWreck }

    [Header("Mode")]
    public DeathMode mode = DeathMode.BreakApart;

    [Header("Lights out")]
    public float emissionFadeTime = 0.5f;
    public bool flickerFirst = true;

    [Header("Death FX (optional)")]
    public GameObject[] spawnOnDeath;
    public Behaviour[] disableOnDeath;

    [Header("Break apart (small / medium units)")]
    public float explosionForce = 6f;
    public float explosionRadius = 1.5f;
    public float upwardsModifier = 0.6f;
    public float randomTorque = 4f;
    public float debrisLifetime = 3f;
    public float debrisShrinkTime = 0.6f;
    public int maxPhysicsPieces = 24;
    public string debrisLayer = "";

    [Header("Animate / swap to wreck")]
    public Animator animator;
    public string deathTrigger = "Die";
    public float deathClipLength = 1.67f;
    public GameObject wreckPrefab;
    public float wreckLifetime = 30f;
    public float sinkTime = 0.4f;
    public float sinkDepth = 0.4f;

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    bool _dead;
    public bool IsDead => _dead;

    public void Die()
    {
        if (_dead) return;
        _dead = true;
        foreach (var b in disableOnDeath) if (b) b.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (var fx in spawnOnDeath) if (fx) Instantiate(fx, transform.position, Quaternion.identity);
        StartCoroutine(FadeEmission(GetComponentsInChildren<Renderer>()));
        switch (mode)
        {
            case DeathMode.BreakApart: BreakApart(); break;
            case DeathMode.AnimateThenWreck: StartCoroutine(AnimateThenWreck()); break;
            case DeathMode.SwapToWreck: StartCoroutine(SinkThenWreck()); break;
        }
    }

    IEnumerator FadeEmission(Renderer[] renderers)
    {
        var entries = new List<(Renderer r, int slot, Color c)>();
        foreach (var r in renderers)
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] && mats[i].HasProperty(EmissionId))
                {
                    Color c = mats[i].GetColor(EmissionId);
                    if (c.maxColorComponent > 0.001f) entries.Add((r, i, c));
                }
        }
        if (entries.Count == 0) yield break;
        var block = new MaterialPropertyBlock();
        void Apply(float k)
        {
            foreach (var e in entries)
            {
                if (!e.r) continue;
                e.r.GetPropertyBlock(block, e.slot);
                block.SetColor(EmissionId, e.c * k);
                e.r.SetPropertyBlock(block, e.slot);
            }
        }
        if (flickerFirst)
        {
            float[] pattern = { 0.2f, 1.2f, 0.1f, 0.8f };
            foreach (var k in pattern) { Apply(k); yield return new WaitForSeconds(0.04f); }
        }
        for (float t = 0; t < emissionFadeTime; t += Time.deltaTime)
        {
            Apply(1f - t / emissionFadeTime);
            yield return null;
        }
        Apply(0f);
    }

    void BreakApart()
    {
        var pieces = new List<MeshRenderer>(GetComponentsInChildren<MeshRenderer>());
        pieces.Sort((a, b) => b.bounds.size.sqrMagnitude.CompareTo(a.bounds.size.sqrMagnitude));
        int layer = string.IsNullOrEmpty(debrisLayer) ? -1 : LayerMask.NameToLayer(debrisLayer);
        Vector3 origin = transform.position + Vector3.down * 0.1f;
        for (int i = 0; i < pieces.Count; i++)
        {
            var mr = pieces[i];
            var go = mr.gameObject;
            go.transform.SetParent(null, true);
            if (layer >= 0) go.layer = layer;
            if (i < maxPhysicsPieces)
            {
                go.AddComponent<BoxCollider>();
                var rb = go.AddComponent<Rigidbody>();
                Vector3 s = mr.bounds.size;
                rb.mass = Mathf.Clamp(s.x * s.y * s.z * 50f, 0.05f, 5f);
                rb.AddExplosionForce(explosionForce, origin, explosionRadius, upwardsModifier, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * randomTorque, ForceMode.Impulse);
            }
            go.AddComponent<DebrisPiece>().Begin(i < maxPhysicsPieces ? debrisLifetime : 0.05f, debrisShrinkTime);
        }
        Destroy(gameObject);
    }

    IEnumerator AnimateThenWreck()
    {
        if (animator) animator.SetTrigger(deathTrigger);
        yield return new WaitForSeconds(deathClipLength);
        SpawnWreck();
        Destroy(gameObject);
    }

    IEnumerator SinkThenWreck()
    {
        Vector3 start = transform.position;
        for (float t = 0; t < sinkTime; t += Time.deltaTime)
        {
            float k = t / sinkTime;
            transform.position = start + Vector3.down * sinkDepth * k * k + Random.insideUnitSphere * 0.03f * (1 - k);
            yield return null;
        }
        transform.position = start;
        SpawnWreck();
        Destroy(gameObject);
    }

    void SpawnWreck()
    {
        if (!wreckPrefab) return;
        var w = Instantiate(wreckPrefab, transform.position, transform.rotation);
        if (wreckLifetime > 0) Destroy(w, wreckLifetime);
    }
}

public class DebrisPiece : MonoBehaviour
{
    public void Begin(float lifetime, float shrinkTime) => StartCoroutine(Run(lifetime, shrinkTime));
    IEnumerator Run(float lifetime, float shrinkTime)
    {
        yield return new WaitForSeconds(lifetime);
        Vector3 s0 = transform.localScale;
        for (float t = 0; t < shrinkTime; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(s0, Vector3.zero, t / shrinkTime);
            yield return null;
        }
        Destroy(gameObject);
    }
}
