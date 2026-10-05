using System.Threading;
using System.Threading.Tasks;
using TravelPlanning.Data;

namespace TravelPlanning.Tracking
{
    /// <summary>The UI entry point delegates account-scoped operations to focused actions.</summary>
    public sealed class PriceTrackingService
    {
        private readonly LoadTrackingAction load;
        private readonly GetTrackingTargetAction target;
        private readonly TrackPriceAction track;
        private readonly StopTrackingAction stop;
        private readonly ReadNotificationsAction read;
        public PriceTrackingService(TravelDatabase database, string userId)
        {
            var repository = new TrackingRepository(userId);
            load = new LoadTrackingAction(database, userId, repository);
            target = new GetTrackingTargetAction(database, userId, repository);
            track = new TrackPriceAction(database, userId, repository);
            stop = new StopTrackingAction(database, userId, repository);
            read = new ReadNotificationsAction(database, userId, repository);
        }

#region Load tracking information
        public Task<TrackingSnapshot> LoadAsync(CancellationToken cancellationToken = default)
        {
            return load.ExecuteAsync(cancellationToken);
        }

        public Task<WatchOption> GetTargetAsync(
            PriceTargetKind kind,
            string targetId,
            CancellationToken cancellationToken = default)
        {
            return target.ExecuteAsync(kind, targetId, cancellationToken);
        }

        public Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
        {
            return read.GetUnreadCountAsync(cancellationToken);
        }

#endregion
#region Start or stop tracking
        public Task<bool> TrackAsync(
            PriceTargetKind kind,
            string targetId,
            CancellationToken cancellationToken = default)
        {
            return track.ExecuteAsync(kind, targetId, cancellationToken);
        }

        public Task<bool> StopAsync(string watchId, CancellationToken cancellationToken = default)
        {
            return stop.ExecuteAsync(watchId, cancellationToken);
        }

#endregion
#region Mark notifications read
        public Task<bool> MarkReadAsync(string notificationId, CancellationToken cancellationToken = default)
        {
            return read.MarkReadAsync(notificationId, cancellationToken);
        }

        public Task<int> MarkAllReadAsync(CancellationToken cancellationToken = default)
        {
            return read.MarkAllReadAsync(cancellationToken);
        }
#endregion
    }
}
