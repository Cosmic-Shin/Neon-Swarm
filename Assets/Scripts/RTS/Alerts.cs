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
    [SerializeField] GameObjectEventChannel unitPromoted; // raised by Veterancy

    void OnEnable() => unitPromoted.Raised += Promoted;
    void OnDisable() => unitPromoted.Raised -= Promoted;
    void Promoted(GameObject unit) => Show($"Unit promoted: {unit.GetComponent<Veterancy>().RankName}", Good);

    static Alerts instance;
    static readonly Dictionary<string, float> quietUntil = new(); // throttle per message
    readonly List<(Text text, float born, Color color)> lines = new();
    bool lowPower;

    void Awake()
    {
        instance = this;
        quietUntil.Clear();
        LastSpot = null;
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
        Sfx.Play(color == Good ? Snd.Good : color == Bad ? Snd.Bad : color == Warn ? Snd.Warn : Snd.Info, 0.7f);
        instance.Add(message, color);
    }

    // Where the latest alert with a place happened: Space jumps the camera there (CameraPan).
    public static Vector3? LastSpot { get; private set; }

    // An alert about a place: remembered for Space, and a red pulse on the minimap.
    public static void ShowAt(string message, Color color, Vector3 at, float throttle = 0f)
    {
        if (throttle > 0f && quietUntil.TryGetValue(message, out float t) && Time.time < t) return;
        Show(message, color, throttle);
        LastSpot = at;
        Ping.Active.Add((at, color, Time.time));
    }

    // Health calls this when one of ours is shot.
    public static void Attacked(Health h)
    {
        Vector3 p = h.transform.position;
        if (h.TryGetComponent(out OutpostCapture _)) ShowAt("Outpost under attack", Warn, p, 15f);
        else if (h.TryGetComponent(out Structure _)) ShowAt("Our base is under attack", Bad, p, 12f);
        else if (h.TryGetComponent(out Harvester _)) ShowAt("Harvester under attack", Warn, p, 15f);
        else ShowAt("Unit under attack", Warn, p, 15f);
    }

    // Multiplayer: a teammate's building lost health. Tells us once in a while, with a ping on the spot.
    public static void AllyAttacked(Health h)
    {
        string message = $"Ally {Net.NameOf(h.team)}'s base is under attack";
        if (quietUntil.TryGetValue(message, out float t) && Time.time < t) return;
        Show(message, Warn, 15f);
        Ping.Show(h.transform.position, Net.ColorOf(h.team), null);
    }

    void Add(string message, Color color)
    {
        var line = Instantiate(lineTemplate, lineTemplate.transform.parent);
        line.gameObject.SetActive(true);
        line.text = message;
        line.color = color;
        line.transform.SetSiblingIndex(1); // just under the hidden template: newest on top
        lines.Add((line, Time.time, color));
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
            float pop = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / 0.25f), 3f); // new lines snap open from a white streak
            var c = Color.Lerp(Color.white, lines[i].color, pop);
            c.a = Mathf.Clamp01((showTime - age) / 1f); // fade over the last second
            lines[i].text.color = c;
            lines[i].text.rectTransform.localScale = new Vector3(Mathf.Lerp(1.4f, 1f, pop), Mathf.Lerp(0.15f, 1f, pop), 1f);
            if (age < showTime) continue;
            Destroy(lines[i].text.gameObject);
            lines.RemoveAt(i);
        }
    }
}
