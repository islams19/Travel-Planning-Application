using System;

namespace TravelPlanning.Data
{
    // Plain data containers match the JSON field names. They contain no database or Unity logic.
    [Serializable]
    public sealed class TravelSeedData
    {
        public string catalogVersion;
        public string startDate;
        public string endDate;
        public SeedDestination[] destinations;
        public SeedAirport[] airports;
        public SeedAirline[] airlines;
        public SeedFlight[] flights;
        public SeedPlace[] places;
        public SeedReview[] reviews;
    }
}
