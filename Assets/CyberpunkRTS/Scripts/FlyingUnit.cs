using UnityEngine;

/// <summary>
/// Air unit movement: holds a cruise altitude, flies to a destination, banks into turns,
/// and keeps a soft shadow on the ground under it so players can read where it is.
/// Drives the Animator "Speed" parameter (Fly clip when moving, Hover clip when idle).
/// </summary>
public class FlyingUnit : MonoBehaviour
{
    public float cruiseAltitude = 2.5f;
    public float speed = 4f;
    public float turnSpeed = 160f;
    public float climbRate = 1.6f;
    public float bankAngle = 22f;
    public float arriveDistance = 0.3f;
    [Tooltip("Set to 180 if the model flies backwards in your project.")]
    public float modelYawOffset = 0f;
    public bool showShadow = true;
    public float shadowScale = 1f;

    public bool IsMoving { get; private set; }
    public bool IsAirborne => !takingOff;

    Vector3? destination; bool takingOff;
    float yaw, bank;
    Animator anim; Transform shadow;
    static GameObject shadowPrefab; static bool loaded;
    static readonly int SpeedId = Animator.StringToHash("Speed");

    void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        yaw = transform.eulerAngles.y;
        if (!loaded) { loaded = true; shadowPrefab = Resources.Load<GameObject>("Air/AirShadow"); }
        if (showShadow && shadowPrefab) { shadow = Instantiate(shadowPrefab).transform; shadow.name = name + "_Shadow"; }
    }
    void OnEnable() { if (shadow) shadow.gameObject.SetActive(true); }
    void OnDisable() { if (shadow) shadow.gameObject.SetActive(false); }
    void OnDestroy() { if (shadow) Destroy(shadow.gameObject); }

    public void SetDestination(Vector3 p) { destination = p; }
    public void Stop() { destination = null; }
    /// <summary>Climb to cruise altitude before moving (used after launching from an Aerie).</summary>
    public void TakeOff() { takingOff = true; yaw = transform.eulerAngles.y; }

    void Update()
    {
        Vector3 pos = transform.position;
        pos.y = Mathf.MoveTowards(pos.y, cruiseAltitude, climbRate * Time.deltaTime);
        if (takingOff && pos.y >= cruiseAltitude * 0.6f) takingOff = false;

        float turn = 0f; IsMoving = false;
        if (destination.HasValue && !takingOff)
        {
            Vector3 to = destination.Value - pos; to.y = 0f;
            float dist = to.magnitude;
            if (dist > arriveDistance)
            {
                float want = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg + modelYawOffset;
                float next = Mathf.MoveTowardsAngle(yaw, want, turnSpeed * Time.deltaTime);
                turn = Mathf.DeltaAngle(yaw, next) / Mathf.Max(Time.deltaTime, 1e-4f) / turnSpeed;
                yaw = next;
                Vector3 fwd = Quaternion.Euler(0f, yaw - modelYawOffset, 0f) * Vector3.forward;
                float facing = Mathf.Clamp01(Vector3.Dot(fwd, to / dist) * 0.5f + 0.5f);
                pos += fwd * Mathf.Min(speed * facing * Time.deltaTime, dist);
                IsMoving = true;
            }
            else destination = null;
        }
        bank = Mathf.Lerp(bank, -turn * bankAngle, 5f * Time.deltaTime);
        transform.position = pos;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, bank);
        if (anim) anim.SetFloat(SpeedId, IsMoving ? 1f : 0f);

        if (shadow)
        {
            shadow.position = new Vector3(pos.x, 0.01f, pos.z);
            float s = 1f / (1f + 0.15f * Mathf.Max(0f, pos.y));
            shadow.localScale = Vector3.one * s * 1.4f * shadowScale;
        }
    }
}
