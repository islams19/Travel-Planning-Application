using UnityEngine;
using UIButton = UnityEngine.UI.Button;

[RequireComponent(typeof(UIButton))]
public class Button : MonoBehaviour
{
    private UIButton button;

    private void Awake()
    {
        button = GetComponent<UIButton>();

        if (button != null)
        {
            button.onClick.AddListener(OnButtonPressed);
        }
    }

    private void OnButtonPressed()
    {
        if (ButtonManager.Instance == null)
        {
            Debug.LogError("ButtonManager could not be found.");
            return;
        }

        ButtonManager.Instance.ButtonPressed(gameObject.name);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonPressed);
        }
    }
}