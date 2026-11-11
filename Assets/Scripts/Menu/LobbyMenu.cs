using UnityEngine;
using UnityEngine.UI;

// Main menu shown after sign-in, plus the Account screen it opens.
public class LobbyMenu : MonoBehaviour
{
    public LoginScreen login;
    public Text welcome, status;
    public Button skirmish, multiplayer, settings, account, friends, quit;

    public GameObject accountPanel, multiplayerPanel;
    public Text accountInfo;
    public Button signOut, back;

    void Awake()
    {
        skirmish.onClick.AddListener(() => SkirmishPanel.Open(this));
        multiplayer.onClick.AddListener(() => { gameObject.SetActive(false); multiplayerPanel.SetActive(true); });
        settings.onClick.AddListener(() => SettingsPanel.Open(this));
        account.onClick.AddListener(OpenAccount);
        FriendsPanel.Hook(friends);
        quit.onClick.AddListener(Quit);
        back.onClick.AddListener(() => { accountPanel.SetActive(false); gameObject.SetActive(true); });
        signOut.onClick.AddListener(() => { FriendsHub.SignedOut(); AccountService.SignOut(); ProfileService.Forget(); accountPanel.SetActive(false); login.Show(); });
    }

    void OnEnable()
    {
        welcome.text = "WELCOME, " + AccountService.Username?.ToUpper();
        status.text = "";
    }

    void Soon(string feature) => status.text = feature + " is coming soon.";

    void OpenAccount()
    {
        gameObject.SetActive(false);
        accountPanel.SetActive(true);
        if (!accountPanel.TryGetComponent(out AccountScreen screen)) screen = accountPanel.AddComponent<AccountScreen>();
        screen.Show(); // profile, rank, win rates, recent matches
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
