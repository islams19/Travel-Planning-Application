using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SQLite;
using TravelPlanning.Data;

namespace TravelPlanning.Destinations
{
    /// <summary>Reads destination places and sample-review summaries without blocking the Unity UI.</summary>
    public sealed class DestinationHubService : DatabaseServiceBase
    {
        private const string DestinationColumns =
            "id AS Id, name AS Name, country AS Country, region AS Region, description AS Description";
        public DestinationHubService(TravelDatabase database) : base(database)
        {
        }

#region Load Options Async
        public Task<IReadOnlyList<DestinationOption>> LoadOptionsAsync(CancellationToken cancellationToken = default)
        {
            return base.ExecuteAsync<IReadOnlyList<DestinationOption>>(connection =>
            {
                var destinations = connection.Query<DestinationOption>(
                    "SELECT " + DestinationColumns + " FROM destinations ORDER BY name, id");
                cancellationToken.ThrowIfCancellationRequested();
                return destinations.AsReadOnly();
            }, cancellationToken);
        }
#endregion

#region Load Async
        public Task<DestinationHubResult> LoadAsync(string destinationId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(destinationId))
                throw new ArgumentException("Choose a destination first.", nameof(destinationId));
            var elapsed = Stopwatch.StartNew();
            return base.ExecuteAsync(connection =>
            {
                var destination = connection.FindWithQuery<DestinationOption>(
                    "SELECT " +
                    DestinationColumns +
                    " FROM destinations WHERE id=?",
                    destinationId);
                if (destination == null)
                    throw new ArgumentException("Choose a destination from the catalog.", nameof(destinationId));
                var result = new DestinationHubResult
                {
                    Destination = destination,
                    Hotels = ReadPlaces(connection, destinationId, PlaceCategory.Hotel, cancellationToken),
                    Restaurants = ReadPlaces(connection, destinationId, PlaceCategory.Restaurant, cancellationToken),
                    Experiences = ReadPlaces(connection, destinationId, PlaceCategory.Experience, cancellationToken),
                    Hotspots = ReadPlaces(connection, destinationId, PlaceCategory.Hotspot, cancellationToken),
                    WorkerThreadId = Thread.CurrentThread.ManagedThreadId
                };
                cancellationToken.ThrowIfCancellationRequested();
                elapsed.Stop();
                result.ElapsedMilliseconds = elapsed.ElapsedMilliseconds;
                return result;
            }, cancellationToken);
        }
#endregion

#region Read Places
        private static IReadOnlyList<PlaceOption> ReadPlaces(
            SQLiteConnection connection,
            string destinationId,
            PlaceCategory category,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string table;
            string reviewColumn;
            // SQL identifiers cannot be parameters, so use only these fixed, trusted table names.
            switch (category)
            {
                case PlaceCategory.Hotel:
                    table = "hotels";
                    reviewColumn = "hotel_id";
                    break;
                case PlaceCategory.Restaurant:
                    table = "restaurants";
                    reviewColumn = "restaurant_id";
                    break;
                case PlaceCategory.Experience:
                    table = "experiences";
                    reviewColumn = "experience_id";
                    break;
                case PlaceCategory.Hotspot:
                    table = "hotspots";
                    reviewColumn = "hotspot_id";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(category));
            }

            // Separate indexed aggregates keep each place to one row, even if it has many reviews.
            string sql = "SELECT p.id AS Id, p.destination_id AS DestinationId, p.name AS Name, p.description AS Description, " +
                "p.address AS Address, p.price_cents AS PriceCents, p.currency AS Currency, p.google_maps_url AS GoogleMapsUrl, " +
                "(SELECT AVG(r.rating) FROM reviews r WHERE r." +
                reviewColumn +
                "=p.id) AS AverageRating, " +
                "(SELECT COUNT(*) FROM reviews r WHERE r." +
                reviewColumn +
                "=p.id) AS ReviewCount " +
                "FROM " +
                table +
                " p WHERE p.destination_id=? ORDER BY p.name, p.id";
            var places = connection.Query<PlaceOption>(sql, destinationId);
            token.ThrowIfCancellationRequested();
            foreach (var place in places)
                place.Category = category;
            return places.AsReadOnly();
        }
#endregion
    }
}
