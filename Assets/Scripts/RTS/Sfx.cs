using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum Snd { Laser, Blaster, Cannon, Flame, Zap, BoomSmall, BoomBig, Click, Ack, Good, Bad, Warn, Info, Coin, Promote }

// All game audio, synthesised in code at startup (no sound files): weapon shots, explosions, UI clicks,
// order chirps, alert tones and a looping synthwave track. One object lives across scenes and carries
// the only AudioListener. World sounds are 2D, quieter the further they are from the camera's view,
// and silent under the fog of war.
public class Sfx : MonoBehaviour
{
    const int Rate = 22050, Voices = 24, SameAtOnce = 3;
    const float MinGap = 0.05f; // the same sound can't restart faster than this (20 Skitters = one rattle)

    static Sfx I;
    AudioClip[] clips;
    readonly AudioSource[] voices = new AudioSource[Voices];
    readonly Snd[] voiceSnd = new Snd[Voices];
    float[] lastPlayed;
    AudioSource music;
    readonly List<RaycastResult> uiHits = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        var go = new GameObject("Audio");
        DontDestroyOnLoad(go);
        go.AddComponent<Sfx>();
    }

    void Awake()
    {
        I = this;
        gameObject.AddComponent<AudioListener>();
        gameObject.AddComponent<UIJuice>(); // button hover / flash, credits "can't afford" pulse
        SceneManager.sceneLoaded += (_, _) => { foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) if (l.gameObject != gameObject) Destroy(l); };
        for (int i = 0; i < Voices; i++) { voices[i] = gameObject.AddComponent<AudioSource>(); voices[i].playOnAwake = false; }
        clips = Build();
        lastPlayed = new float[clips.Length];
        for (int i = 0; i < lastPlayed.Length; i++) lastPlayed[i] = -1f;
        music = gameObject.AddComponent<AudioSource>();
        music.clip = Music();
        music.loop = true;
        music.Play();
        GameSettings.Changed += ApplyVolume;
        GameSettings.Apply();
    }

    void OnDestroy() => GameSettings.Changed -= ApplyVolume;

    void ApplyVolume() => music.volume = 0.45f * GameSettings.Music;

    // ---- playing ----

    // A sound with no place in the world (UI, alerts).
    public static void Play(Snd s, float volume = 1f, float pitch = 1f) { if (I != null) I.Voice(s, volume, pitch); }

    // A sound at a world spot: skipped off-screen or in fog, fades with distance from the view centre.
    public static void At(Snd s, Vector3 at, float volume = 1f)
    {
        if (I == null || !Juice.Seen(at)) return;
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 v = cam.WorldToViewportPoint(at);
        if (v.z < 0f || v.x < -0.25f || v.x > 1.25f || v.y < -0.25f || v.y > 1.25f) return;
        Vector3 focus = cam.transform.position + cam.transform.forward * 30f; // same view centre as Juice.Shake
        float d = Vector2.Distance(new Vector2(at.x, at.z), new Vector2(focus.x, focus.z));
        float k = Mathf.Clamp01(1.15f - d / 70f);
        if (k > 0.05f) I.Voice(s, volume * k, Random.Range(0.92f, 1.08f));
    }

    void Voice(Snd s, float volume, float pitch)
    {
        int id = (int)s;
        float now = Time.unscaledTime;
        if (now - lastPlayed[id] < MinGap) return;
        int same = 0, free = -1;
        for (int i = 0; i < Voices; i++)
        {
            if (!voices[i].isPlaying) { if (free < 0) free = i; continue; }
            if (voiceSnd[i] == s && ++same >= SameAtOnce) return;
        }
        if (free < 0) return; // every voice busy: drop it, the mix is full anyway
        lastPlayed[id] = now;
        var a = voices[free];
        voiceSnd[free] = s;
        a.clip = clips[id];
        a.volume = Mathf.Clamp01(volume) * GameSettings.Effects;
        a.pitch = pitch;
        a.Play();
    }

    // Weapon sound for a unit's or turret's shot.
    public static Snd ShotOf(float damage, float splash, bool electric) =>
        electric ? Snd.Zap : damage >= 50f || splash > 0f ? Snd.Cannon : damage >= 15f ? Snd.Blaster : Snd.Laser;

    // UI click: any button, toggle or slider pressed, in every scene, including buttons made at runtime.
    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame || EventSystem.current == null) return;
        uiHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = mouse.position.ReadValue() }, uiHits);
        if (uiHits.Count > 0 && uiHits[0].gameObject.GetComponentInParent<Selectable>() is Selectable sel && sel.IsInteractable())
            Play(Snd.Click, 0.6f);
    }

    // ---- synthesis ----

    static readonly System.Random rng = new(7);
    static float Noise() => (float)rng.NextDouble() * 2f - 1f;
    const float Tau = Mathf.PI * 2f;

    delegate float Wave(float t, ref float phase, ref float low);

    static AudioClip Make(string name, float seconds, Wave wave)
    {
        int n = (int)(seconds * Rate);
        var data = new float[n];
        float phase = 0f, low = 0f, peak = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            data[i] = wave(i / (float)Rate, ref phase, ref low);
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }
        float fadeOut = Mathf.Min(n, Rate / 100); // last 10 ms to zero: no click
        for (int i = 0; i < n; i++) data[i] *= 0.85f / peak * Mathf.Clamp01((n - i) / fadeOut);
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Tone(float f, float t) => Mathf.Sin(Tau * f * t);

    // Short notes one after another (alert jingles); each note is a soft bell.
    static AudioClip Notes(string name, float noteLength, float decay, bool square, params float[] freqs) =>
        Make(name, noteLength * freqs.Length + 0.15f, (float t, ref float p, ref float l) =>
        {
            int i = Mathf.Min((int)(t / noteLength), freqs.Length - 1);
            float tn = t - i * noteLength, f = freqs[i];
            float s = square ? Mathf.Sign(Tone(f, t)) * 0.5f : Tone(f, t) + 0.3f * Tone(f * 2f, t);
            l += 0.3f * (s - l); // soften the square's edges
            return l * Mathf.Exp(-tn * decay) * Mathf.Clamp01(tn * 400f);
        });

    static AudioClip[] Build() => new[]
    {
        // Laser: a quick falling "pew"
        Make("Laser", 0.15f, (float t, ref float p, ref float l) =>
        {
            p += Tau * (300f + 1600f * Mathf.Exp(-t * 18f)) / Rate;
            return (Mathf.Sin(p) * 0.7f + Mathf.Sign(Mathf.Sin(p)) * 0.15f) * Mathf.Exp(-t * 22f);
        }),
        // Blaster: lower, buzzier bolt
        Make("Blaster", 0.22f, (float t, ref float p, ref float l) =>
        {
            p += Tau * (150f + 700f * Mathf.Exp(-t * 12f)) / Rate;
            float s = Mathf.Sign(Mathf.Sin(p)) * 0.5f + Mathf.Sin(p) * 0.4f + Noise() * 0.15f;
            l += 0.35f * (s - l);
            return l * Mathf.Exp(-t * 13f);
        }),
        // Cannon: noise crack over a low thump
        Make("Cannon", 0.6f, (float t, ref float p, ref float l) =>
        {
            l += 0.15f * (Noise() - l);
            p += Tau * (55f + 90f * Mathf.Exp(-t * 20f)) / Rate;
            return l * 1.6f * Mathf.Exp(-t * 9f) + Mathf.Sin(p) * Mathf.Exp(-t * 9f);
        }),
        // Flame: a breathy roar with crackles
        Make("Flame", 0.55f, (float t, ref float p, ref float l) =>
        {
            l += 0.25f * (Noise() - l);
            float crackle = rng.NextDouble() < 0.002 ? Noise() * 3f : 0f;
            return (l + crackle * 0.3f) * Mathf.Clamp01(t * 20f) * Mathf.Clamp01((0.55f - t) * 5f);
        }),
        // Zap: electric crackle
        Make("Zap", 0.3f, (float t, ref float p, ref float l) =>
        {
            if (((int)(t * 120f) * 7919) % 3 == 0) l = Noise(); // gate jumps every ~8 ms
            return (Tone(2400f + l * 600f, t) * 0.6f + Noise() * 0.4f) * Mathf.Abs(l) * Mathf.Exp(-t * 9f);
        }),
        // Small explosion: unit death, shell impact
        Make("BoomSmall", 0.9f, (float t, ref float p, ref float l) =>
        {
            l += Mathf.Lerp(0.4f, 0.04f, t / 0.9f) * (Noise() - l);
            p += Tau * (45f + 60f * Mathf.Exp(-t * 15f)) / Rate;
            return l * 2f * Mathf.Exp(-t * 5f) + Mathf.Sin(p) * 0.8f * Mathf.Exp(-t * 8f);
        }),
        // Big explosion: building destroyed (long rumble)
        Make("BoomBig", 2.2f, (float t, ref float p, ref float l) =>
        {
            l += Mathf.Lerp(0.3f, 0.015f, t / 2.2f) * (Noise() - l);
            p += Tau * (30f + 50f * Mathf.Exp(-t * 8f)) / Rate;
            return l * 2.5f * Mathf.Exp(-t * 2f) + Mathf.Sin(p) * Mathf.Exp(-t * 3f);
        }),
        Make("Click", 0.05f, (float t, ref float p, ref float l) => Tone(1100f, t) * Mathf.Exp(-t * 110f)),
        Notes("Ack", 0.05f, 30f, false, 880f, 1320f),
        Notes("Good", 0.09f, 12f, false, 523.3f, 659.3f, 784f),
        Notes("Bad", 0.15f, 6f, true, 440f, 330f),
        Notes("Warn", 0.1f, 18f, true, 620f, 620f),
        Notes("Info", 0.12f, 20f, false, 990f),
        Notes("Coin", 0.07f, 14f, false, 1318.5f, 1760f, 2093f),
        // Promotion: rising shimmer
        Make("Promote", 0.6f, (float t, ref float p, ref float l) =>
        {
            p += Tau * (400f + 1800f * t) / Rate;
            return (Mathf.Sin(p) + 0.4f * Mathf.Sin(p * 1.5f)) * Mathf.Clamp01(t * 30f) * Mathf.Exp(-t * 3f);
        }),
    };

    // 20 s synthwave loop in A minor, 96 BPM: pad chords, pulsing bass, plucked arpeggio, kick and hats.
    static AudioClip Music()
    {
        const float beat = 60f / 96f, bar = beat * 4f;
        float[][] chords = { new[] { 220f, 261.6f, 329.6f }, new[] { 174.6f, 220f, 261.6f }, new[] { 261.6f, 329.6f, 392f }, new[] { 196f, 246.9f, 293.7f } }; // Am F C G
        float[] roots = { 55f, 43.65f, 65.41f, 49f };
        int[] arp = { 0, 1, 2, 1, 2, 1, 0, 2 };
        int n = (int)(bar * 8f * Rate);
        var data = new float[n];
        float low = 0f, hatLow = 0f, peak = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            int b = (int)(t / bar), c = (b / 2) % 4;
            float tb = t - (b / 2) * 2f * bar; // time in this chord (2 bars)
            float padEnv = Mathf.Clamp01(tb / 0.4f) * Mathf.Clamp01((2f * bar - tb) / 0.4f);
            float s = 0f;
            foreach (float f in chords[c]) s += (Tone(f, t) + Tone(f * 1.004f, t) + 0.3f * Tone(f * 2f, t)) * 0.07f * padEnv;

            float e8 = beat / 2f, tn = t % e8; // bass: eighth notes on the root
            float rf = roots[c] * 2f, bassRaw = 0f;
            for (int h = 1; h <= 5; h++) bassRaw += Tone(rf * h, t) / h; // saw-ish
            low += 0.12f * (bassRaw - low);
            s += low * 0.22f * Mathf.Exp(-tn * 5f) * Mathf.Clamp01(tn * 300f);

            float e16 = beat / 4f, ta = t % e16; // arpeggio: sixteenths, chord tones an octave up
            int step = (int)(t / e16);
            s += Tone(chords[c][arp[step % arp.Length]] * 2f, t) * 0.09f * Mathf.Exp(-ta * 16f) * Mathf.Clamp01(ta * 400f);

            float tk = t % beat; // kick on every beat
            s += Mathf.Sin(Tau * (45f * tk + 40f * (1f - Mathf.Exp(-tk * 25f)) / 25f)) * 0.4f * Mathf.Exp(-tk * 10f);

            float th = (t + e8) % beat; // hat on the off-beats
            float nz = Noise(); hatLow += 0.5f * (nz - hatLow);
            s += (nz - hatLow) * 0.08f * Mathf.Exp(-th * 50f);

            data[i] = s;
            peak = Mathf.Max(peak, Mathf.Abs(s));
        }
        for (int i = 0; i < n; i++) data[i] *= 0.8f / peak;
        var clip = AudioClip.Create("Music", n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
