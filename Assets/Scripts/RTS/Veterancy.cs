using System.Collections.Generic;
using UnityEngine;

// Units rank up from kills: Veteran at 3, Elite at 8, Heroic at 15. The bonuses go through Stats;
// the unit's neon glows brighter each rank (MaterialPropertyBlock, like UnitDeath's fade).
// Added to a unit the first time it scores a kill (Health.Die).
public class Veterancy : MonoBehaviour
{
    public static readonly string[] RankNames = { "Rookie", "Veteran", "Elite", "Heroic" };
    static readonly int[] KillsFor = { 0, 3, 8, 15 };
    static readonly StatMod[][] RankMods =
    {
        new StatMod[0],
        new[] { Mod(Stat.Damage, 0.1f), Mod(Stat.MaxHealth, 0.1f) },
        new[] { Mod(Stat.Damage, 0.2f), Mod(Stat.MaxHealth, 0.2f) },
        new[] { Mod(Stat.Damage, 0.3f), Mod(Stat.MaxHealth, 0.3f), Mod(Stat.FireRate, 0.1f) },
    };
    const float SelfRepair = 0.01f;  // of max HP per second, Elite and Heroic
    const float GlowPerRank = 0.4f;  // emission x1.4 / x1.8 / x2.2

    static StatMod Mod(Stat s, float p) => new() { stat = s, percent = p };
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    public int Kills { get; private set; }
    public int Rank { get; private set; }
    public IReadOnlyList<StatMod> Mods => RankMods[Rank];

    Health health;
    readonly List<(Renderer r, int slot, Color c)> glow = new();
    MaterialPropertyBlock block;

    void Awake()
    {
        health = GetComponent<Health>();
        block = new MaterialPropertyBlock();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is LineRenderer) continue; // lasers, rings
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] && mats[i].HasProperty(EmissionId) && mats[i].IsKeywordEnabled("_EMISSION"))
                {
                    Color c = mats[i].GetColor(EmissionId);
                    if (c.maxColorComponent > 0.001f) glow.Add((r, i, c));
                }
        }
    }

    public void AddKill() => SetKills(Kills + 1);

    // Saves call this directly (quietly).
    public void SetKills(int kills, bool announce = true)
    {
        Kills = kills;
        int rank = 0;
        while (rank + 1 < KillsFor.Length && Kills >= KillsFor[rank + 1]) rank++;
        if (rank == Rank) return;
        Rank = rank;
        Glow();
        Stats.Refresh(); // max HP etc.
        if (announce && health != null && health.team == 0) Alerts.Show($"Unit promoted: {RankNames[Rank]}", Alerts.Good);
    }

    // Multiplayer: another player's unit; its owner counts the kills, we only show the glow.
    public void ShowRank(int rank)
    {
        if (rank == Rank) return;
        Rank = Mathf.Clamp(rank, 0, RankNames.Length - 1);
        Glow();
    }

    void Glow()
    {
        float k = 1f + GlowPerRank * Rank;
        foreach (var (r, slot, c) in glow)
        {
            if (!r) continue;
            r.GetPropertyBlock(block, slot);
            block.SetColor(EmissionId, c * k);
            r.SetPropertyBlock(block, slot);
        }
    }

    void Update()
    {
        if (Rank >= 2 && health != null && health.enabled && health.IsDamaged)
            health.Heal(SelfRepair * health.MaxHealth * Time.deltaTime);
    }
}
