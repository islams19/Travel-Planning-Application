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
    public sealed class ReviewsPage : Shared.ScreenControllerBase
    {
        [Header("Screen references")]
        [SerializeField]
        private LoginPage account;
        [SerializeField]
        private DestinationHubPage hub;
        [SerializeField]
        private GameObject overlayCanvas;
        [Header("Results and presentation")]
        [SerializeField]
        private TMP_Text placeName, summary, status;
        [Header("Buttons")]
        [SerializeField]
        private UnityEngine.UI.Button closeButton, retryButton, mapsButton;
        [Header("Results and presentation")]
        [SerializeField]
        private UnityEngine.UI.ScrollRect scroll;
        [SerializeField]
        private ReviewRow rowPrefab;
        private ReviewsView view;
        private ReviewsView View => view ?? (view = new ReviewsView(placeName, summary, status, scroll, rowPrefab));

        private PlaceOption selectedPlace;
        public bool IsBusy { get; private set; }
        public bool IsOpen => overlayCanvas && overlayCanvas.activeSelf;
        public ReviewDetails LastResult { get; private set; }
        public long LastLoadMilliseconds { get; private set; }
        // Tests replace only this boundary; normal clicks use the system browser.
        public Action<string> UrlOpener { get; set; } = Application.OpenURL;

        public void Configure(Shared.TravelSceneBindings scene)
        {
            account = scene.Account;
            hub = scene.Hub;
        }

        // Scene initialization and event wiring.
        private void Start()
        {
            if (!account || !hub || !overlayCanvas || !placeName || !summary || !status || !closeButton || !retryButton || !mapsButton || !scroll || !rowPrefab)
            {
                Debug.LogError("ReviewsPage: assign all references on ReviewsController using the prepared scene.");
                enabled = false;
                return;
            }

            hub.ReviewRequested += Open;
            hub.ContentCleared += Close;
            account.LoggedOut += Close;
            closeButton.onClick.AddListener(Close);
            retryButton.onClick.AddListener(Retry);
            mapsButton.onClick.AddListener(OpenMaps);
        }

        // Open screen.
        private async void Open(PlaceOption place)
        {
            if (place == null || !hub.IsOpen || string.IsNullOrEmpty(account.SignedInUserId))
            {
                return;
            }

            Cancel();
            ClearRows();
            selectedPlace = place;
            overlayCanvas.SetActive(true);
            placeName.text = place.Name;
            if (UnityEngine.EventSystems.EventSystem.current)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
            }

            int current = RequestRevision;
            var token = base.BeginScreenRequest();
            IsBusy = true;
            summary.text = "";
            status.text = "Loading traveler reviews...";
            mapsButton.interactable = retryButton.interactable = false;
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var result = await new ReviewService(account.Database).LoadAsync(place.Category, place.Id, token);
                if (!Current(current))
                {
                    return;
                }

                LastResult = result;
                View.Show(result);
                mapsButton.interactable = GoogleMapsReference.TryGetUrl(result.Place.GoogleMapsUrl, out _);
                LastLoadMilliseconds = elapsed.ElapsedMilliseconds;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                if (Current(current))
                {
                    status.text = "Reviews could not be loaded. Choose Retry or close this window.";
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

        // Retry the current request.
        private void Retry()
        {
            if (selectedPlace != null)
            {
                Open(selectedPlace);
            }
        }

        // Open the validated map link.
        private void OpenMaps()
        {
            if (!IsOpen || IsBusy || LastResult == null)
            {
                return;
            }

            if (!GoogleMapsReference.TryGetUrl(LastResult.Place.GoogleMapsUrl, out string url))
            {
                status.text = "A Google Maps search link is not available for this place.";
                mapsButton.interactable = false;
                return;
            }

            try
            {
                UrlOpener(url);
                status.text = "Opened the Google Maps search in your browser. Its results are separate from these demo reviews.";
            }
            catch (Exception)
            {
                status.text = "Your browser could not be opened. Please try the Google Maps search button again.";
            }
        }

        // Close screen and clear account-specific state.
        public void Close()
        {
            Cancel();
            ClearRows();
            selectedPlace = null;
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events && events.currentSelectedGameObject && overlayCanvas && events.currentSelectedGameObject.transform.IsChildOf(overlayCanvas.transform))
            {
                events.SetSelectedGameObject(null);
            }

            if (overlayCanvas)
            {
                overlayCanvas.SetActive(false);
            }
        }

        private void ClearRows()
        {
            View.Clear();
            LastResult = null;
            LastLoadMilliseconds = 0;
        }

        private void Cancel()
        {
            base.CancelScreenRequest();
            IsBusy = false;
        }

        private bool Current(int current) => base.IsRequestCurrent(current) && IsOpen && hub.IsOpen && !string.IsNullOrEmpty(account.SignedInUserId);
        // Lifecycle cleanup.
        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (hub)
            {
                hub.ReviewRequested -= Open;
                hub.ContentCleared -= Close;
            }

            if (account)
            {
                account.LoggedOut -= Close;
            }

            if (closeButton)
            {
                closeButton.onClick.RemoveListener(Close);
            }

            if (retryButton)
            {
                retryButton.onClick.RemoveListener(Retry);
            }

            if (mapsButton)
            {
                mapsButton.onClick.RemoveListener(OpenMaps);
            }
        }
    }
}
