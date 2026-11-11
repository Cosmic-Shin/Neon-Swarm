using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BuildingPanel : MonoBehaviour
{
    [SerializeField] GoodSelectionManager manager;
    [SerializeField] RectTransform panel;
    [SerializeField] Button buttonTemplate; // disabled template, cloned per unit

    Building building;
    Building watched; // production state from last frame, for the "done" flash
    int wasProducing = -1;
    float wasProgress;
    readonly List<Image> sweeps = new(); // per button: Card_ProgressFill rising from the card's bottom while it's being built
    readonly List<Text> counts = new();  // per button: "x2" when queued
    readonly List<Button> buttons = new();
    readonly List<string> blockers = new(); // per button: why it can't be queued, as last shown in its tooltip

    void Awake()
    {
        panel.gameObject.SetActive(false);
        buttonTemplate.gameObject.SetActive(false);
    }

    void OnEnable() => manager.OnBuildingSelected += Show;
    void OnDisable() => manager.OnBuildingSelected -= Show;

    void Show(Building b)
    {
        building = b;
        panel.gameObject.SetActive(b != null);
        if (b != null) Rebuild();
    }

    // One button per unit the building can summon.
    void Rebuild()
    {
        Transform list = buttonTemplate.transform.parent;
        for (int i = list.childCount - 1; i >= 0; i--)
        {
            var child = list.GetChild(i);
            if (child != buttonTemplate.transform && child.GetComponent<Button>()) Destroy(child.gameObject); // keep the panel's frame
        }

        sweeps.Clear();
        counts.Clear();
        buttons.Clear();
        blockers.Clear();
        for (int i = 0; i < building.OptionCount; i++)
        {
            int index = i; // captured per button
            Button btn = Instantiate(buttonTemplate, list);
            btn.gameObject.SetActive(true);
            btn.transform.Find("Text").GetComponent<Text>().text = building.NameOf(i) + (building.SquadOf(i) > 1 ? $" x{building.SquadOf(i)}" : "");
            btn.transform.Find("Time").GetComponent<Text>().text = $"{building.TimeOf(i):0}s";
            btn.transform.Find("Cost").GetComponent<Text>().text = $"${building.CostOf(i)}";
            buttons.Add(btn);
            if (building.IconOf(i) != null) btn.transform.Find("Icon").GetComponent<Image>().sprite = building.IconOf(i);
            btn.onClick.AddListener(() => building.Enqueue(index)); // left-click (Button ignores right)

            // Not EventTrigger: it swallows scroll/drag, so the row couldn't scroll over the buttons.
            btn.gameObject.AddComponent<RightClick>().action = () => building.CancelOne(index);
            btn.gameObject.AddComponent<Tooltip>().text = building.TooltipOf(i);
            blockers.Add(building.Blocker(i));
            sweeps.Add(btn.transform.Find("Progress").GetComponent<Image>());
            counts.Add(btn.transform.Find("Count").GetComponent<Text>());
        }
    }

    void LateUpdate()
    {
        // Panel is docked at the bottom of the screen; just hide it if the building is gone.
        if (building == null) { if (panel.gameObject.activeSelf) panel.gameObject.SetActive(false); return; }

        // Finished one: its button flashes (production moved on, or the bar restarted for the next in the queue).
        int now = building.Producing;
        float progress = building.Progress01;
        if (building == watched && wasProducing >= 0 && wasProducing < buttons.Count && wasProgress > 0.8f && (now != wasProducing || progress < wasProgress - 0.5f))
            UIJuice.Flash(buttons[wasProducing], Alerts.Good);
        (watched, wasProducing, wasProgress) = (building, now, progress);

        for (int i = 0; i < sweeps.Count; i++)
        {
            bool active = building.Producing == i;
            HoloSkin.SetProgress(sweeps[i], active ? building.Progress01 : 0f);
            int n = building.QueuedCount(i);
            string needs = building.Needs(i);
            HoloSkin.SetCard(buttons[i], needs != null ? HoloUI.CardState.Locked : active || n > 0 ? HoloUI.CardState.Building : HoloUI.CardState.Normal);
            counts[i].text = building.IsResearched(i) ? "DONE" : n > 0 ? $"x{n}" : "";
            buttons[i].interactable = building.CanQueue(i); // red frame when short on credits (or already researched)
            LockBadge.Set(buttons[i], needs, building.IsResearched(i) || ResourceBank.GetOrCreate().Credits >= building.CostOf(i));
            string why = building.Blocker(i);
            if (why != blockers[i]) { blockers[i] = why; buttons[i].GetComponent<Tooltip>().text = building.TooltipOf(i); } // red reason line
        }
    }
}

// Right-click only; ignores scroll/drag so they reach the ScrollRect.
public class RightClick : MonoBehaviour, IPointerClickHandler
{
    public System.Action action;
    public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Right) action?.Invoke(); }
}
