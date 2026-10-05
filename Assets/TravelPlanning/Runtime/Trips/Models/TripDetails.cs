using System.Collections.Generic;

namespace TravelPlanning.Trips
{
    public sealed class TripDetails
    {
        public TripSummary Trip { get; internal set; }
        public IReadOnlyList<SavedTripItem> Items { get; internal set; }
        public long ElapsedMilliseconds { get; internal set; }
        public int WorkerThreadId { get; internal set; }
    }
}
