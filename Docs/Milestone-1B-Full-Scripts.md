# Milestone 1B — Complete source files
These files are already installed in the project. Do not paste a second copy into Assets. JSON source is in Assets/TravelPlanning/SeedData/catalog.json; its format and editing steps are in the adjacent README.md.

## Assets/TravelPlanning/Runtime/Data/SqliteRuntime.cs

```csharp
namespace TravelPlanning.Data
{
    /// <summary>Initializes the Windows SQLite provider once for all database services.</summary>
    public static class SqliteRuntime
    {
        private static readonly object sync = new object();
        private static bool initialized;

        public static void Initialize()
        {
            lock (sync)
            {
                if (initialized) return;
                SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
                initialized = true;
            }
        }
    }
}

```

## Assets/TravelPlanning/Runtime/Data/SqliteProofDatabase.cs

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SQLite;

namespace TravelPlanning.Data
{
    /// <summary>Milestone 1A only: proves the native database can save and reload one row.</summary>
    public sealed class SqliteProofDatabase
    {
        public const string SampleMessage = "Hello, travel planner! Tokyo / O'Hare / 東京";

        // A single doorway prevents two proof runs from writing at the same time.
        private static readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

        public async Task<SqliteProofResult> RunAsync(string path, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A database file path is required.", nameof(path));

            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Opening, SQL, file access and disposal all happen on this worker.
                return await Task.Run(() => Run(path, cancellationToken), cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }

        private static SqliteProofResult Run(string path, CancellationToken cancellationToken)
        {
            var timer = Stopwatch.StartNew();
            cancellationToken.ThrowIfCancellationRequested();
            SqliteRuntime.Initialize();

            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            using (var connection = new SQLiteConnection(fullPath))
            {
                connection.BusyTimeout = TimeSpan.FromSeconds(3);
                connection.Execute("PRAGMA foreign_keys = ON");
                connection.Execute("CREATE TABLE IF NOT EXISTS proof_rows (" +
                    "id INTEGER PRIMARY KEY CHECK (id = 1), " +
                    "message TEXT NOT NULL, created_utc TEXT NOT NULL, " +
                    "visits INTEGER NOT NULL CHECK (visits >= 1))");

                bool existed = false;
                connection.RunInTransaction(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    existed = connection.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM proof_rows WHERE id = ?", 1) == 1;
                    if (existed)
                    {
                        if (connection.ExecuteScalar<string>("SELECT message FROM proof_rows WHERE id = ?", 1) != SampleMessage)
                            throw new InvalidDataException("The saved proof message is unexpected; the file was not reset.");
                        // Never overwrite the saved message or creation time on later runs.
                        connection.Execute("UPDATE proof_rows SET visits = visits + 1 WHERE id = ?", 1);
                    }
                    else
                    {
                        // Apostrophes and Unicode are data, never concatenated into SQL.
                        connection.Execute("INSERT INTO proof_rows (id, message, created_utc, visits) " +
                            "VALUES (?, ?, ?, ?)", 1, SampleMessage, DateTime.UtcNow.ToString("O"), 1);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                });

                string message = connection.ExecuteScalar<string>(
                    "SELECT message FROM proof_rows WHERE id = ?", 1);
                if (message != SampleMessage)
                    throw new InvalidDataException("The saved proof message is unexpected; the file was not reset.");

                return new SqliteProofResult
                {
                    DatabasePath = fullPath,
                    Message = message,
                    CreatedUtc = connection.ExecuteScalar<string>(
                        "SELECT created_utc FROM proof_rows WHERE id = ?", 1),
                    Visits = connection.ExecuteScalar<int>("SELECT visits FROM proof_rows WHERE id = ?", 1),
                    WasAlreadySaved = existed,
                    SqliteVersion = connection.ExecuteScalar<string>("SELECT sqlite_version()"),
                    WorkerThreadId = Thread.CurrentThread.ManagedThreadId,
                    ElapsedMilliseconds = timer.ElapsedMilliseconds
                };
            }
        }
    }
}

```

## Assets/TravelPlanning/Runtime/Data/TravelDatabaseSchema.cs

```csharp
using System;
using SQLite;

namespace TravelPlanning.Data
{
    /// <summary>The database blueprint. Only the seed builder creates this version.</summary>
    public static class TravelDatabaseSchema
    {
        public const int Version = 1;
        public const int ApplicationId = 0x5452504C;
        private const string Targets = "flight_id TEXT REFERENCES flights(id), hotel_id TEXT REFERENCES hotels(id), " +
            "restaurant_id TEXT REFERENCES restaurants(id), experience_id TEXT REFERENCES experiences(id), hotspot_id TEXT REFERENCES hotspots(id)";

        public static void Create(SQLiteConnection connection)
        {
            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'") != 0)
                throw new InvalidOperationException("Create requires an empty seed database; existing data was not replaced.");
            connection.Execute("PRAGMA foreign_keys = ON");
            connection.RunInTransaction(() =>
            {
                connection.Execute("CREATE TABLE metadata (key TEXT PRIMARY KEY NOT NULL, value TEXT NOT NULL)");
                connection.Execute("CREATE TABLE users (id TEXT PRIMARY KEY NOT NULL, email_normalized TEXT NOT NULL UNIQUE CHECK(email_normalized=lower(trim(email_normalized))), " +
                    "password_hash TEXT NOT NULL CHECK(length(password_hash)>0), password_salt TEXT NOT NULL CHECK(length(password_salt)>0), " +
                    "password_iterations INTEGER NOT NULL CHECK(password_iterations>0), password_algorithm TEXT NOT NULL DEFAULT 'PBKDF2-SHA256', created_utc TEXT NOT NULL)");
                connection.Execute("CREATE TABLE destinations (id TEXT PRIMARY KEY NOT NULL, name TEXT NOT NULL, country TEXT NOT NULL, region TEXT NOT NULL, description TEXT NOT NULL)");
                connection.Execute("CREATE TABLE airports (id TEXT PRIMARY KEY NOT NULL CHECK(length(id)=3), destination_id TEXT NOT NULL REFERENCES destinations(id), name TEXT NOT NULL, time_zone TEXT NOT NULL)");
                connection.Execute("CREATE TABLE airlines (id TEXT PRIMARY KEY NOT NULL, name TEXT NOT NULL)");
                connection.Execute("CREATE TABLE flights (id TEXT PRIMARY KEY NOT NULL, airline_id TEXT NOT NULL REFERENCES airlines(id), flight_number TEXT NOT NULL, " +
                    "origin_airport_id TEXT NOT NULL REFERENCES airports(id), destination_airport_id TEXT NOT NULL REFERENCES airports(id), " +
                    "departure_utc TEXT NOT NULL, arrival_utc TEXT NOT NULL CHECK(arrival_utc>departure_utc), departure_local_date TEXT NOT NULL, " +
                    "price_cents INTEGER NOT NULL CHECK(typeof(price_cents)='integer' AND price_cents>=0), available_seats INTEGER NOT NULL CHECK(available_seats>=0), currency TEXT NOT NULL CHECK(currency='USD'), " +
                    "CHECK(origin_airport_id<>destination_airport_id))");
                foreach (string table in new[] { "hotels", "restaurants", "experiences", "hotspots" })
                    connection.Execute("CREATE TABLE " + table + " (id TEXT PRIMARY KEY NOT NULL, destination_id TEXT NOT NULL REFERENCES destinations(id), " +
                        "name TEXT NOT NULL, description TEXT NOT NULL, address TEXT NOT NULL, price_cents INTEGER CHECK(price_cents IS NULL OR (typeof(price_cents)='integer' AND price_cents>=0)), " +
                        "currency TEXT NOT NULL CHECK(currency='USD'), google_maps_url TEXT NOT NULL" + (table == "hotels" ? ", CHECK(price_cents IS NOT NULL))" : ")"));
                connection.Execute("CREATE TABLE reviews (id TEXT PRIMARY KEY NOT NULL, hotel_id TEXT REFERENCES hotels(id), restaurant_id TEXT REFERENCES restaurants(id), " +
                    "experience_id TEXT REFERENCES experiences(id), hotspot_id TEXT REFERENCES hotspots(id), traveler_name TEXT NOT NULL, " +
                    "rating INTEGER NOT NULL CHECK(typeof(rating)='integer' AND rating BETWEEN 1 AND 5), body TEXT NOT NULL, " +
                    "is_demo INTEGER NOT NULL DEFAULT 1 CHECK(is_demo IN (0,1)), " +
                    "CHECK((hotel_id IS NOT NULL)+(restaurant_id IS NOT NULL)+(experience_id IS NOT NULL)+(hotspot_id IS NOT NULL)=1))");
                connection.Execute("CREATE TABLE trips (id TEXT PRIMARY KEY NOT NULL, user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE, " +
                    "name TEXT NOT NULL, start_date TEXT NOT NULL, end_date TEXT NOT NULL CHECK(end_date>=start_date), created_utc TEXT NOT NULL, updated_utc TEXT NOT NULL, UNIQUE(id,user_id))");
                connection.Execute("CREATE TABLE saved_items (id TEXT PRIMARY KEY NOT NULL, user_id TEXT NOT NULL, trip_id TEXT NOT NULL, " + Targets + ", created_utc TEXT NOT NULL, " +
                    "FOREIGN KEY(trip_id,user_id) REFERENCES trips(id,user_id) ON DELETE CASCADE, " +
                    "CHECK((flight_id IS NOT NULL)+(hotel_id IS NOT NULL)+(restaurant_id IS NOT NULL)+(experience_id IS NOT NULL)+(hotspot_id IS NOT NULL)=1))");
                connection.Execute("CREATE TABLE price_watches (id TEXT PRIMARY KEY NOT NULL, user_id TEXT NOT NULL REFERENCES users(id) ON DELETE CASCADE, " +
                    "flight_id TEXT REFERENCES flights(id), hotel_id TEXT REFERENCES hotels(id), last_price_cents INTEGER NOT NULL CHECK(typeof(last_price_cents)='integer' AND last_price_cents>=0), active INTEGER NOT NULL DEFAULT 1 CHECK(active IN (0,1)), created_utc TEXT NOT NULL, UNIQUE(id,user_id), " +
                    "CHECK((flight_id IS NOT NULL)+(hotel_id IS NOT NULL)=1))");
                connection.Execute("CREATE TABLE price_history (id TEXT PRIMARY KEY NOT NULL, flight_id TEXT REFERENCES flights(id), hotel_id TEXT REFERENCES hotels(id), " +
                    "old_price_cents INTEGER NOT NULL CHECK(typeof(old_price_cents)='integer' AND old_price_cents>=0), " +
                    "new_price_cents INTEGER NOT NULL CHECK(typeof(new_price_cents)='integer' AND new_price_cents>=0 AND new_price_cents<>old_price_cents), changed_utc TEXT NOT NULL, " +
                    "CHECK((flight_id IS NOT NULL)+(hotel_id IS NOT NULL)=1))");
                connection.Execute("CREATE TABLE notifications (id TEXT PRIMARY KEY NOT NULL, user_id TEXT NOT NULL, watch_id TEXT NOT NULL, " +
                    "price_history_id TEXT NOT NULL REFERENCES price_history(id), title TEXT NOT NULL, body TEXT NOT NULL, created_utc TEXT NOT NULL, " +
                    "is_read INTEGER NOT NULL DEFAULT 0 CHECK(is_read IN (0,1)), UNIQUE(watch_id,price_history_id), " +
                    "FOREIGN KEY(watch_id,user_id) REFERENCES price_watches(id,user_id) ON DELETE CASCADE)");
                CreateIndexes(connection);
                // A notification must describe a price change for the item this user watches.
                foreach (string action in new[] { "INSERT", "UPDATE" })
                    connection.Execute("CREATE TRIGGER notification_target_" + action.ToLowerInvariant() + " BEFORE " + action + " ON notifications " +
                        "WHEN NOT EXISTS (SELECT 1 FROM price_watches w JOIN price_history h ON h.id=NEW.price_history_id " +
                        "WHERE w.id=NEW.watch_id AND w.user_id=NEW.user_id AND ((w.flight_id IS NOT NULL AND w.flight_id=h.flight_id) OR " +
                        "(w.hotel_id IS NOT NULL AND w.hotel_id=h.hotel_id))) BEGIN SELECT RAISE(ABORT,'Notification target does not match watch'); END");
                // Target identities are permanent. Remove/recreate a watch instead of repointing it.
                connection.Execute("CREATE TRIGGER watch_target_immutable BEFORE UPDATE OF user_id,flight_id,hotel_id ON price_watches " +
                    "WHEN NEW.user_id<>OLD.user_id OR NEW.flight_id IS NOT OLD.flight_id OR NEW.hotel_id IS NOT OLD.hotel_id " +
                    "BEGIN SELECT RAISE(ABORT,'Watch ownership and target cannot change'); END");
                connection.Execute("CREATE TRIGGER history_immutable BEFORE UPDATE ON price_history BEGIN SELECT RAISE(ABORT,'Price history is immutable'); END");
                connection.Execute("PRAGMA application_id = " + ApplicationId);
                connection.Execute("PRAGMA user_version = " + Version);
            });
        }

        private static void CreateIndexes(SQLiteConnection connection)
        {
            connection.Execute("CREATE INDEX flights_search ON flights(origin_airport_id,destination_airport_id,departure_local_date,price_cents)");
            connection.Execute("CREATE INDEX flights_airline ON flights(airline_id)");
            connection.Execute("CREATE INDEX flights_destination ON flights(destination_airport_id)");
            connection.Execute("CREATE INDEX airports_destination ON airports(destination_id)");
            connection.Execute("CREATE INDEX trips_user ON trips(user_id)");
            connection.Execute("CREATE INDEX saved_items_owner ON saved_items(user_id,trip_id)");
            connection.Execute("CREATE INDEX notifications_unread ON notifications(user_id,is_read,created_utc)");
            connection.Execute("CREATE INDEX notifications_history ON notifications(price_history_id)");
            foreach (string table in new[] { "hotels", "restaurants", "experiences", "hotspots" })
                connection.Execute("CREATE INDEX " + table + "_destination ON " + table + "(destination_id)");
            foreach (string target in new[] { "flight", "hotel", "restaurant", "experience", "hotspot" })
            {
                string column = target + "_id";
                connection.Execute("CREATE UNIQUE INDEX saved_" + target + " ON saved_items(trip_id," + column + ") WHERE " + column + " IS NOT NULL");
                connection.Execute("CREATE INDEX saved_" + target + "_target ON saved_items(" + column + ") WHERE " + column + " IS NOT NULL");
                if (target != "flight") connection.Execute("CREATE INDEX reviews_" + target + " ON reviews(" + column + ") WHERE " + column + " IS NOT NULL");
                if (target == "flight" || target == "hotel")
                {
                    connection.Execute("CREATE UNIQUE INDEX watches_" + target + " ON price_watches(user_id," + column + ") WHERE " + column + " IS NOT NULL");
                    connection.Execute("CREATE INDEX watches_" + target + "_target ON price_watches(" + column + ") WHERE " + column + " IS NOT NULL");
                    connection.Execute("CREATE INDEX history_" + target + " ON price_history(" + column + ",changed_utc) WHERE " + column + " IS NOT NULL");
                }
            }
        }

        public static void Validate(SQLiteConnection connection)
        {
            if (connection.ExecuteScalar<int>("PRAGMA application_id") != ApplicationId || connection.ExecuteScalar<int>("PRAGMA user_version") != Version)
                throw new InvalidOperationException("This database is not a supported Travel Planner database.");
            if (connection.ExecuteScalar<string>("PRAGMA integrity_check") != "ok" || connection.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_foreign_key_check") != 0)
                throw new InvalidOperationException("The database failed its integrity or foreign-key check.");
            foreach (string table in new[] { "metadata", "users", "destinations", "airports", "airlines", "flights", "hotels", "restaurants", "experiences", "hotspots", "reviews", "trips", "saved_items", "price_watches", "notifications", "price_history" })
                if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=?", table) != 1)
                    throw new InvalidOperationException("Missing required table: " + table);
            // A matching version number alone cannot prove that columns/indexes still exist.
            foreach (string contract in new[] {
                "metadata:key,value", "users:id,email_normalized,password_hash,password_salt,password_iterations,password_algorithm,created_utc",
                "destinations:id,name,country,region,description", "airports:id,destination_id,name,time_zone", "airlines:id,name",
                "flights:id,airline_id,flight_number,origin_airport_id,destination_airport_id,departure_utc,arrival_utc,departure_local_date,price_cents,available_seats,currency",
                "hotels:id,destination_id,name,description,address,price_cents,currency,google_maps_url", "restaurants:id,destination_id,name,description,address,price_cents,currency,google_maps_url",
                "experiences:id,destination_id,name,description,address,price_cents,currency,google_maps_url", "hotspots:id,destination_id,name,description,address,price_cents,currency,google_maps_url",
                "reviews:id,hotel_id,restaurant_id,experience_id,hotspot_id,traveler_name,rating,body,is_demo", "trips:id,user_id,name,start_date,end_date,created_utc,updated_utc",
                "saved_items:id,user_id,trip_id,flight_id,hotel_id,restaurant_id,experience_id,hotspot_id,created_utc",
                "price_watches:id,user_id,flight_id,hotel_id,last_price_cents,active,created_utc", "price_history:id,flight_id,hotel_id,old_price_cents,new_price_cents,changed_utc",
                "notifications:id,user_id,watch_id,price_history_id,title,body,created_utc,is_read" })
            {
                string[] parts = contract.Split(':');
                foreach (string column in parts[1].Split(','))
                    if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM pragma_table_info(?) WHERE name=?", parts[0], column) != 1)
                        throw new InvalidOperationException("Missing required column: " + parts[0] + "." + column);
            }
            foreach (string index in new[] { "flights_search", "notifications_unread", "saved_flight", "saved_hotel", "saved_restaurant", "saved_experience", "saved_hotspot", "watches_flight", "watches_hotel" })
                RequireObject(connection, "index", index);
            foreach (string trigger in new[] { "notification_target_insert", "notification_target_update", "watch_target_immutable", "history_immutable" })
                RequireObject(connection, "trigger", trigger);
        }

        private static void RequireObject(SQLiteConnection connection, string type, string name)
        {
            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE type=? AND name=?", type, name) != 1)
                throw new InvalidOperationException("Missing required " + type + ": " + name);
        }
    }
}

```

## Assets/TravelPlanning/Runtime/Data/TravelSeedData.cs

```csharp
using System;

namespace TravelPlanning.Data
{
    // Plain data containers match the JSON field names. They contain no database or Unity logic.
    [Serializable] public sealed class TravelSeedData
    {
        public string catalogVersion;
        public string startDate;
        public string endDate;
        public SeedDestination[] destinations;
        public SeedAirport[] airports;
        public SeedAirline[] airlines;
        public SeedFlight[] flights;
        public SeedPlace[] places;
        public SeedReview[] reviews;
    }
    [Serializable] public sealed class SeedDestination { public string id, name, country, region, description; }
    [Serializable] public sealed class SeedAirport { public string id, destinationId, name, timeZone; }
    [Serializable] public sealed class SeedAirline { public string id, name; }
    [Serializable] public sealed class SeedFlight
    {
        public string id, airlineId, flightNumber, originAirportId, destinationAirportId;
        public string departureUtc, arrivalUtc, departureLocalDate;
        public int priceCents, availableSeats;
    }
    [Serializable] public sealed class SeedPlace
    {
        public string id, kind, destinationId, name, description, address, googleMapsUrl;
        public int priceCents;
    }
    [Serializable] public sealed class SeedReview { public string id, placeId, kind, travelerName, body; public int rating; }
}

```

## Assets/TravelPlanning/Runtime/Data/TravelDatabaseStatus.cs

```csharp
namespace TravelPlanning.Data
{
    /// <summary>Startup information returned to the diagnostic UI, not a Unity component.</summary>
    public sealed class TravelDatabaseStatus
    {
        public string DatabasePath { get; set; }
        public bool CopiedSeed { get; set; }
        public int SchemaVersion { get; set; }
        public string CatalogVersion { get; set; }
        public int Destinations { get; set; }
        public int Flights { get; set; }
        public int Hotels { get; set; }
        public int Restaurants { get; set; }
        public int Experiences { get; set; }
        public int Hotspots { get; set; }
        public int Reviews { get; set; }
        public int Users { get; set; }
        public int Trips { get; set; }
        public int WorkerThreadId { get; set; }
    }
}

```

## Assets/TravelPlanning/Runtime/Data/TravelDatabase.cs

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SQLite;

namespace TravelPlanning.Data
{
    /// <summary>Copies a validated seed once, then owns serialized background database access.</summary>
    public sealed class TravelDatabase
    {
        private static readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private readonly string seedPath;
        private readonly string databasePath;
        private bool initialized;

        // Capture Application.streamingAssetsPath/persistentDataPath on the Unity main thread first.
        public TravelDatabase(string seedPath, string databasePath)
        {
            if (string.IsNullOrWhiteSpace(seedPath) || string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("Both seed and writable database paths are required.");
            this.seedPath = Path.GetFullPath(seedPath);
            this.databasePath = Path.GetFullPath(databasePath);
            if (string.Equals(this.seedPath, this.databasePath, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The writable database must be separate from the bundled seed.");
        }

        public Task<TravelDatabaseStatus> InitializeAsync(CancellationToken cancellationToken)
        {
            return OnWorkerAsync(() =>
            {
                SqliteRuntime.Initialize();
                bool copied = CopySeedIfMissing(cancellationToken);
                using (var connection = Open(databasePath, SQLiteOpenFlags.ReadOnly))
                {
                    TravelDatabaseSchema.Validate(connection);
                    cancellationToken.ThrowIfCancellationRequested();
                    var status = ReadStatus(connection, copied);
                    initialized = true;
                    return status;
                }
            }, cancellationToken);
        }

        // Repositories supply SQL operations here. Their delegates must not use any Unity API.
        public Task<T> ExecuteAsync<T>(Func<SQLiteConnection, T> operation, CancellationToken cancellationToken)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            return OnWorkerAsync(() =>
            {
                if (!initialized) throw new InvalidOperationException("Initialize the travel database before using it.");
                using (var connection = Open(databasePath, SQLiteOpenFlags.ReadWrite))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return operation(connection);
                }
            }, cancellationToken);
        }

        private static async Task<T> OnWorkerAsync<T>(Func<T> operation, CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { return await Task.Run(operation, cancellationToken).ConfigureAwait(false); }
            finally { gate.Release(); }
        }

        private bool CopySeedIfMissing(CancellationToken cancellationToken)
        {
            if (File.Exists(databasePath)) return false; // Never replace an existing user's database.
            if (!File.Exists(seedPath)) throw new FileNotFoundException("The bundled travel seed database is missing.", seedPath);
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath));
            string temporary = databasePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Copy(seedPath, temporary, false);
                using (var copy = Open(temporary, SQLiteOpenFlags.ReadOnly)) TravelDatabaseSchema.Validate(copy);
                cancellationToken.ThrowIfCancellationRequested();
                try { File.Move(temporary, databasePath); }
                catch (IOException) when (File.Exists(databasePath))
                {
                    // Another process won the first-launch race. Validate its file in the caller.
                    return false;
                }
                return true;
            }
            finally
            {
                // Only our UUID temporary file is eligible for cleanup, never the user's database.
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private static SQLiteConnection Open(string path, SQLiteOpenFlags flags)
        {
            var connection = new SQLiteConnection(path, flags);
            try
            {
                connection.BusyTimeout = TimeSpan.FromSeconds(3);
                connection.Execute("PRAGMA foreign_keys = ON");
                return connection;
            }
            catch { connection.Dispose(); throw; }
        }

        private TravelDatabaseStatus ReadStatus(SQLiteConnection connection, bool copied)
        {
            return new TravelDatabaseStatus
            {
                DatabasePath = databasePath, CopiedSeed = copied,
                SchemaVersion = connection.ExecuteScalar<int>("PRAGMA user_version"),
                CatalogVersion = connection.ExecuteScalar<string>("SELECT value FROM metadata WHERE key = ?", "catalog_version"),
                Destinations = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM destinations"),
                Flights = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM flights"),
                Hotels = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM hotels"),
                Restaurants = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM restaurants"),
                Experiences = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM experiences"),
                Hotspots = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM hotspots"),
                Reviews = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM reviews"),
                Users = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM users"),
                Trips = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM trips"),
                WorkerThreadId = Thread.CurrentThread.ManagedThreadId
            };
        }
    }
}

```

## Assets/TravelPlanning/UI/TravelDatabasePage.cs

```csharp
using System;
using System.IO;
using System.Threading;
using TravelPlanning.Data;
using UnityEngine;

namespace TravelPlanning.UI
{
    /// <summary>Milestone 1B diagnostic screen; it does not replace the login screen.</summary>
    public sealed class TravelDatabasePage : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Text statusText;
        [SerializeField] private UnityEngine.UI.Text detailsText;
        [SerializeField] private UnityEngine.UI.Text heartbeatText;
        [SerializeField] private UnityEngine.UI.Button runButton;
        private TravelDatabase database;
        private CancellationToken lifetime;
        private bool busy;
        private bool smoke;
        private int mainThreadId;

        private void Start()
        {
            lifetime = destroyCancellationToken;
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            smoke = HasArgument("-travelSeedSmoke");
            if (statusText == null || detailsText == null || heartbeatText == null || runButton == null)
            {
                Debug.LogError("TravelDatabasePage: assign Status Text, Details Text, Heartbeat Text and Run Button.");
                if (smoke) Application.Quit(2);
                return;
            }
            try
            {
                string fileName = ArgumentValue("-travelSeedFile") ?? "travel.db";
                if (Path.GetFileName(fileName) != fileName || !fileName.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Use a plain database filename ending in .db.");
                database = new TravelDatabase(Path.Combine(Application.streamingAssetsPath, "Database", "travel_seed.db"),
                    Path.Combine(Application.persistentDataPath, fileName));
                runButton.onClick.AddListener(InitializeDatabase);
                InitializeDatabase();
            }
            catch (Exception error) { ReportFailure(error); }
        }

        private void Update()
        {
            if (heartbeatText != null) heartbeatText.text = "UI alive: " + Time.realtimeSinceStartup.ToString("F1") + " s";
        }

        private async void InitializeDatabase()
        {
            if (busy) return;
            busy = true;
            runButton.interactable = false;
            statusText.text = "Validating the travel database on a worker thread...";
            try
            {
                var result = await database.InitializeAsync(lifetime);
                bool copied = result.CopiedSeed;
                if (smoke && Debug.isDebugBuild && HasArgument("-travelSeedWriteMarker"))
                {
                    // Development smoke fixture only; not a real account or authentication flow.
                    await database.ExecuteAsync(connection =>
                    {
                        connection.RunInTransaction(() =>
                        {
                            connection.Execute("INSERT INTO users (id,email_normalized,password_hash,password_salt,password_iterations,created_utc) VALUES (?,?,?,?,?,?)",
                                "smoke-user", "smoke@example.invalid", Convert.ToBase64String(new byte[32]),
                                Convert.ToBase64String(new byte[16]), 600000, "2027-01-01T00:00:00Z");
                            connection.Execute("INSERT INTO trips (id,user_id,name,start_date,end_date,created_utc,updated_utc) VALUES (?,?,?,?,?,?,?)",
                                "smoke-trip", "smoke-user", "Original trip", "2027-06-15", "2027-06-22",
                                "2027-01-01T00:00:00Z", "2027-01-01T00:00:00Z");
                            connection.Execute("UPDATE trips SET name = ? WHERE id = ?", "Kept after restart", "smoke-trip");
                        });
                        return true;
                    }, lifetime);
                    result = await database.InitializeAsync(lifetime);
                }
                if (lifetime.IsCancellationRequested) return;
                if (result.WorkerThreadId == mainThreadId || Thread.CurrentThread.ManagedThreadId != mainThreadId)
                    throw new InvalidOperationException("The database/UI thread boundary failed.");
                if (smoke)
                {
                    CheckExpected("-travelSeedExpectedCopied", copied ? 1 : 0);
                    CheckExpected("-travelSeedExpectedUsers", result.Users);
                    CheckExpected("-travelSeedExpectedTrips", result.Trips);
                    if (result.Users == 1 && result.Trips == 1)
                    {
                        string name = await database.ExecuteAsync(connection =>
                            connection.ExecuteScalar<string>("SELECT name FROM trips WHERE id = ?", "smoke-trip"), lifetime);
                        if (name != "Kept after restart") throw new InvalidDataException("The user's edited trip was not preserved.");
                    }
                }
                if (lifetime.IsCancellationRequested) return;
                statusText.text = copied ? "PASS - seed copied and validated" : "PASS - existing database preserved";
                detailsText.text = "Schema: " + result.SchemaVersion + " | Catalog: " + result.CatalogVersion +
                    "\nDestinations: " + result.Destinations + " | Flights: " + result.Flights +
                    "\nHotels: " + result.Hotels + " | Restaurants: " + result.Restaurants +
                    "\nExperiences: " + result.Experiences + " | Hotspots: " + result.Hotspots +
                    "\nDemo reviews: " + result.Reviews + " | Users: " + result.Users + " | Trips: " + result.Trips +
                    "\nWorker: " + result.WorkerThreadId + " | UI: " + mainThreadId +
                    "\n\n" + result.DatabasePath + "\n\nSample prices/reviews. Demo travel dates: June 2027.";
                Debug.Log("TRAVEL_DATABASE_PASS copied=" + copied + " destinations=" + result.Destinations +
                    " users=" + result.Users + " trips=" + result.Trips + " worker=" + result.WorkerThreadId +
                    " main=" + mainThreadId + " catalog=" + result.CatalogVersion + " path=" + result.DatabasePath);
                if (smoke) Application.Quit(0);
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (!lifetime.IsCancellationRequested) ReportFailure(error); }
            finally
            {
                busy = false;
                if (!lifetime.IsCancellationRequested && runButton != null) runButton.interactable = true;
            }
        }

        private void ReportFailure(Exception error)
        {
            if (statusText != null) statusText.text = "FAIL - travel database could not be initialized";
            if (detailsText != null) detailsText.text = error.GetBaseException().Message +
                "\n\nExisting data has not been replaced. See the Console or Player.log.";
            Debug.LogError("TRAVEL_DATABASE_FAIL " + error);
            if (smoke) Application.Quit(1);
        }

        private void OnDestroy()
        {
            if (runButton != null) runButton.onClick.RemoveListener(InitializeDatabase);
        }

        private static void CheckExpected(string option, int actual)
        {
            string value = ArgumentValue(option);
            if (value != null && (!int.TryParse(value, out int expected) || expected != actual))
                throw new InvalidDataException(option + " did not match. Actual: " + actual);
        }
        private static bool HasArgument(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;
        private static string ArgumentValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}

```

## Assets/TravelPlanning/UI/Editor/TravelSeedBuilder.cs

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SQLite;
using TravelPlanning.Data;
using UnityEditor;
using UnityEngine;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Turns editable JSON into a checked SQLite template, never into a user's live file.</summary>
    public static class TravelSeedBuilder
    {
        public const string JsonPath = "Assets/TravelPlanning/SeedData/catalog.json";
        public const string DatabasePath = "Assets/StreamingAssets/Database/travel_seed.db";
        private static readonly string[] Kinds = { "hotel", "restaurant", "experience", "hotspot" };

        [MenuItem("Travel Planning/Database/Build Seed Database")]
        public static void BuildSeed()
        {
            BuildFromJson(JsonPath, DatabasePath);
            AssetDatabase.Refresh();
        }

        public static void BuildFromJson(string jsonPath, string outputPath)
        {
            TravelSeedData data = JsonUtility.FromJson<TravelSeedData>(File.ReadAllText(jsonPath));
            ValidateData(data);
            SqliteRuntime.Initialize();
            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            string staging = outputPath + ".staging-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var db = new SQLiteConnection(staging))
                {
                    TravelDatabaseSchema.Create(db);
                    db.RunInTransaction(() => InsertData(db, data));
                    TravelDatabaseSchema.Validate(db);
                    if (db.ExecuteScalar<int>("SELECT COUNT(*) FROM users") != 0)
                        throw new InvalidDataException("A seed must not contain user accounts.");
                }
                // Validate completely before replacing the template. Its Unity .meta is untouched.
                if (File.Exists(outputPath)) File.Replace(staging, outputPath, null);
                else File.Move(staging, outputPath);
                Debug.Log("TRAVEL_SEED_READY catalog=" + data.catalogVersion + " destinations=" + data.destinations.Length + " flights=" + data.flights.Length);
            }
            finally
            {
                if (File.Exists(staging)) File.Delete(staging);
            }
        }

        // The importer validates data, not a fixed list of city names or a fixed count of cities.
        public static void ValidateData(TravelSeedData data)
        {
            if (data == null) throw new InvalidDataException("The catalog JSON is empty.");
            Required(data.catalogVersion, "catalogVersion");
            DateTime start = Date(data.startDate), end = Date(data.endDate);
            if (end < start) throw new InvalidDataException("The catalog date range is reversed.");
            if (data.destinations == null || data.airports == null || data.airlines == null || data.flights == null || data.places == null || data.reviews == null)
                throw new InvalidDataException("All catalog arrays must be present, even when empty.");
            var destinations = Ids(data.destinations.Select(x => x?.id), "destination");
            var airports = Ids(data.airports.Select(x => x?.id), "airport");
            var airlines = Ids(data.airlines.Select(x => x?.id), "airline");
            Ids(data.flights.Select(x => x?.id), "flight");
            var places = Ids(data.places.Select(x => x?.id), "place");
            Ids(data.reviews.Select(x => x?.id), "review");
            if (destinations.Count == 0) throw new InvalidDataException("At least one destination is required.");
            foreach (var d in data.destinations) { Required(d.name, d.id); Required(d.country, d.id); Required(d.region, d.id); Required(d.description, d.id); }
            foreach (var a in data.airlines) Required(a.name, a.id);
            var zones = new Dictionary<string, TimeZoneInfo>();
            foreach (var a in data.airports)
            {
                Reference(destinations, a.destinationId, a.id); Required(a.name, a.id);
                if (a.id.Length != 3 || a.id.Any(c => c < 'A' || c > 'Z')) throw new InvalidDataException("Airport ID must be three uppercase letters: " + a.id);
                zones[a.id] = TimeZoneInfo.FindSystemTimeZoneById(a.timeZone);
            }
            foreach (var f in data.flights)
            {
                Reference(airports, f.originAirportId, f.id); Reference(airports, f.destinationAirportId, f.id); Reference(airlines, f.airlineId, f.id);
                Required(f.flightNumber, f.id); Price(f.priceCents, f.id);
                DateTime departure = Utc(f.departureUtc), arrival = Utc(f.arrivalUtc), local = Date(f.departureLocalDate);
                if (arrival <= departure || f.originAirportId == f.destinationAirportId || f.availableSeats < 0 || local < start || local > end)
                    throw new InvalidDataException("Invalid flight dates, route or seats: " + f.id);
                if (TimeZoneInfo.ConvertTimeFromUtc(departure, zones[f.originAirportId]).Date != local)
                    throw new InvalidDataException("Departure local date does not match its airport time zone: " + f.id);
            }
            var kinds = new Dictionary<string, string>();
            foreach (var p in data.places)
            {
                Kind(p.kind); kinds[p.id] = p.kind; Reference(destinations, p.destinationId, p.id);
                Required(p.name, p.id); Required(p.description, p.id); Required(p.address, p.id); Price(p.priceCents, p.id);
                if (!Uri.TryCreate(p.googleMapsUrl, UriKind.Absolute, out Uri url) || url.Scheme != "https" ||
                    !(url.Host == "www.google.com" || url.Host == "google.com") || url.AbsolutePath != "/maps/search/" || !url.Query.Contains("query="))
                    throw new InvalidDataException("Use an HTTPS Google Maps search reference: " + p.id);
            }
            foreach (var r in data.reviews)
            {
                Reference(places, r.placeId, r.id); Kind(r.kind); Required(r.travelerName, r.id); Required(r.body, r.id);
                if (r.rating < 1 || r.rating > 5 || kinds[r.placeId] != r.kind) throw new InvalidDataException("Invalid review rating or target: " + r.id);
            }
        }

        private static void InsertData(SQLiteConnection db, TravelSeedData data)
        {
            foreach (var pair in new[] { new[] { "catalog_version", data.catalogVersion }, new[] { "is_demo", "1" }, new[] { "seed_start_date", data.startDate }, new[] { "seed_end_date", data.endDate } })
                db.Execute("INSERT INTO metadata(key,value) VALUES (?,?)", pair[0], pair[1]);
            foreach (var d in data.destinations) db.Execute("INSERT INTO destinations VALUES (?,?,?,?,?)", d.id, d.name, d.country, d.region, d.description);
            foreach (var a in data.airports) db.Execute("INSERT INTO airports VALUES (?,?,?,?)", a.id, a.destinationId, a.name, a.timeZone);
            foreach (var a in data.airlines) db.Execute("INSERT INTO airlines VALUES (?,?)", a.id, a.name);
            foreach (var f in data.flights)
                db.Execute("INSERT INTO flights VALUES (?,?,?,?,?,?,?,?,?,?,?)", f.id, f.airlineId, f.flightNumber, f.originAirportId, f.destinationAirportId,
                    f.departureUtc, f.arrivalUtc, f.departureLocalDate, f.priceCents, f.availableSeats, "USD");
            foreach (var p in data.places)
                db.Execute("INSERT INTO " + Table(p.kind) + " VALUES (?,?,?,?,?,?,?,?)", p.id, p.destinationId, p.name, p.description, p.address, p.priceCents, "USD", p.googleMapsUrl);
            foreach (var r in data.reviews)
                db.Execute("INSERT INTO reviews(id," + r.kind + "_id,traveler_name,rating,body,is_demo) VALUES (?,?,?,?,?,?)", r.id, r.placeId, r.travelerName, r.rating, r.body, 1);
        }

        private static HashSet<string> Ids(IEnumerable<string> ids, string label)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids) { Required(id, label); if (!result.Add(id)) throw new InvalidDataException("Duplicate " + label + " ID: " + id); }
            return result;
        }
        private static void Required(string value, string label) { if (string.IsNullOrWhiteSpace(value) || value != value.Trim()) throw new InvalidDataException("Missing or padded text: " + label); }
        private static void Reference(HashSet<string> ids, string id, string owner) { if (id == null || !ids.Contains(id)) throw new InvalidDataException("Unknown reference in " + owner + ": " + id); }
        private static void Price(int cents, string owner) { if (cents < 0) throw new InvalidDataException("Negative price: " + owner); }
        private static void Kind(string kind) { if (!Kinds.Contains(kind)) throw new InvalidDataException("Unknown venue kind: " + kind); }
        private static string Table(string kind) { Kind(kind); return kind == "hotspot" ? "hotspots" : kind + "s"; }
        private static DateTime Date(string value)
        {
            if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result)) throw new InvalidDataException("Invalid date: " + value);
            return result;
        }
        private static DateTime Utc(string value)
        {
            if (!DateTime.TryParseExact(value, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime result)) throw new InvalidDataException("Invalid UTC timestamp: " + value);
            return result;
        }
    }
}

```

## Assets/TravelPlanning/UI/Editor/TravelDatabaseSetup.cs

```csharp
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TravelPlanning.UI.Editor
{
    /// <summary>Builds a separate seed-database diagnostic without altering Kevin's login.</summary>
    public static class TravelDatabaseSetup
    {
        public const string ScenePath = "Assets/TravelPlanning/Scenes/TravelDatabase.unity";
        public const string BuildPath = "Builds/TravelDatabase/TravelPlannerDatabase.exe";

        [MenuItem("Travel Planning/Travel Database/2 - Create Diagnostic Scene")]
        public static void CreateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            if (!File.Exists(SqliteProofSetup.ScenePath)) SqliteProofSetup.CreateScene();
            // Use the established diagnostic layout, then save as a NEW scene.
            var scene = EditorSceneManager.OpenScene(SqliteProofSetup.ScenePath);
            var oldPage = UnityEngine.Object.FindFirstObjectByType<SqliteProofPage>();
            var oldFields = new SerializedObject(oldPage);
            string[] names = { "statusText", "detailsText", "heartbeatText", "runButton" };
            var references = new UnityEngine.Object[names.Length];
            for (int index = 0; index < names.Length; index++) references[index] = oldFields.FindProperty(names[index]).objectReferenceValue;
            GameObject panel = oldPage.gameObject;
            UnityEngine.Object.DestroyImmediate(oldPage);
            var newFields = new SerializedObject(panel.AddComponent<TravelDatabasePage>());
            for (int index = 0; index < names.Length; index++) newFields.FindProperty(names[index]).objectReferenceValue = references[index];
            newFields.ApplyModifiedPropertiesWithoutUndo();
            panel.transform.Find("Title").GetComponent<UnityEngine.UI.Text>().text = "Travel database - Milestone 1B";
            panel.transform.Find("RunAgain/Label").GetComponent<UnityEngine.UI.Text>().text = "Validate again (preserves your database)";
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("TRAVEL_DATABASE_SCENE_READY " + ScenePath);
        }

        public static void Prepare()
        {
            TravelSeedBuilder.BuildSeed();
            CreateScene();
        }

        [MenuItem("Travel Planning/Travel Database/3 - Build Windows x64")]
        public static void BuildWindows()
        {
            TravelSeedBuilder.BuildSeed();
            CreateScene();
            Directory.CreateDirectory(Path.GetDirectoryName(BuildPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Travel database build failed.");
            string notices = Path.Combine(Path.GetDirectoryName(BuildPath), "ThirdPartyNotices");
            Directory.CreateDirectory(notices);
            foreach (string file in Directory.GetFiles("Assets/Plugins/SQLite/Licenses"))
                if (!file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) File.Copy(file, Path.Combine(notices, Path.GetFileName(file)), true);
            File.Copy("Assets/Plugins/LiteDB/LICENSE.txt", Path.Combine(notices, "LiteDB-LICENSE.txt"), true);
            Debug.Log("TRAVEL_DATABASE_BUILD_PASS " + Path.GetFullPath(BuildPath));
        }
    }
}

```

## Assets/TravelPlanning/Tests/EditMode/TravelDatabaseTests.cs

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
using UnityEngine;
using UnityEngine.TestTools;

namespace TravelPlanning.Tests
{
    public sealed class TravelDatabaseTests
    {
        private string folder;
        private string seed;
        private string writable;

        [SetUp]
        public void SetUp()
        {
            SqliteRuntime.Initialize();
            folder = Path.Combine(Path.GetTempPath(), "TravelDatabaseTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            seed = Path.Combine(Application.streamingAssetsPath, "Database", "travel_seed.db");
            writable = Path.Combine(folder, "travel.db");
            Assert.That(File.Exists(seed), Is.True, "Build the seed database before running these tests.");
        }

        [TearDown]
        public void TearDown() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }

        [UnityTest]
        public IEnumerator FirstCopyAndReopenPreserveEditedUserTrip()
        {
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            var service = new TravelDatabase(seed, writable);
            var firstTask = service.InitializeAsync(CancellationToken.None);
            yield return Complete(firstTask);
            var first = firstTask.GetAwaiter().GetResult();
            Assert.That(first.CopiedSeed, Is.True);
            Assert.That(first.Destinations, Is.EqualTo(12));
            Assert.That(first.Users, Is.Zero);
            Assert.That(first.WorkerThreadId, Is.Not.EqualTo(mainThread));
            var write = service.ExecuteAsync(connection => { AddUserAndTrip(connection); return true; }, CancellationToken.None);
            yield return Complete(write);
            write.GetAwaiter().GetResult();
            byte[] before = Hash(writable);
            // Existing installations do not depend on the shipped seed still being present.
            var reopen = new TravelDatabase(Path.Combine(folder, "missing-seed.db"), writable).InitializeAsync(CancellationToken.None);
            yield return Complete(reopen);
            var second = reopen.GetAwaiter().GetResult();
            Assert.That(second.CopiedSeed, Is.False);
            Assert.That(second.Users, Is.EqualTo(1));
            Assert.That(second.Trips, Is.EqualTo(1));
            Assert.That(Hash(writable), Is.EqualTo(before), "Startup must not rewrite an existing database.");
        }

        [UnityTest]
        public IEnumerator ConcurrentFirstLaunchCopiesOnlyOnce()
        {
            var pending = Task.WhenAll(new TravelDatabase(seed, writable).InitializeAsync(CancellationToken.None),
                new TravelDatabase(seed, writable).InitializeAsync(CancellationToken.None));
            yield return Complete(pending);
            Assert.That(pending.GetAwaiter().GetResult().Count(result => result.CopiedSeed), Is.EqualTo(1));
            Assert.That(Directory.GetFiles(folder, "*.tmp"), Is.Empty);
        }

        [UnityTest]
        public IEnumerator MissingSeedFailsWithoutCreatingTarget()
        {
            var pending = new TravelDatabase(Path.Combine(folder, "absent.db"), writable).InitializeAsync(CancellationToken.None);
            yield return Complete(pending);
            Assert.That(pending.Exception?.GetBaseException(), Is.InstanceOf<FileNotFoundException>());
            Assert.That(File.Exists(writable), Is.False);
        }

        [UnityTest]
        public IEnumerator CorruptSeedNeverBecomesWritableDatabase()
        {
            string corrupt = Path.Combine(folder, "bad-seed.db");
            File.WriteAllBytes(corrupt, Enumerable.Repeat((byte)171, 1024).ToArray());
            var pending = new TravelDatabase(corrupt, writable).InitializeAsync(CancellationToken.None);
            yield return Complete(pending);
            Assert.That(pending.IsFaulted, Is.True);
            Assert.That(File.Exists(writable), Is.False);
            Assert.That(Directory.GetFiles(folder, "*.tmp"), Is.Empty);
        }

        [UnityTest]
        public IEnumerator CorruptExistingDatabaseIsPreserved()
        {
            File.WriteAllBytes(writable, Enumerable.Repeat((byte)171, 1024).ToArray());
            byte[] original = Hash(writable);
            var pending = new TravelDatabase(seed, writable).InitializeAsync(CancellationToken.None);
            yield return Complete(pending);
            Assert.That(pending.IsFaulted, Is.True);
            Assert.That(Hash(writable), Is.EqualTo(original));
        }

        [UnityTest]
        public IEnumerator NewerSchemaIsRejectedWithoutChanges()
        {
            File.Copy(seed, writable);
            using (var connection = new SQLiteConnection(writable)) connection.Execute("PRAGMA user_version = 999");
            byte[] original = Hash(writable);
            var pending = new TravelDatabase(seed, writable).InitializeAsync(CancellationToken.None);
            yield return Complete(pending);
            Assert.That(pending.IsFaulted, Is.True);
            Assert.That(Hash(writable), Is.EqualTo(original));
        }

        [Test]
        public void SeedHasAllDestinationsAndValidRelationships()
        {
            using (var connection = new SQLiteConnection(seed, SQLiteOpenFlags.ReadOnly))
            {
                connection.Execute("PRAGMA foreign_keys = ON");
                TravelDatabaseSchema.Validate(connection);
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM destinations"), Is.EqualTo(12));
                foreach (string table in new[] { "hotels", "restaurants", "experiences", "hotspots" })
                {
                    // Table names here are trusted constants, not user input.
                    Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(DISTINCT destination_id) FROM " + table), Is.EqualTo(12));
                    Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM " + table), Is.GreaterThanOrEqualTo(36));
                }
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM reviews WHERE is_demo <> 1"), Is.Zero);
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM flights WHERE departure_local_date NOT BETWEEN ? AND ?",
                    "2027-06-01", "2027-06-30"), Is.Zero);
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM flights WHERE departure_local_date = ?", "2027-06-15"), Is.GreaterThan(0));
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM flights WHERE departure_local_date = ?", "2027-06-22"), Is.GreaterThan(0));
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM users"), Is.Zero);
            }
        }

        [Test]
        public void DatabaseConstraintsRejectBadPricesRatingsAndBrokenReferences()
        {
            File.Copy(seed, writable);
            using (var connection = new SQLiteConnection(writable))
            {
                connection.Execute("PRAGMA foreign_keys = ON");
                string hotel = connection.ExecuteScalar<string>("SELECT id FROM hotels LIMIT 1");
                Assert.Throws<SQLiteException>(() => connection.Execute("UPDATE hotels SET price_cents = ? WHERE id = ?", -1, hotel));
                Assert.Throws<SQLiteException>(() => connection.Execute("UPDATE hotels SET destination_id = ? WHERE id = ?", "missing-city", hotel));
                string review = connection.ExecuteScalar<string>("SELECT id FROM reviews LIMIT 1");
                Assert.Throws<SQLiteException>(() => connection.Execute("UPDATE reviews SET rating = ? WHERE id = ?", 6, review));
                AddUserAndTrip(connection);
                Assert.Throws<SQLiteException>(() => connection.Execute(
                    "INSERT INTO saved_items (id,user_id,trip_id,created_utc) VALUES (?,?,?,?)",
                    "invalid", "test-user", "test-trip", "2027-01-01T00:00:00Z"));
                connection.Execute("INSERT INTO saved_items (id,user_id,trip_id,hotel_id,created_utc) VALUES (?,?,?,?,?)",
                    "saved-one", "test-user", "test-trip", hotel, "2027-01-01T00:00:00Z");
                Assert.Throws<SQLiteException>(() => connection.Execute(
                    "INSERT INTO saved_items (id,user_id,trip_id,hotel_id,created_utc) VALUES (?,?,?,?,?)",
                    "saved-duplicate", "test-user", "test-trip", hotel, "2027-01-01T00:00:00Z"));
            }
        }

        [Test]
        public void OwnershipAndNotificationTargetsAreEnforced()
        {
            File.Copy(seed, writable);
            using (var connection = new SQLiteConnection(writable))
            {
                connection.Execute("PRAGMA foreign_keys = ON");
                AddUserAndTrip(connection);
                string hotel = connection.ExecuteScalar<string>("SELECT id FROM hotels LIMIT 1");
                string otherHotel = connection.ExecuteScalar<string>("SELECT id FROM hotels WHERE id<>? LIMIT 1", hotel);
                const string now = "2027-01-01T00:00:00Z";
                Assert.Throws<SQLiteException>(() => connection.Execute(
                    "INSERT INTO saved_items (id,user_id,trip_id,hotel_id,created_utc) VALUES (?,?,?,?,?)",
                    "wrong-owner", "other-user", "test-trip", hotel, now));
                connection.Execute("INSERT INTO price_watches (id,user_id,hotel_id,last_price_cents,created_utc) VALUES (?,?,?,?,?)",
                    "watch", "test-user", hotel, 10000, now);
                connection.Execute("INSERT INTO price_history (id,hotel_id,old_price_cents,new_price_cents,changed_utc) VALUES (?,?,?,?,?)",
                    "wrong-history", otherHotel, 10000, 9000, now);
                Assert.Throws<SQLiteException>(() => connection.Execute(
                    "INSERT INTO notifications (id,user_id,watch_id,price_history_id,title,body,created_utc) VALUES (?,?,?,?,?,?,?)",
                    "wrong-notice", "test-user", "watch", "wrong-history", "Price changed", "Demo", now));
                connection.Execute("INSERT INTO price_history (id,hotel_id,old_price_cents,new_price_cents,changed_utc) VALUES (?,?,?,?,?)",
                    "history", hotel, 10000, 9000, now);
                connection.Execute("INSERT INTO notifications (id,user_id,watch_id,price_history_id,title,body,created_utc) VALUES (?,?,?,?,?,?,?)",
                    "notice", "test-user", "watch", "history", "Price changed", "Demo", now);
                Assert.Throws<SQLiteException>(() => connection.Execute("UPDATE price_watches SET hotel_id=? WHERE id=?", otherHotel, "watch"));
                connection.Execute("DELETE FROM users WHERE id=?", "test-user");
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM notifications"), Is.Zero);
                Assert.That(connection.ExecuteScalar<int>("SELECT COUNT(*) FROM trips"), Is.Zero);
            }
        }

        [Test]
        public void MissingSearchIndexIsRejected()
        {
            File.Copy(seed, writable);
            using (var connection = new SQLiteConnection(writable))
            {
                connection.Execute("DROP INDEX flights_search");
                Assert.Throws<InvalidOperationException>(() => TravelDatabaseSchema.Validate(connection));
            }
        }

        [Test]
        public void CancelledStartupDoesNotCopySeed()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var pending = new TravelDatabase(seed, writable).InitializeAsync(cancellation.Token);
                Assert.That(pending.IsCanceled, Is.True);
                Assert.That(File.Exists(writable), Is.False);
            }
        }

        private static void AddUserAndTrip(SQLiteConnection connection)
        {
            connection.RunInTransaction(() =>
            {
                connection.Execute("INSERT INTO users (id,email_normalized,password_hash,password_salt,password_iterations,created_utc) VALUES (?,?,?,?,?,?)",
                    "test-user", "test@example.invalid", Convert.ToBase64String(new byte[32]), Convert.ToBase64String(new byte[16]), 600000, "2027-01-01T00:00:00Z");
                connection.Execute("INSERT INTO trips (id,user_id,name,start_date,end_date,created_utc,updated_utc) VALUES (?,?,?,?,?,?,?)",
                    "test-trip", "test-user", "Trip", "2027-06-15", "2027-06-22", "2027-01-01T00:00:00Z", "2027-01-01T00:00:00Z");
                connection.Execute("UPDATE trips SET name = ? WHERE id = ?", "Edited trip", "test-trip");
            });
        }

        private static byte[] Hash(string path)
        {
            using (var stream = File.OpenRead(path)) using (var algorithm = SHA256.Create()) return algorithm.ComputeHash(stream);
        }
        private static IEnumerator Complete(Task task)
        {
            float end = Time.realtimeSinceStartup + 20;
            while (!task.IsCompleted && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(task.IsCompleted, Is.True, "Database worker exceeded 20 seconds.");
        }
    }
}

```

## Assets/TravelPlanning/Tests/EditMode/TravelSeedBuilderTests.cs

```csharp
using System;
using System.IO;
using NUnit.Framework;
using TravelPlanning.Data;
using TravelPlanning.UI.Editor;
using UnityEngine;

namespace TravelPlanning.Tests
{
    public sealed class TravelSeedBuilderTests
    {
        [Test]
        public void InvalidCatalogCannotReplaceExistingSeed()
        {
            string folder = Path.Combine(Path.GetTempPath(), "TravelSeedTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string source = Path.Combine(folder, "invalid.json");
                string output = Path.Combine(folder, "existing.db");
                File.WriteAllText(source, "{}");
                File.WriteAllText(output, "Preserve this existing output.");
                Assert.Throws<InvalidDataException>(() => TravelSeedBuilder.BuildFromJson(source, output));
                Assert.That(File.ReadAllText(output), Is.EqualTo("Preserve this existing output."));
                Assert.That(Directory.GetFiles(folder, "*.staging-*"), Is.Empty);
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void ImporterRejectsDuplicatesUnknownReferencesBadDatesAndNegativePrices()
        {
            var catalog = Load();
            catalog.destinations[1].id = catalog.destinations[0].id;
            Assert.Throws<InvalidDataException>(() => TravelSeedBuilder.ValidateData(catalog));
            catalog = Load();
            catalog.flights[0].originAirportId = "XXX";
            Assert.Throws<InvalidDataException>(() => TravelSeedBuilder.ValidateData(catalog));
            catalog = Load();
            catalog.flights[0].departureUtc = "not-a-date";
            Assert.Throws<InvalidDataException>(() => TravelSeedBuilder.ValidateData(catalog));
            catalog = Load();
            catalog.places[0].priceCents = -1;
            Assert.Throws<InvalidDataException>(() => TravelSeedBuilder.ValidateData(catalog));
        }

        [Test]
        public void ImporterAcceptsAnotherDestinationWithoutCodeChanges()
        {
            var catalog = Load();
            var destinations = catalog.destinations;
            Array.Resize(ref destinations, destinations.Length + 1);
            // Clone the record through JSON to use the actual DTO without a second test model.
            destinations[destinations.Length - 1] = JsonUtility.FromJson<SeedDestination>(JsonUtility.ToJson(destinations[0]));
            destinations[destinations.Length - 1].id = "extra-destination";
            destinations[destinations.Length - 1].name = "Additional destination";
            catalog.destinations = destinations;
            Assert.DoesNotThrow(() => TravelSeedBuilder.ValidateData(catalog));
        }

        private static TravelSeedData Load() => JsonUtility.FromJson<TravelSeedData>(File.ReadAllText(TravelSeedBuilder.JsonPath));
    }
}

```

## tools/Test-TravelDatabase.ps1

```powershell
param([string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Builds\TravelDatabase'))
$ErrorActionPreference = 'Stop'
$buildRoot = (Resolve-Path -LiteralPath $BuildDirectory).Path
$exe = Join-Path $buildRoot 'TravelPlannerDatabase.exe'
if (!(Test-Path -LiteralPath $exe)) { throw "Build not found: $exe" }
$logRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Logs\TravelDatabase'))
[IO.Directory]::CreateDirectory($logRoot) | Out-Null
$runId = [Guid]::NewGuid().ToString('N')
$fileName = "travel-smoke-$runId.db"

function Invoke-Database([string]$Name, [string]$FileName, [bool]$ExpectedSuccess, [int]$Copied, [bool]$WriteMarker) {
    $log = Join-Path $logRoot "$Name-$runId.log"
    $arguments = @('-batchmode', '-nographics', '-travelSeedSmoke', '-travelSeedFile', $FileName,
        '-travelSeedExpectedCopied', $Copied, '-travelSeedExpectedUsers', '1', '-travelSeedExpectedTrips', '1',
        '-logFile', ('"' + $log + '"'))
    if ($WriteMarker) { $arguments += '-travelSeedWriteMarker' }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(60000)) { Stop-Process -Id $process.Id; throw "Player timed out: $log" }
    $process.Refresh()
    $text = Get-Content -LiteralPath $log -Raw
    $marker = if ($ExpectedSuccess) { 'TRAVEL_DATABASE_PASS' } else { 'TRAVEL_DATABASE_FAIL' }
    $exitCode = if ($ExpectedSuccess) { 0 } else { 1 }
    if ($process.ExitCode -ne $exitCode -or !$text.Contains($marker)) { throw "Unexpected $Name result; see $log" }
    Write-Host "PASS $Name : $log"
    return $text
}

$first = Invoke-Database 'first-copy' $fileName $true 1 $true
$path = [regex]::Match($first, 'path=([^\r\n]+)').Groups[1].Value
if (!$path -or [IO.Path]::GetFileName($path) -ne $fileName) { throw 'Unexpected writable database path.' }
$originalHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
$null = Invoke-Database 'preserve-user-trip' $fileName $true 0 $false
if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $originalHash) { throw 'Second launch rewrote the database.' }

$corruptName = "travel-corrupt-$runId.db"
$corruptPath = Join-Path ([IO.Path]::GetDirectoryName($path)) $corruptName
$bytes = New-Object byte[] 1024
for ($index = 0; $index -lt $bytes.Length; $index++) { $bytes[$index] = 171 }
[IO.File]::WriteAllBytes($corruptPath, $bytes)
$corruptHash = (Get-FileHash -LiteralPath $corruptPath -Algorithm SHA256).Hash
$null = Invoke-Database 'corrupt-existing' $corruptName $false 0 $false
if ((Get-FileHash -LiteralPath $corruptPath -Algorithm SHA256).Hash -ne $corruptHash) { throw 'Corrupt file was replaced.' }

# Test this build's seed packaging only. Preserve the source seed and every existing writable DB.
$seedPath = Join-Path $buildRoot 'TravelPlannerDatabase_Data\StreamingAssets\Database\travel_seed.db'
$disabledPath = $seedPath + '.test-disabled'
if (!(Test-Path -LiteralPath $seedPath) -or (Test-Path -LiteralPath $disabledPath)) { throw 'Unexpected seed file state.' }
if (!$seedPath.StartsWith($buildRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Seed is outside build.' }
Move-Item -LiteralPath $seedPath -Destination $disabledPath
try {
    $null = Invoke-Database 'missing-seed' "travel-missing-$runId.db" $false 1 $false
    $null = Invoke-Database 'existing-without-seed' $fileName $true 0 $false
}
finally { Move-Item -LiteralPath $disabledPath -Destination $seedPath }
Write-Host "All five player checks passed. Test user/trip database retained: $path"

```
