using UnityEngine;
using UnityEngine.UI;

// Info bar for the selected building: name, health, power, SELL and REPAIR.
public class StructureBar : MonoBehaviour
{
    [SerializeField] GoodSelectionManager manager;
    [SerializeField] GameObject panel;
    [SerializeField] Text title;
    [SerializeField] Text stats;
    [SerializeField] Button sellButton;
    [SerializeField] Button repairButton;

    Text sellText, repairText;

    void Awake()
    {
        sellText = sellButton.GetComponentInChildren<Text>();
        repairText = repairButton.GetComponentInChildren<Text>();
        sellButton.onClick.AddListener(() => manager.SelectedStructure?.Sell());
        repairButton.onClick.AddListener(() => manager.SelectedStructure?.ToggleRepair());
        panel.SetActive(false);
    }

    void LateUpdate()
    {
        var s = manager.SelectedStructure;
        panel.SetActive(s != null);
        if (s == null) return;

        string power = s.power > 0 ? $"+{s.power}" : s.power.ToString();
        title.text = s.Name.ToUpper();
        stats.text = $"HP {s.Health.Current:0}/{s.Health.MaxHealth:0}    POWER {power}";
        sellButton.interactable = s.CanSell;
        sellText.text = s.CanSell ? $"SELL ${s.SellValue}" : "SELL";
        repairButton.interactable = s.Health.IsDamaged;
        repairText.text = s.Repairing ? "REPAIRING" : "REPAIR";
    }
}
