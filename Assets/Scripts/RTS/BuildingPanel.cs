using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BuildingPanel : MonoBehaviour
{
    [SerializeField] GoodSelectionManager manager;
    [SerializeField] RectTransform panel;
    [SerializeField] Button buttonTemplate;

    Building building;
    Building watched;
    int wasProducing = -1;
    float wasProgress;
    readonly List<Image> sweeps = new();
    readonly List<Text> counts = new();
    readonly List<Button> buttons = new();
    readonly List<string> blockers = new();

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

    void Rebuild()
    {
        Transform list = buttonTemplate.transform.parent;
        for (int i = list.childCount - 1; i >= 0; i--)
        {
            var child = list.GetChild(i);
            if (child != buttonTemplate.transform && child.GetComponent<Button>()) Destroy(child.gameObject);
        }

        sweeps.Clear();
        counts.Clear();
        buttons.Clear();
        blockers.Clear();
        for (int i = 0; i < building.OptionCount; i++)
        {
            int index = i;
            Button btn = Instantiate(buttonTemplate, list);
            HoloSkin.SquareCard(btn);
            btn.gameObject.SetActive(true);
            btn.transform.Find("Text").GetComponent<Text>().text = building.NameOf(i) + (building.SquadOf(i) > 1 ? $" x{building.SquadOf(i)}" : "");
            btn.transform.Find("Time").GetComponent<Text>().text = $"{building.TimeOf(i):0}s";
            btn.transform.Find("Cost").GetComponent<Text>().text = $"${building.CostOf(i)}";
            buttons.Add(btn);
            if (building.IconOf(i) != null) btn.transform.Find("Icon").GetComponent<Image>().sprite = building.IconOf(i);
            btn.onClick.AddListener(() => building.Enqueue(index));

            btn.gameObject.AddComponent<RightClick>().action = () => building.CancelOne(index);
            btn.gameObject.AddComponent<Tooltip>().text = building.TooltipOf(i);
            blockers.Add(building.Blocker(i));
            sweeps.Add(btn.transform.Find("Progress").GetComponent<Image>());
            counts.Add(btn.transform.Find("Count").GetComponent<Text>());
        }
    }

    void LateUpdate()
    {
        if (building == null) { if (panel.gameObject.activeSelf) panel.gameObject.SetActive(false); return; }

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
            buttons[i].interactable = building.CanQueue(i);
            LockBadge.Set(buttons[i], needs, building.IsResearched(i) || ResourceBank.GetOrCreate().Credits >= building.CostOf(i));
            string why = building.Blocker(i);
            if (why != blockers[i]) { blockers[i] = why; buttons[i].GetComponent<Tooltip>().text = building.TooltipOf(i); }
        }
    }
}

public class RightClick : MonoBehaviour, IPointerClickHandler
{
    public System.Action action;
    public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Right) action?.Invoke(); }
}
