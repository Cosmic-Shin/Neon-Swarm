using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Esc menu: Resume, Surrender (multiplayer) and Leave. Esc only opens it when nothing else uses Esc
// (build menu, building ghost, chat box). The game keeps running underneath (no pause in a shared match).
[DefaultExecutionOrder(-200)] // before Chat and BuildingPlacer see the same Esc
public class GameMenu : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] Button resume, surrender, leave;
    [SerializeField] MatchManager match;

    public static bool IsOpen { get; private set; }

    void Awake()
    {
        IsOpen = false;
        panel.SetActive(false);
        resume.onClick.AddListener(() => Show(false));
        surrender.onClick.AddListener(() => { Show(false); match.Surrender(); });
        leave.onClick.AddListener(Leave);
    }

    void Update()
    {
        var kb = Chat.Keys;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame || match.IsOver) return;
        if (!IsOpen && (BuildingPlacer.MenuOpen || BuildingPlacer.Placing)) return; // their Esc
        Show(!IsOpen);
    }

    void Show(bool open)
    {
        IsOpen = open;
        panel.SetActive(open);
        surrender.gameObject.SetActive(Net.Online);
    }

    void Leave()
    {
        Show(false);
        if (Net.Online) match.BackToMenu(); // our units leave with us (DestroyWhenStateAuthorityLeaves)
        else SceneManager.LoadScene(0);
    }
}
