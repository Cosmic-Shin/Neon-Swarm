using UnityEngine;
using UnityEngine.UI;

// Main menu shown after sign-in, plus the Account screen it opens.
public class LobbyMenu : MonoBehaviour
{
    public LoginScreen login;
    public Text welcome, status;
    public Button skirmish, multiplayer, settings, account, quit;

    public GameObject accountPanel, multiplayerPanel;
    public Text accountInfo;
    public Button signOut, back;

    void Awake()
    {
        skirmish.onClick.AddListener(() => SkirmishPanel.Open(this));
        multiplayer.onClick.AddListener(() => { gameObject.SetActive(false); multiplayerPanel.SetActive(true); });
        settings.onClick.AddListener(() => SettingsPanel.Open(this));
        account.onClick.AddListener(OpenAccount);
        quit.onClick.AddListener(Quit);
        back.onClick.AddListener(() => { accountPanel.SetActive(false); gameObject.SetActive(true); });
        signOut.onClick.AddListener(() => { AccountService.SignOut(); accountPanel.SetActive(false); login.Show(); });
    }

    void OnEnable()
    {
        welcome.text = "WELCOME, " + AccountService.Username?.ToUpper();
        status.text = "";
    }

    void Soon(string feature) => status.text = feature + " is coming soon.";

    async void OpenAccount()
    {
        gameObject.SetActive(false);
        accountPanel.SetActive(true);
        string info = "Username:  " + AccountService.Username + "\nPlayer ID:  " + AccountService.PlayerId;
        accountInfo.text = info + "\nMember since:  ...";
        try
        {
            var since = await AccountService.MemberSince();
            accountInfo.text = info + "\nMember since:  " + (since?.ToLocalTime().ToString("MMM d, yyyy") ?? "unknown");
        }
        catch (System.Exception e)
        {
            accountInfo.text = info + "\nMember since:  unavailable";
            Debug.LogWarning(e);
        }
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
