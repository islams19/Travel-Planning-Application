# Milestone 1A validation - 2026-09-29

## Status: Ready with limitations

The required SQLite Editor/runtime and Windows x64 persistence checks passed.
Visual screenshot review and a separate Windows 10 machine were not validated.
No login assets were edited. No commit or push was performed.

## Environment

- Branch: DuyEdit, starting commit `12da1557ac9b73b82423e6e20227de84c572207d`.
- Unity: 6000.3.24f1 (4e7b9b5b6244).
- Host: Windows 11 Home, 10.0.26200, 64-bit.
- Player: Windows x64, Mono, .NET Standard 2.1, Development build.
- SQLite runtime reported by both Editor and player: 3.53.3.
- sqlite-net-pcl 1.11.285; SQLitePCLRaw core/provider 3.0.3;
  SourceGear.sqlite3 3.53.3. Framework assemblies supplied by Unity.

## Acceptance evidence

| Criterion | Result | Evidence |
|---|---|---|
| Unity compilation | Passed | `Logs/sqlite-build-render.log`, successful build and exit 0 |
| Editor/native provider loading | Passed | SQLite tests and Play-mode proof |
| Automated tests | Passed: 20/20 | `Logs/sqlite-tests-final.xml` |
| Existing account behavior | Passed: 13/13 | AccountTests; these still describe legacy LiteDB, not secure new auth |
| SQLite persistence/concurrency/errors/cancellation | Passed: 5/5 | SqliteProofTests |
| UI status/retry + missing Inspector references | Passed: 2/2 | SqliteProofPlayModeTests explicitly enter/exit Play mode |
| Windows x64 executable created | Passed | `Builds/SqliteProof/TravelPlannerSqliteProof.exe` |
| First player launch creates/inserts/reads | Passed | First-launch log below; visits=1, existing=False |
| Second process reads persisted row | Passed | Second-launch log; visits=2, existing=True, identical created timestamp |
| Corrupt database handled without overwrite | Passed | Error marker, exit 1, unchanged SHA-256 |
| Missing native DLL handled | Passed | Error marker, exit 1; build DLL restored afterward |
| Database operations off UI thread | Passed | Player worker=8 versus main=1; Editor test also asserts different threads |
| Native notices distributed | Passed | `Builds/SqliteProof/ThirdPartyNotices`, including retained LiteDB license |
| Visual rendering/layout screenshot | Not validated | Hidden-window ScreenCapture attempts failed; no screenshot claimed |
| Windows 10 / IL2CPP | Not run | Windows 11 Mono is the tested target |

Final player log suffix: `e51113c656214762a519408e86491303`.

- `Logs/SqliteProof/first-launch-e51113c656214762a519408e86491303.log`
- `Logs/SqliteProof/second-launch-e51113c656214762a519408e86491303.log`
- `Logs/SqliteProof/corrupt-database-e51113c656214762a519408e86491303.log`
- `Logs/SqliteProof/missing-native-e51113c656214762a519408e86491303.log`

Both successful launches returned creation time
`2026-09-29T06:14:23.3955494Z`. The row contains apostrophe and Unicode text.
The database resides in `Application.persistentDataPath/SQLiteProof`, not Assets
or the executable directory. UUID-named test files are retained for inspection.

## Reproduce the build and tests

Close Unity before running another Editor process against this project.
Use the Editor menu instructions in `Milestone-1A-SQLite.md`, or from PowerShell
in the project directory:

```powershell
$unityExe = 'C:\Program Files\Unity\Hub\Editor\6000.3.24f1\Editor\Unity.exe'
$projectRoot = (Get-Location).Path

# Prepare once; this process exits so the next launch sees legacy input enabled.
& $unityExe -batchmode -nographics -quit -projectPath $projectRoot -executeMethod TravelPlanning.UI.Editor.SqliteProofSetup.Prepare -logFile "$projectRoot\Logs\prepare.log"

# Do not supply -quit to the Unity test runner; it exits when tests finish.
& $unityExe -batchmode -nographics -projectPath $projectRoot -runTests -testPlatform EditMode -testResults "$projectRoot\Logs\tests.xml" -logFile "$projectRoot\Logs\tests.log"

& $unityExe -batchmode -nographics -quit -projectPath $projectRoot -executeMethod TravelPlanning.UI.Editor.SqliteProofSetup.BuildWindows -logFile "$projectRoot\Logs\build.log"

powershell -ExecutionPolicy Bypass -File .\tools\Test-SqliteProof.ps1
```

Run these commands sequentially and wait for each Unity process to finish before
starting another. Build and log folders are ignored by Git; source, dependency
DLLs, licenses and Unity metadata are intended to be shared.

## Changes and review notes

Intentional settings: Input Manager (Old), Windows Mono, .NET Standard 2.1,
windowed/resizable 1000x700, Run In Background. The proof builder supplies its
scene directly and does not replace the application's shared build-scene list.

Unity generated missing folder/asset metadata and default scene-template settings
during first import. Import/build also serialized URP volume/global runtime
settings, shader-prefilter fields, standalone batching/default platform icon
fields, and enabled UnityConnect in its settings file. Those Editor-generated
changes are retained and visible in the working diff; no manual URP redesign or
login changes were made. Some additional settings files have line-ending-only changes.

The initial compile exposed missing runtime/test assembly setup; the added Core
asmdef and explicit test DLL references resolve it. An initial test harness using
synchronous NUnit async assertions stalled under Unity's context; final tests
yield until worker tasks complete and all pass. SQLite runtime code did not need
blocking waits.

The diagnostic scene includes a camera, Canvas, wired controller and legacy
EventSystem. The Play-mode test exercises status and retry behavior, but is not
a claim of visual inspection. A normal manual launch is the remaining layout check.

## Next boundary

Stop here until the user says next. Milestone 1B adds the full schema, seed builder,
12-destination data, and first-launch copy from StreamingAssets. Secure login
integration follows in Milestone 2.
