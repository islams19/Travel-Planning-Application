using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using TravelPlanning.Trips;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Flights;
using UnityEngine;

namespace TravelPlanning.UI.Trips
{
    /// <summary>Drives the same trip controls as a person, using two isolated test accounts.</summary>
    public sealed class TripSmokeRunner : MonoBehaviour
    {
        [Header("Screen references")]
        [SerializeField]
        private LoginPage account;
        [SerializeField]
        private FlightSearchPage flights;
        [SerializeField]
        private DestinationHubPage hub;
        [SerializeField]
        private SavedTripsPage trips;
        [SerializeField]
        private GameObject authCanvas, flightCanvas, hubCanvas, tripsCanvas;
        private int frames;
        private void Update() => frames++;
        public void Configure(Shared.TravelSceneBindings scene)
        {
            account = scene.Account;
            flights = scene.Flights;
            hub = scene.Hub;
            trips = scene.Trips;
            authCanvas = scene.LoginCanvas;
            flightCanvas = scene.FlightCanvas;
            hubCanvas = scene.HubCanvas;
            tripsCanvas = scene.TripsCanvas;
        }

        private async void Start()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-tripSmoke") < 0)
            {
                return;
            }

            try
            {
                int index = Array.IndexOf(args, "-authFile");
                string file = index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
                if (!Debug.isDebugBuild || !file.StartsWith("auth-smoke-", StringComparison.Ordinal) || !file.EndsWith(".db",
                    StringComparison.Ordinal) || System.IO.Path.GetFileName(file) != file || args.Any(x => x == "-authSmoke" ||
                    x == "-flightSmoke" || x == "-destinationSmoke" || x == "-reviewSmoke"))
                {
                    throw new InvalidOperationException("Saved-trip smoke checks require their isolated database and flag.");
                }

                await RunChecksAsync(Array.IndexOf(args, "-tripReopen") < 0);
                Debug.Log("SAVED_TRIPS_UI_SMOKE_PASS");
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("SAVED_TRIPS_UI_SMOKE_FAIL " + exception.GetType().Name + ": " + exception.Message);
                Application.Quit(1);
            }
        }

        public async Task RunChecksAsync(bool register)
        {
            await WaitUntil(() => account.IsReady, "Account startup");
            var right = authCanvas.transform.Find("Right Panel");
            var login = right.Find("LoginCard");
            var registration = account.RegistrationPanel.transform;
            var home = account.HomePanel.transform;
            const string mainEmail = "trips-smoke@example.test", otherEmail = "trips-other@example.test", password = "Travel saved trips demo phrase 2027!";
            if (register)
            {
                await Register(login, registration, mainEmail, password);
            }

            await Login(login, mainEmail, password);
            var main = tripsCanvas.transform.Find("Main");
            Click(home, "SavedTripsButton");
            await WaitTrips();
            if (register)
            {
                Require(trips.LastList.Trips.Count == 0, "A new user saw existing trips.");
                Click(main, "CreateForm/CreateAction/CreateButton");
                await WaitTrips();
                Require(trips.LastList.Trips.Count == 0 && trips.Status.Length > 0, "Empty trip name was accepted.");
                Require(Selected(main, "CreateForm/StartDate/Dropdown") == "2027-06-15" && Selected(main,
                    "CreateForm/EndDate/Dropdown") == "2027-06-22", "Planning dates did not default to June 15-22.");
                Input(main, "CreateForm/TripName/Input").text = "Summer in London";
                Click(main, "CreateForm/CreateAction/CreateButton");
                await WaitTrips();
            }
            else
            {
                Require(trips.LastList.Trips.Count == 1 && trips.LastDetails.Items.Count == 5, "Saved trips did not persist across player runs.");
            }

            string ownedTripId = trips.LastDetails.Trip.Id;
            Click(main, "Header/CloseButton");
            Click(home, "SearchFlightsButton");
            await WaitUntil(() => !flights.IsBusy && flights.LastResult != null, "Flights");
            var flightResult = flights.LastResult;
            var flightRow = flightCanvas.transform.Find("Main/Results/Outbound/ScrollView/Viewport/Content").GetComponentsInChildren<FlightResultRow>().First();
            Click(flightRow.transform, "SaveActions/SaveToTripButton");
            await WaitTrips();
            Click(main, "SaveActions/SaveSelectedButton");
            await WaitTrips();
            Require(trips.LastDetails.Items.Any(item => item.Kind == SavedItemKind.Flight), "Flight was not saved.");
            Click(main, "SaveActions/SaveSelectedButton");
            await WaitTrips();
            Require(trips.Status.Contains("already"), "Duplicate save was not reported.");
            Click(main, "Header/CloseButton");
            Require(ReferenceEquals(flightResult, flights.LastResult), "Closing trips reset flight results.");
            Click(flightCanvas.transform, "Main/Header/ExploreDestinationButton");
            await WaitUntil(() => !hub.IsBusy && hub.LastResult != null, "Destination");
            foreach (string category in new[]
            {
                "Hotels",
                "Restaurants",
                "Experiences",
                "Hotspots"
            }

            )
            {
                ClickPlace(category);
                await WaitTrips();
                Click(main, "SaveActions/SaveSelectedButton");
                await WaitTrips();
                Click(main, "Header/CloseButton");
            }

            Click(hubCanvas.transform, "Main/Header/SavedTripsButton");
            await WaitTrips();
            Require(trips.LastDetails.Items.Count == 5 && trips.LastDetails.Items.Select(item => item.Kind).Distinct().Count() == 5,
                "Expected all five saved item kinds.");
            int hotspot = trips.LastDetails.Items.ToList().FindIndex(item => item.Kind == SavedItemKind.Hotspot);
            var savedRows = tripsCanvas.GetComponentsInChildren<SavedTripRow>();
            Click(savedRows[hotspot].transform, "Top/RemoveButton");
            await WaitTrips();
            Require(trips.LastDetails.Items.Count == 4, "Remove item failed.");
            Click(main, "Header/CloseButton");
            ClickPlace("Hotspots");
            await WaitTrips();
            Click(main, "SaveActions/SaveSelectedButton");
            await WaitTrips();
            Require(trips.LastDetails.Items.Count == 5, "Re-saving removed item failed.");
            await CheckFailedLoad(main, ownedTripId);
            await WithBlockedDatabase(async () =>
            {
                Input(main, "CreateForm/TripName/Input").text = "Queued must cancel";
                Click(main, "CreateForm/CreateAction/CreateButton");
                int before = frames;
                await Task.Delay(60);
                Require(trips.IsBusy && frames > before, "UI froze during a queued trip write.");
                Click(hubCanvas.transform, "Main/Header/LogoutButton");
                Require(!trips.IsOpen && trips.LastDetails == null && account.SignedInUserId == null, "Logout left saved-trip state open.");
            });
            await Task.Delay(30);
            Require(!trips.IsOpen && trips.LastList == null, "A cancelled write repopulated the old user's UI.");
            if (register)
            {
                await Register(login, registration, otherEmail, password);
            }

            await Login(login, otherEmail, password);
            Click(home, "SavedTripsButton");
            Require(main.Find("TripSelection/TripChoice").GetComponent<TMP_Dropdown>().options.Count == 0, "Old user trip names remained visible during loading.");
            await WaitTrips();
            Require(!trips.LastList.Trips.Any(trip => trip.Id == ownedTripId), "Another user saw the first user's trip.");
            if (register)
            {
                Require(trips.LastList.Trips.Count == 0, "The second user should begin with no trips.");
                Input(main, "CreateForm/TripName/Input").text = "Other user's private plan";
                Click(main, "CreateForm/CreateAction/CreateButton");
                await WaitTrips();
            }

            Require(trips.LastList.Trips.Count == 1 && trips.LastDetails.Items.Count == 0, "Second user's isolated plan did not persist.");
            Click(main, "Header/CloseButton");
            Click(home, "LogoutButton");
            await Login(login, mainEmail, password);
            Click(home, "SavedTripsButton");
            await WaitTrips();
            Require(trips.LastList.Trips.Count == 1 && trips.LastDetails.Trip.Id == ownedTripId && trips.LastDetails.Items.Count == 5,
                "Main user data or queued-write cancellation was incorrect.");
            Require(trips.LastDetails.WorkerThreadId != Thread.CurrentThread.ManagedThreadId, "Trip query ran on the UI thread.");
            Debug.Log("SAVED_TRIPS_LOAD_TIMING databaseMs=" + trips.LastDetails.ElapsedMilliseconds + " worker=" +
                trips.LastDetails.WorkerThreadId + " main=" + Thread.CurrentThread.ManagedThreadId);
            Click(main, "Header/CloseButton");
            Click(home, "LogoutButton");
        }

        private async Task CheckFailedLoad(Transform main, string ownedTripId)
        {
            Input(main, "CreateForm/TripName/Input").text = "Load failure probe";
            Click(main, "CreateForm/CreateAction/CreateButton");
            await WaitTrips();
            string probeId = trips.LastDetails.Trip.Id;
            var choices = main.Find("TripSelection/TripChoice").GetComponent<TMP_Dropdown>();
            int mainIndex = trips.LastList.Trips.ToList().FindIndex(trip => trip.Id == ownedTripId), probeIndex = trips.LastList.Trips.ToList().FindIndex(trip => trip.Id == probeId);
            choices.value = mainIndex;
            await WaitTrips();
            // Only this isolated empty fixture trip is removed to reproduce a stale selection.
            string userId = account.SignedInUserId;
            await account.Database.ExecuteAsync(connection => connection.Execute("DELETE FROM trips WHERE id = ? AND user_id = ?",
                probeId, userId), CancellationToken.None);
            choices.value = probeIndex;
            await WaitTrips();
            Require(trips.LastDetails == null && !main.Find("SaveActions/SaveSelectedButton").GetComponent<UnityEngine.UI.Button>().interactable &&
                tripsCanvas.GetComponentsInChildren<SavedTripRow>().Length == 0, "Failed load left actions attached to the previous trip.");
            Click(main, "TripSelection/RetryButton");
            await WaitTrips();
            Require(trips.LastDetails.Trip.Id == ownedTripId, "Retry did not recover the remaining trip.");
        }

        private void ClickPlace(string category)
        {
            var card = hubCanvas.transform.Find("Main/ScrollView/Viewport/Content/" + category + "/Items").GetComponentsInChildren<PlaceCard>().First();
            Click(card.transform, "ReviewActions/SaveToTripButton");
        }

        private async Task WithBlockedDatabase(Func<Task> action)
        {
            using (var release = new ManualResetEventSlim(false))
            {
                var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var blocker = account.Database.ExecuteAsync(connection =>
                {
                    entered.SetResult(true);
                    if (!release.Wait(10000))
                    {
                        throw new TimeoutException("Trip smoke gate timeout.");
                    }

                    return true;
                }, CancellationToken.None);
                try
                {
                    await entered.Task;
                    await action();
                }
                finally
                {
                    release.Set();
                }

                await blocker;
            }
        }

        private async Task Register(Transform login, Transform registration, string email, string password)
        {
            Click(login, "Create an account");
            Input(registration, "EmailInput").text = email;
            Input(registration, "PasswordInput").text = Input(registration, "ConfirmationInput").text = password;
            Click(registration, "LoginButton");
            await WaitUntil(() => !account.IsBusy, "Registration");
            Require(login.gameObject.activeSelf, "Registration failed.");
        }

        private async Task Login(Transform login, string email, string password)
        {
            Input(login, "EmailInput").text = email;
            Input(login, "PasswordInput").text = password;
            Click(login, "LoginButton");
            await WaitUntil(() => !account.IsBusy, "Sign in");
            Require(account.SignedInEmail == email, "Sign in failed.");
        }

        private Task WaitTrips() => WaitUntil(() => !trips.IsBusy && (trips.LastList != null || !trips.IsOpen), "Saved trips");
        private static string Selected(Transform root, string path)
        {
            var dropdown = root.Find(path).GetComponent<TMP_Dropdown>();
            return dropdown.options[dropdown.value].text;
        }

        private static void Click(Transform root, string path) => root.Find(path).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        private static TMP_InputField Input(Transform root, string path) => root.Find(path).GetComponent<TMP_InputField>();
        private static void Require(bool value, string message)
        {
            if (!value)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static async Task WaitUntil(Func<bool> predicate, string operation)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!predicate())
            {
                if (watch.ElapsedMilliseconds > 45000)
                {
                    throw new TimeoutException(operation);
                }

                await Task.Delay(10);
            }
        }
    }
}
