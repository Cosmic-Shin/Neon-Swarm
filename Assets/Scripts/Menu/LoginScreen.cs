using System;
using UnityEngine;
using UnityEngine.UI;

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
        username.onSubmit.AddListener(_ => { if (password.text.Length == 0) { password.Select(); password.ActivateInputField(); } else Submit(false); });
        password.onSubmit.AddListener(_ => Submit(false));
        Show();
#if DEVELOPMENT_BUILD
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "-signin");
        if (i >= 0 && i + 2 < args.Length && !AccountService.SignedIn) { username.text = args[i + 1]; password.text = args[i + 2]; Submit(false); }
#endif
    }

    async void Submit(bool isNew)
    {
        if (!signIn.interactable) return;
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
        if (inside && RoomService.Again != null) MultiplayerMenu.PlayAgain();
    }

    void SetBusy(bool busy) => signIn.interactable = signUp.interactable = !busy;
}
