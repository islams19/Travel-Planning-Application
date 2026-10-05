using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using TravelPlanning.Destinations;
using TravelPlanning.Flights;
using TravelPlanning.Trips;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Flights;
using TravelPlanning.UI.Reviews;
using UnityEngine;

namespace TravelPlanning.UI.Trips
{
    /// <summary>One modal handles browsing trips and picking a trip for a selected flight or place.</summary>
    public sealed class SavedTripsPage : Shared.ScreenControllerBase
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
        private GameObject modalCanvas;
        [SerializeField]
        private CanvasGroup[] underlyingGroups;
        [Header("Buttons")]
        [SerializeField]
        private UnityEngine.UI.Button homeButton, flightButton, hubButton, closeButton, createButton, saveButton, retryButton;
        [Header("Input fields")]
        [SerializeField]
        private TMP_InputField tripName;
        [SerializeField]
        private TMP_Dropdown tripChoice, startDate, endDate;
        [Header("Results and presentation")]
        [SerializeField]
        private TMP_Text targetPreview, status, tripInfo;
        [SerializeField]
        private UnityEngine.UI.ScrollRect scroll;
        [SerializeField]
        private SavedTripRow rowPrefab;
        private SavedTripsView view;
        private SavedTripsView View => view ?? (view = new SavedTripsView(tripChoice, startDate, endDate, tripInfo, scroll, rowPrefab));

        private TripService service;
        private string ownerId, targetId, targetTitle;
        private SavedItemKind targetKind;
        private bool groupsLocked;
        private bool[] previousInteraction;
        public bool IsOpen => modalCanvas && modalCanvas.activeSelf;
        public bool IsBusy { get; private set; }
        public TripListResult LastList { get; private set; }
        public TripDetails LastDetails { get; private set; }
        public string Status => status.text;

        public void Configure(Shared.TravelSceneBindings scene)
        {
            account = scene.Account;
            flights = scene.Flights;
            hub = scene.Hub;
            reviews = scene.Reviews;
            underlyingGroups = scene.UnderlyingGroups;
            homeButton = scene.Button(scene.HomeCard, "SavedTripsButton");
            flightButton = scene.Button(scene.FlightCanvas.transform, "Main/Header/SavedTripsButton");
            hubButton = scene.Button(scene.HubCanvas.transform, "Main/Header/SavedTripsButton");
        }

        // Scene initialization and event wiring.
        private void Start()
        {
            if (!account || !flights || !hub || !reviews || !modalCanvas || !homeButton || !flightButton ||
                !hubButton || !closeButton || !createButton || !saveButton || !retryButton || !tripName || !tripChoice ||
                !startDate || !endDate || !targetPreview || !status || !tripInfo || !scroll || !rowPrefab || underlyingGroups == null ||
                underlyingGroups.Length != 3 || underlyingGroups.Any(group => !group))
            {
                Debug.LogError("SavedTripsPage: assign all references on SavedTripsController using the prepared scene.");
                enabled = false;
                return;
            }

            homeButton.onClick.AddListener(Browse);
            flightButton.onClick.AddListener(Browse);
            hubButton.onClick.AddListener(Browse);
            closeButton.onClick.AddListener(Close);
            createButton.onClick.AddListener(Create);
            saveButton.onClick.AddListener(Save);
            retryButton.onClick.AddListener(Open);
            tripChoice.onValueChanged.AddListener(SelectTrip);
            flights.SaveRequested += OpenFlight;
            hub.SaveRequested += OpenPlace;
            flights.ContentCleared += Close;
            hub.ContentCleared += Close;
            account.LoggedOut += Close;
        }

        // Browse saved data.
        public void Browse()
        {
            targetId = targetTitle = null;
            Open();
        }

        // Open from a flight card.
        private void OpenFlight(FlightOption flight)
        {
            targetKind = SavedItemKind.Flight;
            targetId = flight.Id;
            targetTitle = flight.AirlineName + " " + flight.FlightNumber + " • " + flight.OriginAirportId + " → " + flight.DestinationAirportId;
            Open();
        }

        // Open from a place card.
        private void OpenPlace(PlaceOption place)
        {
            targetKind = place.Category switch
            {
                PlaceCategory.Hotel => SavedItemKind.Hotel,
                PlaceCategory.Restaurant => SavedItemKind.Restaurant,
                PlaceCategory.Experience => SavedItemKind.Experience,
                _ => SavedItemKind.Hotspot
            };
            targetId = place.Id;
            targetTitle = place.Name;
            Open();
        }

        // Open screen.
        private async void Open()
        {
            if (!account.IsReady || string.IsNullOrEmpty(account.SignedInUserId))
            {
                return;
            }

            reviews.Close();
            Cancel();
            ClearRows();
            LastList = null;
            tripChoice.ClearOptions();
            ownerId = account.SignedInUserId;
            service = new TripService(account.Database, ownerId);
            LockUnderlying();
            modalCanvas.SetActive(true);
            targetPreview.text = targetId == null ? "Browse your saved plans on this computer." : "Save selected item: " + targetTitle;
            if (UnityEngine.EventSystems.EventSystem.current)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
            }

            int current = RequestRevision;
            var token = Begin();
            var requestService = service;
            SetBusy(true);
            status.text = "Loading your trips...";
            try
            {
                var result = await requestService.ListAsync(token);
                if (!Current(current))
                {
                    return;
                }

                LastList = result;
                View.PopulateDates(result.StartDate, result.EndDate);
                View.ShowChoices(LastList, null);
                if (result.Trips.Count == 0)
                {
                    status.text = "No trips yet. Enter a name and planning dates, then choose Create trip.";
                }
                else
                {
                    await LoadAndRender(requestService, result.Trips[0].Id, token, current);
                    if (Current(current))
                    {
                        status.text = targetId == null ? "Choose a trip to see its saved items." : "Choose a trip, then save the selected item.";
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                if (Current(current))
                {
                    status.text = "Your trips could not be loaded. Choose Retry.";
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

        // Select a saved trip.
        private async void SelectTrip(int index)
        {
            if (IsBusy || !IsOpen || LastList == null || index < 0 || index >= LastList.Trips.Count)
            {
                return;
            }

            Cancel();
            int current = RequestRevision;
            var token = Begin();
            var requestService = service;
            string tripId = LastList.Trips[index].Id;
            SetBusy(true);
            try
            {
                await LoadAndRender(requestService, tripId, token, current);
                if (Current(current))
                {
                    status.text = "Trip loaded. Prices reflect the current sample catalog.";
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                if (Current(current))
                {
                    status.text = "This trip could not be loaded. Choose Retry.";
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

        // Create trip.
        private async void Create()
        {
            if (IsBusy || !IsOpen || LastList == null)
            {
                return;
            }

            string name = tripName.text, start = startDate.options[startDate.value].text, end = endDate.options[endDate.value].text;
            await Write(async (requestService, token) =>
            {
                var trip = await requestService.CreateAsync(name, start, end, token);
                return (trip.Id, "Trip created. " + (targetId == null ? "Add flights and places using Save to trip." : "Now choose Save selected item."));
            });
        }

        // Save the selected item.
        private async void Save()
        {
            if (IsBusy || !IsOpen || LastDetails == null || string.IsNullOrEmpty(targetId))
            {
                return;
            }

            string tripId = LastDetails.Trip.Id, itemId = targetId;
            SavedItemKind kind = targetKind;
            await Write(async (requestService, token) =>
            {
                bool added = await requestService.SaveAsync(tripId, kind, itemId, token);
                return (tripId, added ? "Item saved to this trip." : "This item is already saved to this trip.");
            });
        }

        // Remove a saved item.
        private async void Remove(string tripId, string itemId)
        {
            if (IsBusy || !IsOpen || LastDetails?.Trip.Id != tripId)
            {
                return;
            }

            await Write(async (requestService, token) =>
            {
                bool removed = await requestService.RemoveAsync(tripId, itemId, token);
                return (tripId, removed ? "Item removed from this trip." : "That item is no longer in this trip.");
            });
        }

        // Run a saved-data action.
        private async Task Write(Func<TripService, CancellationToken, Task<(string tripId, string message)>> operation)
        {
            Cancel();
            int current = RequestRevision;
            var token = Begin();
            var requestService = service;
            SetBusy(true);
            status.text = "Saving your changes...";
            try
            {
                var result = await operation(requestService, token);
                if (!Current(current))
                {
                    return;
                }

                var list = await requestService.ListAsync(token);
                if (!Current(current))
                {
                    return;
                }

                LastList = list;
                View.ShowChoices(LastList, result.tripId);
                await LoadAndRender(requestService, result.tripId, token, current);
                if (Current(current))
                {
                    status.text = result.message;
                }
            }
            catch (OperationCanceledException)
            {
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
                    status.text = "The change could not be completed. Reload with Retry to check the saved state.";
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

        private async Task LoadAndRender(TripService requestService, string tripId, CancellationToken token, int current)
        {
            ClearRows();
            var details = await requestService.LoadAsync(tripId, token);
            if (!Current(current))
            {
                return;
            }

            ClearRows();
            LastDetails = details;
            View.Show(details, Remove);
        }

        private void LockUnderlying()
        {
            if (groupsLocked)
            {
                return;
            }

            previousInteraction = underlyingGroups.Select(group => group.interactable).ToArray();
            foreach (var group in underlyingGroups)
            {
                group.interactable = false;
            }

            groupsLocked = true;
        }

        // Close screen and clear account-specific state.
        public void Close()
        {
            Cancel();
            ClearRows();
            LastList = null;
            service = null;
            ownerId = targetId = targetTitle = null;
            if (tripChoice)
            {
                tripChoice.ClearOptions();
            }

            if (tripName)
            {
                tripName.text = "";
            }

            if (targetPreview)
            {
                targetPreview.text = "";
            }

            if (status)
            {
                status.text = "";
            }

            if (groupsLocked)
            {
                for (int i = 0; i < underlyingGroups.Length; i++)
                {
                    if (underlyingGroups[i])
                    {
                        underlyingGroups[i].interactable = previousInteraction[i];
                    }
                }

                groupsLocked = false;
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

        private void ClearRows()
        {
            View.Clear();
            LastDetails = null;
        }

        private CancellationToken Begin() => base.BeginScreenRequest();
        private void Cancel()
        {
            base.CancelScreenRequest();
            IsBusy = false;
        }

        private bool Current(int current) => base.IsRequestCurrent(current) && IsOpen && !string.IsNullOrEmpty(ownerId) && ownerId == account.SignedInUserId;
        private void SetBusy(bool busy)
        {
            IsBusy = busy;
            createButton.interactable = !busy && LastList != null;
            saveButton.interactable = !busy && targetId != null && LastDetails != null;
            retryButton.interactable = !busy;
            tripChoice.interactable = !busy && LastList?.Trips.Count > 0;
            tripName.interactable = startDate.interactable = endDate.interactable = !busy;
            View.SetRowsInteractable(!busy);
        }

        // Lifecycle cleanup.
        protected override void OnDestroy()
        {
            base.OnDestroy();
            Close();
            if (flights)
            {
                flights.SaveRequested -= OpenFlight;
                flights.ContentCleared -= Close;
            }

            if (hub)
            {
                hub.SaveRequested -= OpenPlace;
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

            if (createButton)
            {
                createButton.onClick.RemoveListener(Create);
            }

            if (saveButton)
            {
                saveButton.onClick.RemoveListener(Save);
            }

            if (retryButton)
            {
                retryButton.onClick.RemoveListener(Open);
            }

            if (tripChoice)
            {
                tripChoice.onValueChanged.RemoveListener(SelectTrip);
            }
        }
    }
}
