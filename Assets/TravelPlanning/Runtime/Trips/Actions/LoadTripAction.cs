using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TravelPlanning.Data;

namespace TravelPlanning.Trips
{
    /// <summary>One saved-trip operation, executed through the shared database parent.</summary>
    internal sealed class LoadTripAction : UserDatabaseActionBase
    {
        private readonly TripRepository repository;
        internal LoadTripAction(
            TravelDatabase database,
            string userId,
            TripRepository repository) : base(database, userId, "Sign in before opening saved trips.")
        {
            this.repository = repository;
        }

#region Run action
        internal Task<TripDetails> ExecuteAsync(string tripId, CancellationToken token)
        {
            var elapsed = Stopwatch.StartNew();
            return base.ExecuteAsync(connection =>
            {
                var trip = repository.RequireTrip(connection, tripId);
                var items = TripCatalog.ReadItems(connection, UserId, tripId, token);
                token.ThrowIfCancellationRequested();
                elapsed.Stop();
                return new TripDetails
                {
                    Trip = trip,
                    Items = items,
                    ElapsedMilliseconds = elapsed.ElapsedMilliseconds,
                    WorkerThreadId = Thread.CurrentThread.ManagedThreadId
                };
            }, token);
        }
#endregion
    }
}
