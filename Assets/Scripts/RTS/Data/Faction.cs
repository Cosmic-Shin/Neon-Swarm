using System.Collections.Generic;
using UnityEngine;

public enum FactionId { Hive, Aegis }

[CreateAssetMenu(menuName = "RTS/Faction", fileName = "Faction")]
public class Faction : ScriptableObject
{
    [System.Serializable]
    public struct Twin
    {
        public Object hive;
        public Object own;
    }

    public FactionId id;
    public string displayName;
    [TextArea] public string tagline;
    public Sprite emblem;
    public Color color = Color.cyan;
    public int order;

    [Header("Starting base")]
    public GameObject hq;
    public GameObject factory;
    public GameObject harvester;

    [Header("Build menu")]
    public BuildingData[] buildings;

    [Tooltip("Hive data (AI build orders, unit picks, missions) and what this faction uses instead.")]
    public Twin[] twins;

    public T Swap<T>(T hive) where T : Object
    {
        if (hive == null || twins == null) return hive;
        foreach (var t in twins) if (t.hive == hive) return t.own as T;
        return twins.Length == 0 ? hive : null;
    }

    public T[] Swap<T>(T[] hive) where T : Object
    {
        if (hive == null) return null;
        var list = new List<T>();
        foreach (var h in hive) if (Swap(h) is T own && own != null) list.Add(own);
        return list.ToArray();
    }

    static Faction[] all;
    public static Faction[] All
    {
        get
        {
            if (all == null || all.Length == 0)
            {
                all = Resources.LoadAll<Faction>("Factions");
                System.Array.Sort(all, (a, b) => a.order.CompareTo(b.order));
            }
            return all;
        }
    }

    public static Faction Hive => All.Length > 0 ? All[0] : null;
    public static Faction Get(int index) => All.Length == 0 ? null : All[Mathf.Clamp(index, 0, All.Length - 1)];
    public static int IndexOf(Faction f) => System.Array.IndexOf(All, f);

    static readonly Dictionary<int, Faction> teams = new();
    public static System.Func<int, Faction> Online;

    public static Faction Of(int team)
    {
        if (Online != null && Online(team) is Faction live) return live;
        if (teams.TryGetValue(team, out var f) && f != null) return f;
        return Skirmish.Active ? Get(Skirmish.FactionOf(team)) : Hive;
    }

    public static Faction Mine => Of(0);
    public static string PowerName(int team)
    {
        if (Of(team) is Faction f && f.buildings != null)
            foreach (var d in f.buildings) if (d != null && d.prefab != null && d.prefab.TryGetComponent(out Structure s) && s.power > 0) return d.displayName;
        return "Power Pylon";
    }
    public static string HqName(int team) => Of(team) is Faction f && f.hq != null && f.hq.TryGetComponent(out Structure s) ? s.Name : "Core Nexus";
    public static void Set(int team, Faction f) => teams[team] = f;
    public static void Clear() => teams.Clear();
}
