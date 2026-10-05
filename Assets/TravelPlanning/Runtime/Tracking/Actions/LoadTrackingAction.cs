using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TravelPlanning.Data;

namespace TravelPlanning.Tracking
{
    internal sealed class LoadTrackingAction : UserDatabaseActionBase
    {
        private readonly TrackingRepository repository;
        internal LoadTrackingAction(
            TravelDatabase database,
            string userId,
            TrackingRepository repository) : base(database, userId, "Sign in before tracking prices.")
        {
            this.repository = repository;
        }

#region Load watches and notices
        internal Task<TrackingSnapshot> ExecuteAsync(CancellationToken token)
        {
            var elapsed = Stopwatch.StartNew();
            return base.ExecuteAsync(connection =>
            {
                RequireUser(connection);
                var watches = repository.ReadWatches(connection);
                var notices = repository.ReadNotices(connection);
                token.ThrowIfCancellationRequested();
                elapsed.Stop();
                return new TrackingSnapshot
                {
                    Watches = watches.AsReadOnly(),
                    Notifications = notices.AsReadOnly(),
                    UnreadCount = notices.Count(notice => !notice.IsRead),
                    ElapsedMilliseconds = elapsed.ElapsedMilliseconds,
                    WorkerThreadId = Thread.CurrentThread.ManagedThreadId
                };
            }, token);
        }
#endregion
    }
}
