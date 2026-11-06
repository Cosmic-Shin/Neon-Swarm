using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Multiplayer chat: Enter opens the box, Enter sends, Esc cancels. Messages show in the Alerts feed
// in the sender's colour (RoomState.RPC_Chat). While typing, game hotkeys read Chat.Keys and get nothing.
[DefaultExecutionOrder(-100)] // before the scripts that read hotkeys
public class Chat : MonoBehaviour
{
    [SerializeField] InputField input; // inactive until Enter

    public static bool Typing { get; private set; }
    static int closedFrame = -1;

    // The keyboard for game hotkeys: null while typing (and on the frame the box closes, so its Enter/Esc don't leak).
    public static Keyboard Keys => Typing || Time.frameCount == closedFrame ? null : Keyboard.current;

    void Awake() { Typing = false; input.gameObject.SetActive(false); }

    void OnEnable() => RoomState.ChatReceived += Show;
    void OnDisable() => RoomState.ChatReceived -= Show;
    static void Show(int slot, string text) => Alerts.Show($"{RoomState.Instance?.NameOf(slot)}: {text}", TeamMember.ColorOf(slot));

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !Net.Online) return;
        bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;

        if (!Typing)
        {
            if (!enter || GameMenu.IsOpen) return;
            Typing = true;
            input.gameObject.SetActive(true);
            input.text = "";
            input.ActivateInputField();
            return;
        }

        if (enter)
        {
            string text = input.text.Trim();
            if (text.Length > 0 && RoomState.Instance != null) RoomState.Instance.SendChat(text);
            Close();
        }
        else if (kb.escapeKey.wasPressedThisFrame) Close();
        else if (!input.isFocused) input.ActivateInputField(); // clicking the map mustn't strand the box
    }

    void Close()
    {
        Typing = false;
        closedFrame = Time.frameCount;
        input.DeactivateInputField();
        input.gameObject.SetActive(false);
    }
}
