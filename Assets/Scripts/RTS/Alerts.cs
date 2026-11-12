using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Alerts : MonoBehaviour
{
    public static readonly Color Info = new(0.45f, 1f, 1f), Good = new(0.55f, 1f, 0.3f), Warn = new(1f, 0.85f, 0.2f), Bad = new(1f, 0.3f, 0.35f);

    [SerializeField] Text lineTemplate;
    [SerializeField] float showTime = 4f;
    [SerializeField] int maxLines = 5;
    [SerializeField] GameObjectEventChannel unitPromoted;

    void OnEnable() => unitPromoted.Raised += Promoted;
    void OnDisable() => unitPromoted.Raised -= Promoted;
    void Promoted(GameObject unit) => Show($"Unit promoted: {unit.GetComponent<Veterancy>().RankName}", Good);

    static Alerts instance;
    static readonly Dictionary<string, float> quietUntil = new();
    readonly List<(Text text, Image toast, float born, Color color)> lines = new();
    const float ToastHeight = 48f;
    bool lowPower;

    void Awake()
    {
        instance = this;
        quietUntil.Clear();
        LastSpot = null;
        lineTemplate.gameObject.SetActive(false);
    }

    public static void Show(string message, Color color, float throttle = 0f, bool silent = false)
    {
        if (instance == null) return;
        if (throttle > 0f)
        {
            if (quietUntil.TryGetValue(message, out float t) && Time.time < t) return;
            quietUntil[message] = Time.time + throttle;
        }
        if (!silent) Sfx.Play(color == Good ? Snd.Good : color == Bad ? Snd.Bad : color == Warn ? Snd.Warn : Snd.Info, 0.7f);
        instance.Add(message, color);
    }

    public static Vector3? LastSpot { get; private set; }

    public static void ShowAt(string message, Color color, Vector3 at, float throttle = 0f)
    {
        if (throttle > 0f && quietUntil.TryGetValue(message, out float t) && Time.time < t) return;
        Show(message, color, throttle);
        LastSpot = at;
        Ping.Active.Add((at, color, Time.time));
    }

    public static void Attacked(Health h)
    {
        Vector3 p = h.transform.position;
        if (h.TryGetComponent(out OutpostCapture _)) ShowAt("Outpost under attack", Warn, p, 15f);
        else if (h.TryGetComponent(out Structure _)) { ShowAt("Our base is under attack", Bad, p, 12f); Sfx.Say(Line.UnderAttack, 25f); }
        else if (h.TryGetComponent(out Harvester _)) ShowAt("Harvester under attack", Warn, p, 15f);
        else ShowAt("Unit under attack", Warn, p, 15f);
    }

    public static void AllyAttacked(Health h)
    {
        string message = $"Ally {Net.NameOf(h.team)}'s base is under attack";
        if (quietUntil.TryGetValue(message, out float t) && Time.time < t) return;
        Show(message, Warn, 15f);
        Ping.Show(h.transform.position, Net.ColorOf(h.team), null);
    }

    void Add(string message, Color color)
    {
        var box = new GameObject("Toast", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
        box.SetParent(lineTemplate.transform.parent, false);
        box.SetSiblingIndex(1);
        box.sizeDelta = new Vector2(((RectTransform)lineTemplate.transform).sizeDelta.x, ToastHeight);
        var layout = box.GetComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = ToastHeight;

        var toast = new GameObject("Back", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        toast.transform.SetParent(box, false);
        toast.raycastTarget = false;
        HoloUI.SetSliced(toast, color == Good ? "Toast_Green" : color == Bad || color == Warn ? "Toast_Pink" : "Toast_Cyan");

        var line = Instantiate(lineTemplate, box);
        line.gameObject.SetActive(true);
        line.text = message;
        line.color = color;
        var lr = line.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
        toast.rectTransform.sizeDelta = new Vector2(Mathf.Min(line.preferredWidth + 96f, box.sizeDelta.x), ToastHeight);

        lines.Add((line, toast, Time.time, color));
        if (lines.Count > maxLines) { Destroy(lines[0].text.transform.parent.gameObject); lines.RemoveAt(0); }
    }

    void Update()
    {
        if (Structure.LowPower != lowPower)
        {
            lowPower = Structure.LowPower;
            if (lowPower) { Show("Low power: build a Power Pylon", Bad); Sfx.Play(Snd.PowerDown, 0.8f); }
            else Show("Power restored", Good);
        }

        for (int i = lines.Count - 1; i >= 0; i--)
        {
            float age = Time.time - lines[i].born;
            float pop = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / 0.25f), 3f);
            var c = Color.Lerp(Color.white, lines[i].color, pop);
            c.a = Mathf.Clamp01((showTime - age) / 1f);
            lines[i].text.color = c;
            lines[i].toast.color = new Color(1f, 1f, 1f, c.a);
            lines[i].text.transform.parent.localScale = new Vector3(Mathf.Lerp(1.4f, 1f, pop), Mathf.Lerp(0.15f, 1f, pop), 1f);
            if (age < showTime) continue;
            Destroy(lines[i].text.transform.parent.gameObject);
            lines.RemoveAt(i);
        }
    }
}
