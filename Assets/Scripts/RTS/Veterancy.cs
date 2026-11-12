using System.Collections.Generic;
using UnityEngine;

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
            if (r is LineRenderer) continue;
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

    public void SetKills(int kills, bool announce = true)
    {
        Kills = kills;
        int rank = 0;
        while (rank + 1 < Data.ranks.Length && Kills >= Data.ranks[rank + 1].kills) rank++;
        if (rank == Rank) return;
        bool up = rank > Rank;
        Rank = rank;
        Glow();
        if (up && announce) Juice.Promoted(gameObject, RankName);
        Stats.Refresh();
        if (announce && health != null && health.team == 0 && Data.promoted) Data.promoted.Raise(gameObject);
    }

    public void ShowRank(int rank)
    {
        if (rank == Rank) return;
        bool up = rank > Rank;
        Rank = Mathf.Clamp(rank, 0, Data.ranks.Length - 1);
        Glow();
        if (up) Juice.Promoted(gameObject, RankName);
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

    readonly List<LineRenderer> chevrons = new();
    float top = -1f;

    void LateUpdate()
    {
        int n = health != null && health.enabled ? Mathf.Min(Rank, 3) : 0;
        while (chevrons.Count < n)
        {
            var lr = new GameObject("RankChevron").AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.sharedMaterial = Resources.Load<Material>("HarvestFX");
            lr.positionCount = 3;
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.14f;
            lr.startColor = lr.endColor = Juice.Gold;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            chevrons.Add(lr);
        }
        if (chevrons.Count == 0) return;
        if (top < 0f) top = ConstructionSite.RendererBounds(gameObject).max.y - transform.position.y;
        var cam = Camera.main;
        Vector3 right = cam != null ? cam.transform.right : Vector3.right;
        Vector3 basePos = transform.position + Vector3.up * (top + 0.8f + Mathf.Sin(Time.time * 2f) * 0.1f);
        for (int i = 0; i < chevrons.Count; i++)
        {
            var lr = chevrons[i];
            lr.enabled = i < n;
            lr.gameObject.layer = gameObject.layer;
            Vector3 c = basePos + Vector3.up * i * 0.35f;
            lr.SetPosition(0, c - right * 0.45f - Vector3.up * 0.2f);
            lr.SetPosition(1, c);
            lr.SetPosition(2, c + right * 0.45f - Vector3.up * 0.2f);
        }
    }

    void Update()
    {
        float repair = Data.ranks[Rank].selfRepair;
        if (repair > 0f && health != null && health.enabled && health.IsDamaged)
            health.Heal(repair * health.MaxHealth * Time.deltaTime);
    }
}
