using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TravelPlanning.Data;
using TravelPlanning.Destinations;

namespace TravelPlanning.Reviews
{
    /// <summary>Loads a fresh place and its own reviews from SQLite on the database worker.</summary>
    public sealed class ReviewService : DatabaseServiceBase
    {
        public ReviewService(TravelDatabase database) : base(database)
        {
        }

#region Load Async
        public Task<ReviewDetails> LoadAsync(
            PlaceCategory category,
            string placeId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(placeId))
                throw new ArgumentException("Choose a place first.", nameof(placeId));
            string table;
            string reviewColumn;
            // Table/column names come only from this list, never from user text.
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
                    throw new ArgumentException("Choose a valid place category.", nameof(category));
            }

            var elapsed = Stopwatch.StartNew();
            return base.ExecuteAsync(connection =>
            {
                var place = connection.FindWithQuery<PlaceOption>(
                    "SELECT id AS Id, destination_id AS DestinationId, name AS Name, description AS Description, address AS Address, " +
                    "price_cents AS PriceCents, currency AS Currency, google_maps_url AS GoogleMapsUrl FROM " +
                    table +
                    " WHERE id=?",
                    placeId);
                if (place == null)
                    throw new ArgumentException("This place is not available in the selected category.", nameof(placeId));
                cancellationToken.ThrowIfCancellationRequested();
                var reviews = connection.Query<ReviewOption>(
                    "SELECT id AS Id, traveler_name AS TravelerName, rating AS Rating, body AS Body, is_demo AS IsDemo " +
                    "FROM reviews WHERE " +
                    reviewColumn +
                    "=? ORDER BY id",
                    placeId);
                cancellationToken.ThrowIfCancellationRequested();
                place.Category = category;
                place.ReviewCount = reviews.Count;
                place.AverageRating = reviews.Count == 0 ? (double? )null : reviews.Average(review => review.Rating);
                cancellationToken.ThrowIfCancellationRequested();
                elapsed.Stop();
                return new ReviewDetails
                {
                    Place = place,
                    Reviews = reviews.AsReadOnly(),
                    ElapsedMilliseconds = elapsed.ElapsedMilliseconds,
                    WorkerThreadId = Thread.CurrentThread.ManagedThreadId
                };
            }, cancellationToken);
        }
#endregion
    }
}
