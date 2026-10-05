using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using TMPro;
using TravelPlanning.Tracking;
using TravelPlanning.Trips;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Flights;
using TravelPlanning.UI.Reviews;
using TravelPlanning.UI.Tracking;
using TravelPlanning.UI.Trips;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

namespace TravelPlanning.UI.Polish
{
    /// <summary>Opt-in QA for a real release player, restricted to a GUID-named disposable database.</summary>
    public sealed class ReleaseSmokeRunner : MonoBehaviour
    {
        [Header("Screen references")]
        [SerializeField]
        private LoginPage account;
        [SerializeField]
        private FlightSearchPage flights;
        [SerializeField]
        private DestinationHubPage hub;
        [SerializeField]
        private ReviewsPage reviews;
        [SerializeField]
        private SavedTripsPage trips;
        [SerializeField]
        private PriceTrackingPage tracking;
        [SerializeField]
        private GameObject authCanvas, flightCanvas, hubCanvas, trackingCanvas;
        private ReleaseScreenshots captures;
        private const string Email = "release-qa@example.test";
        private const string Password = "Release QA isolated passphrase 2027!";
        public void Configure(Shared.TravelSceneBindings scene)
        {
            account = scene.Account;
            flights = scene.Flights;
            hub = scene.Hub;
            reviews = scene.Reviews;
            trips = scene.Trips;
            tracking = scene.Tracking;
            authCanvas = scene.LoginCanvas;
            flightCanvas = scene.FlightCanvas;
            hubCanvas = scene.HubCanvas;
            trackingCanvas = scene.TrackingCanvas;
        }

        private async void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!args.Contains("-releaseSmoke"))
            {
                return;
            }

            try
            {
                string file = ReleaseSmokeArguments.ResolveDatabaseFile(args, Debug.isDebugBuild);
                if (args.Contains("-releaseScreenshots"))
                {
                    string directory = ReleaseSmokeArguments.Value(args, "-releaseCaptureDir");
                    if (string.IsNullOrEmpty(directory) || !Path.IsPathRooted(directory))
                    {
                        throw new ArgumentException("Release screenshot output must be an absolute directory.");
                    }

                    captures = gameObject.AddComponent<ReleaseScreenshots>();
                    captures.OutputDirectory = Path.GetFullPath(directory);
                }

                await RunAsync(!args.Contains("-releaseReopen"), file);
                Debug.Log("RELEASE_UI_SMOKE_PASS development=" + Debug.isDebugBuild + " reopen=" + args.Contains("-releaseReopen"));
                Application.Quit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("RELEASE_UI_SMOKE_FAIL " + error.GetType().Name + ": " + error.Message);
                Application.Quit(1);
            }
        }

        private async Task RunAsync(bool firstLaunch, string file)
        {
            Require(!Debug.isDebugBuild, "QA must execute the actual nondevelopment release.");
            await Until(() => account.IsReady, "Account startup");
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<Shared.TravelSceneBootstrap>();
            await Until(() => bootstrap && bootstrap.IsReady, "Scene bootstrap");
            string[] expectedScenes = { "Login", "Registration", "Home", "Flights", "Destinations", "Reviews", "SavedTrips", "Notifications" };
            Require(SceneManager.sceneCount == expectedScenes.Length && expectedScenes.All(name => SceneManager.GetSceneByName(name).isLoaded),
                "The eight application scenes were not all loaded.");
            Require(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
                "The application must have exactly one EventSystem.");
            var right = authCanvas.transform.Find("Right Panel");
            var login = right.Find("LoginCard");
            var registration = account.RegistrationPanel.transform;
            var home = account.HomePanel.transform;
            await Capture("01-login");
            Click(login, "Create an account");
            await Capture("02-registration");
            if (firstLaunch)
            {
                Field(registration, "EmailInput").text = Email;
                Field(registration, "PasswordInput").text = Field(registration, "ConfirmationInput").text = Password;
                Click(registration, "LoginButton");
                await Until(() => !account.IsBusy, "Registration");
                Require(login.gameObject.activeSelf, "Registration failed.");
            }
            else
            {
                Click(registration, "Create an account");
            }

            Field(login, "EmailInput").text = "not-an-email";
            Field(login, "PasswordInput").text = Password;
            Click(login, "LoginButton");
            await Until(() => !account.IsBusy, "Invalid login");
            Require(account.SignedInUserId == null && login.gameObject.activeSelf, "Invalid login was accepted.");
            await Capture("08-login-error");
            Field(login, "EmailInput").text = Email;
            Field(login, "PasswordInput").text = Password;
            Click(login, "LoginButton");
            await Until(() => !account.IsBusy, "Login");
            Require(account.SignedInEmail == Email, "Login failed.");
            await Capture("09-signed-in-home");
            Click(home, "SearchFlightsButton");
            await Until(() => !flights.IsBusy && flights.LastResult != null, "Flight search");
            Require(flights.LastResult.Outbound.Count == 2 && flights.LastResult.Return.Count == 2, "Seeded round trip was not returned.");
            Debug.Log("RELEASE_FLIGHT_TIMING queryMs=" + flights.LastResult.ElapsedMilliseconds + " uiMs=" +
                flights.LastSearchMilliseconds + " worker=" + flights.LastResult.WorkerThreadId + " main=" + System.Threading.Thread.CurrentThread.ManagedThreadId);
            Require(flights.LastSearchMilliseconds < 3000 && flights.LastResult.WorkerThreadId != System.Threading.Thread.CurrentThread.ManagedThreadId,
                "Release flight search exceeded its responsiveness target.");
            await Capture("03-flights");
            var selectedFlight = flights.LastResult.Outbound.First(flight => flight.Id == "JFK-LHR-20270615-BA");
            Click(flightCanvas.transform, "Main/Header/ExploreDestinationButton");
            await Until(() => !hub.IsBusy && hub.LastResult != null, "Destination hub");
            Require(hub.CardCount == 12, "Destination categories did not render.");
            await Capture("04-destination");
            var hotelCard = hubCanvas.transform.Find("Main/ScrollView/Viewport/Content/Hotels/Items").GetComponentsInChildren<PlaceCard>().First();
            Click(hotelCard.transform, "ReviewActions/ViewReviewsButton");
            await Until(() => !reviews.IsBusy && reviews.LastResult != null, "Reviews");
            Require(reviews.LastResult.Reviews.Count == 2 && reviews.LastResult.Reviews.All(review => review.IsDemo), "Sample review details are missing.");
            string hotelId = reviews.LastResult.Place.Id;
            await Capture("05-reviews");
            reviews.Close();
            var plans = new TripService(account.Database, account.SignedInUserId);
            var watches = new PriceTrackingService(account.Database, account.SignedInUserId);
            if (firstLaunch)
            {
                var trip = await plans.CreateAsync("Release QA London plan", "2027-06-15", "2027-06-22", destroyCancellationToken);
                await plans.SaveAsync(trip.Id, SavedItemKind.Flight, selectedFlight.Id, destroyCancellationToken);
                await plans.SaveAsync(trip.Id, SavedItemKind.Hotel, hotelId, destroyCancellationToken);
                await watches.TrackAsync(PriceTargetKind.Flight, selectedFlight.Id, destroyCancellationToken);
                // Explicit isolated QA fixture provisioning; the release UI still cannot invoke demo changes.
                await new DemoPriceService(account.Database).ChangePriceAsync(PriceTargetKind.Flight, selectedFlight.Id,
                    selectedFlight.PriceCents + 1, destroyCancellationToken);
            }

            var list = await plans.ListAsync(destroyCancellationToken);
            Require(list.Trips.Count == 1 && list.Trips[0].ItemCount == 2, "Saved fixture did not persist correctly.");
            trips.Browse();
            await Until(() => !trips.IsBusy && trips.LastDetails != null, "Saved trip UI");
            Require(trips.LastDetails.Items.Count == 2, "Saved-trip rows did not load.");
            await Capture("06-saved-trips");
            trips.Close();
            Click(hubCanvas.transform, "Main/Header/NotificationsButton");
            await Until(() => !tracking.IsBusy && tracking.LastSnapshot != null && tracking.CurrentTarget != null, "Tracking UI");
            Require(tracking.LastSnapshot.Watches.Count == 1 && tracking.LastSnapshot.UnreadCount == 1, "Watch or unread notification did not persist.");
            var main = trackingCanvas.transform.Find("Main");
            var demo = main.Find("DemoPanel");
            Require(!demo.gameObject.activeSelf, "Release demo panel is visible.");
            string databasePath = Path.Combine(Application.persistentDataPath, file);
            byte[] before = Hash(databasePath);
            Field(demo, "Controls/PriceInput").text = "999.99";
            var apply = demo.Find("Controls/ApplyButton").GetComponent<UnityEngine.UI.Button>();
            Require(!apply.interactable, "Release demo button is enabled.");
            apply.onClick.Invoke(); // Bypass the disabled button, proving its callback also checks release mode.
            await Task.Delay(150, destroyCancellationToken);
            Require(Hash(databasePath).SequenceEqual(before), "A release demo callback changed the database.");
            await Capture("07-notifications");
            tracking.Close();
            account.Logout();
            Require(account.SignedInUserId == null, "Logout retained a session.");
            Debug.Log("RELEASE_QA_DATABASE " + databasePath);
        }

        private async Task Capture(string name)
        {
            string expectedScene;
            switch (name)
            {
                case "01-login":
                case "08-login-error": expectedScene = "Login"; break;
                case "02-registration": expectedScene = "Registration"; break;
                case "03-flights": expectedScene = "Flights"; break;
                case "04-destination": expectedScene = "Destinations"; break;
                case "05-reviews": expectedScene = "Reviews"; break;
                case "06-saved-trips": expectedScene = "SavedTrips"; break;
                case "07-notifications": expectedScene = "Notifications"; break;
                case "09-signed-in-home": expectedScene = "Home"; break;
                default: throw new ArgumentException("Unknown release QA screen: " + name, nameof(name));
            }
            await Until(() => SceneManager.GetActiveScene().name == expectedScene, "Scene navigation to " + expectedScene);
            Debug.Log("RELEASE_SCENE_NAVIGATION_PASS screen=" + name + " scene=" + expectedScene);
            if (captures) await captures.CaptureMatrixAsync(name, destroyCancellationToken);
        }
        private async Task Until(Func<bool> condition, string step)
        {
            for (int attempt = 0; !condition() && attempt < 3000; attempt++)
            {
                await Task.Delay(10, destroyCancellationToken);
            }

            Require(condition(), step + " timed out.");
        }

        private static byte[] Hash(string path)
        {
            using (var hash = SHA256.Create())
                return hash.ComputeHash(File.ReadAllBytes(path));
        }

        private static TMP_InputField Field(Transform root, string path) => root.Find(path).GetComponent<TMP_InputField>();
        private static void Click(Transform root, string path) => root.Find(path).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        private static void Require(bool value, string message)
        {
            if (!value)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
