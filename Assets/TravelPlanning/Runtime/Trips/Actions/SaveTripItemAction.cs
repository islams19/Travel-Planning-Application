using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TravelPlanning.Data;

namespace TravelPlanning.Trips
{
    /// <summary>One saved-trip operation, executed through the shared database parent.</summary>
    internal sealed class SaveTripItemAction : UserDatabaseActionBase
    {
        private readonly TripRepository repository;
        internal SaveTripItemAction(
            TravelDatabase database,
            string userId,
            TripRepository repository) : base(database, userId, "Sign in before opening saved trips.")
        {
            this.repository = repository;
        }

#region Run action
        internal Task<bool> ExecuteAsync(string tripId, SavedItemKind kind, string targetId, CancellationToken token)
        {
            TripCatalog.Target(kind, out _, out _, out _);
            return base.ExecuteAsync(connection =>
            {
                bool added = false;
                connection.RunInTransaction(() =>
                {
                    repository.RequireTrip(connection, tripId);
                    repository.RequireTarget(connection, kind, targetId);
                    if (repository.ContainsTarget(connection, tripId, kind, targetId))
                    {
                        token.ThrowIfCancellationRequested();
                        return;
                    }

                    token.ThrowIfCancellationRequested();
                    repository.InsertTarget(connection, tripId, kind, targetId, TripValidation.Timestamp());
                    token.ThrowIfCancellationRequested();
                    added = true;
                });
                return added;
            }, token);
        }
#endregion
    }
}
