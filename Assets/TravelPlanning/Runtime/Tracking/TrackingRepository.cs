using System;
using System.Collections.Generic;
using System.Linq;
using SQLite;

namespace TravelPlanning.Tracking
{
    /// <summary>Stores account-scoped watch and notification SQL in one place.</summary>
    internal sealed class TrackingRepository
    {
        private readonly string userId;
        internal TrackingRepository(string userId)
        {
            this.userId = userId;
        }

#region Read watches and notices
        internal List<WatchOption> ReadWatches(SQLiteConnection connection)
        {
            var watches = TrackingCatalog.ReadWatches(connection, userId, PriceTargetKind.Flight);
            watches.AddRange(TrackingCatalog.ReadWatches(connection, userId, PriceTargetKind.Hotel));
            return watches
                .OrderByDescending(watch => watch.IsActive)
                .ThenBy(watch => watch.Title, StringComparer.Ordinal)
                .ThenBy(watch => watch.Id, StringComparer.Ordinal)
                .ToList();
        }

        internal List<PriceNotice> ReadNotices(SQLiteConnection connection)
        {
            return connection.Query<PriceNotice>(
                "SELECT n.id AS Id, n.watch_id AS WatchId, n.price_history_id AS HistoryId, n.title AS Title, " +
                "n.body AS Body, n.created_utc AS CreatedUtc, n.is_read AS IsRead, h.old_price_cents AS " +
                "OldPriceCents, h.new_price_cents AS NewPriceCents " +
                "FROM notifications n JOIN price_history h ON h.id=n.price_history_id WHERE n.user_id=? ORDER BY " +
                "n.created_utc DESC, n.id DESC",
                userId);
        }

        internal int UnreadCount(SQLiteConnection connection)
        {
            return connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM notifications WHERE user_id=? AND is_read=0",
                userId);
        }

        internal TrackingWatchRow FindWatch(SQLiteConnection connection, PriceTargetKind kind, string targetId)
        {
            TrackingCatalog.Target(kind, out _, out string column, out _);
            return connection.FindWithQuery<TrackingWatchRow>(
                "SELECT id AS Id, last_price_cents AS LastPriceCents, active AS IsActive FROM price_watches WHERE user_id=? AND " +
                column +
                "=?",
                userId,
                targetId);
        }

#endregion
#region Update watches
        internal void CreateWatch(SQLiteConnection connection, PriceTargetKind kind, string targetId, int price)
        {
            TrackingCatalog.Target(kind, out _, out string column, out _);
            connection.Execute(
                "INSERT INTO price_watches (id,user_id," +
                column +
                ",last_price_cents,active,created_utc) VALUES (?,?,?,?,?,?)",
                Guid.NewGuid().ToString("N"),
                userId,
                targetId,
                price,
                1,
                TrackingCatalog.Timestamp());
        }

        internal void Reactivate(SQLiteConnection connection, string watchId, int price)
        {
            connection.Execute(
                "UPDATE price_watches SET active=1,last_price_cents=? WHERE id=? AND user_id=?",
                price,
                watchId,
                userId);
        }

        internal bool Stop(SQLiteConnection connection, string watchId)
        {
            return connection.Execute(
                "UPDATE price_watches SET active=0 WHERE id=? AND user_id=? AND active=1",
                watchId,
                userId) > 0;
        }

#endregion
#region Mark notices read
        internal bool MarkRead(SQLiteConnection connection, string notificationId)
        {
            return connection.Execute(
                "UPDATE notifications SET is_read=1 WHERE id=? AND user_id=? AND is_read=0",
                notificationId,
                userId) > 0;
        }

        internal int MarkAllRead(SQLiteConnection connection)
        {
            return connection.Execute("UPDATE notifications SET is_read=1 WHERE user_id=? AND is_read=0", userId);
        }
#endregion
    }
}
