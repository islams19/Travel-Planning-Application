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
#region Run Async
        public async Task<SqliteProofResult> RunAsync(string path, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A database file path is required.", nameof(path));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Opening, SQL, file access and disposal all happen on this worker.
                return await Task.Run(() => Run(path, cancellationToken), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }
#endregion

#region Run
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
                connection.Execute(
                    "CREATE TABLE IF NOT EXISTS proof_rows (" +
                    "id INTEGER PRIMARY KEY CHECK (id = 1), " +
                    "message TEXT NOT NULL, created_utc TEXT NOT NULL, " +
                    "visits INTEGER NOT NULL CHECK (visits >= 1))");
                bool existed = false;
                connection.RunInTransaction(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    existed = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM proof_rows WHERE id = ?", 1) == 1;
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
                        connection.Execute(
                            "INSERT INTO proof_rows (id, message, created_utc, visits) " +
                            "VALUES (?, ?, ?, ?)",
                            1,
                            SampleMessage,
                            DateTime.UtcNow.ToString("O"),
                            1);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                });
                string message = connection.ExecuteScalar<string>("SELECT message FROM proof_rows WHERE id = ?", 1);
                if (message != SampleMessage)
                    throw new InvalidDataException("The saved proof message is unexpected; the file was not reset.");
                return new SqliteProofResult
                {
                    DatabasePath = fullPath,
                    Message = message,
                    CreatedUtc = connection.ExecuteScalar<string>("SELECT created_utc FROM proof_rows WHERE id = ?", 1),
                    Visits = connection.ExecuteScalar<int>("SELECT visits FROM proof_rows WHERE id = ?", 1),
                    WasAlreadySaved = existed,
                    SqliteVersion = connection.ExecuteScalar<string>("SELECT sqlite_version()"),
                    WorkerThreadId = Thread.CurrentThread.ManagedThreadId,
                    ElapsedMilliseconds = timer.ElapsedMilliseconds
                };
            }
        }
#endregion
    }
}
