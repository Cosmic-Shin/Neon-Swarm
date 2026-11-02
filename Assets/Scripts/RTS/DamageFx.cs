using UnityEngine;

// Building damage you can see: below half health it smokes and throws sparks, below a quarter it burns.
// Effects sit in the world (not parented, the building's scale would stretch them) and the smoke lingers over the wreck.
[RequireComponent(typeof(Health))]
public class DamageFx : MonoBehaviour
{
    [SerializeField] GameObject smoke; // looping particles
    [SerializeField] GameObject fire;  // looping particles
    [SerializeField] GameObject spark; // short Fx flash, spawned through the pool
    [SerializeField] float smokeLinger = 10f; // seconds the smoke stays after the building dies

    Health health;
    Collider col;
    GameObject smokeObj, fireObj;
    float nextSpark;

    void Awake()
    {
        health = GetComponent<Health>();
        col = GetComponent<Collider>();
    }

    void Update()
    {
        float f = health.Fraction;
        Toggle(ref smokeObj, smoke, f < 0.5f, 0.3f);
        Toggle(ref fireObj, fire, f < 0.25f, -0.3f);
        if (f < 0.5f && spark != null && Time.time >= nextSpark)
        {
            nextSpark = Time.time + Random.Range(0.25f, f < 0.25f ? 0.6f : 1.2f);
            Fx.Spawn(spark, PointOnRoof(Random.Range(-0.4f, 0.4f), Random.Range(-0.4f, 0.4f), Random.Range(0.5f, 1f)), Random.rotation, 1.5f);
        }
    }

    // Spawned on first need, then just switched on/off (repairs put it out). side: where on the roof, -0.5..0.5 of the width.
    void Toggle(ref GameObject obj, GameObject prefab, bool on, float side)
    {
        if (prefab == null) return;
        if (on && obj == null)
        {
            Bounds b = col.bounds;
            obj = Instantiate(prefab, PointOnRoof(side, side * 0.5f, 0.9f), Quaternion.identity);
            obj.transform.localScale *= Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) / 8f, 0.8f, 3f); // bigger buildings, bigger plume
        }
        if (obj != null && obj.activeSelf != on) obj.SetActive(on);
    }

    Vector3 PointOnRoof(float x, float z, float height)
    {
        Bounds b = col.bounds;
        return new Vector3(b.center.x + b.size.x * x, b.min.y + b.size.y * height, b.center.z + b.size.z * z);
    }

    void OnDestroy()
    {
        if (fireObj != null) Destroy(fireObj);
        if (smokeObj != null) Destroy(smokeObj, health != null && health.IsDead ? smokeLinger : 0f);
    }
}
