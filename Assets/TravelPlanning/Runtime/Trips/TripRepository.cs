using System;
using System.Collections.Generic;
using SQLite;

namespace TravelPlanning.Trips
{
    /// <summary>Owns trip SQL. Every trip and saved-item query includes the immutable owner.</summary>
    internal sealed class TripRepository
    {
        private readonly string userId;
        private const string SummarySql = "SELECT t.id AS Id, t.name AS Name, t.start_date AS StartDate, t.end_date AS EndDate, " +
            "t.created_utc AS CreatedUtc, t.updated_utc AS UpdatedUtc, " +
            "(SELECT COUNT(*) FROM saved_items s WHERE s.trip_id=t.id AND s.user_id=t.user_id) AS ItemCount FROM trips t ";
        internal TripRepository(string userId)
        {
            this.userId = userId;
        }

#region Read trips
        internal List<TripSummary> List(SQLiteConnection connection)
        {
            return connection.Query<TripSummary>(
                SummarySql +
                "WHERE t.user_id=? ORDER BY t.created_utc DESC, t.id",
                userId);
        }

        internal TripSummary RequireTrip(SQLiteConnection connection, string tripId)
        {
            var trip = connection.FindWithQuery<TripSummary>(SummarySql + "WHERE t.id=? AND t.user_id=?", tripId, userId);
            if (trip == null)
                throw new ArgumentException("This trip is not available for your account.", nameof(tripId));
            return trip;
        }

        internal void CatalogDates(SQLiteConnection connection, out string first, out string last)
        {
            first = connection.ExecuteScalar<string>("SELECT value FROM metadata WHERE key=?", "seed_start_date");
            last = connection.ExecuteScalar<string>("SELECT value FROM metadata WHERE key=?", "seed_end_date");
            TripValidation.ValidateCatalogDates(first, last);
        }

#endregion
#region Write trips and saved references
        internal void Insert(SQLiteConnection connection, TripSummary trip)
        {
            connection.Execute(
                "INSERT INTO trips (id,user_id,name,start_date,end_date,created_utc,updated_utc) VALUES (?,?,?,?,?,?,?)",
                trip.Id,
                userId,
                trip.Name,
                trip.StartDate,
                trip.EndDate,
                trip.CreatedUtc,
                trip.UpdatedUtc);
        }

        internal void RequireTarget(SQLiteConnection connection, SavedItemKind kind, string targetId)
        {
            TripCatalog.Target(kind, out string table, out _, out _);
            if (string.IsNullOrWhiteSpace(targetId) ||
                connection.ExecuteScalar<int>("SELECT COUNT(*) FROM " + table + " WHERE id=?", targetId) != 1)
                throw new ArgumentException("Choose an item available in this category.", nameof(targetId));
        }

        internal bool ContainsTarget(SQLiteConnection connection, string tripId, SavedItemKind kind, string targetId)
        {
            TripCatalog.Target(kind, out _, out string column, out _);
            return connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM saved_items WHERE user_id=? AND trip_id=? AND " +
                column +
                "=?",
                userId,
                tripId,
                targetId) > 0;
        }

        internal void InsertTarget(
            SQLiteConnection connection,
            string tripId,
            SavedItemKind kind,
            string targetId,
            string now)
        {
            TripCatalog.Target(kind, out _, out string column, out _);
            connection.Execute(
                "INSERT INTO saved_items (id,user_id,trip_id," +
                column +
                ",created_utc) VALUES (?,?,?,?,?)",
                Guid.NewGuid().ToString("N"),
                userId,
                tripId,
                targetId,
                now);
            Touch(connection, tripId, now);
        }

        internal bool RemoveTarget(SQLiteConnection connection, string tripId, string savedItemId)
        {
            return connection.Execute(
                "DELETE FROM saved_items WHERE id=? AND user_id=? AND trip_id=?",
                savedItemId,
                userId,
                tripId) > 0;
        }

        internal void Touch(SQLiteConnection connection, string tripId, string now)
        {
            connection.Execute("UPDATE trips SET updated_utc=? WHERE id=? AND user_id=?", now, tripId, userId);
        }
#endregion
    }
}
