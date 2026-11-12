using UnityEngine;

public class FitWidth : MonoBehaviour
{
    [SerializeField] RectTransform content;
    [SerializeField] float padding = 12f;
    [SerializeField] float margin = 24f;

    void LateUpdate()
    {
        var rt = (RectTransform)transform;
        float max = ((RectTransform)rt.parent).rect.width - margin * 2f;
        rt.sizeDelta = new Vector2(Mathf.Min(content.rect.width + padding, max), rt.sizeDelta.y);
    }
}
