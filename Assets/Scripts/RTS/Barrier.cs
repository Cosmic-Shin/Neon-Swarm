using UnityEngine;

// Map feature: a neutral wall piece sealing a chasm crossing (built by Tools > GD5 > Build Maps from the
// Firewall Gate model). Its Health is scenery (team = Creep.Team), so only right-click attack orders hurt it;
// its carving NavMeshObstacle opens the crossing when it breaks. Online the host runs its health
// (RoomState.Outposts.cs); saves keep which pieces are still standing.
public class Barrier : MonoBehaviour
{
}
