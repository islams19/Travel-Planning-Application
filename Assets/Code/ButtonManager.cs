using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonManager : MonoBehaviour
{
    public static ButtonManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("More than one ButtonManager exists in the scene.");
            return;
        }

        Instance = this;
    }

    public void ButtonPressed(string buttonName)
    {
        Debug.Log("Button Manager received: " + buttonName);

        switch (buttonName)
        {
            case "LoginButton":
                LoginManager.Instance.AttemptLogin();
                if (LoginManager.Instance.IsLoggedIn()){
                    SceneManager.LoadScene("HomeScreen");
                }
                break;
            
            case "QuitButton":
                Application.Quit();
                break;

            default:
                Debug.LogWarning("No action assigned for: " + buttonName);
                break;
        }
    }

    private void Login()
    {
        Debug.Log("LOGIN ACTION");
    }
}