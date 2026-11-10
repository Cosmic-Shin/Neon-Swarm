using UnityEngine;

[RequireComponent(typeof(Refinery))]
public class FreeHarvester : MonoBehaviour
{
    [SerializeField] SelectableUnit harvesterPrefab;
    [SerializeField] float exitSpeed = 4f;

    void OnConstructed()
    {
        Vector3 dock = GetComponent<Refinery>().DockPosition;
        Vector3 outDir = dock - transform.position; outDir.y = 0f;
        Vector3 inside = transform.position - outDir.normalized * 2.5f; 
        inside.y = dock.y;

        var unit = Net.Spawn(harvesterPrefab, dock, Quaternion.LookRotation(outDir));
        Attacker.SetTeam(unit, TeamEconomy.TeamOf(this)); // an AI's refinery: its harvester
        unit.WalkOut(inside, dock, exitSpeed);
    }
}
