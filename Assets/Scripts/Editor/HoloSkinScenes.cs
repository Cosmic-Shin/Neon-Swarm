using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Style = HoloUI.ButtonStyle;

// Tools > GD5 > Apply Holo Skin: restyles the scene-built UI of Login and RTSSelection (whichever are open) with
// the Chamfered Holo kit, through the same HoloSkin helpers the code-built screens use. Frames and backgrounds
// only: no layout, text or behaviour changes. Safe to run again.
public static class HoloSkinScenes
{
    [MenuItem("Tools/GD5/Apply Holo Skin")]
    public static void Apply()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            var canvas = Find(scene, "Canvas");
            if (canvas == null) continue;
            canvas.GetComponent<CanvasScaler>().referencePixelsPerUnit = 100f; // the 2x sprites show at 1x
            if (scene.name == "Login") Login(canvas);
            else if (scene.name == "RTSSelection") Hud(canvas);
            else continue;
            HoloSkin.ApplyFont(canvas);
            foreach (var t in canvas.GetComponentsInChildren<HoloTitle>(true)) t.Build(); // titles back to the bold face
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[HoloSkin] styled " + scene.name);
        }
    }

    static Transform Find(Scene scene, string root)
    {
        foreach (var go in scene.GetRootGameObjects()) if (go.name == root) return go.transform;
        return null;
    }

    static RectTransform R(Transform t, string path)
    {
        var r = t.Find(path) as RectTransform;
        if (r == null) Debug.LogWarning("[HoloSkin] not found: " + t.name + "/" + path);
        return r;
    }

    static void Btn(Transform t, string path, Style style = Style.Default)
    {
        var r = R(t, path);
        if (r != null) HoloSkin.Button(r.GetComponent<Selectable>(), style);
    }

    static void Field(Transform t, string path) { var r = R(t, path); if (r != null) HoloSkin.Input(r.GetComponent<InputField>()); }
    static void Title(Transform t, string path, float rule) { var r = R(t, path); if (r != null) HoloSkin.Title(r.GetComponent<Text>(), rule); }
    static void Inset(Transform t, string path) { var r = R(t, path); if (r != null) HoloSkin.Back(r, "Panel_Inset"); }

    // ---------------- menus (Login scene) ----------------

    static void Login(Transform c)
    {
        var signIn = R(c, "SignInPanel");
        HoloSkin.Modal(signIn);
        Title(signIn, "Header", 360f);
        Field(signIn, "Username");
        Field(signIn, "Password");
        Btn(signIn, "SIGNINButton");
        Btn(signIn, "CREATEACCOUNTButton");

        var lobby = R(c, "LobbyPanel");
        HoloSkin.Modal(lobby);
        Title(lobby, "Welcome", 380f);
        var entries = new[] { "SKIRMISHButton", "MULTIPLAYERButton", "FRIENDSButton", "SETTINGSButton", "ACCOUNTButton", "QUITButton" };
        for (int i = 0; i < entries.Length; i++)
        {
            Btn(lobby, entries[i]);
            if (R(lobby, entries[i]).TryGetComponent(out HoloButtonFX fx)) fx.index = (i + 1).ToString("00"); // "01".."06"
        }

        var account = R(c, "AccountPanel");
        HoloSkin.Modal(account);
        Btn(account, "SIGNOUTButton", Style.Danger);
        Btn(account, "BACKButton");

        var mp = R(c, "MultiplayerPanel");
        HoloSkin.Modal(mp);
        Title(mp, "Header", 540f);
        Btn(mp, "QuickMatchButton");
        Field(mp, "RoomName");
        Btn(mp, "FriendsOnlyButton", Style.Small);
        Btn(mp, "CREATEROOMButton");
        Btn(mp, "JOINROOMButton");
        Inset(mp, "RoomList");
        Btn(mp, "BACKButton");
        Btn(mp, "FRIENDSButton");
        var menu = mp.GetComponent<MultiplayerMenu>();
        HoloSkin.Row(menu.roomTemplate);

        // Lobby (and the skirmish setup, which copies it at runtime).
        var room = R(c, "RoomPanel");
        HoloSkin.Back(room, "Panel_Frame_Pink");
        HoloSkin.FX(room, HoloUI.Pink);
        Title(room, "Title", 520f);
        Btn(room, "LEAVEROOMButton", Style.Danger);
        Btn(room, "StartGameButton", Style.Gold);
        Btn(room, "FRIENDSButton");
        Btn(room, "CreditsButton", Style.Small);
        Btn(room, "TimeButton", Style.Small);
        Field(room, "CustomCredits");
        Field(room, "ChatInput");
        Inset(room, "PlayerRows");
        Inset(room, "ChatLog");
        Btn(room, "PlayerRows/PlayerRow/Team", Style.Small);
        MapFrame(R(room, "Map"));
    }

    // The map preview is a picture, so its Panel_Minimap frame goes behind it, a frame's width bigger.
    static void MapFrame(RectTransform map)
    {
        var parent = map.parent;
        var frame = parent.Find("MapFrame") as RectTransform;
        if (frame == null)
        {
            frame = new GameObject("MapFrame", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            frame.SetParent(parent, false);
        }
        frame.SetSiblingIndex(map.GetSiblingIndex());
        frame.anchorMin = map.anchorMin; frame.anchorMax = map.anchorMax; frame.pivot = map.pivot;
        frame.anchoredPosition = map.anchoredPosition;
        frame.sizeDelta = map.sizeDelta + Vector2.one * 2f * (HoloSkin.Glow("Panel_Minimap") + 14f);
        HoloUI.SetSliced(frame.GetComponent<Image>(), "Panel_Minimap").raycastTarget = false;
    }

    // ---------------- in-match HUD (RTSSelection scene) ----------------

    static void Hud(Transform c)
    {
        HoloSkin.Back(R(c, "MatchTimer"), "Chip_Cyan");
        HoloSkin.Back(R(c, "Power"), "Chip_Green");
        HoloSkin.Back(R(c, "Credits"), "Chip_Green");
        HoloSkin.Back(R(c, "Minimap"), "Panel_Minimap");
        HoloSkin.FX(R(c, "Minimap"), HoloUI.Cyan, intro: false, strength: 0.5f);
        Btn(c, "BuildingsButton", Style.Small);

        foreach (var panel in new[] { "BuildingPanel", "BuildMenuPanel" })
        {
            HoloSkin.Back(R(c, panel), "Panel_BuildBar");
            HoloSkin.FX(R(c, panel), HoloUI.Cyan, intro: true, strength: 0.6f); // a quick fade-in each time it opens
        }
        HoloSkin.Card(R(c, "BuildingPanel/Viewport/Content/UnitButtonTemplate").GetComponent<Button>());
        HoloSkin.Card(R(c, "BuildMenuPanel/Viewport/Content/BuildingButtonTemplate").GetComponent<Button>());

        var bar = R(c, "StructureBar");
        HoloSkin.Back(bar, "Panel_Frame");
        HoloSkin.FX(bar, HoloUI.Cyan, intro: true, strength: 0.6f);
        Btn(bar, "UpgradeButton", Style.Gold);
        Btn(bar, "SellButton");
        Btn(bar, "RepairButton", Style.Danger);
        var upFill = R(bar, "UpgradeButton/Fill").GetComponent<Image>();
        HoloUI.SetSliced(upFill, "Fill_White", new Color(HoloUI.Yellow.r, HoloUI.Yellow.g, HoloUI.Yellow.b, 0.3f));
        upFill.type = Image.Type.Filled; // StructureBar fills it left to right while upgrading
        upFill.fillMethod = Image.FillMethod.Horizontal;

        HoloSkin.Back(R(c, "TooltipBox"), "Tooltip");

        var progress = R(c, "BuildProgressTemplate");
        var old = progress.Find("Frame");
        if (old != null) old.gameObject.SetActive(false);
        var progressFill = R(progress, "Fill").GetComponent<Image>();
        HoloSkin.Bar(progress.GetComponent<Image>(), progressFill, HoloUI.Cyan);
        progressFill.type = Image.Type.Filled; // Construction sets fillAmount
        progressFill.fillMethod = Image.FillMethod.Horizontal;

        var menu = R(c, "GameMenu");
        Title(menu, "Title", 520f);
        Btn(menu, "Resume");
        Btn(menu, "Surrender", Style.Danger);
        Btn(menu, "Leave", Style.Danger);

        var end = R(c, "MatchEnd");
        Btn(end, "MenuButton");

        Field(c, "ChatInput");
    }
}
