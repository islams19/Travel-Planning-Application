# Milestone 4 — Full scripts snapshot

Exact source snapshot for the destination hub and touched integration files. Validation results are recorded in Milestone-4-Validation.md. Current source files remain authoritative if subsequent fixes change them.

## Assets/TravelPlanning/Runtime/Destinations/DestinationModels.cs

```csharp
using System.Collections.Generic;

namespace TravelPlanning.Destinations
{
    public enum PlaceCategory { Hotel, Restaurant, Experience, Hotspot }

    /// <summary>A destination stored in the catalog. Public properties also support SQLite row mapping.</summary>
    public sealed class DestinationOption
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Country { get; set; }
        public string Region { get; set; }
        public string Description { get; set; }
    }

    /// <summary>A place with its sample-review summary. Null price means unknown; zero means free.</summary>
    public sealed class PlaceOption
    {
        public string Id { get; set; }
        public string DestinationId { get; set; }
        public PlaceCategory Category { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Address { get; set; }
        public int? PriceCents { get; set; }
        public string Currency { get; set; }
        public string GoogleMapsUrl { get; set; }
        // An unrated place has no average, rather than a misleading zero-star rating.
        public double? AverageRating { get; set; }
        public int ReviewCount { get; set; }
    }

    public sealed class DestinationHubResult
    {
        public DestinationOption Destination { get; internal set; }
        public IReadOnlyList<PlaceOption> Hotels { get; internal set; }
        public IReadOnlyList<PlaceOption> Restaurants { get; internal set; }
        public IReadOnlyList<PlaceOption> Experiences { get; internal set; }
        public IReadOnlyList<PlaceOption> Hotspots { get; internal set; }
        // For developer validation; the app does not need to show implementation details.
        public long ElapsedMilliseconds { get; internal set; }
        public int WorkerThreadId { get; internal set; }
    }
}
```

## Assets/TravelPlanning/Runtime/Destinations/DestinationHubService.cs

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SQLite;
using TravelPlanning.Data;

namespace TravelPlanning.Destinations
{
    /// <summary>Reads destination places and sample-review summaries without blocking the Unity UI.</summary>
    public sealed class DestinationHubService
    {
        private readonly TravelDatabase database;
        private const string DestinationColumns = "id AS Id, name AS Name, country AS Country, region AS Region, description AS Description";

        public DestinationHubService(TravelDatabase database)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public Task<IReadOnlyList<DestinationOption>> LoadOptionsAsync(CancellationToken cancellationToken = default)
        {
            return database.ExecuteAsync<IReadOnlyList<DestinationOption>>(connection =>
            {
                var destinations = connection.Query<DestinationOption>("SELECT " + DestinationColumns + " FROM destinations ORDER BY name, id");
                cancellationToken.ThrowIfCancellationRequested();
                return destinations.AsReadOnly();
            }, cancellationToken);
        }

        public Task<DestinationHubResult> LoadAsync(string destinationId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(destinationId))
                throw new ArgumentException("Choose a destination first.", nameof(destinationId));
            var elapsed = Stopwatch.StartNew();
            return database.ExecuteAsync(connection =>
            {
                var destination = connection.FindWithQuery<DestinationOption>(
                    "SELECT " + DestinationColumns + " FROM destinations WHERE id=?", destinationId);
                if (destination == null) throw new ArgumentException("Choose a destination from the catalog.", nameof(destinationId));
                var result = new DestinationHubResult
                {
                    Destination = destination,
                    Hotels = ReadPlaces(connection, destinationId, PlaceCategory.Hotel, cancellationToken),
                    Restaurants = ReadPlaces(connection, destinationId, PlaceCategory.Restaurant, cancellationToken),
                    Experiences = ReadPlaces(connection, destinationId, PlaceCategory.Experience, cancellationToken),
                    Hotspots = ReadPlaces(connection, destinationId, PlaceCategory.Hotspot, cancellationToken),
                    WorkerThreadId = Thread.CurrentThread.ManagedThreadId
                };
                cancellationToken.ThrowIfCancellationRequested();
                elapsed.Stop();
                result.ElapsedMilliseconds = elapsed.ElapsedMilliseconds;
                return result;
            }, cancellationToken);
        }

        private static IReadOnlyList<PlaceOption> ReadPlaces(SQLiteConnection connection, string destinationId,
            PlaceCategory category, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string table;
            string reviewColumn;
            // SQL identifiers cannot be parameters, so use only these fixed, trusted table names.
            switch (category)
            {
                case PlaceCategory.Hotel: table = "hotels"; reviewColumn = "hotel_id"; break;
                case PlaceCategory.Restaurant: table = "restaurants"; reviewColumn = "restaurant_id"; break;
                case PlaceCategory.Experience: table = "experiences"; reviewColumn = "experience_id"; break;
                case PlaceCategory.Hotspot: table = "hotspots"; reviewColumn = "hotspot_id"; break;
                default: throw new ArgumentOutOfRangeException(nameof(category));
            }
            // Separate indexed aggregates keep each place to one row, even if it has many reviews.
            string sql = "SELECT p.id AS Id, p.destination_id AS DestinationId, p.name AS Name, p.description AS Description, " +
                "p.address AS Address, p.price_cents AS PriceCents, p.currency AS Currency, p.google_maps_url AS GoogleMapsUrl, " +
                "(SELECT AVG(r.rating) FROM reviews r WHERE r." + reviewColumn + "=p.id) AS AverageRating, " +
                "(SELECT COUNT(*) FROM reviews r WHERE r." + reviewColumn + "=p.id) AS ReviewCount " +
                "FROM " + table + " p WHERE p.destination_id=? ORDER BY p.name, p.id";
            var places = connection.Query<PlaceOption>(sql, destinationId);
            token.ThrowIfCancellationRequested();
            foreach (var place in places) place.Category = category;
            return places.AsReadOnly();
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
                hotels.Show(result.Hotels, cardPrefab); restaurants.Show(result.Restaurants, cardPrefab); experiences.Show(result.Experiences, cardPrefab); hotspots.Show(result.Hotspots, cardPrefab);
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
            LastResult = null; LastLoadMilliseconds = 0; description.text = "";
            hotels.Clear(); restaurants.Clear(); experiences.Clear(); hotspots.Clear();
        }
        private CancellationToken Begin() { requestLifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken); return requestLifetime.Token; }
        private void Cancel() { revision++; requestLifetime?.Cancel(); requestLifetime?.Dispose(); requestLifetime = null; IsBusy = false; }
        private bool Current(int current) => this && revision == current && IsOpen && !string.IsNullOrEmpty(account.SignedInUserId);
        private void OnDestroy()
        {
            Cancel();
            if (account) account.LoggedOut -= OnLoggedOut;
            if (destination) destination.onValueChanged.RemoveListener(SelectDestination);
            if (openButton) openButton.onClick.RemoveListener(Open); if (backButton) backButton.onClick.RemoveListener(Back); if (logoutButton) logoutButton.onClick.RemoveListener(Logout); if (retryButton) retryButton.onClick.RemoveListener(Retry);
        }
    }
}
```

## Assets/TravelPlanning/UI/Destinations/PlaceCard.cs

```csharp
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
        public void Show(PlaceOption place)
        {
            placeName.text = place.Name;
            price.text = FormatPrice(place.PriceCents, place.Category);
            rating.text = place.AverageRating.HasValue && place.ReviewCount > 0
                ? place.AverageRating.Value.ToString("0.0", CultureInfo.InvariantCulture) + " / 5 • " + place.ReviewCount + " demo reviews" : "No reviews yet";
            description.text = place.Description;
            address.text = place.Address;
        }
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
        public void Show(IReadOnlyList<PlaceOption> places, PlaceCard prefab)
        {
            Clear(); emptyMessage.gameObject.SetActive(places.Count == 0);
            foreach (var place in places)
            {
                var card = Instantiate(prefab, items); card.Show(place); cards.Add(card.gameObject);
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

## Assets/TravelPlanning/UI/Destinations/DestinationSmokeRunner.cs

```csharp
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
        [SerializeField] private LoginPage account;
        [SerializeField] private FlightSearchPage flights;
        [SerializeField] private DestinationHubPage hub;
        [SerializeField] private GameObject authCanvas, flightCanvas, hubCanvas;
        private int frames;
        private void Update() => frames++;
        private async void Start()
        {
            string[] args = Environment.GetCommandLineArgs(); if (Array.IndexOf(args, "-destinationSmoke") < 0) return;
            try
            {
                int index = Array.IndexOf(args, "-authFile"); string file = index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
                if (!Debug.isDebugBuild || !file.StartsWith("auth-smoke-", StringComparison.Ordinal) || !file.EndsWith(".db", StringComparison.Ordinal) || System.IO.Path.GetFileName(file) != file || Array.IndexOf(args, "-authSmoke") >= 0 || Array.IndexOf(args, "-flightSmoke") >= 0) throw new InvalidOperationException("Destination smoke checks need their own isolated database and flag.");
                await RunChecksAsync(Array.IndexOf(args, "-destinationReopen") < 0);
                Debug.Log("DESTINATION_UI_SMOKE_PASS"); Application.Quit(0);
            }
            catch (Exception exception) { Debug.LogError("DESTINATION_UI_SMOKE_FAIL " + exception.GetType().Name + ": " + exception.Message); Application.Quit(1); }
        }
        public async Task RunChecksAsync(bool register)
        {
            await WaitUntil(() => account.IsReady, "Account startup");
            var right = authCanvas.transform.Find("Right Panel"); var login = right.Find("LoginCard"); var registration = right.Find("RegistrationCard"); var signedIn = right.Find("SignedInCard");
            const string email = "destination-smoke@example.test", password = "Travel destination demo phrase 2027!";
            if (register)
            {
                Click(login, "Create an account"); Input(registration, "EmailInput").text = email; Input(registration, "PasswordInput").text = Input(registration, "ConfirmationInput").text = password;
                Click(registration, "LoginButton"); await WaitUntil(() => !account.IsBusy, "Account registration"); Require(login.gameObject.activeSelf, "Registration failed.");
            }
            await Login(login, email, password); Click(signedIn, "SearchFlightsButton"); await WaitUntil(() => !flights.IsBusy && flights.LastResult != null, "Flight startup");
            var flightMain = flightCanvas.transform.Find("Main"); var hubMain = hubCanvas.transform.Find("Main");
            var max = Input(flightMain, "FilterFields/MaximumPrice/Input"); max.text = "550";
            var sort = flightMain.Find("FilterFields/Sort/Dropdown").GetComponent<TMP_Dropdown>(); sort.value = 1; Click(flightMain, "Actions/SearchButton"); await WaitUntil(() => !flights.IsBusy, "Filtered flights"); var retainedResult = flights.LastResult;
            Click(flightMain, "Header/ExploreDestinationButton"); await WaitForCity("London");
            var cities = hubMain.Find("DestinationSelection/Dropdown").GetComponent<TMP_Dropdown>(); Require(cities.options.Count == 12, "Expected all 12 destinations.");
            Require(hub.CardCount == 12 && hub.LastResult.Hotels.Count == 3 && hub.LastResult.Restaurants.Count == 3 && hub.LastResult.Experiences.Count == 3 && hub.LastResult.Hotspots.Count == 3, "Expected three cards in each category.");
            Require(hub.LastResult.WorkerThreadId != Thread.CurrentThread.ManagedThreadId, "Destination query used the UI thread.");
            Debug.Log("DESTINATION_LOAD_TIMING uiMs=" + hub.LastLoadMilliseconds + " databaseMs=" + hub.LastResult.ElapsedMilliseconds + " worker=" + hub.LastResult.WorkerThreadId + " main=" + Thread.CurrentThread.ManagedThreadId);
            Click(hubMain, "Header/BackButton"); Require(flights.IsOpen && ReferenceEquals(retainedResult, flights.LastResult) && max.text == "550" && sort.value == 1, "Back did not preserve the flight form and offers.");
            // A suspended search is retried when Back returns from the hub, even if its database work had not started.
            await WithBlockedDatabase(async () =>
            {
                Click(flightMain, "Actions/SearchButton"); Require(flights.IsBusy, "Expected a pending flight search.");
                Click(flightMain, "Header/ExploreDestinationButton"); Click(hubMain, "Header/BackButton");
                Require(flights.IsOpen && flights.IsBusy, "Interrupted flight search did not resume."); await Task.Delay(30);
            });
            await WaitUntil(() => !flights.IsBusy && flights.LastResult != null, "Resumed flight search");
            Require(max.text == "550" && sort.value == 1, "Resuming changed the filters.");
            Click(flightMain, "Header/ExploreDestinationButton"); await WaitForCity("London");
            int paris = CityIndex(cities, "Paris"), tokyo = CityIndex(cities, "Tokyo");
            cities.value = paris; await WaitForCity("Paris");
            await WithBlockedDatabase(async () =>
            {
                cities.value = tokyo; cities.value = paris; cities.value = tokyo;
                int before = frames; await Task.Delay(60); Require(hub.IsBusy && frames > before, "UI stopped updating during a pending city change.");
            });
            await WaitForCity("Tokyo"); Require(hub.CardCount == 12, "Rapid city changes duplicated or lost cards.");
            await WithBlockedDatabase(async () =>
            {
                cities.value = paris; int before = frames; await Task.Delay(60); Require(hub.IsBusy && frames > before, "Expected a responsive pending destination request.");
                Click(hubMain, "Header/LogoutButton"); Require(!hub.IsOpen && !flights.IsOpen && account.SignedInUserId == null && hub.LastResult == null && hub.CardCount == 0, "Logout did not clear both screens.");
            });
            await Task.Delay(30); Require(!hub.IsOpen && hub.LastResult == null, "A cancelled city load reappeared after logout.");
            await Login(login, email, password); Click(signedIn, "SearchFlightsButton"); await WaitUntil(() => !flights.IsBusy && flights.LastResult != null, "Flights after re-login");
            Click(flightMain, "Header/ExploreDestinationButton"); await WaitForCity("London"); Click(hubMain, "Header/LogoutButton");
        }
        private async Task WithBlockedDatabase(Func<Task> action)
        {
            using (var release = new ManualResetEventSlim(false))
            {
                var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var blocker = account.Database.ExecuteAsync(connection => { entered.SetResult(true); if (!release.Wait(10000)) throw new TimeoutException("Destination smoke gate timeout."); return true; }, CancellationToken.None);
                try { await entered.Task; await action(); } finally { release.Set(); }
                await blocker;
            }
        }
        private async Task Login(Transform login, string email, string password) { Input(login, "EmailInput").text = email; Input(login, "PasswordInput").text = password; Click(login, "LoginButton"); await WaitUntil(() => !account.IsBusy, "Account sign in"); Require(account.SignedInEmail == email, "Sign in failed."); }
        private Task WaitForCity(string name) => WaitUntil(() => !hub.IsBusy && hub.LastResult != null && hub.LastResult.Destination.Name == name, "Destination " + name);
        private static int CityIndex(TMP_Dropdown dropdown, string name) { int index = dropdown.options.FindIndex(x => x.text.StartsWith(name + ",", StringComparison.Ordinal)); Require(index >= 0, "Destination was not listed: " + name); return index; }
        private static void Click(Transform root, string path) => root.Find(path).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        private static TMP_InputField Input(Transform root, string path) => root.Find(path).GetComponent<TMP_InputField>();
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static async Task WaitUntil(Func<bool> predicate, string operation) { var watch = System.Diagnostics.Stopwatch.StartNew(); while (!predicate()) { if (watch.ElapsedMilliseconds > 45000) throw new TimeoutException(operation); await Task.Delay(10); } }
    }
}
```

## Assets/TravelPlanning/UI/Editor/DestinationHubSetup.cs

```csharp
using System;
using System.IO;
using TMPro;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Flights;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Builds the destination screen separately from the team's existing login and flight screens.</summary>
    public static class DestinationHubSetup
    {
        public const string ScenePath = AuthSetup.ScenePath;
        public const string BuildPath = "Builds/DestinationHub/TravelPlannerDestinations.exe";
        public const string PlaceCardPath = "Assets/TravelPlanning/Prefabs/Destinations/PlaceCard.prefab";
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        [MenuItem("Travel Planning/Destination Hub/1 - Prepare Destination Hub")]
        public static void Prepare()
        {
            FlightSearchSetup.Prepare();
            if (UnityEngine.Object.FindFirstObjectByType<DestinationHubPage>() != null) return;
            var account = UnityEngine.Object.FindFirstObjectByType<LoginPage>(); var flights = UnityEngine.Object.FindFirstObjectByType<FlightSearchPage>();
            var flightFields = new SerializedObject(flights); var flightCanvas = (GameObject)flightFields.FindProperty("flightCanvas").objectReferenceValue;
            var flightHeader = flightCanvas.transform.Find("Main/Header");
            var open = CloneButton(flightHeader.Find("BackButton").gameObject, flightHeader, "ExploreDestinationButton", "Explore destination", 300); open.transform.SetSiblingIndex(1);
            var canvas = new GameObject("DestinationCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster)); canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; canvas.GetComponent<Canvas>().sortingOrder = 3;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0;
            var background = Rect("Background", canvas.transform); Stretch(background); background.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.white;
            var main = Rect("Main", canvas.transform); Stretch(main); main.offsetMin = new Vector2(40, 32); main.offsetMax = new Vector2(-40, -32); Vertical(main, 16);
            var header = Horizontal("Header", main, 68); var title = Label(header, "Title", "Explore your destination", 42); Layout(title.gameObject, -1, 1);
            var back = CloneButton(flightHeader.Find("BackButton").gameObject, header, "BackButton", "Back to flights", 250);
            var logout = CloneButton(flightHeader.Find("LogoutButton").gameObject, header, "LogoutButton", "Log out", 160);
            var selection = Horizontal("DestinationSelection", main, 60); var label = Label(selection, "Label", "Destination", 26); Layout(label.gameObject, -1, 0, 180);
            var dropdownObject = UnityEngine.Object.Instantiate(flightCanvas.transform.Find("Main/RouteFields/Destination/Dropdown").gameObject, selection); dropdownObject.name = "Dropdown"; Layout(dropdownObject, 54, 1); var dropdown = dropdownObject.GetComponent<TMP_Dropdown>(); dropdown.ClearOptions();
            var retry = CloneButton(flightHeader.Find("BackButton").gameObject, selection, "RetryButton", "Retry", 160);
            var description = Label(main, "Description", "", 24); Layout(description.gameObject, 64);
            var note = Label(main, "DemoNote", "USD sample prices • Fictional demo reviews • Public hotspots are free; optional services are excluded", 22); Layout(note.gameObject, 34);
            var status = Label(main, "Status", "", 22); Layout(status.gameObject, 46);
            var scrollObject = Rect("ScrollView", main); Layout(scrollObject.gameObject, -1, 1); var scroll = scrollObject.gameObject.AddComponent<UnityEngine.UI.ScrollRect>(); scroll.horizontal = false; scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 45;
            var viewport = Rect("Viewport", scrollObject); Stretch(viewport); viewport.gameObject.AddComponent<UnityEngine.UI.Image>(); viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var content = Rect("Content", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero; Vertical(content, 28); content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize; scroll.viewport = viewport; scroll.content = content;
            var hotels = Section(content, "Hotels", "Hotels — sample room rates per night"); var restaurants = Section(content, "Restaurants", "Restaurants — sample meal budgets per person"); var experiences = Section(content, "Experiences", "Experiences — sample activities per adult"); var hotspots = Section(content, "Hotspots", "Hotspots — public places to explore");
            var controller = new GameObject("DestinationController").AddComponent<DestinationHubPage>(); var fields = new SerializedObject(controller);
            Set(fields, "account", account); Set(fields, "flights", flights); Set(fields, "hubCanvas", canvas); Set(fields, "destination", dropdown); Set(fields, "description", description); Set(fields, "status", status); Set(fields, "openButton", open); Set(fields, "backButton", back); Set(fields, "logoutButton", logout); Set(fields, "retryButton", retry); Set(fields, "scroll", scroll); Set(fields, "hotels", hotels); Set(fields, "restaurants", restaurants); Set(fields, "experiences", experiences); Set(fields, "hotspots", hotspots); Set(fields, "cardPrefab", CreateCard()); fields.ApplyModifiedPropertiesWithoutUndo();
            var runner = controller.gameObject.AddComponent<DestinationSmokeRunner>(); var runnerFields = new SerializedObject(runner); Set(runnerFields, "account", account); Set(runnerFields, "flights", flights); Set(runnerFields, "hub", controller); Set(runnerFields, "authCanvas", GameObject.Find("Canvas")); Set(runnerFields, "flightCanvas", flightCanvas); Set(runnerFields, "hubCanvas", canvas); runnerFields.ApplyModifiedPropertiesWithoutUndo();
            canvas.SetActive(false); EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); Debug.Log("DESTINATION_HUB_SCENE_READY " + ScenePath);
        }
        [MenuItem("Travel Planning/Destination Hub/2 - Build Windows x64")]
        public static void BuildWindows()
        {
            Prepare(); Directory.CreateDirectory(Path.GetDirectoryName(BuildPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = BuildPath, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Destination hub build failed.");
            var notices = Path.Combine(Path.GetDirectoryName(BuildPath), "ThirdPartyNotices"); Directory.CreateDirectory(notices);
            foreach (string file in Directory.GetFiles("Assets/Plugins/SQLite/Licenses")) if (!file.EndsWith(".meta")) File.Copy(file, Path.Combine(notices, Path.GetFileName(file)), true);
            File.Copy("Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt", Path.Combine(notices, "LiberationSans-OFL.txt"), true); File.Copy("Assets/Plugins/LiteDB/LICENSE.txt", Path.Combine(notices, "LiteDB-LICENSE.txt"), true);
            Debug.Log("DESTINATION_HUB_BUILD_PASS " + Path.GetFullPath(BuildPath));
        }
        private static PlaceSection Section(Transform parent, string name, string title)
        {
            var root = Rect(name, parent); Vertical(root, 12);
            var heading = Label(root, "Heading", title, 30); Layout(heading.gameObject, 46);
            var empty = Label(root, "EmptyMessage", "No " + name.ToLowerInvariant() + " have been added here yet.", 24); Layout(empty.gameObject, 44);
            var items = Rect("Items", root); Vertical(items, 14);
            var section = root.gameObject.AddComponent<PlaceSection>(); var fields = new SerializedObject(section); Set(fields, "emptyMessage", empty); Set(fields, "items", items); fields.ApplyModifiedPropertiesWithoutUndo(); empty.gameObject.SetActive(false); return section;
        }
        private static PlaceCard CreateCard()
        {
            if (File.Exists(PlaceCardPath)) return AssetDatabase.LoadAssetAtPath<GameObject>(PlaceCardPath).GetComponent<PlaceCard>();
            Directory.CreateDirectory(Path.GetDirectoryName(PlaceCardPath)); var root = Rect("PlaceCard", null); root.sizeDelta = new Vector2(1800, 232); root.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(244, 247, 255, 255); Layout(root.gameObject, 232); var layout = Vertical(root, 8); layout.padding = new RectOffset(20, 20, 16, 16);
            var top = Horizontal("Top", root, 40); var name = Label(top, "PlaceName", "Place", 28); Layout(name.gameObject, -1, 1); var price = Label(top, "Price", "", 26); Layout(price.gameObject, -1, 0, 520); price.alignment = TextAlignmentOptions.Right;
            var rating = Label(root, "Rating", "", 22); Layout(rating.gameObject, 28); var description = Label(root, "Description", "", 22); Layout(description.gameObject, 64); var address = Label(root, "Address", "", 22); Layout(address.gameObject, 34);
            var card = root.gameObject.AddComponent<PlaceCard>(); var fields = new SerializedObject(card); Set(fields, "placeName", name); Set(fields, "price", price); Set(fields, "rating", rating); Set(fields, "description", description); Set(fields, "address", address); fields.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PlaceCardPath); UnityEngine.Object.DestroyImmediate(root.gameObject); return prefab.GetComponent<PlaceCard>();
        }
        private static UnityEngine.UI.Button CloneButton(GameObject template, Transform parent, string name, string label, float width) { var obj = UnityEngine.Object.Instantiate(template, parent); obj.name = name; obj.GetComponentInChildren<TMP_Text>().text = label; Layout(obj, 56, 0, width); return obj.GetComponent<UnityEngine.UI.Button>(); }
        private static RectTransform Horizontal(string name, Transform parent, float height) { var rect = Rect(name, parent); Layout(rect.gameObject, height); var layout = rect.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>(); layout.spacing = 20; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = true; return rect; }
        private static UnityEngine.UI.VerticalLayoutGroup Vertical(RectTransform rect, int spacing) { var layout = rect.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(); layout.spacing = spacing; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false; return layout; }
        private static TMP_Text Label(Transform parent, string name, string text, int size) { var rect = Rect(name, parent); var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = Font; label.text = text; label.fontSize = size; label.color = new Color32(51, 51, 51, 255); label.raycastTarget = false; return label; }
        private static RectTransform Rect(string name, Transform parent) { var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false); return obj.GetComponent<RectTransform>(); }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Layout(GameObject obj, float height = -1, float flexible = 0, float width = -1) { var layout = obj.GetComponent<UnityEngine.UI.LayoutElement>() ?? obj.AddComponent<UnityEngine.UI.LayoutElement>(); layout.preferredHeight = height; layout.flexibleHeight = flexible; layout.flexibleWidth = flexible; layout.preferredWidth = width; }
        private static void Set(SerializedObject fields, string name, UnityEngine.Object value) => fields.FindProperty(name).objectReferenceValue = value;
    }
}
```

## Assets/TravelPlanning/Tests/EditMode/DestinationHubTests.cs

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TravelPlanning.Data;
using TravelPlanning.Destinations;
using TravelPlanning.Flights;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    /// <summary>Checks the real seeded SQLite catalog using isolated temporary copies.</summary>
    public sealed class DestinationHubTests
    {
        private string folder;
        private string path;
        private TravelDatabase database;
        private DestinationHubService service;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "TravelDestinationTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, "travel.db");
            database = new TravelDatabase(Path.Combine(Application.streamingAssetsPath, "Database", "travel_seed.db"), path);
            service = new DestinationHubService(database);
        }

        [TearDown]
        public void TearDown() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }

        [UnityTest]
        public IEnumerator AllTwelveDestinationsHaveCorrectPlacesPricesAndSampleRatingsOffMainThread()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            var options = service.LoadOptionsAsync();
            yield return Wait(options);
            Assert.That(options.Result.Count, Is.EqualTo(12));
            foreach (var destination in options.Result)
            {
                var load = service.LoadAsync(destination.Id);
                yield return Wait(load);
                var hub = load.Result;
                Assert.That(hub.Destination.Id, Is.EqualTo(destination.Id));
                Assert.That(hub.Destination.Name, Is.EqualTo(destination.Name));
                Assert.That(hub.Destination.Country, Is.Not.Empty);
                Assert.That(hub.Destination.Region, Is.Not.Empty);
                Assert.That(hub.Destination.Description, Is.Not.Empty);
                CheckCategory(hub.Hotels, destination.Id, PlaceCategory.Hotel, new[] { 16000, 24500, 33000 });
                CheckCategory(hub.Restaurants, destination.Id, PlaceCategory.Restaurant, new[] { 2500, 4700, 6900 });
                CheckCategory(hub.Experiences, destination.Id, PlaceCategory.Experience, new[] { 3000, 4800, 6600 });
                CheckCategory(hub.Hotspots, destination.Id, PlaceCategory.Hotspot, new[] { 0, 0, 0 });
                Assert.That(hub.WorkerThreadId, Is.Not.EqualTo(mainThread));
                Assert.That(hub.ElapsedMilliseconds, Is.LessThan(3000));
            }
        }

        [UnityTest]
        public IEnumerator UnknownPriceFreePlaceNoReviewsAndEmptyCategoryStayDistinct()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(database.ExecuteAsync(connection =>
            {
                connection.RunInTransaction(() =>
                {
                    connection.Execute("UPDATE restaurants SET price_cents=NULL WHERE id=?", "london-restaurant-1");
                    connection.Execute("UPDATE restaurants SET price_cents=0 WHERE id=?", "london-restaurant-2");
                    connection.Execute("DELETE FROM reviews WHERE restaurant_id=?", "london-restaurant-1");
                    connection.Execute("DELETE FROM reviews WHERE hotspot_id IN (SELECT id FROM hotspots WHERE destination_id=?)", "london");
                    connection.Execute("DELETE FROM hotspots WHERE destination_id=?", "london");
                });
                return 0;
            }, CancellationToken.None));
            var load = service.LoadAsync("london");
            yield return Wait(load);
            var unknown = load.Result.Restaurants.Single(place => place.Id == "london-restaurant-1");
            var free = load.Result.Restaurants.Single(place => place.Id == "london-restaurant-2");
            Assert.That(unknown.PriceCents, Is.Null);
            Assert.That(unknown.AverageRating, Is.Null);
            Assert.That(unknown.ReviewCount, Is.Zero);
            Assert.That(free.PriceCents, Is.EqualTo(0));
            Assert.That(free.AverageRating, Is.EqualTo(4.0));
            Assert.That(free.ReviewCount, Is.EqualTo(2));
            Assert.That(load.Result.Hotspots, Is.Empty);
            Assert.That(load.Result.Hotels.Count, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator UnknownAndSqlInjectionIdsAreRejected()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            foreach (string id in new[] { "unknown", "london' OR 1=1 --", "london'; DELETE FROM destinations; --", "LONDON" })
            {
                var load = service.LoadAsync(id);
                yield return Completion(load);
                Assert.That(load.IsFaulted, Is.True);
                Assert.That(load.Exception.GetBaseException(), Is.InstanceOf<ArgumentException>());
            }
            Assert.Throws<ArgumentException>(() => service.LoadAsync(null));
            Assert.Throws<ArgumentException>(() => service.LoadAsync(""));
            Assert.Throws<ArgumentException>(() => service.LoadAsync("  "));
            var options = service.LoadOptionsAsync();
            yield return Wait(options);
            Assert.That(options.Result.Count, Is.EqualTo(12));
        }

        [UnityTest]
        public IEnumerator CancellationWhileWaitingForDatabaseStopsHubLoad()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            using (var release = new ManualResetEventSlim(false))
            using (var entered = new ManualResetEventSlim(false))
            using (var cancellation = new CancellationTokenSource())
            {
                var blocker = database.ExecuteAsync(connection => { entered.Set(); return release.Wait(TimeSpan.FromSeconds(15)); }, CancellationToken.None);
                while (!entered.IsSet) yield return null;
                Task<DestinationHubResult> pending;
                Task<IReadOnlyList<DestinationOption>> options;
                try
                {
                    pending = service.LoadAsync("london", cancellation.Token);
                    options = service.LoadOptionsAsync(cancellation.Token);
                    cancellation.Cancel();
                }
                finally { release.Set(); }
                yield return Wait(blocker);
                yield return Completion(pending);
                yield return Completion(options);
                Assert.That(pending.IsCanceled, Is.True);
                Assert.That(options.IsCanceled, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator BrowsingPreservesExistingUserTripAndAllDatabaseBytes()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(database.ExecuteAsync(connection =>
            {
                connection.Execute("INSERT INTO users (id,email_normalized,password_hash,password_salt,password_iterations,created_utc) VALUES (?,?,?,?,?,?)", "owner", "owner@example.com", "testfixture", "testfixture", 600000, "2026-09-29T00:00:00Z");
                connection.Execute("INSERT INTO trips (id,user_id,name,start_date,end_date,created_utc,updated_utc) VALUES (?,?,?,?,?,?,?)", "trip", "owner", "Keep my trip", "2027-06-15", "2027-06-22", "2026-09-29T00:00:00Z", "2026-09-29T00:00:00Z");
                return 0;
            }, CancellationToken.None));
            byte[] before = Hash();
            var options = service.LoadOptionsAsync();
            yield return Wait(options);
            foreach (var destination in options.Result) yield return Wait(service.LoadAsync(destination.Id));
            Assert.That(Hash(), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator NewDestinationNeedsOnlyDataAndAirportOptionsExposeDestinationId()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(database.ExecuteAsync(connection =>
            {
                connection.Execute("INSERT INTO destinations (id,name,country,region,description) VALUES (?,?,?,?,?)", "extra", "Extra City", "Example", "Demo", "New destination fixture");
                connection.Execute("INSERT INTO airports (id,destination_id,name,time_zone) VALUES (?,?,?,?)", "EXT", "extra", "Extra Airport", "UTC");
                return 0;
            }, CancellationToken.None));
            var options = service.LoadOptionsAsync();
            yield return Wait(options);
            Assert.That(options.Result.Count, Is.EqualTo(13));
            var hub = service.LoadAsync("extra");
            yield return Wait(hub);
            Assert.That(hub.Result.Destination.Name, Is.EqualTo("Extra City"));
            Assert.That(hub.Result.Hotels, Is.Empty);
            Assert.That(hub.Result.Restaurants, Is.Empty);
            Assert.That(hub.Result.Experiences, Is.Empty);
            Assert.That(hub.Result.Hotspots, Is.Empty);
            var flightOptions = new FlightSearchService(database).LoadOptionsAsync();
            yield return Wait(flightOptions);
            Assert.That(flightOptions.Result.Airports.Single(airport => airport.Id == "LHR").DestinationId, Is.EqualTo("london"));
            Assert.That(flightOptions.Result.Airports.Single(airport => airport.Id == "EXT").DestinationId, Is.EqualTo("extra"));
        }

        private static void CheckCategory(IReadOnlyList<PlaceOption> places, string destinationId, PlaceCategory category, int[] prices)
        {
            Assert.That(places.Count, Is.EqualTo(3));
            Assert.That(places.Select(place => place.Id).Distinct().Count(), Is.EqualTo(3));
            var orderedById = places.OrderBy(place => place.Id, StringComparer.Ordinal).ToArray();
            for (int index = 0; index < orderedById.Length; index++)
            {
                var place = orderedById[index];
                Assert.That(place.DestinationId, Is.EqualTo(destinationId));
                Assert.That(place.Category, Is.EqualTo(category));
                Assert.That(place.PriceCents, Is.EqualTo(prices[index]));
                Assert.That(place.Currency, Is.EqualTo("USD"));
                Assert.That(place.AverageRating, Is.EqualTo(4.5 - index * 0.5));
                Assert.That(place.ReviewCount, Is.EqualTo(2));
                Assert.That(place.Name, Is.Not.Empty);
                Assert.That(place.Description, Is.Not.Empty);
                Assert.That(place.Address, Is.Not.Empty);
                Assert.That(place.GoogleMapsUrl, Does.StartWith("https://www.google.com/maps/search/"));
            }
        }

        private byte[] Hash() { using (var hash = SHA256.Create()) return hash.ComputeHash(File.ReadAllBytes(path)); }
        private static IEnumerator Wait(Task task) { yield return Completion(task); task.GetAwaiter().GetResult(); }
        private static IEnumerator Completion(Task task)
        {
            double deadline = UnityEditor.EditorApplication.timeSinceStartup + 30;
            while (!task.IsCompleted)
            {
                Assert.That(UnityEditor.EditorApplication.timeSinceStartup, Is.LessThan(deadline), "Destination operation timed out.");
                yield return null;
            }
        }
    }
}
```

## Assets/TravelPlanning/Tests/EditMode/DestinationHubSceneTests.cs

```csharp
using System.Collections;
using NUnit.Framework;
using TravelPlanning.Destinations;
using TravelPlanning.UI;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class DestinationHubSceneTests
    {
        [TestCase(null, PlaceCategory.Hotel, "Price unavailable")]
        [TestCase(0, PlaceCategory.Hotspot, "Free")]
        [TestCase(12345, PlaceCategory.Hotel, "USD 123.45 / room / night")]
        [TestCase(2500, PlaceCategory.Restaurant, "USD 25.00 / person (meal)")]
        [TestCase(5000, PlaceCategory.Experience, "USD 50.00 / adult")]
        public void PriceLabelsExplainDemoUnits(int? cents, PlaceCategory category, string expected) => Assert.That(PlaceCard.FormatPrice(cents, category), Is.EqualTo(expected));
        [Test]
        public void DestinationScreenReferencesScrollHierarchyAndCardPrefabAreComplete()
        {
            EditorSceneManager.OpenScene(DestinationHubSetup.ScenePath);
            var hub = Object.FindFirstObjectByType<DestinationHubPage>(); Assert.That(hub.transform.parent, Is.Null);
            var fields = new SerializedObject(hub);
            foreach (string name in new[] { "account", "flights", "hubCanvas", "destination", "description", "status", "openButton", "backButton", "logoutButton", "retryButton", "scroll", "hotels", "restaurants", "experiences", "hotspots", "cardPrefab" }) Assert.That(fields.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
            var canvas = (GameObject)fields.FindProperty("hubCanvas").objectReferenceValue; Assert.That(canvas.activeSelf, Is.False);
            var scroll = (UnityEngine.UI.ScrollRect)fields.FindProperty("scroll").objectReferenceValue;
            Assert.That(scroll.content.parent, Is.EqualTo(scroll.viewport)); Assert.That(scroll.viewport.GetComponent<UnityEngine.UI.Mask>(), Is.Not.Null);
            Assert.That(scroll.content.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit, Is.EqualTo(UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize));
            Assert.That(canvas.GetComponentsInChildren<PlaceSection>(true).Length, Is.EqualTo(4));
            var card = new SerializedObject(fields.FindProperty("cardPrefab").objectReferenceValue);
            foreach (string name in new[] { "placeName", "price", "rating", "description", "address" }) Assert.That(card.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
            canvas.SetActive(true); Canvas.ForceUpdateCanvases(); Assert.That(scroll.viewport.rect.width, Is.GreaterThan(200)); Assert.That(scroll.viewport.rect.height, Is.GreaterThan(100));
            Assert.That(GameObject.Find("Canvas/Right Panel/LoginCard/EmailInput").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(0, -230)));
        }
        [UnityTest]
        public IEnumerator ActualControlsExploreChangeCitiesPreserveFlightsAndCancelOnLogout()
        {
            EditorSceneManager.OpenScene(DestinationHubSetup.ScenePath);
            var configuration = new SerializedObject(Object.FindFirstObjectByType<LoginPage>());
            configuration.FindProperty("developmentDatabaseFile").stringValue = "destination-editor-" + System.Guid.NewGuid().ToString("N") + ".db";
            configuration.ApplyModifiedPropertiesWithoutUndo(); yield return new EnterPlayMode();
            var account = Object.FindFirstObjectByType<LoginPage>(); string file = new SerializedObject(account).FindProperty("developmentDatabaseFile").stringValue;
            Assert.That(file, Does.StartWith("destination-editor-")); Assert.That(System.IO.Path.GetFileName(file), Is.EqualTo(file));
            var task = Object.FindFirstObjectByType<DestinationSmokeRunner>().RunChecksAsync(true); float deadline = Time.realtimeSinceStartup + 90;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(task.IsCompleted, Is.True, "Destination UI flow timed out."); if (task.IsFaulted) Assert.Fail(task.Exception.GetBaseException().ToString()); Assert.That(task.IsCanceled, Is.False);
            string path = System.IO.Path.Combine(Application.persistentDataPath, file); Assert.That(System.IO.File.Exists(path), Is.True); System.IO.File.Delete(path);
            yield return new ExitPlayMode();
        }
    }
}
```

## tools/Test-DestinationHub.ps1

```powershell
param([string]$BuildRoot = 'Builds/DestinationHub')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $BuildRoot).Path
$exe = Join-Path $root 'TravelPlannerDestinations.exe'
$id = [Guid]::NewGuid().ToString('N')
$file = "auth-smoke-$id.db"
$logs = Join-Path (Get-Location).Path "Logs/DestinationHub/$id"
New-Item -ItemType Directory -Path $logs -Force | Out-Null
foreach ($phase in @('register-explore','reopen-explore')) {
    $log = Join-Path $logs "$phase.log"
    $arguments = @('-batchmode','-nographics','-destinationSmoke','-authFile',$file,'-logFile',('"' + $log + '"'))
    if ($phase -eq 'reopen-explore') { $arguments += '-destinationReopen' }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(120000)) { $process.Kill(); throw "Destination $phase timed out. See $log" }
    $process.Refresh()
    $output = Get-Content -LiteralPath $log -Raw
    if ($process.ExitCode -ne 0 -or $output -notmatch 'DESTINATION_UI_SMOKE_PASS') { throw "Destination $phase failed. See $log" }
    Write-Output "PASS $phase - real UI destination, navigation, cancellation and recovery checks. Log: $log"
    Select-String -LiteralPath $log -Pattern 'DESTINATION_LOAD_TIMING' | ForEach-Object { $_.Line }
}
```

## Assets/TravelPlanning/Runtime/Flights/FlightModels.cs

```csharp
using System;
using System.Collections.Generic;

namespace TravelPlanning.Flights
{
    public enum FlightSort { PriceAscending, PriceDescending, DepartureEarliest, DepartureLatest, AirlineName }
    public enum DepartureTimeBand { Any, Night, Morning, Afternoon, Evening }

    /// <summary>The form values. SearchAsync takes a copy so editing the form cannot change a running search.</summary>
    public sealed class FlightSearchRequest
    {
        public string OriginAirportId { get; set; }
        public string DestinationAirportId { get; set; }
        public string DepartureDate { get; set; }
        public string ReturnDate { get; set; }
        public int? MaxPriceCents { get; set; }
        public string AirlineId { get; set; }
        public FlightSort Sort { get; set; }
        public DepartureTimeBand TimeBand { get; set; }

        internal FlightSearchRequest Snapshot() => (FlightSearchRequest)MemberwiseClone();
    }

    public sealed class FlightSearchOptions
    {
        public IReadOnlyList<AirportOption> Airports { get; internal set; }
        public IReadOnlyList<AirlineOption> Airlines { get; internal set; }
        public string StartDate { get; internal set; }
        public string EndDate { get; internal set; }
    }

    public sealed class FlightSearchResult
    {
        public IReadOnlyList<FlightOption> Outbound { get; internal set; }
        public IReadOnlyList<FlightOption> Return { get; internal set; }
        // Diagnostics for tests and logs; these are not labels intended for the app screen.
        public long ElapsedMilliseconds { get; internal set; }
        public int WorkerThreadId { get; internal set; }
    }

    public sealed class AirportOption
    {
        public string Id { get; set; }
        public string DestinationId { get; set; }
        public string City { get; set; }
        public string Name { get; set; }
        public string TimeZoneId { get; set; }
    }

    public sealed class AirlineOption
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    /// <summary>One flight and its one-way USD fare. Local times belong to the corresponding airport.</summary>
    public sealed class FlightOption
    {
        public string Id { get; internal set; }
        public string AirlineId { get; internal set; }
        public string AirlineName { get; internal set; }
        public string FlightNumber { get; internal set; }
        public string OriginAirportId { get; internal set; }
        public string DestinationAirportId { get; internal set; }
        public string OriginCity { get; internal set; }
        public string DestinationCity { get; internal set; }
        public string OriginTimeZoneId { get; internal set; }
        public string DestinationTimeZoneId { get; internal set; }
        public DateTime DepartureUtc { get; internal set; }
        public DateTime ArrivalUtc { get; internal set; }
        public DateTime DepartureLocal { get; internal set; }
        public DateTime ArrivalLocal { get; internal set; }
        public int PriceCents { get; internal set; }
        public int AvailableSeats { get; internal set; }
        public string Currency { get; internal set; }
    }
}

```

## Assets/TravelPlanning/Runtime/Flights/FlightSearchService.cs

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SQLite;
using TravelPlanning.Data;

namespace TravelPlanning.Flights
{
    /// <summary>Reads the offline catalog on a worker thread. Search never changes the database.</summary>
    public sealed class FlightSearchService
    {
        private readonly TravelDatabase database;
        public FlightSearchService(TravelDatabase database)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public Task<FlightSearchOptions> LoadOptionsAsync(CancellationToken cancellationToken = default)
        {
            return database.ExecuteAsync(connection =>
            {
                var options = LoadOptions(connection);
                cancellationToken.ThrowIfCancellationRequested();
                return options;
            }, cancellationToken);
        }

        public Task<FlightSearchResult> SearchAsync(FlightSearchRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentException("Choose the route and travel dates first.", nameof(request));
            var snapshot = request.Snapshot();
            var elapsed = Stopwatch.StartNew();
            return database.ExecuteAsync(connection =>
            {
                Validate(snapshot, LoadOptions(connection));
                var zones = new Dictionary<string, TimeZoneInfo>(StringComparer.Ordinal);
                var outbound = FindLeg(connection, snapshot, false, zones, cancellationToken);
                var returning = FindLeg(connection, snapshot, true, zones, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                elapsed.Stop();
                return new FlightSearchResult
                {
                    Outbound = outbound.AsReadOnly(), Return = returning.AsReadOnly(),
                    ElapsedMilliseconds = elapsed.ElapsedMilliseconds,
                    WorkerThreadId = Thread.CurrentThread.ManagedThreadId
                };
            }, cancellationToken);
        }

        /// <summary>Developer diagnostic: asks SQLite to explain the actual outbound search SQL.</summary>
        public Task<IReadOnlyList<string>> ExplainOutboundQueryAsync(FlightSearchRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentException("Choose the route and travel dates first.", nameof(request));
            var snapshot = request.Snapshot();
            return database.ExecuteAsync<IReadOnlyList<string>>(connection =>
            {
                Validate(snapshot, LoadOptions(connection));
                string sql = FlightSearchQuery.Build(snapshot, false, out var arguments);
                var details = connection.Query<FlightQueryPlanRow>("EXPLAIN QUERY PLAN " + sql, arguments).Select(row => row.Detail).ToList();
                cancellationToken.ThrowIfCancellationRequested();
                return details.AsReadOnly();
            }, cancellationToken);
        }

        private static FlightSearchOptions LoadOptions(SQLiteConnection connection)
        {
            var airports = connection.Query<AirportOption>("SELECT a.id AS Id, a.destination_id AS DestinationId, d.name AS City, a.name AS Name, a.time_zone AS TimeZoneId FROM airports a JOIN destinations d ON d.id=a.destination_id ORDER BY d.name, a.id");
            var airlines = connection.Query<AirlineOption>("SELECT id AS Id, name AS Name FROM airlines ORDER BY name, id");
            string start = connection.ExecuteScalar<string>("SELECT value FROM metadata WHERE key=?", "seed_start_date");
            string end = connection.ExecuteScalar<string>("SELECT value FROM metadata WHERE key=?", "seed_end_date");
            if (!TryDate(start, out var first) || !TryDate(end, out var last) || last < first)
                throw new InvalidOperationException("The travel catalog has missing or invalid date information.");
            if (airports.Count == 0 || airlines.Count == 0)
                throw new InvalidOperationException("The travel catalog is missing airports or airlines.");
            return new FlightSearchOptions { Airports = airports.AsReadOnly(), Airlines = airlines.AsReadOnly(), StartDate = start, EndDate = end };
        }

        private static void Validate(FlightSearchRequest request, FlightSearchOptions options)
        {
            if (!options.Airports.Any(airport => airport.Id == request.OriginAirportId))
                throw new ArgumentException("Choose a valid origin airport.");
            if (!options.Airports.Any(airport => airport.Id == request.DestinationAirportId))
                throw new ArgumentException("Choose a valid destination airport.");
            if (request.OriginAirportId == request.DestinationAirportId)
                throw new ArgumentException("Choose different origin and destination airports.");
            if (!TryDate(request.DepartureDate, out var departure) || !TryDate(request.ReturnDate, out var returning))
                throw new ArgumentException("Enter both dates as YYYY-MM-DD.");
            if (returning < departure)
                throw new ArgumentException("The return date must be on or after the departure date.");
            TryDate(options.StartDate, out var first);
            TryDate(options.EndDate, out var last);
            if (departure < first || returning > last)
                throw new ArgumentException("Choose dates from " + options.StartDate + " through " + options.EndDate + ".");
            if (request.MaxPriceCents.HasValue && request.MaxPriceCents.Value < 0)
                throw new ArgumentException("The maximum price cannot be negative.");
            if (request.AirlineId != null && !options.Airlines.Any(airline => airline.Id == request.AirlineId))
                throw new ArgumentException("Choose a valid airline, or all airlines.");
            if (!Enum.IsDefined(typeof(FlightSort), request.Sort) || !Enum.IsDefined(typeof(DepartureTimeBand), request.TimeBand))
                throw new ArgumentException("Choose a valid sorting and departure-time option.");
        }

        private static bool TryDate(string value, out DateTime date) => DateTime.TryParseExact(value, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

        private static List<FlightOption> FindLeg(SQLiteConnection connection, FlightSearchRequest request, bool returning,
            Dictionary<string, TimeZoneInfo> zones, CancellationToken token)
        {
            string sql = FlightSearchQuery.Build(request, returning, out var arguments);
            var rows = connection.Query<FlightRow>(sql, arguments);
            token.ThrowIfCancellationRequested();
            var flights = new List<FlightOption>();
            foreach (var row in rows)
            {
                token.ThrowIfCancellationRequested();
                var flight = FlightSearchQuery.ConvertRow(row, zones);
                // Every leg uses its departure airport's local clock, including the return leg.
                if (MatchesTime(flight.DepartureLocal.Hour, request.TimeBand)) flights.Add(flight);
            }
            flights.Sort((left, right) => Compare(left, right, request.Sort));
            return flights;
        }

        private static bool MatchesTime(int hour, DepartureTimeBand band)
        {
            switch (band)
            {
                case DepartureTimeBand.Night: return hour < 6;
                case DepartureTimeBand.Morning: return hour >= 6 && hour < 12;
                case DepartureTimeBand.Afternoon: return hour >= 12 && hour < 18;
                case DepartureTimeBand.Evening: return hour >= 18;
                default: return true;
            }
        }

        private static int Compare(FlightOption left, FlightOption right, FlightSort sort)
        {
            int result;
            switch (sort)
            {
                case FlightSort.PriceDescending: result = right.PriceCents.CompareTo(left.PriceCents); break;
                case FlightSort.DepartureEarliest: result = left.DepartureLocal.CompareTo(right.DepartureLocal); break;
                case FlightSort.DepartureLatest: result = right.DepartureLocal.CompareTo(left.DepartureLocal); break;
                case FlightSort.AirlineName: result = StringComparer.OrdinalIgnoreCase.Compare(left.AirlineName, right.AirlineName); break;
                default: result = left.PriceCents.CompareTo(right.PriceCents); break;
            }
            // Stable tie-breaking makes repeated searches display the same order.
            return result != 0 ? result : StringComparer.Ordinal.Compare(left.Id, right.Id);
        }
    }
}

```

## Assets/TravelPlanning/UI/Flights/FlightSearchPage.cs

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using TravelPlanning.Flights;
using UnityEngine;

namespace TravelPlanning.UI.Flights
{
    /// <summary>Reads search controls, awaits the database, and displays each direction separately.</summary>
    public sealed class FlightSearchPage : MonoBehaviour
    {
        [SerializeField] private LoginPage account;
        [SerializeField] private GameObject authCanvas, flightCanvas;
        [SerializeField] private TMP_Dropdown origin, destination, departureDate, returnDate, airline, timeBand, sort;
        [SerializeField] private TMP_InputField maximumPrice;
        [SerializeField] private TMP_Text status, outboundHeading, returnHeading;
        [SerializeField] private UnityEngine.UI.Button openButton, searchButton, clearButton, backButton, logoutButton;
        [SerializeField] private RectTransform outboundContent, returnContent;
        [SerializeField] private UnityEngine.UI.ScrollRect outboundScroll, returnScroll;
        [SerializeField] private FlightResultRow rowPrefab;
        private FlightSearchService service;
        private FlightSearchOptions options;
        private CancellationTokenSource requestLifetime;
        private int revision;
        private bool resumeInterruptedSearch;
        private readonly List<GameObject> rows = new List<GameObject>();
        public bool IsBusy { get; private set; }
        public bool IsOpen => flightCanvas && flightCanvas.activeSelf;
        public FlightSearchResult LastResult { get; private set; }
        public long LastSearchMilliseconds { get; private set; }
        public string Status => status.text;
        public string SelectedDestinationId => options != null && destination.value >= 0 && destination.value < options.Airports.Count
            ? options.Airports[destination.value].DestinationId : null;

        private void Start()
        {
            if (!account || !authCanvas || !flightCanvas || !origin || !destination || !departureDate || !returnDate || !airline || !timeBand || !sort || !maximumPrice || !status || !outboundHeading || !returnHeading || !openButton || !searchButton || !clearButton || !backButton || !logoutButton || !outboundContent || !returnContent || !outboundScroll || !returnScroll || !rowPrefab)
            {
                Debug.LogError("FlightSearchPage: assign all references on FlightController using the prepared scene."); enabled = false; return;
            }
            openButton.onClick.AddListener(Open);
            searchButton.onClick.AddListener(Search);
            clearButton.onClick.AddListener(ClearFilters);
            backButton.onClick.AddListener(Close);
            logoutButton.onClick.AddListener(Logout);
            account.LoggedOut += OnLoggedOut;
            account.LoggedIn += OnLoggedIn;
        }
        public async void Open()
        {
            if (!account.IsReady || string.IsNullOrEmpty(account.SignedInUserId)) return;
            CancelRequest();
            flightCanvas.SetActive(true); authCanvas.SetActive(false);
            ClearRows();
            service = new FlightSearchService(account.Database);
            int current = revision;
            var token = BeginRequest();
            SetBusy(true); status.text = "Loading airports and travel dates...";
            try
            {
                var loaded = await service.LoadOptionsAsync(token);
                if (!Current(current)) return;
                options = loaded;
                PopulateOptions();
                SetBusy(false);
                Search();
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (Current(current)) { status.text = "We could not open flight search. Please go back and try again."; SetBusy(false); } }
        }
        public async void Search()
        {
            if (!IsOpen || IsBusy || options == null || string.IsNullOrEmpty(account.SignedInUserId)) return;
            CancelRequest();
            int current = revision;
            var token = BeginRequest();
            SetBusy(true); ClearRows(); status.text = "Searching the sample flight catalog...";
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var request = new FlightSearchRequest
                {
                    OriginAirportId = options.Airports[origin.value].Id,
                    DestinationAirportId = options.Airports[destination.value].Id,
                    DepartureDate = departureDate.options[departureDate.value].text,
                    ReturnDate = returnDate.options[returnDate.value].text,
                    MaxPriceCents = ParseMaximumPrice(maximumPrice.text),
                    AirlineId = airline.value == 0 ? null : options.Airlines[airline.value - 1].Id,
                    TimeBand = (DepartureTimeBand)timeBand.value, Sort = (FlightSort)sort.value
                };
                var result = await service.SearchAsync(request, token);
                if (!Current(current)) return;
                LastResult = result;
                Fill(result.Outbound, outboundContent); Fill(result.Return, returnContent);
                outboundHeading.text = "Outbound • " + request.OriginAirportId + " → " + request.DestinationAirportId + " • " + request.DepartureDate + " (" + result.Outbound.Count + ")";
                returnHeading.text = "Return • " + request.DestinationAirportId + " → " + request.OriginAirportId + " • " + request.ReturnDate + " (" + result.Return.Count + ")";
                status.text = result.Outbound.Count == 0 && result.Return.Count == 0 ? "No flights match. Try another route or date, or clear the filters."
                    : result.Outbound.Count == 0 || result.Return.Count == 0 ? "One direction has no matching flights. Try another date or clear the filters." : "Choose flights independently for each direction. Prices are per flight in USD.";
                Canvas.ForceUpdateCanvases();
                outboundScroll.verticalNormalizedPosition = returnScroll.verticalNormalizedPosition = 1;
                LastSearchMilliseconds = elapsed.ElapsedMilliseconds;
            }
            catch (OperationCanceledException) { }
            catch (ArgumentException exception) { if (Current(current)) status.text = exception.Message; }
            catch (Exception) { if (Current(current)) status.text = "Flight search is unavailable right now. Please try again."; }
            finally { if (Current(current)) SetBusy(false); }
        }
        public static int? ParseMaximumPrice(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = text.Trim();
            if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal price) || price < 0 || price > 21474836.47m || decimal.Round(price, 2) != price)
                throw new ArgumentException("Enter a maximum price in USD with up to two decimal places, for example 527.00.");
            return (int)(price * 100m);
        }
        private void PopulateOptions()
        {
            SetOptions(origin, options.Airports.Select(x => x.City + " (" + x.Id + ")").ToList());
            SetOptions(destination, options.Airports.Select(x => x.City + " (" + x.Id + ")").ToList());
            var dates = new List<string>();
            var first = DateTime.ParseExact(options.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var last = DateTime.ParseExact(options.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (last < first || (last - first).TotalDays > 366) throw new InvalidOperationException("The seed catalog date range must fit within one year.");
            for (var date = first; date <= last; date = date.AddDays(1)) dates.Add(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            SetOptions(departureDate, dates); SetOptions(returnDate, dates);
            origin.value = Math.Max(0, options.Airports.ToList().FindIndex(x => x.Id == "JFK"));
            destination.value = Math.Max(0, options.Airports.ToList().FindIndex(x => x.Id == "LHR"));
            departureDate.value = Math.Max(0, dates.IndexOf("2027-06-15")); returnDate.value = Math.Max(0, dates.IndexOf("2027-06-22"));
            var airlines = new List<string> { "All airlines" }; airlines.AddRange(options.Airlines.Select(x => x.Name)); SetOptions(airline, airlines);
            SetOptions(timeBand, new List<string> { "Any departure time", "Night (00:00-05:59)", "Morning (06:00-11:59)", "Afternoon (12:00-17:59)", "Evening (18:00-23:59)" });
            SetOptions(sort, new List<string> { "Price: low to high", "Price: high to low", "Departure: earliest", "Departure: latest", "Airline: A-Z" });
            maximumPrice.text = "";
        }
        private static void SetOptions(TMP_Dropdown dropdown, List<string> values) { dropdown.ClearOptions(); dropdown.AddOptions(values); dropdown.value = 0; dropdown.RefreshShownValue(); }
        public void ClearFilters() { if (IsBusy || options == null) return; maximumPrice.text = ""; airline.value = timeBand.value = sort.value = 0; Search(); }
        public void Close() { resumeInterruptedSearch = false; CancelRequest(); ClearRows(); flightCanvas.SetActive(false); authCanvas.SetActive(true); }
        /// <summary>Temporarily hides this screen while keeping the current form and completed offers.</summary>
        public void Suspend()
        {
            resumeInterruptedSearch = IsBusy;
            CancelRequest(); flightCanvas.SetActive(false);
        }
        public void Resume()
        {
            if (string.IsNullOrEmpty(account.SignedInUserId)) return;
            flightCanvas.SetActive(true); authCanvas.SetActive(false);
            if (options == null) { Open(); return; }
            bool retry = resumeInterruptedSearch; resumeInterruptedSearch = false;
            if (retry) Search();
        }
        private void Logout() => account.Logout();
        private void OnLoggedOut() { Close(); options = null; service = null; status.text = ""; }
        private void OnLoggedIn(string _) { CancelRequest(); ClearRows(); }
        private void Fill(IReadOnlyList<FlightOption> flights, Transform parent)
        {
            foreach (var flight in flights) { var row = Instantiate(rowPrefab, parent); row.gameObject.SetActive(true); row.Show(flight); rows.Add(row.gameObject); }
        }
        private void ClearRows()
        {
            foreach (var row in rows) if (row) { row.SetActive(false); Destroy(row); }
            rows.Clear(); LastResult = null; LastSearchMilliseconds = 0;
            outboundHeading.text = "Outbound"; returnHeading.text = "Return";
        }
        private CancellationToken BeginRequest() { requestLifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken); return requestLifetime.Token; }
        private void CancelRequest() { revision++; requestLifetime?.Cancel(); requestLifetime?.Dispose(); requestLifetime = null; SetBusy(false); }
        private bool Current(int current) => this && revision == current && IsOpen && !string.IsNullOrEmpty(account.SignedInUserId);
        private void SetBusy(bool value)
        {
            IsBusy = value;
            if (!searchButton) return;
            searchButton.interactable = !value;
            if (clearButton) clearButton.interactable = !value;
            foreach (var control in new UnityEngine.UI.Selectable[] { origin, destination, departureDate, returnDate, airline, timeBand, sort, maximumPrice }) if (control) control.interactable = !value;
        }
        private void OnDestroy()
        {
            CancelRequest();
            if (account) { account.LoggedOut -= OnLoggedOut; account.LoggedIn -= OnLoggedIn; }
            if (openButton) openButton.onClick.RemoveListener(Open);
            if (searchButton) searchButton.onClick.RemoveListener(Search);
            if (clearButton) clearButton.onClick.RemoveListener(ClearFilters);
            if (backButton) backButton.onClick.RemoveListener(Close);
            if (logoutButton) logoutButton.onClick.RemoveListener(Logout);
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
                if ((ArgumentPresent("-authSmoke") || ArgumentPresent("-flightSmoke") || ArgumentPresent("-destinationSmoke")) && (!Debug.isDebugBuild || string.IsNullOrEmpty(smokeFile) || !smokeFile.StartsWith("auth-smoke-", StringComparison.Ordinal)))
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
