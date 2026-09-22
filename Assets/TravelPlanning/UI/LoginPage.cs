using System;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using TravelPlanning.Accounts;
using UnityEngine;

namespace TravelPlanning.UI
{
    /// <summary>The bridge: takes text from Unity's form and displays the account service's answer.</summary>
    public sealed class LoginPage : MonoBehaviour
    {
        // The email address is also the username; keep one field for both forms.
        [SerializeField] private TMP_InputField emailField;
        [SerializeField] private TMP_InputField passwordField;
        [SerializeField] private TMP_InputField confirmationField;
        [SerializeField] private TMP_Text heading;
        [SerializeField] private TMP_Text feedback;
        [SerializeField] private TMP_Text submitLabel;
        [SerializeField] private TMP_Text switchLabel;
        [SerializeField] private UnityEngine.UI.Button submitButton;
        [SerializeField] private UnityEngine.UI.Button switchButton;
        [SerializeField] private UnityEngine.UI.Button logoutButton;

        private AccountDatabase database;
        private AccountService service;
        private bool registering;
        private bool busy;
        private bool destroyed;

        // Your team's next screen can subscribe to this event. No scene names are hard-coded.
        public event Action<string> LoggedIn;
        public string SignedInEmail => service?.SignedInEmail;

        private void Start()
        {
            try
            {
                database = new AccountDatabase(Path.Combine(Application.persistentDataPath, "accounts.db"));
                service = new AccountService(database);
                submitButton.onClick.AddListener(Submit);
                switchButton.onClick.AddListener(SwitchForm);
                logoutButton.onClick.AddListener(Logout);
                ShowForm();
            }
            catch (Exception)
            {
                feedback.text = "The account file could not be opened. Close any other copy of the app and try again.";
                submitButton.interactable = false;
                switchButton.interactable = false;
            }
        }

        private async void Submit()
        {
            if (busy || service == null)
                return;
            busy = true;
            SetInteractable(false);
            feedback.text = "Please wait...";
            string email = emailField.text;
            string password = passwordField.text;
            string confirmation = confirmationField.text;
            bool isRegistration = registering;
            AccountResult result;
            try
            {
                // Keep database work off Unity's main thread so the form stays responsive.
                result = await Task.Run(() => isRegistration
                    ? service.Register(email, password, confirmation)
                    : service.Login(email, password));
            }
            catch (Exception)
            {
                if (!destroyed)
                    feedback.text = "The account file could not be read or saved. Please try again.";
                return;
            }
            finally
            {
                busy = false;
                if (destroyed)
                    database?.Dispose();
                else
                {
                    passwordField.text = "";
                    confirmationField.text = "";
                    SetInteractable(true);
                }
            }
            if (destroyed)
                return;
            if (result.Success)
            {
                emailField.text = result.Email;
                registering = false;
                ShowForm();
            }
            feedback.text = result.Message;
            if (result.Success && !isRegistration)
                LoggedIn?.Invoke(result.Email);
        }

        private void SwitchForm()
        {
            if (busy)
                return;
            registering = !registering;
            passwordField.text = "";
            confirmationField.text = "";
            feedback.text = "";
            ShowForm();
        }

        private void Logout()
        {
            service.Logout();
            feedback.text = "You are logged out.";
            ShowForm();
        }

        private void ShowForm()
        {
            bool signedIn = service.SignedInEmail != null;
            heading.text = signedIn ? "Welcome" : registering ? "Create an account" : "Login";
            ((TMP_Text)emailField.placeholder).text = registering ? "Email address" : "Username (email address)";
            ((TMP_Text)passwordField.placeholder).text = registering ? "Password (8-128 characters)" : "Password";
            emailField.gameObject.SetActive(!signedIn);
            passwordField.gameObject.SetActive(!signedIn);
            confirmationField.gameObject.SetActive(!signedIn && registering);
            submitButton.gameObject.SetActive(!signedIn);
            switchButton.gameObject.SetActive(!signedIn);
            logoutButton.gameObject.SetActive(signedIn);
            submitLabel.text = registering ? "Create account" : "Log in";
            switchLabel.text = registering ? "Back to login" : "Create an account";
        }

        private void SetInteractable(bool value)
        {
            submitButton.interactable = value;
            switchButton.interactable = value;
            emailField.interactable = value;
            passwordField.interactable = value;
            confirmationField.interactable = value;
        }

        private void OnDestroy()
        {
            destroyed = true;
            // An in-flight request finishes before its file handle is closed.
            if (!busy)
                database?.Dispose();
        }
    }
}
