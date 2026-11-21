#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

namespace TravelApp.FlightMap.Editor
{
    // This validator is editor-only and is never included in a player build.
    /// <summary>
    /// One-shot smoke-test driver used by Tools/Flight Map/Run Play Mode Validation.
    /// It opens the existing scene, checks serialized/runtime references, exercises the flight
    /// controls, captures the Game view, and writes a truthful report under FlightMap/Tests.
    /// </summary>
    [InitializeOnLoad]
    public static class FlightMapPlayModeValidator
    {
        const string RequestPath = "Assets/FlightMap/Tests/RunValidation.request";
        const string ReportPath = "Assets/FlightMap/Tests/RuntimeValidation.txt";
        const string ScreenshotPath = "Assets/FlightMap/Tests/FlightMapGameView1280x720.png";
        const string FullHdScreenshotPath = "Assets/FlightMap/Tests/FlightMapGameView1920x1080.png";
        const string ScenePath = "Assets/FlightMap/Scenes/FlightMapScene.unity";
        static readonly List<string> results = new();
        static double stageStarted;
        static int stage;
        static Vector2 startPosition;
        static Vector2 pausedPosition;
        static AirplaneAnimationController animator;
        static FlightMapViewport viewport;
        static FlightPriceManager priceManager;
        static Button startButton;
        static Button pauseButton;
        static Button restartButton;
        static Button zoomInButton;
        static Button zoomOutButton;
        static bool previousEnterPlayModeOptionsEnabled;
        static EnterPlayModeOptions previousEnterPlayModeOptions;

        static FlightMapPlayModeValidator()
        {
            if (Application.isBatchMode) return;
            if (!File.Exists(RequestPath)) return;
            EditorApplication.update += BeginWhenReady;
        }

        static void BeginWhenReady()
        {
            // InitializeOnLoad can run while the project is still completing its initial import.
            // Waiting here prevents Unity from dropping a delayCall during startup.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EditorApplication.update -= BeginWhenReady;
            if (!File.Exists(RequestPath)) return;
            try { File.Delete(RequestPath); }
            catch (IOException) { return; } // Another Editor instance claimed the request.
            Begin();
        }

        [MenuItem("Tools/Flight Map/Run Play Mode Validation")]
        public static void RequestValidation()
        {
            File.WriteAllText(RequestPath, "One-shot validation request");
            AssetDatabase.Refresh();
            Debug.Log("Flight Map validation requested; scripts will reload once before the test begins.");
        }

        static void Begin()
        {
            results.Clear();
            results.Add("Flight Map runtime validation — " + DateTime.Now.ToString("O"));
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Record("Game view accepted fixed 1280x720 sharp-preview configuration", FlightMapSharpGameView.Configure(1280, 720));
            // Keep this editor-only staged validator alive across the Play Mode transition.
            // The developer's original setting is restored as soon as the test returns to Edit Mode.
            previousEnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.isPlaying = true;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                Screen.SetResolution(1280, 720, false);
                stage = 0; stageStarted = EditorApplication.timeSinceStartup;
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
                EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
                File.WriteAllLines(ReportPath, results);
                AssetDatabase.Refresh();
                Debug.Log("Flight Map validation complete. Report: " + ReportPath);
            }
        }

        static void Tick()
        {
            double elapsed = EditorApplication.timeSinceStartup - stageStarted;
            if (stage == 0 && elapsed > 1.5)
            {
                animator = UnityEngine.Object.FindFirstObjectByType<AirplaneAnimationController>();
                viewport = UnityEngine.Object.FindFirstObjectByType<FlightMapViewport>();
                priceManager = UnityEngine.Object.FindFirstObjectByType<FlightPriceManager>();
                var manager = UnityEngine.Object.FindFirstObjectByType<FlightMapManager>();
                var selector = UnityEngine.Object.FindFirstObjectByType<FlightLocationSelector>();
                var route = UnityEngine.Object.FindFirstObjectByType<FlightRouteRenderer>();
                var raw = GameObject.Find("MapContent")?.GetComponent<RawImage>();
                var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
                var canvasScaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
                var eventSystem = UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
                var inputModule = eventSystem != null ? eventSystem.GetComponent<InputSystemUIInputModule>() : null;
                var departureInput = GameObject.Find("DepartureSearch")?.GetComponent<TMP_InputField>();
                var planeRect = GameObject.Find("Airplane")?.GetComponent<RectTransform>();
                var startRect = GameObject.Find("StartFlightButton")?.GetComponent<RectTransform>();
                var pauseRect = GameObject.Find("PauseResumeButton")?.GetComponent<RectTransform>();
                var restartRect = GameObject.Find("RestartFlightButton")?.GetComponent<RectTransform>();
                startButton = GameObject.Find("StartFlightButton")?.GetComponent<Button>();
                pauseButton = GameObject.Find("PauseResumeButton")?.GetComponent<Button>();
                restartButton = GameObject.Find("RestartFlightButton")?.GetComponent<Button>();
                zoomInButton = GameObject.Find("ZoomInButton")?.GetComponent<Button>();
                zoomOutButton = GameObject.Find("ZoomOutButton")?.GetComponent<Button>();
                Record("Canvas is active and overlay", canvas != null && canvas.isActiveAndEnabled && canvas.renderMode == RenderMode.ScreenSpaceOverlay);
                Record("Canvas uses readable 1280x720 scaling", canvasScaler != null && canvasScaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize && canvasScaler.referenceResolution == new Vector2(1280, 720));
                Record("Canvas uses pixel-perfect overlay alignment", canvas != null && canvas.pixelPerfect);
                Record("EventSystem exists", eventSystem != null && eventSystem.isActiveAndEnabled);
                Record("Input System UI actions are assigned", inputModule?.actionsAsset != null && inputModule.point?.action != null && inputModule.leftClick?.action != null);
                Record("Manager, selector, route, viewport, animator exist", manager != null && selector != null && route != null && viewport != null && animator != null);
                Record("Demo price manager and provider are active", priceManager != null && priceManager.isActiveAndEnabled && UnityEngine.Object.FindFirstObjectByType<DemoFlightPriceDataProvider>() != null);
                Record("Map texture is assigned at 4096x2048", raw != null && raw.texture != null && raw.texture.width == 4096 && raw.texture.height == 2048);
                Record("Search text is readable and correctly inset", departureInput?.textComponent != null && departureInput.textComponent.fontSize >= 19f && departureInput.textComponent.rectTransform.rect.height > 20f);
                Record("Airplane icon is at least 64 pixels", planeRect != null && planeRect.rect.width >= 64f && planeRect.rect.height >= 64f);
                Record("Flight controls have visible positive-height rectangles", startRect != null && pauseRect != null && restartRect != null && startRect.rect.height >= 40f && pauseRect.rect.height >= 40f && restartRect.rect.height >= 40f);
                Record("Route contains samples", route != null && route.Points.Count > 2);
                Record("Markers and airplane are active", Active("DepartureMarker") && Active("DestinationMarker") && Active("Airplane"));
                Record("Start/Pause/Restart buttons are active", Active("StartFlightButton") && Active("PauseResumeButton") && Active("RestartFlightButton"));
                Record("All five buttons have serialized onClick callbacks", HasPersistentClick(startButton) && HasPersistentClick(pauseButton) && HasPersistentClick(restartButton) && HasPersistentClick(zoomInButton) && HasPersistentClick(zoomOutButton));
                Record("All five buttons are interactable raycast targets", AcceptsRaycasts(startButton) && AcceptsRaycasts(pauseButton) && AcceptsRaycasts(restartButton) && AcceptsRaycasts(zoomInButton) && AcceptsRaycasts(zoomOutButton));
                Record("Multiple demo price bubbles are displayed", priceManager != null && priceManager.OfferCount >= 6 && priceManager.Markers.Count == priceManager.OfferCount);
                Record("Price bubbles share the map transform", priceManager != null && priceManager.Markers.Count > 0 && priceManager.Markers[0].transform.IsChildOf(GameObject.Find("MapContent").transform));
                TMP_Text selectedPriceLabel = GameObject.Find("Text_Selected demo fare: loading")?.GetComponent<TMP_Text>();
                Record("Demo prices are clearly labeled not live", GameObject.Find("Text_DEMO PRICES · NOT LIVE") != null && selectedPriceLabel != null);
                if (File.Exists(ScreenshotPath)) File.Delete(ScreenshotPath);
                ScreenCapture.CaptureScreenshot(ScreenshotPath, 1);
                if (animator == null) { Finish("Animator missing; interaction test aborted."); return; }
                var destinationSearchField = typeof(FlightLocationSelector).GetField("destinationSearch", BindingFlags.Instance | BindingFlags.NonPublic);
                var destinationSearch = destinationSearchField?.GetValue(selector) as TMP_InputField;
                string originalDistance = GameObject.Find("DistanceInfo")?.GetComponent<TMP_Text>()?.text;
                if (destinationSearch != null) destinationSearch.text = "Miami";
                Record("Destination search filters to MIA", selector != null && selector.Destination?.code == "MIA");
                string filteredDistance = GameObject.Find("DistanceInfo")?.GetComponent<TMP_Text>()?.text;
                Record("Changing destination updates distance", !string.IsNullOrEmpty(filteredDistance) && filteredDistance != originalDistance);
                var content = GameObject.Find("MapContent")?.GetComponent<RectTransform>();
                Vector2 beforePan = content != null ? content.anchoredPosition : Vector2.zero;
                var drag = new PointerEventData(EventSystem.current) { position = Vector2.zero };
                viewport.OnBeginDrag(drag); drag.position = new Vector2(60f, 30f); viewport.OnDrag(drag);
                Record("Pointer drag pans shared map content", content != null && Vector2.Distance(beforePan, content.anchoredPosition) > 1f);
                if (destinationSearch != null) destinationSearch.text = "Los Angeles";
                Record("Changing destination restores LAX route", selector != null && selector.Destination?.code == "LAX" && route.Points.Count > 2);
                FlightPriceMarker jfkLaxMarker = FindPriceMarker("LAX");
                int jfkLaxPrice = jfkLaxMarker?.Offer?.Amount ?? -1;
                selector.SelectDepartureByCode("ORD");
                FlightPriceMarker ordLaxMarker = FindPriceMarker("LAX");
                Record("Changing departure refreshes demo offers", priceManager.LoadedDepartureCode == "ORD" && ordLaxMarker != null && ordLaxMarker.Offer.Amount != jfkLaxPrice);
                selector.SelectDepartureByCode("JFK");
                selector.SelectDestinationByCode("DXB");
                Record("Missing demo fare is handled visibly", selectedPriceLabel.text.Contains("unavailable"));
                selector.SelectDestinationByCode("LAX");
                FlightPriceMarker miaPriceMarker = FindPriceMarker("MIA");
                Record("Price bubble is an interactable raycast target", AcceptsRaycasts(miaPriceMarker?.Button));
                miaPriceMarker?.Button?.onClick.Invoke();
                Record("Clicking a price bubble selects its destination", selector.Destination?.code == "MIA");
                Record("Price selection updates route and information", route.Points.Count > 2 && selectedPriceLabel.text.Contains("$155") && selectedPriceLabel.text.Contains("not live"));
                var duration = typeof(AirplaneAnimationController).GetField("demonstrationDurationSeconds", BindingFlags.Instance | BindingFlags.NonPublic);
                Record("Configured demonstration duration is 12 seconds", Mathf.Approximately(animator.DemonstrationDurationSeconds, 12f));
                float zoomBefore = viewport.Zoom;
                zoomInButton.onClick.Invoke();
                Record("Zoom In button enlarges map", viewport.Zoom > zoomBefore);
                float zoomAfterIn = viewport.Zoom;
                zoomOutButton.onClick.Invoke();
                Record("Zoom Out button reduces map", viewport.Zoom < zoomAfterIn);
                duration?.SetValue(animator, 2f);
                startPosition = GameObject.Find("Airplane").GetComponent<RectTransform>().anchoredPosition;
                ClickThroughEventSystem(startButton);
                stage = 1; stageStarted = EditorApplication.timeSinceStartup;
            }
            else if (stage == 1 && elapsed > .55)
            {
                FlightPriceMarker scaledMarker = FindPriceMarker("MIA");
                float expectedMarkerScale = 1f / Mathf.Max(.01f, viewport.Zoom);
                Record("Price bubbles keep a constant readable screen size", scaledMarker != null && Mathf.Abs(scaledMarker.transform.localScale.x - expectedMarkerScale) < .02f);
                Record("Game view capture is native 1280x720", PngHasSize(ScreenshotPath, 1280, 720));
                Vector2 moved = GameObject.Find("Airplane").GetComponent<RectTransform>().anchoredPosition;
                Record("Start Flight moves the airplane", animator.State == AirplaneAnimationController.FlightState.Flying && Vector2.Distance(startPosition, moved) > 1f);
                ClickThroughEventSystem(pauseButton); pausedPosition = moved;
                stage = 2; stageStarted = EditorApplication.timeSinceStartup;
            }
            else if (stage == 2 && elapsed > .45)
            {
                Vector2 current = GameObject.Find("Airplane").GetComponent<RectTransform>().anchoredPosition;
                Record("Pause stops movement", animator.State == AirplaneAnimationController.FlightState.Paused && Vector2.Distance(pausedPosition, current) < .1f);
                ClickThroughEventSystem(pauseButton);
                Record("Resume returns to Flying", animator.State == AirplaneAnimationController.FlightState.Flying);
                stage = 3; stageStarted = EditorApplication.timeSinceStartup;
            }
            else if (stage == 3 && elapsed > .35)
            {
                ClickThroughEventSystem(restartButton);
                Vector2 current = GameObject.Find("Airplane").GetComponent<RectTransform>().anchoredPosition;
                Record("Restart returns to departure", animator.State == AirplaneAnimationController.FlightState.Ready && Vector2.Distance(startPosition, current) < .1f);
                float oldZoom = viewport.Zoom; ClickThroughEventSystem(zoomInButton);
                Record("Zoom In remains functional after Restart", viewport.Zoom > oldZoom);
                ClickThroughEventSystem(zoomOutButton);
                ClickThroughEventSystem(startButton);
                ClickThroughEventSystem(startButton);
                Record("Repeated Start clicks keep one flight active", animator.State == AirplaneAnimationController.FlightState.Flying);
                stage = 4; stageStarted = EditorApplication.timeSinceStartup;
            }
            else if (stage == 4 && (animator.State == AirplaneAnimationController.FlightState.Arrived || elapsed > 20.0))
            {
                Record("Flight reaches Arrived", animator.State == AirplaneAnimationController.FlightState.Arrived);
                var arrival = GameObject.Find("Text_Arrived at destination");
                Record("Arrival message is visible", arrival != null && arrival.activeInHierarchy);
                FlightMapSharpGameView.Configure(1920, 1080);
                Screen.SetResolution(1920, 1080, false);
                stage = 5; stageStarted = EditorApplication.timeSinceStartup;
            }
            else if (stage == 5 && elapsed > 1.0)
            {
                if (File.Exists(FullHdScreenshotPath)) File.Delete(FullHdScreenshotPath);
                ScreenCapture.CaptureScreenshot(FullHdScreenshotPath, 1);
                stage = 6; stageStarted = EditorApplication.timeSinceStartup;
            }
            else if (stage == 6 && elapsed > 1.0)
            {
                Record("Game view capture is native 1920x1080", PngHasSize(FullHdScreenshotPath, 1920, 1080));
                Finish(null);
            }
        }

        static bool Active(string name) { var go = GameObject.Find(name); return go != null && go.activeInHierarchy; }
        static bool HasPersistentClick(Button button) => button != null && button.onClick.GetPersistentEventCount() > 0;
        static FlightPriceMarker FindPriceMarker(string destinationCode)
        {
            if (priceManager == null) return null;
            foreach (FlightPriceMarker marker in priceManager.Markers)
                if (marker != null && marker.Offer?.Destination?.code == destinationCode) return marker;
            return null;
        }
        static bool IsRaycastable(Button button)
        {
            if (button == null || EventSystem.current == null) return false;
            RectTransform rect = button.transform as RectTransform;
            var data = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            return hits.Exists(hit => hit.gameObject == button.gameObject || hit.gameObject.transform.IsChildOf(button.transform));
        }
        static bool AcceptsRaycasts(Button button)
        {
            Graphic target = button != null ? button.targetGraphic : null;
            return button != null && button.isActiveAndEnabled && button.interactable &&
                   target != null && target.isActiveAndEnabled && target.raycastTarget;
        }
        static bool ClickThroughEventSystem(Button button)
        {
            if (button == null || EventSystem.current == null) return false;
            RectTransform rect = button.transform as RectTransform;
            var data = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            RaycastResult hit = hits.Find(result => result.gameObject == button.gameObject || result.gameObject.transform.IsChildOf(button.transform));
            if (hit.gameObject == null) return false;
            GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
            return handler != null && ExecuteEvents.Execute(handler, data, ExecuteEvents.pointerClickHandler);
        }
        static bool PngHasSize(string path, int expectedWidth, int expectedHeight)
        {
            if (!File.Exists(path)) return false;
            byte[] header = File.ReadAllBytes(path);
            if (header.Length < 24) return false;
            int width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            int height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            return width == expectedWidth && height == expectedHeight;
        }
        static void Record(string name, bool passed) => results.Add((passed ? "PASS — " : "FAIL — ") + name);
        static void Finish(string note)
        {
            if (!string.IsNullOrEmpty(note)) results.Add("FAIL — " + note);
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
