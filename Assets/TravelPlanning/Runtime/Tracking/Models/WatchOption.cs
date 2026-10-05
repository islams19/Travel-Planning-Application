using System.Collections.Generic;

namespace TravelPlanning.Tracking
{
    public sealed class WatchOption
    {
        // Null for a target preview that has never been tracked by this account.
        public string Id { get; set; }
        public string TargetId { get; set; }
        public PriceTargetKind Kind { get; set; }
        public string Title { get; set; }
        public int CurrentPriceCents { get; set; }
        public int LastPriceCents { get; set; }
        public bool IsActive { get; set; }
        public string Currency { get; set; }
        public string PriceUnit { get; set; }
    }
}
