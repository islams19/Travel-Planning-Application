using System.Collections.Generic;

namespace TravelPlanning.Trips
{
    /// <summary>The saved reference resolved against today's local catalog, not a price snapshot.</summary>
    public sealed class SavedTripItem
    {
        public string Id { get; internal set; }
        public string TargetId { get; internal set; }
        public SavedItemKind Kind { get; internal set; }
        public string Title { get; internal set; }
        public string Details { get; internal set; }
        public int? PriceCents { get; internal set; }
        public string Currency { get; internal set; }
        public string PriceUnit { get; internal set; }
    }
}
