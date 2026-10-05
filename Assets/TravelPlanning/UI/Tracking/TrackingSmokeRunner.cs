using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using TravelPlanning.Tracking;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Flights;
using TravelPlanning.UI.Trips;
using UnityEngine;

namespace TravelPlanning.UI.Tracking
{
    /// <summary>Exercises price tracking using two real UI accounts and an isolated seeded database.</summary>
    public sealed class TrackingSmokeRunner : MonoBehaviour
    {
        [Header("Screen references")]
        [SerializeField]
        private LoginPage account;
        [SerializeField]
        private FlightSearchPage flights;
        [SerializeField]
        private DestinationHubPage hub;
        [SerializeField]
        private PriceTrackingPage tracking;
        [SerializeField]
        private NotificationBadge badge;
        [SerializeField]
        private GameObject authCanvas, flightCanvas, hubCanvas, trackingCanvas;
        private int frames;
        private void Update() => frames++;
        public void Configure(Shared.TravelSceneBindings scene)
        {
            account = scene.Account;
            flights = scene.Flights;
            hub = scene.Hub;
            tracking = scene.Tracking;
            badge = scene.Badge;
            authCanvas = scene.LoginCanvas;
            flightCanvas = scene.FlightCanvas;
            hubCanvas = scene.HubCanvas;
            trackingCanvas = scene.TrackingCanvas;
        }

        private async void Start()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-trackingSmoke") < 0)
            {
                return;
            }

            try
            {
                int index = Array.IndexOf(args, "-authFile");
                string file = index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
                if (!Debug.isDebugBuild || !file.StartsWith("auth-smoke-", StringComparison.Ordinal) || !file.EndsWith(".db",
                    StringComparison.Ordinal) || System.IO.Path.GetFileName(file) != file || args.Any(x => x == "-authSmoke" ||
                    x == "-flightSmoke" || x == "-destinationSmoke" || x == "-reviewSmoke" || x == "-tripSmoke"))
                {
                    throw new InvalidOperationException("Tracking smoke checks require their isolated database and flag.");
                }

                await RunChecksAsync(Array.IndexOf(args, "-trackingReopen") < 0);
                Debug.Log("PRICE_TRACKING_UI_SMOKE_PASS");
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("PRICE_TRACKING_UI_SMOKE_FAIL " + exception.GetType().Name + ": " + exception.Message);
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
            var main = trackingCanvas.transform.Find("Main");
            const string first = "tracking-first@example.test", second = "tracking-second@example.test", password = "Travel tracking demo phrase 2027!";
            if (register)
            {
                await Register(login, registration, first, password);
            }

            await Login(login, first, password);
            string flightId = null;
            if (!register)
            {
                await WaitUntil(() => badge.UnreadCount == 1, "Persisted first-user unread badge");
                Click(home, "NotificationsButton");
                await WaitTracking();
                Require(tracking.LastSnapshot.UnreadCount == 1 && tracking.LastSnapshot.Watches.Count(watch => watch.IsActive) == 2,
                    "First user's watches or unread state did not persist.");
                flightId = tracking.LastSnapshot.Watches.First(watch => watch.Kind == PriceTargetKind.Flight).TargetId;
                await ReadAll(main);
                Click(main, "Header/CloseButton");
            }

            flightId = await OpenFlight(home, flightId);
            await EnsureTracking(main);
            Click(main, "Header/CloseButton");
            account.Logout();
            if (register)
            {
                await Register(login, registration, second, password);
            }

            await Login(login, second, password);
            await WaitUntil(() => badge.UnreadCount == 0, "Second user's read state");
            await OpenFlight(home, flightId);
            await EnsureTracking(main);
            Require(tracking.LastSnapshot.UnreadCount == 0, "Another account's unread state leaked.");
            var price = main.Find("DemoPanel/Controls/PriceInput").GetComponent<TMP_InputField>();
            price.text = "-1";
            Require(!main.Find("DemoPanel/Controls/ApplyButton").GetComponent<UnityEngine.UI.Button>().interactable, "Negative demo price remained enabled.");
            int original = tracking.CurrentTarget.CurrentPriceCents;
            await Apply(main, original + 123);
            Require(tracking.LastSnapshot.UnreadCount == 1, "Price change did not notify the active watcher.");
            await WaitUntil(() => badge.UnreadCount == 1, "Second user's unread badge");
            string sharedHistory = tracking.LastSnapshot.Notifications.First(notice => !notice.IsRead).HistoryId;
            var noticeRow = trackingCanvas.GetComponentsInChildren<PriceNoticeRow>().First();
            Click(noticeRow.transform, "Top/ReadButton");
            await WaitTracking();
            Require(tracking.LastSnapshot.UnreadCount == 0, "Mark read failed.");
            Click(main, "Header/CloseButton");
            account.Logout();
            await Login(login, first, password);
            await WaitUntil(() => badge.UnreadCount == 1, "First user's fan-out badge");
            Click(home, "NotificationsButton");
            await WaitTracking();
            Require(tracking.LastSnapshot.Notifications.Any(notice => notice.HistoryId == sharedHistory &&
                !notice.IsRead), "Two watchers did not reference the same price event.");
            await SelectFlight(main, flightId);
            Click(main, "TargetActions/TrackButton");
            await WaitTracking();
            Require(!tracking.CurrentTarget.IsActive, "Stop tracking failed.");
            int noticesBefore = tracking.LastSnapshot.Notifications.Count;
            await Apply(main, tracking.CurrentTarget.CurrentPriceCents + 200);
            Require(tracking.LastSnapshot.Notifications.Count == noticesBefore, "A stopped watch received a new notification.");
            await Apply(main, tracking.CurrentTarget.CurrentPriceCents);
            Require(tracking.Status.Contains("unchanged") && tracking.LastSnapshot.Notifications.Count == noticesBefore, "A no-op price change created a notice.");
            Click(main, "TargetActions/TrackButton");
            await WaitTracking();
            Require(tracking.CurrentTarget.IsActive && tracking.CurrentTarget.LastPriceCents == tracking.CurrentTarget.CurrentPriceCents,
                "Retracking did not establish the current baseline.");
            Click(main, "Header/CloseButton");
            // Open the hotel screen from home, then prove old Save-to-trip navigation still coexists with tracking.
            Click(home, "SearchFlightsButton");
            await WaitUntil(() => !flights.IsBusy && flights.LastResult != null, "Flight navigation");
            Click(flightCanvas.transform, "Main/Header/ExploreDestinationButton");
            await WaitUntil(() => !hub.IsBusy && hub.LastResult != null, "Hotel hub");
            foreach (string category in new[]
            {
                "Restaurants",
                "Experiences",
                "Hotspots"
            }

            )
            {
                Require(!FirstPlace(category).transform.Find("ReviewActions/TrackPriceButton").gameObject.activeSelf, "Tracking appeared on an unsupported category.");
            }

            Click(hubCanvas.transform, "Main/Header/SavedTripsButton");
            var trips = UnityEngine.Object.FindFirstObjectByType<SavedTripsPage>();
            await WaitUntil(() => !trips.IsBusy && trips.LastList != null, "Existing saved-trip navigation");
            var hotel = FirstPlace("Hotels");
            Click(hotel.transform, "ReviewActions/TrackPriceButton");
            await WaitTracking();
            Require(!trips.IsOpen && tracking.CurrentTarget.Kind == PriceTargetKind.Hotel, "Tracking did not close the saved-trip modal.");
            await EnsureTracking(main);
            string hotelId = tracking.CurrentTarget.TargetId;
            await Apply(main, tracking.CurrentTarget.CurrentPriceCents + 10);
            Require(tracking.LastSnapshot.UnreadCount == 2, "Hotel change did not create a notification.");
            await ReadAll(main);
            await WaitUntil(() => badge.UnreadCount == 0, "Mark-all-read badge");
            await WithBlockedDatabase(async () =>
            {
                Click(main, "TargetActions/TrackButton");
                int before = frames;
                await Task.Delay(60);
                Require(tracking.IsBusy && frames > before, "UI froze during queued watch mutation.");
                Click(hubCanvas.transform, "Main/Header/LogoutButton");
                Require(!tracking.IsOpen && tracking.LastSnapshot == null, "Logout left the tracking modal open.");
            });
            await Login(login, first, password);
            Click(home, "NotificationsButton");
            await WaitTracking();
            Require(tracking.LastSnapshot.Watches.Any(watch => watch.Kind == PriceTargetKind.Hotel && watch.TargetId == hotelId &&
                watch.IsActive), "Queued stop was not cancelled on logout.");
            Click(main, "Header/CloseButton");
            account.Logout();
            await Login(login, second, password);
            Click(home, "NotificationsButton");
            await WaitTracking();
            Require(tracking.LastSnapshot.UnreadCount == 1, "Other watcher missed the change while the first watcher was stopped.");
            await ReadAll(main);
            await SelectFlight(main, flightId);
            await Apply(main, tracking.CurrentTarget.CurrentPriceCents + 100);
            await ReadAll(main);
            Click(main, "Header/CloseButton");
            account.Logout();
            await Login(login, first, password);
            await WaitUntil(() => badge.UnreadCount == 1, "Reactivated watcher final unread badge");
            Click(home, "NotificationsButton");
            await WaitTracking();
            Require(tracking.LastSnapshot.UnreadCount == 1 && tracking.LastSnapshot.Watches.Count(watch => watch.IsActive) == 2, "Final persisted watch state is wrong.");
            Require(tracking.LastSnapshot.WorkerThreadId != Thread.CurrentThread.ManagedThreadId, "Tracking query ran on the UI thread.");
            Debug.Log("PRICE_TRACKING_LOAD_TIMING databaseMs=" + tracking.LastSnapshot.ElapsedMilliseconds +
                " worker=" + tracking.LastSnapshot.WorkerThreadId + " main=" + Thread.CurrentThread.ManagedThreadId);
            Click(main, "Header/CloseButton");
            account.Logout();
        }

        private async Task<string> OpenFlight(Transform home, string targetId)
        {
            Click(home, "SearchFlightsButton");
            await WaitUntil(() => !flights.IsBusy && flights.LastResult != null, "Flight search");
            targetId = targetId ?? flights.LastResult.Outbound[0].Id;
            int index = flights.LastResult.Outbound.ToList().FindIndex(flight => flight.Id == targetId);
            Require(index >= 0, "Stable watched flight was not found.");
            var rows = flightCanvas.transform.Find("Main/Results/Outbound/ScrollView/Viewport/Content").GetComponentsInChildren<FlightResultRow>();
            Click(rows[index].transform, "SaveActions/TrackPriceButton");
            await WaitTracking();
            Require(tracking.CurrentTarget.TargetId == targetId, "Wrong flight tracking target opened.");
            return targetId;
        }

        private async Task SelectFlight(Transform main, string targetId)
        {
            int index = tracking.LastSnapshot.Watches.ToList().FindIndex(watch => watch.Kind == PriceTargetKind.Flight && watch.TargetId == targetId);
            Require(index >= 0, "Flight watch was missing.");
            var dropdown = main.Find("WatchSelection/Dropdown").GetComponent<TMP_Dropdown>();
            dropdown.value = index + 1;
            await WaitTracking();
            Require(tracking.CurrentTarget.TargetId == targetId, "Wrong selected watch.");
        }

        private async Task EnsureTracking(Transform main)
        {
            if (!tracking.CurrentTarget.IsActive)
            {
                Click(main, "TargetActions/TrackButton");
                await WaitTracking();
            }

            Require(tracking.CurrentTarget.IsActive, "Tracking did not start.");
        }

        private async Task Apply(Transform main, int cents)
        {
            main.Find("DemoPanel/Controls/PriceInput").GetComponent<TMP_InputField>().text = (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
            Click(main, "DemoPanel/Controls/ApplyButton");
            await WaitTracking();
        }

        private async Task ReadAll(Transform main)
        {
            if (tracking.LastSnapshot.UnreadCount > 0)
            {
                Click(main, "TargetActions/ReadAllButton");
                await WaitTracking();
            }

            Require(tracking.LastSnapshot.UnreadCount == 0, "Mark all read failed.");
        }

        private PlaceCard FirstPlace(string category) => hubCanvas.transform.Find("Main/ScrollView/Viewport/Content/" +
            category + "/Items").GetComponentsInChildren<PlaceCard>().First();
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
                        throw new TimeoutException("Tracking smoke gate timeout.");
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

        private Task WaitTracking() => WaitUntil(() => !tracking.IsBusy && tracking.LastSnapshot != null, "Tracking panel");
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
