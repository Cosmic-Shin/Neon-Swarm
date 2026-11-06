using System.Collections.Generic;
using UnityEngine;

// Units rank up from kills. Ranks, kills needed and bonuses live in the VeterancyData asset
// (Resources/Veterancy). The bonuses go through Stats; the unit's neon glows brighter each rank
// (MaterialPropertyBlock, like UnitDeath's fade). Added to a unit the first time it scores a kill (Health.Die).
public class Veterancy : MonoBehaviour
{
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static VeterancyData Data => VeterancyData.Get;

    public int Kills { get; private set; }
    public int Rank { get; private set; }
    public IReadOnlyList<StatMod> Mods => Data.ranks[Rank].bonuses;
    public string RankName => Data.ranks[Rank].name;

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
        while (rank + 1 < Data.ranks.Length && Kills >= Data.ranks[rank + 1].kills) rank++;
        if (rank == Rank) return;
        Rank = rank;
        Glow();
        Stats.Refresh(); // max HP etc.
        if (announce && health != null && health.team == 0 && Data.promoted) Data.promoted.Raise(gameObject);
    }

    // Multiplayer: another player's unit; its owner counts the kills, we only show the glow.
    public void ShowRank(int rank)
    {
        if (rank == Rank) return;
        Rank = Mathf.Clamp(rank, 0, Data.ranks.Length - 1);
        Glow();
    }

    void Glow()
    {
        float k = 1f + Data.glowPerRank * Rank;
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
        float repair = Data.ranks[Rank].selfRepair;
        if (repair > 0f && health != null && health.enabled && health.IsDamaged)
            health.Heal(repair * health.MaxHealth * Time.deltaTime);
    }
}
