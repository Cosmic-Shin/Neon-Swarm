using UnityEngine;
using UnityEngine.UI;

public class StructureBar : MonoBehaviour
{
    [SerializeField] GoodSelectionManager manager;
    [SerializeField] GameObject panel;
    [SerializeField] Text title;
    [SerializeField] Text stats;
    [SerializeField] Button sellButton;
    [SerializeField] Button repairButton;
    [SerializeField] Button upgradeButton;
    [SerializeField] float upgradeRowHeight = 54f;

    Text sellText, repairText, upgradeText;
    Image upgradeFill;
    RectTransform hpFill;
    Tooltip upgradeTip;
    RectTransform panelRect;
    float baseHeight;

    Button fireButton;
    Text fireText;
    Image fireFill;

    void Awake()
    {
        fireButton = Instantiate(upgradeButton, upgradeButton.transform.parent);
        fireButton.name = "FireSiloButton";
        fireText = fireButton.GetComponentInChildren<Text>();
        fireFill = fireButton.transform.Find("Fill").GetComponent<Image>();
        HoloSkin.Button(fireButton, HoloUI.ButtonStyle.Danger);
        fireButton.onClick.AddListener(() => { if (Silo(manager.SelectedStructure) is SwarmSilo silo) SiloTargeting.Begin(silo, manager); });
        fireButton.gameObject.SetActive(false);
        sellText = sellButton.GetComponentInChildren<Text>();
        repairText = repairButton.GetComponentInChildren<Text>();
        upgradeText = upgradeButton.GetComponentInChildren<Text>();
        upgradeFill = upgradeButton.transform.Find("Fill").GetComponent<Image>();
        upgradeTip = upgradeButton.gameObject.AddComponent<Tooltip>();
        panelRect = (RectTransform)panel.transform;
        baseHeight = panelRect.sizeDelta.y;
        sellButton.onClick.AddListener(() => manager.SelectedStructure?.Sell());
        repairButton.onClick.AddListener(() => manager.SelectedStructure?.ToggleRepair());
        upgradeButton.onClick.AddListener(() => Upgrades(manager.SelectedStructure)?.Upgrade.Upgrade());
        hpFill = HpBar();
        panel.SetActive(false);
    }

    RectTransform HpBar()
    {
        var back = new GameObject("HpBar", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        back.transform.SetParent(panel.transform, false);
        back.raycastTarget = false;
        var r = back.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(14f, -60f);
        r.sizeDelta = new Vector2(352f, 6f);
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        fill.transform.SetParent(r, false);
        fill.raycastTarget = false;
        var f = fill.rectTransform;
        f.anchorMin = Vector2.zero; f.anchorMax = Vector2.one; f.offsetMin = f.offsetMax = Vector2.zero;
        HoloSkin.Bar(back, fill, HoloUI.Green);
        return f;
    }

    static UpgradeEffects Upgrades(Structure s) => s != null && s.TryGetComponent(out UpgradeEffects fx) ? fx : null;
    static SwarmSilo Silo(Structure s) => s != null && s.TryGetComponent(out SwarmSilo silo) && silo.Built && silo.Team == 0 ? silo : null;

    void ShowSilo(SwarmSilo silo)
    {
        fireButton.gameObject.SetActive(silo != null);
        if (silo == null) return;
        panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, baseHeight + upgradeRowHeight);
        fireButton.interactable = silo.Ready && !SiloTargeting.Active;
        fireFill.fillAmount = silo.Ready ? 0f : silo.Charge01;
        fireText.text = SiloTargeting.Active ? "SELECT TARGET" : silo.Ready ? $"FIRE  [{Controls.Label(Act.FireSilo)}]" : silo.Firing ? "LAUNCHING" : silo.Paused ? $"PAUSED: LOW POWER  {SwarmSilo.Clock(silo.Left)}" : $"CHARGING  {SwarmSilo.Clock(silo.Left)}";
        if (silo.Ready && Controls.Down(Act.FireSilo)) SiloTargeting.Begin(silo, manager);
    }

    void LateUpdate()
    {
        var s = manager.SelectedStructure;
        panel.SetActive(s != null);
        if (s == null) return;

        string power = s.power > 0 ? $"+{s.power}" : s.power.ToString();
        title.text = s.Name.ToUpper();
        stats.text = $"HP {s.Health.Current:0}/{s.Health.MaxHealth:0}    POWER {power}";
        float hp = s.Health.MaxHealth > 0f ? s.Health.Current / s.Health.MaxHealth : 0f;
        hpFill.anchorMax = new Vector2(Mathf.Clamp01(hp), 1f);
        hpFill.GetComponent<Image>().color = hp < 0.3f ? HoloUI.Pink : HoloUI.Green;
        sellButton.interactable = s.CanSell;
        sellText.text = s.CanSell ? $"SELL ${s.SellValue}" : "SELL";
        repairButton.interactable = s.Health.IsDamaged;
        repairText.text = s.Repairing ? "REPAIRING" : "REPAIR";
        ShowUpgrade(Upgrades(s));
        ShowSilo(Silo(s));
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
