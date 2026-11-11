using System;
using UnityEngine;
using UnityEngine.UI;

// Sign in / create account screen. On success it hands over to the lobby.
public class LoginScreen : MonoBehaviour
{
    public InputField username, password;
    public Button signIn, signUp;
    public Text status;
    public GameObject signInPanel, lobbyPanel;

    void Start()
    {
        signIn.onClick.AddListener(() => Submit(false));
        signUp.onClick.AddListener(() => Submit(true));
        // Enter in the username box: on to the password (or sign in if it is already filled).
        username.onSubmit.AddListener(_ => { if (password.text.Length == 0) { password.Select(); password.ActivateInputField(); } else Submit(false); });
        password.onSubmit.AddListener(_ => Submit(false)); // Enter in the password box = Sign In
        Show();
#if DEVELOPMENT_BUILD
        // Test builds only: "GD5.exe -signin user pass" signs in at start (a second player for friends tests).
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-signin");
        if (i >= 0 && i + 2 < args.Length && !AccountService.SignedIn) { username.text = args[i + 1]; password.text = args[i + 2]; Submit(false); }
#endif
    }

    async void Submit(bool isNew)
    {
        if (!signIn.interactable) return; // request already running
        string error = AccountService.Validate(username.text.Trim(), password.text, isNew);
        if (error != null) { status.text = error; return; }

        SetBusy(true);
        status.text = isNew ? "Creating account..." : "Signing in...";
        try
        {
            if (isNew) await AccountService.SignUp(username.text.Trim(), password.text);
            else await AccountService.SignIn(username.text.Trim(), password.text);
            password.text = "";
            status.text = "";
            Show();
        }
        catch (Exception e)
        {
            status.text = e.Message;
            Debug.LogWarning(e);
        }
        SetBusy(false);
    }

    public void Show()
    {
        bool inside = AccountService.SignedIn;
        signInPanel.SetActive(!inside);
        lobbyPanel.SetActive(inside);
        if (inside) _ = FriendsHub.Init();
        if (inside && RoomService.Again != null) MultiplayerMenu.PlayAgain(); // back from a match with PLAY AGAIN
    }

    void SetBusy(bool busy) => signIn.interactable = signUp.interactable = !busy;
}
