using System;
using System.IO;
using System.Threading;
using TravelPlanning.Data;
using UnityEngine;

namespace TravelPlanning.UI
{
    /// <summary>Milestone 1B diagnostic screen; it does not replace the login screen.</summary>
    public sealed class TravelDatabasePage : MonoBehaviour
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
        private TravelDatabase database;
        private CancellationToken lifetime;
        private bool busy;
        private bool smoke;
        private int mainThreadId;
        // Scene initialization and event wiring.
        private void Start()
        {
            lifetime = destroyCancellationToken;
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            smoke = HasArgument("-travelSeedSmoke");
            if (statusText == null || detailsText == null || heartbeatText == null || runButton == null)
            {
                Debug.LogError("TravelDatabasePage: assign Status Text, Details Text, Heartbeat Text and Run Button.");
                if (smoke)
                {
                    Application.Quit(2);
                }

                return;
            }

            try
            {
                string fileName = ArgumentValue("-travelSeedFile") ?? "travel.db";
                if (Path.GetFileName(fileName) != fileName || !fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("Use a plain database filename ending in .db.");
                }

                database = new TravelDatabase(Path.Combine(Application.streamingAssetsPath, "Database", "travel_seed.db"),
                    Path.Combine(Application.persistentDataPath, fileName));
                runButton.onClick.AddListener(InitializeDatabase);
                InitializeDatabase();
            }
            catch (Exception error)
            {
                ReportFailure(error);
            }
        }

        private void Update()
        {
            if (heartbeatText != null)
            {
                heartbeatText.text = "UI alive: " + Time.realtimeSinceStartup.ToString("F1") + " s";
            }
        }

        private async void InitializeDatabase()
        {
            if (busy)
            {
                return;
            }

            busy = true;
            runButton.interactable = false;
            statusText.text = "Validating the travel database on a worker thread...";
            try
            {
                var result = await database.InitializeAsync(lifetime);
                bool copied = result.CopiedSeed;
                if (smoke && Debug.isDebugBuild && HasArgument("-travelSeedWriteMarker"))
                {
                    // Development smoke fixture only; not a real account or authentication flow.
                    await database.ExecuteAsync(connection =>
                    {
                        connection.RunInTransaction(() =>
                        {
                            connection.Execute("INSERT INTO users (id,email_normalized,password_hash,password_salt,password_iterations,created_utc) VALUES (?,?,?,?,?,?)",
                                "smoke-user", "smoke@example.invalid", Convert.ToBase64String(new byte[32]), Convert.ToBase64String(new byte[16]),
                                600000, "2027-01-01T00:00:00Z");
                            connection.Execute("INSERT INTO trips (id,user_id,name,start_date,end_date,created_utc,updated_utc) VALUES (?,?,?,?,?,?,?)",
                                "smoke-trip", "smoke-user", "Original trip", "2027-06-15", "2027-06-22", "2027-01-01T00:00:00Z", "2027-01-01T00:00:00Z");
                            connection.Execute("UPDATE trips SET name = ? WHERE id = ?", "Kept after restart", "smoke-trip");
                        });
                        return true;
                    }, lifetime);
                    result = await database.InitializeAsync(lifetime);
                }

                if (lifetime.IsCancellationRequested)
                {
                    return;
                }

                if (result.WorkerThreadId == mainThreadId || Thread.CurrentThread.ManagedThreadId != mainThreadId)
                {
                    throw new InvalidOperationException("The database/UI thread boundary failed.");
                }

                if (smoke)
                {
                    CheckExpected("-travelSeedExpectedCopied", copied ? 1 : 0);
                    CheckExpected("-travelSeedExpectedUsers", result.Users);
                    CheckExpected("-travelSeedExpectedTrips", result.Trips);
                    if (result.Users == 1 && result.Trips == 1)
                    {
                        string name = await database.ExecuteAsync(connection => connection.ExecuteScalar<string>("SELECT name FROM trips WHERE id = ?",
                            "smoke-trip"), lifetime);
                        if (name != "Kept after restart")
                        {
                            throw new InvalidDataException("The user's edited trip was not preserved.");
                        }
                    }
                }

                if (lifetime.IsCancellationRequested)
                {
                    return;
                }

                statusText.text = copied ? "PASS - seed copied and validated" : "PASS - existing database preserved";
                detailsText.text = "Schema: " + result.SchemaVersion + " | Catalog: " + result.CatalogVersion +
                    "\nDestinations: " + result.Destinations + " | Flights: " + result.Flights + "\nHotels: " + result.Hotels +
                    " | Restaurants: " + result.Restaurants + "\nExperiences: " + result.Experiences + " | Hotspots: " + result.Hotspots +
                    "\nDemo reviews: " + result.Reviews + " | Users: " + result.Users + " | Trips: " + result.Trips + "\nWorker: " +
                    result.WorkerThreadId + " | UI: " + mainThreadId + "\n\n" + result.DatabasePath + "\n\nSample prices/reviews. Demo travel dates: June 2027.";
                Debug.Log("TRAVEL_DATABASE_PASS copied=" + copied + " destinations=" + result.Destinations +
                    " users=" + result.Users + " trips=" + result.Trips + " worker=" + result.WorkerThreadId + " main=" + mainThreadId +
                    " catalog=" + result.CatalogVersion + " path=" + result.DatabasePath);
                if (smoke)
                {
                    Application.Quit(0);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                if (!lifetime.IsCancellationRequested)
                {
                    ReportFailure(error);
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

        private void ReportFailure(Exception error)
        {
            if (statusText != null)
            {
                statusText.text = "FAIL - travel database could not be initialized";
            }

            if (detailsText != null)
            {
                detailsText.text = error.GetBaseException().Message + "\n\nExisting data has not been replaced. See the Console or Player.log.";
            }

            Debug.LogError("TRAVEL_DATABASE_FAIL " + error);
            if (smoke)
            {
                Application.Quit(1);
            }
        }

        // Lifecycle cleanup.
        private void OnDestroy()
        {
            if (runButton != null)
            {
                runButton.onClick.RemoveListener(InitializeDatabase);
            }
        }

        private static void CheckExpected(string option, int actual)
        {
            string value = ArgumentValue(option);
            if (value != null && (!int.TryParse(value, out int expected) || expected != actual))
            {
                throw new InvalidDataException(option + " did not match. Actual: " + actual);
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
