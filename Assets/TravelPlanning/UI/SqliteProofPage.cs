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
        [Header("Screen references")]
        [SerializeField]
        private UnityEngine.UI.Text statusText;
        [SerializeField]
        private UnityEngine.UI.Text detailsText;
        [SerializeField]
        private UnityEngine.UI.Text heartbeatText;
        [Header("Buttons")]
        [SerializeField]
        private UnityEngine.UI.Button runButton;
        private readonly SqliteProofDatabase database = new SqliteProofDatabase();
        private CancellationToken lifetime;
        private bool busy;
        private int mainThreadId;
        private string databasePath;
        private bool smokeTest;
        // Scene initialization and event wiring.
        private void Start()
        {
            lifetime = destroyCancellationToken;
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            smokeTest = HasArgument("-sqliteProofSmoke");
            if (statusText == null || detailsText == null || heartbeatText == null || runButton == null)
            {
                Debug.LogError("SqliteProofPage: assign Status Text, Details Text, Heartbeat Text and Run Button in the Inspector.");
                if (smokeTest)
                {
                    Application.Quit(2);
                }

                return;
            }

            // Unity paths are captured here, never accessed from a worker thread.
            string fileName = ArgumentValue("-sqliteProofFile") ?? "sqlite-proof.db";
            if (Path.GetFileName(fileName) != fileName || !fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            {
                statusText.text = "Invalid proof filename. Use a plain filename ending in .db.";
                if (smokeTest)
                {
                    Application.Quit(2);
                }

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
            {
                heartbeatText.text = "UI alive: " + Time.realtimeSinceStartup.ToString("F1") + " s";
            }
        }

        private async void RunProof()
        {
            if (busy)
            {
                return;
            }

            busy = true;
            runButton.interactable = false;
            statusText.text = "Opening SQLite on a background thread...";
            detailsText.text = databasePath;
            try
            {
                SqliteProofResult result = await database.RunAsync(databasePath, lifetime);
                if (lifetime.IsCancellationRequested)
                {
                    return;
                }

                if (Thread.CurrentThread.ManagedThreadId != mainThreadId || result.WorkerThreadId == mainThreadId)
                {
                    throw new InvalidOperationException("The database/UI thread boundary failed.");
                }

                statusText.text = result.WasAlreadySaved ? "PASS - saved row loaded again" : "PASS - row created and read back";
                detailsText.text = "Visits: " + result.Visits + "\nCreated UTC: " + result.CreatedUtc + "\nMessage: " +
                    result.Message + "\nSQLite: " + result.SqliteVersion + "\nWorker thread: " + result.WorkerThreadId + " | UI thread: " +
                    mainThreadId + "\nDatabase work: " + result.ElapsedMilliseconds + " ms\n\n" + result.DatabasePath;
                Debug.Log("SQLITE_PROOF_PASS visits=" + result.Visits + " existing=" + result.WasAlreadySaved +
                    " created=" + result.CreatedUtc + " worker=" + result.WorkerThreadId + " main=" + mainThreadId + " sqlite=" +
                    result.SqliteVersion + " path=" + result.DatabasePath);
                if (smokeTest)
                {
                    string expected = ArgumentValue("-sqliteProofExpectedVisits");
                    if (expected != null && (!int.TryParse(expected, out int visits) || visits != result.Visits))
                    {
                        throw new InvalidOperationException("Unexpected persisted visit count: " + result.Visits);
                    }

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
            catch (OperationCanceledException)
            { /* Closing the scene cancels pending work. */
            }
            catch (Exception error)
            {
                if (lifetime.IsCancellationRequested)
                {
                    return;
                }

                statusText.text = "FAIL - SQLite could not complete the proof";
                detailsText.text = error.GetBaseException().Message + "\n\nDatabase: " + databasePath + "\n\nSee the Console or Player.log. Check the Windows x64 native plugin and folder permissions.";
                Debug.LogError("SQLITE_PROOF_FAIL " + error);
                if (smokeTest)
                {
                    Application.Quit(1);
                }
            }
            finally
            {
                busy = false;
                if (!lifetime.IsCancellationRequested && runButton != null)
                {
                    runButton.interactable = true;
                }
            }
        }

        // Lifecycle cleanup.
        private void OnDestroy()
        {
            if (runButton != null)
            {
                runButton.onClick.RemoveListener(RunProof);
            }
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
