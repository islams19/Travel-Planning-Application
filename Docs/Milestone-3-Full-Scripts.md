# Milestone 3 — Complete source files
These files are installed in the project. Do not paste duplicate classes into Assets. The guide has Editor and Inspector instructions. Existing Milestone 1B database and Milestone 2 authentication code remain dependencies; the extended LoginPage is included here in full.

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

## Assets/TravelPlanning/Runtime/Flights/FlightSearchQuery.cs

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using SQLite;

namespace TravelPlanning.Flights
{
    /// <summary>Builds only fixed SQL clauses; user values are always parameters.</summary>
    internal static class FlightSearchQuery
    {
        private const string RouteSql =
            "SELECT f.id AS Id, f.airline_id AS AirlineId, a.name AS AirlineName, f.flight_number AS FlightNumber, " +
            "f.origin_airport_id AS OriginAirportId, f.destination_airport_id AS DestinationAirportId, " +
            "od.name AS OriginCity, dd.name AS DestinationCity, oa.time_zone AS OriginTimeZoneId, da.time_zone AS DestinationTimeZoneId, " +
            "f.departure_utc AS DepartureUtcText, f.arrival_utc AS ArrivalUtcText, f.price_cents AS PriceCents, " +
            "f.available_seats AS AvailableSeats, f.currency AS Currency " +
            "FROM flights f JOIN airlines a ON a.id=f.airline_id " +
            "JOIN airports oa ON oa.id=f.origin_airport_id JOIN airports da ON da.id=f.destination_airport_id " +
            "JOIN destinations od ON od.id=oa.destination_id JOIN destinations dd ON dd.id=da.destination_id " +
            "WHERE f.origin_airport_id=? AND f.destination_airport_id=? AND f.departure_local_date=?";

        internal static string Build(FlightSearchRequest request, bool returning, out object[] arguments)
        {
            var values = new List<object>
            {
                returning ? request.DestinationAirportId : request.OriginAirportId,
                returning ? request.OriginAirportId : request.DestinationAirportId,
                returning ? request.ReturnDate : request.DepartureDate
            };
            string sql = RouteSql;
            if (request.MaxPriceCents.HasValue) { sql += " AND f.price_cents<=?"; values.Add(request.MaxPriceCents.Value); }
            if (request.AirlineId != null) { sql += " AND f.airline_id=?"; values.Add(request.AirlineId); }
            arguments = values.ToArray();
            return sql;
        }

        internal static FlightOption ConvertRow(FlightRow row, Dictionary<string, TimeZoneInfo> zones)
        {
            DateTime departure = ParseUtc(row.DepartureUtcText);
            DateTime arrival = ParseUtc(row.ArrivalUtcText);
            return new FlightOption
            {
                Id = row.Id, AirlineId = row.AirlineId, AirlineName = row.AirlineName, FlightNumber = row.FlightNumber,
                OriginAirportId = row.OriginAirportId, DestinationAirportId = row.DestinationAirportId,
                OriginCity = row.OriginCity, DestinationCity = row.DestinationCity,
                OriginTimeZoneId = row.OriginTimeZoneId, DestinationTimeZoneId = row.DestinationTimeZoneId,
                DepartureUtc = departure, ArrivalUtc = arrival,
                DepartureLocal = TimeZoneInfo.ConvertTimeFromUtc(departure, Zone(row.OriginTimeZoneId, zones)),
                ArrivalLocal = TimeZoneInfo.ConvertTimeFromUtc(arrival, Zone(row.DestinationTimeZoneId, zones)),
                PriceCents = row.PriceCents, AvailableSeats = row.AvailableSeats, Currency = row.Currency
            };
        }

        private static TimeZoneInfo Zone(string id, Dictionary<string, TimeZoneInfo> zones)
        {
            if (!zones.TryGetValue(id, out var zone))
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById(id);
                zones.Add(id, zone);
            }
            return zone;
        }

        private static DateTime ParseUtc(string value) => DateTime.ParseExact(value, "yyyy-MM-ddTHH:mm:ssZ",
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    // Public setters/constructors let sqlite-net map SQL aliases onto simple rows.
    public sealed class FlightRow
    {
        public string Id { get; set; }
        public string AirlineId { get; set; }
        public string AirlineName { get; set; }
        public string FlightNumber { get; set; }
        public string OriginAirportId { get; set; }
        public string DestinationAirportId { get; set; }
        public string OriginCity { get; set; }
        public string DestinationCity { get; set; }
        public string OriginTimeZoneId { get; set; }
        public string DestinationTimeZoneId { get; set; }
        public string DepartureUtcText { get; set; }
        public string ArrivalUtcText { get; set; }
        public int PriceCents { get; set; }
        public int AvailableSeats { get; set; }
        public string Currency { get; set; }
    }

    public sealed class FlightQueryPlanRow
    {
        [Column("detail")] public string Detail { get; set; }
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
            var airports = connection.Query<AirportOption>("SELECT a.id AS Id, d.name AS City, a.name AS Name, a.time_zone AS TimeZoneId FROM airports a JOIN destinations d ON d.id=a.destination_id ORDER BY d.name, a.id");
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
        private readonly List<GameObject> rows = new List<GameObject>();
        public bool IsBusy { get; private set; }
        public bool IsOpen => flightCanvas && flightCanvas.activeSelf;
        public FlightSearchResult LastResult { get; private set; }
        public long LastSearchMilliseconds { get; private set; }
        public string Status => status.text;

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
        public void Close() { CancelRequest(); ClearRows(); flightCanvas.SetActive(false); authCanvas.SetActive(true); }
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

## Assets/TravelPlanning/UI/Flights/FlightResultRow.cs

```csharp
using System.Globalization;
using TMPro;
using TravelPlanning.Flights;
using UnityEngine;

namespace TravelPlanning.UI.Flights
{
    /// <summary>Formats one seeded offer; every displayed time belongs to that airport's local time zone.</summary>
    public sealed class FlightResultRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text airlineText, routeText, priceText;
        public void Show(FlightOption flight)
        {
            airlineText.text = flight.AirlineName + " • " + flight.FlightNumber;
            routeText.text = flight.OriginAirportId + " " + flight.DepartureLocal.ToString("dd MMM HH:mm", CultureInfo.InvariantCulture)
                + " → " + flight.DestinationAirportId + " " + flight.ArrivalLocal.ToString("dd MMM HH:mm", CultureInfo.InvariantCulture)
                + "\nTimes local to each airport • " + flight.AvailableSeats + " sample seats";
            priceText.text = "USD " + (flight.PriceCents / 100m).ToString("N2", CultureInfo.InvariantCulture);
        }
    }
}
```

## Assets/TravelPlanning/UI/Flights/FlightSmokeRunner.cs

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace TravelPlanning.UI.Flights
{
    /// <summary>Development-only checks drive the real controls using a separate disposable demo account.</summary>
    public sealed class FlightSmokeRunner : MonoBehaviour
    {
        [SerializeField] private LoginPage account;
        [SerializeField] private FlightSearchPage flights;
        [SerializeField] private GameObject authCanvas, flightCanvas;
        private int frames;
        private void Update() => frames++;
        private async void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-flightSmoke") < 0) return;
            try
            {
                int fileIndex = Array.IndexOf(args, "-authFile");
                string file = fileIndex >= 0 && fileIndex + 1 < args.Length ? args[fileIndex + 1] : "";
                if (!Debug.isDebugBuild || !file.StartsWith("auth-smoke-", StringComparison.Ordinal) || !file.EndsWith(".db", StringComparison.Ordinal) || System.IO.Path.GetFileName(file) != file)
                    throw new InvalidOperationException("Flight smoke checks require an isolated test database.");
                await RunChecksAsync(Array.IndexOf(args, "-flightReopen") < 0);
                Debug.Log("FLIGHT_UI_SMOKE_PASS"); Application.Quit(0);
            }
            catch (Exception exception) { Debug.LogError("FLIGHT_UI_SMOKE_FAIL " + exception.GetType().Name + ": " + exception.Message); Application.Quit(1); }
        }
        public async Task RunChecksAsync(bool register)
        {
            await WaitUntil(() => account.IsReady, "Account database startup");
            string email = "flight-smoke@example.test";
            string password = "Travel flight demo phrase 2027!";
            Transform right = authCanvas.transform.Find("Right Panel");
            var login = right.Find("LoginCard"); var registration = right.Find("RegistrationCard"); var signedIn = right.Find("SignedInCard");
            if (register)
            {
                Click(login, "Create an account");
                Input(registration, "EmailInput").text = email; Input(registration, "PasswordInput").text = Input(registration, "ConfirmationInput").text = password;
                Click(registration, "LoginButton"); await WaitUntil(() => !account.IsBusy, "Account registration");
                Require(login.gameObject.activeSelf, "Registration did not return to sign in.");
            }
            await Login(login, email, password);
            Click(signedIn, "SearchFlightsButton"); await WaitUntil(() => !flights.IsBusy && flights.LastResult != null, "Initial flight search");
            Require(flights.LastResult.Outbound.Count == 2 && flights.LastResult.Return.Count == 2, "Expected two seeded offers in each direction.");
            Require(flights.LastSearchMilliseconds <= 3000, "Flight search exceeded three seconds.");
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            Require(flights.LastResult.WorkerThreadId != mainThread, "Flight database work ran on the UI thread.");
            Debug.Log("FLIGHT_SEARCH_TIMING uiMs=" + flights.LastSearchMilliseconds + " databaseMs=" + flights.LastResult.ElapsedMilliseconds + " worker=" + flights.LastResult.WorkerThreadId + " main=" + mainThread);
            Transform main = flightCanvas.transform.Find("Main");
            var airline = Dropdown(main, "FilterFields/Airline/Dropdown");
            string chosenAirline = flights.LastResult.Outbound[0].AirlineName;
            airline.value = airline.options.FindIndex(option => option.text == chosenAirline);
            Require(airline.value > 0, "The matching airline was not listed.");
            await Search(main);
            Require(flights.LastResult.Outbound.Count > 0 && flights.LastResult.Outbound.All(x => x.AirlineName == chosenAirline), "Airline filter failed.");
            Click(main, "Actions/ClearFiltersButton"); await WaitUntil(() => !flights.IsBusy, "Clear airline filter");
            Dropdown(main, "FilterFields/Sort/Dropdown").value = 1;
            await Search(main);
            Require(flights.LastResult.Outbound.First().PriceCents >= flights.LastResult.Outbound.Last().PriceCents, "Descending price sort failed.");
            Input(main, "FilterFields/MaximumPrice/Input").text = "0";
            await Search(main);
            Require(flights.LastResult.Outbound.Count == 0 && flights.LastResult.Return.Count == 0, "Zero-price filter should be empty.");
            Click(main, "Actions/ClearFiltersButton"); await WaitUntil(() => !flights.IsBusy, "Clear filters");
            Require(flights.LastResult.Outbound.Count == 2 && flights.LastResult.Return.Count == 2, "Clear filters did not restore offers.");
            Dropdown(main, "FilterFields/TimeBand/Dropdown").value = 2;
            await Search(main);
            Require(flights.LastResult.Outbound.Count > 0 && flights.LastResult.Outbound.All(x => x.DepartureLocal.Hour >= 6 && x.DepartureLocal.Hour < 12), "Morning filter failed.");
            Click(main, "Actions/ClearFiltersButton"); await WaitUntil(() => !flights.IsBusy, "Clear time filter");
            var destination = Dropdown(main, "RouteFields/Destination/Dropdown"); int destinationValue = destination.value;
            destination.value = Dropdown(main, "RouteFields/Origin/Dropdown").value;
            await Search(main);
            Require(flights.LastResult == null && !string.IsNullOrEmpty(flights.Status), "Same-airport validation failed.");
            destination.value = destinationValue;
            int unseededDestination = destination.options.FindIndex(option => option.text.Contains("(HND)"));
            Require(unseededDestination >= 0, "Tokyo airport was not listed.");
            destination.value = unseededDestination;
            await Search(main);
            Require(flights.LastResult != null && flights.LastResult.Outbound.Count == 0 && flights.LastResult.Return.Count == 0, "The unseeded direct route should show an empty result.");
            destination.value = destinationValue;
            // Hold the database gate so closing the screen has a deterministic in-flight request to cancel.
            using (var release = new ManualResetEventSlim(false))
            {
                var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var blocker = account.Database.ExecuteAsync(connection => { entered.SetResult(true); if (!release.Wait(10000)) throw new TimeoutException("Smoke database gate was not released."); return true; }, CancellationToken.None);
                try
                {
                    await entered.Task;
                    int before = frames;
                    Click(main, "Actions/SearchButton");
                    await Task.Delay(60);
                    Require(flights.IsBusy && frames > before, "UI did not keep updating while the database was occupied.");
                    Click(main, "Header/LogoutButton");
                    Require(!flights.IsOpen && account.SignedInUserId == null && flights.LastResult == null, "Logout did not close and clear flight search.");
                }
                finally { release.Set(); }
                await blocker;
                await Task.Delay(30);
                Require(!flights.IsOpen && flights.LastResult == null, "A cancelled request displayed stale results.");
            }
            await Login(login, email, password);
            Click(signedIn, "SearchFlightsButton"); await WaitUntil(() => !flights.IsBusy && flights.LastResult != null, "Search after re-login");
            Require(flights.LastResult.Outbound.Count == 2, "Search failed after cancelling the previous session.");
            Click(main, "Header/BackButton"); Require(!flights.IsOpen && signedIn.gameObject.activeInHierarchy, "Back did not return to the signed-in screen.");
            Click(signedIn, "LogoutButton");
        }
        private async Task Login(Transform login, string email, string password)
        {
            Input(login, "EmailInput").text = email; Input(login, "PasswordInput").text = password;
            Click(login, "LoginButton"); await WaitUntil(() => !account.IsBusy, "Account sign in");
            Require(account.SignedInEmail == email, "Sign in failed.");
        }
        private async Task Search(Transform main) { Click(main, "Actions/SearchButton"); await WaitUntil(() => !flights.IsBusy, "Flight search"); }
        private static void Click(Transform root, string path) => root.Find(path).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        private static TMP_InputField Input(Transform root, string path) => root.Find(path).GetComponent<TMP_InputField>();
        private static TMP_Dropdown Dropdown(Transform root, string path) => root.Find(path).GetComponent<TMP_Dropdown>();
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static async Task WaitUntil(Func<bool> predicate, string operation)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!predicate()) { if (watch.ElapsedMilliseconds > 45000) throw new TimeoutException(operation); await Task.Delay(10); }
        }
    }
}
```

## Assets/TravelPlanning/UI/Editor/FlightSearchSetup.cs

```csharp
using System;
using System.IO;
using TMPro;
using TravelPlanning.UI.Flights;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Adds the independent flight screen without changing the login form.</summary>
    public static class FlightSearchSetup
    {
        public const string ScenePath = AuthSetup.ScenePath;
        public const string BuildPath = "Builds/FlightSearch/TravelPlannerFlights.exe";
        public const string RowPath = "Assets/TravelPlanning/Prefabs/Flights/FlightResultRow.prefab";
        private static readonly Color Blue = new Color32(49, 87, 255, 255);
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        [MenuItem("Travel Planning/Flight Search/1 - Prepare Flight Search")]
        public static void Prepare()
        {
            AuthSetup.Prepare();
            if (UnityEngine.Object.FindFirstObjectByType<FlightSearchPage>() != null) return;
            var account = UnityEngine.Object.FindFirstObjectByType<LoginPage>();
            var authCanvas = GameObject.Find("Canvas");
            var accountFields = new SerializedObject(account);
            var signedIn = ((GameObject)accountFields.FindProperty("signedInPanel").objectReferenceValue).transform;
            var next = signedIn.Find("NextMilestone").GetComponent<TMP_Text>(); next.text = "Search sample flights for your next trip.";
            next.rectTransform.anchoredPosition = new Vector2(0, -270); next.rectTransform.sizeDelta = new Vector2(580, 65);
            var open = UnityEngine.Object.Instantiate(signedIn.Find("LogoutButton").gameObject, signedIn).GetComponent<UnityEngine.UI.Button>();
            open.name = "SearchFlightsButton"; open.GetComponentInChildren<TMP_Text>().text = "Search flights";
            var openRect = open.GetComponent<RectTransform>(); openRect.anchoredPosition = new Vector2(openRect.anchoredPosition.x, -380);
            var canvasObject = new GameObject("FlightCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<Canvas>().sortingOrder = 2;
            var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0;
            var background = Rect("Background", canvasObject.transform); Stretch(background); background.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.white;
            var main = Rect("Main", canvasObject.transform); Stretch(main); main.offsetMin = new Vector2(40, 32); main.offsetMax = new Vector2(-40, -32);
            Vertical(main, 18);
            var header = Horizontal("Header", main, 68);
            var title = Label(header, "Title", "Flight search", 42); Layout(title.gameObject, -1, 1);
            var back = Button(header, "BackButton", "Back", 160);
            var logout = Button(header, "LogoutButton", "Log out", 160);
            var note = Label(main, "CatalogNote", "Sample catalog • Fixed June 2027 dates • Prices in USD per flight • Times local to each airport", 22); Layout(note.gameObject, 36);
            var route = Horizontal("RouteFields", main, 90);
            var origin = Dropdown(route, "Origin", "Origin airport"); var destination = Dropdown(route, "Destination", "Destination airport");
            var departure = Dropdown(route, "DepartureDate", "Departure date"); var returning = Dropdown(route, "ReturnDate", "Return date");
            var filters = Horizontal("FilterFields", main, 90);
            var priceCell = Cell(filters, "MaximumPrice", "Maximum USD per flight");
            var maxPriceObject = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources()); maxPriceObject.name = "Input"; maxPriceObject.transform.SetParent(priceCell, false); Layout(maxPriceObject, 54);
            var maxPrice = maxPriceObject.GetComponent<TMP_InputField>(); maxPrice.characterLimit = 20; maxPrice.contentType = TMP_InputField.ContentType.Standard; maxPriceObject.GetComponent<UnityEngine.UI.Image>().color = new Color32(244, 247, 255, 255); ((TMP_Text)maxPrice.placeholder).text = "Any price"; Style(maxPriceObject);
            var airline = Dropdown(filters, "Airline", "Airline"); var time = Dropdown(filters, "TimeBand", "Departure time"); var sort = Dropdown(filters, "Sort", "Sort both directions");
            var actions = Horizontal("Actions", main, 56); var search = Button(actions, "SearchButton", "Search flights", 250); var clear = Button(actions, "ClearFiltersButton", "Clear filters", 220);
            var help = Label(actions, "ApplyHint", "Change the filters, then choose Search flights.", 22); Layout(help.gameObject, -1, 1);
            var status = Label(main, "Status", "", 23); Layout(status.gameObject, 60);
            var lists = Horizontal("Results", main, -1); Layout(lists.gameObject, -1, 1);
            var outbound = Results(lists, "Outbound", out TMP_Text outboundHeading, out RectTransform outboundContent);
            var inbound = Results(lists, "Return", out TMP_Text returnHeading, out RectTransform returnContent);
            var controller = new GameObject("FlightController").AddComponent<FlightSearchPage>();
            var fields = new SerializedObject(controller);
            Set(fields, "account", account); Set(fields, "authCanvas", authCanvas); Set(fields, "flightCanvas", canvasObject);
            Set(fields, "origin", origin); Set(fields, "destination", destination); Set(fields, "departureDate", departure); Set(fields, "returnDate", returning); Set(fields, "airline", airline); Set(fields, "timeBand", time); Set(fields, "sort", sort); Set(fields, "maximumPrice", maxPrice);
            Set(fields, "status", status); Set(fields, "outboundHeading", outboundHeading); Set(fields, "returnHeading", returnHeading);
            Set(fields, "openButton", open); Set(fields, "searchButton", search); Set(fields, "clearButton", clear); Set(fields, "backButton", back); Set(fields, "logoutButton", logout);
            Set(fields, "outboundContent", outboundContent); Set(fields, "returnContent", returnContent); Set(fields, "outboundScroll", outbound); Set(fields, "returnScroll", inbound); Set(fields, "rowPrefab", CreateRow()); fields.ApplyModifiedPropertiesWithoutUndo();
            var smoke = controller.gameObject.AddComponent<FlightSmokeRunner>(); var smokeFields = new SerializedObject(smoke); Set(smokeFields, "account", account); Set(smokeFields, "flights", controller); Set(smokeFields, "authCanvas", authCanvas); Set(smokeFields, "flightCanvas", canvasObject); smokeFields.ApplyModifiedPropertiesWithoutUndo();
            canvasObject.SetActive(false);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("FLIGHT_SEARCH_SCENE_READY " + ScenePath);
        }
        [MenuItem("Travel Planning/Flight Search/2 - Build Windows x64")]
        public static void BuildWindows()
        {
            Prepare(); Directory.CreateDirectory(Path.GetDirectoryName(BuildPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = BuildPath, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Flight search build failed.");
            var notices = Path.Combine(Path.GetDirectoryName(BuildPath), "ThirdPartyNotices"); Directory.CreateDirectory(notices);
            foreach (string file in Directory.GetFiles("Assets/Plugins/SQLite/Licenses")) if (!file.EndsWith(".meta")) File.Copy(file, Path.Combine(notices, Path.GetFileName(file)), true);
            File.Copy("Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt", Path.Combine(notices, "LiberationSans-OFL.txt"), true);
            File.Copy("Assets/Plugins/LiteDB/LICENSE.txt", Path.Combine(notices, "LiteDB-LICENSE.txt"), true);
            Debug.Log("FLIGHT_SEARCH_BUILD_PASS " + Path.GetFullPath(BuildPath));
        }
        private static FlightResultRow CreateRow()
        {
            if (File.Exists(RowPath)) return AssetDatabase.LoadAssetAtPath<GameObject>(RowPath).GetComponent<FlightResultRow>();
            Directory.CreateDirectory(Path.GetDirectoryName(RowPath));
            var root = Rect("FlightResultRow", null); root.sizeDelta = new Vector2(850, 140); root.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(244, 247, 255, 255);
            var layout = Vertical(root, 8); layout.padding = new RectOffset(18, 18, 14, 14); Layout(root.gameObject, 140);
            var top = Horizontal("Top", root, 38); var airline = Label(top, "Airline", "Airline", 26); Layout(airline.gameObject, -1, 1); var price = Label(top, "Price", "USD 0.00", 26); Layout(price.gameObject, -1, 0, 220); price.alignment = TextAlignmentOptions.Right;
            var route = Label(root, "Route", "Route", 22); Layout(route.gameObject, 64);
            var row = root.gameObject.AddComponent<FlightResultRow>(); var fields = new SerializedObject(row); Set(fields, "airlineText", airline); Set(fields, "priceText", price); Set(fields, "routeText", route); fields.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, RowPath); UnityEngine.Object.DestroyImmediate(root.gameObject); return prefab.GetComponent<FlightResultRow>();
        }
        private static UnityEngine.UI.ScrollRect Results(Transform parent, string name, out TMP_Text heading, out RectTransform content)
        {
            var column = Rect(name, parent); Layout(column.gameObject, -1, 1); Vertical(column, 12); heading = Label(column, "Heading", name, 24); Layout(heading.gameObject, 48);
            var scroll = Rect("ScrollView", column); Layout(scroll.gameObject, -1, 1); var component = scroll.gameObject.AddComponent<UnityEngine.UI.ScrollRect>(); component.horizontal = false; component.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; component.scrollSensitivity = 40;
            var viewport = Rect("Viewport", scroll); Stretch(viewport); viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.white; viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            content = Rect("Content", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            Vertical(content, 12); content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            component.content = content; component.viewport = viewport; return component;
        }
        private static TMP_Dropdown Dropdown(Transform parent, string name, string caption)
        {
            var cell = Cell(parent, name, caption); var obj = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources()); obj.name = "Dropdown"; obj.transform.SetParent(cell, false); Layout(obj, 54); Style(obj);
            obj.GetComponent<UnityEngine.UI.Image>().color = new Color32(244, 247, 255, 255);
            var arrow = obj.transform.Find("Arrow"); UnityEngine.Object.DestroyImmediate(arrow.GetComponent<UnityEngine.UI.Image>());
            var arrowText = arrow.gameObject.AddComponent<TextMeshProUGUI>(); arrowText.font = Font; arrowText.text = "v"; arrowText.fontSize = 20; arrowText.color = Blue; arrowText.alignment = TextAlignmentOptions.Center; arrowText.raycastTarget = false;
            var dropdown = obj.GetComponent<TMP_Dropdown>(); var template = dropdown.template; template.sizeDelta = new Vector2(template.sizeDelta.x, 280);
            var item = template.Find("Viewport/Content/Item").GetComponent<RectTransform>(); item.sizeDelta = new Vector2(0, 40);
            template.Find("Viewport/Content").GetComponent<RectTransform>().sizeDelta = new Vector2(0, 48);
            var mark = item.Find("Item Checkmark").GetComponent<UnityEngine.UI.Image>(); mark.color = Blue; mark.rectTransform.sizeDelta = new Vector2(10, 10);
            return dropdown;
        }
        private static RectTransform Cell(Transform parent, string name, string caption) { var cell = Rect(name, parent); Layout(cell.gameObject, -1, 1); Vertical(cell, 4); var label = Label(cell, "Label", caption, 22); Layout(label.gameObject, 30); return cell; }
        private static RectTransform Horizontal(string name, Transform parent, float height)
        {
            var rect = Rect(name, parent); Layout(rect.gameObject, height); var layout = rect.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>(); layout.spacing = 20; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = true; return rect;
        }
        private static UnityEngine.UI.VerticalLayoutGroup Vertical(RectTransform rect, int spacing)
        {
            var layout = rect.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(); layout.spacing = spacing; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false; return layout;
        }
        private static UnityEngine.UI.Button Button(Transform parent, string name, string text, float width)
        {
            var rect = Rect(name, parent); Layout(rect.gameObject, 56, 0, width); var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = Blue; var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var label = Label(rect, "Label", text, 24); Stretch(label.rectTransform); label.alignment = TextAlignmentOptions.Center; label.color = Color.white; return button;
        }
        private static TMP_Text Label(Transform parent, string name, string text, int size) { var rect = Rect(name, parent); var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = Font; label.text = text; label.fontSize = size; label.color = new Color32(51, 51, 51, 255); label.raycastTarget = false; return label; }
        private static void Style(GameObject root) { foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) { text.font = Font; text.fontSize = 22; text.color = new Color32(51, 51, 51, 255); } }
        private static RectTransform Rect(string name, Transform parent) { var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false); return obj.GetComponent<RectTransform>(); }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Layout(GameObject obj, float height = -1, float flexible = 0, float width = -1) { var layout = obj.GetComponent<UnityEngine.UI.LayoutElement>() ?? obj.AddComponent<UnityEngine.UI.LayoutElement>(); layout.preferredHeight = height; layout.flexibleHeight = flexible; layout.flexibleWidth = flexible; layout.preferredWidth = width; }
        private static void Set(SerializedObject fields, string name, UnityEngine.Object value) => fields.FindProperty(name).objectReferenceValue = value;
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
                if ((ArgumentPresent("-authSmoke") || ArgumentPresent("-flightSmoke")) && (!Debug.isDebugBuild || string.IsNullOrEmpty(smokeFile) || !smokeFile.StartsWith("auth-smoke-", StringComparison.Ordinal)))
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

## Assets/TravelPlanning/Tests/EditMode/FlightSearchTests.cs

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
using TravelPlanning.Flights;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class FlightSearchTests
    {
        private string folder;
        private string path;
        private TravelDatabase database;
        private FlightSearchService service;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "TravelFlightTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, "travel.db");
            database = new TravelDatabase(Path.Combine(Application.streamingAssetsPath, "Database", "travel_seed.db"), path);
            service = new FlightSearchService(database);
        }

        [TearDown]
        public void TearDown() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }

        [UnityTest]
        public IEnumerator RoundTripReturnsTwoAirlinesPerLegWithinThreeSecondsOffMainThread()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            var task = service.SearchAsync(Request());
            yield return Wait(task);
            var result = task.Result;
            Assert.That(result.Outbound.Count, Is.EqualTo(2));
            Assert.That(result.Return.Count, Is.EqualTo(2));
            Assert.That(result.Outbound.Select(flight => flight.AirlineId), Is.EquivalentTo(new[] { "BA", "AA" }));
            Assert.That(result.Return.Select(flight => flight.AirlineId), Is.EquivalentTo(new[] { "BA", "AA" }));
            Assert.That(result.Outbound.All(flight => flight.OriginAirportId == "JFK" && flight.DestinationAirportId == "LHR" && flight.DepartureLocal.Date == new DateTime(2027, 6, 15)), Is.True);
            Assert.That(result.Return.All(flight => flight.OriginAirportId == "LHR" && flight.DestinationAirportId == "JFK" && flight.DepartureLocal.Date == new DateTime(2027, 6, 22)), Is.True);
            var overnight = result.Outbound.Single(flight => flight.AirlineId == "AA");
            Assert.That(overnight.ArrivalLocal, Is.EqualTo(new DateTime(2027, 6, 16, 5, 0, 0)));
            Assert.That(overnight.DepartureLocal, Is.EqualTo(new DateTime(2027, 6, 15, 17, 0, 0)));
            Assert.That(result.WorkerThreadId, Is.Not.EqualTo(mainThread));
            Assert.That(result.ElapsedMilliseconds, Is.LessThan(3000));
        }

        [UnityTest]
        public IEnumerator AllSortsApplyIndependentlyToBothLegs()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            foreach (FlightSort sort in Enum.GetValues(typeof(FlightSort)))
            {
                var request = Request(); request.Sort = sort;
                var task = service.SearchAsync(request);
                yield return Wait(task);
                foreach (var leg in new[] { task.Result.Outbound, task.Result.Return })
                {
                    IEnumerable<FlightOption> expected;
                    switch (sort)
                    {
                        case FlightSort.PriceDescending: expected = leg.OrderByDescending(flight => flight.PriceCents); break;
                        case FlightSort.DepartureEarliest: expected = leg.OrderBy(flight => flight.DepartureLocal); break;
                        case FlightSort.DepartureLatest: expected = leg.OrderByDescending(flight => flight.DepartureLocal); break;
                        case FlightSort.AirlineName: expected = leg.OrderBy(flight => flight.AirlineName, StringComparer.OrdinalIgnoreCase); break;
                        default: expected = leg.OrderBy(flight => flight.PriceCents); break;
                    }
                    Assert.That(leg.Select(flight => flight.Id), Is.EqualTo(expected.Select(flight => flight.Id)), sort.ToString());
                }
            }
        }

        [UnityTest]
        public IEnumerator CombinedFiltersUseEachOriginsLocalClockAndInclusivePriceLimit()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            var request = Request(); request.AirlineId = "BA"; request.MaxPriceCents = 52700; request.TimeBand = DepartureTimeBand.Morning;
            var morning = service.SearchAsync(request);
            yield return Wait(morning);
            Assert.That(morning.Result.Outbound.Single().AirlineId, Is.EqualTo("BA"));
            Assert.That(morning.Result.Return.Single().AirlineId, Is.EqualTo("BA"));
            Assert.That(morning.Result.Outbound.Single().DepartureLocal.Hour, Is.EqualTo(8));
            Assert.That(morning.Result.Outbound.Single().DepartureUtc.Hour, Is.EqualTo(12));
            Assert.That(morning.Result.Return.Single().DepartureLocal.Hour, Is.EqualTo(8));
            request.MaxPriceCents = 52699;
            var tooCheap = service.SearchAsync(request);
            yield return Wait(tooCheap);
            Assert.That(tooCheap.Result.Outbound, Is.Empty);
            Assert.That(tooCheap.Result.Return, Is.Empty);
            request.MaxPriceCents = null; request.AirlineId = "AA"; request.TimeBand = DepartureTimeBand.Afternoon;
            var afternoon = service.SearchAsync(request);
            yield return Wait(afternoon);
            Assert.That(afternoon.Result.Outbound.Single().DepartureLocal.Hour, Is.EqualTo(17));
            Assert.That(afternoon.Result.Outbound.Single().DepartureUtc.Hour, Is.EqualTo(21));
            Assert.That(afternoon.Result.Return.Single().DepartureLocal.Hour, Is.EqualTo(17));
            foreach (var band in new[] { DepartureTimeBand.Night, DepartureTimeBand.Evening })
            {
                request.AirlineId = null; request.TimeBand = band;
                var empty = service.SearchAsync(request);
                yield return Wait(empty);
                Assert.That(empty.Result.Outbound, Is.Empty);
                Assert.That(empty.Result.Return, Is.Empty);
            }
        }

        [UnityTest]
        public IEnumerator ValidUnseededRouteReturnsEmptyLists()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            var request = Request(); request.OriginAirportId = "LHR"; request.DestinationAirportId = "HND";
            var task = service.SearchAsync(request);
            yield return Wait(task);
            Assert.That(task.Result.Outbound, Is.Empty);
            Assert.That(task.Result.Return, Is.Empty);
        }

        [UnityTest]
        public IEnumerator InvalidRequestValuesAreRejectedBeforeSearch()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            var changes = new Action<FlightSearchRequest>[]
            {
                request => request.OriginAirportId = "XXX",
                request => request.DestinationAirportId = "JFK",
                request => request.ReturnDate = "2027-06-14",
                request => request.DepartureDate = "2027-05-31",
                request => request.ReturnDate = "2027-07-01",
                request => request.DepartureDate = "06/15/2027",
                request => request.ReturnDate = "2027-06-31",
                request => request.DepartureDate = null,
                request => request.OriginAirportId = "JFK' OR 1=1 --",
                request => request.AirlineId = "BA' OR 1=1 --",
                request => request.MaxPriceCents = -1,
                request => request.Sort = (FlightSort)999,
                request => request.TimeBand = (DepartureTimeBand)(-1)
            };
            foreach (var change in changes)
            {
                var request = Request(); change(request);
                var task = service.SearchAsync(request);
                yield return Completion(task);
                Assert.That(task.IsFaulted, Is.True);
                Assert.That(task.Exception.GetBaseException(), Is.InstanceOf<ArgumentException>());
            }
            Assert.Throws<ArgumentException>(() => service.SearchAsync(null));
        }

        [UnityTest]
        public IEnumerator CatalogOptionsComeFromDatabaseAndMissingMetadataIsAnError()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            var original = service.LoadOptionsAsync();
            yield return Wait(original);
            Assert.That(original.Result.Airports.Count, Is.EqualTo(12));
            Assert.That(original.Result.StartDate, Is.EqualTo("2027-06-01"));
            Assert.That(original.Result.EndDate, Is.EqualTo("2027-06-30"));
            yield return Wait(database.ExecuteAsync(connection =>
            {
                connection.Execute("INSERT INTO destinations (id,name,country,region,description) VALUES (?,?,?,?,?)", "extra", "Extra City", "Example", "Demo", "Test");
                connection.Execute("INSERT INTO airports (id,destination_id,name,time_zone) VALUES (?,?,?,?)", "EXT", "extra", "Extra Airport", "UTC");
                return 0;
            }, CancellationToken.None));
            var expanded = service.LoadOptionsAsync();
            yield return Wait(expanded);
            Assert.That(expanded.Result.Airports.Count, Is.EqualTo(original.Result.Airports.Count + 1));
            Assert.That(expanded.Result.Airports.Any(airport => airport.Id == "EXT" && airport.City == "Extra City"), Is.True);
            yield return Wait(database.ExecuteAsync(connection => connection.Execute("DELETE FROM metadata WHERE key=?", "seed_start_date"), CancellationToken.None));
            var missing = service.LoadOptionsAsync();
            yield return Completion(missing);
            Assert.That(missing.IsFaulted, Is.True);
            Assert.That(missing.Exception.GetBaseException(), Is.InstanceOf<InvalidOperationException>());
        }

        [UnityTest]
        public IEnumerator ActualSearchQueryUsesRouteDateIndex()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            var request = Request(); request.AirlineId = "BA"; request.MaxPriceCents = 55000;
            var plan = service.ExplainOutboundQueryAsync(request);
            yield return Wait(plan);
            Assert.That(plan.Result.Any(detail => detail.Contains("flights_search") && detail.Contains("origin_airport_id") && detail.Contains("departure_local_date")), Is.True, string.Join("\n", plan.Result));
        }

        [UnityTest]
        public IEnumerator SearchesDoNotChangeExistingUserTripOrDatabaseBytes()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(database.ExecuteAsync(connection =>
            {
                connection.Execute("INSERT INTO users (id,email_normalized,password_hash,password_salt,password_iterations,created_utc) VALUES (?,?,?,?,?,?)", "owner", "owner@example.com", "testfixture", "testfixture", 600000, "2026-09-29T00:00:00Z");
                connection.Execute("INSERT INTO trips (id,user_id,name,start_date,end_date,created_utc,updated_utc) VALUES (?,?,?,?,?,?,?)", "trip", "owner", "Keep my trip", "2027-06-15", "2027-06-22", "2026-09-29T00:00:00Z", "2026-09-29T00:00:00Z");
                return 0;
            }, CancellationToken.None));
            byte[] before = Hash();
            yield return Wait(service.LoadOptionsAsync());
            yield return Wait(service.SearchAsync(Request()));
            yield return Wait(service.ExplainOutboundQueryAsync(Request()));
            Assert.That(Hash(), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator RequestIsSnapshottedAndCancellationIsHonored()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            using (var release = new ManualResetEventSlim(false))
            using (var entered = new ManualResetEventSlim(false))
            {
                var blocker = database.ExecuteAsync(connection => { entered.Set(); return release.Wait(TimeSpan.FromSeconds(15)); }, CancellationToken.None);
                while (!entered.IsSet) yield return null;
                Task<FlightSearchResult> search;
                try
                {
                    var request = Request();
                    search = service.SearchAsync(request);
                    request.OriginAirportId = "Changed after starting";
                    request.DepartureDate = "invalid";
                }
                finally { release.Set(); }
                yield return Wait(blocker);
                yield return Wait(search);
                Assert.That(search.Result.Outbound.Count, Is.EqualTo(2));
            }
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                var search = service.SearchAsync(Request(), cancelled.Token);
                var options = service.LoadOptionsAsync(cancelled.Token);
                yield return Completion(search);
                yield return Completion(options);
                Assert.That(search.IsCanceled, Is.True);
                Assert.That(options.IsCanceled, Is.True);
            }
        }

        private static FlightSearchRequest Request() => new FlightSearchRequest
        {
            OriginAirportId = "JFK", DestinationAirportId = "LHR", DepartureDate = "2027-06-15", ReturnDate = "2027-06-22"
        };

        private byte[] Hash() { using (var hash = SHA256.Create()) return hash.ComputeHash(File.ReadAllBytes(path)); }
        private static IEnumerator Wait(Task task) { yield return Completion(task); task.GetAwaiter().GetResult(); }
        private static IEnumerator Completion(Task task)
        {
            double deadline = UnityEditor.EditorApplication.timeSinceStartup + 30;
            while (!task.IsCompleted)
            {
                Assert.That(UnityEditor.EditorApplication.timeSinceStartup, Is.LessThan(deadline), "Flight operation timed out.");
                yield return null;
            }
        }
    }
}
```

## Assets/TravelPlanning/Tests/EditMode/FlightSearchSceneTests.cs

```csharp
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TravelPlanning.UI;
using TravelPlanning.UI.Editor;
using TravelPlanning.UI.Flights;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class FlightSearchSceneTests
    {
        [TestCase("", null)]
        [TestCase("527.25", 52725)]
        [TestCase("0", 0)]
        [TestCase("21474836.47", int.MaxValue)]
        public void MaximumPriceUsesExactUsdCents(string input, int? expected) => Assert.That(FlightSearchPage.ParseMaximumPrice(input), Is.EqualTo(expected));
        [TestCase("-1")]
        [TestCase("1.001")]
        [TestCase("NaN")]
        [TestCase("1,000")]
        [TestCase("21474836.48")]
        public void MaximumPriceRejectsAmbiguousOrOutOfRangeValues(string input) => Assert.Throws<System.ArgumentException>(() => FlightSearchPage.ParseMaximumPrice(input));
        [Test]
        public void FlightScreenReferencesAndScrollViewHierarchyAreComplete()
        {
            EditorSceneManager.OpenScene(FlightSearchSetup.ScenePath);
            var page = Object.FindFirstObjectByType<FlightSearchPage>();
            Assert.That(page.transform.parent, Is.Null);
            var fields = new SerializedObject(page);
            foreach (string name in new[] { "account", "authCanvas", "flightCanvas", "origin", "destination", "departureDate", "returnDate", "airline", "timeBand", "sort", "maximumPrice", "status", "outboundHeading", "returnHeading", "openButton", "searchButton", "clearButton", "backButton", "logoutButton", "outboundContent", "returnContent", "outboundScroll", "returnScroll", "rowPrefab" })
                Assert.That(fields.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
            var canvas = (GameObject)fields.FindProperty("flightCanvas").objectReferenceValue;
            Assert.That(canvas.activeSelf, Is.False);
            foreach (var scroll in canvas.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true).Where(x => x.name == "ScrollView"))
            {
                Assert.That(scroll.content.parent, Is.EqualTo(scroll.viewport));
                Assert.That(scroll.viewport.GetComponent<UnityEngine.UI.Mask>(), Is.Not.Null);
                Assert.That(scroll.content.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit, Is.EqualTo(UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize));
                Assert.That(scroll.horizontal, Is.False);
            }
            canvas.SetActive(true); Canvas.ForceUpdateCanvases();
            foreach (var scroll in canvas.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true).Where(x => x.name == "ScrollView"))
            {
                Assert.That(scroll.viewport.rect.width, Is.GreaterThan(200));
                Assert.That(scroll.viewport.rect.height, Is.GreaterThan(100));
            }
            Assert.That(GameObject.Find("Canvas/Right Panel/LoginCard/EmailInput").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(0, -230)));
        }
        [UnityTest]
        public IEnumerator ActualControlsSearchFilterValidateCancelAndRecover()
        {
            EditorSceneManager.OpenScene(FlightSearchSetup.ScenePath);
            var configuration = new SerializedObject(Object.FindFirstObjectByType<LoginPage>());
            configuration.FindProperty("developmentDatabaseFile").stringValue = "flight-editor-" + System.Guid.NewGuid().ToString("N") + ".db";
            configuration.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            var account = Object.FindFirstObjectByType<LoginPage>();
            string file = new SerializedObject(account).FindProperty("developmentDatabaseFile").stringValue;
            Assert.That(file, Does.StartWith("flight-editor-"));
            var runner = Object.FindFirstObjectByType<FlightSmokeRunner>();
            var task = runner.RunChecksAsync(true);
            float deadline = Time.realtimeSinceStartup + 90;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(task.IsCompleted, Is.True, "UI flow timed out.");
            if (task.IsFaulted) Assert.Fail(task.Exception.GetBaseException().ToString());
            Assert.That(task.IsCanceled, Is.False);
            account.Logout();
            string path = System.IO.Path.Combine(Application.persistentDataPath, file);
            Assert.That(System.IO.File.Exists(path), Is.True);
            System.IO.File.Delete(path);
            yield return new ExitPlayMode();
        }
    }
}
```

## tools/Test-FlightSearch.ps1

```powershell
param([string]$BuildRoot = 'Builds/FlightSearch')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $BuildRoot).Path
$exe = Join-Path $root 'TravelPlannerFlights.exe'
$id = [Guid]::NewGuid().ToString('N')
$file = "auth-smoke-$id.db"
$logs = Join-Path (Get-Location).Path "Logs/FlightSearch/$id"
New-Item -ItemType Directory -Path $logs -Force | Out-Null
foreach ($phase in @('register-search','reopen-search')) {
    $log = Join-Path $logs "$phase.log"
    $arguments = @('-batchmode','-nographics','-flightSmoke','-authFile',$file,'-logFile',('"' + $log + '"'))
    if ($phase -eq 'reopen-search') { $arguments += '-flightReopen' }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(120000)) { $process.Kill(); throw "Flight search $phase timed out. See $log" }
    $process.Refresh()
    $output = Get-Content -LiteralPath $log -Raw
    if ($process.ExitCode -ne 0 -or $output -notmatch 'FLIGHT_UI_SMOKE_PASS') { throw "Flight search $phase failed. See $log" }
    Write-Output "PASS $phase - real UI search/filter/sort/cancellation checks. Log: $log"
    Select-String -LiteralPath $log -Pattern 'FLIGHT_SEARCH_TIMING' | ForEach-Object { $_.Line }
}
```
