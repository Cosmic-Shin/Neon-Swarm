using UnityEngine;

public class DeathFlash : MonoBehaviour
{
    [SerializeField] float duration = 0.5f;
    [SerializeField] float endScale = 4f;
    public float size = 1f;

    Light flash;
    float intensity;
    float start;

    void Awake()
    {
        flash = GetComponentInChildren<Light>();
        intensity = flash.intensity * GameSettings.FlashScale;
        start = Time.time;
    }

    void Update()
    {
        float k = (Time.time - start) / duration;
        if (k >= 1f) { Destroy(gameObject); return; }
        float easeOut = 1f - (1f - k) * (1f - k);
        transform.localScale = Vector3.one * Mathf.Lerp(0.3f, endScale, easeOut) * size;
        flash.intensity = intensity * (1f - k) * Mathf.Min(1f, size);
    }
}
