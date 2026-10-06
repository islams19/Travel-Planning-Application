using UnityEngine;
using TMPro;

public class LoginManager : MonoBehaviour
{
    public static LoginManager Instance;

    [Header("Login Inputs")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField passwordInput;

    [Header("Database")]
    [SerializeField] private UserDatabase userDatabase;

    public User CurrentUser { get; private set; }

    private void Awake()
    {
        // Prevent duplicate LoginManagers
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Keep LoginManager alive when changing scenes
        DontDestroyOnLoad(gameObject);
    }

    public bool AttemptLogin()
    {
        string username = usernameInput.text.Trim();
        string password = passwordInput.text;

        User user = userDatabase.GetUser(username);

        if (user == null)
        {
            Debug.Log("User not found.");
            return false;
        }

        if (user.password != password)
        {
            Debug.Log("Incorrect password.");
            return false;
        }

        CurrentUser = user;

        Debug.Log("Login successful!");
        Debug.Log("Logged in as: " + CurrentUser.username);

        return true;
    }

    public void Logout()
    {
        if (CurrentUser != null)
        {
            Debug.Log("Logging out: " + CurrentUser.username);
        }

        CurrentUser = null;

        Debug.Log("User logged out.");
    }

    public bool IsLoggedIn()
    {
        return CurrentUser != null;
    }
}