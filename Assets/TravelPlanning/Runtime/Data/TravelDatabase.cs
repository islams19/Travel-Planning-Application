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

#region Initialize Async
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
#endregion

#region Execute Async
        // Repositories supply SQL operations here. Their delegates must not use any Unity API.
        public Task<T> ExecuteAsync<T>(Func<SQLiteConnection, T> operation, CancellationToken cancellationToken)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            return OnWorkerAsync(() =>
            {
                if (!initialized)
                    throw new InvalidOperationException("Initialize the travel database before using it.");
                using (var connection = Open(databasePath, SQLiteOpenFlags.ReadWrite))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return operation(connection);
                }
            }, cancellationToken);
        }
#endregion

#region On Worker Async
        private static async Task<T> OnWorkerAsync<T>(Func<T> operation, CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await Task.Run(operation, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }
#endregion

#region Copy Seed If Missing
        private bool CopySeedIfMissing(CancellationToken cancellationToken)
        {
            if (File.Exists(databasePath))
                return false; // Never replace an existing user's database.
            if (!File.Exists(seedPath))
                throw new FileNotFoundException("The bundled travel seed database is missing.", seedPath);
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath));
            string temporary = databasePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Copy(seedPath, temporary, false);
                using (var copy = Open(temporary, SQLiteOpenFlags.ReadOnly))
                    TravelDatabaseSchema.Validate(copy);
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    File.Move(temporary, databasePath);
                }
                catch (IOException)when (File.Exists(databasePath))
                {
                    // Another process won the first-launch race. Validate its file in the caller.
                    return false;
                }

                return true;
            }
            finally
            {
                // Only our UUID temporary file is eligible for cleanup, never the user's database.
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
#endregion

#region Open
        private static SQLiteConnection Open(string path, SQLiteOpenFlags flags)
        {
            var connection = new SQLiteConnection(path, flags);
            try
            {
                connection.BusyTimeout = TimeSpan.FromSeconds(3);
                connection.Execute("PRAGMA foreign_keys = ON");
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }
#endregion

#region Read Status
        private TravelDatabaseStatus ReadStatus(SQLiteConnection connection, bool copied)
        {
            return new TravelDatabaseStatus
            {
                DatabasePath = databasePath,
                CopiedSeed = copied,
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
#endregion
    }
}
