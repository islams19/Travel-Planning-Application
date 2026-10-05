using System.Collections.Generic;

namespace TravelPlanning.Tracking
{
    public sealed class PriceNotice
    {
        public string Id { get; set; }
        public string WatchId { get; set; }
        public string HistoryId { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string CreatedUtc { get; set; }
        public bool IsRead { get; set; }
        public int OldPriceCents { get; set; }
        public int NewPriceCents { get; set; }
    }
}
