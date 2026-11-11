using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Moved out of HoloUI.cs unchanged: a MonoBehaviour needs its own file (same name) to be saved in scenes.
/// <summary>Swaps a button's label colour so text stays readable on the bright pressed sprite.</summary>
public class HoloLabelColor : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public Color normal = new Color32(237, 239, 247, 255);
    public Color pressed = new Color32(4, 18, 26, 255);
    Graphic label;

    void Awake() => Refresh();

    public void Refresh()
    {
        if (!label)
            foreach (var g in GetComponentsInChildren<Graphic>(true))
                if (g.gameObject != gameObject && !(g is Image)) { label = g; break; }
        if (label) label.color = normal;
    }

    public void OnPointerDown(PointerEventData e) { if (label) label.color = pressed; }
    public void OnPointerUp(PointerEventData e) { if (label) label.color = normal; }
    public void OnPointerExit(PointerEventData e) { if (label) label.color = normal; }
}
