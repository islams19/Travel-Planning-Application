# Milestone 7 — Full scripts

Complete source snapshot for price tracking and notifications, including six existing integration scripts. See the [validation record](Milestone-7-Validation.md) for 137 passing tests, the successful Windows development build, two passing player checks and remaining manual/release-build limitations.

## Assets/TravelPlanning/Runtime/Tracking/DemoPriceService.cs

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using TravelPlanning.Data;

namespace TravelPlanning.Tracking
{
    /// <summary>Changes the shared offline catalog and alerts all active watchers atomically.</summary>
    public sealed class DemoPriceService
    {
        private readonly TravelDatabase database;
        public DemoPriceService(TravelDatabase database)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public Task<bool> ChangePriceAsync(PriceTargetKind kind, string targetId, int newPriceCents, CancellationToken cancellationToken = default)
        {
            TrackingCatalog.Target(kind, out string table, out string column, out string unit);
            if (newPriceCents < 0) throw new ArgumentException("The price cannot be negative.", nameof(newPriceCents));
            return database.ExecuteAsync(connection =>
            {
                bool changed = false;
                connection.RunInTransaction(() =>
                {
                    var target = TrackingCatalog.ReadTarget(connection, kind, targetId);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (target.CurrentPriceCents == newPriceCents) return;
                    string historyId = Guid.NewGuid().ToString("N");
                    string now = TrackingCatalog.Timestamp();
                    connection.Execute("UPDATE " + table + " SET price_cents=? WHERE id=?", newPriceCents, targetId);
                    connection.Execute("INSERT INTO price_history (id," + column + ",old_price_cents,new_price_cents,changed_utc) VALUES (?,?,?,?,?)",
                        historyId, targetId, target.CurrentPriceCents, newPriceCents, now);
                    var watchers = connection.Query<TrackingWatchRow>("SELECT id AS Id,user_id AS UserId FROM price_watches WHERE " + column + "=? AND active=1", targetId);
                    string direction = newPriceCents < target.CurrentPriceCents ? "decreased" : "increased";
                    string title = "Price " + direction + ": " + target.Title;
                    string body = target.Title + " " + direction + " from " + TrackingCatalog.Money(target.CurrentPriceCents) + " to " + TrackingCatalog.Money(newPriceCents) + " " + unit + ". Offline demo price update.";
                    foreach (var watcher in watchers)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        connection.Execute("INSERT INTO notifications (id,user_id,watch_id,price_history_id,title,body,created_utc,is_read) VALUES (?,?,?,?,?,?,?,?)",
                            Guid.NewGuid().ToString("N"), watcher.UserId, watcher.Id, historyId, title, body, now, 0);
                        connection.Execute("UPDATE price_watches SET last_price_cents=? WHERE id=? AND user_id=? AND active=1", newPriceCents, watcher.Id, watcher.UserId);
                    }
                    cancellationToken.ThrowIfCancellationRequested(); // Failure/cancellation rolls price, history, notices and baselines back together.
                    changed = true;
                });
                return changed;
            }, cancellationToken);
        }
    }
}
```

## Assets/TravelPlanning/Runtime/Tracking/PriceTrackingService.cs

```csharp
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TravelPlanning.Data;

namespace TravelPlanning.Tracking
{
    /// <summary>Reads and updates only the signed-in account's watches and notifications.</summary>
    public sealed class PriceTrackingService
    {
        private readonly TravelDatabase database;
        private readonly string userId;
        public PriceTrackingService(TravelDatabase database, string userId)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("Sign in before tracking prices.", nameof(userId));
            this.userId = userId;
        }

        public Task<TrackingSnapshot> LoadAsync(CancellationToken cancellationToken = default)
        {
            var elapsed = Stopwatch.StartNew();
            return database.ExecuteAsync(connection =>
            {
                TrackingCatalog.RequireUser(connection, userId);
                var watches = TrackingCatalog.ReadWatches(connection, userId, PriceTargetKind.Flight);
                watches.AddRange(TrackingCatalog.ReadWatches(connection, userId, PriceTargetKind.Hotel));
                watches = watches.OrderByDescending(watch => watch.IsActive).ThenBy(watch => watch.Title, StringComparer.Ordinal).ThenBy(watch => watch.Id, StringComparer.Ordinal).ToList();
                var notices = connection.Query<PriceNotice>("SELECT n.id AS Id, n.watch_id AS WatchId, n.price_history_id AS HistoryId, n.title AS Title, " +
                    "n.body AS Body, n.created_utc AS CreatedUtc, n.is_read AS IsRead, h.old_price_cents AS OldPriceCents, h.new_price_cents AS NewPriceCents " +
                    "FROM notifications n JOIN price_history h ON h.id=n.price_history_id WHERE n.user_id=? ORDER BY n.created_utc DESC, n.id DESC", userId);
                cancellationToken.ThrowIfCancellationRequested();
                elapsed.Stop();
                return new TrackingSnapshot { Watches = watches.AsReadOnly(), Notifications = notices.AsReadOnly(), UnreadCount = notices.Count(notice => !notice.IsRead),
                    ElapsedMilliseconds = elapsed.ElapsedMilliseconds, WorkerThreadId = Thread.CurrentThread.ManagedThreadId };
            }, cancellationToken);
        }

        public Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default) => database.ExecuteAsync(connection =>
        {
            TrackingCatalog.RequireUser(connection, userId);
            int count = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM notifications WHERE user_id=? AND is_read=0", userId);
            cancellationToken.ThrowIfCancellationRequested();
            return count;
        }, cancellationToken);

        public Task<WatchOption> GetTargetAsync(PriceTargetKind kind, string targetId, CancellationToken cancellationToken = default)
        {
            TrackingCatalog.Target(kind, out _, out string column, out _);
            return database.ExecuteAsync(connection =>
            {
                TrackingCatalog.RequireUser(connection, userId);
                var target = TrackingCatalog.ReadTarget(connection, kind, targetId);
                var watch = connection.FindWithQuery<TrackingWatchRow>("SELECT id AS Id, last_price_cents AS LastPriceCents, active AS IsActive FROM price_watches WHERE user_id=? AND " + column + "=?", userId, targetId);
                if (watch != null) { target.Id = watch.Id; target.LastPriceCents = watch.LastPriceCents; target.IsActive = watch.IsActive; }
                cancellationToken.ThrowIfCancellationRequested();
                return target;
            }, cancellationToken);
        }

        public Task<bool> TrackAsync(PriceTargetKind kind, string targetId, CancellationToken cancellationToken = default)
        {
            TrackingCatalog.Target(kind, out _, out string column, out _);
            return database.ExecuteAsync(connection =>
            {
                bool changed = false;
                connection.RunInTransaction(() =>
                {
                    TrackingCatalog.RequireUser(connection, userId);
                    var target = TrackingCatalog.ReadTarget(connection, kind, targetId);
                    var watch = connection.FindWithQuery<TrackingWatchRow>("SELECT id AS Id, active AS IsActive FROM price_watches WHERE user_id=? AND " + column + "=?", userId, targetId);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (watch != null && watch.IsActive) return;
                    if (watch == null)
                        connection.Execute("INSERT INTO price_watches (id,user_id," + column + ",last_price_cents,active,created_utc) VALUES (?,?,?,?,?,?)",
                            Guid.NewGuid().ToString("N"), userId, targetId, target.CurrentPriceCents, 1, TrackingCatalog.Timestamp());
                    else
                        connection.Execute("UPDATE price_watches SET active=1,last_price_cents=? WHERE id=? AND user_id=?", target.CurrentPriceCents, watch.Id, userId);
                    cancellationToken.ThrowIfCancellationRequested();
                    changed = true;
                });
                return changed; // Do not throw cancellation after a committed write.
            }, cancellationToken);
        }

        public Task<bool> StopAsync(string watchId, CancellationToken cancellationToken = default) => UpdateOwnedAsync(
            "UPDATE price_watches SET active=0 WHERE id=? AND user_id=? AND active=1", watchId, cancellationToken);

        public Task<bool> MarkReadAsync(string notificationId, CancellationToken cancellationToken = default) => UpdateOwnedAsync(
            "UPDATE notifications SET is_read=1 WHERE id=? AND user_id=? AND is_read=0", notificationId, cancellationToken);

        public Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default) => database.ExecuteAsync(connection =>
        {
            int changed = 0;
            connection.RunInTransaction(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                changed = connection.Execute("UPDATE notifications SET is_read=1 WHERE user_id=? AND is_read=0", userId);
                cancellationToken.ThrowIfCancellationRequested();
            });
            return changed;
        }, cancellationToken);

        private Task<bool> UpdateOwnedAsync(string sql, string id, CancellationToken token) => database.ExecuteAsync(connection =>
        {
            int changed = 0;
            connection.RunInTransaction(() =>
            {
                token.ThrowIfCancellationRequested();
                changed = connection.Execute(sql, id, userId);
                token.ThrowIfCancellationRequested();
            });
            return changed > 0;
        }, token);
    }
}
```

## Assets/TravelPlanning/Runtime/Tracking/TrackingCatalog.cs

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using SQLite;

namespace TravelPlanning.Tracking
{
    /// <summary>Only these two fixed catalog tables can be tracked or changed by the demo.</summary>
    internal static class TrackingCatalog
    {
        internal static void Target(PriceTargetKind kind, out string table, out string column, out string unit)
        {
            switch (kind)
            {
                case PriceTargetKind.Flight: table = "flights"; column = "flight_id"; unit = "/ flight"; break;
                case PriceTargetKind.Hotel: table = "hotels"; column = "hotel_id"; unit = "/ room / night"; break;
                default: throw new ArgumentException("Choose a flight or hotel.", nameof(kind));
            }
        }

        internal static WatchOption ReadTarget(SQLiteConnection connection, PriceTargetKind kind, string targetId)
        {
            Target(kind, out _, out _, out string unit);
            if (string.IsNullOrWhiteSpace(targetId)) throw new ArgumentException("Choose an available flight or hotel.", nameof(targetId));
            var target = connection.FindWithQuery<WatchOption>("SELECT " + Columns(kind) + From(kind) + " WHERE p.id=?", targetId);
            if (target == null) throw new ArgumentException("Choose an available flight or hotel.", nameof(targetId));
            target.Kind = kind;
            target.PriceUnit = unit;
            target.LastPriceCents = target.CurrentPriceCents;
            return target;
        }

        internal static List<WatchOption> ReadWatches(SQLiteConnection connection, string owner, PriceTargetKind kind)
        {
            Target(kind, out _, out string column, out string unit);
            var watches = connection.Query<WatchOption>("SELECT " + Columns(kind) +
                ", w.id AS Id, w.last_price_cents AS LastPriceCents, w.active AS IsActive" + From(kind) +
                " JOIN price_watches w ON w." + column + "=p.id WHERE w.user_id=?", owner);
            foreach (var watch in watches) { watch.Kind = kind; watch.PriceUnit = unit; }
            return watches;
        }

        private static string Columns(PriceTargetKind kind) =>
            "p.id AS TargetId, p.price_cents AS CurrentPriceCents, p.currency AS Currency, " +
            (kind == PriceTargetKind.Flight ?
                "a.name || ' ' || p.flight_number || ' | ' || p.origin_airport_id || ' to ' || p.destination_airport_id || ' | ' || p.departure_local_date AS Title" :
                "p.name || ' | ' || d.name AS Title");

        private static string From(PriceTargetKind kind) => kind == PriceTargetKind.Flight ?
            " FROM flights p JOIN airlines a ON a.id=p.airline_id" : " FROM hotels p JOIN destinations d ON d.id=p.destination_id";

        internal static void RequireUser(SQLiteConnection connection, string userId)
        {
            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM users WHERE id=?", userId) != 1)
                throw new InvalidOperationException("Your account is not available. Sign in again.");
        }

        internal static string Timestamp() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
        internal static string Money(int cents) => "USD " + (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
    }

    public sealed class TrackingWatchRow
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public int LastPriceCents { get; set; }
        public bool IsActive { get; set; }
    }
}
```

## Assets/TravelPlanning/Runtime/Tracking/TrackingModels.cs

```csharp
using System.Collections.Generic;

namespace TravelPlanning.Tracking
{
    public enum PriceTargetKind { Flight, Hotel }

    public sealed class WatchOption
    {
        // Null for a target preview that has never been tracked by this account.
        public string Id { get; set; }
        public string TargetId { get; set; }
        public PriceTargetKind Kind { get; set; }
        public string Title { get; set; }
        public int CurrentPriceCents { get; set; }
        public int LastPriceCents { get; set; }
        public bool IsActive { get; set; }
        public string Currency { get; set; }
        public string PriceUnit { get; set; }
    }

    public sealed class PriceNotice
    {
        public string Id { get; set; }
        public string WatchId { get; set; }
        public string HistoryId { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string CreatedUtc { get; set; }
        public bool IsRead { get; set; }
        public int OldPriceCents { get; set; }
        public int NewPriceCents { get; set; }
    }

    public sealed class TrackingSnapshot
    {
        public IReadOnlyList<WatchOption> Watches { get; internal set; }
        public IReadOnlyList<PriceNotice> Notifications { get; internal set; }
        public int UnreadCount { get; internal set; }
        public long ElapsedMilliseconds { get; internal set; }
        public int WorkerThreadId { get; internal set; }
    }
}
```

## Assets/TravelPlanning/UI/Tracking/NotificationBadge.cs

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using TravelPlanning.Tracking;
using UnityEngine;

namespace TravelPlanning.UI.Tracking
{
    /// <summary>Refreshes unread badges on explicit events, never by polling.</summary>
    public sealed class NotificationBadge : MonoBehaviour
    {
        [SerializeField] private LoginPage account;
        [SerializeField] private TMP_Text[] labels;
        private CancellationTokenSource lifetime;
        private int revision;
        public int? UnreadCount { get; private set; }
        private void Start()
        {
            if (!account || labels == null || labels.Length != 3 || Array.Exists(labels, label => !label)) { Debug.LogError("NotificationBadge: assign the account and three badge labels."); enabled = false; return; }
            account.LoggedIn += OnLogin; account.LoggedOut += ClearOnLogout;
            if (string.IsNullOrEmpty(account.SignedInUserId)) ClearOnLogout(); else Refresh();
        }
        private void OnLogin(string _) => Refresh();
        public async void Refresh() => await RefreshAsync();
        public async Task RefreshAsync()
        {
            Cancel(); string owner = account.SignedInUserId; if (string.IsNullOrEmpty(owner)) { Display(0); return; }
            int current = revision; lifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken); Display(null);
            try
            {
                int count = await new PriceTrackingService(account.Database, owner).GetUnreadCountAsync(lifetime.Token);
                if (this && current == revision && owner == account.SignedInUserId) Display(count);
            }
            catch (OperationCanceledException) { }
            catch (Exception) { if (this && current == revision && owner == account.SignedInUserId) Display(null); }
        }
        private void Display(int? count) { UnreadCount = count; foreach (var label in labels) if (label) { label.text = count.HasValue ? count.Value > 99 ? "99+" : count.Value.ToString() : "?"; label.gameObject.SetActive(!count.HasValue || count.Value > 0); } }
        private void ClearOnLogout() { Cancel(); Display(0); }
        private void Cancel() { revision++; lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null; }
        private void OnDestroy() { Cancel(); if (account) { account.LoggedIn -= OnLogin; account.LoggedOut -= ClearOnLogout; } }
    }
}
```

## Assets/TravelPlanning/UI/Tracking/PriceNoticeRow.cs

```csharp
using System;
using System.Globalization;
using TMPro;
using TravelPlanning.Tracking;
using UnityEngine;

namespace TravelPlanning.UI.Tracking
{
    /// <summary>Displays one persisted price notice and its read state.</summary>
    public sealed class PriceNoticeRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text title, body, timestamp, actionLabel;
        [SerializeField] private UnityEngine.UI.Button readButton;
        private Action markRead;
        private bool alreadyRead;
        private void Awake() { if (readButton) readButton.onClick.AddListener(Read); }
        public void Show(PriceNotice notice, Action onRead)
        {
            title.text = notice.Title; body.text = notice.Body; timestamp.text = DateTime.TryParse(notice.CreatedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime date) ? date.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) : notice.CreatedUtc;
            alreadyRead = notice.IsRead; markRead = onRead; actionLabel.text = alreadyRead ? "Read" : "Mark read"; SetInteractable(true);
        }
        public void SetInteractable(bool value) { if (readButton) readButton.interactable = value && !alreadyRead; }
        private void Read() => markRead?.Invoke();
        private void OnDestroy() { if (readButton) readButton.onClick.RemoveListener(Read); }
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
            try { await Load(requestService, token, current); if (Current(current)) status.text = LastSnapshot.Notifications.Count == 0 ? "No notifications yet. Track a flight or hotel, then try a demo price change." : "These alerts describe changes to this installation's sample catalog."; }
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

## Assets/TravelPlanning/UI/Tracking/TrackingSmokeRunner.cs

```csharp
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using TravelPlanning.Tracking;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Flights;
using TravelPlanning.UI.Trips;
using UnityEngine;

namespace TravelPlanning.UI.Tracking
{
    /// <summary>Exercises price tracking using two real UI accounts and an isolated seeded database.</summary>
    public sealed class TrackingSmokeRunner : MonoBehaviour
    {
        [SerializeField] private LoginPage account;
        [SerializeField] private FlightSearchPage flights;
        [SerializeField] private DestinationHubPage hub;
        [SerializeField] private PriceTrackingPage tracking;
        [SerializeField] private NotificationBadge badge;
        [SerializeField] private GameObject authCanvas, flightCanvas, hubCanvas, trackingCanvas;
        private int frames;
        private void Update() => frames++;
        private async void Start()
        {
            var args = Environment.GetCommandLineArgs(); if (Array.IndexOf(args, "-trackingSmoke") < 0) return;
            try
            {
                int index = Array.IndexOf(args, "-authFile"); string file = index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
                if (!Debug.isDebugBuild || !file.StartsWith("auth-smoke-", StringComparison.Ordinal) || !file.EndsWith(".db", StringComparison.Ordinal) || System.IO.Path.GetFileName(file) != file || args.Any(x => x == "-authSmoke" || x == "-flightSmoke" || x == "-destinationSmoke" || x == "-reviewSmoke" || x == "-tripSmoke")) throw new InvalidOperationException("Tracking smoke checks require their isolated database and flag.");
                await RunChecksAsync(Array.IndexOf(args, "-trackingReopen") < 0); Debug.Log("PRICE_TRACKING_UI_SMOKE_PASS"); Application.Quit(0);
            }
            catch (Exception exception) { Debug.LogError("PRICE_TRACKING_UI_SMOKE_FAIL " + exception.GetType().Name + ": " + exception.Message); Application.Quit(1); }
        }
        public async Task RunChecksAsync(bool register)
        {
            await WaitUntil(() => account.IsReady, "Account startup"); var right = authCanvas.transform.Find("Right Panel"); var login = right.Find("LoginCard"); var registration = right.Find("RegistrationCard"); var home = right.Find("SignedInCard"); var main = trackingCanvas.transform.Find("Main");
            const string first = "tracking-first@example.test", second = "tracking-second@example.test", password = "Travel tracking demo phrase 2027!";
            if (register) await Register(login, registration, first, password); await Login(login, first, password);
            string flightId = null;
            if (!register)
            {
                await WaitUntil(() => badge.UnreadCount == 1, "Persisted first-user unread badge"); Click(home, "NotificationsButton"); await WaitTracking();
                Require(tracking.LastSnapshot.UnreadCount == 1 && tracking.LastSnapshot.Watches.Count(watch => watch.IsActive) == 2, "First user's watches or unread state did not persist."); flightId = tracking.LastSnapshot.Watches.First(watch => watch.Kind == PriceTargetKind.Flight).TargetId; await ReadAll(main); Click(main, "Header/CloseButton");
            }
            flightId = await OpenFlight(home, flightId); await EnsureTracking(main); Click(main, "Header/CloseButton"); account.Logout();
            if (register) await Register(login, registration, second, password); await Login(login, second, password); await WaitUntil(() => badge.UnreadCount == 0, "Second user's read state");
            await OpenFlight(home, flightId); await EnsureTracking(main); Require(tracking.LastSnapshot.UnreadCount == 0, "Another account's unread state leaked.");
            var price = main.Find("DemoPanel/Controls/PriceInput").GetComponent<TMP_InputField>(); price.text = "-1"; Require(!main.Find("DemoPanel/Controls/ApplyButton").GetComponent<UnityEngine.UI.Button>().interactable, "Negative demo price remained enabled.");
            int original = tracking.CurrentTarget.CurrentPriceCents; await Apply(main, original + 123); Require(tracking.LastSnapshot.UnreadCount == 1, "Price change did not notify the active watcher."); await WaitUntil(() => badge.UnreadCount == 1, "Second user's unread badge"); string sharedHistory = tracking.LastSnapshot.Notifications.First(notice => !notice.IsRead).HistoryId;
            var noticeRow = trackingCanvas.GetComponentsInChildren<PriceNoticeRow>().First(); Click(noticeRow.transform, "Top/ReadButton"); await WaitTracking(); Require(tracking.LastSnapshot.UnreadCount == 0, "Mark read failed."); Click(main, "Header/CloseButton"); account.Logout();
            await Login(login, first, password); await WaitUntil(() => badge.UnreadCount == 1, "First user's fan-out badge"); Click(home, "NotificationsButton"); await WaitTracking();
            Require(tracking.LastSnapshot.Notifications.Any(notice => notice.HistoryId == sharedHistory && !notice.IsRead), "Two watchers did not reference the same price event."); await SelectFlight(main, flightId);
            Click(main, "TargetActions/TrackButton"); await WaitTracking(); Require(!tracking.CurrentTarget.IsActive, "Stop tracking failed."); int noticesBefore = tracking.LastSnapshot.Notifications.Count;
            await Apply(main, tracking.CurrentTarget.CurrentPriceCents + 200); Require(tracking.LastSnapshot.Notifications.Count == noticesBefore, "A stopped watch received a new notification."); await Apply(main, tracking.CurrentTarget.CurrentPriceCents); Require(tracking.Status.Contains("unchanged") && tracking.LastSnapshot.Notifications.Count == noticesBefore, "A no-op price change created a notice.");
            Click(main, "TargetActions/TrackButton"); await WaitTracking(); Require(tracking.CurrentTarget.IsActive && tracking.CurrentTarget.LastPriceCents == tracking.CurrentTarget.CurrentPriceCents, "Retracking did not establish the current baseline."); Click(main, "Header/CloseButton");
            // Open the hotel screen from home, then prove old Save-to-trip navigation still coexists with tracking.
            Click(home, "SearchFlightsButton"); await WaitUntil(() => !flights.IsBusy && flights.LastResult != null, "Flight navigation"); Click(flightCanvas.transform, "Main/Header/ExploreDestinationButton"); await WaitUntil(() => !hub.IsBusy && hub.LastResult != null, "Hotel hub");
            foreach (string category in new[] { "Restaurants", "Experiences", "Hotspots" }) Require(!FirstPlace(category).transform.Find("ReviewActions/TrackPriceButton").gameObject.activeSelf, "Tracking appeared on an unsupported category.");
            Click(hubCanvas.transform, "Main/Header/SavedTripsButton"); var trips = UnityEngine.Object.FindFirstObjectByType<SavedTripsPage>(); await WaitUntil(() => !trips.IsBusy && trips.LastList != null, "Existing saved-trip navigation");
            var hotel = FirstPlace("Hotels"); Click(hotel.transform, "ReviewActions/TrackPriceButton"); await WaitTracking(); Require(!trips.IsOpen && tracking.CurrentTarget.Kind == PriceTargetKind.Hotel, "Tracking did not close the saved-trip modal."); await EnsureTracking(main); string hotelId = tracking.CurrentTarget.TargetId;
            await Apply(main, tracking.CurrentTarget.CurrentPriceCents + 10); Require(tracking.LastSnapshot.UnreadCount == 2, "Hotel change did not create a notification."); await ReadAll(main); await WaitUntil(() => badge.UnreadCount == 0, "Mark-all-read badge");
            await WithBlockedDatabase(async () => { Click(main, "TargetActions/TrackButton"); int before = frames; await Task.Delay(60); Require(tracking.IsBusy && frames > before, "UI froze during queued watch mutation."); Click(hubCanvas.transform, "Main/Header/LogoutButton"); Require(!tracking.IsOpen && tracking.LastSnapshot == null, "Logout left the tracking modal open."); });
            await Login(login, first, password); Click(home, "NotificationsButton"); await WaitTracking(); Require(tracking.LastSnapshot.Watches.Any(watch => watch.Kind == PriceTargetKind.Hotel && watch.TargetId == hotelId && watch.IsActive), "Queued stop was not cancelled on logout."); Click(main, "Header/CloseButton"); account.Logout();
            await Login(login, second, password); Click(home, "NotificationsButton"); await WaitTracking(); Require(tracking.LastSnapshot.UnreadCount == 1, "Other watcher missed the change while the first watcher was stopped."); await ReadAll(main); await SelectFlight(main, flightId); await Apply(main, tracking.CurrentTarget.CurrentPriceCents + 100); await ReadAll(main); Click(main, "Header/CloseButton"); account.Logout();
            await Login(login, first, password); await WaitUntil(() => badge.UnreadCount == 1, "Reactivated watcher final unread badge"); Click(home, "NotificationsButton"); await WaitTracking(); Require(tracking.LastSnapshot.UnreadCount == 1 && tracking.LastSnapshot.Watches.Count(watch => watch.IsActive) == 2, "Final persisted watch state is wrong."); Require(tracking.LastSnapshot.WorkerThreadId != Thread.CurrentThread.ManagedThreadId, "Tracking query ran on the UI thread."); Debug.Log("PRICE_TRACKING_LOAD_TIMING databaseMs=" + tracking.LastSnapshot.ElapsedMilliseconds + " worker=" + tracking.LastSnapshot.WorkerThreadId + " main=" + Thread.CurrentThread.ManagedThreadId); Click(main, "Header/CloseButton"); account.Logout();
        }
        private async Task<string> OpenFlight(Transform home, string targetId)
        {
            Click(home, "SearchFlightsButton"); await WaitUntil(() => !flights.IsBusy && flights.LastResult != null, "Flight search"); targetId = targetId ?? flights.LastResult.Outbound[0].Id; int index = flights.LastResult.Outbound.ToList().FindIndex(flight => flight.Id == targetId); Require(index >= 0, "Stable watched flight was not found.");
            var rows = flightCanvas.transform.Find("Main/Results/Outbound/ScrollView/Viewport/Content").GetComponentsInChildren<FlightResultRow>(); Click(rows[index].transform, "SaveActions/TrackPriceButton"); await WaitTracking(); Require(tracking.CurrentTarget.TargetId == targetId, "Wrong flight tracking target opened."); return targetId;
        }
        private async Task SelectFlight(Transform main, string targetId) { int index = tracking.LastSnapshot.Watches.ToList().FindIndex(watch => watch.Kind == PriceTargetKind.Flight && watch.TargetId == targetId); Require(index >= 0, "Flight watch was missing."); var dropdown = main.Find("WatchSelection/Dropdown").GetComponent<TMP_Dropdown>(); dropdown.value = index + 1; await WaitTracking(); Require(tracking.CurrentTarget.TargetId == targetId, "Wrong selected watch."); }
        private async Task EnsureTracking(Transform main) { if (!tracking.CurrentTarget.IsActive) { Click(main, "TargetActions/TrackButton"); await WaitTracking(); } Require(tracking.CurrentTarget.IsActive, "Tracking did not start."); }
        private async Task Apply(Transform main, int cents) { main.Find("DemoPanel/Controls/PriceInput").GetComponent<TMP_InputField>().text = (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture); Click(main, "DemoPanel/Controls/ApplyButton"); await WaitTracking(); }
        private async Task ReadAll(Transform main) { if (tracking.LastSnapshot.UnreadCount > 0) { Click(main, "TargetActions/ReadAllButton"); await WaitTracking(); } Require(tracking.LastSnapshot.UnreadCount == 0, "Mark all read failed."); }
        private PlaceCard FirstPlace(string category) => hubCanvas.transform.Find("Main/ScrollView/Viewport/Content/" + category + "/Items").GetComponentsInChildren<PlaceCard>().First();
        private async Task WithBlockedDatabase(Func<Task> action)
        {
            using (var release = new ManualResetEventSlim(false))
            {
                var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var blocker = account.Database.ExecuteAsync(connection => { entered.SetResult(true); if (!release.Wait(10000)) throw new TimeoutException("Tracking smoke gate timeout."); return true; }, CancellationToken.None); try { await entered.Task; await action(); } finally { release.Set(); } await blocker;
            }
        }
        private async Task Register(Transform login, Transform registration, string email, string password) { Click(login, "Create an account"); Input(registration, "EmailInput").text = email; Input(registration, "PasswordInput").text = Input(registration, "ConfirmationInput").text = password; Click(registration, "LoginButton"); await WaitUntil(() => !account.IsBusy, "Registration"); Require(login.gameObject.activeSelf, "Registration failed."); }
        private async Task Login(Transform login, string email, string password) { Input(login, "EmailInput").text = email; Input(login, "PasswordInput").text = password; Click(login, "LoginButton"); await WaitUntil(() => !account.IsBusy, "Sign in"); Require(account.SignedInEmail == email, "Sign in failed."); }
        private Task WaitTracking() => WaitUntil(() => !tracking.IsBusy && tracking.LastSnapshot != null, "Tracking panel");
        private static void Click(Transform root, string path) => root.Find(path).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        private static TMP_InputField Input(Transform root, string path) => root.Find(path).GetComponent<TMP_InputField>();
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static async Task WaitUntil(Func<bool> predicate, string operation) { var watch = System.Diagnostics.Stopwatch.StartNew(); while (!predicate()) { if (watch.ElapsedMilliseconds > 45000) throw new TimeoutException(operation); await Task.Delay(10); } }
    }
}
```

## Assets/TravelPlanning/UI/Editor/TrackingSetup.cs

```csharp
using System;
using System.IO;
using TMPro;
using TravelPlanning.UI.Destinations;
using TravelPlanning.UI.Flights;
using TravelPlanning.UI.Reviews;
using TravelPlanning.UI.Trips;
using TravelPlanning.UI.Tracking;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Adds local price watches and notifications to the existing feature screens.</summary>
    public static class TrackingSetup
    {
        public const string ScenePath = AuthSetup.ScenePath;
        public const string BuildPath = "Builds/PriceTracking/TravelPlannerTracking.exe";
        public const string RowPath = "Assets/TravelPlanning/Prefabs/Tracking/PriceNoticeRow.prefab";
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        [MenuItem("Travel Planning/Price Tracking/1 - Prepare Price Tracking")]
        public static void Prepare()
        {
            TripSetup.Prepare(); UpgradeCards();
            var account = UnityEngine.Object.FindFirstObjectByType<LoginPage>(); var flights = UnityEngine.Object.FindFirstObjectByType<FlightSearchPage>(); var hub = UnityEngine.Object.FindFirstObjectByType<DestinationHubPage>();
            var authCanvas = GameObject.Find("Canvas"); var flightCanvas = (GameObject)new SerializedObject(flights).FindProperty("flightCanvas").objectReferenceValue; var hubCanvas = (GameObject)new SerializedObject(hub).FindProperty("hubCanvas").objectReferenceValue;
            var existing = UnityEngine.Object.FindFirstObjectByType<PriceTrackingPage>(); if (existing) { AssignGroups(existing, authCanvas, flightCanvas, hubCanvas); EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); Debug.Log("PRICE_TRACKING_SCENE_READY " + ScenePath); return; }
            var homeRoot = authCanvas.transform.Find("Right Panel/SignedInCard"); var logout = homeRoot.Find("LogoutButton"); var home = UnityEngine.Object.Instantiate(logout.gameObject, homeRoot).GetComponent<UnityEngine.UI.Button>(); home.name = "NotificationsButton"; home.GetComponentInChildren<TMP_Text>().text = "Notifications"; home.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -540); logout.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -620);
            var flightEntry = Button(flightCanvas.transform.Find("Main/Header"), "NotificationsButton", "Notifications", 240); flightEntry.transform.SetSiblingIndex(3); var hubEntry = Button(hubCanvas.transform.Find("Main/Header"), "NotificationsButton", "Notifications", 240); hubEntry.transform.SetSiblingIndex(2);
            var badgeLabels = new[] { Badge(home), Badge(flightEntry), Badge(hubEntry) };
            var canvas = new GameObject("PriceTrackingCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster)); canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; canvas.GetComponent<Canvas>().sortingOrder = 6;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0;
            var blocker = Rect("InputBlocker", canvas.transform); Stretch(blocker); blocker.gameObject.AddComponent<UnityEngine.UI.Image>().color = Color.white;
            var main = Rect("Main", canvas.transform); Stretch(main); main.offsetMin = new Vector2(40, 32); main.offsetMax = new Vector2(-40, -32); Vertical(main, 12);
            var header = Horizontal("Header", main, 64); var title = Label(header, "Title", "Price tracking and notifications", 40); Layout(title.gameObject, -1, 1); var close = Button(header, "CloseButton", "Close", 180);
            var preview = Label(main, "TargetPreview", "", 25); Layout(preview.gameObject, 80);
            var selection = Horizontal("WatchSelection", main, 56); var selectionLabel = Label(selection, "Label", "Your watches", 24); Layout(selectionLabel.gameObject, -1, 0, 200);
            var dropdownObject = UnityEngine.Object.Instantiate(flightCanvas.transform.Find("Main/RouteFields/DepartureDate/Dropdown").gameObject, selection); dropdownObject.name = "Dropdown"; Layout(dropdownObject, 54, 1); var dropdown = dropdownObject.GetComponent<TMP_Dropdown>(); dropdown.ClearOptions(); foreach (var text in dropdownObject.GetComponentsInChildren<TMP_Text>(true)) text.richText = false;
            var refresh = Button(selection, "RefreshButton", "Refresh", 160);
            var actions = Horizontal("TargetActions", main, 48); var track = Button(actions, "TrackButton", "Track price", 250); var readAll = Button(actions, "ReadAllButton", "Mark all read", 250);
            var demo = Rect("DemoPanel", main); Vertical(demo, 8); Layout(demo.gameObject, 152); var demoTitle = Label(demo, "Title", "DEMO ONLY — change a shared sample price on this installation", 25); Layout(demoTitle.gameObject, 34);
            var demoControls = Horizontal("Controls", demo, 54); var priceObject = UnityEngine.Object.Instantiate(flightCanvas.transform.Find("Main/FilterFields/MaximumPrice/Input").gameObject, demoControls); priceObject.name = "PriceInput"; Layout(priceObject, 54, 1); var priceInput = priceObject.GetComponent<TMP_InputField>(); ((TMP_Text)priceInput.placeholder).text = "New sample price in USD"; foreach (var text in priceObject.GetComponentsInChildren<TMP_Text>(true)) text.richText = false; var apply = Button(demoControls, "ApplyButton", "Apply demo price", 300);
            var demoFeedback = Label(demo, "Feedback", "", 22); Layout(demoFeedback.gameObject, 48);
            var status = Label(main, "Status", "", 23); Layout(status.gameObject, 58); var unread = Label(main, "UnreadSummary", "", 25); Layout(unread.gameObject, 36);
            var rootScroll = Rect("ScrollView", main); Layout(rootScroll.gameObject, -1, 1); var scroll = rootScroll.gameObject.AddComponent<UnityEngine.UI.ScrollRect>(); scroll.horizontal = false; scroll.scrollSensitivity = 45; scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            var viewport = Rect("Viewport", rootScroll); Stretch(viewport); viewport.gameObject.AddComponent<UnityEngine.UI.Image>(); viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var content = Rect("Content", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero; Vertical(content, 16); content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize; scroll.content = content; scroll.viewport = viewport;
            var root = new GameObject("PriceTrackingController"); var controller = root.AddComponent<PriceTrackingPage>(); var badge = root.AddComponent<NotificationBadge>(); var badgeFields = new SerializedObject(badge); Set(badgeFields, "account", account); var labels = badgeFields.FindProperty("labels"); labels.arraySize = 3; for (int i = 0; i < 3; i++) labels.GetArrayElementAtIndex(i).objectReferenceValue = badgeLabels[i]; badgeFields.ApplyModifiedPropertiesWithoutUndo();
            var fields = new SerializedObject(controller); Set(fields, "account", account); Set(fields, "flights", flights); Set(fields, "hub", hub); Set(fields, "reviews", UnityEngine.Object.FindFirstObjectByType<ReviewsPage>()); Set(fields, "trips", UnityEngine.Object.FindFirstObjectByType<SavedTripsPage>()); Set(fields, "badge", badge); Set(fields, "modalCanvas", canvas); Set(fields, "demoPanel", demo.gameObject); Set(fields, "homeButton", home); Set(fields, "flightButton", flightEntry); Set(fields, "hubButton", hubEntry); Set(fields, "closeButton", close); Set(fields, "refreshButton", refresh); Set(fields, "trackButton", track); Set(fields, "readAllButton", readAll); Set(fields, "applyButton", apply); Set(fields, "watchChoice", dropdown); Set(fields, "demoPrice", priceInput); Set(fields, "targetPreview", preview); Set(fields, "status", status); Set(fields, "unreadSummary", unread); Set(fields, "trackLabel", track.GetComponentInChildren<TMP_Text>()); Set(fields, "demoFeedback", demoFeedback); Set(fields, "scroll", scroll); Set(fields, "rowPrefab", CreateRow()); fields.ApplyModifiedPropertiesWithoutUndo(); AssignGroups(controller, authCanvas, flightCanvas, hubCanvas);
            var runner = root.AddComponent<TrackingSmokeRunner>(); var runnerFields = new SerializedObject(runner); Set(runnerFields, "account", account); Set(runnerFields, "flights", flights); Set(runnerFields, "hub", hub); Set(runnerFields, "tracking", controller); Set(runnerFields, "badge", badge); Set(runnerFields, "authCanvas", authCanvas); Set(runnerFields, "flightCanvas", flightCanvas); Set(runnerFields, "hubCanvas", hubCanvas); Set(runnerFields, "trackingCanvas", canvas); runnerFields.ApplyModifiedPropertiesWithoutUndo();
            canvas.SetActive(false); EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); Debug.Log("PRICE_TRACKING_SCENE_READY " + ScenePath);
        }
        private static void AssignGroups(PriceTrackingPage controller, params GameObject[] sources)
        {
            var groups = new CanvasGroup[sources.Length]; for (int i = 0; i < sources.Length; i++) { var group = sources[i].GetComponent<CanvasGroup>(); if (!group) group = sources[i].AddComponent<CanvasGroup>(); groups[i] = group; }
            var fields = new SerializedObject(controller); var property = fields.FindProperty("underlyingGroups"); property.arraySize = groups.Length; for (int i = 0; i < groups.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = groups[i]; fields.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void UpgradeCards() { UpgradeCard(FlightSearchSetup.RowPath, true); UpgradeCard(DestinationHubSetup.PlaceCardPath, false); }
        private static void UpgradeCard(string path, bool flight)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { var fields = new SerializedObject(flight ? (UnityEngine.Object)root.GetComponent<FlightResultRow>() : root.GetComponent<PlaceCard>()); if (!fields.FindProperty("trackButton").objectReferenceValue) { var actions = root.transform.Find(flight ? "SaveActions" : "ReviewActions"); var button = Button(actions, "TrackPriceButton", "Track price", 230); Set(fields, "trackButton", button); fields.ApplyModifiedPropertiesWithoutUndo(); } PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        [MenuItem("Travel Planning/Price Tracking/2 - Build Windows x64")]
        public static void BuildWindows()
        {
            Prepare(); Directory.CreateDirectory(Path.GetDirectoryName(BuildPath)); var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = BuildPath, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development }); if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Price tracking build failed.");
            var notices = Path.Combine(Path.GetDirectoryName(BuildPath), "ThirdPartyNotices"); Directory.CreateDirectory(notices); foreach (string file in Directory.GetFiles("Assets/Plugins/SQLite/Licenses")) if (!file.EndsWith(".meta")) File.Copy(file, Path.Combine(notices, Path.GetFileName(file)), true); File.Copy("Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt", Path.Combine(notices, "LiberationSans-OFL.txt"), true); File.Copy("Assets/Plugins/LiteDB/LICENSE.txt", Path.Combine(notices, "LiteDB-LICENSE.txt"), true); Debug.Log("PRICE_TRACKING_BUILD_PASS " + Path.GetFullPath(BuildPath));
        }
        private static PriceNoticeRow CreateRow()
        {
            if (File.Exists(RowPath)) return AssetDatabase.LoadAssetAtPath<GameObject>(RowPath).GetComponent<PriceNoticeRow>();
            Directory.CreateDirectory(Path.GetDirectoryName(RowPath)); var root = Rect("PriceNoticeRow", null); root.sizeDelta = new Vector2(1800, 210); root.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color32(244, 247, 255, 255); var layout = Vertical(root, 8); layout.padding = new RectOffset(20, 20, 16, 16);
            var top = Horizontal("Top", root, 48); var title = Label(top, "Title", "", 26); Layout(title.gameObject, -1, 1); var read = Button(top, "ReadButton", "Mark read", 230); var body = Label(root, "Body", "", 24); body.textWrappingMode = TextWrappingModes.Normal; var timestamp = Label(root, "Timestamp", "", 21); Layout(timestamp.gameObject, 30);
            var row = root.gameObject.AddComponent<PriceNoticeRow>(); var fields = new SerializedObject(row); Set(fields, "title", title); Set(fields, "body", body); Set(fields, "timestamp", timestamp); Set(fields, "readButton", read); Set(fields, "actionLabel", read.GetComponentInChildren<TMP_Text>()); fields.ApplyModifiedPropertiesWithoutUndo(); var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, RowPath); UnityEngine.Object.DestroyImmediate(root.gameObject); return prefab.GetComponent<PriceNoticeRow>();
        }
        private static TMP_Text Badge(UnityEngine.UI.Button button)
        {
            var caption = button.GetComponentInChildren<TMP_Text>(); if (caption) { var offset = caption.rectTransform.offsetMax; offset.x = -44; caption.rectTransform.offsetMax = offset; }
            var badge = Label(button.transform, "UnreadBadge", "", 20); badge.color = Color.white; badge.alignment = TextAlignmentOptions.Center; var rect = badge.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(1, .5f); rect.sizeDelta = new Vector2(38, 32); rect.anchoredPosition = new Vector2(-25, 0); badge.gameObject.SetActive(false); return badge;
        }
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

## Assets/TravelPlanning/Tests/EditMode/PriceTrackingTests.cs

```csharp
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SQLite;
using TravelPlanning.Data;
using TravelPlanning.Tracking;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class PriceTrackingTests
    {
        private const string Flight = "JFK-LHR-20270615-AA";
        private const string Hotel = "london-hotel-1";
        private string folder;
        private string path;
        private TravelDatabase database;
        private PriceTrackingService owner;
        private PriceTrackingService other;
        private DemoPriceService prices;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "TravelTrackingTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, "travel.db");
            database = OpenDatabase();
            owner = new PriceTrackingService(database, "owner");
            other = new PriceTrackingService(database, "other");
            prices = new DemoPriceService(database);
        }

        [TearDown]
        public void TearDown() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }

        [UnityTest]
        public IEnumerator BothKindsHaveFreshPreviewAndConcurrentTrackingCreatesOneWatch()
        {
            yield return Ready();
            var preview = owner.GetTargetAsync(PriceTargetKind.Flight, Flight); yield return Wait(preview);
            Assert.That(preview.Result.Id, Is.Null);
            Assert.That(preview.Result.IsActive, Is.False);
            Assert.That(preview.Result.CurrentPriceCents, Is.EqualTo(56200));
            Assert.That(preview.Result.LastPriceCents, Is.EqualTo(56200));
            Assert.That(preview.Result.Title, Does.Contain("AA101"));
            Assert.That(preview.Result.Title, Does.Contain("JFK to LHR"));
            Assert.That(preview.Result.Title, Does.Contain("2027-06-15"));
            var rival = new PriceTrackingService(database, "owner");
            var race = Task.WhenAll(owner.TrackAsync(PriceTargetKind.Flight, Flight), rival.TrackAsync(PriceTargetKind.Flight, Flight));
            yield return Wait(race);
            Assert.That(race.Result.Count(created => created), Is.EqualTo(1));
            yield return Wait(owner.TrackAsync(PriceTargetKind.Hotel, Hotel));
            int main = Thread.CurrentThread.ManagedThreadId;
            var snapshot = owner.LoadAsync(); yield return Wait(snapshot);
            Assert.That(snapshot.Result.Watches.Count, Is.EqualTo(2));
            Assert.That(snapshot.Result.Watches.All(watch => watch.IsActive && watch.Currency == "USD"), Is.True);
            Assert.That(snapshot.Result.Watches.Single(watch => watch.Kind == PriceTargetKind.Flight).PriceUnit, Is.EqualTo("/ flight"));
            Assert.That(snapshot.Result.Watches.Single(watch => watch.Kind == PriceTargetKind.Hotel).PriceUnit, Is.EqualTo("/ room / night"));
            Assert.That(snapshot.Result.Watches.Single(watch => watch.Kind == PriceTargetKind.Hotel).Title, Does.Contain("London"));
            Assert.That(snapshot.Result.WorkerThreadId, Is.Not.EqualTo(main));
            Assert.That(snapshot.Result.Notifications, Is.Empty);
            byte[] before = Hash();
            var duplicate = owner.TrackAsync(PriceTargetKind.Hotel, Hotel); yield return Wait(duplicate);
            Assert.That(duplicate.Result, Is.False);
            Assert.That(Hash(), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator IncreasesDecreasesAndUnwatchedChangesRecordHistoryButEqualPriceDoesNothing()
        {
            yield return Ready();
            yield return Wait(owner.TrackAsync(PriceTargetKind.Flight, Flight));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Flight, Flight, 60000));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Flight, Flight, 50000));
            byte[] before = Hash();
            var unchanged = prices.ChangePriceAsync(PriceTargetKind.Flight, Flight, 50000); yield return Wait(unchanged);
            Assert.That(unchanged.Result, Is.False);
            Assert.That(Hash(), Is.EqualTo(before));
            var snapshot = owner.LoadAsync(); yield return Wait(snapshot);
            Assert.That(snapshot.Result.UnreadCount, Is.EqualTo(2));
            Assert.That(snapshot.Result.Notifications[0].Title, Does.Contain("decreased"));
            Assert.That(snapshot.Result.Notifications[0].Body, Does.Contain("USD 600.00 to USD 500.00 / flight"));
            Assert.That(snapshot.Result.Notifications[0].OldPriceCents, Is.EqualTo(60000));
            Assert.That(snapshot.Result.Notifications[0].NewPriceCents, Is.EqualTo(50000));
            Assert.That(snapshot.Result.Notifications[1].Title, Does.Contain("increased"));
            Assert.That(snapshot.Result.Notifications.All(notice => notice.Body.Contains("2027-06-15")), Is.True);
            Assert.That(snapshot.Result.Watches.Single().LastPriceCents, Is.EqualTo(50000));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, 15000));
            var historyCount = database.ExecuteAsync(connection => connection.ExecuteScalar<int>("SELECT COUNT(*) FROM price_history"), CancellationToken.None);
            yield return Wait(historyCount);
            Assert.That(historyCount.Result, Is.EqualTo(3));
            var unwatched = owner.GetTargetAsync(PriceTargetKind.Hotel, Hotel); yield return Wait(unwatched);
            Assert.That(unwatched.Result.Id, Is.Null);
            Assert.That(unwatched.Result.CurrentPriceCents, Is.EqualTo(15000));
            var count = owner.GetUnreadCountAsync(); yield return Wait(count);
            Assert.That(count.Result, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator OneChangeFansOutToBothOwnersAndReadOperationsRemainPrivate()
        {
            yield return Ready();
            yield return Wait(owner.TrackAsync(PriceTargetKind.Hotel, Hotel));
            yield return Wait(other.TrackAsync(PriceTargetKind.Hotel, Hotel));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, 17000));
            var mine = owner.LoadAsync(); var theirs = other.LoadAsync(); yield return Wait(mine); yield return Wait(theirs);
            var mineNotice = mine.Result.Notifications.Single(); var otherNotice = theirs.Result.Notifications.Single();
            Assert.That(mineNotice.Id, Is.Not.EqualTo(otherNotice.Id));
            Assert.That(mineNotice.HistoryId, Is.EqualTo(otherNotice.HistoryId));
            Assert.That(mine.Result.Watches.Single().LastPriceCents, Is.EqualTo(17000));
            Assert.That(theirs.Result.Watches.Single().LastPriceCents, Is.EqualTo(17000));
            var foreignRead = owner.MarkReadAsync(otherNotice.Id); var missingRead = owner.MarkReadAsync("missing");
            var foreignStop = owner.StopAsync(theirs.Result.Watches.Single().Id); var missingStop = owner.StopAsync("missing");
            yield return Wait(foreignRead); yield return Wait(missingRead); yield return Wait(foreignStop); yield return Wait(missingStop);
            Assert.That(foreignRead.Result, Is.False); Assert.That(missingRead.Result, Is.False);
            Assert.That(foreignStop.Result, Is.False); Assert.That(missingStop.Result, Is.False);
            var read = owner.MarkReadAsync(mineNotice.Id); yield return Wait(read); Assert.That(read.Result, Is.True);
            var again = owner.MarkReadAsync(mineNotice.Id); yield return Wait(again); Assert.That(again.Result, Is.False);
            var otherUnread = other.GetUnreadCountAsync(); yield return Wait(otherUnread); Assert.That(otherUnread.Result, Is.EqualTo(1));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, 18000));
            var markAll = owner.MarkAllReadAsync(); yield return Wait(markAll); Assert.That(markAll.Result, Is.EqualTo(1));
            var mineCount = owner.GetUnreadCountAsync(); otherUnread = other.GetUnreadCountAsync();
            yield return Wait(mineCount); yield return Wait(otherUnread);
            Assert.That(mineCount.Result, Is.Zero); Assert.That(otherUnread.Result, Is.EqualTo(2));
            var historyCount = database.ExecuteAsync(connection => connection.ExecuteScalar<int>("SELECT COUNT(*) FROM price_history WHERE hotel_id=?", Hotel), CancellationToken.None);
            yield return Wait(historyCount); Assert.That(historyCount.Result, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator StoppingKeepsNoticesAndRetrackingResetsBaselineWithoutHistoricalAlert()
        {
            yield return Ready();
            yield return Wait(owner.TrackAsync(PriceTargetKind.Hotel, Hotel));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, 17000));
            var first = owner.LoadAsync(); yield return Wait(first);
            string watchId = first.Result.Watches.Single().Id;
            var stop = owner.StopAsync(watchId); yield return Wait(stop); Assert.That(stop.Result, Is.True);
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, 18000));
            var stopped = owner.LoadAsync(); yield return Wait(stopped);
            Assert.That(stopped.Result.Watches.Single().IsActive, Is.False);
            Assert.That(stopped.Result.Watches.Single().LastPriceCents, Is.EqualTo(17000));
            Assert.That(stopped.Result.Watches.Single().CurrentPriceCents, Is.EqualTo(18000));
            Assert.That(stopped.Result.Notifications.Single().Id, Is.EqualTo(first.Result.Notifications.Single().Id));
            var resume = owner.TrackAsync(PriceTargetKind.Hotel, Hotel); yield return Wait(resume); Assert.That(resume.Result, Is.True);
            var active = owner.GetTargetAsync(PriceTargetKind.Hotel, Hotel); yield return Wait(active);
            Assert.That(active.Result.Id, Is.EqualTo(watchId)); Assert.That(active.Result.LastPriceCents, Is.EqualTo(18000));
            var unread = owner.GetUnreadCountAsync(); yield return Wait(unread); Assert.That(unread.Result, Is.EqualTo(1));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, 19000));
            var resumed = owner.LoadAsync(); yield return Wait(resumed);
            Assert.That(resumed.Result.Notifications.Count, Is.EqualTo(2));
            Assert.That(resumed.Result.Notifications[0].OldPriceCents, Is.EqualTo(18000));
        }

        [UnityTest]
        public IEnumerator FailureDuringNotificationInsertRollsBackPriceHistoryNoticesAndBaselines()
        {
            yield return Ready();
            yield return Wait(owner.TrackAsync(PriceTargetKind.Hotel, Hotel));
            yield return Wait(other.TrackAsync(PriceTargetKind.Hotel, Hotel));
            // Test-only trigger makes notification creation fail after the price/history writes.
            yield return Wait(database.ExecuteAsync(connection => connection.Execute("CREATE TRIGGER test_fail_notice BEFORE INSERT ON notifications WHEN NEW.user_id='other' BEGIN SELECT RAISE(ABORT,'Injected test failure'); END"), CancellationToken.None));
            byte[] before = Hash();
            var failed = prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, 22000); yield return Completion(failed);
            Assert.That(failed.IsFaulted, Is.True); Assert.That(failed.Exception.GetBaseException(), Is.InstanceOf<SQLiteException>());
            Assert.That(Hash(), Is.EqualTo(before));
            var snapshot = owner.LoadAsync(); yield return Wait(snapshot);
            Assert.That(snapshot.Result.Watches.Single().CurrentPriceCents, Is.EqualTo(16000));
            Assert.That(snapshot.Result.Watches.Single().LastPriceCents, Is.EqualTo(16000));
            Assert.That(snapshot.Result.Notifications, Is.Empty);
            var history = database.ExecuteAsync(connection => connection.ExecuteScalar<int>("SELECT COUNT(*) FROM price_history"), CancellationToken.None);
            yield return Wait(history); Assert.That(history.Result, Is.Zero);
        }

        [UnityTest]
        public IEnumerator QueuedCancellationLeavesEveryDatabaseByteUnchanged()
        {
            yield return Ready();
            yield return Wait(owner.TrackAsync(PriceTargetKind.Hotel, Hotel));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, 17000));
            var current = owner.LoadAsync(); yield return Wait(current);
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
                        owner.TrackAsync(PriceTargetKind.Flight, Flight, cancellation.Token),
                        owner.StopAsync(current.Result.Watches.Single().Id, cancellation.Token),
                        owner.MarkReadAsync(current.Result.Notifications.Single().Id, cancellation.Token),
                        owner.MarkAllReadAsync(cancellation.Token),
                        prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, 18000, cancellation.Token)
                    };
                    cancellation.Cancel();
                }
                finally { release.Set(); }
                yield return Wait(blocker);
                foreach (var task in pending) { yield return Completion(task); Assert.That(task.IsCanceled, Is.True); }
            }
            Assert.That(Hash(), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator InvalidIdsKindsAndNegativePricesFailWhileZeroAndIntMaximumWork()
        {
            yield return Ready();
            Assert.Throws<ArgumentException>(() => owner.TrackAsync((PriceTargetKind)99, Hotel));
            Assert.Throws<ArgumentException>(() => owner.GetTargetAsync((PriceTargetKind)99, Hotel));
            Assert.Throws<ArgumentException>(() => prices.ChangePriceAsync((PriceTargetKind)99, Hotel, 100));
            Assert.Throws<ArgumentException>(() => prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, -1));
            foreach (string id in new[] { "missing", "london-restaurant-1", "london-hotel-1' OR 1=1 --", "'; DELETE FROM users; --" })
            {
                yield return ArgumentFailure(owner.TrackAsync(PriceTargetKind.Hotel, id));
                yield return ArgumentFailure(owner.GetTargetAsync(PriceTargetKind.Hotel, id));
                yield return ArgumentFailure(prices.ChangePriceAsync(PriceTargetKind.Hotel, id, 100));
            }
            var unknownAccount = new PriceTrackingService(database, "owner' OR 1=1 --");
            var invalidOwner = unknownAccount.TrackAsync(PriceTargetKind.Hotel, Hotel); yield return Completion(invalidOwner);
            Assert.That(invalidOwner.Exception.GetBaseException(), Is.InstanceOf<InvalidOperationException>());
            yield return Wait(owner.TrackAsync(PriceTargetKind.Hotel, Hotel));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, 0));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Hotel, Hotel, int.MaxValue));
            var snapshot = owner.LoadAsync(); yield return Wait(snapshot);
            Assert.That(snapshot.Result.Watches.Single().CurrentPriceCents, Is.EqualTo(int.MaxValue));
            Assert.That(snapshot.Result.Notifications[0].Body, Does.Contain("USD 0.00 to USD 21474836.47"));
            var injectedRead = owner.MarkReadAsync("' OR 1=1 --"); var injectedStop = owner.StopAsync("' OR 1=1 --");
            yield return Wait(injectedRead); yield return Wait(injectedStop);
            Assert.That(injectedRead.Result, Is.False); Assert.That(injectedStop.Result, Is.False);
        }

        [UnityTest]
        public IEnumerator ReopenPreservesReadStateAndReadOnlyQueriesPreserveDatabaseBytes()
        {
            yield return Ready();
            yield return Wait(owner.TrackAsync(PriceTargetKind.Flight, Flight));
            yield return Wait(prices.ChangePriceAsync(PriceTargetKind.Flight, Flight, 56000));
            var initial = owner.LoadAsync(); yield return Wait(initial);
            yield return Wait(owner.MarkReadAsync(initial.Result.Notifications.Single().Id));
            var reopened = OpenDatabase(); yield return Wait(reopened.InitializeAsync(CancellationToken.None));
            var service = new PriceTrackingService(reopened, "owner");
            byte[] before = Hash();
            var snapshot = service.LoadAsync(); var count = service.GetUnreadCountAsync(); var preview = service.GetTargetAsync(PriceTargetKind.Flight, Flight);
            yield return Wait(snapshot); yield return Wait(count); yield return Wait(preview);
            Assert.That(snapshot.Result.Notifications.Single().IsRead, Is.True);
            Assert.That(snapshot.Result.Watches.Single().Id, Is.EqualTo(initial.Result.Watches.Single().Id));
            Assert.That(count.Result, Is.Zero); Assert.That(preview.Result.CurrentPriceCents, Is.EqualTo(56000));
            Assert.That(Hash(), Is.EqualTo(before));
        }

        private IEnumerator Ready()
        {
            yield return Wait(database.InitializeAsync(CancellationToken.None));
            yield return Wait(database.ExecuteAsync(connection =>
            {
                foreach (string id in new[] { "owner", "other" }) connection.Execute("INSERT INTO users (id,email_normalized,password_hash,password_salt,password_iterations,created_utc) VALUES (?,?,?,?,?,?)",
                    id, id + "@example.com", "testfixture", "testfixture", 600000, "2026-09-29T00:00:00Z");
                return 0;
            }, CancellationToken.None));
        }
        private TravelDatabase OpenDatabase() => new TravelDatabase(Path.Combine(Application.streamingAssetsPath, "Database", "travel_seed.db"), path);
        private byte[] Hash() { using (var hash = SHA256.Create()) return hash.ComputeHash(File.ReadAllBytes(path)); }
        private static IEnumerator Wait(Task task) { yield return Completion(task); task.GetAwaiter().GetResult(); }
        private static IEnumerator ArgumentFailure(Task task) { yield return Completion(task); Assert.That(task.IsFaulted, Is.True); Assert.That(task.Exception.GetBaseException(), Is.InstanceOf<ArgumentException>()); }
        private static IEnumerator Completion(Task task)
        {
            double deadline = UnityEditor.EditorApplication.timeSinceStartup + 30;
            while (!task.IsCompleted)
            {
                Assert.That(UnityEditor.EditorApplication.timeSinceStartup, Is.LessThan(deadline), "Tracking operation timed out.");
                yield return null;
            }
        }
    }
}
```

## Assets/TravelPlanning/Tests/EditMode/TrackingSceneTests.cs

```csharp
using System.Collections;
using NUnit.Framework;
using TMPro;
using TravelPlanning.Tracking;
using TravelPlanning.UI;
using TravelPlanning.UI.Editor;
using TravelPlanning.UI.Tracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class TrackingSceneTests
    {
        [Test]
        public void TrackingReferencesBadgesAndTrackButtonsAreComplete()
        {
            EditorSceneManager.OpenScene(TrackingSetup.ScenePath); var page = Object.FindFirstObjectByType<PriceTrackingPage>(); Assert.That(page.transform.parent, Is.Null); var fields = new SerializedObject(page);
            foreach (string name in new[] { "account", "flights", "hub", "reviews", "trips", "badge", "modalCanvas", "demoPanel", "homeButton", "flightButton", "hubButton", "closeButton", "refreshButton", "trackButton", "readAllButton", "applyButton", "watchChoice", "demoPrice", "targetPreview", "status", "unreadSummary", "trackLabel", "demoFeedback", "scroll", "rowPrefab" }) Assert.That(fields.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
            var groups = fields.FindProperty("underlyingGroups"); Assert.That(groups.arraySize, Is.EqualTo(3)); for (int i = 0; i < groups.arraySize; i++) Assert.That(groups.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null);
            var badge = new SerializedObject(fields.FindProperty("badge").objectReferenceValue); var labels = badge.FindProperty("labels"); Assert.That(labels.arraySize, Is.EqualTo(3)); for (int i = 0; i < labels.arraySize; i++) Assert.That(labels.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null);
            var canvas = (GameObject)fields.FindProperty("modalCanvas").objectReferenceValue; Assert.That(canvas.activeSelf, Is.False); Assert.That(canvas.GetComponent<Canvas>().sortingOrder, Is.EqualTo(6)); canvas.SetActive(true); Canvas.ForceUpdateCanvases(); var scroll = (UnityEngine.UI.ScrollRect)fields.FindProperty("scroll").objectReferenceValue; Assert.That(scroll.viewport.rect.width, Is.GreaterThan(200)); Assert.That(scroll.viewport.rect.height, Is.GreaterThan(100));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(FlightSearchSetup.RowPath).transform.Find("SaveActions/TrackPriceButton"), Is.Not.Null); Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(DestinationHubSetup.PlaceCardPath).transform.Find("ReviewActions/TrackPriceButton"), Is.Not.Null);
            Assert.That(GameObject.Find("Canvas/Right Panel/LoginCard/EmailInput").GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(0, -230)));
        }
        [Test]
        public void NoticeRowDisplaysLiteralTextAndDisablesAlreadyReadAction()
        {
            EditorSceneManager.OpenScene(TrackingSetup.ScenePath); var fields = new SerializedObject(Object.FindFirstObjectByType<PriceTrackingPage>()); ((GameObject)fields.FindProperty("modalCanvas").objectReferenceValue).SetActive(true); var scroll = (UnityEngine.UI.ScrollRect)fields.FindProperty("scroll").objectReferenceValue; var row = Object.Instantiate((PriceNoticeRow)fields.FindProperty("rowPrefab").objectReferenceValue, scroll.content);
            var notice = new PriceNotice(); typeof(PriceNotice).GetProperty("Title").SetValue(notice, "<b>Literal flight title</b>"); typeof(PriceNotice).GetProperty("Body").SetValue(notice, "USD 527.00 changed to USD 520.00"); typeof(PriceNotice).GetProperty("CreatedUtc").SetValue(notice, "2027-06-01T10:00:00Z"); typeof(PriceNotice).GetProperty("IsRead").SetValue(notice, true);
            row.Show(notice, () => { }); Assert.That(row.transform.Find("Top/Title").GetComponent<TMP_Text>().richText, Is.False); Assert.That(row.transform.Find("Body").GetComponent<TMP_Text>().text, Does.Contain("527.00")); Assert.That(row.transform.Find("Top/ReadButton").GetComponent<UnityEngine.UI.Button>().interactable, Is.False); Object.DestroyImmediate(row.gameObject);
        }
        [UnityTest]
        public IEnumerator ActualTrackingControlsFanOutNotifyPersistAndIsolateAccounts()
        {
            EditorSceneManager.OpenScene(TrackingSetup.ScenePath); var configuration = new SerializedObject(Object.FindFirstObjectByType<LoginPage>()); configuration.FindProperty("developmentDatabaseFile").stringValue = "tracking-editor-" + System.Guid.NewGuid().ToString("N") + ".db"; configuration.ApplyModifiedPropertiesWithoutUndo(); yield return new EnterPlayMode();
            var account = Object.FindFirstObjectByType<LoginPage>(); string file = new SerializedObject(account).FindProperty("developmentDatabaseFile").stringValue; Assert.That(file, Does.StartWith("tracking-editor-")); Assert.That(System.IO.Path.GetFileName(file), Is.EqualTo(file));
            var task = Object.FindFirstObjectByType<TrackingSmokeRunner>().RunChecksAsync(true); float deadline = Time.realtimeSinceStartup + 170; while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(task.IsCompleted, Is.True, "Tracking UI flow timed out."); if (task.IsFaulted) Assert.Fail(task.Exception.GetBaseException().ToString()); Assert.That(task.IsCanceled, Is.False); Assert.That(Object.FindFirstObjectByType<NotificationBadge>().UnreadCount, Is.EqualTo(0), "Logout must clear unread badges.");
            var pageFields = new SerializedObject(Object.FindFirstObjectByType<PriceTrackingPage>()); var groups = pageFields.FindProperty("underlyingGroups"); for (int i = 0; i < groups.arraySize; i++) Assert.That(((CanvasGroup)groups.GetArrayElementAtIndex(i).objectReferenceValue).interactable, Is.True);
            string path = System.IO.Path.Combine(Application.persistentDataPath, file); Assert.That(System.IO.File.Exists(path), Is.True); System.IO.File.Delete(path); yield return new ExitPlayMode();
        }
    }
}
```

## tools/Test-Tracking.ps1

```powershell
param([string]$BuildRoot = 'Builds/PriceTracking')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $BuildRoot).Path
$exe = Join-Path $root 'TravelPlannerTracking.exe'
$id = [Guid]::NewGuid().ToString('N')
$file = "auth-smoke-$id.db"
$logs = Join-Path (Get-Location).Path "Logs/PriceTracking/$id"
New-Item -ItemType Directory -Path $logs -Force | Out-Null
foreach ($phase in @('register-track','reopen-track')) {
    $log = Join-Path $logs "$phase.log"
    $arguments = @('-batchmode','-nographics','-trackingSmoke','-authFile',$file,'-logFile',('"' + $log + '"'))
    if ($phase -eq 'reopen-track') { $arguments += '-trackingReopen' }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(180000)) { $process.Kill(); throw "Price tracking $phase timed out. See $log" }
    $process.Refresh()
    $output = Get-Content -LiteralPath $log -Raw
    if ($process.ExitCode -ne 0 -or $output -notmatch 'PRICE_TRACKING_UI_SMOKE_PASS') { throw "Price tracking $phase failed. See $log" }
    Write-Output "PASS $phase - two-account tracking, shared price events, badges, read state, demo controls and cancellation. Log: $log"
    Select-String -LiteralPath $log -Pattern 'PRICE_TRACKING_LOAD_TIMING' | ForEach-Object { $_.Line }
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
                if ((ArgumentPresent("-authSmoke") || ArgumentPresent("-flightSmoke") || ArgumentPresent("-destinationSmoke") || ArgumentPresent("-reviewSmoke") || ArgumentPresent("-tripSmoke") || ArgumentPresent("-trackingSmoke")) && (!Debug.isDebugBuild || string.IsNullOrEmpty(smokeFile) || !smokeFile.StartsWith("auth-smoke-", StringComparison.Ordinal)))
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
        [SerializeField] private UnityEngine.UI.Button trackButton;
        private FlightOption shownFlight;
        private Action<FlightOption> saveRequested;
        private Action<FlightOption> trackRequested;
        private void Awake() { if (saveButton) saveButton.onClick.AddListener(Save); if (trackButton) trackButton.onClick.AddListener(Track); }
        public void Show(FlightOption flight, Action<FlightOption> onSave = null, Action<FlightOption> onTrack = null)
        {
            shownFlight = flight; saveRequested = onSave; if (saveButton) saveButton.interactable = onSave != null;
            trackRequested = onTrack; if (trackButton) trackButton.interactable = onTrack != null;
            airlineText.text = flight.AirlineName + " • " + flight.FlightNumber;
            routeText.text = flight.OriginAirportId + " " + flight.DepartureLocal.ToString("dd MMM HH:mm", CultureInfo.InvariantCulture)
                + " → " + flight.DestinationAirportId + " " + flight.ArrivalLocal.ToString("dd MMM HH:mm", CultureInfo.InvariantCulture)
                + "\nTimes local to each airport • " + flight.AvailableSeats + " sample seats";
            priceText.text = "USD " + (flight.PriceCents / 100m).ToString("N2", CultureInfo.InvariantCulture);
        }
        private void Save() => saveRequested?.Invoke(shownFlight);
        private void Track() => trackRequested?.Invoke(shownFlight);
        private void OnDestroy() { if (saveButton) saveButton.onClick.RemoveListener(Save); if (trackButton) trackButton.onClick.RemoveListener(Track); }
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
        public event Action<FlightOption> TrackRequested;
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
            foreach (var flight in flights) { var row = Instantiate(rowPrefab, parent); row.gameObject.SetActive(true); row.Show(flight, RequestSave, RequestTrack); rows.Add(row.gameObject); }
        }
        private void RequestSave(FlightOption flight) { if (IsOpen && !IsBusy && LastResult != null) SaveRequested?.Invoke(flight); }
        private void RequestTrack(FlightOption flight) { if (IsOpen && !IsBusy && LastResult != null) TrackRequested?.Invoke(flight); }
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
        [SerializeField] private UnityEngine.UI.Button trackButton;
        private PlaceOption shownPlace;
        private Action<PlaceOption> reviewRequested;
        private Action<PlaceOption> saveRequested;
        private Action<PlaceOption> trackRequested;
        private void Awake() { if (viewReviewsButton) viewReviewsButton.onClick.AddListener(OpenReviews); if (saveButton) saveButton.onClick.AddListener(Save); if (trackButton) trackButton.onClick.AddListener(Track); }
        public void Show(PlaceOption place, Action<PlaceOption> onReviews = null, Action<PlaceOption> onSave = null, Action<PlaceOption> onTrack = null)
        {
            shownPlace = place; reviewRequested = onReviews;
            saveRequested = onSave; if (saveButton) saveButton.interactable = onSave != null;
            trackRequested = onTrack; if (trackButton) { trackButton.gameObject.SetActive(place.Category == PlaceCategory.Hotel); trackButton.interactable = onTrack != null; }
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
        private void Track() { if (shownPlace?.Category == PlaceCategory.Hotel) trackRequested?.Invoke(shownPlace); }
        private void OnDestroy() { if (viewReviewsButton) viewReviewsButton.onClick.RemoveListener(OpenReviews); if (saveButton) saveButton.onClick.RemoveListener(Save); if (trackButton) trackButton.onClick.RemoveListener(Track); }
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
        public void Show(IReadOnlyList<PlaceOption> places, PlaceCard prefab, System.Action<PlaceOption> onReviews = null, System.Action<PlaceOption> onSave = null, System.Action<PlaceOption> onTrack = null)
        {
            Clear(); emptyMessage.gameObject.SetActive(places.Count == 0);
            foreach (var place in places)
            {
                var card = Instantiate(prefab, items); card.Show(place, onReviews, onSave, onTrack); cards.Add(card.gameObject);
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
        public event Action<PlaceOption> TrackRequested;
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
                hotels.Show(result.Hotels, cardPrefab, RequestReview, RequestSave, RequestTrack); restaurants.Show(result.Restaurants, cardPrefab, RequestReview, RequestSave); experiences.Show(result.Experiences, cardPrefab, RequestReview, RequestSave); hotspots.Show(result.Hotspots, cardPrefab, RequestReview, RequestSave);
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
        private void RequestTrack(PlaceOption place) { if (IsOpen && !IsBusy && LastResult != null && place.Category == PlaceCategory.Hotel) TrackRequested?.Invoke(place); }
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
