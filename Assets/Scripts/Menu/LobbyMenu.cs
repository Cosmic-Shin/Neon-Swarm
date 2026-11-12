using UnityEngine;
using UnityEngine.UI;

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
        Tutorial.Active = false;
        AddEntries();
        extras = MenuExtras.Build(transform.parent, OpenAccount);
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
        continueButton.interactable = SaveSystem.HasAnySave;
        if (extras != null) extras.gameObject.SetActive(true);
    }

    void OnDisable()
    {
        if (extras != null) extras.gameObject.SetActive(false);
    }

    MenuExtras extras;

    Button continueButton;

    Button Entry(string label, UnityEngine.Events.UnityAction act)
    {
        var b = Instantiate(skirmish, skirmish.transform.parent);
        b.name = label + "Button";
        b.onClick = new Button.ButtonClickedEvent();
        b.onClick.AddListener(act);
        b.GetComponentInChildren<Text>().text = label;
        return b;
    }

    void AddEntries()
    {
        continueButton = Entry("CONTINUE", () => SaveSystem.LoadGame(SaveSystem.NewestSlot));
        var tutorial = Entry("TUTORIAL", Tutorial.Begin);
        var order = new[] { continueButton, tutorial, skirmish, multiplayer, friends, settings, account, quit };
        const float gap = 66f;
        var panel = (RectTransform)transform;
        float grow = (order.Length - 1) * gap - 5 * 72f;
        panel.sizeDelta += new Vector2(0f, grow);
        panel.anchoredPosition -= new Vector2(0f, grow / 2f);
        welcome.rectTransform.anchoredPosition += new Vector2(0f, grow / 2f);
        status.rectTransform.anchoredPosition -= new Vector2(0f, grow / 2f);
        float top = panel.sizeDelta.y / 2f - 135f;
        for (int i = 0; i < order.Length; i++)
        {
            ((RectTransform)order[i].transform).anchoredPosition = new Vector2(0f, top - i * gap);
            if (order[i].TryGetComponent(out HoloButtonFX fx)) fx.index = $"{i + 1:00}";
            foreach (var t in order[i].GetComponentsInChildren<Text>(true)) if (t.name == "HoloIndex") t.text = $"{i + 1:00}";
        }
    }

    void Soon(string feature) => status.text = feature + " is coming soon.";

    void OpenAccount()
    {
        gameObject.SetActive(false);
        accountPanel.SetActive(true);
        if (!accountPanel.TryGetComponent(out AccountScreen screen)) screen = accountPanel.AddComponent<AccountScreen>();
        screen.Show();
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
