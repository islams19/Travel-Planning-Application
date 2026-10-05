using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TravelPlanning.Data;

namespace TravelPlanning.Tracking
{
    internal sealed class GetTrackingTargetAction : UserDatabaseActionBase
    {
        private readonly TrackingRepository repository;
        internal GetTrackingTargetAction(
            TravelDatabase database,
            string userId,
            TrackingRepository repository) : base(database, userId, "Sign in before tracking prices.")
        {
            this.repository = repository;
        }

#region Load a fresh target preview
        internal Task<WatchOption> ExecuteAsync(PriceTargetKind kind, string targetId, CancellationToken token)
        {
            TrackingCatalog.Target(kind, out _, out _, out _);
            return base.ExecuteAsync(connection =>
            {
                RequireUser(connection);
                var target = TrackingCatalog.ReadTarget(connection, kind, targetId);
                var watch = repository.FindWatch(connection, kind, targetId);
                if (watch != null)
                {
                    target.Id = watch.Id;
                    target.LastPriceCents = watch.LastPriceCents;
                    target.IsActive = watch.IsActive;
                }

                token.ThrowIfCancellationRequested();
                return target;
            }, token);
        }
#endregion
    }
}
