using UnityEngine;

// Panel hugs its buttons but never gets wider than the screen; past that its ScrollRect scrolls.
public class FitWidth : MonoBehaviour
{
    [SerializeField] RectTransform content;
    [SerializeField] float padding = 12f; // frame around the buttons
    [SerializeField] float margin = 24f;  // gap to each screen edge

    void LateUpdate()
    {
        var rt = (RectTransform)transform;
        float max = ((RectTransform)rt.parent).rect.width - margin * 2f;
        rt.sizeDelta = new Vector2(Mathf.Min(content.rect.width + padding, max), rt.sizeDelta.y);
    }
}
