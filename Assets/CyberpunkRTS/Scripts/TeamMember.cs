using UnityEngine;

/// <summary>Which side a unit belongs to. 0 = Hive (player), 1 = Aegis (enemy).</summary>
public class TeamMember : MonoBehaviour
{
    public int team = 0;
    public static readonly Color[] TeamColors = { new Color(0f, 0.9f, 0.85f), new Color(1f, 0.25f, 0.15f), new Color(1f, 0.8f, 0.1f), new Color(0.6f, 0.4f, 1f),
        new Color(0.3f, 1f, 0.3f), new Color(1f, 0.5f, 0f), new Color(1f, 0.3f, 0.8f), new Color(0.2f, 0.5f, 1f) }; // up to 8 teams

    /// <summary>A player slot's colour: the one they picked in the room (multiplayer), else slot n = colour n.</summary>
    public static Color ColorOf(int slot)
    {
        if (slot < 0 || slot >= TeamColors.Length) return Color.white;
        int i = RoomState.Instance != null ? RoomState.Instance.ColorIndexOf(slot) : slot;
        return TeamColors[Mathf.Clamp(i, 0, TeamColors.Length - 1)];
    }
}
