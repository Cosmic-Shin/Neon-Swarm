using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum Act { CamUp, CamDown, CamLeft, CamRight, AttackMove, Patrol, Stop, Hold, Formation, Unload, NextIdle, BuildMenu, JumpAlert, Help, QuickSave, QuickLoad }

public static class Controls
{
    public static readonly (Act act, string name, Key key)[] All =
    {
        (Act.CamUp, "Scroll up", Key.UpArrow),
        (Act.CamDown, "Scroll down", Key.DownArrow),
        (Act.CamLeft, "Scroll left", Key.LeftArrow),
        (Act.CamRight, "Scroll right", Key.RightArrow),
        (Act.AttackMove, "Attack-move", Key.A),
        (Act.Patrol, "Patrol", Key.P),
        (Act.Stop, "Stop", Key.S),
        (Act.Hold, "Hold position", Key.H),
        (Act.Formation, "Next formation", Key.F),
        (Act.Unload, "Unload transport", Key.U),
        (Act.NextIdle, "Next idle unit", Key.Period),
        (Act.BuildMenu, "Build menu", Key.B),
        (Act.JumpAlert, "Jump to attack", Key.Space),
        (Act.Help, "Hotkey sheet", Key.F1),
        (Act.QuickSave, "Quick save", Key.F5),
        (Act.QuickLoad, "Quick load", Key.F9),
    };

    public const string Grid = "QWERTYUIOPASDFGHJKL";
    static readonly Key[] GridKeys = { Key.Q, Key.W, Key.E, Key.R, Key.T, Key.Y, Key.U, Key.I, Key.O, Key.P, Key.A, Key.S, Key.D, Key.F, Key.G, Key.H, Key.J, Key.K, Key.L };

    static readonly HashSet<Act> Camera = new() { Act.CamUp, Act.CamDown, Act.CamLeft, Act.CamRight };
    static readonly Dictionary<Act, Key> keys = new();
    public static event System.Action Changed;

    static Controls() => Load();

    static string Pref(Act a) => "key." + a;

    static void Load()
    {
        keys.Clear();
        foreach (var (a, _, k) in All) keys[a] = (Key)PlayerPrefs.GetInt(Pref(a), (int)k);
    }

    public static Key KeyOf(Act a) => keys.TryGetValue(a, out var k) ? k : Key.None;

    public static bool Down(Act a) => Chat.Keys is Keyboard kb && KeyOf(a) != Key.None && kb[KeyOf(a)].wasPressedThisFrame;
    public static bool Held(Act a) => Chat.Keys is Keyboard kb && KeyOf(a) != Key.None && kb[KeyOf(a)].isPressed;

    public static int GridDown()
    {
        if (Chat.Keys is not Keyboard kb) return -1;
        for (int i = 0; i < GridKeys.Length; i++) if (kb[GridKeys[i]].wasPressedThisFrame) return i;
        return -1;
    }

    public static string GridLabel(int i) => i >= 0 && i < Grid.Length ? Grid[i].ToString() : "";

    public static string Label(Act a) => Label(KeyOf(a));

    public static string Label(Key k) => k switch
    {
        Key.None => "-",
        Key.Space => "Space",
        Key.UpArrow => "Up",
        Key.DownArrow => "Down",
        Key.LeftArrow => "Left",
        Key.RightArrow => "Right",
        Key.Period => ".",
        Key.Comma => ",",
        Key.Minus => "-",
        Key.Equals => "=",
        Key.Slash => "/",
        Key.Semicolon => ";",
        Key.Quote => "'",
        Key.Backquote => "`",
        Key.LeftBracket => "[",
        Key.RightBracket => "]",
        Key.Backslash => "\\",
        Key.Tab => "Tab",
        >= Key.Digit1 and <= Key.Digit0 => k == Key.Digit0 ? "0" : ((int)(k - Key.Digit1) + 1).ToString(),
        _ => k.ToString(),
    };

    public static string Conflict(Act a, Key k)
    {
        foreach (var (other, name, _) in All)
            if (other != a && KeyOf(other) == k && Camera.Contains(other) == Camera.Contains(a)) return name;
        return null;
    }

    public static void Set(Act a, Key k)
    {
        Key old = KeyOf(a);
        foreach (var (other, _, _) in All)
            if (other != a && KeyOf(other) == k && Camera.Contains(other) == Camera.Contains(a)) { keys[other] = old; PlayerPrefs.SetInt(Pref(other), (int)old); }
        keys[a] = k;
        PlayerPrefs.SetInt(Pref(a), (int)k);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void ResetAll()
    {
        foreach (var (a, _, _) in All) PlayerPrefs.DeleteKey(Pref(a));
        PlayerPrefs.Save();
        Load();
        Changed?.Invoke();
    }

    public static bool Reserved(Key k) => k == Key.Escape || k == Key.Enter || k == Key.LeftShift || k == Key.RightShift
        || k == Key.LeftCtrl || k == Key.RightCtrl || k == Key.LeftAlt || k == Key.RightAlt || (k >= Key.Digit1 && k <= Key.Digit0);
}
