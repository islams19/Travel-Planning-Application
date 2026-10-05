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
    public sealed class DestinationHubPage : Shared.ScreenControllerBase
    {
        [Header("Screen references")]
        [SerializeField]
        private LoginPage account;
        [SerializeField]
        private FlightSearchPage flights;
        [SerializeField]
        private GameObject hubCanvas;
        [Header("Input fields")]
        [SerializeField]
        private TMP_Dropdown destination;
        [Header("Results and presentation")]
        [SerializeField]
        private TMP_Text description, status;
        [Header("Buttons")]
        [SerializeField]
        private UnityEngine.UI.Button openButton, backButton, logoutButton, retryButton;
        [Header("Results and presentation")]
        [SerializeField]
        private UnityEngine.UI.ScrollRect scroll;
        [SerializeField]
        private PlaceSection hotels, restaurants, experiences, hotspots;
        [SerializeField]
        private PlaceCard cardPrefab;
        private DestinationHubService service;
        private DestinationHubView view;
        private DestinationHubView View => view ?? (view = new DestinationHubView(destination, description,
            hotels, restaurants, experiences, hotspots, cardPrefab, scroll));

        private IReadOnlyList<DestinationOption> options;
        public bool IsBusy { get; private set; }
        public bool IsOpen => hubCanvas && hubCanvas.activeSelf;
        public DestinationHubResult LastResult { get; private set; }
        public long LastLoadMilliseconds { get; private set; }
        public int CardCount => hotels.CardCount + restaurants.CardCount + experiences.CardCount + hotspots.CardCount;

        public event Action<PlaceOption> ReviewRequested;
        public event Action<PlaceOption> SaveRequested;
        public event Action<PlaceOption> TrackRequested;
        public event Action ContentCleared;
        public void Configure(Shared.TravelSceneBindings scene)
        {
            account = scene.Account;
            flights = scene.Flights;
            openButton = scene.Button(scene.FlightCanvas.transform, "Main/Header/ExploreDestinationButton");
        }

        // Scene initialization and event wiring.
        private void Start()
        {
            if (!account || !flights || !hubCanvas || !destination || !description || !status || !openButton ||
                !backButton || !logoutButton || !retryButton || !scroll || !hotels || !restaurants || !experiences || !hotspots ||
                !cardPrefab)
            {
                Debug.LogError("DestinationHubPage: assign all references on DestinationController using the prepared scene.");
                enabled = false;
                return;
            }

            openButton.onClick.AddListener(Open);
            backButton.onClick.AddListener(Back);
            logoutButton.onClick.AddListener(Logout);
            retryButton.onClick.AddListener(Retry);
            destination.onValueChanged.AddListener(SelectDestination);
            account.LoggedOut += OnLoggedOut;
        }

        // Open screen.
        public async void Open()
        {
            if (!account.IsReady || string.IsNullOrEmpty(account.SignedInUserId))
            {
                return;
            }

            string selectedId = flights.SelectedDestinationId;
            if (flights.IsOpen)
            {
                flights.Suspend();
            }

            hubCanvas.SetActive(true);
            Clear();
            Cancel();
            options = null;
            destination.ClearOptions();
            service = new DestinationHubService(account.Database);
            int current = RequestRevision;
            var token = Begin();
            IsBusy = true;
            destination.interactable = false;
            retryButton.interactable = false;
            status.text = "Loading destinations...";
            try
            {
                var loaded = await service.LoadOptionsAsync(token);
                if (!Current(current))
                {
                    return;
                }

                options = loaded;
                if (options.Count == 0)
                {
                    status.text = "No destinations are available in this sample catalog.";
                    IsBusy = false;
                    retryButton.interactable = true;
                    return;
                }

                View.ShowOptions(options, selectedId);
                destination.interactable = true;
                LoadSelected();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                if (Current(current))
                {
                    status.text = "Destinations could not be loaded. Choose Retry.";
                    IsBusy = false;
                    retryButton.interactable = true;
                }
            }
        }

        // Change destination.
        private void SelectDestination(int _) => LoadSelected();
        // Retry the current request.
        private void Retry()
        {
            if (options == null || options.Count == 0)
            {
                Open();
            }
            else
            {
                LoadSelected();
            }
        }

        // Load the selected destination.
        private async void LoadSelected()
        {
            if (!IsOpen || options == null || destination.value < 0 || destination.value >= options.Count || string.IsNullOrEmpty(account.SignedInUserId))
            {
                return;
            }

            Cancel();
            Clear();
            int current = RequestRevision;
            var token = Begin();
            IsBusy = true;
            retryButton.interactable = false;
            status.text = "Finding places in " + options[destination.value].Name + "...";
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var result = await service.LoadAsync(options[destination.value].Id, token);
                if (!Current(current))
                {
                    return;
                }

                LastResult = result;
                View.Show(result, RequestReview, RequestSave, RequestTrack);
                status.text = CardCount == 0 ? "No places have been added to this destination yet. Try another destination." : "Sample prices and fictional demo reviews. Browse all four categories below.";
                LastLoadMilliseconds = elapsed.ElapsedMilliseconds;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                if (Current(current))
                {
                    status.text = "Places could not be loaded. Choose Retry or another destination.";
                }
            }
            finally
            {
                if (Current(current))
                {
                    IsBusy = false;
                    retryButton.interactable = true;
                }
            }
        }

        public void Back()
        {
            Cancel();
            Clear();
            hubCanvas.SetActive(false);
            flights.Resume();
        }

        private void Logout() => account.Logout();
        private void OnLoggedOut()
        {
            Cancel();
            Clear();
            hubCanvas.SetActive(false);
            options = null;
            service = null;
        }

        private void Clear()
        {
            ContentCleared?.Invoke();
            LastResult = null;
            LastLoadMilliseconds = 0;
            View.Clear();
        }

        private void RequestReview(PlaceOption place)
        {
            if (IsOpen && !IsBusy && LastResult != null)
            {
                ReviewRequested?.Invoke(place);
            }
        }

        private void RequestSave(PlaceOption place)
        {
            if (IsOpen && !IsBusy && LastResult != null)
            {
                SaveRequested?.Invoke(place);
            }
        }

        private void RequestTrack(PlaceOption place)
        {
            if (IsOpen && !IsBusy && LastResult != null && place.Category == PlaceCategory.Hotel)
            {
                TrackRequested?.Invoke(place);
            }
        }

        private CancellationToken Begin() => base.BeginScreenRequest();
        private void Cancel()
        {
            base.CancelScreenRequest();
            IsBusy = false;
        }

        private bool Current(int current) => base.IsRequestCurrent(current) && IsOpen && !string.IsNullOrEmpty(account.SignedInUserId);
        // Lifecycle cleanup.
        protected override void OnDestroy()
        {
            base.OnDestroy();
            ContentCleared?.Invoke();
            if (account)
            {
                account.LoggedOut -= OnLoggedOut;
            }

            if (destination)
            {
                destination.onValueChanged.RemoveListener(SelectDestination);
            }

            if (openButton)
            {
                openButton.onClick.RemoveListener(Open);
            }

            if (backButton)
            {
                backButton.onClick.RemoveListener(Back);
            }

            if (logoutButton)
            {
                logoutButton.onClick.RemoveListener(Logout);
            }

            if (retryButton)
            {
                retryButton.onClick.RemoveListener(Retry);
            }
        }
    }
}
