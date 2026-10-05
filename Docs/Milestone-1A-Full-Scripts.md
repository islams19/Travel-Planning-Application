# Milestone 1A - Complete C# scripts
These are the complete files installed in the project. See Milestone-1A-SQLite.md for Editor steps.

## Assets/TravelPlanning/Runtime/Data/SqliteProofResult.cs

```csharp
namespace TravelPlanning.Data
{
    /// <summary>Plain data returned by the worker. It contains no Unity objects.</summary>
    public sealed class SqliteProofResult
    {
        public string DatabasePath { get; set; }
        public string Message { get; set; }
        public string CreatedUtc { get; set; }
        public string SqliteVersion { get; set; }
        public int Visits { get; set; }
        public bool WasAlreadySaved { get; set; }
        public int WorkerThreadId { get; set; }
        public long ElapsedMilliseconds { get; set; }
    }
}
```

## Assets/TravelPlanning/Runtime/Data/SqliteProofDatabase.cs

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SQLite;

namespace TravelPlanning.Data
{
    /// <summary>Milestone 1A only: proves the native database can save and reload one row.</summary>
    public sealed class SqliteProofDatabase
    {
        public const string SampleMessage = "Hello, travel planner! Tokyo / O'Hare / 東京";

        // A single doorway prevents two proof runs from writing at the same time.
        private static readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private static bool providerReady;

        public async Task<SqliteProofResult> RunAsync(string path, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A database file path is required.", nameof(path));

            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Opening, SQL, file access and disposal all happen on this worker.
                return await Task.Run(() => Run(path, cancellationToken), cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }

        private static SqliteProofResult Run(string path, CancellationToken cancellationToken)
        {
            var timer = Stopwatch.StartNew();
            cancellationToken.ThrowIfCancellationRequested();
            if (!providerReady)
            {
                SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
                providerReady = true;
            }

            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            using (var connection = new SQLiteConnection(fullPath))
            {
                connection.BusyTimeout = TimeSpan.FromSeconds(3);
                connection.Execute("PRAGMA foreign_keys = ON");
                connection.Execute("CREATE TABLE IF NOT EXISTS proof_rows (" +
                    "id INTEGER PRIMARY KEY CHECK (id = 1), " +
                    "message TEXT NOT NULL, created_utc TEXT NOT NULL, " +
                    "visits INTEGER NOT NULL CHECK (visits >= 1))");

                bool existed = false;
                connection.RunInTransaction(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    existed = connection.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM proof_rows WHERE id = ?", 1) == 1;
                    if (existed)
                    {
                        if (connection.ExecuteScalar<string>("SELECT message FROM proof_rows WHERE id = ?", 1) != SampleMessage)
                            throw new InvalidDataException("The saved proof message is unexpected; the file was not reset.");
                        // Never overwrite the saved message or creation time on later runs.
                        connection.Execute("UPDATE proof_rows SET visits = visits + 1 WHERE id = ?", 1);
                    }
                    else
                    {
                        // Apostrophes and Unicode are data, never concatenated into SQL.
                        connection.Execute("INSERT INTO proof_rows (id, message, created_utc, visits) " +
                            "VALUES (?, ?, ?, ?)", 1, SampleMessage, DateTime.UtcNow.ToString("O"), 1);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                });

                string message = connection.ExecuteScalar<string>(
                    "SELECT message FROM proof_rows WHERE id = ?", 1);
                if (message != SampleMessage)
                    throw new InvalidDataException("The saved proof message is unexpected; the file was not reset.");

                return new SqliteProofResult
                {
                    DatabasePath = fullPath,
                    Message = message,
                    CreatedUtc = connection.ExecuteScalar<string>(
                        "SELECT created_utc FROM proof_rows WHERE id = ?", 1),
                    Visits = connection.ExecuteScalar<int>("SELECT visits FROM proof_rows WHERE id = ?", 1),
                    WasAlreadySaved = existed,
                    SqliteVersion = connection.ExecuteScalar<string>("SELECT sqlite_version()"),
                    WorkerThreadId = Thread.CurrentThread.ManagedThreadId,
                    ElapsedMilliseconds = timer.ElapsedMilliseconds
                };
            }
        }
    }
}
```

## Assets/TravelPlanning/UI/SqliteProofPage.cs

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TravelPlanning.Data;
using UnityEngine;

namespace TravelPlanning.UI
{
    /// <summary>Diagnostic uGUI page, separate from Kevin's login and the future travel database.</summary>
    public sealed class SqliteProofPage : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Text statusText;
        [SerializeField] private UnityEngine.UI.Text detailsText;
        [SerializeField] private UnityEngine.UI.Text heartbeatText;
        [SerializeField] private UnityEngine.UI.Button runButton;

        private readonly SqliteProofDatabase database = new SqliteProofDatabase();
        private CancellationToken lifetime;
        private bool busy;
        private int mainThreadId;
        private string databasePath;
        private bool smokeTest;

        private void Start()
        {
            lifetime = destroyCancellationToken;
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            smokeTest = HasArgument("-sqliteProofSmoke");
            if (statusText == null || detailsText == null || heartbeatText == null || runButton == null)
            {
                Debug.LogError("SqliteProofPage: assign Status Text, Details Text, Heartbeat Text and Run Button in the Inspector.");
                if (smokeTest) Application.Quit(2);
                return;
            }

            // Unity paths are captured here, never accessed from a worker thread.
            string fileName = ArgumentValue("-sqliteProofFile") ?? "sqlite-proof.db";
            if (Path.GetFileName(fileName) != fileName || !fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            {
                statusText.text = "Invalid proof filename. Use a plain filename ending in .db.";
                if (smokeTest) Application.Quit(2);
                return;
            }
            databasePath = Path.Combine(Application.persistentDataPath, "SQLiteProof", fileName);
            runButton.onClick.AddListener(RunProof);
            RunProof();
        }

        private void Update()
        {
            // This continues ticking while the database worker is running.
            if (heartbeatText != null)
                heartbeatText.text = "UI alive: " + Time.realtimeSinceStartup.ToString("F1") + " s";
        }

        private async void RunProof()
        {
            if (busy) return;
            busy = true;
            runButton.interactable = false;
            statusText.text = "Opening SQLite on a background thread...";
            detailsText.text = databasePath;
            try
            {
                SqliteProofResult result = await database.RunAsync(databasePath, lifetime);
                if (lifetime.IsCancellationRequested) return;
                if (Thread.CurrentThread.ManagedThreadId != mainThreadId || result.WorkerThreadId == mainThreadId)
                    throw new InvalidOperationException("The database/UI thread boundary failed.");

                statusText.text = result.WasAlreadySaved ? "PASS - saved row loaded again" : "PASS - row created and read back";
                detailsText.text = "Visits: " + result.Visits + "\nCreated UTC: " + result.CreatedUtc +
                    "\nMessage: " + result.Message + "\nSQLite: " + result.SqliteVersion +
                    "\nWorker thread: " + result.WorkerThreadId + " | UI thread: " + mainThreadId +
                    "\nDatabase work: " + result.ElapsedMilliseconds + " ms\n\n" + result.DatabasePath;
                Debug.Log("SQLITE_PROOF_PASS visits=" + result.Visits + " existing=" + result.WasAlreadySaved +
                    " created=" + result.CreatedUtc + " worker=" + result.WorkerThreadId +
                    " main=" + mainThreadId + " sqlite=" + result.SqliteVersion + " path=" + result.DatabasePath);

                if (smokeTest)
                {
                    string expected = ArgumentValue("-sqliteProofExpectedVisits");
                    if (expected != null && (!int.TryParse(expected, out int visits) || visits != result.Visits))
                        throw new InvalidOperationException("Unexpected persisted visit count: " + result.Visits);
                    string screenshot = ArgumentValue("-sqliteProofScreenshot");
                    if (screenshot != null)
                    {
                        // Allow a rendered frame before capturing the diagnostic screen.
                        await Task.Delay(500, lifetime);
                        ScreenCapture.CaptureScreenshot(screenshot);
                        await Task.Delay(1000, lifetime);
                    }
                    Application.Quit(0);
                }
            }
            catch (OperationCanceledException) { /* Closing the scene cancels pending work. */ }
            catch (Exception error)
            {
                if (lifetime.IsCancellationRequested) return;
                statusText.text = "FAIL - SQLite could not complete the proof";
                detailsText.text = error.GetBaseException().Message + "\n\nDatabase: " + databasePath +
                    "\n\nSee the Console or Player.log. Check the Windows x64 native plugin and folder permissions.";
                Debug.LogError("SQLITE_PROOF_FAIL " + error);
                if (smokeTest) Application.Quit(1);
            }
            finally
            {
                busy = false;
                if (!lifetime.IsCancellationRequested && runButton != null)
                    runButton.interactable = true;
            }
        }

        private void OnDestroy()
        {
            if (runButton != null) runButton.onClick.RemoveListener(RunProof);
        }

        private static bool HasArgument(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        private static string ArgumentValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
```

## Assets/TravelPlanning/UI/Editor/SqliteProofSetup.cs

```csharp
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Creates the isolated diagnostic scene and builds it without replacing the app scene list.</summary>
    public static class SqliteProofSetup
    {
        public const string ScenePath = "Assets/TravelPlanning/Scenes/SqliteProof.unity";
        public const string BuildPath = "Builds/SqliteProof/TravelPlannerSqliteProof.exe";

        [MenuItem("Travel Planning/SQLite Proof/1 - Create Scene")]
        public static void CreateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath))
            {
                var existing = EditorSceneManager.OpenScene(ScenePath);
                if (EnsureCamera()) EditorSceneManager.SaveScene(existing, ScenePath);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var canvasObject = new GameObject("SqliteProofCanvas", typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1000, 700);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = new GameObject("ProofPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.VerticalLayoutGroup), typeof(SqliteProofPage));
            panel.transform.SetParent(canvasObject.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(24, 24);
            rect.offsetMax = new Vector2(-24, -24);
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.96f, 0.97f, 1f);
            var layout = panel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 16;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Text(panel.transform, "Title", "SQLite proof - Milestone 1A", 28, 44);
            var status = Text(panel.transform, "Status", "Waiting to start...", 23, 50);
            var details = Text(panel.transform, "Details", "", 18, 320);
            var heartbeat = Text(panel.transform, "Heartbeat", "", 18, 32);
            var buttonObject = new GameObject("RunAgain", typeof(RectTransform), typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button), typeof(UnityEngine.UI.LayoutElement));
            buttonObject.transform.SetParent(panel.transform, false);
            buttonObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 48;
            buttonObject.GetComponent<UnityEngine.UI.Image>().color = new Color32(49, 87, 255, 255);
            var label = Text(buttonObject.transform, "Label", "Run again (keeps the same row)", 20, 48);
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

            var serialized = new SerializedObject(panel.GetComponent<SqliteProofPage>());
            serialized.FindProperty("statusText").objectReferenceValue = status;
            serialized.FindProperty("detailsText").objectReferenceValue = details;
            serialized.FindProperty("heartbeatText").objectReferenceValue = heartbeat;
            serialized.FindProperty("runButton").objectReferenceValue = buttonObject.GetComponent<UnityEngine.UI.Button>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EnsureCamera();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("SQLITE_PROOF_SCENE_READY " + ScenePath);
        }

        [MenuItem("Travel Planning/SQLite Proof/2 - Configure Windows")]
        public static void ConfigureWindows()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1000;
            PlayerSettings.defaultScreenHeight = 700;

            // This is the serialized setting behind Player > Active Input Handling.
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input = settings.FindProperty("activeInputHandler");
            if (input == null) throw new InvalidOperationException("Active Input Handling setting was not found.");
            input.intValue = 0; // Input Manager (Old). No new Input System required by this scene.
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("SQLITE_PROOF_WINDOWS_CONFIGURED - restart the Editor after changing input handling.");
        }

        [MenuItem("Travel Planning/SQLite Proof/3 - Build Windows x64")]
        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath)) throw new FileNotFoundException("Create the SQLite proof scene first.", ScenePath);
            CreateScene();
            // Keep the diagnostic responsive when its window loses focus, including smoke captures.
            PlayerSettings.runInBackground = true;
            Directory.CreateDirectory(Path.GetDirectoryName(BuildPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("SQLite proof build failed: " + report.summary.result);
            string licenses = Path.Combine(Path.GetDirectoryName(BuildPath), "ThirdPartyNotices");
            Directory.CreateDirectory(licenses);
            foreach (string source in Directory.GetFiles("Assets/Plugins/SQLite/Licenses"))
                if (!source.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    File.Copy(source, Path.Combine(licenses, Path.GetFileName(source)), true);
            File.Copy("Assets/Plugins/LiteDB/LICENSE.txt", Path.Combine(licenses, "LiteDB-LICENSE.txt"), true);
            Debug.Log("SQLITE_PROOF_BUILD_PASS " + Path.GetFullPath(BuildPath));
        }

        // Command-line entry point: configuration is separate so the next process uses legacy input.
        public static void Prepare()
        {
            ConfigureWindows();
            CreateScene();
        }

        private static bool EnsureCamera()
        {
            if (UnityEngine.Object.FindFirstObjectByType<Camera>() != null) return false;
            // URP needs a camera to produce a frame, even with Screen Space Overlay UI.
            var camera = new GameObject("ProofCamera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.10f, 0.16f);
            camera.cullingMask = 0;
            return true;
        }

        private static UnityEngine.UI.Text Text(Transform parent, string name, string value, int size, int height)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text), typeof(UnityEngine.UI.LayoutElement));
            item.transform.SetParent(parent, false);
            var text = item.GetComponent<UnityEngine.UI.Text>();
            // Diagnostic scene only: no TMP asset import or changes to Kevin's login font.
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.color = new Color(0.10f, 0.13f, 0.20f);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            item.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
            return text;
        }
    }
}
```

## Assets/TravelPlanning/Tests/EditMode/SqliteProofTests.cs

```csharp
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TravelPlanning.Data;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class SqliteProofTests
    {
        private string directory;
        private string path;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "TravelPlannerSqliteTests", Guid.NewGuid().ToString("N"));
            path = Path.Combine(directory, "proof.db");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [UnityTest]
        public IEnumerator SavedRowSurvivesNewServiceAndRunsOnWorker()
        {
            int callerThread = Thread.CurrentThread.ManagedThreadId;
            var firstTask = new SqliteProofDatabase().RunAsync(path, CancellationToken.None);
            yield return Complete(firstTask);
            var first = firstTask.GetAwaiter().GetResult();
            var secondTask = new SqliteProofDatabase().RunAsync(path, CancellationToken.None);
            yield return Complete(secondTask);
            var second = secondTask.GetAwaiter().GetResult();
            Assert.That(first.WasAlreadySaved, Is.False);
            Assert.That(second.WasAlreadySaved, Is.True);
            Assert.That(second.Visits, Is.EqualTo(2));
            Assert.That(second.CreatedUtc, Is.EqualTo(first.CreatedUtc));
            Assert.That(second.Message, Is.EqualTo(SqliteProofDatabase.SampleMessage));
            Assert.That(first.WorkerThreadId, Is.Not.EqualTo(callerThread));
        }

        [UnityTest]
        public IEnumerator ConcurrentRequestsKeepOneRowAndAllVisits()
        {
            var service = new SqliteProofDatabase();
            var pending = Task.WhenAll(Enumerable.Range(0, 4)
                .Select(_ => service.RunAsync(path, CancellationToken.None)));
            yield return Complete(pending);
            var results = pending.GetAwaiter().GetResult();
            Assert.That(results.Select(row => row.Visits).OrderBy(value => value), Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(results.Count(row => !row.WasAlreadySaved), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CorruptDatabaseIsReportedWithoutReplacingIt()
        {
            Directory.CreateDirectory(directory);
            byte[] original = Enumerable.Repeat((byte)0xAB, 1024).ToArray();
            File.WriteAllBytes(path, original);
            var pending = new SqliteProofDatabase().RunAsync(path, CancellationToken.None);
            yield return Complete(pending);
            Assert.That(pending.Exception?.GetBaseException(), Is.InstanceOf<SQLite.SQLiteException>());
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
        }

        [UnityTest]
        public IEnumerator FileWhereDirectoryShouldBeIsReported()
        {
            Directory.CreateDirectory(directory);
            string blocker = Path.Combine(directory, "not-a-directory");
            File.WriteAllText(blocker, "Keep this file.");
            var pending = new SqliteProofDatabase().RunAsync(Path.Combine(blocker, "proof.db"), CancellationToken.None);
            yield return Complete(pending);
            Assert.That(pending.Exception?.GetBaseException(), Is.InstanceOf<IOException>());
            Assert.That(File.ReadAllText(blocker), Is.EqualTo("Keep this file."));
        }

        [Test]
        public void CancellationDoesNotCreateDatabase()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var pending = new SqliteProofDatabase().RunAsync(path, cancellation.Token);
                Assert.That(pending.IsCanceled, Is.True);
                Assert.That(File.Exists(path), Is.False);
            }
        }

        private static IEnumerator Complete(Task pending)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!pending.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(pending.IsCompleted, Is.True, "Database worker did not finish within 15 seconds.");
        }
    }
}
```

## Assets/TravelPlanning/Tests/EditMode/SqliteProofPlayModeTests.cs

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using TravelPlanning.UI;

namespace TravelPlanning.Tests
{
    public sealed class SqliteProofPlayModeTests
    {
        [UnityTest]
        public IEnumerator MissingInspectorReferencesAreReportedClearly()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            LogAssert.Expect(LogType.Error,
                "SqliteProofPage: assign Status Text, Details Text, Heartbeat Text and Run Button in the Inspector.");
            var incomplete = new GameObject("IncompleteProofPage");
            incomplete.AddComponent<SqliteProofPage>();
            yield return null;
            Object.Destroy(incomplete);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DiagnosticSceneDisplaysSuccessfulResult()
        {
            EditorSceneManager.OpenScene("Assets/TravelPlanning/Scenes/SqliteProof.unity");
            yield return new EnterPlayMode();
            var status = GameObject.Find("Status").GetComponent<UnityEngine.UI.Text>();
            float deadline = Time.realtimeSinceStartup + 30f;
            while (!status.text.StartsWith("PASS") && !status.text.StartsWith("FAIL") && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(status.text, Does.StartWith("PASS"));
            var details = GameObject.Find("Details").GetComponent<UnityEngine.UI.Text>();
            Assert.That(details.text, Does.Contain("Worker thread:"));
            var button = GameObject.Find("RunAgain").GetComponent<UnityEngine.UI.Button>();
            Assert.That(button.interactable, Is.True);
            button.onClick.Invoke();
            while (!status.text.StartsWith("PASS") && !status.text.StartsWith("FAIL") && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(status.text, Does.Contain("saved row loaded again"));
            yield return new ExitPlayMode();
        }
    }
}
```
