using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class Tutorial : MonoBehaviour
{
    public static bool Active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => Active = false;

    public static void Begin()
    {
        Skirmish.Opponents.Clear();
        Skirmish.PlayerSide = Skirmish.PlayerColor = Skirmish.PlayerBase = 0;
        Skirmish.StartCredits = 5000;
        Skirmish.Minutes = 0;
        Skirmish.ElapsedBefore = 0f;
        Skirmish.Active = true;
        Active = true;
        UnityEngine.SceneManagement.SceneManager.LoadScene(GameMaps.All[0].scene);
    }

    public MatchManager match;
    public Building hive;

    struct Step { public string title, text; public Func<bool> done; public Action enter; }

    readonly List<Step> steps = new();
    readonly List<Health> scouts = new();
    GoodSelectionManager manager;
    Text title, body, counter;
    int index = -1;
    float stepStarted;
    bool moved;
    Vector3 cameraStart;
    int unitsBefore, structuresBefore;

    void Start()
    {
        manager = FindFirstObjectByType<GoodSelectionManager>();
        manager.OnMoveCommand += _ => moved = true;
        manager.OnAttackMoveCommand += _ => moved = true;
        BuildPanel();

        Add("WELCOME, COMMANDER", "Your Core Nexus is powering up and the Assembly Hive is sending out harvesters. Watch for a moment.",
            () => Time.timeSinceLevelLoad > 6f);
        Add("MOVE THE CAMERA", "Pan with <b>W A S D</b> or the arrow keys, or push the mouse against the screen edge. Scroll to zoom.",
            () => Vector3.Distance(CameraPan.LookingAt, cameraStart) > 20f, () => cameraStart = CameraPan.LookingAt);
        Add("SELECT A HARVESTER", "<b>Left-click</b> one of your harvesters. They mine ore and bring credits home on their own.",
            () => Selected(u => u.TryGetComponent(out Harvester _)));
        Add("TRAIN A UNIT", "<b>Click your Assembly Hive</b>, then pick a unit from the row at the bottom. Right-click a queued unit to cancel it.",
            () => hive == null || hive.QueueLength > 0 || MyUnits() > unitsBefore, () => unitsBefore = MyUnits());
        Add("WAIT FOR IT", "Units take a few seconds to build. The new unit drives out through the Hive's gate.",
            () => MyUnits() > unitsBefore);
        Add("SELECT YOUR UNIT", "<b>Left-drag</b> a box around your new unit to select it. Ctrl + a number key saves a control group.",
            () => Selected(u => !u.TryGetComponent(out Harvester _)));
        Add("MOVE OUT", "<b>Right-click</b> the ground to move the selected units there. Hold Shift to queue several orders.",
            () => moved, () => moved = false);
        Add("BUILD A STRUCTURE", "Click <b>BUILDINGS</b>, pick one, then click the ground to place it. Drag while holding the button to rotate it. Power Plants keep your base running.",
            () => Structure.All.Count > structuresBefore || FindObjectsByType<Construction>(FindObjectsSortMode.None).Length > 0,
            () => structuresBefore = Structure.All.Count);
        Add("ENEMY SCOUTS!", "Enemy scouts are coming. Select your units and <b>right-click an enemy</b> to attack it. Press <b>A</b> then click the ground to attack-move.",
            () => scouts.Count > 0 && scouts.TrueForAll(h => h == null || h.IsDead), SpawnScouts);
        Next();
    }

    void Add(string t, string text, Func<bool> done, Action enter = null) => steps.Add(new Step { title = t, text = text, done = done, enter = enter });

    void Update()
    {
        if (index < 0 || index >= steps.Count || Time.time - stepStarted < 1f) return;
        if (steps[index].done()) Next();
    }

    void Next()
    {
        index++;
        stepStarted = Time.time;
        if (index > 0) Sfx.Play(Snd.Good, 0.6f);
        if (index >= steps.Count) { match.Finish("TUTORIAL COMPLETE", "You know the basics. Try a Skirmish against the AI next!"); title.transform.parent.gameObject.SetActive(false); enabled = false; return; }
        var s = steps[index];
        s.enter?.Invoke();
        title.text = s.title;
        body.text = s.text;
        counter.text = $"{index + 1} / {steps.Count}";
    }

    bool Selected(Predicate<SelectableUnit> match)
    {
        foreach (var u in manager.SelectedUnits) if (u != null && match(u)) return true;
        return false;
    }

    static int MyUnits()
    {
        int n = 0;
        foreach (var u in FindObjectsByType<SelectableUnit>(FindObjectsSortMode.None))
            if (u.TryGetComponent(out Health h) && h.team == 0 && !h.IsDead && !u.TryGetComponent(out Harvester _)) n++;
        return n;
    }

    void SpawnScouts()
    {
        var prefab = hive != null ? hive.CheapestFighter() : null;
        if (prefab == null) return;
        Vector3 home = hive.transform.position;
        Vector3 at = home + (Vector3.zero - home).normalized * 45f;
        for (int i = 0; i < 2; i++)
        {
            Vector3 p = at + new Vector3(i * 3f, 0f, 0f);
            if (NavMesh.SamplePosition(p, out var hit, 10f, NavMesh.AllAreas)) p = hit.position;
            var go = MatchManager.SpawnFor(1, prefab.gameObject, p, Quaternion.LookRotation(home - p));
            scouts.Add(go.GetComponent<Health>());
            if (go.TryGetComponent(out Attacker a)) a.AttackMove(home);
        }
        Alerts.ShowAt("Enemy scouts spotted", Alerts.Bad, at);
    }

    void BuildPanel()
    {
        var canvas = new GameObject("TutorialCanvas", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvas.transform.SetParent(transform, false);

        var panel = new GameObject("TutorialPanel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        panel.SetParent(canvas.transform, false);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = new Vector2(0f, -96f);
        panel.sizeDelta = new Vector2(900f, 130f);
        panel.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.07f, 0.92f);
        panel.GetComponent<Image>().raycastTarget = false;
        HoloSkin.Back(panel, "Panel_Frame");
        HoloSkin.FX(panel, HoloUI.Cyan, intro: true, strength: 0.6f);

        title = Label(panel, 26, FontStyle.Bold, Alerts.Info, new Vector2(24f, -14f), new Vector2(700f, 34f), TextAnchor.UpperLeft);
        counter = Label(panel, 18, FontStyle.Normal, new Color(0.6f, 0.68f, 0.8f), new Vector2(-24f, -18f), new Vector2(160f, 30f), TextAnchor.UpperRight, true);
        body = Label(panel, 20, FontStyle.Normal, Color.white, new Vector2(24f, -54f), new Vector2(852f, 70f), TextAnchor.UpperLeft);
    }

    static Text Label(RectTransform parent, int size, FontStyle style, Color c, Vector2 pos, Vector2 box, TextAnchor align, bool right = false)
    {
        var t = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
        var rt = t.rectTransform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = right ? Vector2.one : new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        t.font = style == FontStyle.Bold ? HoloSkin.BoldFont : HoloSkin.Font;
        t.fontSize = size;
        t.color = c;
        t.alignment = align;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.raycastTarget = false;
        return t;
    }
}
