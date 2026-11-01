using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Top-centre message feed ("Low power", "Our base is under attack", "Unit ready"...). Newest at the top,
// each line fades out after a few seconds. Noisy events (attacks, losses) are throttled.
public class Alerts : MonoBehaviour
{
    public static readonly Color Info = new(0.45f, 1f, 1f), Good = new(0.55f, 1f, 0.3f), Warn = new(1f, 0.85f, 0.2f), Bad = new(1f, 0.3f, 0.35f);

    [SerializeField] Text lineTemplate; // disabled; cloned per message inside a VerticalLayoutGroup
    [SerializeField] float showTime = 4f;
    [SerializeField] int maxLines = 5;

    static Alerts instance;
    static readonly Dictionary<string, float> quietUntil = new(); // throttle per message
    readonly List<(Text text, float born)> lines = new();
    bool lowPower;

    void Awake()
    {
        instance = this;
        quietUntil.Clear();
        lineTemplate.gameObject.SetActive(false);
    }

    // throttle: seconds before the same message can show again.
    public static void Show(string message, Color color, float throttle = 0f)
    {
        if (instance == null) return;
        if (throttle > 0f)
        {
            if (quietUntil.TryGetValue(message, out float t) && Time.time < t) return;
            quietUntil[message] = Time.time + throttle;
        }
        instance.Add(message, color);
    }

    // Health calls this when one of ours is shot.
    public static void Attacked(Health h)
    {
        if (h.TryGetComponent(out OutpostCapture _)) Show("Outpost under attack", Warn, 15f);
        else if (h.TryGetComponent(out Structure _)) Show("Our base is under attack", Bad, 12f);
        else if (h.TryGetComponent(out Harvester _)) Show("Harvester under attack", Warn, 15f);
        else Show("Unit under attack", Warn, 15f);
    }

    void Add(string message, Color color)
    {
        var line = Instantiate(lineTemplate, lineTemplate.transform.parent);
        line.gameObject.SetActive(true);
        line.text = message;
        line.color = color;
        line.transform.SetSiblingIndex(1); // just under the hidden template: newest on top
        lines.Add((line, Time.time));
        if (lines.Count > maxLines) { Destroy(lines[0].text.gameObject); lines.RemoveAt(0); }
    }

    void Update()
    {
        if (Structure.LowPower != lowPower)
        {
            lowPower = Structure.LowPower;
            if (lowPower) Show("Low power: build a Power Pylon", Bad);
            else Show("Power restored", Good);
        }

        for (int i = lines.Count - 1; i >= 0; i--)
        {
            float age = Time.time - lines[i].born;
            var c = lines[i].text.color;
            c.a = Mathf.Clamp01((showTime - age) / 1f); // fade over the last second
            lines[i].text.color = c;
            if (age < showTime) continue;
            Destroy(lines[i].text.gameObject);
            lines.RemoveAt(i);
        }
    }
}
