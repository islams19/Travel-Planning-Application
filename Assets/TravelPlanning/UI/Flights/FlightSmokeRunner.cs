using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace TravelPlanning.UI.Flights
{
    /// <summary>Development-only checks drive the real controls using a separate disposable demo account.</summary>
    public sealed class FlightSmokeRunner : MonoBehaviour
    {
        [Header("Screen references")]
        [SerializeField]
        private LoginPage account;
        [SerializeField]
        private FlightSearchPage flights;
        [SerializeField]
        private GameObject authCanvas, flightCanvas;
        private int frames;
        private void Update() => frames++;
        public void Configure(Shared.TravelSceneBindings scene)
        {
            account = scene.Account;
            flights = scene.Flights;
            authCanvas = scene.LoginCanvas;
            flightCanvas = scene.FlightCanvas;
        }

        private async void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-flightSmoke") < 0)
            {
                return;
            }

            try
            {
                int fileIndex = Array.IndexOf(args, "-authFile");
                string file = fileIndex >= 0 && fileIndex + 1 < args.Length ? args[fileIndex + 1] : "";
                if (!Debug.isDebugBuild || !file.StartsWith("auth-smoke-", StringComparison.Ordinal) || !file.EndsWith(".db",
                    StringComparison.Ordinal) || System.IO.Path.GetFileName(file) != file)
                {
                    throw new InvalidOperationException("Flight smoke checks require an isolated test database.");
                }

                await RunChecksAsync(Array.IndexOf(args, "-flightReopen") < 0);
                Debug.Log("FLIGHT_UI_SMOKE_PASS");
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("FLIGHT_UI_SMOKE_FAIL " + exception.GetType().Name + ": " + exception.Message);
                Application.Quit(1);
            }
        }

        public async Task RunChecksAsync(bool register)
        {
            await WaitUntil(() => account.IsReady, "Account database startup");
            string email = "flight-smoke@example.test";
            string password = "Travel flight demo phrase 2027!";
            Transform right = authCanvas.transform.Find("Right Panel");
            var login = right.Find("LoginCard");
            var registration = account.RegistrationPanel.transform;
            var signedIn = account.HomePanel.transform;
            if (register)
            {
                Click(login, "Create an account");
                Input(registration, "EmailInput").text = email;
                Input(registration, "PasswordInput").text = Input(registration, "ConfirmationInput").text = password;
                Click(registration, "LoginButton");
                await WaitUntil(() => !account.IsBusy, "Account registration");
                Require(login.gameObject.activeSelf, "Registration did not return to sign in.");
            }

            await Login(login, email, password);
            Click(signedIn, "SearchFlightsButton");
            await WaitUntil(() => !flights.IsBusy && flights.LastResult != null, "Initial flight search");
            Require(flights.LastResult.Outbound.Count == 2 && flights.LastResult.Return.Count == 2, "Expected two seeded offers in each direction.");
            Require(flights.LastSearchMilliseconds <= 3000, "Flight search exceeded three seconds.");
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            Require(flights.LastResult.WorkerThreadId != mainThread, "Flight database work ran on the UI thread.");
            Debug.Log("FLIGHT_SEARCH_TIMING uiMs=" + flights.LastSearchMilliseconds + " databaseMs=" + flights.LastResult.ElapsedMilliseconds +
                " worker=" + flights.LastResult.WorkerThreadId + " main=" + mainThread);
            Transform main = flightCanvas.transform.Find("Main");
            var airline = Dropdown(main, "FilterFields/Airline/Dropdown");
            string chosenAirline = flights.LastResult.Outbound[0].AirlineName;
            airline.value = airline.options.FindIndex(option => option.text == chosenAirline);
            Require(airline.value > 0, "The matching airline was not listed.");
            await Search(main);
            Require(flights.LastResult.Outbound.Count > 0 && flights.LastResult.Outbound.All(x => x.AirlineName == chosenAirline), "Airline filter failed.");
            Click(main, "Actions/ClearFiltersButton");
            await WaitUntil(() => !flights.IsBusy, "Clear airline filter");
            Dropdown(main, "FilterFields/Sort/Dropdown").value = 1;
            await Search(main);
            Require(flights.LastResult.Outbound.First().PriceCents >= flights.LastResult.Outbound.Last().PriceCents, "Descending price sort failed.");
            Input(main, "FilterFields/MaximumPrice/Input").text = "0";
            await Search(main);
            Require(flights.LastResult.Outbound.Count == 0 && flights.LastResult.Return.Count == 0, "Zero-price filter should be empty.");
            Click(main, "Actions/ClearFiltersButton");
            await WaitUntil(() => !flights.IsBusy, "Clear filters");
            Require(flights.LastResult.Outbound.Count == 2 && flights.LastResult.Return.Count == 2, "Clear filters did not restore offers.");
            Dropdown(main, "FilterFields/TimeBand/Dropdown").value = 2;
            await Search(main);
            Require(flights.LastResult.Outbound.Count > 0 && flights.LastResult.Outbound.All(x => x.DepartureLocal.Hour >= 6 &&
                x.DepartureLocal.Hour < 12), "Morning filter failed.");
            Click(main, "Actions/ClearFiltersButton");
            await WaitUntil(() => !flights.IsBusy, "Clear time filter");
            var destination = Dropdown(main, "RouteFields/Destination/Dropdown");
            int destinationValue = destination.value;
            destination.value = Dropdown(main, "RouteFields/Origin/Dropdown").value;
            await Search(main);
            Require(flights.LastResult == null && !string.IsNullOrEmpty(flights.Status), "Same-airport validation failed.");
            destination.value = destinationValue;
            int unseededDestination = destination.options.FindIndex(option => option.text.Contains("(HND)"));
            Require(unseededDestination >= 0, "Tokyo airport was not listed.");
            destination.value = unseededDestination;
            await Search(main);
            Require(flights.LastResult != null && flights.LastResult.Outbound.Count == 0 && flights.LastResult.Return.Count == 0,
                "The unseeded direct route should show an empty result.");
            destination.value = destinationValue;
            // Hold the database gate so closing the screen has a deterministic in-flight request to cancel.
            using (var release = new ManualResetEventSlim(false))
            {
                var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var blocker = account.Database.ExecuteAsync(connection =>
                {
                    entered.SetResult(true);
                    if (!release.Wait(10000))
                    {
                        throw new TimeoutException("Smoke database gate was not released.");
                    }

                    return true;
                }, CancellationToken.None);
                try
                {
                    await entered.Task;
                    int before = frames;
                    Click(main, "Actions/SearchButton");
                    await Task.Delay(60);
                    Require(flights.IsBusy && frames > before, "UI did not keep updating while the database was occupied.");
                    Click(main, "Header/LogoutButton");
                    Require(!flights.IsOpen && account.SignedInUserId == null && flights.LastResult == null, "Logout did not close and clear flight search.");
                }
                finally
                {
                    release.Set();
                }

                await blocker;
                await Task.Delay(30);
                Require(!flights.IsOpen && flights.LastResult == null, "A cancelled request displayed stale results.");
            }

            await Login(login, email, password);
            Click(signedIn, "SearchFlightsButton");
            await WaitUntil(() => !flights.IsBusy && flights.LastResult != null, "Search after re-login");
            Require(flights.LastResult.Outbound.Count == 2, "Search failed after cancelling the previous session.");
            Click(main, "Header/BackButton");
            Require(!flights.IsOpen && signedIn.gameObject.activeInHierarchy, "Back did not return to the signed-in screen.");
            Click(signedIn, "LogoutButton");
        }

        private async Task Login(Transform login, string email, string password)
        {
            Input(login, "EmailInput").text = email;
            Input(login, "PasswordInput").text = password;
            Click(login, "LoginButton");
            await WaitUntil(() => !account.IsBusy, "Account sign in");
            Require(account.SignedInEmail == email, "Sign in failed.");
        }

        private async Task Search(Transform main)
        {
            Click(main, "Actions/SearchButton");
            await WaitUntil(() => !flights.IsBusy, "Flight search");
        }

        private static void Click(Transform root, string path) => root.Find(path).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        private static TMP_InputField Input(Transform root, string path) => root.Find(path).GetComponent<TMP_InputField>();
        private static TMP_Dropdown Dropdown(Transform root, string path) => root.Find(path).GetComponent<TMP_Dropdown>();
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
