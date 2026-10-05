using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TravelPlanning.Data;

namespace TravelPlanning.Tracking
{
    internal sealed class TrackPriceAction : UserDatabaseActionBase
    {
        private readonly TrackingRepository repository;
        internal TrackPriceAction(
            TravelDatabase database,
            string userId,
            TrackingRepository repository) : base(database, userId, "Sign in before tracking prices.")
        {
            this.repository = repository;
        }

#region Start or resume tracking
        internal Task<bool> ExecuteAsync(PriceTargetKind kind, string targetId, CancellationToken token)
        {
            TrackingCatalog.Target(kind, out _, out _, out _);
            return base.ExecuteAsync(connection =>
            {
                bool changed = false;
                connection.RunInTransaction(() =>
                {
                    RequireUser(connection);
                    var target = TrackingCatalog.ReadTarget(connection, kind, targetId);
                    var watch = repository.FindWatch(connection, kind, targetId);
                    token.ThrowIfCancellationRequested();
                    if (watch != null && watch.IsActive)
                    {
                        return;
                    }

                    if (watch == null)
                    {
                        repository.CreateWatch(connection, kind, targetId, target.CurrentPriceCents);
                    }
                    else
                    {
                        repository.Reactivate(connection, watch.Id, target.CurrentPriceCents);
                    }

                    token.ThrowIfCancellationRequested();
                    changed = true;
                });
                // Returning after commit must not turn a completed write into a cancellation error.
                return changed;
            }, token);
        }
#endregion
    }
}
