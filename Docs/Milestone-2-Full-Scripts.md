# Milestone 2 — Complete source files
These files are already installed. Do not paste duplicate classes into Assets. The setup guide contains the exact scene and Inspector steps. Authentication uses the Milestone 1B TravelDatabase and seeded schema without changing its version.

## Assets/TravelPlanning/Runtime/Accounts/AccountDatabase.cs

```csharp
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using SQLite;
using TravelPlanning.Data;

namespace TravelPlanning.Accounts
{
    /// <summary>Reads and writes accounts in the already initialized travel SQLite database.</summary>
    public sealed class AccountDatabase
    {
        private readonly TravelDatabase database;
        public AccountDatabase(TravelDatabase database)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
        }

        internal Task<AccountRecord> FindAsync(string email, CancellationToken token)
        {
            return database.ExecuteAsync(connection => connection.FindWithQuery<AccountRecord>(
                "SELECT id, email_normalized, password_hash, password_salt, password_iterations, password_algorithm FROM users WHERE email_normalized = ?", email), token);
        }

        internal Task<bool> InsertAsync(AccountRecord account, CancellationToken token)
        {
            return database.ExecuteAsync(connection =>
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    connection.Execute("INSERT INTO users (id, email_normalized, password_hash, password_salt, password_iterations, password_algorithm, created_utc) VALUES (?, ?, ?, ?, ?, ?, ?)",
                        account.Id, account.Email, account.Hash, account.Salt, account.Iterations, account.Algorithm,
                        DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
                    return true;
                }
                catch (SQLiteException error) when (error.Result == SQLite3.Result.Constraint)
                {
                    // Only a duplicate email is a normal form error; other database problems propagate.
                    if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM users WHERE email_normalized = ?", account.Email) > 0)
                        return false;
                    throw;
                }
            }, token);
        }
    }

    // Public for sqlite-net's simple row mapping. Never send this credential record to the UI.
    public sealed class AccountRecord
    {
        [Column("id")] public string Id { get; set; }
        [Column("email_normalized")] public string Email { get; set; }
        [Column("password_hash")] public string Hash { get; set; }
        [Column("password_salt")] public string Salt { get; set; }
        [Column("password_iterations")] public int Iterations { get; set; }
        [Column("password_algorithm")] public string Algorithm { get; set; }
    }
}
```

## Assets/TravelPlanning/Runtime/Accounts/AccountService.cs

```csharp
using System;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;

namespace TravelPlanning.Accounts
{
    /// <summary>Checks account forms and owns the current in-memory login session.</summary>
    public sealed class AccountService
    {
        private readonly AccountDatabase database;
        private readonly object sessionLock = new object();
        private long sessionGeneration;
        private string signedInEmail;
        private string signedInUserId;
        public string SignedInEmail { get { lock (sessionLock) return signedInEmail; } }
        public string SignedInUserId { get { lock (sessionLock) return signedInUserId; } }

        public AccountService(AccountDatabase database)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public async Task<AccountResult> RegisterAsync(string email, string password, string confirmation, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string normalized = NormalizeEmail(email);
            if (normalized == null)
                return new AccountResult(false, "Enter a valid email address, such as name@example.com.");
            if (string.IsNullOrWhiteSpace(password) || password.Length < 10 || password.Length > 128)
                return new AccountResult(false, "Use a password with 10 to 128 characters.");
            if (!string.Equals(password, confirmation, StringComparison.Ordinal))
                return new AccountResult(false, "The passwords do not match.");

            // The slow password calculation runs on a worker thread, never the Unity UI thread.
            var account = await Task.Run(() => PasswordHasher.Create(normalized, password), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!await database.InsertAsync(account, cancellationToken).ConfigureAwait(false))
                return new AccountResult(false, "An account with this email already exists. Please log in.");
            // Registration deliberately does not create a login session.
            return new AccountResult(true, "Account created. You can now log in.", normalized, account.Id);
        }

        public async Task<AccountResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            long generation;
            lock (sessionLock)
            {
                generation = ++sessionGeneration;
                signedInEmail = null;
                signedInUserId = null;
            }
            cancellationToken.ThrowIfCancellationRequested();
            string normalized = NormalizeEmail(email);
            if (normalized == null || string.IsNullOrWhiteSpace(password) || password.Length < 10 || password.Length > 128)
                return InvalidLogin();
            var account = await database.FindAsync(normalized, cancellationToken).ConfigureAwait(false);
            bool matches = await Task.Run(() => PasswordHasher.Verify(account, password), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            lock (sessionLock)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Logout or a newer login attempt invalidates this result, even if hashing finishes later.
                if (generation != sessionGeneration || !matches) return InvalidLogin();
                signedInEmail = account.Email;
                signedInUserId = account.Id;
                return new AccountResult(true, "You are logged in.", signedInEmail, signedInUserId);
            }
        }

        public void Logout()
        {
            lock (sessionLock)
            {
                ++sessionGeneration;
                signedInEmail = null;
                signedInUserId = null;
            }
        }

        private static AccountResult InvalidLogin() => new AccountResult(false, "Email or password is incorrect.");

        private static string NormalizeEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || email.IndexOf('\r') >= 0 || email.IndexOf('\n') >= 0) return null;
            string value = email.Trim().ToLowerInvariant();
            if (value.Length > 254) return null;
            try
            {
                var parsed = new MailAddress(value);
                return parsed.Address == value && parsed.Host.Contains(".") ? value : null;
            }
            catch (FormatException) { return null; }
        }
    }
}
```

## Assets/TravelPlanning/Runtime/Accounts/AccountResult.cs

```csharp
namespace TravelPlanning.Accounts
{
    /// <summary>A safe result for the UI, containing no password or stored credentials.</summary>
    public sealed class AccountResult
    {
        public bool Success { get; }
        public string Message { get; }
        public string Email { get; }
        public string UserId { get; }

        internal AccountResult(bool success, string message, string email = null, string userId = null)
        {
            Success = success;
            Message = message;
            Email = email;
            UserId = userId;
        }
    }
}
```

## Assets/TravelPlanning/Runtime/Accounts/PasswordHasher.cs

```csharp
using System;
using System.Security.Cryptography;

namespace TravelPlanning.Accounts
{
    /// <summary>PBKDF2 repeats a password calculation to make guessing expensive; each account has a random salt.</summary>
    internal static class PasswordHasher
    {
        private const string Algorithm = "PBKDF2-SHA256";
        private const int Iterations = 600000;
        private const int SaltBytes = 16;
        private const int HashBytes = 32;
        private static readonly byte[] DummySalt = new byte[SaltBytes];

        internal static AccountRecord Create(string email, string password)
        {
            var salt = new byte[SaltBytes];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(salt);
            byte[] hash = Derive(password, salt, Iterations);
            try
            {
                return new AccountRecord
                {
                    Id = Guid.NewGuid().ToString("N"), Email = email,
                    Hash = Convert.ToBase64String(hash), Salt = Convert.ToBase64String(salt),
                    Iterations = Iterations, Algorithm = Algorithm
                };
            }
            finally { Array.Clear(hash, 0, hash.Length); }
        }

        internal static bool Verify(AccountRecord account, string password)
        {
            if (account == null)
            {
                // Unknown emails still do the normal amount of expensive work.
                byte[] dummy = Derive(password, DummySalt, Iterations);
                Array.Clear(dummy, 0, dummy.Length);
                return false;
            }
            // Reject damaged or unexpected metadata before allocating/starting an expensive calculation.
            if (account.Algorithm != Algorithm || account.Iterations < Iterations || account.Iterations > 2000000 ||
                account.Salt == null || account.Salt.Length != 24 || account.Hash == null || account.Hash.Length != 44)
                return false;
            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(account.Salt);
                expected = Convert.FromBase64String(account.Hash);
            }
            catch (FormatException) { return false; }
            if (salt.Length != SaltBytes || expected.Length != HashBytes) return false;
            byte[] actual = Derive(password, salt, account.Iterations);
            try { return CryptographicOperations.FixedTimeEquals(actual, expected); }
            finally
            {
                Array.Clear(actual, 0, actual.Length);
                Array.Clear(expected, 0, expected.Length);
            }
        }

        private static byte[] Derive(string password, byte[] salt, int iterations)
        {
            using (var calculation = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                return calculation.GetBytes(HashBytes);
        }
    }
}
```

## Assets/TravelPlanning/UI/LoginPage.cs

```csharp
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
    public sealed class LoginPage : MonoBehaviour
    {
        [SerializeField] private GameObject loginPanel, registrationPanel, signedInPanel;
        [SerializeField] private TMP_InputField emailField, passwordField, registrationEmail, registrationPassword, confirmationField;
        [SerializeField] private TMP_Text feedback, registrationFeedback, signedInEmail;
        [SerializeField] private UnityEngine.UI.Button submitButton, switchButton, registerButton, backButton, logoutButton, forgotButton;
        [SerializeField, HideInInspector] private string developmentDatabaseFile;
        private AccountService service;
        private TMP_InputField[] loginFields, registrationFields;
        private bool busy;
        public bool IsReady { get; private set; }
        public bool IsBusy => busy;
        public string SignedInEmail => service?.SignedInEmail;
        public string SignedInUserId => service?.SignedInUserId;
        public event Action<string> LoggedIn;

        private async void Start()
        {
            if (!loginPanel || !registrationPanel || !signedInPanel || !emailField || !passwordField || !registrationEmail || !registrationPassword || !confirmationField || !feedback || !registrationFeedback || !signedInEmail || !submitButton || !switchButton || !registerButton || !backButton || !logoutButton || !forgotButton)
            {
                Debug.LogError("LoginPage: account UI references are missing. Reopen the prepared Login scene and check AccountController.");
                enabled = false;
                if (ArgumentPresent("-authSmoke")) Application.Quit(2);
                return;
            }
            loginFields = new[] { emailField, passwordField };
            registrationFields = new[] { registrationEmail, registrationPassword, confirmationField };
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
                string file = "travel.db";
                string overrideFile = Argument("-authFile") ?? developmentDatabaseFile;
                // Smoke automation must never write fixture accounts into the normal user database.
                string smokeFile = Argument("-authFile");
                if (ArgumentPresent("-authSmoke") && (!Debug.isDebugBuild || string.IsNullOrEmpty(smokeFile) || !smokeFile.StartsWith("auth-smoke-", StringComparison.Ordinal)))
                    throw new ArgumentException("Authentication smoke checks require an explicit isolated auth-smoke- database.");
                if (Debug.isDebugBuild && !string.IsNullOrEmpty(overrideFile))
                {
                    if (Path.GetFileName(overrideFile) != overrideFile || !overrideFile.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("The test database must be a .db file name.");
                    file = overrideFile;
                }
                var database = new TravelDatabase(Path.Combine(Application.streamingAssetsPath, "Database", "travel_seed.db"), Path.Combine(Application.persistentDataPath, file));
                await database.InitializeAsync(destroyCancellationToken);
                if (!this) return;
                service = new AccountService(new AccountDatabase(database));
                IsReady = true;
                feedback.text = "";
                SetBusy(false);
                if (Debug.isDebugBuild && ArgumentPresent("-authSmoke")) await RunSmokeAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                if (!this) return;
                feedback.text = "The local database could not be opened. Your existing files have been kept. Check the database setup guide.";
                Debug.LogError("AUTH_DATABASE_UNAVAILABLE");
                if (ArgumentPresent("-authSmoke")) Application.Quit(1);
            }
        }

        private async void Submit(bool registering)
        {
            if (busy || !IsReady) return;
            var email = registering ? registrationEmail : emailField;
            var password = registering ? registrationPassword : passwordField;
            var message = registering ? registrationFeedback : feedback;
            SetBusy(true);
            message.text = "Please wait...";
            try
            {
                var result = registering
                    ? await service.RegisterAsync(email.text, password.text, confirmationField.text, destroyCancellationToken)
                    : await service.LoginAsync(email.text, password.text, destroyCancellationToken);
                if (!this) return;
                message.text = result.Message;
                if (result.Success && registering)
                {
                    emailField.text = result.Email;
                    ShowRegistration(false);
                    feedback.text = result.Message;
                }
                else if (result.Success)
                {
                    signedInEmail.text = result.Email;
                    loginPanel.SetActive(false);
                    registrationPanel.SetActive(false);
                    signedInPanel.SetActive(true);
                    LoggedIn?.Invoke(result.Email);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (this) message.text = "We could not complete that request. Please try again."; }
            finally
            {
                if (this) { ClearPasswords(); SetBusy(false); }
            }
        }

        private void ShowRegistration(bool show)
        {
            // This controller lives outside these panels, so changing screens never stops a request.
            ClearPasswords();
            registrationEmail.text = emailField.text;
            feedback.text = registrationFeedback.text = "";
            loginPanel.SetActive(!show);
            registrationPanel.SetActive(show);
            signedInPanel.SetActive(false);
        }
        private void Logout()
        {
            service.Logout();
            signedInEmail.text = "";
            ShowRegistration(false);
            feedback.text = "You are logged out.";
        }
        private void ClearPasswords() { passwordField.text = registrationPassword.text = confirmationField.text = ""; }
        private void SubmitLogin() => Submit(false);
        private void SubmitRegistration() => Submit(true);
        private void OpenRegistration() => ShowRegistration(true);
        private void OpenLogin() => ShowRegistration(false);
        private void ExplainPasswordRecovery() => feedback.text = "Password recovery is not available for this local demo. Create a new account if needed.";
        private void Update()
        {
            if (!IsReady || busy || signedInPanel.activeSelf) return;
            bool registering = registrationPanel.activeSelf;
            var fields = registering ? registrationFields : loginFields;
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                int current = Array.FindIndex(fields, field => field.isFocused);
                int direction = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1;
                int next = current < 0 ? 0 : (current + direction + fields.Length) % fields.Length;
                fields[next].Select(); fields[next].ActivateInputField();
            }
            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && Array.Exists(fields, field => field.isFocused)) Submit(registering);
        }
        private void OnDestroy()
        {
            service?.Logout();
            if (submitButton) submitButton.onClick.RemoveListener(SubmitLogin);
            if (registerButton) registerButton.onClick.RemoveListener(SubmitRegistration);
            if (switchButton) switchButton.onClick.RemoveListener(OpenRegistration);
            if (backButton) backButton.onClick.RemoveListener(OpenLogin);
            if (logoutButton) logoutButton.onClick.RemoveListener(Logout);
            if (forgotButton) forgotButton.onClick.RemoveListener(ExplainPasswordRecovery);
        }
        private void SetBusy(bool value)
        {
            busy = value;
            foreach (var button in new[] { submitButton, switchButton, registerButton, backButton, logoutButton, forgotButton }) button.interactable = !value;
            foreach (var field in new[] { emailField, passwordField, registrationEmail, registrationPassword, confirmationField }) field.interactable = !value;
        }
        private static string Argument(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
            return null;
        }
        private static bool ArgumentPresent(string key) => Array.IndexOf(Environment.GetCommandLineArgs(), key) >= 0;

        // Development-build automation drives the same controls a person uses. Never logs credentials.
        private async Task RunSmokeAsync()
        {
            try
            {
                var elapsed = System.Diagnostics.Stopwatch.StartNew();
                string email = "ui-smoke@example.test";
                string password = "Travel demo smoke phrase 2027!";
                if (ArgumentPresent("-authRegister"))
                {
                    switchButton.onClick.Invoke();
                    registrationEmail.text = email;
                    registrationPassword.text = confirmationField.text = password;
                    registerButton.onClick.Invoke();
                    await WaitForRequest();
                    if (!loginPanel.activeSelf || service.SignedInEmail != null) throw new InvalidOperationException();
                }
                emailField.text = email;
                passwordField.text = "Definitely an incorrect password";
                submitButton.onClick.Invoke();
                await WaitForRequest();
                if (service.SignedInEmail != null || !loginPanel.activeSelf) throw new InvalidOperationException();
                passwordField.text = password;
                submitButton.onClick.Invoke();
                await WaitForRequest();
                if (service.SignedInEmail != email || !signedInPanel.activeSelf || passwordField.text.Length != 0) throw new InvalidOperationException();
                logoutButton.onClick.Invoke();
                if (service.SignedInEmail != null || !loginPanel.activeSelf) throw new InvalidOperationException();
                Debug.Log("AUTH_UI_SMOKE_PASS registration=" + ArgumentPresent("-authRegister") + " elapsedMs=" + elapsed.ElapsedMilliseconds);
                Application.Quit(0);
            }
            catch (Exception) { Debug.LogError("AUTH_UI_SMOKE_FAIL"); Application.Quit(1); }
        }
        private async Task WaitForRequest()
        {
            for (int i = 0; busy && i < 3000; i++) await Task.Delay(10, destroyCancellationToken);
            if (busy) throw new TimeoutException();
        }
    }
}
```

## Assets/TravelPlanning/UI/Editor/AuthSetup.cs

```csharp
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Adds account behavior to Kevin's imported scene, preserving his login layout.</summary>
    public static class AuthSetup
    {
        public const string ScenePath = "Assets/Scenes/Login.unity";
        public const string BuildPath = "Builds/Authentication/TravelPlannerAuthentication.exe";
        [MenuItem("Travel Planning/Authentication/1 - Prepare Login Scene")]
        public static void Prepare()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            TravelSeedBuilder.BuildSeed();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            if (UnityEngine.Object.FindFirstObjectByType<LoginPage>() != null)
            {
                // Include inactive registration cards when refreshing policy text in a prepared scene.
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                        if (text.name == "PasswordPolicy" && text.transform.parent.name == "RegistrationCard")
                            text.text = "Use 10-128 characters. Spaces are welcome.\nThis account is stored only on this computer.";
                EditorSceneManager.SaveScene(scene);
                return;
            }
            Transform card = GameObject.Find("Canvas/Right Panel/LoginCard").transform;
            var email = card.Find("EmailInput").GetComponent<TMP_InputField>();
            var password = card.Find("PasswordInput").GetComponent<TMP_InputField>();
            email.characterLimit = 254;
            password.characterLimit = 128;
            password.contentType = TMP_InputField.ContentType.Password;
            var submit = card.Find("LoginButton").GetComponent<UnityEngine.UI.Button>();
            var create = TextButton(card.Find("Create an account"));
            var forgot = TextButton(card.Find("Forgot?"));
            var feedback = Text(card, "AccountFeedback", "", new Vector2(0, -600), new Vector2(580, 90), 22);
            var registration = UnityEngine.Object.Instantiate(card.gameObject, card.parent);
            registration.name = "RegistrationCard";
            Transform form = registration.transform;
            form.GetComponent<RectTransform>().sizeDelta = new Vector2(580, 800);
            form.Find("Welcome back").GetComponent<TMP_Text>().text = "Create account";
            form.Find("Subtitle").GetComponent<TMP_Text>().text = "Save your trips on this computer";
            UnityEngine.Object.DestroyImmediate(form.Find("Forgot?").gameObject);
            UnityEngine.Object.DestroyImmediate(form.Find("First Time?").gameObject);
            var registrationEmail = form.Find("EmailInput").GetComponent<TMP_InputField>();
            var registrationPassword = form.Find("PasswordInput").GetComponent<TMP_InputField>();
            Move(form.Find("EmailLabel"), -184);
            form.Find("EmailLabel").GetComponent<RectTransform>().sizeDelta = new Vector2(580, 24);
            Move(form.Find("PasswordLabel"), -285);
            form.Find("PasswordLabel").GetComponent<RectTransform>().sizeDelta = new Vector2(580, 24);
            Move(registrationPassword.transform, -330);
            var confirmation = UnityEngine.Object.Instantiate(registrationPassword, form);
            confirmation.name = "ConfirmationInput";
            Move(confirmation.transform, -425);
            var confirmationLabel = UnityEngine.Object.Instantiate(form.Find("PasswordLabel").gameObject, form);
            confirmationLabel.name = "ConfirmationLabel";
            confirmationLabel.GetComponent<TMP_Text>().text = "Confirm password";
            Move(confirmationLabel.transform, -380);
            confirmationLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(580, 24);
            var registrationSubmit = form.Find("LoginButton").GetComponent<UnityEngine.UI.Button>();
            Move(registrationSubmit.transform, -550);
            registrationSubmit.GetComponentInChildren<TMP_Text>().text = "Create account";
            var back = form.Find("Create an account").GetComponent<UnityEngine.UI.Button>();
            back.GetComponent<TMP_Text>().text = "Back to sign in";
            Move(back.transform, -615);
            Text(form, "PasswordPolicy", "Use 10-128 characters. Spaces are welcome.\nThis account is stored only on this computer.", new Vector2(0, -465), new Vector2(580, 50), 20);
            var registerFeedback = form.Find("AccountFeedback").GetComponent<TMP_Text>();
            Move(registerFeedback.transform, -665);
            var signedIn = new GameObject("SignedInCard", typeof(RectTransform));
            signedIn.transform.SetParent(card.parent, false);
            CopyRect(card.GetComponent<RectTransform>(), signedIn.GetComponent<RectTransform>());
            Text(signedIn.transform, "Title", "Welcome", new Vector2(0, -65), new Vector2(580, 80), 48);
            var accountEmail = Text(signedIn.transform, "AccountEmail", "", new Vector2(0, -180), new Vector2(580, 80), 28);
            Text(signedIn.transform, "NextMilestone", "You are signed in.\nFlight search is the next milestone.", new Vector2(0, -290), new Vector2(580, 100), 26);
            var logout = UnityEngine.Object.Instantiate(submit, signedIn.transform);
            logout.name = "LogoutButton";
            logout.GetComponentInChildren<TMP_Text>().text = "Log out";
            Move(logout.transform, -460);
            var controller = new GameObject("AccountController").AddComponent<LoginPage>();
            var fields = new SerializedObject(controller);
            Set(fields, "loginPanel", card.gameObject); Set(fields, "registrationPanel", registration); Set(fields, "signedInPanel", signedIn);
            Set(fields, "emailField", email); Set(fields, "passwordField", password); Set(fields, "registrationEmail", registrationEmail); Set(fields, "registrationPassword", registrationPassword); Set(fields, "confirmationField", confirmation);
            Set(fields, "feedback", feedback); Set(fields, "registrationFeedback", registerFeedback); Set(fields, "signedInEmail", accountEmail);
            Set(fields, "submitButton", submit); Set(fields, "switchButton", create); Set(fields, "registerButton", registrationSubmit); Set(fields, "backButton", back); Set(fields, "logoutButton", logout); Set(fields, "forgotButton", forgot);
            fields.ApplyModifiedPropertiesWithoutUndo();
            registration.SetActive(false); signedIn.SetActive(false);
            var events = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            foreach (var module in events.GetComponents<BaseInputModule>()) UnityEngine.Object.DestroyImmediate(module);
            events.gameObject.AddComponent<StandaloneInputModule>();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("AUTH_SCENE_READY " + ScenePath);
        }
        [MenuItem("Travel Planning/Authentication/2 - Build Windows x64")]
        public static void BuildWindows()
        {
            Prepare();
            Directory.CreateDirectory(Path.GetDirectoryName(BuildPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = BuildPath, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Authentication build failed.");
            var notices = Path.Combine(Path.GetDirectoryName(BuildPath), "ThirdPartyNotices");
            Directory.CreateDirectory(notices);
            foreach (string file in Directory.GetFiles("Assets/Plugins/SQLite/Licenses")) if (!file.EndsWith(".meta")) File.Copy(file, Path.Combine(notices, Path.GetFileName(file)), true);
            File.Copy("Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt", Path.Combine(notices, "LiberationSans-OFL.txt"), true);
            File.Copy("Assets/Plugins/LiteDB/LICENSE.txt", Path.Combine(notices, "LiteDB-LICENSE.txt"), true);
            Debug.Log("AUTH_BUILD_PASS " + Path.GetFullPath(BuildPath));
        }
        private static UnityEngine.UI.Button TextButton(Transform target)
        {
            var graphic = target.GetComponent<TMP_Text>(); graphic.raycastTarget = true;
            var button = target.GetComponent<UnityEngine.UI.Button>() ?? target.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = graphic;
            button.transition = Selectable.Transition.None;
            return button;
        }
        private static TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = obj.GetComponent<TextMeshProUGUI>(); text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset"); text.text = value; text.fontSize = fontSize; text.color = new Color32(35, 43, 57, 255); text.raycastTarget = false;
            return text;
        }
        private static void Move(Transform target, float y) { var r = target.GetComponent<RectTransform>(); r.anchoredPosition = new Vector2(r.anchoredPosition.x, y); }
        private static void CopyRect(RectTransform source, RectTransform target) { target.anchorMin = source.anchorMin; target.anchorMax = source.anchorMax; target.pivot = source.pivot; target.anchoredPosition = source.anchoredPosition; target.sizeDelta = source.sizeDelta; }
        private static void Set(SerializedObject fields, string name, UnityEngine.Object value) => fields.FindProperty(name).objectReferenceValue = value;
    }
}
```

## Assets/TravelPlanning/UI/Editor/LoginPageSetup.cs

```csharp
namespace TravelPlanning.UI.Editor
{
    /// <summary>Compatibility entry point; always opens the team's actual login design.</summary>
    public static class LoginPageSetup
    {
        public static void CreateScene() => AuthSetup.Prepare();
    }
}
```

## Assets/TravelPlanning/Tests/EditMode/AccountTests.cs

```csharp
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TravelPlanning.Accounts;
using TravelPlanning.Data;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    /// <summary>Exercises real SQLite and PBKDF2 against temporary databases, never user accounts.</summary>
    public sealed class AccountTests
    {
        private const string Password = " My travel password! ";
        private string folder;
        private string path;
        private TravelDatabase database;
        private AccountService service;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "TravelAccountTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, "travel.db");
            database = NewDatabase();
            service = new AccountService(new AccountDatabase(database));
        }

        [TearDown]
        public void TearDown() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }

        [UnityTest]
        public IEnumerator AccountsSurviveReopenAndLogoutClearsIdentity()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            var registration = service.RegisterAsync(" Duy@Example.com ", Password, Password);
            yield return Wait(registration);
            Assert.That(registration.Result.Success, Is.True);
            Assert.That(registration.Result.UserId, Is.Not.Empty);
            Assert.That(service.SignedInEmail, Is.Null);
            yield return Wait(database.ExecuteAsync(connection => connection.Execute(
                "INSERT INTO trips (id, user_id, name, start_date, end_date, created_utc, updated_utc) VALUES (?, ?, ?, ?, ?, ?, ?)",
                "preserved-trip", registration.Result.UserId, "My saved trip", "2027-06-15", "2027-06-22", "2026-09-29T00:00:00Z", "2026-09-29T00:00:00Z"), CancellationToken.None));
            var reopened = NewDatabase();
            yield return Wait(reopened.InitializeAsync(CancellationToken.None));
            var second = new AccountService(new AccountDatabase(reopened));
            var login = second.LoginAsync("DUY@example.com", Password);
            yield return Wait(login);
            Assert.That(login.Result.Success, Is.True);
            Assert.That(second.SignedInEmail, Is.EqualTo("duy@example.com"));
            Assert.That(second.SignedInUserId, Is.EqualTo(registration.Result.UserId));
            var tripName = reopened.ExecuteAsync(connection => connection.ExecuteScalar<string>(
                "SELECT name FROM trips WHERE id = ? AND user_id = ?", "preserved-trip", second.SignedInUserId), CancellationToken.None);
            yield return Wait(tripName);
            Assert.That(tripName.Result, Is.EqualTo("My saved trip"));
            second.Logout();
            Assert.That(second.SignedInEmail, Is.Null);
            Assert.That(second.SignedInUserId, Is.Null);
        }

        [UnityTest]
        public IEnumerator EqualPasswordsHaveDifferentSaltsAndHashesWithoutPlaintextStorage()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            var registrations = Task.WhenAll(service.RegisterAsync("first@example.com", Password, Password),
                service.RegisterAsync("second@example.com", Password, Password));
            yield return Wait(registrations);
            Assert.That(registrations.Result.All(result => result.Success), Is.True);
            var rows = database.ExecuteAsync(connection => connection.Query<AccountRecord>("SELECT * FROM users ORDER BY email_normalized"), CancellationToken.None);
            yield return Wait(rows);
            Assert.That(rows.Result.Count, Is.EqualTo(2));
            Assert.That(rows.Result[0].Salt, Is.Not.EqualTo(rows.Result[1].Salt));
            Assert.That(rows.Result[0].Hash, Is.Not.EqualTo(rows.Result[1].Hash));
            foreach (var row in rows.Result)
            {
                Assert.That(row.Hash, Is.Not.EqualTo(Password));
                Assert.That(Convert.FromBase64String(row.Hash).Length, Is.EqualTo(32));
                Assert.That(Convert.FromBase64String(row.Salt).Length, Is.EqualTo(16));
                Assert.That(row.Iterations, Is.EqualTo(600000));
                Assert.That(row.Algorithm, Is.EqualTo("PBKDF2-SHA256"));
            }
            Assert.That(System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(path)), Does.Not.Contain(Password));
        }

        [UnityTest]
        public IEnumerator ConcurrentDuplicateCannotReplaceWinningPassword()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            const string otherPassword = "A different password!";
            var first = service.RegisterAsync("same@example.com", Password, Password);
            var second = service.RegisterAsync(" SAME@example.com ", otherPassword, otherPassword);
            yield return Wait(Task.WhenAll(first, second));
            Assert.That(first.Result.Success ^ second.Result.Success, Is.True);
            var winner = service.LoginAsync("same@example.com", first.Result.Success ? Password : otherPassword);
            yield return Wait(winner);
            Assert.That(winner.Result.Success, Is.True);
            var loser = service.LoginAsync("same@example.com", first.Result.Success ? otherPassword : Password);
            yield return Wait(loser);
            Assert.That(loser.Result.Success, Is.False);
        }

        [UnityTest]
        public IEnumerator InvalidFormsDoNotWriteAccounts()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            foreach (string email in new[] { null, "", "not-email", "Name <name@example.com>", "a@example.com\n", new string('a', 250) + "@example.com" })
            {
                var result = service.RegisterAsync(email, Password, Password);
                yield return Wait(result);
                Assert.That(result.Result.Success, Is.False);
            }
            foreach (string password in new[] { null, "", new string('x', 9), new string(' ', 10), new string('x', 129) })
            {
                var result = service.RegisterAsync("test@example.com", password, password);
                yield return Wait(result);
                Assert.That(result.Result.Success, Is.False);
            }
            var mismatch = service.RegisterAsync("test@example.com", Password, Password.Trim());
            yield return Wait(mismatch);
            Assert.That(mismatch.Result.Success, Is.False);
            var count = database.ExecuteAsync(connection => connection.ExecuteScalar<int>("SELECT COUNT(*) FROM users"), CancellationToken.None);
            yield return Wait(count);
            Assert.That(count.Result, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PasswordLengthsTenAnd128CanRegisterAndLogin()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            foreach (int length in new[] { 10, 128 })
            {
                string password = new string('x', length);
                string email = "boundary" + length + "@example.com";
                var registration = service.RegisterAsync(email, password, password);
                yield return Wait(registration);
                Assert.That(registration.Result.Success, Is.True, "Registration length " + length);
                var login = service.LoginAsync(email, password);
                yield return Wait(login);
                Assert.That(login.Result.Success, Is.True, "Login length " + length);
                service.Logout();
            }
        }

        [UnityTest]
        public IEnumerator FailedLoginClearsSessionAndUsesGenericMessage()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(service.RegisterAsync("test@example.com", Password, Password));
            var good = service.LoginAsync("test@example.com", Password);
            yield return Wait(good);
            Assert.That(good.Result.Success, Is.True);
            var wrong = service.LoginAsync("test@example.com", "A wrong password!");
            yield return Wait(wrong);
            Assert.That(wrong.Result.Success, Is.False);
            Assert.That(service.SignedInUserId, Is.Null);
            var missing = service.LoginAsync("missing@example.com", Password);
            yield return Wait(missing);
            Assert.That(missing.Result.Success, Is.False);
            Assert.That(missing.Result.Message, Is.EqualTo(wrong.Result.Message));
            Assert.That(missing.Result.Email, Is.Null);
        }

        [UnityTest]
        public IEnumerator PasswordComparisonPreservesCaseAndSpaces()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(service.RegisterAsync("test@example.com", Password, Password));
            foreach (string altered in new[] { Password.Trim(), Password.ToLowerInvariant() })
            {
                var bad = service.LoginAsync("test@example.com", altered);
                yield return Wait(bad);
                Assert.That(bad.Result.Success, Is.False);
            }
            var good = service.LoginAsync("test@example.com", Password);
            yield return Wait(good);
            Assert.That(good.Result.Success, Is.True);
        }

        [UnityTest]
        public IEnumerator SqlTextInEmailIsStoredAsData()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            const string email = "o'brien@example.com";
            var registration = service.RegisterAsync(email, Password, Password);
            yield return Wait(registration);
            Assert.That(registration.Result.Success, Is.True);
            var attack = service.LoginAsync("' OR 1=1 --@example.com", Password);
            yield return Wait(attack);
            Assert.That(attack.Result.Success, Is.False);
            var login = service.LoginAsync(email, Password);
            yield return Wait(login);
            Assert.That(login.Result.Success, Is.True);
        }

        [UnityTest]
        public IEnumerator MalformedCredentialMetadataFailsClosed()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(service.RegisterAsync("test@example.com", Password, Password));
            var originals = database.ExecuteAsync(connection => connection.FindWithQuery<AccountRecord>("SELECT * FROM users LIMIT 1"), CancellationToken.None);
            yield return Wait(originals);
            var original = originals.Result;
            // Each malformed row must be rejected before expensive work with an attacker-selected cost.
            object[][] badValues = {
                new object[] { "UNKNOWN", 600000, original.Salt, original.Hash },
                new object[] { "PBKDF2-SHA256", 1, original.Salt, original.Hash },
                new object[] { "PBKDF2-SHA256", int.MaxValue, original.Salt, original.Hash },
                new object[] { "PBKDF2-SHA256", 600000, new string('!', 24), original.Hash },
                new object[] { "PBKDF2-SHA256", 600000, original.Salt, new string('!', 44) },
                new object[] { "PBKDF2-SHA256", 600000, "AA==", original.Hash }
            };
            foreach (var values in badValues)
            {
                yield return Wait(database.ExecuteAsync(connection => connection.Execute(
                    "UPDATE users SET password_algorithm = ?, password_iterations = ?, password_salt = ?, password_hash = ?", values), CancellationToken.None));
                var login = service.LoginAsync("test@example.com", Password);
                yield return Wait(login);
                Assert.That(login.Result.Success, Is.False);
                Assert.That(login.Result.Message, Is.EqualTo("Email or password is incorrect."));
            }
        }

        [UnityTest]
        public IEnumerator LogoutWhileLoginIsPendingCannotRestoreSession()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(service.RegisterAsync("test@example.com", Password, Password));
            using (var release = new ManualResetEventSlim(false))
            using (var entered = new ManualResetEventSlim(false))
            {
                var blocker = database.ExecuteAsync(connection => { entered.Set(); return release.Wait(TimeSpan.FromSeconds(30)); }, CancellationToken.None);
                while (!entered.IsSet) yield return null;
                Task<AccountResult> login;
                try
                {
                    login = service.LoginAsync("test@example.com", Password);
                    service.Logout();
                }
                finally { release.Set(); }
                yield return Wait(blocker);
                yield return Wait(login);
                Assert.That(login.Result.Success, Is.False);
                Assert.That(service.SignedInUserId, Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator CancellationPreventsRegistrationAndLoginSession()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            using (var release = new ManualResetEventSlim(false))
            using (var entered = new ManualResetEventSlim(false))
            using (var cancellation = new CancellationTokenSource())
            {
                var blocker = database.ExecuteAsync(connection => { entered.Set(); return release.Wait(TimeSpan.FromSeconds(30)); }, CancellationToken.None);
                while (!entered.IsSet) yield return null;
                Task<AccountResult> pending;
                try
                {
                    pending = service.RegisterAsync("cancelled@example.com", Password, Password, cancellation.Token);
                    cancellation.Cancel();
                }
                finally { release.Set(); }
                yield return Wait(blocker);
                yield return WaitCompletion(pending);
                Assert.That(pending.IsCanceled, Is.True);
            }
            var count = database.ExecuteAsync(connection => connection.ExecuteScalar<int>("SELECT COUNT(*) FROM users"), CancellationToken.None);
            yield return Wait(count);
            Assert.That(count.Result, Is.Zero);
            yield return Wait(service.RegisterAsync("test@example.com", Password, Password));
            using (var release = new ManualResetEventSlim(false))
            using (var entered = new ManualResetEventSlim(false))
            using (var cancellation = new CancellationTokenSource())
            {
                var blocker = database.ExecuteAsync(connection => { entered.Set(); return release.Wait(TimeSpan.FromSeconds(30)); }, CancellationToken.None);
                while (!entered.IsSet) yield return null;
                Task<AccountResult> pending;
                try
                {
                    pending = service.LoginAsync("test@example.com", Password, cancellation.Token);
                    cancellation.Cancel();
                }
                finally { release.Set(); }
                yield return Wait(blocker);
                yield return WaitCompletion(pending);
                Assert.That(pending.IsCanceled, Is.True);
                Assert.That(service.SignedInUserId, Is.Null);
            }
        }

        [UnityTest]
        public IEnumerator LegacyAccountFileIsNeverReadOrModified()
        {
            string legacy = Path.Combine(folder, "accounts.db");
            byte[] original = { 7, 9, 12, 34, 99 };
            File.WriteAllBytes(legacy, original);
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(service.RegisterAsync("test@example.com", Password, Password));
            Assert.That(File.ReadAllBytes(legacy), Is.EqualTo(original));
        }

        private TravelDatabase NewDatabase() => new TravelDatabase(
            Path.Combine(Application.streamingAssetsPath, "Database", "travel_seed.db"), path);

        private static IEnumerator Wait(Task task)
        {
            yield return WaitCompletion(task);
            task.GetAwaiter().GetResult(); // Safe only after the task finished; never block Unity's main thread.
        }

        private static IEnumerator WaitCompletion(Task task)
        {
            double deadline = UnityEditor.EditorApplication.timeSinceStartup + 120;
            while (!task.IsCompleted)
            {
                Assert.That(UnityEditor.EditorApplication.timeSinceStartup, Is.LessThan(deadline), "Account operation timed out.");
                yield return null;
            }
        }
    }
}
```

## Assets/TravelPlanning/Tests/EditMode/AuthenticationSceneTests.cs

```csharp
using System.Collections;
using NUnit.Framework;
using TMPro;
using TravelPlanning.UI;
using TravelPlanning.UI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TravelPlanning.Tests
{
    public sealed class AuthenticationSceneTests
    {
        [Test]
        public void KevinLoginGeometryAndLegacyInputArePreserved()
        {
            EditorSceneManager.OpenScene(AuthSetup.ScenePath);
            var card = GameObject.Find("Canvas/Right Panel/LoginCard").transform;
            Assert.That(card.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(580, 700)));
            Assert.That(card.Find("EmailInput").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(0, -230)));
            Assert.That(card.Find("PasswordInput").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(0, -355)));
            Assert.That(card.Find("LoginButton").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(0, -460)));
            Assert.That(card.Find("Welcome back").GetComponent<TMP_Text>().text.Trim(), Is.EqualTo("Welcome back"));
            Assert.That(GameObject.Find("Canvas").GetComponent<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(1920, 1080)));
            Assert.That(Object.FindFirstObjectByType<EventSystem>().GetComponent<StandaloneInputModule>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<EventSystem>().GetComponents<BaseInputModule>().Length, Is.EqualTo(1));
        }
        [Test]
        public void AccountControllerHasEveryReferenceAndLivesOutsideSwitchablePanels()
        {
            EditorSceneManager.OpenScene(AuthSetup.ScenePath);
            var page = Object.FindFirstObjectByType<LoginPage>();
            Assert.That(page.transform.parent, Is.Null);
            var serialized = new SerializedObject(page);
            foreach (string name in new[] { "loginPanel", "registrationPanel", "signedInPanel", "emailField", "passwordField", "registrationEmail", "registrationPassword", "confirmationField", "feedback", "registrationFeedback", "signedInEmail", "submitButton", "switchButton", "registerButton", "backButton", "logoutButton", "forgotButton" })
                Assert.That(serialized.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
        }
        [UnityTest]
        public IEnumerator FormSwitchingAndEmptySubmissionStayResponsive()
        {
            EditorSceneManager.OpenScene(AuthSetup.ScenePath);
            string file = "auth-editor-" + System.Guid.NewGuid().ToString("N") + ".db";
            var configuration = new SerializedObject(Object.FindFirstObjectByType<LoginPage>());
            configuration.FindProperty("developmentDatabaseFile").stringValue = file;
            configuration.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            var page = Object.FindFirstObjectByType<LoginPage>();
            // Enter/Exit Play mode reloads the C# domain; recover the serialized test file before cleanup.
            string activeTestFile = new SerializedObject(page).FindProperty("developmentDatabaseFile").stringValue;
            Assert.That(activeTestFile, Does.StartWith("auth-editor-"));
            Assert.That(System.IO.Path.GetFileName(activeTestFile), Is.EqualTo(activeTestFile));
            float deadline = Time.realtimeSinceStartup + 30;
            while (!page.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(page.IsReady, Is.True);
            var right = GameObject.Find("Canvas/Right Panel").transform;
            var login = right.Find("LoginCard");
            var registration = right.Find("RegistrationCard");
            login.Find("Create an account").GetComponent<Button>().onClick.Invoke();
            Assert.That(registration.gameObject.activeSelf, Is.True);
            Assert.That(login.gameObject.activeSelf, Is.False);
            registration.Find("LoginButton").GetComponent<Button>().onClick.Invoke();
            while (page.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(page.IsBusy, Is.False);
            Assert.That(registration.Find("AccountFeedback").GetComponent<TMP_Text>().text, Is.Not.Empty);
            Assert.That(page.SignedInEmail, Is.Null);
            registration.Find("Create an account").GetComponent<Button>().onClick.Invoke();
            Assert.That(login.gameObject.activeSelf, Is.True);
            Object.Destroy(page.gameObject);
            yield return null;
            login.Find("Create an account").GetComponent<Button>().onClick.Invoke();
            Assert.That(registration.gameObject.activeSelf, Is.False, "Destroyed controllers must detach their button listeners.");
            string testPath = System.IO.Path.Combine(Application.persistentDataPath, activeTestFile);
            Assert.That(System.IO.File.Exists(testPath), Is.True, "The isolated test database should have been created.");
            System.IO.File.Delete(testPath);
            yield return new ExitPlayMode();
        }
    }
}
```

## tools/Test-Authentication.ps1

```powershell
param([string]$BuildRoot = 'Builds/Authentication')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $BuildRoot).Path
$exe = Join-Path $root 'TravelPlannerAuthentication.exe'
$run = [Guid]::NewGuid().ToString('N')
$file = "auth-smoke-$run.db"
$logs = Join-Path (Get-Location).Path "Logs/Authentication/$run"
New-Item -ItemType Directory -Path $logs -Force | Out-Null
foreach ($phase in @('register','reopen')) {
    $log = Join-Path $logs "$phase.log"
    $arguments = @('-batchmode','-nographics','-authSmoke','-authFile',$file,'-logFile',('"' + $log + '"'))
    if ($phase -eq 'register') { $arguments += '-authRegister' }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(90000)) { $process.Kill(); throw "Authentication $phase timed out." }
    $process.Refresh()
    $output = Get-Content -LiteralPath $log -Raw
    if ($process.ExitCode -ne 0 -or $output -notmatch 'AUTH_UI_SMOKE_PASS') { throw "Authentication $phase failed. See $log" }
    Write-Output "PASS $phase - real UI registration/login/logout checks. Log: $log"
}
```

