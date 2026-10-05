using System;
using System.Collections.Generic;
using System.Globalization;
using SQLite;

namespace TravelPlanning.Tracking
{
    /// <summary>Only these two fixed catalog tables can be tracked or changed by the demo.</summary>
    internal static class TrackingCatalog
    {
#region Target
        internal static void Target(PriceTargetKind kind, out string table, out string column, out string unit)
        {
            switch (kind)
            {
                case PriceTargetKind.Flight:
                    table = "flights";
                    column = "flight_id";
                    unit = "/ flight";
                    break;
                case PriceTargetKind.Hotel:
                    table = "hotels";
                    column = "hotel_id";
                    unit = "/ room / night";
                    break;
                default:
                    throw new ArgumentException("Choose a flight or hotel.", nameof(kind));
            }
        }
#endregion

#region Read Target
        internal static WatchOption ReadTarget(SQLiteConnection connection, PriceTargetKind kind, string targetId)
        {
            Target(kind, out _, out _, out string unit);
            if (string.IsNullOrWhiteSpace(targetId))
                throw new ArgumentException("Choose an available flight or hotel.", nameof(targetId));
            var target = connection.FindWithQuery<WatchOption>("SELECT " + Columns(kind) + From(kind) + " WHERE p.id=?", targetId);
            if (target == null)
                throw new ArgumentException("Choose an available flight or hotel.", nameof(targetId));
            target.Kind = kind;
            target.PriceUnit = unit;
            target.LastPriceCents = target.CurrentPriceCents;
            return target;
        }
#endregion

#region Read Watches
        internal static List<WatchOption> ReadWatches(SQLiteConnection connection, string owner, PriceTargetKind kind)
        {
            Target(kind, out _, out string column, out string unit);
            var watches = connection.Query<WatchOption>(
                "SELECT " +
                Columns(kind) +
                ", w.id AS Id, w.last_price_cents AS LastPriceCents, w.active AS IsActive" +
                From(kind) +
                " JOIN price_watches w ON w." +
                column +
                "=p.id WHERE w.user_id=?",
                owner);
            foreach (var watch in watches)
            {
                watch.Kind = kind;
                watch.PriceUnit = unit;
            }

            return watches;
        }
#endregion

#region Columns
        private static string Columns(
            PriceTargetKind kind) => "p.id AS TargetId, p.price_cents AS CurrentPriceCents, p.currency AS Currency, " +
            (kind == PriceTargetKind.Flight
            ? "a.name || ' ' || p.flight_number || ' | ' || p.origin_airport_id || ' to ' || " +
            "p.destination_airport_id || ' | ' || p.departure_local_date AS Title"
            : "p.name || ' | ' || d.name AS Title");
#endregion
#region From
        private static string From(PriceTargetKind kind) => kind == PriceTargetKind.Flight
            ? " FROM flights p JOIN airlines a ON a.id=p.airline_id"
            : " FROM hotels p JOIN destinations d ON d.id=p.destination_id";
#endregion
#region Timestamp
        internal static string Timestamp(
            ) => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
#endregion
#region Money
        internal static string Money(int cents) => "USD " +
            (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
#endregion
    }
}
