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
                
            case "BookingButton":
                LoadSceneIfLoggedIn("BookingScreen");
                break;

            case "AccountButton":
                LoadSceneIfLoggedIn("AccountScreen");
                break;
            
            case "QuitButton":
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
                break;

            case "SearchButton":
                BookingManager.Instance.Search();
                break;

            case "ResetFiltersButton":
                BookingManager.Instance.ResetFilters();
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

    private void LoadSceneIfLoggedIn(string sceneName)
    {
        if (LoginManager.Instance != null &&
            LoginManager.Instance.IsLoggedIn())
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.Log("User is not logged in.");
            SceneManager.LoadScene("LoginScene");
        }
    }

}