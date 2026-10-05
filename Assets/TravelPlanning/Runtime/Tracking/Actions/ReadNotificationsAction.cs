using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TravelPlanning.Data;

namespace TravelPlanning.Tracking
{
    internal sealed class ReadNotificationsAction : UserDatabaseActionBase
    {
        private readonly TrackingRepository repository;
        internal ReadNotificationsAction(
            TravelDatabase database,
            string userId,
            TrackingRepository repository) : base(database, userId, "Sign in before tracking prices.")
        {
            this.repository = repository;
        }

#region Read unread count
        internal Task<int> GetUnreadCountAsync(CancellationToken token)
        {
            return base.ExecuteAsync(connection =>
            {
                RequireUser(connection);
                int count = repository.UnreadCount(connection);
                token.ThrowIfCancellationRequested();
                return count;
            }, token);
        }

#endregion
#region Mark one notice read
        internal Task<bool> MarkReadAsync(string notificationId, CancellationToken token)
        {
            return base.ExecuteAsync(connection =>
            {
                bool changed = false;
                connection.RunInTransaction(() =>
                {
                    token.ThrowIfCancellationRequested();
                    changed = repository.MarkRead(connection, notificationId);
                    token.ThrowIfCancellationRequested();
                });
                return changed;
            }, token);
        }

#endregion
#region Mark all notices read
        internal Task<int> MarkAllReadAsync(CancellationToken token)
        {
            return base.ExecuteAsync(connection =>
            {
                int changed = 0;
                connection.RunInTransaction(() =>
                {
                    token.ThrowIfCancellationRequested();
                    changed = repository.MarkAllRead(connection);
                    token.ThrowIfCancellationRequested();
                });
                return changed;
            }, token);
        }
#endregion
    }
}
