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
    public sealed class PriceTrackingPage : Shared.ScreenControllerBase
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
        private NotificationBadge badge;
        [SerializeField]
        private GameObject modalCanvas, demoPanel;
        [SerializeField]
        private CanvasGroup[] underlyingGroups;
        [Header("Buttons")]
        [SerializeField]
        private UnityEngine.UI.Button homeButton, flightButton, hubButton, closeButton, refreshButton, trackButton, readAllButton, applyButton;
        [Header("Input fields")]
        [SerializeField]
        private TMP_Dropdown watchChoice;
        [SerializeField]
        private TMP_InputField demoPrice;
        [Header("Results and presentation")]
        [SerializeField]
        private TMP_Text targetPreview, status, unreadSummary, trackLabel, demoFeedback;
        [SerializeField]
        private UnityEngine.UI.ScrollRect scroll;
        [SerializeField]
        private PriceNoticeRow rowPrefab;
        private PriceTrackingView view;
        private PriceTrackingView View => view ?? (view = new PriceTrackingView(watchChoice, demoPrice, targetPreview, unreadSummary, trackLabel, scroll, rowPrefab));

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

        public void Configure(Shared.TravelSceneBindings scene)
        {
            account = scene.Account;
            flights = scene.Flights;
            hub = scene.Hub;
            reviews = scene.Reviews;
            trips = scene.Trips;
            badge = scene.Badge;
            underlyingGroups = scene.UnderlyingGroups;
            homeButton = scene.Button(scene.HomeCard, "NotificationsButton");
            flightButton = scene.Button(scene.FlightCanvas.transform, "Main/Header/NotificationsButton");
            hubButton = scene.Button(scene.HubCanvas.transform, "Main/Header/NotificationsButton");
        }

        // Scene initialization and event wiring.
        private void Start()
        {
            if (!account || !flights || !hub || !reviews || !trips || !badge || !modalCanvas || !demoPanel ||
                underlyingGroups == null || underlyingGroups.Length != 3 || underlyingGroups.Any(group => !group) || !homeButton ||
                !flightButton || !hubButton || !closeButton || !refreshButton || !trackButton || !readAllButton || !applyButton ||
                !watchChoice || !demoPrice || !targetPreview || !status || !unreadSummary || !trackLabel || !demoFeedback ||
                !scroll || !rowPrefab)
            {
                Debug.LogError("PriceTrackingPage: assign all references on PriceTrackingController using the prepared scene.");
                enabled = false;
                return;
            }

            homeButton.onClick.AddListener(Browse);
            flightButton.onClick.AddListener(Browse);
            hubButton.onClick.AddListener(Browse);
            closeButton.onClick.AddListener(Close);
            refreshButton.onClick.AddListener(Refresh);
            trackButton.onClick.AddListener(ToggleTracking);
            readAllButton.onClick.AddListener(ReadAll);
            applyButton.onClick.AddListener(ApplyDemo);
            watchChoice.onValueChanged.AddListener(ChooseWatch);
            demoPrice.onValueChanged.AddListener(ValidateDemo);
            flights.TrackRequested += OpenFlight;
            hub.TrackRequested += OpenHotel;
            flights.ContentCleared += Close;
            hub.ContentCleared += Close;
            account.LoggedOut += Close;
            demoPanel.SetActive(Debug.isDebugBuild);
        }

        // Browse saved data.
        private void Browse()
        {
            targetId = null;
            Open();
        }

        // Open from a flight card.
        private void OpenFlight(FlightOption flight)
        {
            targetKind = PriceTargetKind.Flight;
            targetId = flight.Id;
            Open();
        }

        // Open from a hotel card.
        private void OpenHotel(PlaceOption hotel)
        {
            if (hotel.Category != PlaceCategory.Hotel)
            {
                return;
            }

            targetKind = PriceTargetKind.Hotel;
            targetId = hotel.Id;
            Open();
        }

        // Open screen.
        private void Open()
        {
            if (!account.IsReady || string.IsNullOrEmpty(account.SignedInUserId))
            {
                return;
            }

            reviews.Close();
            trips.Close();
            Cancel();
            ClearContent();
            ownerId = account.SignedInUserId;
            service = new PriceTrackingService(account.Database, ownerId);
            if (!locked)
            {
                originalInteraction = underlyingGroups.Select(group => group.interactable).ToArray();
                foreach (var group in underlyingGroups)
                {
                    group.interactable = false;
                }

                locked = true;
            }

            modalCanvas.SetActive(true);
            if (UnityEngine.EventSystems.EventSystem.current)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
            }

            Refresh();
        }

        // Refresh tracking state.
        private async void Refresh()
        {
            if (!IsOpen || ownerId != account.SignedInUserId)
            {
                return;
            }

            Cancel();
            int current = RequestRevision;
            var token = Begin();
            var requestService = service;
            ClearContent();
            SetBusy(true);
            status.text = "Loading your price watches and notifications...";
            badge.Refresh();
            try
            {
                await Load(requestService, token, current);
                if (Current(current))
                {
                    status.text = LastSnapshot.Notifications.Count == 0
                        ? EmptyNotificationsMessage()
                        : "These alerts describe changes to this installation's sample catalog.";
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                if (Current(current))
                {
                    status.text = "Price tracking could not be loaded. Choose Refresh.";
                }
            }
            finally
            {
                if (Current(current))
                {
                    SetBusy(false);
                }
            }
        }

        private static string EmptyNotificationsMessage()
        {
            return Debug.isDebugBuild
                ? "No notifications yet. Track a flight or hotel, then try a demo price change."
                : "No notifications yet. You will see an alert when a tracked sample price changes.";
        }

        private async Task Load(PriceTrackingService requestService, CancellationToken token, int current)
        {
            var snapshot = await requestService.LoadAsync(token);
            if (!Current(current))
            {
                return;
            }

            LastSnapshot = snapshot;
            if (targetId == null && snapshot.Watches.Count > 0)
            {
                targetId = snapshot.Watches[0].TargetId;
                targetKind = snapshot.Watches[0].Kind;
            }

            View.Show(snapshot, targetId, targetKind, Read);
            if (targetId != null)
            {
                var target = await requestService.GetTargetAsync(targetKind, targetId, token);
                if (!Current(current))
                {
                    return;
                }

                CurrentTarget = target;
                View.ShowTarget(target);
            }
            else
            {
                View.ShowTarget(null);
            }

            View.ScrollToTop();
        }

        // Select a price watch.
        private void ChooseWatch(int index)
        {
            if (IsBusy || LastSnapshot == null || index < 0 || index > LastSnapshot.Watches.Count)
            {
                return;
            }

            if (index == 0)
            {
                targetId = null;
                CurrentTarget = null;
                targetPreview.text = "Choose a tracked item, or use Track price on a flight or hotel card.";
                trackLabel.text = "Track price";
                demoPrice.SetTextWithoutNotify("");
                SetBusy(false);
                return;
            }

            var watch = LastSnapshot.Watches[index - 1];
            targetId = watch.TargetId;
            targetKind = watch.Kind;
            Refresh();
        }

        // Start or stop price tracking.
        private async void ToggleTracking()
        {
            if (IsBusy || !IsOpen || CurrentTarget == null)
            {
                return;
            }

            var target = CurrentTarget;
            await Write(async (requestService, token) => target.IsActive ? await requestService.StopAsync(target.Id,
                token) ? "Price tracking stopped." : "This watch was already stopped." : await requestService.TrackAsync(target.Kind,
                target.TargetId, token) ? "Price tracking started at the current sample price." : "You are already tracking this item.");
        }

        // Mark one notification read.
        private async void Read(string id)
        {
            if (IsBusy || !IsOpen)
            {
                return;
            }

            await Write(async (requestService, token) => await requestService.MarkReadAsync(id, token) ? "Notification marked read." : "Notification is already read.");
        }

        // Mark all notifications read.
        private async void ReadAll()
        {
            if (IsBusy || !IsOpen)
            {
                return;
            }

            await Write(async (requestService, token) => "Marked " + await requestService.MarkAllReadAsync(token) + " notifications read.");
        }

        // Apply a development-only sample price.
        private async void ApplyDemo()
        {
            if (!Debug.isDebugBuild || IsBusy || !IsOpen || CurrentTarget == null || !TryDemoPrice(out int cents))
            {
                return;
            }

            var target = CurrentTarget;
            var database = account.Database;
            await Write(async (_, token) => await new DemoPriceService(database).ChangePriceAsync(target.Kind,
                target.TargetId, cents, token) ? "Demo price changed. Re-run searches or reload destinations to refresh existing cards." : "Price unchanged. No notification was created.");
        }

        // Run a saved-data action.
        private async Task Write(Func<PriceTrackingService, CancellationToken, Task<string>> operation)
        {
            Cancel();
            int current = RequestRevision;
            var token = Begin();
            var requestService = service;
            string requestOwner = ownerId;
            ClearContent();
            SetBusy(true);
            status.text = "Saving your change...";
            try
            {
                string message = await operation(requestService, token);
                // A committed write still updates the badge if the panel was closed, but never for another account.
                if (this && requestOwner == account.SignedInUserId)
                {
                    await badge.RefreshAsync();
                }

                if (!Current(current))
                {
                    return;
                }

                await Load(requestService, token, current);
                if (Current(current))
                {
                    status.text = message;
                }
            }
            catch (OperationCanceledException)
            {
                if (this && requestOwner == account.SignedInUserId)
                {
                    await badge.RefreshAsync();
                }
            }
            catch (ArgumentException exception)
            {
                if (Current(current))
                {
                    status.text = exception.Message;
                }
            }
            catch (Exception)
            {
                if (Current(current))
                {
                    status.text = "The change could not be completed. Choose Refresh to check the saved state.";
                }
            }
            finally
            {
                if (Current(current))
                {
                    SetBusy(false);
                }
            }
        }

        private bool TryDemoPrice(out int cents)
        {
            cents = 0;
            try
            {
                int? parsed = FlightSearchPage.ParseMaximumPrice(demoPrice.text);
                if (!parsed.HasValue)
                {
                    return false;
                }

                cents = parsed.Value;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private void ValidateDemo(string ignored)
        {
            bool valid = TryDemoPrice(out _);
            if (demoFeedback)
            {
                demoFeedback.text = valid ? "Demo only: changes are shared by all accounts on this installation." : "Enter a nonnegative USD price with at most two decimal places, for example 527.00.";
            }

            if (applyButton)
            {
                applyButton.interactable = Debug.isDebugBuild && !IsBusy && CurrentTarget != null && valid;
            }
        }

        private void SetBusy(bool busy)
        {
            IsBusy = busy;
            refreshButton.interactable = !busy;
            trackButton.interactable = !busy && CurrentTarget != null;
            readAllButton.interactable = !busy && LastSnapshot != null && LastSnapshot.UnreadCount > 0;
            watchChoice.interactable = !busy && LastSnapshot != null && LastSnapshot.Watches.Count > 0;
            demoPrice.interactable = !busy && CurrentTarget != null;
            View.SetRowsInteractable(!busy);
            ValidateDemo(null);
        }

        private void ClearContent()
        {
            View.Clear();
            LastSnapshot = null;
            CurrentTarget = null;
        }

        // Close screen and clear account-specific state.
        public void Close()
        {
            Cancel();
            ClearContent();
            ownerId = targetId = null;
            service = null;
            if (status)
            {
                status.text = "";
            }

            if (locked)
            {
                for (int i = 0; i < underlyingGroups.Length; i++)
                {
                    if (underlyingGroups[i])
                    {
                        underlyingGroups[i].interactable = originalInteraction[i];
                    }
                }

                locked = false;
            }

            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events && events.currentSelectedGameObject && modalCanvas && events.currentSelectedGameObject.transform.IsChildOf(modalCanvas.transform))
            {
                events.SetSelectedGameObject(null);
            }

            if (modalCanvas)
            {
                modalCanvas.SetActive(false);
            }
        }

        private CancellationToken Begin() => base.BeginScreenRequest();
        private void Cancel()
        {
            base.CancelScreenRequest();
            IsBusy = false;
        }

        private bool Current(int current) => base.IsRequestCurrent(current) && IsOpen && !string.IsNullOrEmpty(ownerId) && ownerId == account.SignedInUserId;
        // Lifecycle cleanup.
        protected override void OnDestroy()
        {
            base.OnDestroy();
            Close();
            if (flights)
            {
                flights.TrackRequested -= OpenFlight;
                flights.ContentCleared -= Close;
            }

            if (hub)
            {
                hub.TrackRequested -= OpenHotel;
                hub.ContentCleared -= Close;
            }

            if (account)
            {
                account.LoggedOut -= Close;
            }

            if (homeButton)
            {
                homeButton.onClick.RemoveListener(Browse);
            }

            if (flightButton)
            {
                flightButton.onClick.RemoveListener(Browse);
            }

            if (hubButton)
            {
                hubButton.onClick.RemoveListener(Browse);
            }

            if (closeButton)
            {
                closeButton.onClick.RemoveListener(Close);
            }

            if (refreshButton)
            {
                refreshButton.onClick.RemoveListener(Refresh);
            }

            if (trackButton)
            {
                trackButton.onClick.RemoveListener(ToggleTracking);
            }

            if (readAllButton)
            {
                readAllButton.onClick.RemoveListener(ReadAll);
            }

            if (applyButton)
            {
                applyButton.onClick.RemoveListener(ApplyDemo);
            }

            if (watchChoice)
            {
                watchChoice.onValueChanged.RemoveListener(ChooseWatch);
            }

            if (demoPrice)
            {
                demoPrice.onValueChanged.RemoveListener(ValidateDemo);
            }
        }
    }
}
