using UnityEngine;

public class CreditsPopup : MonoBehaviour
{
    const float Life = 1.6f, Rise = 2.5f;

    TextMesh text;
    Color color;
    float born;

    public float Age => Time.time - born;

    public void Restart(string message)
    {
        text.text = message;
        born = Time.time - 0.05f;
    }

    public static CreditsPopup Show(Vector3 at, string message, Color color)
    {
        var go = new GameObject("CreditsPopup");
        go.transform.position = at;
        var t = go.AddComponent<TextMesh>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        go.GetComponent<MeshRenderer>().sharedMaterial = t.font.material;
        t.text = message;
        t.fontSize = 64;
        t.characterSize = 0.14f;
        t.anchor = TextAnchor.MiddleCenter;
        t.fontStyle = FontStyle.Bold;
        var p = go.AddComponent<CreditsPopup>();
        p.text = t;
        p.color = Color.Lerp(color, Color.white, 0.1f);
        p.born = Time.time;
        p.LateUpdate();
        return p;
    }

    void LateUpdate()
    {
        float age = Time.time - born;
        if (age >= Life) { Destroy(gameObject); return; }
        transform.position += Vector3.up * Rise * Time.deltaTime * (1f - age / Life);
        if (Camera.main != null) transform.rotation = Camera.main.transform.rotation;
        float pop = age < 0.15f ? Mathf.Lerp(0.5f, 1.2f, age / 0.15f) : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01((age - 0.15f) / 0.2f));
        transform.localScale = Vector3.one * pop;
        var c = color;
        c.a = Mathf.Clamp01((Life - age) / 0.5f);
        text.color = c;
    }
}
