using System;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using TravelPlanning.Accounts;
using TravelPlanning.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TravelPlanning.UI
{
    /// <summary>Connects Kevin's form to local SQLite accounts. Unity UI stays on the main thread.</summary>
    public sealed class LoginPage : Shared.ScreenControllerBase
    {
        [Header("Screen references")]
        [SerializeField]
        private GameObject loginPanel, registrationPanel, signedInPanel;
        [Header("Input fields")]
        [SerializeField]
        private TMP_InputField emailField, passwordField, registrationEmail, registrationPassword, confirmationField;
        [Header("Results and presentation")]
        [SerializeField]
        private TMP_Text feedback, registrationFeedback, signedInEmail;
        [Header("Buttons")]
        [SerializeField]
        private UnityEngine.UI.Button submitButton, switchButton, registerButton, backButton, logoutButton, forgotButton;
        [SerializeField, HideInInspector]
        private string developmentDatabaseFile;
        private AccountService service;
        private TMP_InputField[] loginFields, registrationFields;
        private Accounts.AccountFormView view;
        private Accounts.AccountFormView View => view ?? (view = new Accounts.AccountFormView(loginPanel,
            registrationPanel, signedInPanel, loginFields, registrationFields, feedback, registrationFeedback, signedInEmail,
            new[] { submitButton, switchButton, registerButton, backButton, logoutButton, forgotButton }));

        private bool busy;
        public bool IsReady { get; private set; }
        public bool IsBusy => busy;
        public bool ExternalKeyboardNavigation { get; set; }
        public string SignedInEmail => service?.SignedInEmail;
        public string SignedInUserId => service?.SignedInUserId;
        public TravelDatabase Database { get; private set; }

        public GameObject LoginPanel => loginPanel;
        public GameObject RegistrationPanel => registrationPanel;
        public GameObject HomePanel => signedInPanel;

        public event Action<string> LoggedIn;
        public event Action LoggedOut;
        public void Configure(Shared.TravelSceneBindings scene)
        {
            registrationPanel = scene.RegistrationCard.gameObject;
            signedInPanel = scene.HomeCard.gameObject;
            registrationEmail = scene.RegistrationCard.Find("EmailInput").GetComponent<TMP_InputField>();
            registrationPassword = scene.RegistrationCard.Find("PasswordInput").GetComponent<TMP_InputField>();
            confirmationField = scene.RegistrationCard.Find("ConfirmationInput").GetComponent<TMP_InputField>();
            registrationFeedback = scene.RegistrationCard.Find("AccountFeedback").GetComponent<TMP_Text>();
            signedInEmail = scene.HomeCard.Find("AccountEmail").GetComponent<TMP_Text>();
            registerButton = scene.Button(scene.RegistrationCard, "LoginButton");
            backButton = scene.Button(scene.RegistrationCard, "Create an account");
            logoutButton = scene.Button(scene.HomeCard, "LogoutButton");
        }

        // Scene initialization and event wiring.
        private async void Start()
        {
            if (!loginPanel || !registrationPanel || !signedInPanel || !emailField || !passwordField || !registrationEmail ||
                !registrationPassword || !confirmationField || !feedback || !registrationFeedback || !signedInEmail || !submitButton ||
                !switchButton || !registerButton || !backButton || !logoutButton || !forgotButton)
            {
                Debug.LogError("LoginPage: account UI references are missing. Reopen the prepared Login scene and check AccountController.");
                enabled = false;
                if (ArgumentPresent("-authSmoke") || ArgumentPresent("-releaseSmoke"))
                {
                    Application.Quit(2);
                }

                return;
            }

            loginFields = new[]
            {
                emailField,
                passwordField
            };
            registrationFields = new[]
            {
                registrationEmail,
                registrationPassword,
                confirmationField
            };
            submitButton.onClick.AddListener(SubmitLogin);
            registerButton.onClick.AddListener(SubmitRegistration);
            switchButton.onClick.AddListener(OpenRegistration);
            backButton.onClick.AddListener(OpenLogin);
            logoutButton.onClick.AddListener(Logout);
            forgotButton.onClick.AddListener(ExplainPasswordRecovery);
            SetBusy(true);
            feedback.text = "Opening your local travel database...";
            try
            {
                string file = Polish.ReleaseSmokeArguments.ResolveDatabaseFile(Environment.GetCommandLineArgs(), Debug.isDebugBuild, developmentDatabaseFile);
                var database = new TravelDatabase(Path.Combine(Application.streamingAssetsPath, "Database",
                    "travel_seed.db"), Path.Combine(Application.persistentDataPath, file));
                await database.InitializeAsync(base.BeginScreenRequest());
                if (!this)
                {
                    return;
                }

                Database = database;
                service = new AccountService(new AccountDatabase(database));
                IsReady = true;
                feedback.text = "";
                SetBusy(false);
                if (Debug.isDebugBuild && ArgumentPresent("-authSmoke"))
                {
                    await new QA.AuthSmokeRunner(this, loginPanel, signedInPanel, loginFields, registrationFields,
                        switchButton, registerButton, submitButton, logoutButton, destroyCancellationToken).RunAsync(ArgumentPresent("-authRegister"));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ArgumentException)when (Polish.ReleaseSmokeArguments.IsSmokeRequested(Environment.GetCommandLineArgs()))
            {
                Debug.LogError("AUTH_QA_ARGUMENTS_REJECTED");
                Application.Quit(2);
            }
            catch (Exception)
            {
                if (!this)
                {
                    return;
                }

                feedback.text = "The local database could not be opened. Your existing files have been kept. Check the database setup guide.";
                Debug.LogError("AUTH_DATABASE_UNAVAILABLE");
                if (ArgumentPresent("-authSmoke") || ArgumentPresent("-releaseSmoke"))
                {
                    Application.Quit(1);
                }
            }
        }

        // Login action: verify the account, then show the signed-in panel.
        private async void SubmitLogin()
        {
            if (busy || !IsReady)
            {
                return;
            }

            await RunAccountRequest(token => service.LoginAsync(emailField.text, passwordField.text, token), feedback, CompleteLogin);
        }

        private void CompleteLogin(AccountResult result)
        {
            View.ShowSignedIn(result.Email);
            LoggedIn?.Invoke(result.Email);
        }

        // Registration action: create an account, then return to the login form.
        private async void SubmitRegistration()
        {
            if (busy || !IsReady)
            {
                return;
            }

            await RunAccountRequest(token => service.RegisterAsync(registrationEmail.text, registrationPassword.text,
                confirmationField.text, token), registrationFeedback, CompleteRegistration);
        }

        private void CompleteRegistration(AccountResult result)
        {
            emailField.text = result.Email;
            ShowRegistration(false);
            feedback.text = result.Message;
        }

        // Both actions share only their waiting/error state, not their success behavior.
        private async Task RunAccountRequest(Func<System.Threading.CancellationToken, Task<AccountResult>> operation, TMP_Text message, Action<AccountResult> complete)
        {
            base.CancelScreenRequest();
            int current = RequestRevision;
            var token = base.BeginScreenRequest();
            SetBusy(true);
            message.text = "Please wait...";
            try
            {
                var result = await operation(token);
                if (!base.IsRequestCurrent(current))
                {
                    return;
                }

                message.text = result.Message;
                if (result.Success)
                {
                    complete(result);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                if (base.IsRequestCurrent(current))
                {
                    message.text = "We could not complete that request. Please try again.";
                }
            }
            finally
            {
                if (base.IsRequestCurrent(current))
                {
                    ClearPasswords();
                    SetBusy(false);
                }
            }
        }

        private void ShowRegistration(bool show) => View.ShowRegistration(show);
        // Logout action: invalidate pending account work before exposing the login form.
        public void Logout()
        {
            base.CancelScreenRequest();
            SetBusy(false);
            service?.Logout();
            signedInEmail.text = "";
            ShowRegistration(false);
            feedback.text = "You are logged out.";
            LoggedOut?.Invoke();
        }

        private void ClearPasswords() => View.ClearPasswords();
        private void OpenRegistration() => ShowRegistration(true);
        private void OpenLogin() => ShowRegistration(false);
        private void ExplainPasswordRecovery() => feedback.text = "Password recovery is not available for this local demo. Create a new account if needed.";
        private void Update()
        {
            if (!IsReady || busy || signedInPanel.activeSelf)
            {
                return;
            }

            bool registering = registrationPanel.activeSelf;
            var fields = registering ? registrationFields : loginFields;
            if (!ExternalKeyboardNavigation && Input.GetKeyDown(KeyCode.Tab))
            {
                int current = Array.FindIndex(fields, field => field.isFocused);
                int direction = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1;
                int next = current < 0 ? 0 : (current + direction + fields.Length) % fields.Length;
                fields[next].Select();
                fields[next].ActivateInputField();
            }

            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && Array.Exists(fields, field => field.isFocused))
            {
                if (registering)
                {
                    SubmitRegistration();
                }
                else
                {
                    SubmitLogin();
                }
            }
        }

        // Lifecycle cleanup.
        protected override void OnDestroy()
        {
            base.OnDestroy();
            service?.Logout();
            if (submitButton)
            {
                submitButton.onClick.RemoveListener(SubmitLogin);
            }

            if (registerButton)
            {
                registerButton.onClick.RemoveListener(SubmitRegistration);
            }

            if (switchButton)
            {
                switchButton.onClick.RemoveListener(OpenRegistration);
            }

            if (backButton)
            {
                backButton.onClick.RemoveListener(OpenLogin);
            }

            if (logoutButton)
            {
                logoutButton.onClick.RemoveListener(Logout);
            }

            if (forgotButton)
            {
                forgotButton.onClick.RemoveListener(ExplainPasswordRecovery);
            }
        }

        private void SetBusy(bool value)
        {
            busy = value;
            View.SetBusy(value);
        }

        private static bool ArgumentPresent(string key) => Array.IndexOf(Environment.GetCommandLineArgs(), key) >= 0;
    }
}
