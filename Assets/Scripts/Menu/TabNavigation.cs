using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Tab = next field/button, Shift+Tab = previous, on every menu form (it follows the UI's own
// navigation order, i.e. what's below / above). Enter is handled by each form's onSubmit.
public class TabNavigation : MonoBehaviour
{
    void Update()
    {
        var kb = Keyboard.current;
        var es = EventSystem.current;
        if (kb == null || es == null || !kb.tabKey.wasPressedThisFrame) return;

        var current = es.currentSelectedGameObject != null ? es.currentSelectedGameObject.GetComponent<Selectable>() : null;
        bool back = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        var next = current == null ? FirstField()
                 : back ? current.FindSelectableOnUp() ?? current.FindSelectableOnLeft()
                 : current.FindSelectableOnDown() ?? current.FindSelectableOnRight();
        if (next == null) return;

        next.Select();
        if (next is InputField field) field.ActivateInputField();
    }

    // Nothing selected yet: the top field of whatever form is showing.
    static InputField FirstField()
    {
        InputField best = null;
        foreach (var f in FindObjectsByType<InputField>(FindObjectsSortMode.None))
            if (f.IsInteractable() && (best == null || f.transform.position.y > best.transform.position.y)) best = f;
        return best;
    }
}
