using UnityEngine;

/// <summary>Which side a unit belongs to. 0 = Hive (player), 1 = Aegis (enemy).</summary>
public class TeamMember : MonoBehaviour
{
    public int team = 0;
    public static readonly Color[] TeamColors = { new Color(0f, 0.9f, 0.85f), new Color(1f, 0.25f, 0.15f), new Color(1f, 0.8f, 0.1f), new Color(0.6f, 0.4f, 1f),
        new Color(0.3f, 1f, 0.3f), new Color(1f, 0.5f, 0f), new Color(1f, 0.3f, 0.8f), new Color(0.2f, 0.5f, 1f) }; // up to 8 teams
    public static Color ColorOf(int team) => team >= 0 && team < TeamColors.Length ? TeamColors[team] : Color.white;
}
