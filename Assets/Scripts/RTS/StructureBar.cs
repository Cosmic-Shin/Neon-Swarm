using UnityEngine;
using UnityEngine.UI;

// Info bar for the selected building: name, health, power, SELL, REPAIR, and UPGRADE (Mk II / III) when it has one.
public class StructureBar : MonoBehaviour
{
    [SerializeField] GoodSelectionManager manager;
    [SerializeField] GameObject panel;
    [SerializeField] Text title;
    [SerializeField] Text stats;
    [SerializeField] Button sellButton;
    [SerializeField] Button repairButton;
    [SerializeField] Button upgradeButton; // "Fill" child = progress bar while upgrading
    [SerializeField] float upgradeRowHeight = 54f; // the bar grows by this when the building can upgrade

    Text sellText, repairText, upgradeText;
    Image upgradeFill;
    Tooltip upgradeTip;
    RectTransform panelRect;
    float baseHeight;

    void Awake()
    {
        sellText = sellButton.GetComponentInChildren<Text>();
        repairText = repairButton.GetComponentInChildren<Text>();
        upgradeText = upgradeButton.GetComponentInChildren<Text>();
        upgradeFill = upgradeButton.transform.Find("Fill").GetComponent<Image>();
        upgradeTip = upgradeButton.gameObject.AddComponent<Tooltip>();
        panelRect = (RectTransform)panel.transform;
        baseHeight = panelRect.sizeDelta.y;
        sellButton.onClick.AddListener(() => manager.SelectedStructure?.Sell());
        repairButton.onClick.AddListener(() => manager.SelectedStructure?.ToggleRepair());
        upgradeButton.onClick.AddListener(() => Upgrades(manager.SelectedStructure)?.Upgrade.Upgrade()); // pays from the ResourceBank
        panel.SetActive(false);
    }

    static UpgradeEffects Upgrades(Structure s) => s != null && s.TryGetComponent(out UpgradeEffects fx) ? fx : null;

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
        ShowUpgrade(Upgrades(s));
    }

    void ShowUpgrade(UpgradeEffects fx)
    {
        upgradeButton.gameObject.SetActive(fx != null);
        panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, baseHeight + (fx != null ? upgradeRowHeight : 0f));
        if (fx == null) return;

        var up = fx.Upgrade;
        var next = fx.Next;
        upgradeFill.fillAmount = up.IsUpgrading ? up.Progress : 0f;
        if (up.IsUpgrading)
        {
            upgradeButton.interactable = false;
            upgradeText.text = $"UPGRADING  {up.Progress * 100f:0}%";
        }
        else if (next == null)
        {
            upgradeButton.interactable = false;
            upgradeText.text = $"MK {Roman(up.level)}  ·  MAX LEVEL";
        }
        else
        {
            bool slot = BuildSlots.Free;
            upgradeButton.interactable = slot && up.CanUpgrade && ResourceBank.GetOrCreate().Credits >= up.NextCost;
            upgradeText.text = slot ? $"UPGRADE TO MK {Roman(next.level)}  ${up.NextCost}" : "BUSY: 2 BUILDINGS IN PROGRESS";
        }
        upgradeTip.text = next == null
            ? "Fully upgraded."
            : $"<color=#00E6D9><b>{next.displayName}</b></color>   ${up.NextCost}  ·  {up.upgradeTime[next.level]:0}s\n{next.description}";
    }

    static string Roman(int n) => n == 3 ? "III" : n == 2 ? "II" : "I";
}
