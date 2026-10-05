# Milestone 2 — Secure local accounts

## Scope

Register, sign in and sign out using the existing SQLite `travel.db`. Kevin's login on `kevin-frontend-login` is the visual source. Accounts start fresh: the previous LiteDB `accounts.db` is neither opened, converted nor deleted. This milestone does not implement flight search or email/password recovery.

## Security choices

A password hash is a one-way derived value used to check a password without saving it. Each account receives a random 16-byte salt, which makes identical passwords produce different hashes. PBKDF2-HMAC-SHA256 repeats the derivation 600,000 times, yielding 32 bytes; the database stores hash/salt as Base64 plus the algorithm and iteration count. The comparison uses a fixed-time byte comparison. Unsupported or malformed stored credentials are rejected, and iteration counts are bounded before hashing.

Registration requires a plain email address and a password of 10–128 characters. Spaces and mixed case in passwords are preserved; confirmation must match exactly. Email addresses are trimmed and normalized to lowercase for this local MVP. There are no forced punctuation/digit rules. Registration returns to login rather than automatically signing in. Unknown email and wrong password share the same login error. Salt generation uses the platform cryptographic random generator.

These choices use Microsoft's .NET Standard 2.1 cryptographic APIs, already available in Unity; no extra authentication package is installed. PBKDF2-SHA256's 600,000-iteration baseline follows the [OWASP password-storage guidance](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html). This is a local demonstration account system, not online identity verification. Someone with access to the computer can edit its SQLite file; hashing protects stored passwords, not the confidentiality of all trip data.

## Runtime flow

1. Capture Unity's StreamingAssets and persistent-data paths on the main thread.
2. Await the existing `TravelDatabase.InitializeAsync` seed-copy/validation operation.
3. Create `AccountDatabase` with that initialized database, then `AccountService`.
4. Enable the form. Each submit captures text on the main thread and awaits an account operation.
5. Hashing runs through Task.Run on a worker; parameterized SQL runs through the existing database worker gate. UI updates happen after returning to Unity's main thread.
6. After successful login, the service holds the user ID/email only in memory. Logout clears them and invalidates unfinished login operations. Restarting the app starts signed out.

The backend API is `RegisterAsync(email, password, confirmation, cancellationToken)`, `LoginAsync(email, password, cancellationToken)`, and `Logout()`. `SignedInUserId` is the identity later trip/watch features should use. Never use an email input field as proof that someone is authenticated. No credentials are passed in build command-line arguments or written to diagnostic logs.

## Files touched on the existing login page

Kevin's `Assets/Scenes/Login.unity` and required image/TMP resources are imported from branch commit `0ae085f9889591310dc46e6e14b7bd507c8c0ab3`, preserving GUIDs. Photo, colors, fonts, original form dimensions and placement are retained. Functional additions wire sign-in/create-account controls, add status feedback and matching registration/signed-in panels, and replace the scene's new Input System module with `StandaloneInputModule` for legacy input. Existing plaintext account scripts are replaced with the SQLite implementation.

## Common mistakes

| Symptom | What to check |
|---|---|
| Form never enables | Console/database startup error; packaged seed must exist |
| NullReferenceException | Required Inspector fields on LoginPage; use the scene preparation menu |
| Buttons ignore clicks | One EventSystem, StandaloneInputModule, Canvas GraphicRaycaster, Old input enabled |
| Invalid credentials after upgrade | Old LiteDB accounts do not migrate; register a fresh SQLite account |
| Registration rejected | Plain email, 10–128-character password, exact confirmation, no duplicate normalized email |
| Account disappears on restart | Do not change Company/Product name or database path; inspect persistentDataPath |
| UI freezes | Do not call .Wait() or .Result on account tasks; await them |
| Duplicate event firing | Do not wire Inspector listeners in addition to controller-owned listeners |
| Script fails compilation | Class/file name, assembly references and first red Console error |

## Team boundary

The auth teammate owns `Runtime/Accounts`, `UI/LoginPage` and login integration. Other teammates should build their screens as separate prefabs/controllers and receive the signed-in user ID through an explicit reference/event. Keep one integrator for the shared login scene; avoid simultaneous scene edits. Our changes remain on DuyEdit. Do not commit Library, Logs, Builds, persistent databases, or real credentials. The sample seed database is safe to commit because it has no users.


## Editor step 1 — Prepare Kevin's scene

1. Open the project in Unity **6000.3.24f1** and wait for compilation.
2. Choose **Travel Planning > Authentication > 1 - Prepare Login Scene**. This builds the packaged seed and opens/wires `Assets/Scenes/Login.unity`.
3. In Hierarchy, expand **Canvas > Right Panel**. You should see **LoginCard**, **RegistrationCard**, and **SignedInCard**. Only LoginCard starts active.
4. Select the separate root **AccountController**. Its **Login Page** component owns the form behavior.
5. Select **EventSystem**. Confirm **Standalone Input Module** is present. Under **Edit > Project Settings > Player > Other Settings > Configuration > Active Input Handling**, keep **Input Manager (Old)** or **Both**.
6. Do not add manual On Click events to the buttons; LoginPage registers listeners at startup.

The preparation command is idempotent: it preserves an already prepared scene. It does not regenerate or replace teammates' subsequent layout edits. If a reference is later removed, restore it with the table below rather than deleting the scene.

## Inspector reference map

All card paths below start at `Canvas/Right Panel`. Assign GameObjects for panels, TMP_InputField components for inputs, TMP_Text components for labels/feedback, and Button components for buttons.

| Login Page field | Object |
|---|---|
| Login Panel | LoginCard |
| Registration Panel | RegistrationCard |
| Signed In Panel | SignedInCard |
| Email Field | LoginCard/EmailInput |
| Password Field | LoginCard/PasswordInput |
| Registration Email | RegistrationCard/EmailInput |
| Registration Password | RegistrationCard/PasswordInput |
| Confirmation Field | RegistrationCard/ConfirmationInput |
| Feedback | LoginCard/AccountFeedback |
| Registration Feedback | RegistrationCard/AccountFeedback |
| Signed In Email | SignedInCard/AccountEmail |
| Submit Button | LoginCard/LoginButton |
| Switch Button | LoginCard/Create an account |
| Register Button | RegistrationCard/LoginButton |
| Back Button | RegistrationCard/Create an account |
| Logout Button | SignedInCard/LogoutButton |
| Forgot Button | LoginCard/Forgot? |

## Editor step 2 — Play-mode test

1. Click **Play** and wait for the startup message to clear.
2. Click **Create an account**. Enter a test email and a password of at least 10 characters, then the exact confirmation. Use a test credential rather than a password from another service.
3. Click **Create account**. Expect the original login form, normalized email filled in, empty password inputs, and the account-created message.
4. Try a wrong password: expect **Email or password is incorrect.**
5. Enter the correct password and click **Sign In**. Expect the signed-in panel and email.
6. Click **Log out**. Expect the login form and an empty password field.
7. Try the same email with different capitalization during registration: expect duplicate-account feedback and no replacement of the original password.
8. Stop and restart Play mode. Sign in again with the same account. It persists, but the session does not.
9. Exercise Tab, Shift+Tab and Enter, and resize the window. Check that all three forms remain readable; extreme narrow/short aspect ratios still need visual review of the original fixed-width design.

Clicking **Forgot?** explains that offline password recovery is not implemented. There is no email service or reset token in this milestone.

## Editor step 3 — Build and run Windows

1. Stop Play mode.
2. Choose **Travel Planning > Authentication > 2 - Build Windows x64**.
3. Wait for **AUTH_BUILD_PASS** in Console.
4. Open `Builds/Authentication/TravelPlannerAuthentication.exe` in Explorer.
5. Repeat register, wrong password, correct password, logout, close and reopen. The app uses `Application.persistentDataPath/travel.db`; changing Unity Company/Product settings changes that location.
6. To repeat automated checks from PowerShell in the project root:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Test-Authentication.ps1
```

The automated player checks use their own UUID-named development database and operate the actual form buttons/fields. They do not use your normal `travel.db`. Test fixtures are artificial accounts, not real credentials. Build and logs stay outside Assets and are ignored by Git.

## Files and responsibilities

| File | Job |
|---|---|
| Runtime/Accounts/AccountDatabase.cs | Parameterized SQLite account reads/writes |
| Runtime/Accounts/AccountService.cs | Form rules, asynchronous operations, in-memory session |
| Runtime/Accounts/PasswordHasher.cs | Random salts, PBKDF2, fixed-time verification |
| Runtime/Accounts/AccountResult.cs | Safe success/error data returned to UI |
| UI/LoginPage.cs | Form events, busy state, panel switching, cleared password fields |
| UI/Editor/AuthSetup.cs | Targeted scene wiring and standalone Windows build |
| UI/Editor/LoginPageSetup.cs | Compatibility redirect to the actual team scene |
| Tests/EditMode/AccountTests.cs | Isolated real SQLite/PBKDF2 behavior tests |
| Tests/EditMode/AuthenticationSceneTests.cs | Scene wiring, geometry, legacy input and Play-mode interactions |

Source paths in this table start at `Assets/TravelPlanning/`. Complete source is reproduced in `Milestone-2-Full-Scripts.md`; evidence and remaining limitations are in `Milestone-2-Validation.md`.

