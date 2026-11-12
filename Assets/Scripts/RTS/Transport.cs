using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Flyer))]
public class Transport : MonoBehaviour
{
    public int capacity = 5;
    public GameObject beamPrefab;
    const float Below = 60f, LoadRange = 3.5f, DropHover = 2f;

    readonly List<SelectableUnit> carried = new();
    public int Carried => carried.Count;
    readonly HashSet<SelectableUnit> inside = new();
    public int Count => carried.Count;
    public bool Full => carried.Count >= capacity;
    public bool Busy { get; private set; }

    SelectableUnit self;
    Flyer flyer;
    Transform beam;
    float beamUntil;
    Health health;

    public static bool CanCarry(SelectableUnit u) =>
        u != null && !Flyer.IsAir(u) && u.Data.role is UnitData.Role.Infantry or UnitData.Role.Support or UnitData.Role.Scout;

    void Awake()
    {
        self = GetComponent<SelectableUnit>();
        flyer = GetComponent<Flyer>();
        health = GetComponent<Health>();
        if (beamPrefab != null)
        {
            beam = Instantiate(beamPrefab, transform).transform;
            beam.name = "TractorBeam";
            beam.gameObject.SetActive(false);
        }
    }

    public bool InReach(SelectableUnit u)
    {
        Vector3 d = u.transform.position - transform.position; d.y = 0f;
        return d.magnitude <= LoadRange + flyer.size;
    }

    public bool TryLoad(SelectableUnit u)
    {
        if (Full || Busy || !CanCarry(u) || carried.Contains(u) || health.IsDead) return false;
        carried.Add(u);
        u.Deselect();
        StartCoroutine(BeamUp(u));
        return true;
    }

    IEnumerator BeamUp(SelectableUnit u)
    {
        Sfx.At(Snd.Load, transform.position, 0.6f);
        Stow(u);
        Color c = ProductionFX.HoloColor(gameObject);
        Juice.Ring(u.transform.position + Vector3.up * 0.2f, c, 0.5f, 2.5f, 0.4f, 0.2f);
        beamUntil = Time.time + 1f;
        Vector3 from = u.transform.position, scale = u.transform.localScale;
        for (float t = 0f; t < 0.6f && u != null; t += Time.deltaTime)
        {
            float k = t / 0.6f;
            u.transform.position = Vector3.Lerp(from, Flyer.Center(this), k * k);
            u.transform.localScale = scale * (1f - 0.8f * k);
            yield return null;
        }
        if (u == null) yield break;
        u.transform.localScale = scale;
        inside.Add(u);
        Alerts.Show($"Wasp loaded {carried.Count}/{capacity}", Alerts.Info);
    }

    static void Stow(SelectableUnit u)
    {
        if (u.TryGetComponent(out NavMeshAgent a)) a.enabled = false;
        if (u.TryGetComponent(out Health h)) h.enabled = false;
        if (u.TryGetComponent(out Collider col)) col.enabled = false;
        if (u.TryGetComponent(out Attacker at)) { at.Stop(); at.enabled = false; }
    }

    static void Unstow(SelectableUnit u, Vector3 at)
    {
        u.transform.position = at;
        if (u.TryGetComponent(out Collider col)) col.enabled = true;
        if (u.TryGetComponent(out Health h)) h.enabled = true;
        if (u.TryGetComponent(out NavMeshAgent a)) { a.enabled = true; a.Warp(at); }
        if (u.TryGetComponent(out Attacker atk)) atk.enabled = true;
    }

    public void Unload()
    {
        if (!Busy && carried.Count > 0) StartCoroutine(Drop());
    }

    IEnumerator Drop()
    {
        Sfx.At(Snd.Unload, transform.position, 0.6f);
        Busy = true;
        var agent = GetComponent<NavMeshAgent>();
        if (agent.isOnNavMesh) agent.ResetPath();
        float high = flyer.hover;
        for (float t = 0f; t < 0.6f; t += Time.deltaTime) { flyer.hover = Mathf.Lerp(high, DropHover, t / 0.6f); yield return null; }
        flyer.hover = DropHover;
        Color c = ProductionFX.HoloColor(gameObject);
        int n = 0;
        while (carried.Count > 0)
        {
            var u = carried[0];
            carried.RemoveAt(0);
            inside.Remove(u);
            if (u == null) continue;
            Vector3 spot = transform.position + Quaternion.Euler(0f, n++ * 72f, 0f) * Vector3.forward * (1.5f + flyer.size * 0.5f);
            if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 20f, NavMesh.AllAreas)) spot = hit.position;
            Unstow(u, spot);
            beamUntil = Time.time + 0.6f;
            Juice.Ring(spot + Vector3.up * 0.2f, c, 0.5f, 3f, 0.4f, 0.25f);
            Juice.Burst(spot + Vector3.up * 0.5f, c, 12, 4f, 0.12f, 0.5f);
            yield return new WaitForSeconds(0.25f);
        }
        for (float t = 0f; t < 0.8f; t += Time.deltaTime) { flyer.hover = Mathf.Lerp(DropHover, high, t / 0.8f); yield return null; }
        flyer.hover = high;
        Busy = false;
    }

    void Update()
    {
        if (health.IsDead) { SinkWithShip(); return; }
        foreach (var u in inside) if (u != null) u.transform.position = transform.position - Vector3.up * Below;
        if (beam != null)
        {
            bool on = Time.time < beamUntil;
            if (beam.gameObject.activeSelf != on) beam.gameObject.SetActive(on);
            if (on)
            {
                float h = Flyer.HeightOf(this);
                beam.localPosition = Vector3.up * h;
                beam.localScale = new Vector3(flyer.size * 2f, h, flyer.size * 2f);
            }
        }
        if (self != null && self.IsSelected && carried.Count > 0 && Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame) Unload();
    }

    void SinkWithShip()
    {
        if (beam != null) beam.gameObject.SetActive(false);
        inside.Clear();
        foreach (var u in carried)
            if (u != null) { Unstow(u, transform.position); if (u.TryGetComponent(out Health h)) h.TakeDamage(1e6f); }
        carried.Clear();
    }
}
