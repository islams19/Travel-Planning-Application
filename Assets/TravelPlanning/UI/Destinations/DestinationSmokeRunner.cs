using System;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using TravelPlanning.UI.Flights;
using UnityEngine;

namespace TravelPlanning.UI.Destinations
{
    /// <summary>Checks the actual buttons and city dropdown in an isolated development database.</summary>
    public sealed class DestinationSmokeRunner : MonoBehaviour
    {
        [Header("Screen references")]
        [SerializeField]
        private LoginPage account;
        [SerializeField]
        private FlightSearchPage flights;
        [SerializeField]
        private DestinationHubPage hub;
        [SerializeField]
        private GameObject authCanvas, flightCanvas, hubCanvas;
        private int frames;
        private void Update() => frames++;
        public void Configure(Shared.TravelSceneBindings scene)
        {
            account = scene.Account;
            flights = scene.Flights;
            hub = scene.Hub;
            authCanvas = scene.LoginCanvas;
            flightCanvas = scene.FlightCanvas;
            hubCanvas = scene.HubCanvas;
        }

        private async void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-destinationSmoke") < 0)
            {
                return;
            }

            try
            {
                int index = Array.IndexOf(args, "-authFile");
                string file = index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
                if (!Debug.isDebugBuild || !file.StartsWith("auth-smoke-", StringComparison.Ordinal) || !file.EndsWith(".db",
                    StringComparison.Ordinal) || System.IO.Path.GetFileName(file) != file || Array.IndexOf(args, "-authSmoke") >= 0 ||
                    Array.IndexOf(args, "-flightSmoke") >= 0)
                {
                    throw new InvalidOperationException("Destination smoke checks need their own isolated database and flag.");
                }

                await RunChecksAsync(Array.IndexOf(args, "-destinationReopen") < 0);
                Debug.Log("DESTINATION_UI_SMOKE_PASS");
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("DESTINATION_UI_SMOKE_FAIL " + exception.GetType().Name + ": " + exception.Message);
                Application.Quit(1);
            }
        }

        public async Task RunChecksAsync(bool register)
        {
            await WaitUntil(() => account.IsReady, "Account startup");
            var right = authCanvas.transform.Find("Right Panel");
            var login = right.Find("LoginCard");
            var registration = account.RegistrationPanel.transform;
            var signedIn = account.HomePanel.transform;
            const string email = "destination-smoke@example.test", password = "Travel destination demo phrase 2027!";
            if (register)
            {
                Click(login, "Create an account");
                Input(registration, "EmailInput").text = email;
                Input(registration, "PasswordInput").text = Input(registration, "ConfirmationInput").text = password;
                Click(registration, "LoginButton");
                await WaitUntil(() => !account.IsBusy, "Account registration");
                Require(login.gameObject.activeSelf, "Registration failed.");
            }

            await Login(login, email, password);
            Click(signedIn, "SearchFlightsButton");
            await WaitUntil(() => !flights.IsBusy && flights.LastResult != null, "Flight startup");
            var flightMain = flightCanvas.transform.Find("Main");
            var hubMain = hubCanvas.transform.Find("Main");
            var max = Input(flightMain, "FilterFields/MaximumPrice/Input");
            max.text = "550";
            var sort = flightMain.Find("FilterFields/Sort/Dropdown").GetComponent<TMP_Dropdown>();
            sort.value = 1;
            Click(flightMain, "Actions/SearchButton");
            await WaitUntil(() => !flights.IsBusy, "Filtered flights");
            var retainedResult = flights.LastResult;
            Click(flightMain, "Header/ExploreDestinationButton");
            await WaitForCity("London");
            var cities = hubMain.Find("DestinationSelection/Dropdown").GetComponent<TMP_Dropdown>();
            Require(cities.options.Count == 12, "Expected all 12 destinations.");
            Require(hub.CardCount == 12 && hub.LastResult.Hotels.Count == 3 && hub.LastResult.Restaurants.Count == 3 &&
                hub.LastResult.Experiences.Count == 3 && hub.LastResult.Hotspots.Count == 3, "Expected three cards in each category.");
            Require(hub.LastResult.WorkerThreadId != Thread.CurrentThread.ManagedThreadId, "Destination query used the UI thread.");
            Debug.Log("DESTINATION_LOAD_TIMING uiMs=" + hub.LastLoadMilliseconds + " databaseMs=" + hub.LastResult.ElapsedMilliseconds +
                " worker=" + hub.LastResult.WorkerThreadId + " main=" + Thread.CurrentThread.ManagedThreadId);
            Click(hubMain, "Header/BackButton");
            Require(flights.IsOpen && ReferenceEquals(retainedResult, flights.LastResult) && max.text == "550" &&
                sort.value == 1, "Back did not preserve the flight form and offers.");
            // A suspended search is retried when Back returns from the hub, even if its database work had not started.
            await WithBlockedDatabase(async () =>
            {
                Click(flightMain, "Actions/SearchButton");
                Require(flights.IsBusy, "Expected a pending flight search.");
                Click(flightMain, "Header/ExploreDestinationButton");
                Click(hubMain, "Header/BackButton");
                Require(flights.IsOpen && flights.IsBusy, "Interrupted flight search did not resume.");
                await Task.Delay(30);
            });
            await WaitUntil(() => !flights.IsBusy && flights.LastResult != null, "Resumed flight search");
            Require(max.text == "550" && sort.value == 1, "Resuming changed the filters.");
            Click(flightMain, "Header/ExploreDestinationButton");
            await WaitForCity("London");
            int paris = CityIndex(cities, "Paris"), tokyo = CityIndex(cities, "Tokyo");
            cities.value = paris;
            await WaitForCity("Paris");
            await WithBlockedDatabase(async () =>
            {
                cities.value = tokyo;
                cities.value = paris;
                cities.value = tokyo;
                int before = frames;
                await Task.Delay(60);
                Require(hub.IsBusy && frames > before, "UI stopped updating during a pending city change.");
            });
            await WaitForCity("Tokyo");
            Require(hub.CardCount == 12, "Rapid city changes duplicated or lost cards.");
            await WithBlockedDatabase(async () =>
            {
                cities.value = paris;
                int before = frames;
                await Task.Delay(60);
                Require(hub.IsBusy && frames > before, "Expected a responsive pending destination request.");
                Click(hubMain, "Header/LogoutButton");
                Require(!hub.IsOpen && !flights.IsOpen && account.SignedInUserId == null && hub.LastResult == null &&
                    hub.CardCount == 0, "Logout did not clear both screens.");
            });
            await Task.Delay(30);
            Require(!hub.IsOpen && hub.LastResult == null, "A cancelled city load reappeared after logout.");
            await Login(login, email, password);
            Click(signedIn, "SearchFlightsButton");
            await WaitUntil(() => !flights.IsBusy && flights.LastResult != null, "Flights after re-login");
            Click(flightMain, "Header/ExploreDestinationButton");
            await WaitForCity("London");
            Click(hubMain, "Header/LogoutButton");
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
                        throw new TimeoutException("Destination smoke gate timeout.");
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

        private async Task Login(Transform login, string email, string password)
        {
            Input(login, "EmailInput").text = email;
            Input(login, "PasswordInput").text = password;
            Click(login, "LoginButton");
            await WaitUntil(() => !account.IsBusy, "Account sign in");
            Require(account.SignedInEmail == email, "Sign in failed.");
        }

        private Task WaitForCity(string name) => WaitUntil(() => !hub.IsBusy && hub.LastResult != null && hub.LastResult.Destination.Name == name, "Destination " + name);
        private static int CityIndex(TMP_Dropdown dropdown, string name)
        {
            int index = dropdown.options.FindIndex(x => x.text.StartsWith(name + ",", StringComparison.Ordinal));
            Require(index >= 0, "Destination was not listed: " + name);
            return index;
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
