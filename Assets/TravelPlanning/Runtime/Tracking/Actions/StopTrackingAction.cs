using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TravelPlanning.Data;

namespace TravelPlanning.Tracking
{
    internal sealed class StopTrackingAction : UserDatabaseActionBase
    {
        private readonly TrackingRepository repository;
        internal StopTrackingAction(
            TravelDatabase database,
            string userId,
            TrackingRepository repository) : base(database, userId, "Sign in before tracking prices.")
        {
            this.repository = repository;
        }

#region Stop tracking without deleting notices
        internal Task<bool> ExecuteAsync(string watchId, CancellationToken token)
        {
            return base.ExecuteAsync(connection =>
            {
                bool changed = false;
                connection.RunInTransaction(() =>
                {
                    token.ThrowIfCancellationRequested();
                    changed = repository.Stop(connection, watchId);
                    token.ThrowIfCancellationRequested();
                });
                return changed;
            }, token);
        }
#endregion
    }
}
