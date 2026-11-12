using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SelectionPanel : MonoBehaviour
{
    [SerializeField] GoodSelectionManager manager;
    [SerializeField] Button cellTemplate;

    readonly List<Button> cells = new();
    readonly List<UnitData> order = new();
    readonly Dictionary<UnitData, (int count, float health)> groups = new();

    static readonly Color High = new(0.2f, 1f, 1f), Mid = new(1f, 0.9f, 0.2f), Low = new(1f, 0.2f, 0.6f);

    void Awake() => cellTemplate.gameObject.SetActive(false);

    void LateUpdate()
    {
        order.Clear();
        groups.Clear();
        foreach (var u in manager.SelectedUnits)
        {
            if (u == null || !u.isActiveAndEnabled || !u.TryGetComponent(out Health h)) continue;
            if (!groups.TryGetValue(u.Data, out var g)) { order.Add(u.Data); g = (0, 0f); }
            groups[u.Data] = (g.count + 1, g.health + h.Fraction);
        }

        while (cells.Count < order.Count)
        {
            var cell = Instantiate(cellTemplate, cellTemplate.transform.parent);
            cells.Add(cell);
        }
        for (int i = 0; i < cells.Count; i++)
        {
            bool used = i < order.Count;
            cells[i].gameObject.SetActive(used);
            if (!used) continue;
            var type = order[i];
            var (count, health) = groups[type];
            float avg = health / count;
            cells[i].GetComponent<Image>().sprite = type.icon;
            cells[i].transform.Find("Count").GetComponent<Text>().text = count > 1 ? $"x{count}" : "";
            var bar = cells[i].transform.Find("Health").GetComponent<Image>();
            bar.fillAmount = avg;
            bar.color = avg > 0.5f ? High : avg > 0.25f ? Mid : Low;
            cells[i].onClick.RemoveAllListeners();
            cells[i].onClick.AddListener(() => manager.SelectOnly(type));
        }
    }
}
