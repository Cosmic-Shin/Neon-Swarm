using UnityEngine;
using UnityEngine.UI;

public class SortLayer : MonoBehaviour
{
    public int order;

    void OnEnable()
    {
        if (!TryGetComponent(out Canvas canvas)) canvas = gameObject.AddComponent<Canvas>();
        if (!TryGetComponent(out GraphicRaycaster _)) gameObject.AddComponent<GraphicRaycaster>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = order;
    }
}
