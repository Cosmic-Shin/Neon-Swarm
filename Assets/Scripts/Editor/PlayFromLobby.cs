using UnityEditor;
using UnityEditor.SceneManagement;

// Play in the editor starts from the main menu (build scene 0), like the real game, whatever scene is open.
// Tools > GD5 > Play From Lobby turns it off to test the open scene directly.
[InitializeOnLoad]
static class PlayFromLobby
{
    const string Menu = "Tools/GD5/Play From Lobby", Key = "GD5.PlayFromLobby";

    static PlayFromLobby()
    {
        EditorApplication.delayCall += Apply;
        EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.ExitingEditMode) Apply(); }; // set again right before Play
    }

    static bool On => EditorPrefs.GetBool(Key, true);

    static void Apply()
    {
        var first = EditorBuildSettings.scenes.Length > 0 ? AssetDatabase.LoadAssetAtPath<SceneAsset>(EditorBuildSettings.scenes[0].path) : null;
        EditorSceneManager.playModeStartScene = On ? first : null;
        Menu_Check();
    }

    [MenuItem(Menu)]
    static void Toggle() { EditorPrefs.SetBool(Key, !On); Apply(); }

    static void Menu_Check() => UnityEditor.Menu.SetChecked(Menu, On);
}
