using System.Threading;
using System.Threading.Tasks;
using TravelPlanning.Data;

namespace TravelPlanning.Trips
{
    /// <summary>The UI-facing facade delegates each trip operation to a focused action.</summary>
    public sealed class TripService
    {
        private readonly ListTripsAction list;
        private readonly LoadTripAction load;
        private readonly CreateTripAction create;
        private readonly SaveTripItemAction save;
        private readonly RemoveSavedItemAction remove;
        public TripService(TravelDatabase database, string userId)
        {
            var repository = new TripRepository(userId);
            list = new ListTripsAction(database, userId, repository);
            load = new LoadTripAction(database, userId, repository);
            create = new CreateTripAction(database, userId, repository);
            save = new SaveTripItemAction(database, userId, repository);
            remove = new RemoveSavedItemAction(database, userId, repository);
        }

#region Browse trips
        public Task<TripListResult> ListAsync(CancellationToken cancellationToken = default)
        {
            return list.ExecuteAsync(cancellationToken);
        }

        public Task<TripDetails> LoadAsync(string tripId, CancellationToken cancellationToken = default)
        {
            return load.ExecuteAsync(tripId, cancellationToken);
        }

#endregion
#region Create a trip
        public Task<TripSummary> CreateAsync(
            string name,
            string startDate,
            string endDate,
            CancellationToken cancellationToken = default)
        {
            return create.ExecuteAsync(name, startDate, endDate, cancellationToken);
        }

#endregion
#region Save or remove an item
        public Task<bool> SaveAsync(
            string tripId,
            SavedItemKind kind,
            string targetId,
            CancellationToken cancellationToken = default)
        {
            return save.ExecuteAsync(tripId, kind, targetId, cancellationToken);
        }

        public Task<bool> RemoveAsync(string tripId, string savedItemId, CancellationToken cancellationToken = default)
        {
            return remove.ExecuteAsync(tripId, savedItemId, cancellationToken);
        }
#endregion
    }
}
