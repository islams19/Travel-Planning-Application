using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using TravelPlanning.Reviews;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Flights;
using UnityEngine;

namespace TravelPlanning.UI.Reviews
{
    /// <summary>Checks actual review controls and a fake browser opener in an isolated development database.</summary>
    public sealed class ReviewSmokeRunner : MonoBehaviour
    {
        [Header("Screen references")]
        [SerializeField]
        private LoginPage account;
        [SerializeField]
        private DestinationHubPage hub;
        [SerializeField]
        private ReviewsPage reviews;
        [SerializeField]
        private GameObject authCanvas, flightCanvas, hubCanvas, reviewsCanvas;
        private int frames;
        private void Update() => frames++;
        public void Configure(Shared.TravelSceneBindings scene)
        {
            account = scene.Account;
            hub = scene.Hub;
            reviews = scene.Reviews;
            authCanvas = scene.LoginCanvas;
            flightCanvas = scene.FlightCanvas;
            hubCanvas = scene.HubCanvas;
            reviewsCanvas = scene.ReviewsCanvas;
        }

        private async void Start()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-reviewSmoke") < 0)
            {
                return;
            }

            try
            {
                int index = Array.IndexOf(args, "-authFile");
                string file = index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
                if (!Debug.isDebugBuild || !file.StartsWith("auth-smoke-", StringComparison.Ordinal) || !file.EndsWith(".db",
                    StringComparison.Ordinal) || System.IO.Path.GetFileName(file) != file || args.Any(x => x == "-authSmoke" ||
                    x == "-flightSmoke" || x == "-destinationSmoke"))
                {
                    throw new InvalidOperationException("Review smoke checks require their isolated database and flag.");
                }

                await RunChecksAsync(Array.IndexOf(args, "-reviewReopen") < 0);
                Debug.Log("REVIEWS_UI_SMOKE_PASS");
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("REVIEWS_UI_SMOKE_FAIL " + exception.GetType().Name + ": " + exception.Message);
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
            const string email = "reviews-smoke@example.test", password = "Travel reviews demo phrase 2027!";
            if (register)
            {
                Click(login, "Create an account");
                Input(registration, "EmailInput").text = email;
                Input(registration, "PasswordInput").text = Input(registration, "ConfirmationInput").text = password;
                Click(registration, "LoginButton");
                await WaitUntil(() => !account.IsBusy, "Registration");
                Require(login.gameObject.activeSelf, "Registration failed.");
            }

            await Login(login, email, password);
            await EnterHub(signedIn);
            var hubMain = hubCanvas.transform.Find("Main");
            var main = reviewsCanvas.transform.Find("Main");
            var city = hubMain.Find("DestinationSelection/Dropdown").GetComponent<TMP_Dropdown>();
            var hubScroll = hubMain.Find("ScrollView").GetComponent<UnityEngine.UI.ScrollRect>();
            string openedUrl = null;
            int openedCount = 0;
            var originalOpener = reviews.UrlOpener;
            reviews.UrlOpener = url =>
            {
                openedUrl = url;
                openedCount++;
            };
            try
            {
                foreach (string category in new[]
                {
                    "Hotels",
                    "Restaurants",
                    "Experiences",
                    "Hotspots"
                }

                )
                {
                    string originalCity = hub.LastResult.Destination.Id;
                    Canvas.ForceUpdateCanvases();
                    hubScroll.StopMovement();
                    hubScroll.verticalNormalizedPosition = .45f;
                    ClickCard(category);
                    await WaitReviews();
                    Require(reviews.LastResult.Reviews.Count == 2, "Expected both seeded reviews.");
                    var rows = reviewsCanvas.GetComponentsInChildren<ReviewRow>();
                    Require(rows.Length == 2, "Review cards were duplicated or missing.");
                    foreach (var row in rows)
                    {
                        Require(row.GetComponentsInChildren<StarGraphic>().Count(star => star.Filled) == row.FilledStars, "Stars did not match the numeric rating.");
                    }

                    Require(reviews.LastResult.WorkerThreadId != Thread.CurrentThread.ManagedThreadId, "Review query used the UI thread.");
                    if (category == "Hotels")
                    {
                        Debug.Log("REVIEWS_LOAD_TIMING uiMs=" + reviews.LastLoadMilliseconds + " databaseMs=" +
                            reviews.LastResult.ElapsedMilliseconds + " worker=" + reviews.LastResult.WorkerThreadId + " main=" + Thread.CurrentThread.ManagedThreadId);
                        Click(main, "Actions/GoogleMapsButton");
                        Require(openedCount == 1 && GoogleMapsReference.TryGetUrl(openedUrl, out _), "Google Maps did not use the validated opener.");
                    }

                    Click(main, "Header/CloseButton");
                    Require(hub.LastResult.Destination.Id == originalCity && Mathf.Abs(hubScroll.verticalNormalizedPosition - .45f) < .01f &&
                        !reviews.IsOpen, "Closing reviews changed the hub state.");
                }

                string wanted = hub.LastResult.Restaurants[0].Id;
                await WithBlockedDatabase(async () =>
                {
                    ClickCard("Hotels");
                    ClickCard("Restaurants");
                    int before = frames;
                    await Task.Delay(60);
                    Require(reviews.IsBusy && frames > before, "UI froze during review loading.");
                });
                await WaitReviews();
                Require(reviews.LastResult.Place.Id == wanted, "A stale review request replaced the current card.");
                Click(main, "Header/CloseButton");
                await WithBlockedDatabase(async () =>
                {
                    ClickCard("Hotels");
                    await Task.Delay(30);
                    Click(main, "Header/CloseButton");
                });
                await Task.Delay(30);
                Require(!reviews.IsOpen && reviews.LastResult == null, "A closed request reappeared.");
                await CheckEmptyAndInvalidLink(main, () => openedCount);
                ClickCard("Experiences");
                await WaitReviews();
                int paris = city.options.FindIndex(x => x.text.StartsWith("Paris,", StringComparison.Ordinal));
                Require(paris >= 0, "Paris was not listed.");
                city.value = paris;
                Require(!reviews.IsOpen, "Changing destination did not close reviews.");
                await WaitUntil(() => !hub.IsBusy && hub.LastResult != null, "Paris hub");
                await WithBlockedDatabase(async () =>
                {
                    ClickCard("Hotels");
                    await Task.Delay(30);
                    Click(hubMain, "Header/LogoutButton");
                    Require(!reviews.IsOpen && account.SignedInUserId == null, "Logout left reviews open.");
                });
                await Task.Delay(30);
                Require(!reviews.IsOpen && reviews.LastResult == null, "Reviews reappeared after logout.");
                await Login(login, email, password);
                await EnterHub(signedIn);
                ClickCard("Hotels");
                await WaitReviews();
                Click(main, "Header/CloseButton");
                account.Logout();
            }
            finally
            {
                reviews.UrlOpener = originalOpener;
            }
        }

        private async Task CheckEmptyAndInvalidLink(Transform main, Func<int> openCount)
        {
            var place = hub.LastResult.Hotels[0];
            const string query = "SELECT id AS Id, traveler_name AS TravelerName, rating AS Rating, "
                + "body AS Body, is_demo AS IsDemo FROM reviews WHERE hotel_id = ?";
            var stored = await account.Database.ExecuteAsync(
                connection => connection.Query<ReviewOption>(query, place.Id), CancellationToken.None);
            string url = place.GoogleMapsUrl;
            try
            {
                await account.Database.ExecuteAsync(connection =>
                {
                    connection.Execute("DELETE FROM reviews WHERE hotel_id = ?", place.Id);
                    connection.Execute("UPDATE hotels SET google_maps_url = ? WHERE id = ?", "https://example.invalid/maps", place.Id);
                    return true;
                }, CancellationToken.None);
                ClickCard("Hotels");
                await WaitReviews();
                Require(reviews.LastResult.Reviews.Count == 0 && reviewsCanvas.GetComponentsInChildren<ReviewRow>().Length == 0,
                    "Empty reviews did not show an empty list.");
                var button = main.Find("Actions/GoogleMapsButton").GetComponent<UnityEngine.UI.Button>();
                Require(!button.interactable, "An invalid map link remained enabled.");
                int before = openCount();
                button.onClick.Invoke();
                Require(openCount() == before, "Invalid URL reached the browser opener.");
                Click(main, "Header/CloseButton");
            }
            finally
            {
                await account.Database.ExecuteAsync(connection =>
                {
                    connection.RunInTransaction(() =>
                    {
                        connection.Execute("UPDATE hotels SET google_maps_url = ? WHERE id = ?", url, place.Id);
                        foreach (var review in stored)
                        {
                            connection.Execute("INSERT INTO reviews(id,hotel_id,traveler_name,rating,body,is_demo) VALUES(?,?,?,?,?,?)",
                                review.Id, place.Id, review.TravelerName, review.Rating, review.Body, review.IsDemo ? 1 : 0);
                        }
                    });
                    return true;
                }, CancellationToken.None);
            }
        }

        private void ClickCard(string category)
        {
            var items = hubCanvas.transform.Find("Main/ScrollView/Viewport/Content/" + category + "/Items");
            var card = items.GetComponentsInChildren<PlaceCard>().First();
            Click(card.transform, "ReviewActions/ViewReviewsButton");
        }

        private async Task EnterHub(Transform signedIn)
        {
            Click(signedIn, "SearchFlightsButton");
            var flights = UnityEngine.Object.FindFirstObjectByType<FlightSearchPage>();
            await WaitUntil(() => !flights.IsBusy && flights.LastResult != null, "Flight search");
            Click(flightCanvas.transform, "Main/Header/ExploreDestinationButton");
            await WaitUntil(() => !hub.IsBusy && hub.LastResult != null, "Destination hub");
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
                        throw new TimeoutException("Review smoke gate timeout.");
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
            await WaitUntil(() => !account.IsBusy, "Sign in");
            Require(account.SignedInEmail == email, "Sign in failed.");
        }

        private Task WaitReviews() => WaitUntil(() => !reviews.IsBusy && reviews.LastResult != null, "Review details");
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
