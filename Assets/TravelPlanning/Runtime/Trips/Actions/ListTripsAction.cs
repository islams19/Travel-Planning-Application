using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TravelPlanning.Data;

namespace TravelPlanning.Trips
{
    /// <summary>One saved-trip operation, executed through the shared database parent.</summary>
    internal sealed class ListTripsAction : UserDatabaseActionBase
    {
        private readonly TripRepository repository;
        internal ListTripsAction(
            TravelDatabase database,
            string userId,
            TripRepository repository) : base(database, userId, "Sign in before opening saved trips.")
        {
            this.repository = repository;
        }

#region Run action
        internal Task<TripListResult> ExecuteAsync(CancellationToken token)
        {
            var elapsed = Stopwatch.StartNew();
            return base.ExecuteAsync(connection =>
            {
                RequireUser(connection);
                repository.CatalogDates(connection, out string start, out string end);
                var trips = repository.List(connection);
                token.ThrowIfCancellationRequested();
                elapsed.Stop();
                return new TripListResult
                {
                    Trips = trips.AsReadOnly(),
                    StartDate = start,
                    EndDate = end,
                    ElapsedMilliseconds = elapsed.ElapsedMilliseconds,
                    WorkerThreadId = Thread.CurrentThread.ManagedThreadId
                };
            }, token);
        }
#endregion
    }
}
