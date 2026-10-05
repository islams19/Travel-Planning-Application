using System.Collections.Generic;

namespace TravelPlanning.Trips
{
    public sealed class TripListResult
    {
        public IReadOnlyList<TripSummary> Trips { get; internal set; }
        public string StartDate { get; internal set; }
        public string EndDate { get; internal set; }
        public long ElapsedMilliseconds { get; internal set; }
        public int WorkerThreadId { get; internal set; }
    }
}
