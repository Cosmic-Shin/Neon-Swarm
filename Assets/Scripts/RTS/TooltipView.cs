using UnityEngine;
using UnityEngine.UI;

// The one tooltip box on screen. Shop buttons (Tooltip) ask it to show their text just above them.
public class TooltipView : MonoBehaviour
{
    public static TooltipView Instance { get; private set; }

    [SerializeField] RectTransform box; // pivot bottom-centre, sizes itself to the text
    [SerializeField] Text label;

    void Awake()
    {
        Instance = this;
        box.gameObject.SetActive(false);
    }

    public void Show(string text, RectTransform over)
    {
        label.text = text;
        box.gameObject.SetActive(true);
        Vector3[] corners = new Vector3[4];
        over.GetWorldCorners(corners); // overlay canvas: world = screen pixels
        Vector3 top = (corners[1] + corners[2]) * 0.5f + Vector3.up * 8f;
        LayoutRebuilder.ForceRebuildLayoutImmediate(box);
        float half = box.rect.width * box.lossyScale.x * 0.5f;
        top.x = Mathf.Clamp(top.x, half + 8f, Screen.width - half - 8f); // keep it on screen
        box.position = top;
    }

    public void Hide() => box.gameObject.SetActive(false);
}
