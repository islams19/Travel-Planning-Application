using System;
using System.Threading;
using System.Threading.Tasks;
using TravelPlanning.Data;

namespace TravelPlanning.Tracking
{
    /// <summary>Changes the shared offline catalog and alerts all active watchers atomically.</summary>
    public sealed class DemoPriceService : DatabaseServiceBase
    {
        public DemoPriceService(TravelDatabase database) : base(database)
        {
        }

#region Change Price Async
        public Task<bool> ChangePriceAsync(
            PriceTargetKind kind,
            string targetId,
            int newPriceCents,
            CancellationToken cancellationToken = default)
        {
            TrackingCatalog.Target(kind, out string table, out string column, out string unit);
            if (newPriceCents < 0)
                throw new ArgumentException("The price cannot be negative.", nameof(newPriceCents));
            return base.ExecuteAsync(connection =>
            {
                bool changed = false;
                connection.RunInTransaction(() =>
                {
                    var target = TrackingCatalog.ReadTarget(connection, kind, targetId);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (target.CurrentPriceCents == newPriceCents)
                        return;
                    string historyId = Guid.NewGuid().ToString("N");
                    string now = TrackingCatalog.Timestamp();
                    connection.Execute("UPDATE " + table + " SET price_cents=? WHERE id=?", newPriceCents, targetId);
                    connection.Execute(
                        "INSERT INTO price_history (id," +
                        column +
                        ",old_price_cents,new_price_cents,changed_utc) VALUES (?,?,?,?,?)",
                        historyId,
                        targetId,
                        target.CurrentPriceCents,
                        newPriceCents,
                        now);
                    var watchers = connection.Query<TrackingWatchRow>(
                        "SELECT id AS Id,user_id AS UserId FROM price_watches WHERE " +
                        column +
                        "=? AND active=1",
                        targetId);
                    string direction = newPriceCents < target.CurrentPriceCents ? "decreased" : "increased";
                    string title = "Price " + direction + ": " + target.Title;
                    string body = target.Title +
                        " " +
                        direction +
                        " from " +
                        TrackingCatalog.Money(target.CurrentPriceCents) +
                        " to " +
                        TrackingCatalog.Money(newPriceCents) +
                        " " +
                        unit +
                        ". Offline demo price update.";
                    foreach (var watcher in watchers)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        connection.Execute(
                            "INSERT INTO notifications (id,user_id,watch_id,price_history_id,title,body,created_utc,is_read) " +
                            "VALUES (?,?,?,?,?,?,?,?)",
                            Guid.NewGuid().ToString("N"),
                            watcher.UserId,
                            watcher.Id,
                            historyId,
                            title,
                            body,
                            now,
                            0);
                        connection.Execute(
                            "UPDATE price_watches SET last_price_cents=? WHERE id=? AND user_id=? AND active=1",
                            newPriceCents,
                            watcher.Id,
                            watcher.UserId);
                    }

                    // Failure/cancellation rolls price, history, notices and baselines back together.
                    cancellationToken.ThrowIfCancellationRequested();
                    changed = true;
                });
                return changed;
            }, cancellationToken);
        }
#endregion
    }
}
