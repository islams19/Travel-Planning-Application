# Milestone 6 — Full scripts

Complete source snapshot for Saved trips, including the six existing integration scripts. See the [validation record](Milestone-6-Validation.md) for 126 passing tests, the successful Windows build, two passing player checks and remaining manual limitations.

## Assets/TravelPlanning/Runtime/Trips/TripCatalog.cs

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using SQLite;

namespace TravelPlanning.Trips
{
    /// <summary>Fixed catalog queries used to resolve saved references without one query per item.</summary>
    internal static class TripCatalog
    {
        internal static void Target(SavedItemKind kind, out string table, out string column, out string unit)
        {
            switch (kind)
            {
                case SavedItemKind.Flight: table = "flights"; column = "flight_id"; unit = "/ flight"; break;
                case SavedItemKind.Hotel: table = "hotels"; column = "hotel_id"; unit = "/ room / night"; break;
                case SavedItemKind.Restaurant: table = "restaurants"; column = "restaurant_id"; unit = "/ person (meal)"; break;
                case SavedItemKind.Experience: table = "experiences"; column = "experience_id"; unit = "/ adult"; break;
                case SavedItemKind.Hotspot: table = "hotspots"; column = "hotspot_id"; unit = "/ visit"; break;
                default: throw new ArgumentException("Choose a valid item category.", nameof(kind));
            }
        }

        internal static IReadOnlyList<SavedTripItem> ReadItems(SQLiteConnection connection, string owner, string tripId, CancellationToken token)
        {
            var rows = new List<SavedCatalogRow>();
            foreach (SavedItemKind kind in Enum.GetValues(typeof(SavedItemKind)))
            {
                token.ThrowIfCancellationRequested();
                Target(kind, out string table, out string column, out string unit);
                string sql = "SELECT s.id AS Id, s.created_utc AS SavedUtc, p.id AS TargetId, p.price_cents AS PriceCents, p.currency AS Currency, ";
                if (kind == SavedItemKind.Flight)
                    sql += "a.name AS AirlineName, p.flight_number AS FlightNumber, p.origin_airport_id AS OriginAirportId, p.destination_airport_id AS DestinationAirportId, " +
                        "p.departure_utc AS DepartureUtc, p.arrival_utc AS ArrivalUtc, oa.time_zone AS OriginZone, da.time_zone AS DestinationZone " +
                        "FROM saved_items s JOIN flights p ON p.id=s.flight_id JOIN airlines a ON a.id=p.airline_id " +
                        "JOIN airports oa ON oa.id=p.origin_airport_id JOIN airports da ON da.id=p.destination_airport_id ";
                else
                    sql += "p.name AS Title, p.address AS Address, p.description AS Description, d.name AS DestinationName " +
                        "FROM saved_items s JOIN " + table + " p ON p.id=s." + column + " JOIN destinations d ON d.id=p.destination_id ";
                sql += "WHERE s.user_id=? AND s.trip_id=?";
                var categoryRows = connection.Query<SavedCatalogRow>(sql, owner, tripId);
                foreach (var row in categoryRows) { row.Kind = kind; row.PriceUnit = unit; }
                rows.AddRange(categoryRows);
            }
            var items = new List<SavedTripItem>();
            foreach (var row in rows.OrderBy(row => row.SavedUtc, StringComparer.Ordinal).ThenBy(row => row.Id, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                bool flight = row.Kind == SavedItemKind.Flight;
                items.Add(new SavedTripItem
                {
                    Id = row.Id, TargetId = row.TargetId, Kind = row.Kind, PriceCents = row.PriceCents, Currency = row.Currency, PriceUnit = row.PriceUnit,
                    Title = flight ? row.AirlineName + " " + row.FlightNumber : row.Title,
                    Details = flight ? row.OriginAirportId + " to " + row.DestinationAirportId + "\nDepart " + Local(row.DepartureUtc, row.OriginZone) + " (" + row.OriginAirportId + " local)" +
                        "\nArrive " + Local(row.ArrivalUtc, row.DestinationZone) + " (" + row.DestinationAirportId + " local)" :
                        row.DestinationName + "\n" + row.Address + "\n" + row.Description
                });
            }
            return items.AsReadOnly();
        }

        private static string Local(string utc, string zone)
        {
            var parsed = DateTime.ParseExact(utc, "yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            return TimeZoneInfo.ConvertTimeFromUtc(parsed, TimeZoneInfo.FindSystemTimeZoneById(zone)).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
    }

    // Simple public properties allow sqlite-net to map the SQL column aliases.
    public sealed class SavedCatalogRow
    {
        public string Id { get; set; }
        public string SavedUtc { get; set; }
        public string TargetId { get; set; }
        public int? PriceCents { get; set; }
        public string Currency { get; set; }
        public string Title { get; set; }
        public string Address { get; set; }
        public string Description { get; set; }
        public string DestinationName { get; set; }
        public string AirlineName { get; set; }
        public string FlightNumber { get; set; }
        public string OriginAirportId { get; set; }
        public string DestinationAirportId { get; set; }
        public string DepartureUtc { get; set; }
        public string ArrivalUtc { get; set; }
        public string OriginZone { get; set; }
        public string DestinationZone { get; set; }
        public SavedItemKind Kind { get; set; }
        public string PriceUnit { get; set; }
    }
}

```

## Assets/TravelPlanning/Runtime/Trips/TripModels.cs

```csharp
using System.Collections.Generic;

namespace TravelPlanning.Trips
{
    public enum SavedItemKind { Flight, Hotel, Restaurant, Experience, Hotspot }

    public sealed class TripSummary
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public string CreatedUtc { get; set; }
        public string UpdatedUtc { get; set; }
        public int ItemCount { get; set; }
    }

    public sealed class TripListResult
    {
        public IReadOnlyList<TripSummary> Trips { get; internal set; }
        public string StartDate { get; internal set; }
        public string EndDate { get; internal set; }
        public long ElapsedMilliseconds { get; internal set; }
        public int WorkerThreadId { get; internal set; }
    }

    /// <summary>The saved reference resolved against today's local catalog, not a price snapshot.</summary>
    public sealed class SavedTripItem
    {
        public string Id { get; internal set; }
        public string TargetId { get; internal set; }
        public SavedItemKind Kind { get; internal set; }
        public string Title { get; internal set; }
        public string Details { get; internal set; }
        public int? PriceCents { get; internal set; }
        public string Currency { get; internal set; }
        public string PriceUnit { get; internal set; }
    }

    public sealed class TripDetails
    {
        public TripSummary Trip { get; internal set; }
        public IReadOnlyList<SavedTripItem> Items { get; internal set; }
        public long ElapsedMilliseconds { get; internal set; }
        public int WorkerThreadId { get; internal set; }
    }
}
```

## Assets/TravelPlanning/Runtime/Trips/TripService.cs

```csharp
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using SQLite;
using TravelPlanning.Data;

namespace TravelPlanning.Trips
{
    /// <summary>Owns one signed-in user's trip operations. Construct a new service when the session changes.</summary>
    public sealed class TripService
    {
        private readonly TravelDatabase database;
        private readonly string userId;
        private const string SummarySql = "SELECT t.id AS Id, t.name AS Name, t.start_date AS StartDate, t.end_date AS EndDate, " +
            "t.created_utc AS CreatedUtc, t.updated_utc AS UpdatedUtc, " +
            "(SELECT COUNT(*) FROM saved_items s WHERE s.trip_id=t.id AND s.user_id=t.user_id) AS ItemCount FROM trips t ";

        public TripService(TravelDatabase database, string userId)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("Sign in before opening saved trips.", nameof(userId));
            this.userId = userId;
        }

        public Task<TripListResult> ListAsync(CancellationToken cancellationToken = default)
        {
            var elapsed = Stopwatch.StartNew();
            return database.ExecuteAsync(connection =>
            {
                RequireUser(connection);
                CatalogDates(connection, out string start, out string end);
                var trips = connection.Query<TripSummary>(SummarySql + "WHERE t.user_id=? ORDER BY t.created_utc DESC, t.id", userId);
                cancellationToken.ThrowIfCancellationRequested();
                elapsed.Stop();
                return new TripListResult { Trips = trips.AsReadOnly(), StartDate = start, EndDate = end,
                    ElapsedMilliseconds = elapsed.ElapsedMilliseconds, WorkerThreadId = Thread.CurrentThread.ManagedThreadId };
            }, cancellationToken);
        }

        public Task<TripSummary> CreateAsync(string name, string startDate, string endDate, CancellationToken cancellationToken = default)
        {
            string cleanName = CleanName(name);
            return database.ExecuteAsync(connection =>
            {
                TripSummary created = null;
                connection.RunInTransaction(() =>
                {
                    RequireUser(connection);
                    CatalogDates(connection, out string first, out string last);
                    ValidateDates(startDate, endDate, first, last);
                    cancellationToken.ThrowIfCancellationRequested();
                    string now = Timestamp();
                    created = new TripSummary { Id = Guid.NewGuid().ToString("N"), Name = cleanName, StartDate = startDate, EndDate = endDate, CreatedUtc = now, UpdatedUtc = now, ItemCount = 0 };
                    connection.Execute("INSERT INTO trips (id,user_id,name,start_date,end_date,created_utc,updated_utc) VALUES (?,?,?,?,?,?,?)",
                        created.Id, userId, cleanName, startDate, endDate, now, now);
                    cancellationToken.ThrowIfCancellationRequested(); // An exception here rolls back the entire transaction.
                });
                // A committed write returns success even if cancellation arrives just after commit.
                return created;
            }, cancellationToken);
        }

        public Task<TripDetails> LoadAsync(string tripId, CancellationToken cancellationToken = default)
        {
            var elapsed = Stopwatch.StartNew();
            return database.ExecuteAsync(connection =>
            {
                var trip = RequireTrip(connection, tripId);
                var items = TripCatalog.ReadItems(connection, userId, tripId, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                elapsed.Stop();
                return new TripDetails { Trip = trip, Items = items, ElapsedMilliseconds = elapsed.ElapsedMilliseconds,
                    WorkerThreadId = Thread.CurrentThread.ManagedThreadId };
            }, cancellationToken);
        }

        public Task<bool> SaveAsync(string tripId, SavedItemKind kind, string targetId, CancellationToken cancellationToken = default)
        {
            TripCatalog.Target(kind, out string table, out string column, out _);
            return database.ExecuteAsync(connection =>
            {
                bool added = false;
                connection.RunInTransaction(() =>
                {
                    RequireTrip(connection, tripId);
                    if (string.IsNullOrWhiteSpace(targetId) || connection.ExecuteScalar<int>("SELECT COUNT(*) FROM " + table + " WHERE id=?", targetId) != 1)
                        throw new ArgumentException("Choose an item available in this category.", nameof(targetId));
                    if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM saved_items WHERE user_id=? AND trip_id=? AND " + column + "=?", userId, tripId, targetId) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return;
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    string now = Timestamp();
                    connection.Execute("INSERT INTO saved_items (id,user_id,trip_id," + column + ",created_utc) VALUES (?,?,?,?,?)", Guid.NewGuid().ToString("N"), userId, tripId, targetId, now);
                    connection.Execute("UPDATE trips SET updated_utc=? WHERE id=? AND user_id=?", now, tripId, userId);
                    cancellationToken.ThrowIfCancellationRequested();
                    added = true;
                });
                return added;
            }, cancellationToken);
        }

        public Task<bool> RemoveAsync(string tripId, string savedItemId, CancellationToken cancellationToken = default)
        {
            return database.ExecuteAsync(connection =>
            {
                bool removed = false;
                connection.RunInTransaction(() =>
                {
                    RequireTrip(connection, tripId);
                    cancellationToken.ThrowIfCancellationRequested();
                    removed = connection.Execute("DELETE FROM saved_items WHERE id=? AND user_id=? AND trip_id=?", savedItemId, userId, tripId) > 0;
                    if (removed) connection.Execute("UPDATE trips SET updated_utc=? WHERE id=? AND user_id=?", Timestamp(), tripId, userId);
                    cancellationToken.ThrowIfCancellationRequested();
                });
                return removed;
            }, cancellationToken);
        }

        private TripSummary RequireTrip(SQLiteConnection connection, string tripId)
        {
            var trip = connection.FindWithQuery<TripSummary>(SummarySql + "WHERE t.id=? AND t.user_id=?", tripId, userId);
            // Unknown and other-owner IDs deliberately receive exactly the same error.
            if (trip == null) throw new ArgumentException("This trip is not available for your account.", nameof(tripId));
            return trip;
        }

        private void RequireUser(SQLiteConnection connection)
        {
            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM users WHERE id=?", userId) != 1)
                throw new InvalidOperationException("Your account is not available. Sign in again.");
        }

        private static string CleanName(string name)
        {
            if (name == null) throw new ArgumentException("Enter a trip name with 1 to 60 characters.", nameof(name));
            foreach (char character in name)
                if (char.IsControl(character)) throw new ArgumentException("Trip names cannot contain control characters.", nameof(name));
            string trimmed = name.Trim();
            if (trimmed.Length < 1 || trimmed.Length > 60) throw new ArgumentException("Enter a trip name with 1 to 60 characters.", nameof(name));
            return trimmed;
        }

        private static void CatalogDates(SQLiteConnection connection, out string first, out string last)
        {
            first = connection.ExecuteScalar<string>("SELECT value FROM metadata WHERE key=?", "seed_start_date");
            last = connection.ExecuteScalar<string>("SELECT value FROM metadata WHERE key=?", "seed_end_date");
            if (!ParseDate(first, out var start) || !ParseDate(last, out var end) || end < start || (end - start).TotalDays + 1 > 366)
                throw new InvalidOperationException("The catalog date range is missing or invalid.");
        }

        private static void ValidateDates(string startDate, string endDate, string first, string last)
        {
            if (!ParseDate(startDate, out var start) || !ParseDate(endDate, out var end))
                throw new ArgumentException("Enter both trip dates as YYYY-MM-DD.");
            if (end < start) throw new ArgumentException("The end date must be on or after the start date.");
            ParseDate(first, out var minimum); ParseDate(last, out var maximum);
            if (start < minimum || end > maximum || (end - start).TotalDays + 1 > 366)
                throw new ArgumentException("Choose trip dates from " + first + " through " + last + ".");
        }

        private static bool ParseDate(string value, out DateTime parsed) => DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);
        private static string Timestamp() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
    }
}

```

## Assets/TravelPlanning/UI/Trips/SavedTripRow.cs

```csharp
using System;
using System.Globalization;
using TMPro;
using TravelPlanning.Trips;
using UnityEngine;

namespace TravelPlanning.UI.Trips
{
    /// <summary>Displays one saved reference using its current catalog information.</summary>
    public sealed class SavedTripRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text title, details, price;
        [SerializeField] private UnityEngine.UI.Button removeButton;
        private Action remove;
        private void Awake() => removeButton.onClick.AddListener(Remove);
        public void Show(SavedTripItem item, Action onRemove)
        {
            title.text = item.Title; details.text = item.Details; remove = onRemove;
            price.text = !item.PriceCents.HasValue ? "Price unavailable" : item.PriceCents.Value == 0 ? "Free" : "USD " + (item.PriceCents.Value / 100m).ToString("N2", CultureInfo.InvariantCulture) + " " + item.PriceUnit;
        }
        public void SetInteractable(bool value) { if (removeButton) removeButton.interactable = value; }
        private void Remove() => remove?.Invoke();
        private void OnDestroy() { if (removeButton) removeButton.onClick.RemoveListener(Remove); }
    }
}
```

## Assets/TravelPlanning/UI/Trips/SavedTripsPage.cs

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
using TravelPlanning.Trips;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Flights;
using TravelPlanning.UI.Reviews;
using UnityEngine;

namespace TravelPlanning.UI.Trips
{
    /// <summary>One modal handles browsing trips and picking a trip for a selected flight or place.</summary>
    public sealed class SavedTripsPage : MonoBehaviour
    {
        [SerializeField] private LoginPage account;
        [SerializeField] private FlightSearchPage flights;
        [SerializeField] private DestinationHubPage hub;
        [SerializeField] private ReviewsPage reviews;
        [SerializeField] private GameObject modalCanvas;
        [SerializeField] private CanvasGroup[] underlyingGroups;
        [SerializeField] private UnityEngine.UI.Button homeButton, flightButton, hubButton, closeButton, createButton, saveButton, retryButton;
        [SerializeField] private TMP_InputField tripName;
        [SerializeField] private TMP_Dropdown tripChoice, startDate, endDate;
        [SerializeField] private TMP_Text targetPreview, status, tripInfo;
        [SerializeField] private UnityEngine.UI.ScrollRect scroll;
        [SerializeField] private SavedTripRow rowPrefab;
        private readonly List<SavedTripRow> rows = new List<SavedTripRow>();
        private CancellationTokenSource lifetime;
        private TripService service;
        private string ownerId, targetId, targetTitle;
        private SavedItemKind targetKind;
        private int revision;
        private bool groupsLocked;
        private bool[] previousInteraction;
        public bool IsOpen => modalCanvas && modalCanvas.activeSelf;
        public bool IsBusy { get; private set; }
        public TripListResult LastList { get; private set; }
        public TripDetails LastDetails { get; private set; }
        public string Status => status.text;
        private void Start()
        {
            if (!account || !flights || !hub || !reviews || !modalCanvas || !homeButton || !flightButton || !hubButton || !closeButton || !createButton || !saveButton || !retryButton || !tripName || !tripChoice || !startDate || !endDate || !targetPreview || !status || !tripInfo || !scroll || !rowPrefab || underlyingGroups == null || underlyingGroups.Length != 3 || underlyingGroups.Any(group => !group))
            {
                Debug.LogError("SavedTripsPage: assign all references on SavedTripsController using the prepared scene."); enabled = false; return;
            }
            homeButton.onClick.AddListener(Browse); flightButton.onClick.AddListener(Browse); hubButton.onClick.AddListener(Browse); closeButton.onClick.AddListener(Close);
            createButton.onClick.AddListener(Create); saveButton.onClick.AddListener(Save); retryButton.onClick.AddListener(Open); tripChoice.onValueChanged.AddListener(SelectTrip);
            flights.SaveRequested += OpenFlight; hub.SaveRequested += OpenPlace; flights.ContentCleared += Close; hub.ContentCleared += Close; account.LoggedOut += Close;
        }
        public void Browse() { targetId = targetTitle = null; Open(); }
        private void OpenFlight(FlightOption flight) { targetKind = SavedItemKind.Flight; targetId = flight.Id; targetTitle = flight.AirlineName + " " + flight.FlightNumber + " • " + flight.OriginAirportId + " → " + flight.DestinationAirportId; Open(); }
        private void OpenPlace(PlaceOption place)
        {
            targetKind = place.Category == PlaceCategory.Hotel ? SavedItemKind.Hotel : place.Category == PlaceCategory.Restaurant ? SavedItemKind.Restaurant : place.Category == PlaceCategory.Experience ? SavedItemKind.Experience : SavedItemKind.Hotspot;
            targetId = place.Id; targetTitle = place.Name; Open();
        }
        private async void Open()
        {
            if (!account.IsReady || string.IsNullOrEmpty(account.SignedInUserId)) return;
            reviews.Close(); Cancel(); ClearRows(); LastList = null; tripChoice.ClearOptions();
            ownerId = account.SignedInUserId; service = new TripService(account.Database, ownerId);
            LockUnderlying(); modalCanvas.SetActive(true); targetPreview.text = targetId == null ? "Browse your saved plans on this computer." : "Save selected item: " + targetTitle;
            if (UnityEngine.EventSystems.EventSystem.current) UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
            int current = revision; var token = Begin(); var requestService = service; SetBusy(true); status.text = "Loading your trips...";
            try
            {
                var result = await requestService.ListAsync(token); if (!Current(current)) return;
                LastList = result; PopulateDates(result.StartDate, result.EndDate); ShowChoices(null);
                if (result.Trips.Count == 0) status.text = "No trips yet. Enter a name and planning dates, then choose Create trip.";
                else { await LoadAndRender(requestService, result.Trips[0].Id, token, current); if (Current(current)) status.text = targetId == null ? "Choose a trip to see its saved items." : "Choose a trip, then save the selected item."; }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (Current(current)) status.text = "Your trips could not be loaded. Choose Retry."; }
            finally { if (Current(current)) SetBusy(false); }
        }
        private async void SelectTrip(int index)
        {
            if (IsBusy || !IsOpen || LastList == null || index < 0 || index >= LastList.Trips.Count) return;
            Cancel(); int current = revision; var token = Begin(); var requestService = service; string tripId = LastList.Trips[index].Id; SetBusy(true);
            try { await LoadAndRender(requestService, tripId, token, current); if (Current(current)) status.text = "Trip loaded. Prices reflect the current sample catalog."; }
            catch (OperationCanceledException) { }
            catch (Exception) { if (Current(current)) status.text = "This trip could not be loaded. Choose Retry."; }
            finally { if (Current(current)) SetBusy(false); }
        }
        private async void Create()
        {
            if (IsBusy || !IsOpen || LastList == null) return;
            string name = tripName.text, start = startDate.options[startDate.value].text, end = endDate.options[endDate.value].text;
            await Write(async (requestService, token) => { var trip = await requestService.CreateAsync(name, start, end, token); return (trip.Id, "Trip created. " + (targetId == null ? "Add flights and places using Save to trip." : "Now choose Save selected item.")); });
        }
        private async void Save()
        {
            if (IsBusy || !IsOpen || LastDetails == null || string.IsNullOrEmpty(targetId)) return;
            string tripId = LastDetails.Trip.Id, itemId = targetId; SavedItemKind kind = targetKind;
            await Write(async (requestService, token) => { bool added = await requestService.SaveAsync(tripId, kind, itemId, token); return (tripId, added ? "Item saved to this trip." : "This item is already saved to this trip."); });
        }
        private async void Remove(string tripId, string itemId)
        {
            if (IsBusy || !IsOpen || LastDetails?.Trip.Id != tripId) return;
            await Write(async (requestService, token) => { bool removed = await requestService.RemoveAsync(tripId, itemId, token); return (tripId, removed ? "Item removed from this trip." : "That item is no longer in this trip."); });
        }
        private async Task Write(Func<TripService, CancellationToken, Task<(string tripId, string message)>> operation)
        {
            Cancel(); int current = revision; var token = Begin(); var requestService = service; SetBusy(true); status.text = "Saving your changes...";
            try
            {
                var result = await operation(requestService, token); if (!Current(current)) return;
                var list = await requestService.ListAsync(token); if (!Current(current)) return;
                LastList = list; ShowChoices(result.tripId); await LoadAndRender(requestService, result.tripId, token, current);
                if (Current(current)) status.text = result.message;
            }
            catch (OperationCanceledException) { }
            catch (ArgumentException exception) { if (Current(current)) status.text = exception.Message; }
            catch (Exception) { if (Current(current)) status.text = "The change could not be completed. Reload with Retry to check the saved state."; }
            finally { if (Current(current)) SetBusy(false); }
        }
        private async Task LoadAndRender(TripService requestService, string tripId, CancellationToken token, int current)
        {
            ClearRows();
            var details = await requestService.LoadAsync(tripId, token); if (!Current(current)) return;
            ClearRows(); LastDetails = details; tripInfo.text = details.Trip.Name + " • Planning dates: " + details.Trip.StartDate + " to " + details.Trip.EndDate + " • " + details.Items.Count + " saved items";
            foreach (var item in details.Items) { string itemId = item.Id; var row = Instantiate(rowPrefab, scroll.content); row.Show(item, () => Remove(tripId, itemId)); row.SetInteractable(false); rows.Add(row); }
            Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1;
        }
        private void ShowChoices(string selectedId)
        {
            tripChoice.ClearOptions(); tripChoice.AddOptions(LastList.Trips.Select(trip => trip.Name + " (" + trip.ItemCount + ")").ToList());
            tripChoice.SetValueWithoutNotify(Math.Max(0, LastList.Trips.ToList().FindIndex(trip => trip.Id == selectedId))); tripChoice.RefreshShownValue();
        }
        private void PopulateDates(string start, string end)
        {
            var first = DateTime.ParseExact(start, "yyyy-MM-dd", CultureInfo.InvariantCulture); var last = DateTime.ParseExact(end, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (last < first || (last - first).TotalDays > 366) throw new InvalidOperationException("Catalog date range is too large.");
            var values = new List<string>(); for (var date = first; date <= last; date = date.AddDays(1)) values.Add(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            startDate.ClearOptions(); startDate.AddOptions(values); endDate.ClearOptions(); endDate.AddOptions(values);
            startDate.SetValueWithoutNotify(Math.Max(0, values.IndexOf("2027-06-15"))); endDate.SetValueWithoutNotify(Math.Max(0, values.IndexOf("2027-06-22"))); startDate.RefreshShownValue(); endDate.RefreshShownValue();
        }
        private void LockUnderlying()
        {
            if (groupsLocked) return; previousInteraction = underlyingGroups.Select(group => group.interactable).ToArray();
            foreach (var group in underlyingGroups) group.interactable = false; groupsLocked = true;
        }
        public void Close()
        {
            Cancel(); ClearRows(); LastList = null; service = null; ownerId = targetId = targetTitle = null;
            if (tripChoice) tripChoice.ClearOptions(); if (tripName) tripName.text = ""; if (targetPreview) targetPreview.text = ""; if (status) status.text = "";
            if (groupsLocked) { for (int i = 0; i < underlyingGroups.Length; i++) if (underlyingGroups[i]) underlyingGroups[i].interactable = previousInteraction[i]; groupsLocked = false; }
            var events = UnityEngine.EventSystems.EventSystem.current; if (events && events.currentSelectedGameObject && modalCanvas && events.currentSelectedGameObject.transform.IsChildOf(modalCanvas.transform)) events.SetSelectedGameObject(null);
            if (modalCanvas) modalCanvas.SetActive(false);
        }
        private void ClearRows() { foreach (var row in rows) if (row) { row.gameObject.SetActive(false); Destroy(row.gameObject); } rows.Clear(); LastDetails = null; if (tripInfo) tripInfo.text = ""; }
        private CancellationToken Begin() { lifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken); return lifetime.Token; }
        private void Cancel() { revision++; lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null; IsBusy = false; }
        private bool Current(int current) => this && IsOpen && current == revision && !string.IsNullOrEmpty(ownerId) && ownerId == account.SignedInUserId;
        private void SetBusy(bool busy)
        {
            IsBusy = busy; createButton.interactable = !busy && LastList != null; saveButton.interactable = !busy && targetId != null && LastDetails != null; retryButton.interactable = !busy;
            tripChoice.interactable = !busy && LastList?.Trips.Count > 0; tripName.interactable = startDate.interactable = endDate.interactable = !busy;
            foreach (var row in rows) if (row) row.SetInteractable(!busy);
        }
        private void OnDestroy()
        {
            Close();
            if (flights) { flights.SaveRequested -= OpenFlight; flights.ContentCleared -= Close; } if (hub) { hub.SaveRequested -= OpenPlace; hub.ContentCleared -= Close; } if (account) account.LoggedOut -= Close;
            if (homeButton) homeButton.onClick.RemoveListener(Browse); if (flightButton) flightButton.onClick.RemoveListener(Browse); if (hubButton) hubButton.onClick.RemoveListener(Browse); if (closeButton) closeButton.onClick.RemoveListener(Close);
            if (createButton) createButton.onClick.RemoveListener(Create); if (saveButton) saveButton.onClick.RemoveListener(Save); if (retryButton) retryButton.onClick.RemoveListener(Open); if (tripChoice) tripChoice.onValueChanged.RemoveListener(SelectTrip);
        }
    }
}
```

## Assets/TravelPlanning/UI/Trips/TripSmokeRunner.cs

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using TravelPlanning.Trips;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Flights;
using UnityEngine;

namespace TravelPlanning.UI.Trips
{
    /// <summary>Drives the same trip controls as a person, using two isolated test accounts.</summary>
    public sealed class TripSmokeRunner : MonoBehaviour
    {
        [SerializeField] private LoginPage account;
        [SerializeField] private FlightSearchPage flights;
        [SerializeField] private DestinationHubPage hub;
        [SerializeField] private SavedTripsPage trips;
        [SerializeField] private GameObject authCanvas, flightCanvas, hubCanvas, tripsCanvas;
        private int frames;
        private void Update() => frames++;
        private async void Start()
        {
            var args = Environment.GetCommandLineArgs(); if (Array.IndexOf(args, "-tripSmoke") < 0) return;
            try
            {
                int index = Array.IndexOf(args, "-authFile"); string file = index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
                if (!Debug.isDebugBuild || !file.StartsWith("auth-smoke-", StringComparison.Ordinal) || !file.EndsWith(".db", StringComparison.Ordinal) || System.IO.Path.GetFileName(file) != file || args.Any(x => x == "-authSmoke" || x == "-flightSmoke" || x == "-destinationSmoke" || x == "-reviewSmoke")) throw new InvalidOperationException("Saved-trip smoke checks require their isolated database and flag.");
                await RunChecksAsync(Array.IndexOf(args, "-tripReopen") < 0); Debug.Log("SAVED_TRIPS_UI_SMOKE_PASS"); Application.Quit(0);
            }
            catch (Exception exception) { Debug.LogError("SAVED_TRIPS_UI_SMOKE_FAIL " + exception.GetType().Name + ": " + exception.Message); Application.Quit(1); }
        }
        public async Task RunChecksAsync(bool register)
        {
            await WaitUntil(() => account.IsReady, "Account startup");
            var right = authCanvas.transform.Find("Right Panel"); var login = right.Find("LoginCard"); var registration = right.Find("RegistrationCard"); var home = right.Find("SignedInCard");
            const string mainEmail = "trips-smoke@example.test", otherEmail = "trips-other@example.test", password = "Travel saved trips demo phrase 2027!";
            if (register) await Register(login, registration, mainEmail, password);
            await Login(login, mainEmail, password);
            var main = tripsCanvas.transform.Find("Main"); Click(home, "SavedTripsButton"); await WaitTrips();
            if (register)
            {
                Require(trips.LastList.Trips.Count == 0, "A new user saw existing trips."); Click(main, "CreateForm/CreateAction/CreateButton"); await WaitTrips(); Require(trips.LastList.Trips.Count == 0 && trips.Status.Length > 0, "Empty trip name was accepted.");
                Require(Selected(main, "CreateForm/StartDate/Dropdown") == "2027-06-15" && Selected(main, "CreateForm/EndDate/Dropdown") == "2027-06-22", "Planning dates did not default to June 15-22.");
                Input(main, "CreateForm/TripName/Input").text = "Summer in London"; Click(main, "CreateForm/CreateAction/CreateButton"); await WaitTrips();
            }
            else Require(trips.LastList.Trips.Count == 1 && trips.LastDetails.Items.Count == 5, "Saved trips did not persist across player runs.");
            string ownedTripId = trips.LastDetails.Trip.Id; Click(main, "Header/CloseButton");
            Click(home, "SearchFlightsButton"); await WaitUntil(() => !flights.IsBusy && flights.LastResult != null, "Flights"); var flightResult = flights.LastResult;
            var flightRow = flightCanvas.transform.Find("Main/Results/Outbound/ScrollView/Viewport/Content").GetComponentsInChildren<FlightResultRow>().First(); Click(flightRow.transform, "SaveActions/SaveToTripButton"); await WaitTrips();
            Click(main, "SaveActions/SaveSelectedButton"); await WaitTrips(); Require(trips.LastDetails.Items.Any(item => item.Kind == SavedItemKind.Flight), "Flight was not saved.");
            Click(main, "SaveActions/SaveSelectedButton"); await WaitTrips(); Require(trips.Status.Contains("already"), "Duplicate save was not reported."); Click(main, "Header/CloseButton"); Require(ReferenceEquals(flightResult, flights.LastResult), "Closing trips reset flight results.");
            Click(flightCanvas.transform, "Main/Header/ExploreDestinationButton"); await WaitUntil(() => !hub.IsBusy && hub.LastResult != null, "Destination");
            foreach (string category in new[] { "Hotels", "Restaurants", "Experiences", "Hotspots" }) { ClickPlace(category); await WaitTrips(); Click(main, "SaveActions/SaveSelectedButton"); await WaitTrips(); Click(main, "Header/CloseButton"); }
            Click(hubCanvas.transform, "Main/Header/SavedTripsButton"); await WaitTrips(); Require(trips.LastDetails.Items.Count == 5 && trips.LastDetails.Items.Select(item => item.Kind).Distinct().Count() == 5, "Expected all five saved item kinds.");
            int hotspot = trips.LastDetails.Items.ToList().FindIndex(item => item.Kind == SavedItemKind.Hotspot); var savedRows = tripsCanvas.GetComponentsInChildren<SavedTripRow>(); Click(savedRows[hotspot].transform, "Top/RemoveButton"); await WaitTrips(); Require(trips.LastDetails.Items.Count == 4, "Remove item failed."); Click(main, "Header/CloseButton");
            ClickPlace("Hotspots"); await WaitTrips(); Click(main, "SaveActions/SaveSelectedButton"); await WaitTrips(); Require(trips.LastDetails.Items.Count == 5, "Re-saving removed item failed.");
            await CheckFailedLoad(main, ownedTripId);
            await WithBlockedDatabase(async () =>
            {
                Input(main, "CreateForm/TripName/Input").text = "Queued must cancel"; Click(main, "CreateForm/CreateAction/CreateButton"); int before = frames; await Task.Delay(60);
                Require(trips.IsBusy && frames > before, "UI froze during a queued trip write."); Click(hubCanvas.transform, "Main/Header/LogoutButton"); Require(!trips.IsOpen && trips.LastDetails == null && account.SignedInUserId == null, "Logout left saved-trip state open.");
            });
            await Task.Delay(30); Require(!trips.IsOpen && trips.LastList == null, "A cancelled write repopulated the old user's UI.");
            if (register) await Register(login, registration, otherEmail, password);
            await Login(login, otherEmail, password); Click(home, "SavedTripsButton"); Require(main.Find("TripSelection/TripChoice").GetComponent<TMP_Dropdown>().options.Count == 0, "Old user trip names remained visible during loading."); await WaitTrips();
            Require(!trips.LastList.Trips.Any(trip => trip.Id == ownedTripId), "Another user saw the first user's trip.");
            if (register) { Require(trips.LastList.Trips.Count == 0, "The second user should begin with no trips."); Input(main, "CreateForm/TripName/Input").text = "Other user's private plan"; Click(main, "CreateForm/CreateAction/CreateButton"); await WaitTrips(); }
            Require(trips.LastList.Trips.Count == 1 && trips.LastDetails.Items.Count == 0, "Second user's isolated plan did not persist."); Click(main, "Header/CloseButton"); Click(home, "LogoutButton");
            await Login(login, mainEmail, password); Click(home, "SavedTripsButton"); await WaitTrips(); Require(trips.LastList.Trips.Count == 1 && trips.LastDetails.Trip.Id == ownedTripId && trips.LastDetails.Items.Count == 5, "Main user data or queued-write cancellation was incorrect.");
            Require(trips.LastDetails.WorkerThreadId != Thread.CurrentThread.ManagedThreadId, "Trip query ran on the UI thread."); Debug.Log("SAVED_TRIPS_LOAD_TIMING databaseMs=" + trips.LastDetails.ElapsedMilliseconds + " worker=" + trips.LastDetails.WorkerThreadId + " main=" + Thread.CurrentThread.ManagedThreadId);
            Click(main, "Header/CloseButton"); Click(home, "LogoutButton");
        }
        private async Task CheckFailedLoad(Transform main, string ownedTripId)
        {
            Input(main, "CreateForm/TripName/Input").text = "Load failure probe"; Click(main, "CreateForm/CreateAction/CreateButton"); await WaitTrips(); string probeId = trips.LastDetails.Trip.Id;
            var choices = main.Find("TripSelection/TripChoice").GetComponent<TMP_Dropdown>(); int mainIndex = trips.LastList.Trips.ToList().FindIndex(trip => trip.Id == ownedTripId), probeIndex = trips.LastList.Trips.ToList().FindIndex(trip => trip.Id == probeId);
            choices.value = mainIndex; await WaitTrips();
            // Only this isolated empty fixture trip is removed to reproduce a stale selection.
            string userId = account.SignedInUserId;
            await account.Database.ExecuteAsync(connection => connection.Execute("DELETE FROM trips WHERE id = ? AND user_id = ?", probeId, userId), CancellationToken.None);
            choices.value = probeIndex; await WaitTrips(); Require(trips.LastDetails == null && !main.Find("SaveActions/SaveSelectedButton").GetComponent<UnityEngine.UI.Button>().interactable && tripsCanvas.GetComponentsInChildren<SavedTripRow>().Length == 0, "Failed load left actions attached to the previous trip.");
            Click(main, "TripSelection/RetryButton"); await WaitTrips(); Require(trips.LastDetails.Trip.Id == ownedTripId, "Retry did not recover the remaining trip.");
        }
        private void ClickPlace(string category) { var card = hubCanvas.transform.Find("Main/ScrollView/Viewport/Content/" + category + "/Items").GetComponentsInChildren<PlaceCard>().First(); Click(card.transform, "ReviewActions/SaveToTripButton"); }
        private async Task WithBlockedDatabase(Func<Task> action)
        {
            using (var release = new ManualResetEventSlim(false))
            {
                var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var blocker = account.Database.ExecuteAsync(connection => { entered.SetResult(true); if (!release.Wait(10000)) throw new TimeoutException("Trip smoke gate timeout."); return true; }, CancellationToken.None);
                try { await entered.Task; await action(); } finally { release.Set(); } await blocker;
            }
        }
        private async Task Register(Transform login, Transform registration, string email, string password) { Click(login, "Create an account"); Input(registration, "EmailInput").text = email; Input(registration, "PasswordInput").text = Input(registration, "ConfirmationInput").text = password; Click(registration, "LoginButton"); await WaitUntil(() => !account.IsBusy, "Registration"); Require(login.gameObject.activeSelf, "Registration failed."); }
        private async Task Login(Transform login, string email, string password) { Input(login, "EmailInput").text = email; Input(login, "PasswordInput").text = password; Click(login, "LoginButton"); await WaitUntil(() => !account.IsBusy, "Sign in"); Require(account.SignedInEmail == email, "Sign in failed."); }
        private Task WaitTrips() => WaitUntil(() => !trips.IsBusy && (trips.LastList != null || !trips.IsOpen), "Saved trips");
        private static string Selected(Transform root, string path) { var dropdown = root.Find(path).GetComponent<TMP_Dropdown>(); return dropdown.options[dropdown.value].text; }
        private static void Click(Transform root, string path) => root.Find(path).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        private static TMP_InputField Input(Transform root, string path) => root.Find(path).GetComponent<TMP_InputField>();
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static async Task WaitUntil(Func<bool> predicate, string operation) { var watch = System.Diagnostics.Stopwatch.StartNew(); while (!predicate()) { if (watch.ElapsedMilliseconds > 45000) throw new TimeoutException(operation); await Task.Delay(10); } }
    }
}
```

## Assets/TravelPlanning/UI/Editor/TripSetup.cs

```csharp
using System;
using System.IO;
using TMPro;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Flights;
using TravelPlanning.UI.Reviews;
using TravelPlanning.UI.Trips;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Adds saved-trip controls while preserving the existing login and feature screens.</summary>
    public static class TripSetup
    {
        public const string ScenePath = AuthSetup.ScenePath;
        public const string BuildPath = "Builds/SavedTrips/TravelPlannerTrips.exe";
        public const string RowPath = "Assets/TravelPlanning/Prefabs/Trips/SavedTripRow.prefab";
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        [MenuItem("Travel Planning/Saved Trips/1 - Prepare Saved Trips")]
        public static void Prepare()
        {
            ReviewSetup.Prepare(); UpgradeCards();
            var account = UnityEngine.Object.FindFirstObjectByType<LoginPage>(); var flights = UnityEngine.Object.FindFirstObjectByType<FlightSearchPage>(); var hub = UnityEngine.Object.FindFirstObjectByType<DestinationHubPage>(); var reviews = UnityEngine.Object.FindFirstObjectByType<ReviewsPage>();
            var authCanvas = GameObject.Find("Canvas"); var flightCanvas = (GameObject)new SerializedObject(flights).FindProperty("flightCanvas").objectReferenceValue; var hubCanvas = (GameObject)new SerializedObject(hub).FindProperty("hubCanvas").objectReferenceValue;
            var existing = UnityEngine.Object.FindFirstObjectByType<SavedTripsPage>();
            if (existing)
            {
                AssignUnderlyingGroups(existing, authCanvas, flightCanvas, hubCanvas);
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                Debug.Log("SAVED_TRIPS_SCENE_READY " + ScenePath); return;
            }
            var signedIn = authCanvas.transform.Find("Right Panel/SignedInCard"); var logout = signedIn.Find("LogoutButton"); var home = UnityEngine.Object.Instantiate(logout.gameObject, signedIn).GetComponent<UnityEngine.UI.Button>(); home.name = "SavedTripsButton"; home.GetComponentInChildren<TMP_Text>().text = "Saved trips"; home.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -460); logout.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -540);
            var flightEntry = Button(flightCanvas.transform.Find("Main/Header"), "SavedTripsButton", "Saved trips", 210); flightEntry.transform.SetSiblingIndex(2);
            var hubEntry = Button(hubCanvas.transform.Find("Main/Header"), "SavedTripsButton", "Saved trips", 210); hubEntry.transform.SetSiblingIndex(1);
            var canvas = new GameObject("SavedTripsCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster)); canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; canvas.GetComponent<Canvas>().sortingOrder = 5;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0;
            var blocker = Rect("InputBlocker", canvas.transform); Stretch(blocker); blocker.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.white;
            var main = Rect("Main", canvas.transform); Stretch(main); main.offsetMin = new Vector2(40, 32); main.offsetMax = new Vector2(-40, -32); Vertical(main, 12);
            var header = Horizontal("Header", main, 64); var title = Label(header, "Title", "Saved trips", 40); Layout(title.gameObject, -1, 1); var close = Button(header, "CloseButton", "Close", 180);
            var preview = Label(main, "TargetPreview", "", 25); Layout(preview.gameObject, 50);
            var selection = Horizontal("TripSelection", main, 56); var tripLabel = Label(selection, "Label", "Your trip", 24); Layout(tripLabel.gameObject, -1, 0, 150);
            var dropdownTemplate = flightCanvas.transform.Find("Main/RouteFields/DepartureDate/Dropdown").gameObject;
            var choice = Dropdown(dropdownTemplate, selection, "TripChoice"); Layout(choice.gameObject, 54, 1); var retry = Button(selection, "RetryButton", "Retry", 160);
            var form = Horizontal("CreateForm", main, 88);
            var nameCell = Cell(form, "TripName", "New trip name (1-60 characters)"); var nameObject = UnityEngine.Object.Instantiate(flightCanvas.transform.Find("Main/FilterFields/MaximumPrice/Input").gameObject, nameCell); nameObject.name = "Input"; var name = nameObject.GetComponent<TMP_InputField>(); name.characterLimit = 60; name.contentType = TMP_InputField.ContentType.Standard; ((TMP_Text)name.placeholder).text = "For example: Summer in London"; foreach (var text in nameObject.GetComponentsInChildren<TMP_Text>(true)) text.richText = false; Layout(nameObject, 54);
            var start = Dropdown(dropdownTemplate, Cell(form, "StartDate", "Planning start date"), "Dropdown"); var end = Dropdown(dropdownTemplate, Cell(form, "EndDate", "Planning end date"), "Dropdown"); var create = Button(Cell(form, "CreateAction", "Create a new plan"), "CreateButton", "Create trip", 220);
            var note = Label(main, "PlanningNote", "Planning dates do not change flight dates. Current sample prices; saving does not reserve or purchase anything.", 22); Layout(note.gameObject, 44);
            var actions = Horizontal("SaveActions", main, 48); var save = Button(actions, "SaveSelectedButton", "Save selected item", 300);
            var status = Label(main, "Status", "", 23); Layout(status.gameObject, 50); var info = Label(main, "TripInfo", "", 23); Layout(info.gameObject, 44);
            var rootScroll = Rect("ScrollView", main); Layout(rootScroll.gameObject, -1, 1); var scroll = rootScroll.gameObject.AddComponent<UnityEngine.UI.ScrollRect>(); scroll.horizontal = false; scroll.scrollSensitivity = 45; scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            var viewport = Rect("Viewport", rootScroll); Stretch(viewport); viewport.gameObject.AddComponent<UnityEngine.UI.Image>(); viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var content = Rect("Content", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero; Vertical(content, 16); content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize; scroll.content = content; scroll.viewport = viewport;
            var controller = new GameObject("SavedTripsController").AddComponent<SavedTripsPage>(); var fields = new SerializedObject(controller);
            Set(fields, "account", account); Set(fields, "flights", flights); Set(fields, "hub", hub); Set(fields, "reviews", reviews); Set(fields, "modalCanvas", canvas); Set(fields, "homeButton", home); Set(fields, "flightButton", flightEntry); Set(fields, "hubButton", hubEntry); Set(fields, "closeButton", close); Set(fields, "createButton", create); Set(fields, "saveButton", save); Set(fields, "retryButton", retry); Set(fields, "tripName", name); Set(fields, "tripChoice", choice); Set(fields, "startDate", start); Set(fields, "endDate", end); Set(fields, "targetPreview", preview); Set(fields, "status", status); Set(fields, "tripInfo", info); Set(fields, "scroll", scroll); Set(fields, "rowPrefab", CreateRow());
            fields.ApplyModifiedPropertiesWithoutUndo(); AssignUnderlyingGroups(controller, authCanvas, flightCanvas, hubCanvas);
            var runner = controller.gameObject.AddComponent<TripSmokeRunner>(); var runnerFields = new SerializedObject(runner); Set(runnerFields, "account", account); Set(runnerFields, "flights", flights); Set(runnerFields, "hub", hub); Set(runnerFields, "trips", controller); Set(runnerFields, "authCanvas", authCanvas); Set(runnerFields, "flightCanvas", flightCanvas); Set(runnerFields, "hubCanvas", hubCanvas); Set(runnerFields, "tripsCanvas", canvas); runnerFields.ApplyModifiedPropertiesWithoutUndo();
            canvas.SetActive(false); EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); Debug.Log("SAVED_TRIPS_SCENE_READY " + ScenePath);
        }
        private static void AssignUnderlyingGroups(SavedTripsPage controller, params GameObject[] sources)
        {
            var components = new CanvasGroup[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                // Unity objects have their own null check; a missing native component can retain a managed wrapper.
                var group = sources[i].GetComponent<CanvasGroup>();
                if (!group) group = sources[i].AddComponent<CanvasGroup>();
                if (!group) throw new InvalidOperationException("Could not add the input group to " + sources[i].name);
                components[i] = group;
            }
            var fields = new SerializedObject(controller); var groups = fields.FindProperty("underlyingGroups"); groups.arraySize = components.Length;
            for (int i = 0; i < components.Length; i++) groups.GetArrayElementAtIndex(i).objectReferenceValue = components[i];
            fields.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void UpgradeCards()
        {
            UpgradeCard(FlightSearchSetup.RowPath, true); UpgradeCard(DestinationHubSetup.PlaceCardPath, false);
        }
        private static void UpgradeCard(string path, bool flight)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var component = flight ? (UnityEngine.Object)root.GetComponent<FlightResultRow>() : root.GetComponent<PlaceCard>(); var fields = new SerializedObject(component);
                if (!fields.FindProperty("saveButton").objectReferenceValue)
                {
                    var actions = flight ? Horizontal("SaveActions", root.transform, 48) : root.transform.Find("ReviewActions").GetComponent<RectTransform>(); var save = Button(actions, "SaveToTripButton", "Save to trip", 230); Set(fields, "saveButton", save); fields.ApplyModifiedPropertiesWithoutUndo();
                    if (flight) root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 200;
                }
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) text.richText = false;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        [MenuItem("Travel Planning/Saved Trips/2 - Build Windows x64")]
        public static void BuildWindows()
        {
            Prepare(); Directory.CreateDirectory(Path.GetDirectoryName(BuildPath)); var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = BuildPath, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development }); if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Saved trips build failed.");
            var notices = Path.Combine(Path.GetDirectoryName(BuildPath), "ThirdPartyNotices"); Directory.CreateDirectory(notices); foreach (string file in Directory.GetFiles("Assets/Plugins/SQLite/Licenses")) if (!file.EndsWith(".meta")) File.Copy(file, Path.Combine(notices, Path.GetFileName(file)), true); File.Copy("Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt", Path.Combine(notices, "LiberationSans-OFL.txt"), true); File.Copy("Assets/Plugins/LiteDB/LICENSE.txt", Path.Combine(notices, "LiteDB-LICENSE.txt"), true); Debug.Log("SAVED_TRIPS_BUILD_PASS " + Path.GetFullPath(BuildPath));
        }
        private static SavedTripRow CreateRow()
        {
            if (File.Exists(RowPath)) return AssetDatabase.LoadAssetAtPath<GameObject>(RowPath).GetComponent<SavedTripRow>();
            Directory.CreateDirectory(Path.GetDirectoryName(RowPath)); var root = Rect("SavedTripRow", null); root.sizeDelta = new Vector2(1800, 210); root.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(244, 247, 255, 255); var layout = Vertical(root, 8); layout.padding = new RectOffset(20, 20, 16, 16);
            var top = Horizontal("Top", root, 48); var title = Label(top, "Title", "", 27); Layout(title.gameObject, -1, 1); var remove = Button(top, "RemoveButton", "Remove item", 240); var price = Label(root, "Price", "", 24); Layout(price.gameObject, 34); var details = Label(root, "Details", "", 23); details.textWrappingMode = TextWrappingModes.Normal;
            var row = root.gameObject.AddComponent<SavedTripRow>(); var fields = new SerializedObject(row); Set(fields, "title", title); Set(fields, "details", details); Set(fields, "price", price); Set(fields, "removeButton", remove); fields.ApplyModifiedPropertiesWithoutUndo(); var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, RowPath); UnityEngine.Object.DestroyImmediate(root.gameObject); return prefab.GetComponent<SavedTripRow>();
        }
        private static TMP_Dropdown Dropdown(GameObject template, Transform parent, string name) { var obj = UnityEngine.Object.Instantiate(template, parent); obj.name = name; Layout(obj, 54); foreach (var text in obj.GetComponentsInChildren<TMP_Text>(true)) text.richText = false; var dropdown = obj.GetComponent<TMP_Dropdown>(); dropdown.ClearOptions(); return dropdown; }
        private static RectTransform Cell(Transform parent, string name, string caption) { var cell = Rect(name, parent); Layout(cell.gameObject, -1, 1); Vertical(cell, 4); var label = Label(cell, "Label", caption, 21); Layout(label.gameObject, 30); return cell; }
        private static UnityEngine.UI.Button Button(Transform parent, string name, string text, float width) { var rect = Rect(name, parent); Layout(rect.gameObject, 48, 0, width); var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color32(49, 87, 255, 255); var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image; var label = Label(rect, "Label", text, 24); Stretch(label.rectTransform); label.alignment = TextAlignmentOptions.Center; label.color = Color.white; return button; }
        private static RectTransform Horizontal(string name, Transform parent, float height) { var rect = Rect(name, parent); Layout(rect.gameObject, height); var layout = rect.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>(); layout.spacing = 16; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = true; return rect; }
        private static UnityEngine.UI.VerticalLayoutGroup Vertical(RectTransform rect, int spacing) { var layout = rect.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(); layout.spacing = spacing; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false; return layout; }
        private static TMP_Text Label(Transform parent, string name, string text, int size) { var rect = Rect(name, parent); var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = Font; label.text = text; label.fontSize = size; label.color = new Color32(51, 51, 51, 255); label.raycastTarget = false; label.richText = false; return label; }
        private static RectTransform Rect(string name, Transform parent) { var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false); return obj.GetComponent<RectTransform>(); }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Layout(GameObject obj, float height = -1, float flexible = 0, float width = -1) { var layout = obj.GetComponent<UnityEngine.UI.LayoutElement>(); if (!layout) layout = obj.AddComponent<UnityEngine.UI.LayoutElement>(); layout.preferredHeight = height; layout.flexibleHeight = flexible; layout.flexibleWidth = flexible; layout.preferredWidth = width; }
        private static void Set(SerializedObject fields, string name, UnityEngine.Object value) => fields.FindProperty(name).objectReferenceValue = value;
    }
}
```

## Assets/TravelPlanning/Tests/EditMode/TripServiceTests.cs

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
using TravelPlanning.Trips;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class TripServiceTests
    {
        private string folder;
        private string path;
        private TravelDatabase database;
        private TripService owner;
        private TripService other;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "TravelTripTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, "travel.db");
            database = OpenDatabase();
            owner = new TripService(database, "owner");
            other = new TripService(database, "other");
        }

        [TearDown]
        public void TearDown() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }

        [UnityTest]
        public IEnumerator ListsAndEveryTripOperationRespectOwner()
        {
            yield return Ready();
            var first = owner.CreateAsync("My trip", "2027-06-15", "2027-06-22");
            var second = other.CreateAsync("Other trip", "2027-06-15", "2027-06-22");
            yield return Wait(first); yield return Wait(second);
            var saved = owner.SaveAsync(first.Result.Id, SavedItemKind.Hotel, "london-hotel-1");
            yield return Wait(saved);
            var ownList = owner.ListAsync(); var otherList = other.ListAsync();
            yield return Wait(ownList); yield return Wait(otherList);
            Assert.That(ownList.Result.Trips.Single().Id, Is.EqualTo(first.Result.Id));
            Assert.That(otherList.Result.Trips.Single().Id, Is.EqualTo(second.Result.Id));
            Assert.That(ownList.Result.StartDate, Is.EqualTo("2027-06-01"));
            Assert.That(ownList.Result.EndDate, Is.EqualTo("2027-06-30"));
            var owned = owner.LoadAsync(first.Result.Id); yield return Wait(owned);
            var missing = other.LoadAsync("missing"); var forbidden = other.LoadAsync(first.Result.Id);
            yield return Failure(missing); yield return Failure(forbidden);
            Assert.That(forbidden.Exception.GetBaseException().Message, Is.EqualTo(missing.Exception.GetBaseException().Message));
            yield return Failure(other.SaveAsync(first.Result.Id, SavedItemKind.Hotel, "london-hotel-2"));
            yield return Failure(other.RemoveAsync(first.Result.Id, owned.Result.Items.Single().Id));
            var cannotRemoveThroughOwnTrip = other.RemoveAsync(second.Result.Id, owned.Result.Items.Single().Id);
            yield return Wait(cannotRemoveThroughOwnTrip);
            Assert.That(cannotRemoveThroughOwnTrip.Result, Is.False);
            var preserved = owner.LoadAsync(first.Result.Id); yield return Wait(preserved);
            Assert.That(preserved.Result.Items.Single().TargetId, Is.EqualTo("london-hotel-1"));
        }

        [UnityTest]
        public IEnumerator AllFiveTypesHaveCurrentDetailsUnitsAndOwnFlightDates()
        {
            yield return Ready();
            var created = owner.CreateAsync(" Planning labels ", "2027-06-01", "2027-06-02");
            yield return Wait(created);
            Assert.That(created.Result.Name, Is.EqualTo("Planning labels"));
            string[] targets = { "JFK-LHR-20270615-AA", "london-hotel-1", "london-restaurant-1", "london-experience-1", "london-hotspot-1" };
            foreach (SavedItemKind kind in Enum.GetValues(typeof(SavedItemKind)))
            {
                var save = owner.SaveAsync(created.Result.Id, kind, targets[(int)kind]);
                yield return Wait(save);
                Assert.That(save.Result, Is.True);
            }
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            var loaded = owner.LoadAsync(created.Result.Id); yield return Wait(loaded);
            var details = loaded.Result;
            Assert.That(details.Items.Count, Is.EqualTo(5));
            Assert.That(details.Trip.ItemCount, Is.EqualTo(5));
            Assert.That(details.WorkerThreadId, Is.Not.EqualTo(mainThread));
            string[] units = { "/ flight", "/ room / night", "/ person (meal)", "/ adult", "/ visit" };
            int[] prices = { 56200, 16000, 2500, 3000, 0 };
            foreach (var item in details.Items)
            {
                Assert.That(item.TargetId, Is.EqualTo(targets[(int)item.Kind]));
                Assert.That(item.Title, Is.Not.Empty);
                Assert.That(item.Details, Is.Not.Empty);
                Assert.That(item.Currency, Is.EqualTo("USD"));
                Assert.That(item.PriceUnit, Is.EqualTo(units[(int)item.Kind]));
                Assert.That(item.PriceCents, Is.EqualTo(prices[(int)item.Kind]));
            }
            var flight = details.Items.Single(item => item.Kind == SavedItemKind.Flight);
            Assert.That(flight.Details, Does.Contain("JFK to LHR"));
            Assert.That(flight.Details, Does.Contain("2027-06-15 17:00 (JFK local)"));
            Assert.That(flight.Details, Does.Contain("2027-06-16 05:00 (LHR local)"));
            var list = owner.ListAsync(); yield return Wait(list);
            Assert.That(list.Result.WorkerThreadId, Is.Not.EqualTo(mainThread));
            Assert.That(list.Result.Trips.Single().ItemCount, Is.EqualTo(5));
        }

        [UnityTest]
        public IEnumerator DuplicateRaceSavesOnceButSameTargetCanBelongToTwoTrips()
        {
            yield return Ready();
            var first = owner.CreateAsync("First", "2027-06-15", "2027-06-22");
            var second = owner.CreateAsync("Second", "2027-06-15", "2027-06-22");
            yield return Wait(first); yield return Wait(second);
            var anotherService = new TripService(database, "owner");
            var race = Task.WhenAll(owner.SaveAsync(first.Result.Id, SavedItemKind.Hotel, "london-hotel-1"), anotherService.SaveAsync(first.Result.Id, SavedItemKind.Hotel, "london-hotel-1"));
            yield return Wait(race);
            Assert.That(race.Result.Count(added => added), Is.EqualTo(1));
            var separate = owner.SaveAsync(second.Result.Id, SavedItemKind.Hotel, "london-hotel-1"); yield return Wait(separate);
            Assert.That(separate.Result, Is.True);
            var load = owner.LoadAsync(first.Result.Id); yield return Wait(load);
            Assert.That(load.Result.Items.Count, Is.EqualTo(1));
            byte[] beforeDuplicate = Hash();
            var duplicate = owner.SaveAsync(first.Result.Id, SavedItemKind.Hotel, "london-hotel-1"); yield return Wait(duplicate);
            Assert.That(duplicate.Result, Is.False);
            Assert.That(Hash(), Is.EqualTo(beforeDuplicate));
            var removed = owner.RemoveAsync(first.Result.Id, load.Result.Items.Single().Id); yield return Wait(removed);
            Assert.That(removed.Result, Is.True);
            var again = owner.RemoveAsync(first.Result.Id, load.Result.Items.Single().Id); yield return Wait(again);
            Assert.That(again.Result, Is.False);
            var empty = owner.LoadAsync(first.Result.Id); var untouched = owner.LoadAsync(second.Result.Id);
            yield return Wait(empty); yield return Wait(untouched);
            Assert.That(empty.Result.Trip.ItemCount, Is.Zero);
            Assert.That(empty.Result.Items, Is.Empty);
            Assert.That(untouched.Result.Items.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ReopenKeepsSavedReferencesAndReloadUsesChangedCatalogPrice()
        {
            yield return Ready();
            var created = owner.CreateAsync("Persistent", "2027-06-15", "2027-06-22"); yield return Wait(created);
            yield return Wait(owner.SaveAsync(created.Result.Id, SavedItemKind.Hotel, "london-hotel-1"));
            yield return Wait(owner.SaveAsync(created.Result.Id, SavedItemKind.Restaurant, "london-restaurant-1"));
            var reopened = OpenDatabase(); yield return Wait(reopened.InitializeAsync(CancellationToken.None));
            var service = new TripService(reopened, "owner");
            var original = service.LoadAsync(created.Result.Id); yield return Wait(original);
            Assert.That(original.Result.Items.Single(item => item.Kind == SavedItemKind.Hotel).PriceCents, Is.EqualTo(16000));
            yield return Wait(reopened.ExecuteAsync(connection =>
            {
                connection.Execute("UPDATE hotels SET name=?,price_cents=? WHERE id=?", "Updated hotel", 19999, "london-hotel-1");
                connection.Execute("UPDATE restaurants SET price_cents=NULL WHERE id=?", "london-restaurant-1");
                return 0;
            }, CancellationToken.None));
            var current = service.LoadAsync(created.Result.Id); yield return Wait(current);
            Assert.That(current.Result.Items.Single(item => item.Kind == SavedItemKind.Hotel).PriceCents, Is.EqualTo(19999));
            Assert.That(current.Result.Items.Single(item => item.Kind == SavedItemKind.Hotel).Title, Is.EqualTo("Updated hotel"));
            Assert.That(current.Result.Items.Single(item => item.Kind == SavedItemKind.Restaurant).PriceCents, Is.Null);
            Assert.That(current.Result.Items.Select(item => item.Id), Is.EqualTo(original.Result.Items.Select(item => item.Id)));
        }

        [UnityTest]
        public IEnumerator NamesDatesAndIdentifiersAreValidatedAndSqlTextIsOnlyData()
        {
            yield return Ready();
            foreach (string name in new[] { null, "", "  ", new string('x', 61), "line\nbreak", "\tTrip" })
                Assert.Throws<ArgumentException>(() => owner.CreateAsync(name, "2027-06-15", "2027-06-22"));
            string[][] invalidDates = {
                new[] { "2027-06-22", "2027-06-15" }, new[] { "2027-05-31", "2027-06-22" },
                new[] { "2027-06-15", "2027-07-01" }, new[] { "06/15/2027", "2027-06-22" },
                new[] { "2027-06-31", "2027-06-31" }, new[] { "2027-06-15' OR 1=1 --", "2027-06-22" },
                new string[] { null, "2027-06-22" }
            };
            foreach (var dates in invalidDates) yield return Failure(owner.CreateAsync("Trip", dates[0], dates[1]));
            var smallest = owner.CreateAsync("x", "2027-06-01", "2027-06-01");
            var largest = owner.CreateAsync(new string('x', 60), "2027-06-01", "2027-06-30");
            yield return Wait(smallest); yield return Wait(largest);
            const string sqlName = "Trip'); DROP TABLE trips; --";
            var literal = owner.CreateAsync(sqlName, "2027-06-15", "2027-06-22"); yield return Wait(literal);
            Assert.That(literal.Result.Name, Is.EqualTo(sqlName));
            Assert.Throws<ArgumentException>(() => owner.SaveAsync(literal.Result.Id, (SavedItemKind)99, "london-hotel-1"));
            yield return Failure(owner.LoadAsync("' OR 1=1 --"));
            yield return Failure(owner.SaveAsync(literal.Result.Id, SavedItemKind.Hotel, "london-restaurant-1"));
            yield return Failure(owner.SaveAsync(literal.Result.Id, SavedItemKind.Hotel, "london-hotel-1' OR 1=1 --"));
            yield return Failure(owner.SaveAsync("' OR 1=1 --", SavedItemKind.Hotel, "london-hotel-1"));
            yield return Failure(owner.RemoveAsync("' OR 1=1 --", "anything"));
            var harmless = owner.RemoveAsync(literal.Result.Id, "' OR 1=1 --"); yield return Wait(harmless);
            Assert.That(harmless.Result, Is.False);
            var list = owner.ListAsync(); yield return Wait(list);
            Assert.That(list.Result.Trips.Count, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator CancelledQueuedWritesDoNotMutateDatabase()
        {
            yield return Ready();
            var created = owner.CreateAsync("Keep", "2027-06-15", "2027-06-22"); yield return Wait(created);
            yield return Wait(owner.SaveAsync(created.Result.Id, SavedItemKind.Hotel, "london-hotel-1"));
            var trip = owner.LoadAsync(created.Result.Id); yield return Wait(trip);
            byte[] before = Hash();
            using (var release = new ManualResetEventSlim(false))
            using (var entered = new ManualResetEventSlim(false))
            using (var cancellation = new CancellationTokenSource())
            {
                var blocker = database.ExecuteAsync(connection => { entered.Set(); return release.Wait(TimeSpan.FromSeconds(15)); }, CancellationToken.None);
                while (!entered.IsSet) yield return null;
                Task[] pending;
                try
                {
                    pending = new Task[] {
                        owner.CreateAsync("Cancelled", "2027-06-15", "2027-06-22", cancellation.Token),
                        owner.SaveAsync(created.Result.Id, SavedItemKind.Hotel, "london-hotel-2", cancellation.Token),
                        owner.RemoveAsync(created.Result.Id, trip.Result.Items.Single().Id, cancellation.Token)
                    };
                    cancellation.Cancel();
                }
                finally { release.Set(); }
                yield return Wait(blocker);
                foreach (var operation in pending)
                {
                    yield return Completion(operation);
                    Assert.That(operation.IsCanceled, Is.True);
                }
            }
            Assert.That(Hash(), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator ReadOnlyCallsLeaveTripAndCatalogBytesUnchanged()
        {
            yield return Ready();
            var created = owner.CreateAsync("Keep", "2027-06-15", "2027-06-22"); yield return Wait(created);
            yield return Wait(owner.SaveAsync(created.Result.Id, SavedItemKind.Hotspot, "london-hotspot-1"));
            byte[] before = Hash();
            yield return Wait(owner.ListAsync()); yield return Wait(owner.LoadAsync(created.Result.Id));
            Assert.That(Hash(), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator InvalidCatalogCalendarAndUnknownAccountFailWithoutCreatingTrip()
        {
            yield return Ready();
            var unknown = new TripService(database, "owner' OR 1=1 --");
            var create = unknown.CreateAsync("No owner", "2027-06-15", "2027-06-22");
            yield return Completion(create);
            Assert.That(create.Exception.GetBaseException(), Is.InstanceOf<InvalidOperationException>());
            yield return Wait(database.ExecuteAsync(connection => connection.Execute("UPDATE metadata SET value=? WHERE key=?", "2030-06-30", "seed_end_date"), CancellationToken.None));
            var list = owner.ListAsync(); yield return Completion(list);
            Assert.That(list.Exception.GetBaseException(), Is.InstanceOf<InvalidOperationException>());
            var invalid = owner.CreateAsync("No calendar", "2027-06-15", "2027-06-22"); yield return Completion(invalid);
            Assert.That(invalid.Exception.GetBaseException(), Is.InstanceOf<InvalidOperationException>());
            var count = database.ExecuteAsync(connection => connection.ExecuteScalar<int>("SELECT COUNT(*) FROM trips"), CancellationToken.None);
            yield return Wait(count);
            Assert.That(count.Result, Is.Zero);
        }

        private IEnumerator Ready()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(database.ExecuteAsync(connection =>
            {
                foreach (string id in new[] { "owner", "other" }) connection.Execute(
                    "INSERT INTO users (id,email_normalized,password_hash,password_salt,password_iterations,created_utc) VALUES (?,?,?,?,?,?)",
                    id, id + "@example.com", "testfixture", "testfixture", 600000, "2026-09-29T00:00:00Z");
                return 0;
            }, CancellationToken.None));
        }
        private TravelDatabase OpenDatabase() => new TravelDatabase(Path.Combine(Application.streamingAssetsPath, "Database", "travel_seed.db"), path);
        private byte[] Hash() { using (var hash = SHA256.Create()) return hash.ComputeHash(File.ReadAllBytes(path)); }
        private static IEnumerator Wait(Task task) { yield return Completion(task); task.GetAwaiter().GetResult(); }
        private static IEnumerator Failure(Task task) { yield return Completion(task); Assert.That(task.IsFaulted, Is.True); Assert.That(task.Exception.GetBaseException(), Is.InstanceOf<ArgumentException>()); }
        private static IEnumerator Completion(Task task)
        {
            double deadline = UnityEditor.EditorApplication.timeSinceStartup + 30;
            while (!task.IsCompleted)
            {
                Assert.That(UnityEditor.EditorApplication.timeSinceStartup, Is.LessThan(deadline), "Trip operation timed out.");
                yield return null;
            }
        }
    }
}

```

## Assets/TravelPlanning/Tests/EditMode/TripSceneTests.cs

```csharp
using System.Collections;
using NUnit.Framework;
using TMPro;
using TravelPlanning.Trips;
using TravelPlanning.UI;
using TravelPlanning.UI.Editor;
using TravelPlanning.UI.Trips;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class TripSceneTests
    {
        [Test]
        public void TripModalReferencesAndSourcePrefabButtonsAreComplete()
        {
            EditorSceneManager.OpenScene(TripSetup.ScenePath); var page = Object.FindFirstObjectByType<SavedTripsPage>(); Assert.That(page.transform.parent, Is.Null); var fields = new SerializedObject(page);
            foreach (string name in new[] { "account", "flights", "hub", "reviews", "modalCanvas", "homeButton", "flightButton", "hubButton", "closeButton", "createButton", "saveButton", "retryButton", "tripName", "tripChoice", "startDate", "endDate", "targetPreview", "status", "tripInfo", "scroll", "rowPrefab" }) Assert.That(fields.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
            var groups = fields.FindProperty("underlyingGroups"); Assert.That(groups.arraySize, Is.EqualTo(3)); for (int i = 0; i < groups.arraySize; i++) Assert.That(groups.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null);
            var modal = (GameObject)fields.FindProperty("modalCanvas").objectReferenceValue; Assert.That(modal.activeSelf, Is.False); Assert.That(modal.GetComponent<Canvas>().sortingOrder, Is.EqualTo(5));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(FlightSearchSetup.RowPath).transform.Find("SaveActions/SaveToTripButton"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(DestinationHubSetup.PlaceCardPath).transform.Find("ReviewActions/SaveToTripButton"), Is.Not.Null);
            modal.SetActive(true); Canvas.ForceUpdateCanvases(); var scroll = (UnityEngine.UI.ScrollRect)fields.FindProperty("scroll").objectReferenceValue; Assert.That(scroll.viewport.rect.width, Is.GreaterThan(200)); Assert.That(scroll.viewport.rect.height, Is.GreaterThan(100));
            Assert.That(GameObject.Find("Canvas/Right Panel/LoginCard/EmailInput").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(0, -230)));
        }
        [Test]
        public void SavedItemRowShowsLiteralDetailsCurrentPriceAndUnits()
        {
            EditorSceneManager.OpenScene(TripSetup.ScenePath); var fields = new SerializedObject(Object.FindFirstObjectByType<SavedTripsPage>()); ((GameObject)fields.FindProperty("modalCanvas").objectReferenceValue).SetActive(true);
            var scroll = (UnityEngine.UI.ScrollRect)fields.FindProperty("scroll").objectReferenceValue; var row = Object.Instantiate((SavedTripRow)fields.FindProperty("rowPrefab").objectReferenceValue, scroll.content);
            // The service normally fills these read-only-to-callers model properties.
            var item = new SavedTripItem();
            typeof(SavedTripItem).GetProperty("Title").SetValue(item, "<b>Literal place</b>"); typeof(SavedTripItem).GetProperty("Details").SetValue(item, "Original flight dates stay visible\n2027-06-10 08:00 JFK → LHR"); typeof(SavedTripItem).GetProperty("PriceCents").SetValue(item, 12345); typeof(SavedTripItem).GetProperty("PriceUnit").SetValue(item, "/ flight");
            row.Show(item, () => { }); Canvas.ForceUpdateCanvases();
            Assert.That(row.transform.Find("Top/Title").GetComponent<TMP_Text>().richText, Is.False); Assert.That(row.transform.Find("Price").GetComponent<TMP_Text>().text, Is.EqualTo("USD 123.45 / flight")); Assert.That(row.transform.Find("Details").GetComponent<TMP_Text>().text, Does.Contain("2027-06-10")); Object.DestroyImmediate(row.gameObject);
        }
        [UnityTest]
        public IEnumerator ActualTripControlsSaveAllKindsRecoverAndIsolateTwoAccounts()
        {
            EditorSceneManager.OpenScene(TripSetup.ScenePath); var configuration = new SerializedObject(Object.FindFirstObjectByType<LoginPage>()); configuration.FindProperty("developmentDatabaseFile").stringValue = "trip-editor-" + System.Guid.NewGuid().ToString("N") + ".db"; configuration.ApplyModifiedPropertiesWithoutUndo();
            yield return new EnterPlayMode();
            var account = Object.FindFirstObjectByType<LoginPage>(); string file = new SerializedObject(account).FindProperty("developmentDatabaseFile").stringValue; Assert.That(file, Does.StartWith("trip-editor-")); Assert.That(System.IO.Path.GetFileName(file), Is.EqualTo(file));
            var task = Object.FindFirstObjectByType<TripSmokeRunner>().RunChecksAsync(true); float deadline = Time.realtimeSinceStartup + 130; while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(task.IsCompleted, Is.True, "Saved-trip UI flow timed out."); if (task.IsFaulted) Assert.Fail(task.Exception.GetBaseException().ToString()); Assert.That(task.IsCanceled, Is.False);
            var pageFields = new SerializedObject(Object.FindFirstObjectByType<SavedTripsPage>()); var groups = pageFields.FindProperty("underlyingGroups"); for (int i = 0; i < groups.arraySize; i++) Assert.That(((CanvasGroup)groups.GetArrayElementAtIndex(i).objectReferenceValue).interactable, Is.True, "Modal should restore underlying interaction on logout.");
            string path = System.IO.Path.Combine(Application.persistentDataPath, file); Assert.That(System.IO.File.Exists(path), Is.True); System.IO.File.Delete(path); yield return new ExitPlayMode();
        }
    }
}
```

## tools/Test-Trips.ps1

```powershell
param([string]$BuildRoot = 'Builds/SavedTrips')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $BuildRoot).Path
$exe = Join-Path $root 'TravelPlannerTrips.exe'
$id = [Guid]::NewGuid().ToString('N')
$file = "auth-smoke-$id.db"
$logs = Join-Path (Get-Location).Path "Logs/SavedTrips/$id"
New-Item -ItemType Directory -Path $logs -Force | Out-Null
foreach ($phase in @('register-save','reopen-save')) {
    $log = Join-Path $logs "$phase.log"
    $arguments = @('-batchmode','-nographics','-tripSmoke','-authFile',$file,'-logFile',('"' + $log + '"'))
    if ($phase -eq 'reopen-save') { $arguments += '-tripReopen' }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(150000)) { $process.Kill(); throw "Saved trips $phase timed out. See $log" }
    $process.Refresh()
    $output = Get-Content -LiteralPath $log -Raw
    if ($process.ExitCode -ne 0 -or $output -notmatch 'SAVED_TRIPS_UI_SMOKE_PASS') { throw "Saved trips $phase failed. See $log" }
    Write-Output "PASS $phase - actual trip controls, all item kinds, persistence, isolation and cancelled writes. Log: $log"
    Select-String -LiteralPath $log -Pattern 'SAVED_TRIPS_LOAD_TIMING' | ForEach-Object { $_.Line }
}
```

## Assets/TravelPlanning/UI/Flights/FlightResultRow.cs

```csharp
using System;
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
        [SerializeField] private UnityEngine.UI.Button saveButton;
        private FlightOption shownFlight;
        private Action<FlightOption> saveRequested;
        private void Awake() { if (saveButton) saveButton.onClick.AddListener(Save); }
        public void Show(FlightOption flight, Action<FlightOption> onSave = null)
        {
            shownFlight = flight; saveRequested = onSave; if (saveButton) saveButton.interactable = onSave != null;
            airlineText.text = flight.AirlineName + " • " + flight.FlightNumber;
            routeText.text = flight.OriginAirportId + " " + flight.DepartureLocal.ToString("dd MMM HH:mm", CultureInfo.InvariantCulture)
                + " → " + flight.DestinationAirportId + " " + flight.ArrivalLocal.ToString("dd MMM HH:mm", CultureInfo.InvariantCulture)
                + "\nTimes local to each airport • " + flight.AvailableSeats + " sample seats";
            priceText.text = "USD " + (flight.PriceCents / 100m).ToString("N2", CultureInfo.InvariantCulture);
        }
        private void Save() => saveRequested?.Invoke(shownFlight);
        private void OnDestroy() { if (saveButton) saveButton.onClick.RemoveListener(Save); }
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
        public event Action<FlightOption> SaveRequested;
        public event Action ContentCleared;
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
            foreach (var flight in flights) { var row = Instantiate(rowPrefab, parent); row.gameObject.SetActive(true); row.Show(flight, RequestSave); rows.Add(row.gameObject); }
        }
        private void RequestSave(FlightOption flight) { if (IsOpen && !IsBusy && LastResult != null) SaveRequested?.Invoke(flight); }
        private void ClearRows()
        {
            ContentCleared?.Invoke();
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
            ContentCleared?.Invoke();
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
        [SerializeField] private UnityEngine.UI.Button saveButton;
        private PlaceOption shownPlace;
        private Action<PlaceOption> reviewRequested;
        private Action<PlaceOption> saveRequested;
        private void Awake() { if (viewReviewsButton) viewReviewsButton.onClick.AddListener(OpenReviews); if (saveButton) saveButton.onClick.AddListener(Save); }
        public void Show(PlaceOption place, Action<PlaceOption> onReviews = null, Action<PlaceOption> onSave = null)
        {
            shownPlace = place; reviewRequested = onReviews;
            saveRequested = onSave; if (saveButton) saveButton.interactable = onSave != null;
            if (viewReviewsButton) viewReviewsButton.interactable = onReviews != null;
            placeName.text = place.Name;
            price.text = FormatPrice(place.PriceCents, place.Category);
            rating.text = place.AverageRating.HasValue && place.ReviewCount > 0
                ? place.AverageRating.Value.ToString("0.0", CultureInfo.InvariantCulture) + " / 5 • " + place.ReviewCount + " demo reviews" : "No reviews yet";
            description.text = place.Description;
            address.text = place.Address;
        }
        private void OpenReviews() => reviewRequested?.Invoke(shownPlace);
        private void Save() => saveRequested?.Invoke(shownPlace);
        private void OnDestroy() { if (viewReviewsButton) viewReviewsButton.onClick.RemoveListener(OpenReviews); if (saveButton) saveButton.onClick.RemoveListener(Save); }
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
        public void Show(IReadOnlyList<PlaceOption> places, PlaceCard prefab, System.Action<PlaceOption> onReviews = null, System.Action<PlaceOption> onSave = null)
        {
            Clear(); emptyMessage.gameObject.SetActive(places.Count == 0);
            foreach (var place in places)
            {
                var card = Instantiate(prefab, items); card.Show(place, onReviews, onSave); cards.Add(card.gameObject);
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
        public event Action<PlaceOption> SaveRequested;
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
                hotels.Show(result.Hotels, cardPrefab, RequestReview, RequestSave); restaurants.Show(result.Restaurants, cardPrefab, RequestReview, RequestSave); experiences.Show(result.Experiences, cardPrefab, RequestReview, RequestSave); hotspots.Show(result.Hotspots, cardPrefab, RequestReview, RequestSave);
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
        private void RequestSave(PlaceOption place) { if (IsOpen && !IsBusy && LastResult != null) SaveRequested?.Invoke(place); }
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
                if ((ArgumentPresent("-authSmoke") || ArgumentPresent("-flightSmoke") || ArgumentPresent("-destinationSmoke") || ArgumentPresent("-reviewSmoke") || ArgumentPresent("-tripSmoke")) && (!Debug.isDebugBuild || string.IsNullOrEmpty(smokeFile) || !smokeFile.StartsWith("auth-smoke-", StringComparison.Ordinal)))
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
