using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TravelPlanning.Data;

namespace TravelPlanning.Trips
{
    /// <summary>One saved-trip operation, executed through the shared database parent.</summary>
    internal sealed class RemoveSavedItemAction : UserDatabaseActionBase
    {
        private readonly TripRepository repository;
        internal RemoveSavedItemAction(
            TravelDatabase database,
            string userId,
            TripRepository repository) : base(database, userId, "Sign in before opening saved trips.")
        {
            this.repository = repository;
        }

#region Run action
        internal Task<bool> ExecuteAsync(string tripId, string savedItemId, CancellationToken token)
        {
            return base.ExecuteAsync(connection =>
            {
                bool removed = false;
                connection.RunInTransaction(() =>
                {
                    repository.RequireTrip(connection, tripId);
                    token.ThrowIfCancellationRequested();
                    removed = repository.RemoveTarget(connection, tripId, savedItemId);
                    if (removed)
                    {
                        repository.Touch(connection, tripId, TripValidation.Timestamp());
                    }

                    token.ThrowIfCancellationRequested();
                });
                return removed;
            }, token);
        }
#endregion
    }
}
