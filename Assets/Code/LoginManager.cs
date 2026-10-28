using UnityEngine;
using TMPro;
using TravelApp;
public class LoginManager : MonoBehaviour
{
    public static LoginManager Instance;
    [Header("Login Inputs")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField passwordInput;
    [Header("Database")]
    [SerializeField] private UserDatabase userDatabase;
    public User CurrentUser => TravelAppRuntime.Instance?.Services?.Accounts.CurrentUser;
    void Awake() { Instance = this; if (passwordInput != null) passwordInput.contentType = TMP_InputField.ContentType.Password; }
    public bool AttemptLogin()
    {
        var runtime = TravelAppRuntime.Instance;
        if (runtime?.Services == null) return false;
        var result = runtime.Services.Accounts.Login(usernameInput != null ? usernameInput.text : "", passwordInput != null ? passwordInput.text : "");
        runtime.Show(result); return result.success;
    }
    public void Logout() { TravelAppRuntime.Instance.Services.Accounts.Logout(); TravelAppRuntime.Instance.Navigate("LoginScreen"); }
    public bool IsLoggedIn() => TravelAppRuntime.Instance?.Services?.Accounts.IsLoggedIn == true;
    void OnDestroy() { if (Instance == this) Instance = null; }
}
