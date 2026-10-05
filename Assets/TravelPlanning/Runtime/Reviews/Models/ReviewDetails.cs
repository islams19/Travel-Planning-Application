using System.Collections.Generic;
using TravelPlanning.Destinations;

namespace TravelPlanning.Reviews
{
    public sealed class ReviewDetails
    {
        public PlaceOption Place { get; internal set; }
        public IReadOnlyList<ReviewOption> Reviews { get; internal set; }
        public long ElapsedMilliseconds { get; internal set; }
        public int WorkerThreadId { get; internal set; }
    }
}
