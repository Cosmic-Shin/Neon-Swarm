using System.Collections.Generic;
using UnityEngine;

public static class TeamLook
{
    const string Accent = "Skitter_NeonCyan";
    static readonly Dictionary<(Material, Color), Material> tinted = new();
    static readonly Dictionary<Material, Material> original = new();

    public static void Apply(GameObject go, int team)
    {
        if (!Net.Online && team == 0 && Net.ColorOf(0) == TeamMember.TeamColors[0]) return;
        Color c = Net.ColorOf(team);
        if (c == Color.white) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (r is LineRenderer || r is ParticleSystemRenderer || r is SpriteRenderer) continue;
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var src = mats[i] != null && original.TryGetValue(mats[i], out var o) ? o : mats[i];
                if (src == null || src.name != Accent) continue;
                mats[i] = Tint(src, c);
                changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }
    }

    static Material Tint(Material src, Color c)
    {
        if (tinted.TryGetValue((src, c), out var m)) return m;
        m = new Material(src) { name = $"{src.name}_{ColorUtility.ToHtmlStringRGB(c)}" };
        float glow = src.HasProperty("_EmissionColor") ? src.GetColor("_EmissionColor").maxColorComponent : 1f;
        m.color = c;
        if (src.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * glow);
        tinted[(src, c)] = m;
        original[m] = src;
        return m;
    }
}
