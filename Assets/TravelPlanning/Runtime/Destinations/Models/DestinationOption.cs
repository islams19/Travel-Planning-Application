using System.Collections.Generic;

namespace TravelPlanning.Destinations
{
    /// <summary>A destination stored in the catalog. Public properties also support SQLite row mapping.</summary>
    public sealed class DestinationOption
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Country { get; set; }
        public string Region { get; set; }
        public string Description { get; set; }
    }
}
