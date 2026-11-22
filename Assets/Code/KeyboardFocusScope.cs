using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Only intentional modals restrict Tab traversal. Ordinary menus remain part of page navigation.
public sealed class KeyboardFocusScope : MonoBehaviour
{
    public Selectable Previous { get; private set; }
    void OnEnable()
    {
        Previous=EventSystem.current?.currentSelectedGameObject?.GetComponent<Selectable>();
        KeyboardFocusNavigation.Instance?.EnterModal(this);
    }
    void OnDisable()=>KeyboardFocusNavigation.Instance?.LeaveModal(this);
}
