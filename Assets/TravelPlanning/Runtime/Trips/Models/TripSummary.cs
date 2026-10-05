using System.Collections.Generic;

namespace TravelPlanning.Trips
{
    public sealed class TripSummary
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public string CreatedUtc { get; set; }
        public string UpdatedUtc { get; set; }
        public int ItemCount { get; set; }
    }
}
