using UnityEngine;
using UnityEngine.SceneManagement;

// Picks the HUD sprite + cursor set for the player's faction when a match map loads (menus keep the default Hive look).
// sceneLoaded fires after Awake/OnEnable but before Start, which is where the HUD builds itself.
public static class FactionSkin
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Hook()
    {
        HoloUI.Skin = "";
        SceneManager.sceneLoaded -= Loaded;
        SceneManager.sceneLoaded += Loaded;
    }

    static void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        bool map = System.Array.Exists(GameMaps.All, m => m.scene == scene.name);
        var mine = map ? Faction.Mine : null;
        HoloUI.Skin = mine != null && mine.id == FactionId.Aegis ? "Aegis/" : "";
    }
}
