using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Test controls for the outpost demo scene. 1/2 send a squad into the zone, 3 pulls everyone back, G garrisons, E ejects.</summary>
public class OutpostDemo : MonoBehaviour
{
    public OutpostCapture outpost;
    public float moveSpeed = 2.5f;
    readonly Dictionary<TeamMember, Vector3> home = new Dictionary<TeamMember, Vector3>();
    readonly Dictionary<TeamMember, Vector3> target = new Dictionary<TeamMember, Vector3>();
    static readonly int SpeedId = Animator.StringToHash("Speed");

    void Start()
    {
        if (!outpost) outpost = FindFirstObjectByType<OutpostCapture>();
        foreach (var t in FindObjectsByType<TeamMember>(FindObjectsSortMode.None)) { home[t] = t.transform.position; target[t] = t.transform.position; }
    }

    void Send(int team)
    {
        int i = 0;
        foreach (var t in home.Keys)
            if (t && t.team == team)
            {
                float a = (team == 0 ? 200f : 20f) + i++ * 35f;
                target[t] = outpost.transform.position + Quaternion.Euler(0, a, 0) * Vector3.forward * 1.5f;
            }
    }

    void Update()
    {
        bool k1 = false, k2 = false, k3 = false, kg = false, ke = false;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null) { k1 = kb.digit1Key.wasPressedThisFrame; k2 = kb.digit2Key.wasPressedThisFrame; k3 = kb.digit3Key.wasPressedThisFrame; kg = kb.gKey.wasPressedThisFrame; ke = kb.eKey.wasPressedThisFrame; }
#else
        k1 = Input.GetKeyDown(KeyCode.Alpha1); k2 = Input.GetKeyDown(KeyCode.Alpha2); k3 = Input.GetKeyDown(KeyCode.Alpha3); kg = Input.GetKeyDown(KeyCode.G); ke = Input.GetKeyDown(KeyCode.E);
#endif
        if (k1) Send(0);
        if (k2) Send(1);
        if (k3) foreach (var t in home.Keys) if (t) target[t] = home[t];
        if (kg && outpost.IsOwned)
            foreach (var t in home.Keys) if (t && t.gameObject.activeSelf && t.team == outpost.ownerTeam) outpost.TryGarrison(t);
        if (ke) outpost.EjectAll();

        foreach (var kv in target)
        {
            var t = kv.Key;
            if (!t || !t.gameObject.activeInHierarchy) continue;
            Vector3 to = kv.Value - t.transform.position; to.y = 0;
            bool moving = to.magnitude > 0.05f;
            if (moving)
            {
                t.transform.position += to.normalized * Mathf.Min(moveSpeed * Time.deltaTime, to.magnitude);
                t.transform.rotation = Quaternion.RotateTowards(t.transform.rotation, Quaternion.LookRotation(to.normalized), 360f * Time.deltaTime);
            }
            var anim = t.GetComponentInChildren<Animator>();
            if (anim) anim.SetFloat(SpeedId, moving ? 1f : 0f);
        }
    }

    void OnGUI()
    {
        GUI.Label(new Rect(12, 34, 1000, 24), "1: send Hive squad   2: send Aegis squad   3: pull everyone back   G: garrison owner's units   E: eject garrison");
    }
}
