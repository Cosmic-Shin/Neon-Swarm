using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
static class PlayFromLobby
{
    const string Menu = "Tools/GD5/Play From Lobby", Key = "GD5.PlayFromLobby";

    static PlayFromLobby()
    {
        EditorApplication.delayCall += Apply;
        EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.ExitingEditMode) Apply(); };
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
