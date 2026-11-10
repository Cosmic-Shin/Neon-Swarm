using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// In-place building upgrades (Mk II / Mk III).
/// The model carries hidden add-on groups named "*_Upgrade_Mk2" and "*_Upgrade_Mk3";
/// this turns them on as the building's level rises. Call Upgrade() when the player buys it,
/// or SetLevel() when loading a save.
/// </summary>
public class BuildingUpgrade : MonoBehaviour
{
    [Tooltip("1 = base, 2 = Mk II, 3 = Mk III")]
    [Range(1, 3)] public int level = 1;
    public int maxLevel = 2;
    public int[] upgradeCost = { 0, 0, 1000, 2000 };        // index = target level
    public float[] upgradeTime = { 0, 0, 45f, 90f };         // seconds
    [Tooltip("Small pop when an add-on appears.")]
    public float revealTime = 0.35f;

    public bool IsUpgrading { get; private set; }
    public float Progress { get; private set; }               // 0..1 while upgrading
    public event System.Action<int> OnLevelChanged;

    readonly Dictionary<int, List<Transform>> groups = new Dictionary<int, List<Transform>>();

    void Awake()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name.EndsWith("_Upgrade_Mk2")) Add(2, t);
            else if (t.name.EndsWith("_Upgrade_Mk3")) Add(3, t);
        }
        maxLevel = groups.ContainsKey(3) ? 3 : groups.ContainsKey(2) ? 2 : 1;
        Apply(false);
        if (maxLevel > 1) gameObject.AddComponent<UpgradeFX>(); // GD5: the look while upgrading
    }

    // GD5: the add-on pieces that appear at this level (hidden until then), for the upgrade hologram.
    public IReadOnlyList<Transform> PartsFor(int lvl) => groups.TryGetValue(lvl, out var list) ? list : (IReadOnlyList<Transform>)System.Array.Empty<Transform>();

    void Add(int lvl, Transform t)
    {
        if (!groups.TryGetValue(lvl, out var list)) groups[lvl] = list = new List<Transform>();
        list.Add(t);
    }

    public bool CanUpgrade => !IsUpgrading && level < maxLevel;
    public int NextCost => level + 1 < upgradeCost.Length ? upgradeCost[level + 1] : 0;

    /// <summary>Start the next upgrade. Pays from the ResourceBank if one exists.</summary>
    public bool Upgrade()
    {
        int team = TeamEconomy.TeamOf(this); // GD5: single-player AIs upgrade with their own credits
        if (!CanUpgrade || !BuildSlots.FreeFor(team)) return false; // GD5: max 2 buildings building/upgrading at once
        if (!TeamEconomy.Spend(team, NextCost)) return false;
        StartCoroutine(Run(level + 1));
        return true;
    }

    System.Collections.IEnumerator Run(int target)
    {
        IsUpgrading = true; Progress = 0f;
        BuildSlots.Busy.Add(this);
        float dur = target < upgradeTime.Length ? upgradeTime[target] : 30f;
        for (float t = 0; t < dur; t += Time.deltaTime) { Progress = t / dur; yield return null; }
        IsUpgrading = false; Progress = 1f;
        BuildSlots.Busy.Remove(this);
        SetLevel(target, true);
    }

    /// <summary>Set the level directly (e.g. from a save).</summary>
    public void SetLevel(int newLevel, bool animate = false)
    {
        level = Mathf.Clamp(newLevel, 1, maxLevel);
        Apply(animate);
        OnLevelChanged?.Invoke(level);
    }

    void Apply(bool animate)
    {
        foreach (var kv in groups)
            foreach (var g in kv.Value)
            {
                bool on = level >= kv.Key;
                bool wasOn = g.gameObject.activeSelf;
                g.gameObject.SetActive(on);
                if (on && !wasOn && animate) StartCoroutine(Pop(g));
            }
    }

    System.Collections.IEnumerator Pop(Transform g)
    {
        Vector3 s = g.localScale;
        for (float t = 0; t < revealTime; t += Time.deltaTime)
        {
            float k = t / revealTime;
            g.localScale = s * Mathf.LerpUnclamped(0.6f, 1f, 1f - Mathf.Pow(1f - k, 3f) * (1f - 1.7f * k));
            yield return null;
        }
        g.localScale = s;
    }
}
