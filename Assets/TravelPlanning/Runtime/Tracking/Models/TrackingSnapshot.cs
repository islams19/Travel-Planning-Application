using System.Collections.Generic;

namespace TravelPlanning.Tracking
{
    public sealed class TrackingSnapshot
    {
        public IReadOnlyList<WatchOption> Watches { get; internal set; }
        public IReadOnlyList<PriceNotice> Notifications { get; internal set; }
        public int UnreadCount { get; internal set; }
        public long ElapsedMilliseconds { get; internal set; }
        public int WorkerThreadId { get; internal set; }
    }
}
