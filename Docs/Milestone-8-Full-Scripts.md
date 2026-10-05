# Milestone 8 — Full scripts

Complete desktop-polish and release-QA source snapshot, including changed integration files and both PowerShell harnesses. See [validation results and limitations](Milestone-8-Validation.md) for the 153 passing tests, final player checks and rendered review. Current source files remain authoritative after future edits.

## Assets/TravelPlanning/UI/Desktop/DesktopCanvasFit.cs

```csharp
using UnityEngine;

namespace TravelPlanning.UI.Desktop
{
    /// <summary>Keeps the entire reference layout available when a desktop window becomes short or narrow.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.UI.CanvasScaler))]
    public sealed class DesktopCanvasFit : MonoBehaviour
    {
        private void OnEnable() => Apply();
        public void Apply()
        {
            var scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        }
    }
}
```

## Assets/TravelPlanning/UI/Desktop/DesktopKeyboardNavigation.cs

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TravelPlanning.UI.Desktop
{
    /// <summary>Provides desktop Tab and Escape navigation using the legacy Input Manager.</summary>
    [DefaultExecutionOrder(1000)]
    public sealed class DesktopKeyboardNavigation : MonoBehaviour
    {
        [Serializable]
        public struct ScreenBinding
        {
            public Canvas canvas;
            public UnityEngine.UI.Button backButton;
            public UnityEngine.UI.Selectable defaultFocus;
        }
        [SerializeField] private LoginPage account;
        [SerializeField] private ScreenBinding[] screens;
        private readonly Dictionary<Canvas, GameObject> rememberedFocus = new Dictionary<Canvas, GameObject>();
        private Canvas previousScreen;
        private TMP_Dropdown previousExpanded;
        public Canvas ActiveScreen => FindActiveScreen();
        private void OnEnable() { if (account) account.ExternalKeyboardNavigation = true; }
        private void Start()
        {
            if (!account || screens == null || screens.Length == 0 || screens.Any(binding => !binding.canvas)) { Debug.LogError("DesktopKeyboardNavigation: assign the account and desktop screen bindings."); enabled = false; return; }
            account.ExternalKeyboardNavigation = true;
        }
        private void LateUpdate()
        {
            if (!EventSystem.current) return;
            UpdateScreenFocus();
            if (Input.GetKeyDown(KeyCode.Escape)) HandleEscape();
            else if (Input.GetKeyDown(KeyCode.Tab)) MoveFocus(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            RememberCurrentFocus();
            previousExpanded = FindExpanded(FindActiveScreen());
        }
        public void RememberCurrentFocus()
        {
            if (!EventSystem.current) return;
            var screen = FindActiveScreen(); var selected = EventSystem.current.currentSelectedGameObject;
            if (screen && IsEligible(selected, screen)) rememberedFocus[screen] = selected;
        }
        public void UpdateScreenFocus()
        {
            var screen = FindActiveScreen();
            if (!screen || !EventSystem.current) return;
            if (screen == previousScreen && IsEligible(EventSystem.current.currentSelectedGameObject, screen)) return;
            previousScreen = screen;
            if (IsEligible(EventSystem.current.currentSelectedGameObject, screen)) return;
            if (rememberedFocus.TryGetValue(screen, out GameObject remembered) && IsEligible(remembered, screen)) { Select(remembered.GetComponent<UnityEngine.UI.Selectable>()); return; }
            var binding = screens.First(item => item.canvas == screen);
            if (binding.defaultFocus && IsEligible(binding.defaultFocus.gameObject, screen)) Select(binding.defaultFocus);
            else { var order = GetTabOrder(screen); if (order.Count > 0) Select(order[0]); }
        }
        public void MoveFocus(bool backwards)
        {
            var screen = FindActiveScreen(); if (!screen || FindExpanded(screen) || !EventSystem.current) return;
            var order = GetTabOrder(screen); if (order.Count == 0) return;
            int current = order.FindIndex(item => item.gameObject == EventSystem.current.currentSelectedGameObject);
            int next = current < 0 ? backwards ? order.Count - 1 : 0 : (current + (backwards ? -1 : 1) + order.Count) % order.Count;
            Select(order[next]);
        }
        public void HandleEscape()
        {
            var screen = FindActiveScreen(); if (!screen) return;
            // StandaloneInputModule may already have hidden the popup earlier in this frame.
            var expanded = FindExpanded(screen); if (!expanded && previousExpanded && previousExpanded.transform.IsChildOf(screen.transform)) expanded = previousExpanded;
            if (expanded) { expanded.Hide(); Select(expanded); previousExpanded = null; return; }
            var binding = screens.First(item => item.canvas == screen);
            if (binding.backButton && binding.backButton.gameObject.activeInHierarchy && binding.backButton.IsInteractable()) { binding.backButton.onClick.Invoke(); UpdateScreenFocus(); }
        }
        private Canvas FindActiveScreen()
        {
            if (screens == null) return null;
            Canvas top = null; foreach (var binding in screens) if (binding.canvas && binding.canvas.enabled && binding.canvas.gameObject.activeInHierarchy && (!top || binding.canvas.sortingOrder > top.sortingOrder)) top = binding.canvas;
            return top;
        }
        private static TMP_Dropdown FindExpanded(Canvas screen) => screen ? screen.GetComponentsInChildren<TMP_Dropdown>().FirstOrDefault(dropdown => dropdown.IsExpanded) : null;
        public static List<UnityEngine.UI.Selectable> GetTabOrder(Canvas screen)
        {
            if (!screen) return new List<UnityEngine.UI.Selectable>();
            // Positions are measured in reference-canvas units, keeping the order stable as the window resizes.
            return screen.GetComponentsInChildren<UnityEngine.UI.Selectable>().Where(item => IsEligible(item.gameObject, screen))
                .OrderByDescending(item => Mathf.RoundToInt(screen.transform.InverseTransformPoint(item.transform.TransformPoint(((RectTransform)item.transform).rect.center)).y / 8f))
                .ThenBy(item => screen.transform.InverseTransformPoint(item.transform.TransformPoint(((RectTransform)item.transform).rect.center)).x)
                .ThenBy(item => item.GetInstanceID()).ToList();
        }
        private static bool IsEligible(GameObject obj, Canvas screen)
        {
            if (!obj || !screen || !obj.activeInHierarchy || !obj.transform.IsChildOf(screen.transform)) return false;
            var selectable = obj.GetComponent<UnityEngine.UI.Selectable>(); return selectable && selectable.IsActive() && selectable.IsInteractable() && selectable.navigation.mode != UnityEngine.UI.Navigation.Mode.None;
        }
        private static void Select(UnityEngine.UI.Selectable selectable)
        {
            if (!selectable || !EventSystem.current) return;
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            if (selectable is TMP_InputField input) input.ActivateInputField();
            Reveal(selectable);
        }
        public static void Reveal(UnityEngine.UI.Selectable selectable)
        {
            var scroll = selectable ? selectable.GetComponentInParent<UnityEngine.UI.ScrollRect>() : null;
            if (!scroll || !scroll.viewport || !scroll.content || !selectable.transform.IsChildOf(scroll.content)) return;
            Canvas.ForceUpdateCanvases(); scroll.StopMovement();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, selectable.transform); var view = scroll.viewport.rect; Vector2 position = scroll.content.anchoredPosition;
            if (scroll.vertical) { if (bounds.max.y > view.yMax) position.y -= bounds.max.y - view.yMax; else if (bounds.min.y < view.yMin) position.y += view.yMin - bounds.min.y; }
            if (scroll.horizontal) { if (bounds.min.x < view.xMin) position.x += view.xMin - bounds.min.x; else if (bounds.max.x > view.xMax) position.x -= bounds.max.x - view.xMax; }
            scroll.content.anchoredPosition = position;
        }
        private void OnDisable() { if (account) account.ExternalKeyboardNavigation = false; rememberedFocus.Clear(); previousScreen = null; previousExpanded = null; }
    }
}
```

## Assets/TravelPlanning/UI/Polish/ReleaseScreenshots.cs

```csharp
using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TravelPlanning.UI.Polish
{
    /// <summary>Optional graphical QA capture. A missing rendered frame fails instead of claiming visual validation.</summary>
    public sealed class ReleaseScreenshots : MonoBehaviour
    {
        public string OutputDirectory { get; set; }
        public async Task CaptureMatrixAsync(string name, CancellationToken token)
        {
            // Startup can report the correct window size while the splash renderer still owns the frame.
            for (int wait = 0; !UnityEngine.Rendering.SplashScreen.isFinished && wait < 600; wait++) await Task.Delay(50, token);
            if (!UnityEngine.Rendering.SplashScreen.isFinished) throw new TimeoutException("The splash screen did not finish before capture.");
            foreach (var size in new[] { new Vector2Int(1000, 700), new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(1920, 600) })
            {
                token.ThrowIfCancellationRequested();
                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
                for (int wait = 0; (Screen.width != size.x || Screen.height != size.y) && wait < 100; wait++) await Task.Delay(30, token);
                if (Screen.width != size.x || Screen.height != size.y) throw new InvalidOperationException("Window did not reach requested screenshot size " + size);
                await Task.Delay(150, token);
                var captured = new TaskCompletionSource<bool>();
                StartCoroutine(CaptureFrame(name, size, captured));
                if (await Task.WhenAny(captured.Task, Task.Delay(10000, token)) != captured.Task) throw new TimeoutException("No rendered frame was available for capture.");
                await captured.Task;
            }
        }

        private IEnumerator CaptureFrame(string name, Vector2Int size, TaskCompletionSource<bool> completed)
        {
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            // Wait for a second distinct rendered frame after startup or a resolution change.
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            Texture2D texture = null;
            try
            {
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (!texture || texture.width != size.x || texture.height != size.y) throw new InvalidOperationException("Captured frame dimensions differ from the requested size.");
                if (!HasVisiblePixels(texture)) throw new InvalidOperationException("Captured frame is black; visual QA was not completed.");
                Directory.CreateDirectory(OutputDirectory);
                string path = Path.Combine(OutputDirectory, name + "-" + size.x + "x" + size.y + ".png");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Debug.Log("RELEASE_CAPTURE " + path);
                completed.SetResult(true);
            }
            catch (Exception error) { completed.SetException(error); }
            finally { if (texture) Destroy(texture); }
        }

        private static bool HasVisiblePixels(Texture2D texture)
        {
            // This application's white forms cannot legitimately produce an entirely black frame.
            var pixels = texture.GetPixels32();
            for (int y = 0; y < texture.height; y += Math.Max(1, texture.height / 16))
                for (int x = 0; x < texture.width; x += Math.Max(1, texture.width / 16))
                {
                    var pixel = pixels[y * texture.width + x];
                    if (pixel.r > 3 || pixel.g > 3 || pixel.b > 3) return true;
                }
            return false;
        }
    }
}
```

## Assets/TravelPlanning/UI/Polish/ReleaseSmokeArguments.cs

```csharp
using System;
using System.IO;
using System.Linq;

namespace TravelPlanning.UI.Polish
{
    /// <summary>Validates isolation before LoginPage creates or opens any database.</summary>
    public static class ReleaseSmokeArguments
    {
        private static readonly string[] LegacyFlags = { "-authSmoke", "-flightSmoke", "-destinationSmoke", "-reviewSmoke", "-tripSmoke", "-trackingSmoke" };

        public static bool IsSmokeRequested(string[] args) => args.Contains("-releaseSmoke") || args.Any(value => LegacyFlags.Contains(value));

        public static string ResolveDatabaseFile(string[] args, bool developmentBuild, string developmentOverride = null)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            bool release = args.Contains("-releaseSmoke");
            bool legacy = args.Any(value => LegacyFlags.Contains(value));
            if (release)
            {
                if (developmentBuild || legacy || args.Count(value => value == "-releaseSmoke") != 1 || args.Count(value => value == "-authFile") != 1)
                    throw new ArgumentException("Release QA requires one isolated release flag and a nondevelopment player.");
                string file = Value(args, "-authFile");
                if (!IsIsolatedFile(file)) throw new ArgumentException("Release QA requires auth-smoke-<32 hexadecimal GUID characters>.db.");
                return file;
            }
            string smokeFile = Value(args, "-authFile");
            if (legacy && (!developmentBuild || string.IsNullOrEmpty(smokeFile) || !smokeFile.StartsWith("auth-smoke-", StringComparison.Ordinal)))
                throw new ArgumentException("Development smoke checks require an explicit isolated database.");
            if (!developmentBuild) return "travel.db"; // Ordinary releases ignore arbitrary database overrides.
            string selected = smokeFile ?? developmentOverride;
            if (string.IsNullOrEmpty(selected)) return "travel.db";
            if (Path.GetFileName(selected) != selected || !selected.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The test database must be a .db file name.");
            return selected;
        }

        public static bool IsIsolatedFile(string file)
        {
            const string prefix = "auth-smoke-";
            return file != null && file.Length == prefix.Length + 32 + 3 && file.StartsWith(prefix, StringComparison.Ordinal) &&
                file.EndsWith(".db", StringComparison.Ordinal) && Guid.TryParseExact(file.Substring(prefix.Length, 32), "N", out _);
        }

        public static string Value(string[] args, string key)
        {
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
```

## Assets/TravelPlanning/UI/Polish/ReleaseSmokeRunner.cs

```csharp
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

namespace TravelPlanning.UI.Polish
{
    /// <summary>Opt-in QA for a real release player, restricted to a GUID-named disposable database.</summary>
    public sealed class ReleaseSmokeRunner : MonoBehaviour
    {
        [SerializeField] private LoginPage account;
        [SerializeField] private FlightSearchPage flights;
        [SerializeField] private DestinationHubPage hub;
        [SerializeField] private ReviewsPage reviews;
        [SerializeField] private SavedTripsPage trips;
        [SerializeField] private PriceTrackingPage tracking;
        [SerializeField] private GameObject authCanvas, flightCanvas, hubCanvas, trackingCanvas;
        private ReleaseScreenshots captures;
        private const string Email = "release-qa@example.test";
        private const string Password = "Release QA isolated passphrase 2027!";

        private async void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!args.Contains("-releaseSmoke")) return;
            try
            {
                string file = ReleaseSmokeArguments.ResolveDatabaseFile(args, Debug.isDebugBuild);
                if (args.Contains("-releaseScreenshots"))
                {
                    string directory = ReleaseSmokeArguments.Value(args, "-releaseCaptureDir");
                    if (string.IsNullOrEmpty(directory) || !Path.IsPathRooted(directory)) throw new ArgumentException("Release screenshot output must be an absolute directory.");
                    captures = gameObject.AddComponent<ReleaseScreenshots>(); captures.OutputDirectory = Path.GetFullPath(directory);
                }
                await RunAsync(!args.Contains("-releaseReopen"), file);
                Debug.Log("RELEASE_UI_SMOKE_PASS development=" + Debug.isDebugBuild + " reopen=" + args.Contains("-releaseReopen"));
                Application.Quit(0);
            }
            catch (Exception error) { Debug.LogError("RELEASE_UI_SMOKE_FAIL " + error.GetType().Name + ": " + error.Message); Application.Quit(1); }
        }

        private async Task RunAsync(bool firstLaunch, string file)
        {
            Require(!Debug.isDebugBuild, "QA must execute the actual nondevelopment release.");
            await Until(() => account.IsReady, "Account startup");
            var right = authCanvas.transform.Find("Right Panel"); var login = right.Find("LoginCard"); var registration = right.Find("RegistrationCard"); var home = right.Find("SignedInCard");
            await Capture("01-login");
            Click(login, "Create an account"); await Capture("02-registration");
            if (firstLaunch)
            {
                Field(registration, "EmailInput").text = Email; Field(registration, "PasswordInput").text = Field(registration, "ConfirmationInput").text = Password;
                Click(registration, "LoginButton"); await Until(() => !account.IsBusy, "Registration"); Require(login.gameObject.activeSelf, "Registration failed.");
            }
            else Click(registration, "Create an account");
            Field(login, "EmailInput").text = "not-an-email"; Field(login, "PasswordInput").text = Password;
            Click(login, "LoginButton"); await Until(() => !account.IsBusy, "Invalid login");
            Require(account.SignedInUserId == null && login.gameObject.activeSelf, "Invalid login was accepted.");
            await Capture("08-login-error");
            Field(login, "EmailInput").text = Email; Field(login, "PasswordInput").text = Password;
            Click(login, "LoginButton"); await Until(() => !account.IsBusy, "Login"); Require(account.SignedInEmail == Email, "Login failed.");
            await Capture("09-signed-in-home");
            Click(home, "SearchFlightsButton"); await Until(() => !flights.IsBusy && flights.LastResult != null, "Flight search");
            Require(flights.LastResult.Outbound.Count == 2 && flights.LastResult.Return.Count == 2, "Seeded round trip was not returned.");
            Debug.Log("RELEASE_FLIGHT_TIMING queryMs=" + flights.LastResult.ElapsedMilliseconds + " uiMs=" + flights.LastSearchMilliseconds +
                " worker=" + flights.LastResult.WorkerThreadId + " main=" + System.Threading.Thread.CurrentThread.ManagedThreadId);
            Require(flights.LastSearchMilliseconds < 3000 && flights.LastResult.WorkerThreadId != System.Threading.Thread.CurrentThread.ManagedThreadId, "Release flight search exceeded its responsiveness target.");
            await Capture("03-flights");
            var selectedFlight = flights.LastResult.Outbound.First(flight => flight.Id == "JFK-LHR-20270615-BA");
            Click(flightCanvas.transform, "Main/Header/ExploreDestinationButton"); await Until(() => !hub.IsBusy && hub.LastResult != null, "Destination hub");
            Require(hub.CardCount == 12, "Destination categories did not render."); await Capture("04-destination");
            var hotelCard = hubCanvas.transform.Find("Main/ScrollView/Viewport/Content/Hotels/Items").GetComponentsInChildren<PlaceCard>().First();
            Click(hotelCard.transform, "ReviewActions/ViewReviewsButton"); await Until(() => !reviews.IsBusy && reviews.LastResult != null, "Reviews");
            Require(reviews.LastResult.Reviews.Count == 2 && reviews.LastResult.Reviews.All(review => review.IsDemo), "Sample review details are missing.");
            string hotelId = reviews.LastResult.Place.Id; await Capture("05-reviews"); reviews.Close();

            var plans = new TripService(account.Database, account.SignedInUserId);
            var watches = new PriceTrackingService(account.Database, account.SignedInUserId);
            if (firstLaunch)
            {
                var trip = await plans.CreateAsync("Release QA London plan", "2027-06-15", "2027-06-22", destroyCancellationToken);
                await plans.SaveAsync(trip.Id, SavedItemKind.Flight, selectedFlight.Id, destroyCancellationToken);
                await plans.SaveAsync(trip.Id, SavedItemKind.Hotel, hotelId, destroyCancellationToken);
                await watches.TrackAsync(PriceTargetKind.Flight, selectedFlight.Id, destroyCancellationToken);
                // Explicit isolated QA fixture provisioning; the release UI still cannot invoke demo changes.
                await new DemoPriceService(account.Database).ChangePriceAsync(PriceTargetKind.Flight, selectedFlight.Id, selectedFlight.PriceCents + 1, destroyCancellationToken);
            }
            var list = await plans.ListAsync(destroyCancellationToken);
            Require(list.Trips.Count == 1 && list.Trips[0].ItemCount == 2, "Saved fixture did not persist correctly.");
            trips.Browse(); await Until(() => !trips.IsBusy && trips.LastDetails != null, "Saved trip UI");
            Require(trips.LastDetails.Items.Count == 2, "Saved-trip rows did not load."); await Capture("06-saved-trips"); trips.Close();
            Click(hubCanvas.transform, "Main/Header/NotificationsButton"); await Until(() => !tracking.IsBusy && tracking.LastSnapshot != null && tracking.CurrentTarget != null, "Tracking UI");
            Require(tracking.LastSnapshot.Watches.Count == 1 && tracking.LastSnapshot.UnreadCount == 1, "Watch or unread notification did not persist.");
            var main = trackingCanvas.transform.Find("Main"); var demo = main.Find("DemoPanel");
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
            tracking.Close(); account.Logout(); Require(account.SignedInUserId == null, "Logout retained a session.");
            Debug.Log("RELEASE_QA_DATABASE " + databasePath);
        }

        private Task Capture(string name) => captures ? captures.CaptureMatrixAsync(name, destroyCancellationToken) : Task.CompletedTask;
        private async Task Until(Func<bool> condition, string step)
        {
            for (int attempt = 0; !condition() && attempt < 3000; attempt++) await Task.Delay(10, destroyCancellationToken);
            Require(condition(), step + " timed out.");
        }
        private static byte[] Hash(string path) { using (var hash = SHA256.Create()) return hash.ComputeHash(File.ReadAllBytes(path)); }
        private static TMP_InputField Field(Transform root, string path) => root.Find(path).GetComponent<TMP_InputField>();
        private static void Click(Transform root, string path) => root.Find(path).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
```

## Assets/TravelPlanning/UI/Editor/PolishSetup.cs

```csharp
using TravelPlanning.UI.Desktop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Applies desktop resizing and keyboard behavior without changing the login layout.</summary>
    public static class PolishSetup
    {
        [MenuItem("Travel Planning/Polish/1 - Prepare Desktop Polish")]
        public static void Prepare()
        {
            TrackingSetup.Prepare();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            string[] names = { "Canvas", "FlightCanvas", "DestinationCanvas", "ReviewsCanvas", "SavedTripsCanvas", "PriceTrackingCanvas" };
            string[] back = { "Right Panel/RegistrationCard/Create an account", "Main/Header/BackButton", "Main/Header/BackButton", "Main/Header/CloseButton", "Main/Header/CloseButton", "Main/Header/CloseButton" };
            string[] focus = { "Right Panel/LoginCard/EmailInput", "Main/RouteFields/Origin/Dropdown", "Main/DestinationSelection/Dropdown", "Main/Header/CloseButton", "Main/TripSelection/TripChoice", "Main/WatchSelection/Dropdown" };
            var roots = scene.GetRootGameObjects(); var canvases = new Canvas[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var root = System.Array.Find(roots, candidate => candidate.name == names[i]); if (!root) throw new System.InvalidOperationException("Missing desktop canvas: " + names[i]);
                canvases[i] = root.GetComponent<Canvas>(); var fit = root.GetComponent<DesktopCanvasFit>(); if (!fit) fit = root.AddComponent<DesktopCanvasFit>(); fit.Apply();
            }
            var controller = Object.FindFirstObjectByType<DesktopKeyboardNavigation>(); if (!controller) controller = new GameObject("DesktopNavigationController").AddComponent<DesktopKeyboardNavigation>();
            var fields = new SerializedObject(controller); fields.FindProperty("account").objectReferenceValue = Object.FindFirstObjectByType<LoginPage>(); var bindings = fields.FindProperty("screens"); bindings.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++) { var binding = bindings.GetArrayElementAtIndex(i); binding.FindPropertyRelative("canvas").objectReferenceValue = canvases[i]; binding.FindPropertyRelative("backButton").objectReferenceValue = canvases[i].transform.Find(back[i]).GetComponent<UnityEngine.UI.Button>(); binding.FindPropertyRelative("defaultFocus").objectReferenceValue = canvases[i].transform.Find(focus[i]).GetComponent<UnityEngine.UI.Selectable>(); }
            fields.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.SaveScene(scene); Debug.Log("DESKTOP_POLISH_SCENE_READY " + scene.path);
        }
    }
}
```

## Assets/TravelPlanning/UI/Editor/ReleaseSetup.cs

```csharp
using System;
using System.IO;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Flights;
using TravelPlanning.UI.Polish;
using TravelPlanning.UI.Reviews;
using TravelPlanning.UI.Tracking;
using TravelPlanning.UI.Trips;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TravelPlanning.UI.Editor
{
    public static class ReleaseSetup
    {
        public const string BuildPath = "Builds/TravelPlannerRelease/TravelPlanner.exe";
        public const string DevelopmentBuildPath = "Builds/TravelPlannerDevelopment/TravelPlannerDevelopment.exe";

        [MenuItem("Travel Planning/Release/1 - Prepare Final Scene")]
        public static void Prepare()
        {
            PolishSetup.Prepare(); // Includes TrackingSetup.Prepare and all earlier milestones.
            var account = UnityEngine.Object.FindFirstObjectByType<LoginPage>();
            var flights = UnityEngine.Object.FindFirstObjectByType<FlightSearchPage>();
            var hub = UnityEngine.Object.FindFirstObjectByType<DestinationHubPage>();
            var tracking = UnityEngine.Object.FindFirstObjectByType<PriceTrackingPage>();
            var runner = UnityEngine.Object.FindFirstObjectByType<ReleaseSmokeRunner>();
            if (!runner) runner = new GameObject("ReleaseQaController").AddComponent<ReleaseSmokeRunner>();
            var fields = new SerializedObject(runner);
            Set(fields, "account", account); Set(fields, "flights", flights); Set(fields, "hub", hub); Set(fields, "tracking", tracking);
            Set(fields, "reviews", UnityEngine.Object.FindFirstObjectByType<ReviewsPage>());
            Set(fields, "trips", UnityEngine.Object.FindFirstObjectByType<SavedTripsPage>());
            Set(fields, "authCanvas", GameObject.Find("Canvas"));
            Set(fields, "flightCanvas", new SerializedObject(flights).FindProperty("flightCanvas").objectReferenceValue);
            Set(fields, "hubCanvas", new SerializedObject(hub).FindProperty("hubCanvas").objectReferenceValue);
            Set(fields, "trackingCanvas", new SerializedObject(tracking).FindProperty("modalCanvas").objectReferenceValue);
            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("RELEASE_SCENE_READY " + AuthSetup.ScenePath);
        }

        [MenuItem("Travel Planning/Release/2 - Build Windows Release")]
        public static void BuildWindows() => Build(BuildPath, false);

        [MenuItem("Travel Planning/Release/3 - Build Windows Development QA")]
        public static void BuildDevelopment() => Build(DevelopmentBuildPath, true);

        private static void Build(string path, bool development)
        {
            Prepare();
            var account = UnityEngine.Object.FindFirstObjectByType<LoginPage>();
            string developmentOverride = new SerializedObject(account).FindProperty("developmentDatabaseFile").stringValue;
            if (!string.IsNullOrWhiteSpace(developmentOverride))
                throw new InvalidOperationException("Clear the AccountController development database override before building the final scene. It was not changed automatically.");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { AuthSetup.ScenePath }, locationPathName = path, target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development : BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Final Windows build failed.");
            string root = Path.GetDirectoryName(path);
            string notices = Path.Combine(root, "ThirdPartyNotices"); Directory.CreateDirectory(notices);
            foreach (string file in Directory.GetFiles("Assets/Plugins/SQLite/Licenses"))
                if (!file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) File.Copy(file, Path.Combine(notices, Path.GetFileName(file)), true);
            File.Copy("Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt", Path.Combine(notices, "LiberationSans-OFL.txt"), true);
            File.Copy("Assets/Plugins/LiteDB/LICENSE.txt", Path.Combine(notices, "LiteDB-LICENSE.txt"), true);
            File.WriteAllText(Path.Combine(root, "release-validation.json"), JsonUtility.ToJson(new ValidationSettings
            { companyName = PlayerSettings.companyName, productName = PlayerSettings.productName, developmentBuild = development }, true));
            Debug.Log((development ? "FINAL_DEVELOPMENT_BUILD_PASS " : "FINAL_RELEASE_BUILD_PASS ") + Path.GetFullPath(path));
        }

        [Serializable]
        private sealed class ValidationSettings { public string companyName, productName; public bool developmentBuild; }
        private static void Set(SerializedObject fields, string name, UnityEngine.Object value) => fields.FindProperty(name).objectReferenceValue = value;
    }
}
```

## Assets/TravelPlanning/Tests/EditMode/DesktopPolishTests.cs

```csharp
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using TravelPlanning.UI.Desktop;
using TravelPlanning.UI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class DesktopPolishTests
    {
        private static int backCalls;
        private static void CountBack() => backCalls++;

        [UnityTest]
        public IEnumerator TabSkipsUnavailableControlsWrapsAndRestoresScreenFocus()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            // EventSystem registers itself in OnEnable during Play mode.
            yield return new EnterPlayMode();
            new GameObject("EventSystem", typeof(EventSystem));
            var baseCanvas = Canvas("Base", 0); var modal = Canvas("Modal", 6);
            var first = Button(baseCanvas.transform, "First", new Vector2(-100, 100));
            var second = Button(baseCanvas.transform, "Second", new Vector2(100, 100));
            var disabled = Button(baseCanvas.transform, "Disabled", Vector2.zero); disabled.interactable = false;
            var hidden = Button(baseCanvas.transform, "Hidden", new Vector2(0, -100)); hidden.gameObject.SetActive(false);
            var close = Button(modal.transform, "Close", Vector2.zero);
            var navigation = Navigator(new[] { baseCanvas, modal }, new[] { first, close }); modal.gameObject.SetActive(false);
            CollectionAssert.AreEqual(new[] { first, second }, DesktopKeyboardNavigation.GetTabOrder(baseCanvas));
            navigation.UpdateScreenFocus(); Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(first.gameObject));
            navigation.MoveFocus(true); Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(second.gameObject)); navigation.RememberCurrentFocus();
            modal.gameObject.SetActive(true); navigation.UpdateScreenFocus(); navigation.MoveFocus(false);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(close.gameObject));
            modal.gameObject.SetActive(false); navigation.UpdateScreenFocus(); Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(second.gameObject));
            second.gameObject.SetActive(false); navigation.UpdateScreenFocus(); Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(first.gameObject), "Same-canvas panel changes must repair inactive focus.");
            var group = baseCanvas.gameObject.AddComponent<CanvasGroup>(); group.interactable = false; Assert.That(DesktopKeyboardNavigation.GetTabOrder(baseCanvas), Is.Empty);
            Object.Destroy(navigation.gameObject); Object.Destroy(baseCanvas.gameObject); Object.Destroy(modal.gameObject); Object.Destroy(EventSystem.current.gameObject); yield return null; yield return new ExitPlayMode();
        }

        [Test]
        public void KeyboardSelectionRevealsAnOffscreenRow()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); var canvas = Canvas("Base", 0);
            var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>(); viewport.SetParent(canvas.transform, false); viewport.sizeDelta = new Vector2(400, 200);
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>(); content.SetParent(viewport, false); content.anchorMin = content.anchorMax = new Vector2(.5f, 1); content.pivot = new Vector2(.5f, 1); content.sizeDelta = new Vector2(400, 800);
            var scroll = viewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            var row = Button(content, "Far row", new Vector2(0, -650)); var rect = (RectTransform)row.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            DesktopKeyboardNavigation.Reveal(row); var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, row.transform);
            Assert.That(content.anchoredPosition.y, Is.GreaterThan(400)); Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(viewport.rect.yMin - 1)); Assert.That(bounds.max.y, Is.LessThanOrEqualTo(viewport.rect.yMax + 1));
        }

        [TestCase(1000, 700)]
        [TestCase(1280, 720)]
        [TestCase(1920, 1080)]
        [TestCase(1920, 600)]
        public void ExpandedReferenceLayoutKeepsScrollViewportsInsideCanvas(int width, int height)
        {
            EditorSceneManager.OpenScene(TrackingSetup.ScenePath);
            var canvases = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Select(root => root.GetComponent<Canvas>()).Where(canvas => canvas).ToArray(); Assert.That(canvases.Length, Is.EqualTo(6));
            // Simulate Expand's logical canvas extent; Windows capture validation checks physical pixels separately.
            float scale = Mathf.Min(width / 1920f, height / 1080f);
            foreach (var canvas in canvases)
            {
                var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); Assert.That(scaler.screenMatchMode, Is.EqualTo(UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand)); Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920, 1080)));
                scaler.enabled = false; canvas.renderMode = RenderMode.WorldSpace; canvas.gameObject.SetActive(true); var rect = (RectTransform)canvas.transform; rect.sizeDelta = new Vector2(width / scale, height / scale); rect.localScale = Vector3.one;
                UnityEngine.Canvas.ForceUpdateCanvases(); UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                foreach (var scroll in canvas.GetComponentsInChildren<UnityEngine.UI.ScrollRect>().Where(item => !item.GetComponentInParent<TMP_Dropdown>()))
                {
                    Assert.That(scroll.viewport, Is.Not.Null); Assert.That(scroll.viewport.rect.width, Is.GreaterThan(200), canvas.name); Assert.That(scroll.viewport.rect.height, Is.GreaterThan(100), canvas.name);
                    // Measure the viewport itself; scroll content is intentionally allowed outside it.
                    var corners = new Vector3[4]; scroll.viewport.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var local = rect.InverseTransformPoint(corner);
                        Assert.That(local.x, Is.InRange(rect.rect.xMin - 1, rect.rect.xMax + 1), canvas.name);
                        Assert.That(local.y, Is.InRange(rect.rect.yMin - 1, rect.rect.yMax + 1), canvas.name);
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator EscapeClosesDropdownBeforeInvokingScreenBack()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); yield return new EnterPlayMode();
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)); var canvas = Canvas("Base", 0); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            // A captured local would live in a compiler closure lost across EnterPlayMode's domain reload.
            var back = Button(canvas.transform, "Back", Vector2.zero); backCalls = 0; back.onClick.AddListener(CountBack);
            var dropdownObject = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources()); dropdownObject.transform.SetParent(canvas.transform, false); var dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
            var navigation = Navigator(new[] { canvas }, new[] { back }); yield return null;
            dropdown.Show(); yield return null; Assert.That(dropdown.IsExpanded, Is.True);
            EventSystem.current.SetSelectedGameObject(dropdown.gameObject); navigation.MoveFocus(false); Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(dropdown.gameObject), "Tab must not leave an expanded dropdown.");
            navigation.HandleEscape(); yield return new WaitForSecondsRealtime(.25f); Assert.That(dropdown.IsExpanded, Is.False); Assert.That(backCalls, Is.Zero);
            navigation.HandleEscape(); Assert.That(backCalls, Is.EqualTo(1));
            Object.Destroy(navigation.gameObject); Object.Destroy(canvas.gameObject); Object.Destroy(EventSystem.current.gameObject); yield return null; yield return new ExitPlayMode();
        }

        private static Canvas Canvas(string name, int order)
        {
            var canvas = new GameObject(name, typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = order; ((RectTransform)canvas.transform).sizeDelta = new Vector2(1920, 1080); return canvas;
        }
        private static UnityEngine.UI.Button Button(Transform parent, string name, Vector2 position)
        {
            var button = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Button)).GetComponent<UnityEngine.UI.Button>(); button.transform.SetParent(parent, false); var rect = (RectTransform)button.transform; rect.sizeDelta = new Vector2(100, 40); rect.anchoredPosition = position; return button;
        }
        private static DesktopKeyboardNavigation Navigator(Canvas[] canvases, UnityEngine.UI.Button[] defaults)
        {
            var navigation = new GameObject("Navigation").AddComponent<DesktopKeyboardNavigation>(); navigation.enabled = false; var fields = new SerializedObject(navigation); var screens = fields.FindProperty("screens"); screens.arraySize = canvases.Length;
            for (int i = 0; i < canvases.Length; i++) { var screen = screens.GetArrayElementAtIndex(i); screen.FindPropertyRelative("canvas").objectReferenceValue = canvases[i]; screen.FindPropertyRelative("defaultFocus").objectReferenceValue = defaults[i]; screen.FindPropertyRelative("backButton").objectReferenceValue = defaults[i]; }
            fields.ApplyModifiedPropertiesWithoutUndo(); return navigation;
        }
    }
}
```

## Assets/TravelPlanning/Tests/EditMode/ReleaseSmokeArgumentsTests.cs

```csharp
using System;
using NUnit.Framework;
using TravelPlanning.UI.Polish;

namespace TravelPlanning.Tests
{
    public sealed class ReleaseSmokeArgumentsTests
    {
        private const string Isolated = "auth-smoke-0123456789abcdef0123456789abcdef.db";

        [Test]
        public void ExplicitReleaseQaUsesOnlyItsGuidNamedDatabase()
        {
            Assert.That(ReleaseSmokeArguments.ResolveDatabaseFile(new[] { "app.exe", "-releaseSmoke", "-authFile", Isolated }, false), Is.EqualTo(Isolated));
            Assert.Throws<ArgumentException>(() => ReleaseSmokeArguments.ResolveDatabaseFile(new[] { "-releaseSmoke", "-authFile", Isolated }, true));
        }

        [TestCase("travel.db")]
        [TestCase("auth-smoke-example.db")]
        [TestCase("../auth-smoke-0123456789abcdef0123456789abcdef.db")]
        [TestCase("C:\\auth-smoke-0123456789abcdef0123456789abcdef.db")]
        [TestCase("auth-smoke-0123456789abcdef0123456789abcdeg.db")]
        [TestCase("auth-smoke-0123456789abcdef0123456789abcdef.db.extra")]
        public void MalformedFileCannotResolveToAnyDatabase(string file)
        {
            Assert.Throws<ArgumentException>(() => ReleaseSmokeArguments.ResolveDatabaseFile(new[] { "-releaseSmoke", "-authFile", file }, false));
        }

        [Test]
        public void MissingDuplicateAndCombinedFlagsAreRejected()
        {
            foreach (var args in new[] {
                new[] { "-releaseSmoke" }, new[] { "-releaseSmoke", "-authFile" },
                new[] { "-releaseSmoke", "-releaseSmoke", "-authFile", Isolated },
                new[] { "-releaseSmoke", "-authFile", Isolated, "-authFile", Isolated },
                new[] { "-releaseSmoke", "-authFile", Isolated, "-authSmoke" }
            }) Assert.Throws<ArgumentException>(() => ReleaseSmokeArguments.ResolveDatabaseFile(args, false));
        }

        [Test]
        public void OrdinaryReleaseIgnoresOverrideAndLegacySmokeCannotRunInRelease()
        {
            Assert.That(ReleaseSmokeArguments.ResolveDatabaseFile(new[] { "-authFile", "arbitrary.db" }, false, "editor.db"), Is.EqualTo("travel.db"));
            foreach (string flag in new[] { "-authSmoke", "-flightSmoke", "-destinationSmoke", "-reviewSmoke", "-tripSmoke", "-trackingSmoke" })
                Assert.Throws<ArgumentException>(() => ReleaseSmokeArguments.ResolveDatabaseFile(new[] { flag, "-authFile", Isolated }, false));
            Assert.That(ReleaseSmokeArguments.ResolveDatabaseFile(new[] { "-authSmoke", "-authFile", Isolated }, true), Is.EqualTo(Isolated));
            Assert.That(ReleaseSmokeArguments.ResolveDatabaseFile(new string[0], true, "editor-play-test.db"), Is.EqualTo("editor-play-test.db"));
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
        public bool ExternalKeyboardNavigation { get; set; }
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
                if (ArgumentPresent("-authSmoke") || ArgumentPresent("-releaseSmoke")) Application.Quit(2);
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
                string file = Polish.ReleaseSmokeArguments.ResolveDatabaseFile(Environment.GetCommandLineArgs(), Debug.isDebugBuild, developmentDatabaseFile);
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
            catch (ArgumentException) when (Polish.ReleaseSmokeArguments.IsSmokeRequested(Environment.GetCommandLineArgs()))
            {
                Debug.LogError("AUTH_QA_ARGUMENTS_REJECTED");
                Application.Quit(2);
            }
            catch (Exception)
            {
                if (!this) return;
                feedback.text = "The local database could not be opened. Your existing files have been kept. Check the database setup guide.";
                Debug.LogError("AUTH_DATABASE_UNAVAILABLE");
                if (ArgumentPresent("-authSmoke") || ArgumentPresent("-releaseSmoke")) Application.Quit(1);
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
            if (!ExternalKeyboardNavigation && Input.GetKeyDown(KeyCode.Tab))
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

## Assets/TravelPlanning/UI/Tracking/PriceTrackingPage.cs

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using TravelPlanning.Destinations;
using TravelPlanning.Flights;
using TravelPlanning.Tracking;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Flights;
using TravelPlanning.UI.Reviews;
using TravelPlanning.UI.Trips;
using UnityEngine;

namespace TravelPlanning.UI.Tracking
{
    /// <summary>Tracks seeded prices and displays account-owned notifications without touching live services.</summary>
    public sealed class PriceTrackingPage : MonoBehaviour
    {
        [SerializeField] private LoginPage account;
        [SerializeField] private FlightSearchPage flights;
        [SerializeField] private DestinationHubPage hub;
        [SerializeField] private ReviewsPage reviews;
        [SerializeField] private SavedTripsPage trips;
        [SerializeField] private NotificationBadge badge;
        [SerializeField] private GameObject modalCanvas, demoPanel;
        [SerializeField] private CanvasGroup[] underlyingGroups;
        [SerializeField] private UnityEngine.UI.Button homeButton, flightButton, hubButton, closeButton, refreshButton, trackButton, readAllButton, applyButton;
        [SerializeField] private TMP_Dropdown watchChoice;
        [SerializeField] private TMP_InputField demoPrice;
        [SerializeField] private TMP_Text targetPreview, status, unreadSummary, trackLabel, demoFeedback;
        [SerializeField] private UnityEngine.UI.ScrollRect scroll;
        [SerializeField] private PriceNoticeRow rowPrefab;
        private readonly List<PriceNoticeRow> rows = new List<PriceNoticeRow>();
        private CancellationTokenSource lifetime;
        private int revision;
        private string ownerId, targetId;
        private PriceTargetKind targetKind;
        private PriceTrackingService service;
        private bool locked;
        private bool[] originalInteraction;
        public bool IsOpen => modalCanvas && modalCanvas.activeSelf;
        public bool IsBusy { get; private set; }
        public TrackingSnapshot LastSnapshot { get; private set; }
        public WatchOption CurrentTarget { get; private set; }
        public string Status => status.text;
        private void Start()
        {
            if (!account || !flights || !hub || !reviews || !trips || !badge || !modalCanvas || !demoPanel || underlyingGroups == null || underlyingGroups.Length != 3 || underlyingGroups.Any(group => !group) || !homeButton || !flightButton || !hubButton || !closeButton || !refreshButton || !trackButton || !readAllButton || !applyButton || !watchChoice || !demoPrice || !targetPreview || !status || !unreadSummary || !trackLabel || !demoFeedback || !scroll || !rowPrefab)
            { Debug.LogError("PriceTrackingPage: assign all references on PriceTrackingController using the prepared scene."); enabled = false; return; }
            homeButton.onClick.AddListener(Browse); flightButton.onClick.AddListener(Browse); hubButton.onClick.AddListener(Browse); closeButton.onClick.AddListener(Close); refreshButton.onClick.AddListener(Refresh); trackButton.onClick.AddListener(ToggleTracking); readAllButton.onClick.AddListener(ReadAll); applyButton.onClick.AddListener(ApplyDemo);
            watchChoice.onValueChanged.AddListener(ChooseWatch); demoPrice.onValueChanged.AddListener(ValidateDemo);
            flights.TrackRequested += OpenFlight; hub.TrackRequested += OpenHotel; flights.ContentCleared += Close; hub.ContentCleared += Close; account.LoggedOut += Close;
            demoPanel.SetActive(Debug.isDebugBuild);
        }
        private void Browse() { targetId = null; Open(); }
        private void OpenFlight(FlightOption flight) { targetKind = PriceTargetKind.Flight; targetId = flight.Id; Open(); }
        private void OpenHotel(PlaceOption hotel) { if (hotel.Category != PlaceCategory.Hotel) return; targetKind = PriceTargetKind.Hotel; targetId = hotel.Id; Open(); }
        private void Open()
        {
            if (!account.IsReady || string.IsNullOrEmpty(account.SignedInUserId)) return;
            reviews.Close(); trips.Close(); Cancel(); ClearContent(); ownerId = account.SignedInUserId; service = new PriceTrackingService(account.Database, ownerId);
            if (!locked) { originalInteraction = underlyingGroups.Select(group => group.interactable).ToArray(); foreach (var group in underlyingGroups) group.interactable = false; locked = true; }
            modalCanvas.SetActive(true); if (UnityEngine.EventSystems.EventSystem.current) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(closeButton.gameObject); Refresh();
        }
        private async void Refresh()
        {
            if (!IsOpen || ownerId != account.SignedInUserId) return;
            Cancel(); int current = revision; var token = Begin(); var requestService = service; ClearContent(); SetBusy(true); status.text = "Loading your price watches and notifications...";
            badge.Refresh();
            try { await Load(requestService, token, current); if (Current(current)) status.text = LastSnapshot.Notifications.Count == 0 ? (Debug.isDebugBuild ? "No notifications yet. Track a flight or hotel, then try a demo price change." : "No notifications yet. You will see an alert when a tracked sample price changes.") : "These alerts describe changes to this installation's sample catalog."; }
            catch (OperationCanceledException) { }
            catch (Exception) { if (Current(current)) status.text = "Price tracking could not be loaded. Choose Refresh."; }
            finally { if (Current(current)) SetBusy(false); }
        }
        private async Task Load(PriceTrackingService requestService, CancellationToken token, int current)
        {
            var snapshot = await requestService.LoadAsync(token); if (!Current(current)) return;
            LastSnapshot = snapshot; var choices = new List<string> { "Choose a tracked item" }; choices.AddRange(snapshot.Watches.Select(watch => watch.Title + (watch.IsActive ? " • Tracking" : " • Stopped"))); watchChoice.AddOptions(choices);
            if (targetId == null && snapshot.Watches.Count > 0) { targetId = snapshot.Watches[0].TargetId; targetKind = snapshot.Watches[0].Kind; }
            int index = snapshot.Watches.ToList().FindIndex(watch => watch.TargetId == targetId && watch.Kind == targetKind); watchChoice.SetValueWithoutNotify(index + 1); watchChoice.RefreshShownValue();
            foreach (var notice in snapshot.Notifications) { string id = notice.Id; var row = Instantiate(rowPrefab, scroll.content); row.Show(notice, () => Read(id)); row.SetInteractable(false); rows.Add(row); }
            unreadSummary.text = snapshot.UnreadCount + " unread • " + snapshot.Notifications.Count + " total notifications";
            if (targetId != null)
            {
                var target = await requestService.GetTargetAsync(targetKind, targetId, token); if (!Current(current)) return; CurrentTarget = target;
                targetPreview.text = target.Title + "\nCurrent sample price: USD " + (target.CurrentPriceCents / 100m).ToString("N2", CultureInfo.InvariantCulture) + " " + target.PriceUnit;
                trackLabel.text = target.IsActive ? "Stop tracking" : "Track price"; demoPrice.SetTextWithoutNotify((target.CurrentPriceCents / 100m).ToString("0.00", CultureInfo.InvariantCulture));
            }
            else targetPreview.text = "Use Track price on a flight or hotel card to start a watch.";
            Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1;
        }
        private void ChooseWatch(int index)
        {
            if (IsBusy || LastSnapshot == null || index < 0 || index > LastSnapshot.Watches.Count) return;
            if (index == 0) { targetId = null; CurrentTarget = null; targetPreview.text = "Choose a tracked item, or use Track price on a flight or hotel card."; trackLabel.text = "Track price"; demoPrice.SetTextWithoutNotify(""); SetBusy(false); return; }
            var watch = LastSnapshot.Watches[index - 1]; targetId = watch.TargetId; targetKind = watch.Kind; Refresh();
        }
        private async void ToggleTracking()
        {
            if (IsBusy || !IsOpen || CurrentTarget == null) return;
            var target = CurrentTarget;
            await Write(async (requestService, token) => target.IsActive
                ? await requestService.StopAsync(target.Id, token) ? "Price tracking stopped." : "This watch was already stopped."
                : await requestService.TrackAsync(target.Kind, target.TargetId, token) ? "Price tracking started at the current sample price." : "You are already tracking this item.");
        }
        private async void Read(string id) { if (IsBusy || !IsOpen) return; await Write(async (requestService, token) => await requestService.MarkReadAsync(id, token) ? "Notification marked read." : "Notification is already read."); }
        private async void ReadAll() { if (IsBusy || !IsOpen) return; await Write(async (requestService, token) => "Marked " + await requestService.MarkAllReadAsync(token) + " notifications read."); }
        private async void ApplyDemo()
        {
            if (!Debug.isDebugBuild || IsBusy || !IsOpen || CurrentTarget == null || !TryDemoPrice(out int cents)) return;
            var target = CurrentTarget; var database = account.Database;
            await Write(async (_, token) => await new DemoPriceService(database).ChangePriceAsync(target.Kind, target.TargetId, cents, token) ? "Demo price changed. Re-run searches or reload destinations to refresh existing cards." : "Price unchanged. No notification was created.");
        }
        private async Task Write(Func<PriceTrackingService, CancellationToken, Task<string>> operation)
        {
            Cancel(); int current = revision; var token = Begin(); var requestService = service; string requestOwner = ownerId; ClearContent(); SetBusy(true); status.text = "Saving your change...";
            try
            {
                string message = await operation(requestService, token);
                // A committed write still updates the badge if the panel was closed, but never for another account.
                if (this && requestOwner == account.SignedInUserId) await badge.RefreshAsync();
                if (!Current(current)) return; await Load(requestService, token, current); if (Current(current)) status.text = message;
            }
            catch (OperationCanceledException) { if (this && requestOwner == account.SignedInUserId) await badge.RefreshAsync(); }
            catch (ArgumentException exception) { if (Current(current)) status.text = exception.Message; }
            catch (Exception) { if (Current(current)) status.text = "The change could not be completed. Choose Refresh to check the saved state."; }
            finally { if (Current(current)) SetBusy(false); }
        }
        private bool TryDemoPrice(out int cents) { cents = 0; try { int? parsed = FlightSearchPage.ParseMaximumPrice(demoPrice.text); if (!parsed.HasValue) return false; cents = parsed.Value; return true; } catch (ArgumentException) { return false; } }
        private void ValidateDemo(string ignored) { bool valid = TryDemoPrice(out _); if (demoFeedback) demoFeedback.text = valid ? "Demo only: changes are shared by all accounts on this installation." : "Enter a nonnegative USD price with at most two decimal places, for example 527.00."; if (applyButton) applyButton.interactable = Debug.isDebugBuild && !IsBusy && CurrentTarget != null && valid; }
        private void SetBusy(bool busy)
        {
            IsBusy = busy; refreshButton.interactable = !busy; trackButton.interactable = !busy && CurrentTarget != null; readAllButton.interactable = !busy && LastSnapshot != null && LastSnapshot.UnreadCount > 0; watchChoice.interactable = !busy && LastSnapshot != null && LastSnapshot.Watches.Count > 0; demoPrice.interactable = !busy && CurrentTarget != null;
            foreach (var row in rows) if (row) row.SetInteractable(!busy); ValidateDemo(null);
        }
        private void ClearContent() { foreach (var row in rows) if (row) { row.gameObject.SetActive(false); Destroy(row.gameObject); } rows.Clear(); LastSnapshot = null; CurrentTarget = null; if (watchChoice) watchChoice.ClearOptions(); if (targetPreview) targetPreview.text = ""; if (unreadSummary) unreadSummary.text = ""; if (trackLabel) trackLabel.text = "Track price"; if (demoPrice) demoPrice.SetTextWithoutNotify(""); }
        public void Close()
        {
            Cancel(); ClearContent(); ownerId = targetId = null; service = null; if (status) status.text = "";
            if (locked) { for (int i = 0; i < underlyingGroups.Length; i++) if (underlyingGroups[i]) underlyingGroups[i].interactable = originalInteraction[i]; locked = false; }
            var events = UnityEngine.EventSystems.EventSystem.current; if (events && events.currentSelectedGameObject && modalCanvas && events.currentSelectedGameObject.transform.IsChildOf(modalCanvas.transform)) events.SetSelectedGameObject(null); if (modalCanvas) modalCanvas.SetActive(false);
        }
        private CancellationToken Begin() { lifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken); return lifetime.Token; }
        private void Cancel() { revision++; lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null; IsBusy = false; }
        private bool Current(int current) => this && IsOpen && current == revision && !string.IsNullOrEmpty(ownerId) && ownerId == account.SignedInUserId;
        private void OnDestroy()
        {
            Close(); if (flights) { flights.TrackRequested -= OpenFlight; flights.ContentCleared -= Close; } if (hub) { hub.TrackRequested -= OpenHotel; hub.ContentCleared -= Close; } if (account) account.LoggedOut -= Close;
            if (homeButton) homeButton.onClick.RemoveListener(Browse); if (flightButton) flightButton.onClick.RemoveListener(Browse); if (hubButton) hubButton.onClick.RemoveListener(Browse); if (closeButton) closeButton.onClick.RemoveListener(Close); if (refreshButton) refreshButton.onClick.RemoveListener(Refresh); if (trackButton) trackButton.onClick.RemoveListener(ToggleTracking); if (readAllButton) readAllButton.onClick.RemoveListener(ReadAll); if (applyButton) applyButton.onClick.RemoveListener(ApplyDemo); if (watchChoice) watchChoice.onValueChanged.RemoveListener(ChooseWatch); if (demoPrice) demoPrice.onValueChanged.RemoveListener(ValidateDemo);
        }
    }
}
```

## tools/Test-Mvp.ps1

```powershell
param(
    [string]$Executable = 'Builds/TravelPlannerDevelopment/TravelPlannerDevelopment.exe',
    [ValidateRange(90, 180)][int]$TimeoutSeconds = 150
)
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $Executable).Path
$buildRoot = Split-Path -Parent $exe
# Read the build's identity rather than assuming it matches today's project settings.
$metadataPath = Join-Path $buildRoot 'release-validation.json'
if (-not (Test-Path -LiteralPath $metadataPath)) { throw "Build metadata missing: $metadataPath. Build the current development artifact first." }
$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
if ($metadata.developmentBuild -ne $true) { throw 'Test-Mvp requires a development build; use the separate release harness for release QA.' }
foreach ($name in @($metadata.companyName, $metadata.productName)) {
    if ([string]::IsNullOrWhiteSpace($name) -or $name -in @('.', '..') -or $name.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) { throw 'Invalid company/product name in build metadata.' }
}
$profileRoot = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
$persistent = Join-Path (Join-Path (Join-Path $profileRoot 'AppData/LocalLow') $metadata.companyName) $metadata.productName
$normalDatabase = Join-Path $persistent 'travel.db'
$runId = [Guid]::NewGuid().ToString('N')
$logs = Join-Path (Get-Location).Path "Logs/Mvp/$runId"
New-Item -ItemType Directory -Path $logs -Force | Out-Null

function Get-NormalDatabaseState {
    $state = [ordered]@{}
    foreach ($suffix in @('', '-journal', '-wal', '-shm')) {
        $path = $normalDatabase + $suffix
        $state[$suffix] = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { $null }
    }
    return ($state | ConvertTo-Json -Compress)
}

$features = @(
    @{ Name = 'authentication'; Flag = '-authSmoke'; First = '-authRegister'; Reopen = $null; Marker = 'AUTH_UI_SMOKE_PASS' },
    @{ Name = 'flights'; Flag = '-flightSmoke'; First = $null; Reopen = '-flightReopen'; Marker = 'FLIGHT_UI_SMOKE_PASS' },
    @{ Name = 'destinations'; Flag = '-destinationSmoke'; First = $null; Reopen = '-destinationReopen'; Marker = 'DESTINATION_UI_SMOKE_PASS' },
    @{ Name = 'reviews'; Flag = '-reviewSmoke'; First = $null; Reopen = '-reviewReopen'; Marker = 'REVIEWS_UI_SMOKE_PASS' },
    @{ Name = 'trips'; Flag = '-tripSmoke'; First = $null; Reopen = '-tripReopen'; Marker = 'SAVED_TRIPS_UI_SMOKE_PASS' },
    @{ Name = 'tracking'; Flag = '-trackingSmoke'; First = $null; Reopen = '-trackingReopen'; Marker = 'PRICE_TRACKING_UI_SMOKE_PASS' }
)
$before = Get-NormalDatabaseState
$before | Set-Content -LiteralPath (Join-Path $logs 'personal-database-before.json')
$passed = 0
try {
    foreach ($feature in $features) {
        # One unique database per feature; its second process reuses that same fixture.
        $fixture = 'auth-smoke-' + [Guid]::NewGuid().ToString('N') + '.db'
        foreach ($phase in @('first', 'reopen')) {
            $log = Join-Path $logs ($feature.Name + '-' + $phase + '.log')
            $arguments = @('-batchmode', '-nographics', $feature.Flag, '-authFile', $fixture, '-logFile', ('"' + $log + '"'))
            $extra = if ($phase -eq 'first') { $feature.First } else { $feature.Reopen }
            if ($extra) { $arguments += $extra }
            $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
            if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
                $process.Kill(); $process.WaitForExit()
                throw "$($feature.Name) $phase timed out. See $log"
            }
            $process.Refresh()
            if (-not (Test-Path -LiteralPath $log)) { throw "Player log missing: $log" }
            $output = Get-Content -LiteralPath $log -Raw
            if ($process.ExitCode -ne 0 -or $output -notmatch [regex]::Escape($feature.Marker)) { throw "$($feature.Name) $phase failed (exit $($process.ExitCode)). See $log" }
            if ((Get-NormalDatabaseState) -ne $before) { throw "Personal database or sidecar changed during $($feature.Name) $phase. See $logs" }
            $passed++
            Write-Output "PASS $($feature.Name) $phase - $log"
            Select-String -LiteralPath $log -Pattern '_TIMING' | ForEach-Object { $_.Line }
        }
    }
}
finally {
    $after = Get-NormalDatabaseState
    $after | Set-Content -LiteralPath (Join-Path $logs 'personal-database-after.json')
    if ($after -ne $before) { throw "Personal database guard failed. Compare before/after evidence in $logs; no files were reset or deleted." }
}
Write-Output "MVP_REGRESSION_PASS $passed/12 player scenarios; personal database unchanged. Logs: $logs"
```

## tools/Test-Release.ps1

```powershell
param([string]$BuildRoot = 'Builds/TravelPlannerRelease', [switch]$Screenshots)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $BuildRoot).Path
$exe = Join-Path $root 'TravelPlanner.exe'
$settings = Get-Content -LiteralPath (Join-Path $root 'release-validation.json') -Raw | ConvertFrom-Json
if ($settings.developmentBuild -ne $false) { throw 'This harness requires the nondevelopment release artifact.' }
foreach ($name in @($settings.companyName, $settings.productName)) {
    if ([string]::IsNullOrWhiteSpace($name) -or $name -in @('.', '..') -or $name.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) { throw 'Invalid company/product name in build metadata.' }
}
$persistent = Join-Path ([Environment]::GetFolderPath('UserProfile')) ('AppData/LocalLow/' + $settings.companyName + '/' + $settings.productName)
$normal = Join-Path $persistent 'travel.db'
function Get-NormalState {
    $state = @{}
    foreach ($suffix in @('', '-wal', '-shm', '-journal')) {
        $path = $normal + $suffix
        $state[$suffix] = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { 'absent' }
    }
    return $state
}
$normalBefore = Get-NormalState
$id = [Guid]::NewGuid().ToString('N')
$file = "auth-smoke-$id.db"
$logs = Join-Path (Get-Location).Path "Logs/Release/$id"
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$normalBefore | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $logs 'personal-database-before.json')

function Invoke-Release([string]$Name, [string[]]$Flags, [bool]$ExpectedSuccess, [bool]$Graphics = $false) {
    $log = Join-Path $logs ($Name + '.log')
    $arguments = @('-logFile', ('"' + $log + '"')) + $Flags
    if ($Graphics) { $arguments += @('-force-d3d11', '-screen-fullscreen', '0') }
    else { $arguments += @('-batchmode', '-nographics') }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $timeout = if ($ExpectedSuccess) { 240000 } else { 20000 }
    if (-not $process.WaitForExit($timeout)) { $process.Kill(); throw "Release check $Name timed out. See $log" }
    $process.Refresh()
    $output = Get-Content -LiteralPath $log -Raw
    if ($ExpectedSuccess) {
        if ($process.ExitCode -ne 0 -or $output -notmatch 'RELEASE_UI_SMOKE_PASS development=False') { throw "Release check $Name failed. See $log" }
    } else {
        if ($process.ExitCode -eq 0 -or $output -notmatch 'AUTH_QA_ARGUMENTS_REJECTED|RELEASE_UI_SMOKE_FAIL') { throw "Invalid release arguments were not rejected in $Name. See $log" }
    }
    Write-Output "PASS $Name - $log"
}

try {
    Invoke-Release 'reject-missing-file' @('-releaseSmoke') $false
    Invoke-Release 'reject-normal-file' @('-releaseSmoke', '-authFile', 'travel.db') $false
    Invoke-Release 'reject-malformed-guid' @('-releaseSmoke', '-authFile', 'auth-smoke-not-a-guid.db') $false
    Invoke-Release 'reject-traversal' @('-releaseSmoke', '-authFile', '../travel.db') $false
    Invoke-Release 'reject-duplicate-file' @('-releaseSmoke', '-authFile', $file, '-authFile', $file) $false
    Invoke-Release 'reject-combined-smoke' @('-releaseSmoke', '-authSmoke', '-authFile', $file) $false
    Invoke-Release 'reject-development-flag' @('-flightSmoke', '-authFile', $file) $false
    if (Test-Path -LiteralPath (Join-Path $persistent $file)) { throw 'Invalid argument checks created the isolated fixture unexpectedly.' }
    $flags = @('-releaseSmoke', '-authFile', $file)
    if ($Screenshots) {
        $captures = Join-Path $logs 'Screenshots'
        $flags += @('-releaseScreenshots', '-releaseCaptureDir', ('"' + $captures + '"'))
    }
    Invoke-Release 'first-launch' $flags $true $Screenshots.IsPresent
    if (-not (Test-Path -LiteralPath (Join-Path $persistent $file))) { throw 'First launch did not create the isolated fixture.' }
    Invoke-Release 'reopen' @('-releaseSmoke', '-releaseReopen', '-authFile', $file) $true
    if (-not (Test-Path -LiteralPath (Join-Path $persistent $file))) { throw 'Reopen lost the isolated fixture.' }
    if ($Screenshots -and @(Get-ChildItem -LiteralPath $captures -Filter '*.png').Count -ne 36) { throw 'Expected eight normal screens plus a login error at four resolutions (36 PNG files).' }
} finally {
    $normalAfter = Get-NormalState
    $normalAfter | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $logs 'personal-database-after.json')
    foreach ($suffix in $normalBefore.Keys) {
        if ($normalBefore[$suffix] -ne $normalAfter[$suffix]) { throw "The normal user database or sidecar changed: $normal$suffix" }
    }
}
Write-Output "PASS normal database and sidecar preservation. Isolated fixture: $file"
if ($Screenshots) { Write-Output "Screenshots require visual inspection: $captures" }
```
