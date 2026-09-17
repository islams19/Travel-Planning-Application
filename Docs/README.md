# Travel Planning Application — Explanation Guide

A **class** is a named piece of code with a specific job. Keeping jobs separate lets your team change the appearance of the screen without changing how accounts are saved.

## Our database: LiteDB

We use **LiteDB 5.0.21**, a local document database. Think of it as a filing cabinet stored in one file called `accounts.db`. Each account is one record in its `accounts` collection. It needs no internet connection or database server. The library is bundled with this project.

The records contain an ID, email address, password hash, random salt, and password-hashing iteration count. A **hash** is a one-way fingerprint used to check a password. A **salt** is random data mixed into each password before hashing, so two people choosing the same password still get different stored fingerprints. The typed password and confirmation are never saved.

Unity puts the database inside `Application.persistentDataPath`, its folder for data that should survive closing the application. In a Windows player this is normally under `%USERPROFILE%/AppData/LocalLow/<CompanyName>/<ProductName>`. Keep company/product settings stable so the app continues finding the same file. The current database is local to that computer and OS user: registering on one computer does not register on another.

Email matching removes spaces at the beginning/end and ignores capitalization. `Duy@Example.com` and ` duy@example.com ` therefore become `duy@example.com`. A **unique index** is a database rule that prevents a second record with the same email. This rule still applies if two registrations happen together. Email spelling is checked, but email ownership is not verified because this application is offline. Provider-specific aliases such as `name+tag@example.com` remain distinct addresses.

The file is not encrypted. Password hashing protects stored passwords from being directly readable; a local login is not protection against someone who controls or edits files on the computer. Only one copy of the application should open this file at a time.

Sources: [LiteDB overview](https://www.litedb.org/), [indexes](https://www.litedb.org/docs/indexes/), [Unity persistentDataPath](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Application-persistentDataPath.html).

## What each class does

### AccountDatabase — the filing cabinet

File: `Assets/TravelPlanning/Runtime/Accounts/AccountDatabase.cs`.

It opens or creates the account file and sets the rule that emails cannot repeat. `Insert` saves a new record, and `Find` locates an account by email. `Dispose` closes the file when the application is finished using it. Storage errors are allowed to reach the screen so the application does not pretend an account was saved. It does not overwrite a damaged or newer-version database to make an error disappear.

### AccountService — the receptionist

File: `Assets/TravelPlanning/Runtime/Accounts/AccountService.cs`.

This class handles the actual account rules. `Register` checks the email, requires an 8-128-character password, checks that both password fields match, and asks the database to save the account. It returns a friendly message if the email already exists. Creating an account does not automatically log the user in.

`Login` looks up the email and checks the password fingerprint. On success, `SignedInEmail` remembers who is currently logged in. `Logout` clears that information. This session exists only in memory; restarting the app requires logging in again. A failed login also clears a previous session. Passwords are case-sensitive and are not trimmed.

### PasswordHasher — the fingerprint maker

File: `Assets/TravelPlanning/Runtime/Accounts/PasswordHasher.cs`.

It creates the random salt, calculates a password fingerprint using PBKDF2-SHA256 with 600,000 iterations, and compares fingerprints when someone logs in. The deliberate extra calculation makes password guessing more expensive. This class is internal: the frontend never needs to call it.

### AccountResult — the answer slip

File: `Assets/TravelPlanning/Runtime/Accounts/AccountResult.cs`.

After registration or login, this small object carries three answers: `Success` says whether it worked, `Message` contains text to show on screen, and `Email` contains the normalized email on success. It never carries a password or hash. Expected mistakes, such as a duplicate email, are returned as results rather than crashing the program.

### LoginPage — the bridge to Unity

File: `Assets/TravelPlanning/UI/LoginPage.cs`.

This is a Unity **MonoBehaviour**, which means it can be attached to an object in a scene. It reads the text fields, calls AccountService, and shows the result. It switches between login, registration, and the signed-in welcome state. Password fields are masked and cleared after submission. Buttons temporarily disable during submission to prevent repeated clicks.

Password checking runs in the background so Unity can keep drawing the screen. The code returns to Unity's main thread before changing UI elements. When the scene closes, the database is closed after any in-progress request finishes.

After a successful login it announces `LoggedIn`, an **event** other code can listen for. Your team's future dashboard can respond to that event. No dashboard or automatic scene change is included in this milestone.

### LoginPageSetup — the form assembler

File: `Assets/TravelPlanning/UI/Editor/LoginPageSetup.cs`.

This editor-only helper adds **Travel Planning > Create Login Scene** to Unity's menu. It creates a Canvas (the surface holding the UI), text labels, email/password fields, buttons, and an EventSystem (what lets clicks reach the controls). It connects these controls to LoginPage and saves a separate scene. It asks Unity to handle unsaved scene changes and chooses a new filename if a login scene already exists.

Your team can then change colors, positions, and labels in Unity's Inspector. The backend classes need no migration because they are already ordinary C# used by Unity. Only LoginPage and this helper know about Unity UI.

### AccountTests — the automatic checker

File: `Assets/TravelPlanning/Tests/EditMode/AccountTests.cs`.

These tests create temporary databases to check duplicate registration, persistence after reopening, incorrect passwords, email validation, logout, and password storage. Tests never use the real application's account file. The temporary databases are removed after each test.

## How the pieces work together

1. The user types an email and password into the Unity form.
2. LoginPage sends those values to AccountService.
3. AccountService checks the values and uses PasswordHasher.
4. AccountDatabase saves or finds the record in `accounts.db`.
5. AccountService returns an AccountResult.
6. LoginPage displays its message and, after successful login, shows the welcome state.

## Trying the screen

1. Open this repository with Unity **6000.3.24f1** and wait for package import/compilation.
2. Import **TMP Essential Resources** from Unity's TextMeshPro menu if the default font is missing. These are the fonts/materials used by the form.
3. Click **Travel Planning > Create Login Scene** and press Play.
4. Create `duy@example.com` using a test password entered twice.
5. Try creating `DUY@example.com` again. Registration should report that the account exists.
6. Log in with the original password, log out, and try an incorrect password.
7. Stop Play Mode and start again. The account should still work.

The generated scene is not added to build settings automatically. When the team prepares a player build, add that scene to the active Build Profile's scene list and make it the first scene if login should appear first.

## Testing status

Run **Window > General > Test Runner > EditMode** to test inside Unity. The Windows fallback is `powershell -ExecutionPolicy Bypass -File tools/Test-Core.ps1`. It compiles the actual backend/test files and invokes the synchronous NUnit test methods using Unity's bundled compiler and Mono, without opening the Editor. It is a small runner for these tests, not a replacement for all NUnit/Unity lifecycle features.

The fallback passed **21 cases: 10 trip cases and 11 account cases**. Unity import was attempted but the installed Editor is missing `Data/Resources/PackageManager/Server/UnityPackageManager.exe`. Consequently, the scene builder, visual layout, button interactions, and player builds have not been verified in Unity. Use a complete Unity installation to perform the screen checks above; the actual login scene has not yet been generated here.

## Earlier classes already in the project

- **Trip**: describes a trip, including destination and dates, and owns its list of activities.
- **ItineraryItem**: describes one activity, including its title, day, and location.
- **TripService**: keeps the current session's trips in memory. It does not yet save them or associate them with an account.
- **TripTests**: checks the trip/date rules automatically.
- **CoreTestRunner**: generated temporarily by `tools/Test-Core.ps1`; runs the test methods and counts passes/failures outside Unity. It is development tooling, not part of the application.

Preset flights and prices are the next data milestone. No external flight service or live pricing is needed.
