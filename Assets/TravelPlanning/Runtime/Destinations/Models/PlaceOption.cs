using System.Collections.Generic;

namespace TravelPlanning.Destinations
{
    /// <summary>A place with its sample-review summary. Null price means unknown; zero means free.</summary>
    public sealed class PlaceOption
    {
        public string Id { get; set; }
        public string DestinationId { get; set; }
        public PlaceCategory Category { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Address { get; set; }
        public int? PriceCents { get; set; }
        public string Currency { get; set; }
        public string GoogleMapsUrl { get; set; }
        // An unrated place has no average, rather than a misleading zero-star rating.
        public double? AverageRating { get; set; }
        public int ReviewCount { get; set; }
    }
}
