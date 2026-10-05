# Milestone 1A: SQLite proof of concept

This diagnostic scene proves SQLite works in Unity 6000.3.24f1 and a Windows x64
Mono player. It is separate from Kevin's login. It does not implement accounts,
the travel schema, seeded destinations, or first-launch seed copying yet.

## What the proof does

1. Capture `Application.persistentDataPath` on Unity's main thread.
2. Open `SQLiteProof/sqlite-proof.db` on a worker thread.
3. Create one table and insert one known row using SQL parameters.
4. Read the row and display its message, creation time, and visit count.
5. On later runs, preserve that row and increase the visit count.

The database call uses `Task.Run`: Unity can keep drawing the screen while a
worker does file and SQL work. `await` resumes the screen controller on Unity's
main thread. A `SemaphoreSlim` allows one proof operation at a time, and `using`
closes the connection even when SQL fails. No UI objects are touched by workers.

## Files and responsibilities

| File | Job |
|---|---|
| `Runtime/Data/SqliteProofDatabase.cs` | Worker-thread SQL and one-row persistence |
| `Runtime/Data/SqliteProofResult.cs` | Plain returned data |
| `UI/SqliteProofPage.cs` | Display status and handle the Run again button |
| `UI/Editor/SqliteProofSetup.cs` | Create/wire the scene and build the diagnostic player |
| `Tests/EditMode/SqliteProofTests.cs` | Persistence, concurrency, cancellation and failure tests |
| `Tests/EditMode/SqliteProofPlayModeTests.cs` | Enter Play mode and verify the scene and retry button |
| `Runtime/TravelPlanning.Core.asmdef` | Repair the existing UI reference to the runtime assembly |
| `Tests/EditMode/TravelPlanning.Tests.asmdef` | Keep NUnit/account/proof tests in an Editor test assembly |
| `tools/Import-Sqlite.ps1` | Restore exact pinned DLLs, licenses and metadata |
| `tools/Test-SqliteProof.ps1` | Launch the actual Windows player and check persistence/errors |

Code paths in the table are below `Assets/TravelPlanning/`, except `tools/`.
The Editor assembly now also references `TravelPlanning.Core`.

## Dependency installation

The libraries are already included. Do not install a second SQLite package.
See `Assets/Plugins/SQLite/README.md` for the dependency inventory and licenses.

Pinned packages:

- sqlite-net-pcl **1.11.285** (`SQLite-net.dll`, netstandard2.0).
- SQLitePCLRaw.core **3.0.3** (netstandard2.0).
- SQLitePCLRaw.provider.e_sqlite3 **3.0.3** (netstandard2.0).
- SourceGear.sqlite3 **3.53.3** (`runtimes/win-x64/native/e_sqlite3.dll`).

To restore them, close Unity, open PowerShell in the project directory, and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Import-Sqlite.ps1
```

This downloads pinned packages, verifies SHA-256 hashes, and extracts only the
required files. It does not edit a generated Visual Studio project. No extra
System.Memory/System.Buffers DLLs are installed; Unity supplies the framework
assemblies. Do not mix this provider with older SQLitePCLRaw bundles.

## Editor step 1: Open the correct project

1. In Unity Hub, select **Add > Add project from disk**.
2. Select this repository's root (the folder containing Assets and ProjectSettings).
3. Open it with **6000.3.24f1** and wait for import/compilation.
4. Open **Window > General > Console**. Resolve any red compiler errors before continuing.

Do not run a command-line Editor against a project already open in Unity.

## Editor step 2: Verify the native plugin

1. In Project, open **Assets > Plugins > SQLite > x86_64**.
2. Select **e_sqlite3.dll**.
3. In Inspector, **Any Platform** must be off.
4. Enable **Editor**, OS **Windows**, CPU **x86_64**.
5. Enable **Standalone / Windows x64**, CPU **x86_64**.
6. Leave other platforms disabled and click **Apply** if you changed anything.
7. Restart Unity if you replaced a previously loaded native DLL.

The three managed DLLs in **Managed** target Windows Editor and Windows x64,
CPU **AnyCPU**, with reference validation enabled. Their metadata is included.

## Editor step 3: Configure Windows

1. Choose **Travel Planning > SQLite Proof > 2 - Configure Windows**.
2. Restart Unity after the input-handling change.
3. Verify **Edit > Project Settings > Player > Other Settings > Configuration**:
   **Scripting Backend = Mono**, **API Compatibility Level = .NET Standard 2.1**,
   **Active Input Handling = Input Manager (Old)**.
4. Under **Resolution and Presentation**, verify **Fullscreen Mode = Windowed**,
   **Resizable Window = enabled**, **Run In Background = enabled**, and default resolution **1000 x 700**.

These are deliberate ProjectSettings changes for this Windows diagnostic. The
new Input System package is still installed because existing login-generator
scripts reference it, but the proof uses only legacy input. No login scene is changed.

## Editor step 4: Open the proof scene

1. Choose **Travel Planning > SQLite Proof > 1 - Create Scene**.
2. This opens the existing proof scene, or creates it if absent:
   `Assets/TravelPlanning/Scenes/SqliteProof.unity`.
3. Select **SqliteProofCanvas > ProofPanel** in Hierarchy.
4. The **Sqlite Proof Page** component must have these references:

| Inspector field | Hierarchy object/component |
|---|---|
| Status Text | ProofPanel > Status, Text |
| Details Text | ProofPanel > Details, Text |
| Heartbeat Text | ProofPanel > Heartbeat, Text |
| Run Button | ProofPanel > RunAgain, Button |

The menu creates and assigns all four; no manual GameObject creation is needed.
If you accidentally clear one, drag the matching object from Hierarchy into its
field. The EventSystem must use **Standalone Input Module**. The diagnostic uses
uGUI Text with Unity's built-in font, not Kevin's TMP visual design.

## Editor step 5: Test Play mode

1. Press **Play**.
2. Expect **PASS - row created and read back** on a fresh proof database.
3. Note **Created UTC**, **Visits**, and the full database path.
4. Click **Run again (keeps the same row)**. Visits increases by one; Created UTC stays unchanged.
5. Stop Play mode, then press Play again. Expect **PASS - saved row loaded again**.
6. The UI clock keeps advancing. Worker and UI thread IDs must be different.

The row contains an apostrophe and Unicode to verify parameterized text round trips.
This is not a flight-search benchmark. The target search performance is tested in Milestone 3.

To run automated checks: **Window > General > Test Runner > EditMode > Run All**.
One test explicitly enters and exits Play mode. Existing account tests describe
the old LiteDB implementation, including its insecure plaintext behavior; passing
those tests does not mean authentication meets the future security requirements.

## Editor step 6: Build and run Windows x64

1. Stop Play mode.
2. Choose **Travel Planning > SQLite Proof > 3 - Build Windows x64**.
3. Wait for **SQLITE_PROOF_BUILD_PASS** in Console.
4. Open `Builds/SqliteProof/TravelPlannerSqliteProof.exe` in File Explorer.
5. Note the visit count, close the app, reopen it, and verify the count increased
   and the creation timestamp stayed the same.

The build menu explicitly includes only the proof scene without replacing the
application's shared scene list. It copies dependency notices into the build.
Keep the executable, `_Data` folder, `UnityPlayer.dll`, Mono runtime, and notices
together when sharing the build.

For a manual Build Profiles build: **File > Build Profiles > Add Build Profile >
Windows > Add Build Profile**, activate it, choose **Intel 64-bit**, use its
scene-list override, and include only **SqliteProof.unity**. Build to a separate
directory. The supplied menu is simpler and reproducible.

## Repeat the automated player checks

With no player running, use PowerShell in the project root:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Test-SqliteProof.ps1
```

This launches the built Windows player for first creation, reopening, corrupt
database handling, and missing-native-DLL handling. It uses UUID-named proof
databases in the persistent folder, preserving the default interactive file.
It temporarily moves only the build's native DLL and restores it in a finally
block. Do not launch another copy of that build during this test. Test files are
retained for inspection; logs are under `Logs/SqliteProof`.

## Beginner mistakes and recognizable symptoms

| Symptom | What to check |
|---|---|
| Menu absent / scripts cannot attach | Red Console compiler errors; file/class names must match |
| Message says assign Status/Details/Heartbeat/Run Button | Drag the listed objects into the four Inspector fields |
| `DllNotFoundException` | Native DLL is missing or disabled for Editor/Windows x64 |
| `BadImageFormatException` | Wrong CPU binary, such as x86 instead of x64 |
| Duplicate System.Memory/type errors | Remove accidentally added duplicate framework libraries; use the pinned importer |
| Click does nothing | One EventSystem, Standalone Input Module, legacy input enabled; restart after input change |
| `file is not a database` | The proof file is corrupt; it is intentionally not silently erased |
| Access denied / cannot create directory | Check the displayed path and Windows write permissions |
| Visits starts at 1 unexpectedly | Different filename, Windows account, company/product settings, or project path identity |
| SQL throws with an apostrophe | User text was concatenated into SQL; this proof uses parameters |

On Windows, persistent data normally resolves below
`%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProductName>\SQLiteProof`.
Use the exact path displayed by the app; changing Company/Product changes the location.

## Scope boundary

Kevin's login assets are not imported, redesigned, or edited in this milestone.
Old LiteDB accounts are not migrated or deleted. The seed database, schema,
first-launch seed copying, and secure authentication belong to later milestones.

Validation results are recorded separately in `Milestone-1A-Validation.md`.
Full C# source files follow in the companion `Milestone-1A-Full-Scripts.md`.
