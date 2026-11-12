using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public class Chat : MonoBehaviour
{
    [SerializeField] InputField input;

    public static bool Typing { get; private set; }
    static int closedFrame = -1;
    bool toAll;

    public static Keyboard Keys => Typing || Time.frameCount == closedFrame ? null : Keyboard.current;

    void Awake() { Typing = false; input.gameObject.SetActive(false); }

    void OnEnable() => RoomState.ChatReceived += Show;
    void OnDisable() => RoomState.ChatReceived -= Show;
    static void Show(int slot, string text, bool toAll)
    {
        Sfx.Play(Snd.Chat, 0.7f);
        Alerts.Show($"{(toAll ? "" : "(Team) ")}{RoomState.Instance?.NameOf(slot)}: {text}", TeamMember.ColorOf(slot), silent: true);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !Net.Online) return;
        bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;

        if (!Typing)
        {
            if (!enter || GameMenu.IsOpen) return;
            Typing = true;
            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            toAll = shift || RoomState.Instance == null || !RoomState.Instance.HasAllies();
            if (input.placeholder is Text hint) hint.text = toAll ? "To everyone..." : "To your team...  (Shift+Enter: everyone)";
            input.gameObject.SetActive(true);
            input.text = "";
            input.ActivateInputField();
            return;
        }

        if (enter)
        {
            string text = input.text.Trim();
            if (text.Length > 0 && RoomState.Instance != null) RoomState.Instance.SendChat(text, toAll);
            Close();
        }
        else if (kb.escapeKey.wasPressedThisFrame) Close();
        else if (!input.isFocused) input.ActivateInputField();
    }

    void Close()
    {
        Typing = false;
        closedFrame = Time.frameCount;
        input.DeactivateInputField();
        input.gameObject.SetActive(false);
    }
}
