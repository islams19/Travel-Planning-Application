using UnityEngine;
using TravelApp;
public class ButtonManager : MonoBehaviour
{
    public static ButtonManager Instance { get; private set; }
    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }
    // Compatibility adapter for existing named buttons. New UI uses direct callbacks.
    public void ButtonPressed(string buttonName)
    {
        var runtime = TravelAppRuntime.Instance;
        if (runtime?.Services == null) return;
        switch (buttonName)
        {
            case "LoginButton": if (LoginManager.Instance != null && LoginManager.Instance.AttemptLogin()) runtime.Navigate("HomeScreen"); break;
            case "HomeButton": case "BackButton": runtime.Navigate("HomeScreen"); break;
            case "BookingButton": runtime.Navigate("BookingScreen"); break;
            case "AccountButton": runtime.Navigate("AccountScreen"); break;
            case "LogoutButton": runtime.Services.Accounts.Logout(); runtime.Navigate("LoginScreen"); break;
            case "SearchButton": BookingManager.Instance?.Search(); break;
            case "ResetFiltersButton": BookingManager.Instance?.ResetFilters(); break;
            case "QuitButton":
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;
        }
    }
}
