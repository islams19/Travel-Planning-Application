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
    public sealed class FlightSearchPage : Shared.ScreenControllerBase
    {
        [Header("Screen references")]
        [SerializeField]
        private LoginPage account;
        [SerializeField]
        private GameObject authCanvas, flightCanvas;
        [Header("Input fields")]
        [SerializeField]
        private TMP_Dropdown origin, destination, departureDate, returnDate, airline, timeBand, sort;
        [SerializeField]
        private TMP_InputField maximumPrice;
        [Header("Results and presentation")]
        [SerializeField]
        private TMP_Text status, outboundHeading, returnHeading;
        [Header("Buttons")]
        [SerializeField]
        private UnityEngine.UI.Button openButton, searchButton, clearButton, backButton, logoutButton;
        [Header("Results and presentation")]
        [SerializeField]
        private RectTransform outboundContent, returnContent;
        [SerializeField]
        private UnityEngine.UI.ScrollRect outboundScroll, returnScroll;
        [SerializeField]
        private FlightResultRow rowPrefab;
        private FlightSearchService service;
        private FlightSearchOptions options;
        private bool resumeInterruptedSearch;
        private FlightSearchView view;
        private FlightSearchView View => view ?? (view = new FlightSearchView(origin, destination, departureDate,
            returnDate, airline, timeBand, sort, maximumPrice, outboundHeading, returnHeading, status, outboundContent,
            returnContent, outboundScroll, returnScroll, rowPrefab));
        public bool IsBusy { get; private set; }
        public bool IsOpen => flightCanvas && flightCanvas.activeSelf;
        public FlightSearchResult LastResult { get; private set; }
        public long LastSearchMilliseconds { get; private set; }
        public string Status => status.text;

        public event Action<FlightOption> SaveRequested;
        public event Action<FlightOption> TrackRequested;
        public event Action ContentCleared;
        public string SelectedDestinationId => options != null && destination.value >= 0 && destination.value < options.Airports.Count ? options.Airports[destination.value].DestinationId : null;

        public void Configure(Shared.TravelSceneBindings scene)
        {
            account = scene.Account;
            authCanvas = scene.HomeCanvas;
            openButton = scene.Button(scene.HomeCard, "SearchFlightsButton");
        }

        // Scene initialization and event wiring.
        private void Start()
        {
            if (!account || !authCanvas || !flightCanvas || !origin || !destination || !departureDate || !returnDate ||
                !airline || !timeBand || !sort || !maximumPrice || !status || !outboundHeading || !returnHeading || !openButton ||
                !searchButton || !clearButton || !backButton || !logoutButton || !outboundContent || !returnContent || !outboundScroll ||
                !returnScroll || !rowPrefab)
            {
                Debug.LogError("FlightSearchPage: assign all references on FlightController using the prepared scene.");
                enabled = false;
                return;
            }

            openButton.onClick.AddListener(Open);
            searchButton.onClick.AddListener(Search);
            clearButton.onClick.AddListener(ClearFilters);
            backButton.onClick.AddListener(Close);
            logoutButton.onClick.AddListener(Logout);
            account.LoggedOut += OnLoggedOut;
            account.LoggedIn += OnLoggedIn;
        }

        // Open screen.
        public async void Open()
        {
            if (!account.IsReady || string.IsNullOrEmpty(account.SignedInUserId))
            {
                return;
            }

            CancelRequest();
            flightCanvas.SetActive(true);
            authCanvas.SetActive(false);
            ClearRows();
            service = new FlightSearchService(account.Database);
            int current = RequestRevision;
            var token = BeginRequest();
            SetBusy(true);
            status.text = "Loading airports and travel dates...";
            try
            {
                var loaded = await service.LoadOptionsAsync(token);
                if (!Current(current))
                {
                    return;
                }

                options = loaded;
                View.PopulateOptions(options);
                SetBusy(false);
                Search();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                if (Current(current))
                {
                    status.text = "We could not open flight search. Please go back and try again.";
                    SetBusy(false);
                }
            }
        }

        // Search flights.
        public async void Search()
        {
            if (!IsOpen || IsBusy || options == null || string.IsNullOrEmpty(account.SignedInUserId))
            {
                return;
            }

            CancelRequest();
            int current = RequestRevision;
            var token = BeginRequest();
            SetBusy(true);
            ClearRows();
            status.text = "Searching the sample flight catalog...";
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
                    TimeBand = (DepartureTimeBand)timeBand.value,
                    Sort = (FlightSort)sort.value
                };
                var result = await service.SearchAsync(request, token);
                if (!Current(current))
                {
                    return;
                }

                LastResult = result;
                View.Show(result, request, RequestSave, RequestTrack);
                LastSearchMilliseconds = elapsed.ElapsedMilliseconds;
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
                    status.text = "Flight search is unavailable right now. Please try again.";
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

        public static int? ParseMaximumPrice(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            text = text.Trim();
            if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal price) ||
                price < 0 || price > 21474836.47m || decimal.Round(price, 2) != price)
            {
                throw new ArgumentException("Enter a maximum price in USD with up to two decimal places, for example 527.00.");
            }

            return (int)(price * 100m);
        }

        // Clear search filters.
        public void ClearFilters()
        {
            if (IsBusy || options == null)
            {
                return;
            }

            maximumPrice.text = "";
            airline.value = timeBand.value = sort.value = 0;
            Search();
        }

        // Close screen and clear account-specific state.
        public void Close()
        {
            resumeInterruptedSearch = false;
            CancelRequest();
            ClearRows();
            flightCanvas.SetActive(false);
            authCanvas.SetActive(!string.IsNullOrEmpty(account.SignedInUserId) || authCanvas.scene == account.gameObject.scene);
        }

        /// <summary>Temporarily hides this screen while keeping the current form and completed offers.</summary>
        public void Suspend()
        {
            resumeInterruptedSearch = IsBusy;
            CancelRequest();
            flightCanvas.SetActive(false);
        }

        public void Resume()
        {
            if (string.IsNullOrEmpty(account.SignedInUserId))
            {
                return;
            }

            flightCanvas.SetActive(true);
            authCanvas.SetActive(false);
            if (options == null)
            {
                Open();
                return;
            }

            bool retry = resumeInterruptedSearch;
            resumeInterruptedSearch = false;
            if (retry)
            {
                Search();
            }
        }

        private void Logout() => account.Logout();
        private void OnLoggedOut()
        {
            Close();
            options = null;
            service = null;
            status.text = "";
        }

        private void OnLoggedIn(string _)
        {
            CancelRequest();
            ClearRows();
        }

        private void RequestSave(FlightOption flight)
        {
            if (IsOpen && !IsBusy && LastResult != null)
            {
                SaveRequested?.Invoke(flight);
            }
        }

        private void RequestTrack(FlightOption flight)
        {
            if (IsOpen && !IsBusy && LastResult != null)
            {
                TrackRequested?.Invoke(flight);
            }
        }

        private void ClearRows()
        {
            ContentCleared?.Invoke();
            View.Clear();
            LastResult = null;
            LastSearchMilliseconds = 0;
        }

        private CancellationToken BeginRequest() => base.BeginScreenRequest();
        private void CancelRequest()
        {
            base.CancelScreenRequest();
            SetBusy(false);
        }

        private bool Current(int current) => base.IsRequestCurrent(current) && IsOpen && !string.IsNullOrEmpty(account.SignedInUserId);
        private void SetBusy(bool value)
        {
            IsBusy = value;
            if (!searchButton)
            {
                return;
            }

            searchButton.interactable = !value;
            if (clearButton)
            {
                clearButton.interactable = !value;
            }

            foreach (var control in new UnityEngine.UI.Selectable[]
            {
                origin,
                destination,
                departureDate,
                returnDate,
                airline,
                timeBand,
                sort,
                maximumPrice
            }

            )
            {
                if (control)
                {
                    control.interactable = !value;
                }
            }
        }

        // Lifecycle cleanup.
        protected override void OnDestroy()
        {
            base.OnDestroy();
            ContentCleared?.Invoke();
            if (account)
            {
                account.LoggedOut -= OnLoggedOut;
                account.LoggedIn -= OnLoggedIn;
            }

            if (openButton)
            {
                openButton.onClick.RemoveListener(Open);
            }

            if (searchButton)
            {
                searchButton.onClick.RemoveListener(Search);
            }

            if (clearButton)
            {
                clearButton.onClick.RemoveListener(ClearFilters);
            }

            if (backButton)
            {
                backButton.onClick.RemoveListener(Close);
            }

            if (logoutButton)
            {
                logoutButton.onClick.RemoveListener(Logout);
            }
        }
    }
}
