# Milestone 5 — Full scripts

Complete source snapshot for the Reviews milestone. See the [validation record](Milestone-5-Validation.md) for 115 passing tests, the successful Windows build, two passing player checks and manual limitations. Existing destination and login integration scripts are included in full.

## Assets/TravelPlanning/Runtime/Reviews/GoogleMapsReference.cs

```csharp
using System;
using System.Collections.Generic;
using System.Text;

namespace TravelPlanning.Reviews
{
    /// <summary>Checks a Google Maps search reference before the UI offers to open it. Never opens a browser.</summary>
    public static class GoogleMapsReference
    {
        public static bool TryGetUrl(string raw, out string canonicalUrl)
        {
            canonicalUrl = null;
            if (string.IsNullOrWhiteSpace(raw) || raw != raw.Trim() || HasForbiddenCharacters(raw) || raw.IndexOf('#') >= 0)
                return false;
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
                (uri.Host != "google.com" && uri.Host != "www.google.com") || uri.Port != 443 ||
                !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/maps/search/") return false;
            // Also reject an explicitly empty user-info section (https://@google.com/...).
            int authorityStart = raw.IndexOf("://", StringComparison.Ordinal);
            if (authorityStart < 0) return false;
            int authorityEnd = raw.IndexOf('/', authorityStart + 3);
            if (authorityEnd < 0 || raw.Substring(authorityStart + 3, authorityEnd - authorityStart - 3).IndexOf('@') >= 0)
                return false;
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            // Parse the original text: Uri may normalize malformed escapes or dot segments.
            int queryStart = raw.IndexOf('?', authorityEnd);
            if (queryStart < 0 || raw.Substring(authorityEnd, queryStart - authorityEnd) != "/maps/search/") return false;
            string query = raw.Substring(queryStart + 1);
            if (query.Length == 0) return false;
            foreach (string part in query.Split('&'))
            {
                int equals = part.IndexOf('=');
                if (equals <= 0 || !TryDecode(part.Substring(0, equals), out var key) ||
                    !TryDecode(part.Substring(equals + 1), out var value) ||
                    (key != "api" && key != "query") || values.ContainsKey(key)) return false;
                values.Add(key, value);
            }
            if (values.Count != 2 || !values.TryGetValue("api", out var api) || api != "1" ||
                !values.TryGetValue("query", out var place) || string.IsNullOrWhiteSpace(place) || HasForbiddenCharacters(place))
                return false;
            try
            {
                canonicalUrl = "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString(place);
                return true;
            }
            catch (UriFormatException) { return false; }
        }

        private static bool HasForbiddenCharacters(string value)
        {
            foreach (char character in value) if (char.IsControl(character) || character == '\\') return true;
            return false;
        }

        // Decode + as a space and reject malformed percent escapes or malformed UTF-8 bytes.
        private static bool TryDecode(string value, out string decoded)
        {
            decoded = null;
            var output = new StringBuilder();
            var utf8 = new UTF8Encoding(false, true);
            try
            {
                for (int index = 0; index < value.Length; index++)
                {
                    if (value[index] != '%') { output.Append(value[index] == '+' ? ' ' : value[index]); continue; }
                    var bytes = new List<byte>();
                    while (index < value.Length && value[index] == '%')
                    {
                        if (index + 2 >= value.Length || !IsHex(value[index + 1]) || !IsHex(value[index + 2])) return false;
                        bytes.Add(Convert.ToByte(value.Substring(index + 1, 2), 16));
                        index += 3;
                    }
                    output.Append(utf8.GetString(bytes.ToArray()));
                    index--;
                }
                decoded = output.ToString();
                utf8.GetByteCount(decoded); // Reject an unpaired Unicode surrogate as well.
                return true;
            }
            catch (DecoderFallbackException) { return false; }
            catch (EncoderFallbackException) { return false; }
        }

        private static bool IsHex(char value) => (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f') || (value >= 'A' && value <= 'F');
    }
}
```

## Assets/TravelPlanning/Runtime/Reviews/ReviewModels.cs

```csharp
using System.Collections.Generic;
using TravelPlanning.Destinations;

namespace TravelPlanning.Reviews
{
    /// <summary>A review stored locally; IsDemo tells the screen whether to label it as sample data.</summary>
    public sealed class ReviewOption
    {
        public string Id { get; set; }
        public string TravelerName { get; set; }
        public int Rating { get; set; }
        public string Body { get; set; }
        public bool IsDemo { get; set; }
    }

    public sealed class ReviewDetails
    {
        public PlaceOption Place { get; internal set; }
        public IReadOnlyList<ReviewOption> Reviews { get; internal set; }
        public long ElapsedMilliseconds { get; internal set; }
        public int WorkerThreadId { get; internal set; }
    }
}
```

## Assets/TravelPlanning/Runtime/Reviews/ReviewService.cs

```csharp
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TravelPlanning.Data;
using TravelPlanning.Destinations;

namespace TravelPlanning.Reviews
{
    /// <summary>Loads a fresh place and its own reviews from SQLite on the database worker.</summary>
    public sealed class ReviewService
    {
        private readonly TravelDatabase database;
        public ReviewService(TravelDatabase database)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public Task<ReviewDetails> LoadAsync(PlaceCategory category, string placeId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(placeId)) throw new ArgumentException("Choose a place first.", nameof(placeId));
            string table;
            string reviewColumn;
            // Table/column names come only from this list, never from user text.
            switch (category)
            {
                case PlaceCategory.Hotel: table = "hotels"; reviewColumn = "hotel_id"; break;
                case PlaceCategory.Restaurant: table = "restaurants"; reviewColumn = "restaurant_id"; break;
                case PlaceCategory.Experience: table = "experiences"; reviewColumn = "experience_id"; break;
                case PlaceCategory.Hotspot: table = "hotspots"; reviewColumn = "hotspot_id"; break;
                default: throw new ArgumentException("Choose a valid place category.", nameof(category));
            }
            var elapsed = Stopwatch.StartNew();
            return database.ExecuteAsync(connection =>
            {
                var place = connection.FindWithQuery<PlaceOption>(
                    "SELECT id AS Id, destination_id AS DestinationId, name AS Name, description AS Description, address AS Address, " +
                    "price_cents AS PriceCents, currency AS Currency, google_maps_url AS GoogleMapsUrl FROM " + table + " WHERE id=?", placeId);
                if (place == null) throw new ArgumentException("This place is not available in the selected category.", nameof(placeId));
                cancellationToken.ThrowIfCancellationRequested();
                var reviews = connection.Query<ReviewOption>(
                    "SELECT id AS Id, traveler_name AS TravelerName, rating AS Rating, body AS Body, is_demo AS IsDemo " +
                    "FROM reviews WHERE " + reviewColumn + "=? ORDER BY id", placeId);
                cancellationToken.ThrowIfCancellationRequested();
                place.Category = category;
                place.ReviewCount = reviews.Count;
                place.AverageRating = reviews.Count == 0 ? (double?)null : reviews.Average(review => review.Rating);
                cancellationToken.ThrowIfCancellationRequested();
                elapsed.Stop();
                return new ReviewDetails
                {
                    Place = place, Reviews = reviews.AsReadOnly(), ElapsedMilliseconds = elapsed.ElapsedMilliseconds,
                    WorkerThreadId = Thread.CurrentThread.ManagedThreadId
                };
            }, cancellationToken);
        }
    }
}
```

## Assets/TravelPlanning/UI/Reviews/ReviewRow.cs

```csharp
using TMPro;
using TravelPlanning.Reviews;
using UnityEngine;

namespace TravelPlanning.UI.Reviews
{
    /// <summary>Shows the complete stored review with both numeric and visual ratings.</summary>
    public sealed class ReviewRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text travelerName, ratingText, demoLabel, body;
        [SerializeField] private StarGraphic[] stars;
        public int FilledStars { get; private set; }
        public void Show(ReviewOption review)
        {
            travelerName.text = review.TravelerName; ratingText.text = review.Rating + " / 5";
            demoLabel.text = review.IsDemo ? "Fictional demo review — not a Google review" : "Review stored in this local catalog";
            body.text = review.Body; FilledStars = review.Rating;
            for (int index = 0; index < stars.Length; index++) stars[index].SetFilled(index < review.Rating);
        }
    }
}
```

## Assets/TravelPlanning/UI/Reviews/ReviewSmokeRunner.cs

```csharp
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
        [SerializeField] private LoginPage account;
        [SerializeField] private DestinationHubPage hub;
        [SerializeField] private ReviewsPage reviews;
        [SerializeField] private GameObject authCanvas, flightCanvas, hubCanvas, reviewsCanvas;
        private int frames;
        private void Update() => frames++;
        private async void Start()
        {
            var args = Environment.GetCommandLineArgs(); if (Array.IndexOf(args, "-reviewSmoke") < 0) return;
            try
            {
                int index = Array.IndexOf(args, "-authFile"); string file = index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
                if (!Debug.isDebugBuild || !file.StartsWith("auth-smoke-", StringComparison.Ordinal) || !file.EndsWith(".db", StringComparison.Ordinal) || System.IO.Path.GetFileName(file) != file || args.Any(x => x == "-authSmoke" || x == "-flightSmoke" || x == "-destinationSmoke")) throw new InvalidOperationException("Review smoke checks require their isolated database and flag.");
                await RunChecksAsync(Array.IndexOf(args, "-reviewReopen") < 0); Debug.Log("REVIEWS_UI_SMOKE_PASS"); Application.Quit(0);
            }
            catch (Exception exception) { Debug.LogError("REVIEWS_UI_SMOKE_FAIL " + exception.GetType().Name + ": " + exception.Message); Application.Quit(1); }
        }
        public async Task RunChecksAsync(bool register)
        {
            await WaitUntil(() => account.IsReady, "Account startup");
            var right = authCanvas.transform.Find("Right Panel"); var login = right.Find("LoginCard"); var registration = right.Find("RegistrationCard"); var signedIn = right.Find("SignedInCard");
            const string email = "reviews-smoke@example.test", password = "Travel reviews demo phrase 2027!";
            if (register) { Click(login, "Create an account"); Input(registration, "EmailInput").text = email; Input(registration, "PasswordInput").text = Input(registration, "ConfirmationInput").text = password; Click(registration, "LoginButton"); await WaitUntil(() => !account.IsBusy, "Registration"); Require(login.gameObject.activeSelf, "Registration failed."); }
            await Login(login, email, password); await EnterHub(signedIn);
            var hubMain = hubCanvas.transform.Find("Main"); var main = reviewsCanvas.transform.Find("Main"); var city = hubMain.Find("DestinationSelection/Dropdown").GetComponent<TMP_Dropdown>(); var hubScroll = hubMain.Find("ScrollView").GetComponent<UnityEngine.UI.ScrollRect>();
            string openedUrl = null; int openedCount = 0; var originalOpener = reviews.UrlOpener;
            reviews.UrlOpener = url => { openedUrl = url; openedCount++; };
            try
            {
                foreach (string category in new[] { "Hotels", "Restaurants", "Experiences", "Hotspots" })
                {
                    string originalCity = hub.LastResult.Destination.Id; Canvas.ForceUpdateCanvases(); hubScroll.StopMovement(); hubScroll.verticalNormalizedPosition = .45f;
                    ClickCard(category); await WaitReviews(); Require(reviews.LastResult.Reviews.Count == 2, "Expected both seeded reviews.");
                    var rows = reviewsCanvas.GetComponentsInChildren<ReviewRow>(); Require(rows.Length == 2, "Review cards were duplicated or missing.");
                    foreach (var row in rows) Require(row.GetComponentsInChildren<StarGraphic>().Count(star => star.Filled) == row.FilledStars, "Stars did not match the numeric rating.");
                    Require(reviews.LastResult.WorkerThreadId != Thread.CurrentThread.ManagedThreadId, "Review query used the UI thread.");
                    if (category == "Hotels")
                    {
                        Debug.Log("REVIEWS_LOAD_TIMING uiMs=" + reviews.LastLoadMilliseconds + " databaseMs=" + reviews.LastResult.ElapsedMilliseconds + " worker=" + reviews.LastResult.WorkerThreadId + " main=" + Thread.CurrentThread.ManagedThreadId);
                        Click(main, "Actions/GoogleMapsButton"); Require(openedCount == 1 && GoogleMapsReference.TryGetUrl(openedUrl, out _), "Google Maps did not use the validated opener.");
                    }
                    Click(main, "Header/CloseButton"); Require(hub.LastResult.Destination.Id == originalCity && Mathf.Abs(hubScroll.verticalNormalizedPosition - .45f) < .01f && !reviews.IsOpen, "Closing reviews changed the hub state.");
                }
                string wanted = hub.LastResult.Restaurants[0].Id;
                await WithBlockedDatabase(async () => { ClickCard("Hotels"); ClickCard("Restaurants"); int before = frames; await Task.Delay(60); Require(reviews.IsBusy && frames > before, "UI froze during review loading."); });
                await WaitReviews(); Require(reviews.LastResult.Place.Id == wanted, "A stale review request replaced the current card."); Click(main, "Header/CloseButton");
                await WithBlockedDatabase(async () => { ClickCard("Hotels"); await Task.Delay(30); Click(main, "Header/CloseButton"); }); await Task.Delay(30); Require(!reviews.IsOpen && reviews.LastResult == null, "A closed request reappeared.");
                await CheckEmptyAndInvalidLink(main, () => openedCount);
                ClickCard("Experiences"); await WaitReviews(); int paris = city.options.FindIndex(x => x.text.StartsWith("Paris,", StringComparison.Ordinal)); Require(paris >= 0, "Paris was not listed."); city.value = paris;
                Require(!reviews.IsOpen, "Changing destination did not close reviews."); await WaitUntil(() => !hub.IsBusy && hub.LastResult != null, "Paris hub");
                await WithBlockedDatabase(async () => { ClickCard("Hotels"); await Task.Delay(30); Click(hubMain, "Header/LogoutButton"); Require(!reviews.IsOpen && account.SignedInUserId == null, "Logout left reviews open."); });
                await Task.Delay(30); Require(!reviews.IsOpen && reviews.LastResult == null, "Reviews reappeared after logout.");
                await Login(login, email, password); await EnterHub(signedIn); ClickCard("Hotels"); await WaitReviews(); Click(main, "Header/CloseButton"); account.Logout();
            }
            finally { reviews.UrlOpener = originalOpener; }
        }
        private async Task CheckEmptyAndInvalidLink(Transform main, Func<int> openCount)
        {
            var place = hub.LastResult.Hotels[0];
            var stored = await account.Database.ExecuteAsync(connection => connection.Query<ReviewOption>("SELECT id AS Id, traveler_name AS TravelerName, rating AS Rating, body AS Body, is_demo AS IsDemo FROM reviews WHERE hotel_id = ?", place.Id), CancellationToken.None);
            string url = place.GoogleMapsUrl;
            try
            {
                await account.Database.ExecuteAsync(connection => { connection.Execute("DELETE FROM reviews WHERE hotel_id = ?", place.Id); connection.Execute("UPDATE hotels SET google_maps_url = ? WHERE id = ?", "https://example.invalid/maps", place.Id); return true; }, CancellationToken.None);
                ClickCard("Hotels"); await WaitReviews(); Require(reviews.LastResult.Reviews.Count == 0 && reviewsCanvas.GetComponentsInChildren<ReviewRow>().Length == 0, "Empty reviews did not show an empty list.");
                var button = main.Find("Actions/GoogleMapsButton").GetComponent<UnityEngine.UI.Button>(); Require(!button.interactable, "An invalid map link remained enabled."); int before = openCount(); button.onClick.Invoke(); Require(openCount() == before, "Invalid URL reached the browser opener."); Click(main, "Header/CloseButton");
            }
            finally
            {
                await account.Database.ExecuteAsync(connection => { connection.RunInTransaction(() => { connection.Execute("UPDATE hotels SET google_maps_url = ? WHERE id = ?", url, place.Id); foreach (var review in stored) connection.Execute("INSERT INTO reviews(id,hotel_id,traveler_name,rating,body,is_demo) VALUES(?,?,?,?,?,?)", review.Id, place.Id, review.TravelerName, review.Rating, review.Body, review.IsDemo ? 1 : 0); }); return true; }, CancellationToken.None);
            }
        }
        private void ClickCard(string category) { var items = hubCanvas.transform.Find("Main/ScrollView/Viewport/Content/" + category + "/Items"); var card = items.GetComponentsInChildren<PlaceCard>().First(); Click(card.transform, "ReviewActions/ViewReviewsButton"); }
        private async Task EnterHub(Transform signedIn) { Click(signedIn, "SearchFlightsButton"); var flights = UnityEngine.Object.FindFirstObjectByType<FlightSearchPage>(); await WaitUntil(() => !flights.IsBusy && flights.LastResult != null, "Flight search"); Click(flightCanvas.transform, "Main/Header/ExploreDestinationButton"); await WaitUntil(() => !hub.IsBusy && hub.LastResult != null, "Destination hub"); }
        private async Task WithBlockedDatabase(Func<Task> action)
        {
            using (var release = new ManualResetEventSlim(false))
            {
                var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var blocker = account.Database.ExecuteAsync(connection => { entered.SetResult(true); if (!release.Wait(10000)) throw new TimeoutException("Review smoke gate timeout."); return true; }, CancellationToken.None);
                try { await entered.Task; await action(); } finally { release.Set(); } await blocker;
            }
        }
        private async Task Login(Transform login, string email, string password) { Input(login, "EmailInput").text = email; Input(login, "PasswordInput").text = password; Click(login, "LoginButton"); await WaitUntil(() => !account.IsBusy, "Sign in"); Require(account.SignedInEmail == email, "Sign in failed."); }
        private Task WaitReviews() => WaitUntil(() => !reviews.IsBusy && reviews.LastResult != null, "Review details");
        private static void Click(Transform root, string path) => root.Find(path).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        private static TMP_InputField Input(Transform root, string path) => root.Find(path).GetComponent<TMP_InputField>();
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static async Task WaitUntil(Func<bool> predicate, string operation) { var watch = System.Diagnostics.Stopwatch.StartNew(); while (!predicate()) { if (watch.ElapsedMilliseconds > 45000) throw new TimeoutException(operation); await Task.Delay(10); } }
    }
}
```

## Assets/TravelPlanning/UI/Reviews/ReviewsPage.cs

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using TMPro;
using TravelPlanning.Destinations;
using TravelPlanning.Reviews;
using TravelPlanning.UI.Destinations;
using UnityEngine;

namespace TravelPlanning.UI.Reviews
{
    /// <summary>Shows reviews above the hub without changing the hub's city or scroll position.</summary>
    public sealed class ReviewsPage : MonoBehaviour
    {
        [SerializeField] private LoginPage account;
        [SerializeField] private DestinationHubPage hub;
        [SerializeField] private GameObject overlayCanvas;
        [SerializeField] private TMP_Text placeName, summary, status;
        [SerializeField] private UnityEngine.UI.Button closeButton, retryButton, mapsButton;
        [SerializeField] private UnityEngine.UI.ScrollRect scroll;
        [SerializeField] private ReviewRow rowPrefab;
        private readonly List<GameObject> rows = new List<GameObject>();
        private CancellationTokenSource lifetime;
        private int revision;
        private PlaceOption selectedPlace;
        public bool IsBusy { get; private set; }
        public bool IsOpen => overlayCanvas && overlayCanvas.activeSelf;
        public ReviewDetails LastResult { get; private set; }
        public long LastLoadMilliseconds { get; private set; }
        // Tests replace only this boundary; normal clicks use the system browser.
        public Action<string> UrlOpener { get; set; } = Application.OpenURL;
        private void Start()
        {
            if (!account || !hub || !overlayCanvas || !placeName || !summary || !status || !closeButton || !retryButton || !mapsButton || !scroll || !rowPrefab)
            {
                Debug.LogError("ReviewsPage: assign all references on ReviewsController using the prepared scene."); enabled = false; return;
            }
            hub.ReviewRequested += Open; hub.ContentCleared += Close; account.LoggedOut += Close;
            closeButton.onClick.AddListener(Close); retryButton.onClick.AddListener(Retry); mapsButton.onClick.AddListener(OpenMaps);
        }
        private async void Open(PlaceOption place)
        {
            if (place == null || !hub.IsOpen || string.IsNullOrEmpty(account.SignedInUserId)) return;
            Cancel(); ClearRows(); selectedPlace = place; overlayCanvas.SetActive(true); placeName.text = place.Name;
            if (UnityEngine.EventSystems.EventSystem.current) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
            int current = revision; lifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken); var token = lifetime.Token;
            IsBusy = true; summary.text = ""; status.text = "Loading traveler reviews..."; mapsButton.interactable = retryButton.interactable = false;
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var result = await new ReviewService(account.Database).LoadAsync(place.Category, place.Id, token);
                if (!Current(current)) return;
                LastResult = result; placeName.text = result.Place.Name;
                summary.text = result.Place.AverageRating.HasValue ? result.Place.AverageRating.Value.ToString("0.0", CultureInfo.InvariantCulture) + " / 5 • " + result.Place.ReviewCount + " stored reviews" : "No reviews yet";
                foreach (var review in result.Reviews) { var row = Instantiate(rowPrefab, scroll.content); row.Show(review); rows.Add(row.gameObject); }
                status.text = result.Reviews.Count == 0 ? "No traveler reviews have been added for this place yet." : "These sample reviews are stored locally. They are not imported from Google.";
                mapsButton.interactable = GoogleMapsReference.TryGetUrl(result.Place.GoogleMapsUrl, out _);
                Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1; LastLoadMilliseconds = elapsed.ElapsedMilliseconds;
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (Current(current)) status.text = "Reviews could not be loaded. Choose Retry or close this window."; }
            finally { if (Current(current)) { IsBusy = false; retryButton.interactable = true; } }
        }
        private void Retry() { if (selectedPlace != null) Open(selectedPlace); }
        private void OpenMaps()
        {
            if (!IsOpen || IsBusy || LastResult == null) return;
            if (!GoogleMapsReference.TryGetUrl(LastResult.Place.GoogleMapsUrl, out string url)) { status.text = "A Google Maps search link is not available for this place."; mapsButton.interactable = false; return; }
            try { UrlOpener(url); status.text = "Opened the Google Maps search in your browser. Its results are separate from these demo reviews."; }
            catch (Exception) { status.text = "Your browser could not be opened. Please try the Google Maps search button again."; }
        }
        public void Close()
        {
            Cancel(); ClearRows(); selectedPlace = null;
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events && events.currentSelectedGameObject && overlayCanvas && events.currentSelectedGameObject.transform.IsChildOf(overlayCanvas.transform)) events.SetSelectedGameObject(null);
            if (overlayCanvas) overlayCanvas.SetActive(false);
        }
        private void ClearRows() { foreach (var row in rows) if (row) { row.SetActive(false); Destroy(row); } rows.Clear(); LastResult = null; LastLoadMilliseconds = 0; }
        private void Cancel() { revision++; lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null; IsBusy = false; }
        private bool Current(int current) => this && current == revision && IsOpen && hub.IsOpen && !string.IsNullOrEmpty(account.SignedInUserId);
        private void OnDestroy()
        {
            Cancel();
            if (hub) { hub.ReviewRequested -= Open; hub.ContentCleared -= Close; } if (account) account.LoggedOut -= Close;
            if (closeButton) closeButton.onClick.RemoveListener(Close); if (retryButton) retryButton.onClick.RemoveListener(Retry); if (mapsButton) mapsButton.onClick.RemoveListener(OpenMaps);
        }
    }
}
```

## Assets/TravelPlanning/UI/Reviews/StarGraphic.cs

```csharp
using UnityEngine;

namespace TravelPlanning.UI.Reviews
{
    /// <summary>Draws a star directly, avoiding font symbols and extra image dependencies.</summary>
    public sealed class StarGraphic : UnityEngine.UI.MaskableGraphic
    {
        [SerializeField] private bool filled;
        public bool Filled => filled;
        public void SetFilled(bool value) { filled = value; color = value ? new Color32(49, 87, 255, 255) : new Color32(193, 199, 211, 255); SetVerticesDirty(); }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper helper)
        {
            helper.Clear(); Rect bounds = GetPixelAdjustedRect(); Vector2 center = bounds.center; float radius = Mathf.Min(bounds.width, bounds.height) * .5f;
            helper.AddVert(center, color, Vector2.zero);
            for (int i = 0; i < 10; i++)
            {
                float angle = (90 + i * 36) * Mathf.Deg2Rad; float length = i % 2 == 0 ? radius : radius * .43f;
                helper.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length, color, Vector2.zero);
            }
            for (int i = 0; i < 10; i++) helper.AddTriangle(0, i + 1, (i + 1) % 10 + 1);
        }
    }
}
```

## Assets/TravelPlanning/UI/Editor/ReviewSetup.cs

```csharp
using System;
using System.IO;
using TMPro;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Reviews;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Adds review details and upgrades existing place cards without rebuilding the destination scene.</summary>
    public static class ReviewSetup
    {
        public const string ScenePath = AuthSetup.ScenePath;
        public const string BuildPath = "Builds/Reviews/TravelPlannerReviews.exe";
        public const string RowPath = "Assets/TravelPlanning/Prefabs/Reviews/ReviewRow.prefab";
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        [MenuItem("Travel Planning/Reviews/1 - Prepare Reviews")]
        public static void Prepare()
        {
            DestinationHubSetup.Prepare(); UpgradePlaceCards();
            if (UnityEngine.Object.FindFirstObjectByType<ReviewsPage>() != null) return;
            var account = UnityEngine.Object.FindFirstObjectByType<LoginPage>(); var hub = UnityEngine.Object.FindFirstObjectByType<DestinationHubPage>();
            var canvas = new GameObject("ReviewsCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster)); canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; canvas.GetComponent<Canvas>().sortingOrder = 4;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0;
            var blocker = Rect("InputBlocker", canvas.transform); Stretch(blocker); var image = blocker.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = Color.white; image.raycastTarget = true;
            var main = Rect("Main", canvas.transform); Stretch(main); main.offsetMin = new Vector2(48, 40); main.offsetMax = new Vector2(-48, -40); Vertical(main, 18);
            var header = Horizontal("Header", main, 72); var title = Label(header, "PlaceName", "Traveler reviews", 40); Layout(title.gameObject, -1, 1); var close = Button(header, "CloseButton", "Close reviews", 240);
            var summary = Label(main, "Summary", "", 28); Layout(summary.gameObject, 46);
            var note = Label(main, "DemoNote", "Fictional demo reviews from this local catalog. Google Maps search results are separate.", 23); Layout(note.gameObject, 44);
            var status = Label(main, "Status", "", 23); Layout(status.gameObject, 68);
            var actions = Horizontal("Actions", main, 56); var maps = Button(actions, "GoogleMapsButton", "Google Maps search", 330); var retry = Button(actions, "RetryButton", "Retry", 160);
            // Explicit navigation keeps keyboard focus inside this modal screen.
            Navigation(close, retry, maps); Navigation(retry, maps, close); Navigation(maps, close, retry);
            var rootScroll = Rect("ScrollView", main); Layout(rootScroll.gameObject, -1, 1); var scroll = rootScroll.gameObject.AddComponent<UnityEngine.UI.ScrollRect>(); scroll.horizontal = false; scroll.scrollSensitivity = 45; scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            var viewport = Rect("Viewport", rootScroll); Stretch(viewport); viewport.gameObject.AddComponent<UnityEngine.UI.Image>(); viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var content = Rect("Content", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero; Vertical(content, 20); content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize; scroll.viewport = viewport; scroll.content = content;
            var controller = new GameObject("ReviewsController").AddComponent<ReviewsPage>(); var fields = new SerializedObject(controller); Set(fields, "account", account); Set(fields, "hub", hub); Set(fields, "overlayCanvas", canvas); Set(fields, "placeName", title); Set(fields, "summary", summary); Set(fields, "status", status); Set(fields, "closeButton", close); Set(fields, "retryButton", retry); Set(fields, "mapsButton", maps); Set(fields, "scroll", scroll); Set(fields, "rowPrefab", CreateRow()); fields.ApplyModifiedPropertiesWithoutUndo();
            var runner = controller.gameObject.AddComponent<ReviewSmokeRunner>(); var runnerFields = new SerializedObject(runner); Set(runnerFields, "account", account); Set(runnerFields, "hub", hub); Set(runnerFields, "reviews", controller); Set(runnerFields, "authCanvas", GameObject.Find("Canvas")); Set(runnerFields, "flightCanvas", (GameObject)new SerializedObject(UnityEngine.Object.FindFirstObjectByType<TravelPlanning.UI.Flights.FlightSearchPage>()).FindProperty("flightCanvas").objectReferenceValue); Set(runnerFields, "hubCanvas", (GameObject)new SerializedObject(hub).FindProperty("hubCanvas").objectReferenceValue); Set(runnerFields, "reviewsCanvas", canvas); runnerFields.ApplyModifiedPropertiesWithoutUndo();
            canvas.SetActive(false); EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); Debug.Log("REVIEWS_SCENE_READY " + ScenePath);
        }
        private static void UpgradePlaceCards()
        {
            var root = PrefabUtility.LoadPrefabContents(DestinationHubSetup.PlaceCardPath);
            try
            {
                var card = root.GetComponent<PlaceCard>(); var fields = new SerializedObject(card);
                if (!root.transform.Find("ReviewActions"))
                {
                    var actions = Horizontal("ReviewActions", root.transform, 48); var button = Button(actions, "ViewReviewsButton", "View reviews", 250);
                    fields.FindProperty("viewReviewsButton").objectReferenceValue = button; fields.ApplyModifiedPropertiesWithoutUndo();
                    root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 292;
                }
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) text.richText = false;
                PrefabUtility.SaveAsPrefabAsset(root, DestinationHubSetup.PlaceCardPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        [MenuItem("Travel Planning/Reviews/2 - Build Windows x64")]
        public static void BuildWindows()
        {
            Prepare(); Directory.CreateDirectory(Path.GetDirectoryName(BuildPath)); var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = BuildPath, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Reviews build failed.");
            var notices = Path.Combine(Path.GetDirectoryName(BuildPath), "ThirdPartyNotices"); Directory.CreateDirectory(notices);
            foreach (string file in Directory.GetFiles("Assets/Plugins/SQLite/Licenses")) if (!file.EndsWith(".meta")) File.Copy(file, Path.Combine(notices, Path.GetFileName(file)), true);
            File.Copy("Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt", Path.Combine(notices, "LiberationSans-OFL.txt"), true); File.Copy("Assets/Plugins/LiteDB/LICENSE.txt", Path.Combine(notices, "LiteDB-LICENSE.txt"), true); Debug.Log("REVIEWS_BUILD_PASS " + Path.GetFullPath(BuildPath));
        }
        private static ReviewRow CreateRow()
        {
            if (File.Exists(RowPath)) return AssetDatabase.LoadAssetAtPath<GameObject>(RowPath).GetComponent<ReviewRow>();
            Directory.CreateDirectory(Path.GetDirectoryName(RowPath)); var root = Rect("ReviewRow", null); root.sizeDelta = new Vector2(1800, 220); var background = root.gameObject.AddComponent<UnityEngine.UI.Image>(); background.color = new Color32(244, 247, 255, 255); background.raycastTarget = false;
            var layout = Vertical(root, 8); layout.padding = new RectOffset(20, 20, 16, 16);
            var traveler = Label(root, "TravelerName", "Traveler", 26); Layout(traveler.gameObject, 36);
            var ratings = Horizontal("Rating", root, 32); var stars = new StarGraphic[5];
            for (int i = 0; i < 5; i++) { var star = Rect("Star" + (i + 1), ratings); Layout(star.gameObject, 28, 0, 28); stars[i] = star.gameObject.AddComponent<StarGraphic>(); stars[i].raycastTarget = false; stars[i].SetFilled(false); }
            var numericRating = Label(ratings, "RatingText", "", 24); Layout(numericRating.gameObject, -1, 1);
            var demo = Label(root, "DemoLabel", "", 21); Layout(demo.gameObject, 30);
            var body = Label(root, "Body", "", 24); body.textWrappingMode = TextWrappingModes.Normal; body.overflowMode = TextOverflowModes.Overflow;
            // No fixed height on the body or row: the layout uses the full text's preferred height.
            var row = root.gameObject.AddComponent<ReviewRow>(); var fields = new SerializedObject(row); Set(fields, "travelerName", traveler); Set(fields, "ratingText", numericRating); Set(fields, "demoLabel", demo); Set(fields, "body", body); var starFields = fields.FindProperty("stars"); starFields.arraySize = 5; for (int i = 0; i < 5; i++) starFields.GetArrayElementAtIndex(i).objectReferenceValue = stars[i]; fields.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, RowPath); UnityEngine.Object.DestroyImmediate(root.gameObject); return prefab.GetComponent<ReviewRow>();
        }
        private static void Navigation(UnityEngine.UI.Button button, UnityEngine.UI.Button previous, UnityEngine.UI.Button next) { button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit, selectOnUp = previous, selectOnLeft = previous, selectOnDown = next, selectOnRight = next }; }
        private static UnityEngine.UI.Button Button(Transform parent, string name, string text, float width) { var rect = Rect(name, parent); Layout(rect.gameObject, 48, 0, width); var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color32(49, 87, 255, 255); var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image; var label = Label(rect, "Label", text, 24); Stretch(label.rectTransform); label.alignment = TextAlignmentOptions.Center; label.color = Color.white; return button; }
        private static RectTransform Horizontal(string name, Transform parent, float height) { var rect = Rect(name, parent); Layout(rect.gameObject, height); var layout = rect.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>(); layout.spacing = 16; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = true; return rect; }
        private static UnityEngine.UI.VerticalLayoutGroup Vertical(RectTransform rect, int spacing) { var layout = rect.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(); layout.spacing = spacing; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false; return layout; }
        private static TMP_Text Label(Transform parent, string name, string text, int size) { var rect = Rect(name, parent); var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = Font; label.text = text; label.fontSize = size; label.color = new Color32(51, 51, 51, 255); label.raycastTarget = false; label.richText = false; return label; }
        private static RectTransform Rect(string name, Transform parent) { var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false); return obj.GetComponent<RectTransform>(); }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Layout(GameObject obj, float height = -1, float flexible = 0, float width = -1) { var layout = obj.GetComponent<UnityEngine.UI.LayoutElement>() ?? obj.AddComponent<UnityEngine.UI.LayoutElement>(); layout.preferredHeight = height; layout.flexibleHeight = flexible; layout.flexibleWidth = flexible; layout.preferredWidth = width; }
        private static void Set(SerializedObject fields, string name, UnityEngine.Object value) => fields.FindProperty(name).objectReferenceValue = value;
    }
}
```

## Assets/TravelPlanning/Tests/EditMode/ReviewServiceTests.cs

```csharp
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TravelPlanning.Data;
using TravelPlanning.Destinations;
using TravelPlanning.Reviews;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class ReviewServiceTests
    {
        private string folder;
        private string path;
        private TravelDatabase database;
        private ReviewService service;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "TravelReviewTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, "travel.db");
            database = new TravelDatabase(Path.Combine(Application.streamingAssetsPath, "Database", "travel_seed.db"), path);
            service = new ReviewService(database);
        }

        [TearDown]
        public void TearDown() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }

        [UnityTest]
        public IEnumerator FourPlaceTypesReturnOnlyTheirOwnSampleReviewsAndFreshSummary()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            foreach (PlaceCategory category in Enum.GetValues(typeof(PlaceCategory)))
            {
                string placeId = "london-" + category.ToString().ToLowerInvariant() + "-1";
                var load = service.LoadAsync(category, placeId);
                yield return Wait(load);
                var details = load.Result;
                Assert.That(details.Place.Id, Is.EqualTo(placeId));
                Assert.That(details.Place.Category, Is.EqualTo(category));
                Assert.That(details.Place.DestinationId, Is.EqualTo("london"));
                Assert.That(details.Place.Name, Is.Not.Empty);
                Assert.That(details.Place.GoogleMapsUrl, Does.StartWith("https://www.google.com/maps/search/"));
                Assert.That(details.Reviews.Select(review => review.Id), Is.EqualTo(new[] { placeId + "-review-1", placeId + "-review-2" }));
                Assert.That(details.Reviews.Select(review => review.Rating), Is.EqualTo(new[] { 4, 5 }));
                Assert.That(details.Reviews.All(review => review.IsDemo && review.TravelerName.StartsWith("Demo traveler") && review.Body.StartsWith("FICTIONAL DEMO REVIEW:")), Is.True);
                Assert.That(details.Place.ReviewCount, Is.EqualTo(2));
                Assert.That(details.Place.AverageRating, Is.EqualTo(4.5));
                Assert.That(details.WorkerThreadId, Is.Not.EqualTo(mainThread));
            }
        }

        [UnityTest]
        public IEnumerator ReloadReflectsChangedPlaceNonDemoReviewAndThenEmptyReviews()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(database.ExecuteAsync(connection =>
            {
                connection.Execute("UPDATE hotels SET name=?, price_cents=? WHERE id=?", "Changed hotel", 12345, "london-hotel-1");
                connection.Execute("DELETE FROM reviews WHERE hotel_id=?", "london-hotel-1");
                connection.Execute("INSERT INTO reviews (id,hotel_id,traveler_name,rating,body,is_demo) VALUES (?,?,?,?,?,?)", "fixture-review", "london-hotel-1", "Local traveler", 3, "Local fixture review body", 0);
                return 0;
            }, CancellationToken.None));
            var edited = service.LoadAsync(PlaceCategory.Hotel, "london-hotel-1");
            yield return Wait(edited);
            Assert.That(edited.Result.Place.Name, Is.EqualTo("Changed hotel"));
            Assert.That(edited.Result.Place.PriceCents, Is.EqualTo(12345));
            Assert.That(edited.Result.Place.ReviewCount, Is.EqualTo(1));
            Assert.That(edited.Result.Place.AverageRating, Is.EqualTo(3));
            Assert.That(edited.Result.Reviews.Single().IsDemo, Is.False);
            Assert.That(edited.Result.Reviews.Single().Body, Is.EqualTo("Local fixture review body"));
            yield return Wait(database.ExecuteAsync(connection => connection.Execute("DELETE FROM reviews WHERE hotel_id=?", "london-hotel-1"), CancellationToken.None));
            var empty = service.LoadAsync(PlaceCategory.Hotel, "london-hotel-1");
            yield return Wait(empty);
            Assert.That(empty.Result.Reviews, Is.Empty);
            Assert.That(empty.Result.Place.ReviewCount, Is.Zero);
            Assert.That(empty.Result.Place.AverageRating, Is.Null);
        }

        [UnityTest]
        public IEnumerator TypedForeignKeyKeepsSameIdInAnotherCategoryIsolated()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(database.ExecuteAsync(connection =>
            {
                connection.Execute("INSERT INTO restaurants (id,destination_id,name,description,address,price_cents,currency,google_maps_url) VALUES (?,?,?,?,?,?,?,?)", "london-hotel-1", "london", "Different category", "Fixture", "Fixture address", 1000, "USD", "https://www.google.com/maps/search/?api=1&query=Fixture");
                connection.Execute("INSERT INTO reviews (id,restaurant_id,traveler_name,rating,body,is_demo) VALUES (?,?,?,?,?,?)", "restaurant-collision", "london-hotel-1", "Fixture traveler", 1, "Restaurant only", 1);
                return 0;
            }, CancellationToken.None));
            var hotel = service.LoadAsync(PlaceCategory.Hotel, "london-hotel-1");
            var restaurant = service.LoadAsync(PlaceCategory.Restaurant, "london-hotel-1");
            yield return Wait(hotel);
            yield return Wait(restaurant);
            Assert.That(hotel.Result.Reviews.Count, Is.EqualTo(2));
            Assert.That(hotel.Result.Place.AverageRating, Is.EqualTo(4.5));
            Assert.That(restaurant.Result.Reviews.Single().Id, Is.EqualTo("restaurant-collision"));
            Assert.That(restaurant.Result.Place.AverageRating, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator InvalidCategoryWrongCategoryAndInjectionIdAreRejected()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            Assert.Throws<ArgumentException>(() => service.LoadAsync((PlaceCategory)99, "london-hotel-1"));
            Assert.Throws<ArgumentException>(() => service.LoadAsync(PlaceCategory.Hotel, null));
            Assert.Throws<ArgumentException>(() => service.LoadAsync(PlaceCategory.Hotel, "  "));
            foreach (string id in new[] { "missing", "london-restaurant-1", "london-hotel-1' OR 1=1 --", "'; DELETE FROM reviews; --" })
            {
                var pending = service.LoadAsync(PlaceCategory.Hotel, id);
                yield return Completion(pending);
                Assert.That(pending.IsFaulted, Is.True);
                Assert.That(pending.Exception.GetBaseException(), Is.InstanceOf<ArgumentException>());
            }
            var valid = service.LoadAsync(PlaceCategory.Hotel, "london-hotel-1");
            yield return Wait(valid);
            Assert.That(valid.Result.Reviews.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator CancellationWhileQueuedStopsReviewLoad()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            using (var release = new ManualResetEventSlim(false))
            using (var entered = new ManualResetEventSlim(false))
            using (var cancellation = new CancellationTokenSource())
            {
                var blocker = database.ExecuteAsync(connection => { entered.Set(); return release.Wait(TimeSpan.FromSeconds(15)); }, CancellationToken.None);
                while (!entered.IsSet) yield return null;
                Task<ReviewDetails> pending;
                try
                {
                    pending = service.LoadAsync(PlaceCategory.Hotel, "london-hotel-1", cancellation.Token);
                    cancellation.Cancel();
                }
                finally { release.Set(); }
                yield return Wait(blocker);
                yield return Completion(pending);
                Assert.That(pending.IsCanceled, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator ReadingReviewsPreservesEveryDatabaseByte()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            byte[] before = Hash();
            foreach (PlaceCategory category in Enum.GetValues(typeof(PlaceCategory)))
                yield return Wait(service.LoadAsync(category, "london-" + category.ToString().ToLowerInvariant() + "-1"));
            Assert.That(Hash(), Is.EqualTo(before));
        }

        private byte[] Hash() { using (var hash = SHA256.Create()) return hash.ComputeHash(File.ReadAllBytes(path)); }
        private static IEnumerator Wait(Task task) { yield return Completion(task); task.GetAwaiter().GetResult(); }
        private static IEnumerator Completion(Task task)
        {
            double deadline = UnityEditor.EditorApplication.timeSinceStartup + 30;
            while (!task.IsCompleted)
            {
                Assert.That(UnityEditor.EditorApplication.timeSinceStartup, Is.LessThan(deadline), "Review operation timed out.");
                yield return null;
            }
        }
    }
}
```

## Assets/TravelPlanning/Tests/EditMode/GoogleMapsReferenceTests.cs

```csharp
using System.IO;
using NUnit.Framework;
using SQLite;
using TravelPlanning.Data;
using TravelPlanning.Destinations;
using TravelPlanning.Reviews;
using UnityEngine;

namespace TravelPlanning.Tests
{
    public sealed class GoogleMapsReferenceTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=++")]
        [TestCase("http://www.google.com/maps/search/?api=1&query=London")]
        [TestCase("file:///maps/search/?api=1&query=London")]
        [TestCase("javascript:alert(1)")]
        [TestCase("https://user@www.google.com/maps/search/?api=1&query=London")]
        [TestCase("https://@www.google.com/maps/search/?api=1&query=London")]
        [TestCase("https://www.google.com.evil.example/maps/search/?api=1&query=London")]
        [TestCase("https://evil.example/maps/search/?api=1&query=London")]
        [TestCase("https://maps.google.com/maps/search/?api=1&query=London")]
        [TestCase("https://www.google.com:444/maps/search/?api=1&query=London")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=London#details")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=London#")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=London&redirect=https://evil.example")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=London&query=Paris")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=London&api=1")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=London&%71uery=Paris")]
        [TestCase("https://www.google.com/maps/search/?api=2&query=London")]
        [TestCase("https://www.google.com/maps/search/?query=London")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=London&")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=bad%ZZ")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=bad%")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=%FF")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=%0ALondon")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=London%5CParis")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=London\n")]
        [TestCase("https://www.google.com\\maps/search/?api=1&query=London")]
        [TestCase(" https://www.google.com/maps/search/?api=1&query=London")]
        [TestCase("https://www.google.com/other/../maps/search/?api=1&query=London")]
        [TestCase("https://www.google.com/maps/search?api=1&query=London")]
        [TestCase("https://www.google.com/maps/%73earch/?api=1&query=London")]
        public void UnsafeOrMalformedReferenceFailsWithoutThrowing(string raw)
        {
            string canonical = "unchanged";
            Assert.DoesNotThrow(() => Assert.That(GoogleMapsReference.TryGetUrl(raw, out canonical), Is.False));
            Assert.That(canonical, Is.Null);
        }

        [TestCase("https://google.com/maps/search/?query=London+Eye&api=1", "https://www.google.com/maps/search/?api=1&query=London%20Eye")]
        [TestCase("https://www.google.com:443/maps/search/?api=1&query=A%26B%20%2B%20Cafe", "https://www.google.com/maps/search/?api=1&query=A%26B%20%2B%20Cafe")]
        [TestCase("https://www.google.com/maps/search/?api=1&query=%E6%9D%B1%E4%BA%AC", "https://www.google.com/maps/search/?api=1&query=%E6%9D%B1%E4%BA%AC")]
        public void SafeSearchReferenceIsCanonicalized(string raw, string expected)
        {
            Assert.That(GoogleMapsReference.TryGetUrl(raw, out var canonical), Is.True);
            Assert.That(canonical, Is.EqualTo(expected));
        }

        [Test]
        public void All144BundledPlaceReferencesAreAccepted()
        {
            SqliteRuntime.Initialize();
            using (var connection = new SQLiteConnection(Path.Combine(Application.streamingAssetsPath, "Database", "travel_seed.db"), SQLiteOpenFlags.ReadOnly))
            {
                var places = connection.Query<PlaceOption>("SELECT google_maps_url AS GoogleMapsUrl FROM hotels UNION ALL SELECT google_maps_url FROM restaurants UNION ALL SELECT google_maps_url FROM experiences UNION ALL SELECT google_maps_url FROM hotspots");
                Assert.That(places.Count, Is.EqualTo(144));
                foreach (var place in places)
                {
                    Assert.That(GoogleMapsReference.TryGetUrl(place.GoogleMapsUrl, out var canonical), Is.True, place.GoogleMapsUrl);
                    Assert.That(canonical, Does.StartWith("https://www.google.com/maps/search/?api=1&query="));
                }
            }
        }
    }
}
```

## Assets/TravelPlanning/Tests/EditMode/ReviewSceneTests.cs

```csharp
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using TravelPlanning.Reviews;
using TravelPlanning.UI;
using TravelPlanning.UI.Editor;
using TravelPlanning.UI.Reviews;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class ReviewSceneTests
    {
        [Test]
        public void ReviewOverlayReferencesAndInputBlockingAreComplete()
        {
            EditorSceneManager.OpenScene(ReviewSetup.ScenePath); var page = Object.FindFirstObjectByType<ReviewsPage>(); Assert.That(page.transform.parent, Is.Null);
            var fields = new SerializedObject(page);
            foreach (string name in new[] { "account", "hub", "overlayCanvas", "placeName", "summary", "status", "closeButton", "retryButton", "mapsButton", "scroll", "rowPrefab" }) Assert.That(fields.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
            var canvas = (GameObject)fields.FindProperty("overlayCanvas").objectReferenceValue; Assert.That(canvas.activeSelf, Is.False); Assert.That(canvas.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(3));
            Assert.That(canvas.transform.Find("InputBlocker").GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.True);
            foreach (var button in canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true)) Assert.That(button.navigation.mode, Is.EqualTo(UnityEngine.UI.Navigation.Mode.Explicit));
            var row = (ReviewRow)fields.FindProperty("rowPrefab").objectReferenceValue; Assert.That(row.GetComponentsInChildren<StarGraphic>(true).Length, Is.EqualTo(5));
            Assert.That(row.transform.Find("Body").GetComponent<UnityEngine.UI.LayoutElement>(), Is.Null, "The review body must use its full preferred height.");
            Assert.That(GameObject.Find("Canvas/Right Panel/LoginCard/EmailInput").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(0, -230)));
        }
        [Test]
        public void LongLiteralReviewBodyExpandsWithoutClippingAndShowsFiveStarShapes()
        {
            EditorSceneManager.OpenScene(ReviewSetup.ScenePath); var fields = new SerializedObject(Object.FindFirstObjectByType<ReviewsPage>());
            var canvas = (GameObject)fields.FindProperty("overlayCanvas").objectReferenceValue; canvas.SetActive(true);
            var scroll = (UnityEngine.UI.ScrollRect)fields.FindProperty("scroll").objectReferenceValue; var prefab = (ReviewRow)fields.FindProperty("rowPrefab").objectReferenceValue;
            var row = Object.Instantiate(prefab, scroll.content); string body = string.Concat(Enumerable.Repeat("<b>Literal catalog text</b> should remain readable while the whole review wraps onto more lines. ", 35));
            row.Show(new ReviewOption { TravelerName = "<b>Demo traveler</b>", Rating = 3, IsDemo = true, Body = body });
            Canvas.ForceUpdateCanvases(); UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content); Canvas.ForceUpdateCanvases();
            var text = row.transform.Find("Body").GetComponent<TMP_Text>(); Assert.That(text.richText, Is.False); Assert.That(text.text, Is.EqualTo(body)); Assert.That(text.rectTransform.rect.height + 1, Is.GreaterThanOrEqualTo(text.preferredHeight));
            Assert.That(row.GetComponent<RectTransform>().rect.height, Is.GreaterThan(250)); Assert.That(row.GetComponentsInChildren<StarGraphic>().Count(star => star.Filled), Is.EqualTo(3));
            Object.DestroyImmediate(row.gameObject);
        }
        [UnityTest]
        public IEnumerator ActualReviewControlsPreserveHubValidateLinksAndCancelStaleRequests()
        {
            EditorSceneManager.OpenScene(ReviewSetup.ScenePath); var configuration = new SerializedObject(Object.FindFirstObjectByType<LoginPage>());
            configuration.FindProperty("developmentDatabaseFile").stringValue = "review-editor-" + System.Guid.NewGuid().ToString("N") + ".db"; configuration.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            var account = Object.FindFirstObjectByType<LoginPage>(); string file = new SerializedObject(account).FindProperty("developmentDatabaseFile").stringValue;
            Assert.That(file, Does.StartWith("review-editor-")); Assert.That(System.IO.Path.GetFileName(file), Is.EqualTo(file));
            var task = Object.FindFirstObjectByType<ReviewSmokeRunner>().RunChecksAsync(true); float deadline = Time.realtimeSinceStartup + 100;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(task.IsCompleted, Is.True, "Review UI flow timed out."); if (task.IsFaulted) Assert.Fail(task.Exception.GetBaseException().ToString()); Assert.That(task.IsCanceled, Is.False);
            string path = System.IO.Path.Combine(Application.persistentDataPath, file); Assert.That(System.IO.File.Exists(path), Is.True); System.IO.File.Delete(path);
            yield return new ExitPlayMode();
        }
    }
}
```

## tools/Test-Reviews.ps1

```powershell
param([string]$BuildRoot = 'Builds/Reviews')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $BuildRoot).Path
$exe = Join-Path $root 'TravelPlannerReviews.exe'
$id = [Guid]::NewGuid().ToString('N')
$file = "auth-smoke-$id.db"
$logs = Join-Path (Get-Location).Path "Logs/Reviews/$id"
New-Item -ItemType Directory -Path $logs -Force | Out-Null
foreach ($phase in @('register-reviews','reopen-reviews')) {
    $log = Join-Path $logs "$phase.log"
    $arguments = @('-batchmode','-nographics','-reviewSmoke','-authFile',$file,'-logFile',('"' + $log + '"'))
    if ($phase -eq 'reopen-reviews') { $arguments += '-reviewReopen' }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(120000)) { $process.Kill(); throw "Review $phase timed out. See $log" }
    $process.Refresh()
    $output = Get-Content -LiteralPath $log -Raw
    if ($process.ExitCode -ne 0 -or $output -notmatch 'REVIEWS_UI_SMOKE_PASS') { throw "Review $phase failed. See $log" }
    Write-Output "PASS $phase - review UI, stars, fake browser, empty/invalid-link, cancellation and recovery checks. Log: $log"
    Select-String -LiteralPath $log -Pattern 'REVIEWS_LOAD_TIMING' | ForEach-Object { $_.Line }
}
```

## Assets/TravelPlanning/UI/Destinations/PlaceCard.cs

```csharp
using System;
using System.Globalization;
using TMPro;
using TravelPlanning.Destinations;
using UnityEngine;

namespace TravelPlanning.UI.Destinations
{
    /// <summary>Displays a seeded place, including clear demo price and review labels.</summary>
    public sealed class PlaceCard : MonoBehaviour
    {
        [SerializeField] private TMP_Text placeName, price, rating, description, address;
        [SerializeField] private UnityEngine.UI.Button viewReviewsButton;
        private PlaceOption shownPlace;
        private Action<PlaceOption> reviewRequested;
        private void Awake() { if (viewReviewsButton) viewReviewsButton.onClick.AddListener(OpenReviews); }
        public void Show(PlaceOption place, Action<PlaceOption> onReviews = null)
        {
            shownPlace = place; reviewRequested = onReviews;
            if (viewReviewsButton) viewReviewsButton.interactable = onReviews != null;
            placeName.text = place.Name;
            price.text = FormatPrice(place.PriceCents, place.Category);
            rating.text = place.AverageRating.HasValue && place.ReviewCount > 0
                ? place.AverageRating.Value.ToString("0.0", CultureInfo.InvariantCulture) + " / 5 • " + place.ReviewCount + " demo reviews" : "No reviews yet";
            description.text = place.Description;
            address.text = place.Address;
        }
        private void OpenReviews() => reviewRequested?.Invoke(shownPlace);
        private void OnDestroy() { if (viewReviewsButton) viewReviewsButton.onClick.RemoveListener(OpenReviews); }
        public static string FormatPrice(int? cents, PlaceCategory category)
        {
            if (!cents.HasValue) return "Price unavailable";
            if (cents.Value == 0) return "Free";
            string unit = category == PlaceCategory.Hotel ? " / room / night" : category == PlaceCategory.Restaurant ? " / person (meal)" : category == PlaceCategory.Experience ? " / adult" : " / visit";
            return "USD " + (cents.Value / 100m).ToString("N2", CultureInfo.InvariantCulture) + unit;
        }
    }
}

```

## Assets/TravelPlanning/UI/Destinations/PlaceSection.cs

```csharp
using System.Collections.Generic;
using TMPro;
using TravelPlanning.Destinations;
using UnityEngine;

namespace TravelPlanning.UI.Destinations
{
    /// <summary>Owns the cards for one category, so clearing a city never duplicates its old cards.</summary>
    public sealed class PlaceSection : MonoBehaviour
    {
        [SerializeField] private TMP_Text emptyMessage;
        [SerializeField] private Transform items;
        private readonly List<GameObject> cards = new List<GameObject>();
        public int CardCount => cards.Count;
        public void Show(IReadOnlyList<PlaceOption> places, PlaceCard prefab, System.Action<PlaceOption> onReviews = null)
        {
            Clear(); emptyMessage.gameObject.SetActive(places.Count == 0);
            foreach (var place in places)
            {
                var card = Instantiate(prefab, items); card.Show(place, onReviews); cards.Add(card.gameObject);
            }
        }
        public void Clear()
        {
            foreach (var card in cards) if (card) { card.SetActive(false); Destroy(card); }
            cards.Clear(); emptyMessage.gameObject.SetActive(false);
        }
    }
}
```

## Assets/TravelPlanning/UI/Destinations/DestinationHubPage.cs

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using TravelPlanning.Destinations;
using TravelPlanning.UI.Flights;
using UnityEngine;

namespace TravelPlanning.UI.Destinations
{
    /// <summary>Loads one destination off the UI thread and ignores responses from closed or older requests.</summary>
    public sealed class DestinationHubPage : MonoBehaviour
    {
        [SerializeField] private LoginPage account;
        [SerializeField] private FlightSearchPage flights;
        [SerializeField] private GameObject hubCanvas;
        [SerializeField] private TMP_Dropdown destination;
        [SerializeField] private TMP_Text description, status;
        [SerializeField] private UnityEngine.UI.Button openButton, backButton, logoutButton, retryButton;
        [SerializeField] private UnityEngine.UI.ScrollRect scroll;
        [SerializeField] private PlaceSection hotels, restaurants, experiences, hotspots;
        [SerializeField] private PlaceCard cardPrefab;
        private DestinationHubService service;
        private IReadOnlyList<DestinationOption> options;
        private CancellationTokenSource requestLifetime;
        private int revision;
        public bool IsBusy { get; private set; }
        public bool IsOpen => hubCanvas && hubCanvas.activeSelf;
        public DestinationHubResult LastResult { get; private set; }
        public long LastLoadMilliseconds { get; private set; }
        public int CardCount => hotels.CardCount + restaurants.CardCount + experiences.CardCount + hotspots.CardCount;
        public event Action<PlaceOption> ReviewRequested;
        public event Action ContentCleared;
        private void Start()
        {
            if (!account || !flights || !hubCanvas || !destination || !description || !status || !openButton || !backButton || !logoutButton || !retryButton || !scroll || !hotels || !restaurants || !experiences || !hotspots || !cardPrefab)
            {
                Debug.LogError("DestinationHubPage: assign all references on DestinationController using the prepared scene."); enabled = false; return;
            }
            openButton.onClick.AddListener(Open); backButton.onClick.AddListener(Back); logoutButton.onClick.AddListener(Logout); retryButton.onClick.AddListener(Retry);
            destination.onValueChanged.AddListener(SelectDestination); account.LoggedOut += OnLoggedOut;
        }
        public async void Open()
        {
            if (!account.IsReady || string.IsNullOrEmpty(account.SignedInUserId)) return;
            string selectedId = flights.SelectedDestinationId;
            if (flights.IsOpen) flights.Suspend();
            hubCanvas.SetActive(true); Clear(); Cancel(); options = null; destination.ClearOptions();
            service = new DestinationHubService(account.Database);
            int current = revision; var token = Begin();
            IsBusy = true; destination.interactable = false; retryButton.interactable = false; status.text = "Loading destinations...";
            try
            {
                var loaded = await service.LoadOptionsAsync(token);
                if (!Current(current)) return;
                options = loaded;
                if (options.Count == 0) { status.text = "No destinations are available in this sample catalog."; IsBusy = false; retryButton.interactable = true; return; }
                destination.ClearOptions(); destination.AddOptions(options.Select(x => x.Name + ", " + x.Country).ToList());
                destination.SetValueWithoutNotify(Math.Max(0, options.ToList().FindIndex(x => x.Id == selectedId))); destination.RefreshShownValue();
                destination.interactable = true; LoadSelected();
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (Current(current)) { status.text = "Destinations could not be loaded. Choose Retry."; IsBusy = false; retryButton.interactable = true; } }
        }
        private void SelectDestination(int _) => LoadSelected();
        private void Retry() { if (options == null || options.Count == 0) Open(); else LoadSelected(); }
        private async void LoadSelected()
        {
            if (!IsOpen || options == null || destination.value < 0 || destination.value >= options.Count || string.IsNullOrEmpty(account.SignedInUserId)) return;
            Cancel(); Clear(); int current = revision; var token = Begin();
            IsBusy = true; retryButton.interactable = false; status.text = "Finding places in " + options[destination.value].Name + "...";
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var result = await service.LoadAsync(options[destination.value].Id, token);
                if (!Current(current)) return;
                LastResult = result; description.text = result.Destination.Description;
                hotels.Show(result.Hotels, cardPrefab, RequestReview); restaurants.Show(result.Restaurants, cardPrefab, RequestReview); experiences.Show(result.Experiences, cardPrefab, RequestReview); hotspots.Show(result.Hotspots, cardPrefab, RequestReview);
                status.text = CardCount == 0 ? "No places have been added to this destination yet. Try another destination." : "Sample prices and fictional demo reviews. Browse all four categories below.";
                Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1;
                LastLoadMilliseconds = elapsed.ElapsedMilliseconds;
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (Current(current)) status.text = "Places could not be loaded. Choose Retry or another destination."; }
            finally { if (Current(current)) { IsBusy = false; retryButton.interactable = true; } }
        }
        public void Back() { Cancel(); Clear(); hubCanvas.SetActive(false); flights.Resume(); }
        private void Logout() => account.Logout();
        private void OnLoggedOut() { Cancel(); Clear(); hubCanvas.SetActive(false); options = null; service = null; }
        private void Clear()
        {
            ContentCleared?.Invoke();
            LastResult = null; LastLoadMilliseconds = 0; description.text = "";
            hotels.Clear(); restaurants.Clear(); experiences.Clear(); hotspots.Clear();
        }
        private void RequestReview(PlaceOption place) { if (IsOpen && !IsBusy && LastResult != null) ReviewRequested?.Invoke(place); }
        private CancellationToken Begin() { requestLifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken); return requestLifetime.Token; }
        private void Cancel() { revision++; requestLifetime?.Cancel(); requestLifetime?.Dispose(); requestLifetime = null; IsBusy = false; }
        private bool Current(int current) => this && revision == current && IsOpen && !string.IsNullOrEmpty(account.SignedInUserId);
        private void OnDestroy()
        {
            Cancel();
            ContentCleared?.Invoke();
            if (account) account.LoggedOut -= OnLoggedOut;
            if (destination) destination.onValueChanged.RemoveListener(SelectDestination);
            if (openButton) openButton.onClick.RemoveListener(Open); if (backButton) backButton.onClick.RemoveListener(Back); if (logoutButton) logoutButton.onClick.RemoveListener(Logout); if (retryButton) retryButton.onClick.RemoveListener(Retry);
        }
    }
}
```

## Assets/TravelPlanning/UI/LoginPage.cs

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using TravelPlanning.Accounts;
using TravelPlanning.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TravelPlanning.UI
{
    /// <summary>Connects Kevin's form to local SQLite accounts. Unity UI stays on the main thread.</summary>
    public sealed class LoginPage : MonoBehaviour
    {
        [SerializeField] private GameObject loginPanel, registrationPanel, signedInPanel;
        [SerializeField] private TMP_InputField emailField, passwordField, registrationEmail, registrationPassword, confirmationField;
        [SerializeField] private TMP_Text feedback, registrationFeedback, signedInEmail;
        [SerializeField] private UnityEngine.UI.Button submitButton, switchButton, registerButton, backButton, logoutButton, forgotButton;
        [SerializeField, HideInInspector] private string developmentDatabaseFile;
        private AccountService service;
        private TMP_InputField[] loginFields, registrationFields;
        private bool busy;
        public bool IsReady { get; private set; }
        public bool IsBusy => busy;
        public string SignedInEmail => service?.SignedInEmail;
        public string SignedInUserId => service?.SignedInUserId;
        public TravelDatabase Database { get; private set; }
        public event Action<string> LoggedIn;
        public event Action LoggedOut;

        private async void Start()
        {
            if (!loginPanel || !registrationPanel || !signedInPanel || !emailField || !passwordField || !registrationEmail || !registrationPassword || !confirmationField || !feedback || !registrationFeedback || !signedInEmail || !submitButton || !switchButton || !registerButton || !backButton || !logoutButton || !forgotButton)
            {
                Debug.LogError("LoginPage: account UI references are missing. Reopen the prepared Login scene and check AccountController.");
                enabled = false;
                if (ArgumentPresent("-authSmoke")) Application.Quit(2);
                return;
            }
            loginFields = new[] { emailField, passwordField };
            registrationFields = new[] { registrationEmail, registrationPassword, confirmationField };
            submitButton.onClick.AddListener(SubmitLogin);
            registerButton.onClick.AddListener(SubmitRegistration);
            switchButton.onClick.AddListener(OpenRegistration);
            backButton.onClick.AddListener(OpenLogin);
            logoutButton.onClick.AddListener(Logout);
            forgotButton.onClick.AddListener(ExplainPasswordRecovery);
            SetBusy(true);
            feedback.text = "Opening your local travel database...";
            try
            {
                string file = "travel.db";
                string overrideFile = Argument("-authFile") ?? developmentDatabaseFile;
                // Smoke automation must never write fixture accounts into the normal user database.
                string smokeFile = Argument("-authFile");
                if ((ArgumentPresent("-authSmoke") || ArgumentPresent("-flightSmoke") || ArgumentPresent("-destinationSmoke") || ArgumentPresent("-reviewSmoke")) && (!Debug.isDebugBuild || string.IsNullOrEmpty(smokeFile) || !smokeFile.StartsWith("auth-smoke-", StringComparison.Ordinal)))
                    throw new ArgumentException("Authentication smoke checks require an explicit isolated auth-smoke- database.");
                if (Debug.isDebugBuild && !string.IsNullOrEmpty(overrideFile))
                {
                    if (Path.GetFileName(overrideFile) != overrideFile || !overrideFile.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("The test database must be a .db file name.");
                    file = overrideFile;
                }
                var database = new TravelDatabase(Path.Combine(Application.streamingAssetsPath, "Database", "travel_seed.db"), Path.Combine(Application.persistentDataPath, file));
                await database.InitializeAsync(destroyCancellationToken);
                if (!this) return;
                Database = database;
                service = new AccountService(new AccountDatabase(database));
                IsReady = true;
                feedback.text = "";
                SetBusy(false);
                if (Debug.isDebugBuild && ArgumentPresent("-authSmoke")) await RunSmokeAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                if (!this) return;
                feedback.text = "The local database could not be opened. Your existing files have been kept. Check the database setup guide.";
                Debug.LogError("AUTH_DATABASE_UNAVAILABLE");
                if (ArgumentPresent("-authSmoke")) Application.Quit(1);
            }
        }

        private async void Submit(bool registering)
        {
            if (busy || !IsReady) return;
            var email = registering ? registrationEmail : emailField;
            var password = registering ? registrationPassword : passwordField;
            var message = registering ? registrationFeedback : feedback;
            SetBusy(true);
            message.text = "Please wait...";
            try
            {
                var result = registering
                    ? await service.RegisterAsync(email.text, password.text, confirmationField.text, destroyCancellationToken)
                    : await service.LoginAsync(email.text, password.text, destroyCancellationToken);
                if (!this) return;
                message.text = result.Message;
                if (result.Success && registering)
                {
                    emailField.text = result.Email;
                    ShowRegistration(false);
                    feedback.text = result.Message;
                }
                else if (result.Success)
                {
                    signedInEmail.text = result.Email;
                    loginPanel.SetActive(false);
                    registrationPanel.SetActive(false);
                    signedInPanel.SetActive(true);
                    LoggedIn?.Invoke(result.Email);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (this) message.text = "We could not complete that request. Please try again."; }
            finally
            {
                if (this) { ClearPasswords(); SetBusy(false); }
            }
        }

        private void ShowRegistration(bool show)
        {
            // This controller lives outside these panels, so changing screens never stops a request.
            ClearPasswords();
            registrationEmail.text = emailField.text;
            feedback.text = registrationFeedback.text = "";
            loginPanel.SetActive(!show);
            registrationPanel.SetActive(show);
            signedInPanel.SetActive(false);
        }
        public void Logout()
        {
            service?.Logout();
            signedInEmail.text = "";
            ShowRegistration(false);
            feedback.text = "You are logged out.";
            LoggedOut?.Invoke();
        }
        private void ClearPasswords() { passwordField.text = registrationPassword.text = confirmationField.text = ""; }
        private void SubmitLogin() => Submit(false);
        private void SubmitRegistration() => Submit(true);
        private void OpenRegistration() => ShowRegistration(true);
        private void OpenLogin() => ShowRegistration(false);
        private void ExplainPasswordRecovery() => feedback.text = "Password recovery is not available for this local demo. Create a new account if needed.";
        private void Update()
        {
            if (!IsReady || busy || signedInPanel.activeSelf) return;
            bool registering = registrationPanel.activeSelf;
            var fields = registering ? registrationFields : loginFields;
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                int current = Array.FindIndex(fields, field => field.isFocused);
                int direction = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1;
                int next = current < 0 ? 0 : (current + direction + fields.Length) % fields.Length;
                fields[next].Select(); fields[next].ActivateInputField();
            }
            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && Array.Exists(fields, field => field.isFocused)) Submit(registering);
        }
        private void OnDestroy()
        {
            service?.Logout();
            if (submitButton) submitButton.onClick.RemoveListener(SubmitLogin);
            if (registerButton) registerButton.onClick.RemoveListener(SubmitRegistration);
            if (switchButton) switchButton.onClick.RemoveListener(OpenRegistration);
            if (backButton) backButton.onClick.RemoveListener(OpenLogin);
            if (logoutButton) logoutButton.onClick.RemoveListener(Logout);
            if (forgotButton) forgotButton.onClick.RemoveListener(ExplainPasswordRecovery);
        }
        private void SetBusy(bool value)
        {
            busy = value;
            foreach (var button in new[] { submitButton, switchButton, registerButton, backButton, logoutButton, forgotButton }) button.interactable = !value;
            foreach (var field in new[] { emailField, passwordField, registrationEmail, registrationPassword, confirmationField }) field.interactable = !value;
        }
        private static string Argument(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
            return null;
        }
        private static bool ArgumentPresent(string key) => Array.IndexOf(Environment.GetCommandLineArgs(), key) >= 0;

        // Development-build automation drives the same controls a person uses. Never logs credentials.
        private async Task RunSmokeAsync()
        {
            try
            {
                var elapsed = System.Diagnostics.Stopwatch.StartNew();
                string email = "ui-smoke@example.test";
                string password = "Travel demo smoke phrase 2027!";
                if (ArgumentPresent("-authRegister"))
                {
                    switchButton.onClick.Invoke();
                    registrationEmail.text = email;
                    registrationPassword.text = confirmationField.text = password;
                    registerButton.onClick.Invoke();
                    await WaitForRequest();
                    if (!loginPanel.activeSelf || service.SignedInEmail != null) throw new InvalidOperationException();
                }
                emailField.text = email;
                passwordField.text = "Definitely an incorrect password";
                submitButton.onClick.Invoke();
                await WaitForRequest();
                if (service.SignedInEmail != null || !loginPanel.activeSelf) throw new InvalidOperationException();
                passwordField.text = password;
                submitButton.onClick.Invoke();
                await WaitForRequest();
                if (service.SignedInEmail != email || !signedInPanel.activeSelf || passwordField.text.Length != 0) throw new InvalidOperationException();
                logoutButton.onClick.Invoke();
                if (service.SignedInEmail != null || !loginPanel.activeSelf) throw new InvalidOperationException();
                Debug.Log("AUTH_UI_SMOKE_PASS registration=" + ArgumentPresent("-authRegister") + " elapsedMs=" + elapsed.ElapsedMilliseconds);
                Application.Quit(0);
            }
            catch (Exception) { Debug.LogError("AUTH_UI_SMOKE_FAIL"); Application.Quit(1); }
        }
        private async Task WaitForRequest()
        {
            for (int i = 0; busy && i < 3000; i++) await Task.Delay(10, destroyCancellationToken);
            if (busy) throw new TimeoutException();
        }
    }
}
```
