using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum Snd
{
    Laser, Blaster, Cannon, Flame, Zap, BoomSmall, BoomBig, Click, Ack, Good, Bad, Warn, Info, Coin, Promote,
    Select, Place, UnitReady, Deposit, WallBreak, Hover, Weld, Ability, Vent, Emp, Cloak, Load, Unload, PowerDown, Hit,
    Footstep, Mining, Ping, Chat, OutpostWon, OutpostLost,
}

public enum Line
{
    Ready, Built, UnderAttack, Destroyed, Research, Upgrade, Start, Hurry, TimeUp, Win, Lose,
}

public class Sfx : MonoBehaviour
{
    const int Voices = 24, SameAtOnce = 3;
    const float MinGap = 0.05f;

    static Sfx I;
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
        gameObject.AddComponent<UIJuice>();
        SceneManager.sceneLoaded += (_, _) => { foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) if (l.gameObject != gameObject) Destroy(l); };
        for (int i = 0; i < Voices; i++) { voices[i] = gameObject.AddComponent<AudioSource>(); voices[i].playOnAwake = false; }
        int count = System.Enum.GetValues(typeof(Snd)).Length;
        variants = new AudioClip[count][];
        foreach (var (snd, names) in Files)
        {
            variants[(int)snd] = System.Array.FindAll(System.Array.ConvertAll(names, n => Resources.Load<AudioClip>("Sfx/" + n)), c => c != null);
            if (variants[(int)snd].Length == 0) Debug.LogWarning($"[Sfx] no files for {snd}");
        }
        lastPlayed = new float[count];
        for (int i = 0; i < lastPlayed.Length; i++) lastPlayed[i] = -1f;
        music = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.ignoreListenerPause = true;
        rain = gameObject.AddComponent<AudioSource>();
        rain.playOnAwake = false;
        rain.loop = true;
        rain.clip = Resources.Load<AudioClip>("Sfx/rain_loop");
        thunder = Resources.Load<AudioClip>("Sfx/thunder_far");
        engine = gameObject.AddComponent<AudioSource>();
        engine.playOnAwake = false;
        engine.loop = true;
        engine.clip = Resources.Load<AudioClip>("Sfx/spaceEngineSmall_000");
        announcer = gameObject.AddComponent<AudioSource>();
        announcer.playOnAwake = false;
        announcer.ignoreListenerPause = true;
        lines = System.Array.ConvertAll(LineFiles, f => Resources.Load<AudioClip>("Sfx/Voice/" + f));
        SceneManager.sceneLoaded += (s, _) => { PickMusic(s.buildIndex == 0); inMatch = s.buildIndex != 0; System.Array.Clear(lineReady, 0, lineReady.Length); };
        GameSettings.Changed += ApplyVolume;
        GameSettings.Apply();
    }

    void OnDestroy() => GameSettings.Changed -= ApplyVolume;

    void ApplyVolume() => music.volume = 0.45f * GameSettings.Music * fade * duck;


    static readonly (Snd, string[])[] Files =
    {
        (Snd.Laser, new[] { "laserSmall_000", "laserSmall_001", "laserSmall_002", "laserSmall_003", "laserSmall_004" }),
        (Snd.Blaster, new[] { "laserRetro_000", "laserRetro_001", "laserRetro_002", "laserRetro_003", "laserRetro_004" }),
        (Snd.Cannon, new[] { "laserLarge_000", "laserLarge_001", "laserLarge_002", "laserLarge_003", "laserLarge_004" }),
        (Snd.Flame, new[] { "thrusterFire_000", "thrusterFire_001", "thrusterFire_002", "thrusterFire_003", "thrusterFire_004" }),
        (Snd.Zap, new[] { "glitch_001", "glitch_003", "glitch_004" }),
        (Snd.BoomSmall, new[] { "explosionCrunch_003", "explosionCrunch_004" }),
        (Snd.BoomBig, new[] { "lowFrequency_explosion_000", "lowFrequency_explosion_001" }),
        (Snd.Click, new[] { "click_002", "click_003" }),
        (Snd.Ack, new[] { "switch_002", "switch_003" }),
        (Snd.Good, new[] { "confirmation_001", "confirmation_003" }),
        (Snd.Bad, new[] { "error_004", "error_006" }),
        (Snd.Warn, new[] { "question_001" }),
        (Snd.Info, new[] { "bong_001" }),
        (Snd.Promote, new[] { "maximize_008" }),
        (Snd.Coin, new[] { "handleCoins" }),
        (Snd.Select, new[] { "select_002", "select_003" }),
        (Snd.Hover, new[] { "tick_001", "tick_002" }),
        (Snd.Deposit, new[] { "confirmation_002" }),
        (Snd.PowerDown, new[] { "minimize_009" }),
        (Snd.Cloak, new[] { "glitch_002" }),
        (Snd.Place, new[] { "impactMetal_heavy_000", "impactMetal_heavy_001", "impactMetal_heavy_002" }),
        (Snd.Weld, new[] { "impactMetal_light_000", "impactMetal_light_001", "impactMetal_light_002", "impactMetal_light_003", "impactMetal_light_004" }),
        (Snd.UnitReady, new[] { "forceField_000", "forceField_001" }),
        (Snd.Ability, new[] { "forceField_002" }),
        (Snd.Vent, new[] { "computerNoise_000", "computerNoise_001" }),
        (Snd.Emp, new[] { "forceField_003", "forceField_004" }),
        (Snd.WallBreak, new[] { "explosionCrunch_000", "explosionCrunch_001", "explosionCrunch_002" }),
        (Snd.Load, new[] { "doorClose_000", "doorClose_001" }),
        (Snd.Unload, new[] { "doorOpen_000", "doorOpen_001" }),
        (Snd.Hit, new[] { "impactMetal_000", "impactMetal_001", "impactMetal_002", "impactMetal_003", "impactMetal_004" }),
        (Snd.Footstep, new[] { "footstep_concrete_000", "footstep_concrete_001", "footstep_concrete_002", "footstep_concrete_003", "footstep_concrete_004" }),
        (Snd.Mining, new[] { "impactMining_000", "impactMining_001", "impactMining_002", "impactMining_003", "impactMining_004" }),
        (Snd.Ping, new[] { "pluck_001" }),
        (Snd.Chat, new[] { "drop_001" }),
        (Snd.OutpostWon, new[] { "maximize_006" }),
        (Snd.OutpostLost, new[] { "minimize_006" }),
    };
    AudioClip[][] variants;


    AudioSource rain;
    AudioClip thunder;
    bool inMatch;

    void UpdateRain()
    {
        bool on = inMatch && GameSettings.Rain && rain.clip != null;
        float target = on ? 0.22f * GameSettings.Effects : 0f;
        rain.volume = Mathf.MoveTowards(rain.volume, target, Time.unscaledDeltaTime * 0.15f);
        if (target > 0f && !rain.isPlaying) rain.Play();
        else if (target <= 0f && rain.volume <= 0f && rain.isPlaying) rain.Stop();
        if (thunderAt > 0f && Time.time >= thunderAt) { thunderAt = 0f; if (on && thunder != null) rain.PlayOneShot(thunder, thunderLoud); }
        engine.volume = Mathf.MoveTowards(engine.volume, inMatch ? EngineLevel * 0.3f * GameSettings.Effects : 0f, Time.unscaledDeltaTime * 0.5f);
        if (engine.volume > 0f && !engine.isPlaying && engine.clip != null) engine.Play();
        else if (engine.volume <= 0f && engine.isPlaying) engine.Stop();
    }

    float thunderAt, thunderLoud;

    public static void Thunder(float delay, float loudness) { if (I != null) { I.thunderAt = Time.time + delay; I.thunderLoud = loudness; } }


    public static float EngineLevel;
    AudioSource engine;


    static readonly string[] LineFiles = { "ready", "objective_achieved", "war_call_for_backup", "war_target_destroyed", "level_up", "power_up", "war_go_go_go", "hurry_up", "time_over", "you_win", "you_lose" };
    AudioSource announcer;
    AudioClip[] lines;
    readonly float[] lineReady = new float[LineFiles.Length];

    public static void Say(Line line, float cooldown = 10f)
    {
        if (I == null) return;
        int i = (int)line;
        bool final = line == Line.Win || line == Line.Lose;
        if (I.lines[i] == null || Time.unscaledTime < I.lineReady[i] || (I.announcer.isPlaying && !final)) return;
        I.lineReady[i] = Time.unscaledTime + cooldown;
        I.announcer.Stop();
        I.announcer.clip = I.lines[i];
        I.announcer.volume = 0.9f * GameSettings.Effects;
        I.announcer.Play();
    }

    public static void Stinger(bool won) { if (I != null) I.StartCoroutine(I.PlayStinger(won)); }

    System.Collections.IEnumerator PlayStinger(bool won)
    {
        duck = 0.25f;
        var jingle = Resources.Load<AudioClip>(won ? "Sfx/jingles_STEEL15" : "Sfx/jingles_STEEL01");
        if (jingle != null) { announcer.PlayOneShot(jingle, GameSettings.Effects); yield return new WaitForSecondsRealtime(jingle.length); }
        lineReady[(int)(won ? Line.Win : Line.Lose)] = 0f;
        Say(won ? Line.Win : Line.Lose);
        yield return new WaitForSecondsRealtime(3f);
        duck = 1f;
    }

    float duck = 1f;


    const string MenuTrack = "MiningbyMoonlight";
    static readonly string[] MatchTracks = { "CyborgNinja", "VoxelRevolution", "SpaceFighterLoop", "Ouroboros" };
    public static readonly string[] Credits = { "Mining by Moonlight", "Cyborg Ninja", "Voxel Revolution", "Space Fighter Loop", "Ouroboros" };
    const float FadeTime = 1.5f;

    bool inMenu;
    string want;
    float fade = 1f;
    int next;
    readonly List<string> playlist = new();

    void PickMusic(bool menu)
    {
        if (menu && inMenu && want == MenuTrack) return;
        inMenu = menu;
        want = menu ? MenuTrack : NextMatchTrack();
    }

    string NextMatchTrack()
    {
        if (next >= playlist.Count)
        {
            playlist.Clear();
            playlist.AddRange(MatchTracks);
            for (int i = playlist.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (playlist[i], playlist[j]) = (playlist[j], playlist[i]); }
            next = 0;
        }
        return playlist[next++];
    }

    void UpdateMusic()
    {
        if (want == null) return;
        bool switching = music.clip == null || music.clip.name != want;
        fade = Mathf.MoveTowards(fade, switching && music.isPlaying ? 0f : 1f, Time.unscaledDeltaTime / FadeTime);
        if (switching && (fade <= 0f || !music.isPlaying))
        {
            if (music.clip != null) Resources.UnloadAsset(music.clip);
            music.clip = Resources.Load<AudioClip>("Music/" + want);
            if (music.clip == null) { want = null; return; }
            music.loop = inMenu;
            music.Play();
        }
        else if (!inMenu && !music.isPlaying && music.clip != null && Application.isFocused) want = NextMatchTrack();
        ApplyVolume();
    }


    public static void Play(Snd s, float volume = 1f, float pitch = 1f) { if (I != null) I.Voice(s, volume, pitch); }

    public static void At(Snd s, Vector3 at, float volume = 1f)
    {
        if (I == null || !Juice.Seen(at)) return;
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 v = cam.WorldToViewportPoint(at);
        if (v.z < 0f || v.x < -0.25f || v.x > 1.25f || v.y < -0.25f || v.y > 1.25f) return;
        Vector3 focus = cam.transform.position + cam.transform.forward * 30f;
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
        if (free < 0) return;
        lastPlayed[id] = now;
        var a = voices[free];
        voiceSnd[free] = s;
        var files = variants[id];
        if (files == null || files.Length == 0) return;
        a.clip = files[Random.Range(0, files.Length)];
        a.volume = Mathf.Clamp01(volume) * GameSettings.Effects;
        a.pitch = pitch;
        a.Play();
    }

    public static Snd ShotOf(float damage, float splash, bool electric) =>
        electric ? Snd.Zap : damage >= 50f || splash > 0f ? Snd.Cannon : damage >= 15f ? Snd.Blaster : Snd.Laser;

    void Update()
    {
        UpdateMusic();
        UpdateRain();
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame || EventSystem.current == null) return;
        uiHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = mouse.position.ReadValue() }, uiHits);
        if (uiHits.Count > 0 && uiHits[0].gameObject.GetComponentInParent<Selectable>() is Selectable sel && sel.IsInteractable())
            Play(Snd.Click, 0.6f);
    }
}
