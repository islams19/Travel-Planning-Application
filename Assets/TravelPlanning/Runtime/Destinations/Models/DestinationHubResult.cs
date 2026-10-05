using System.Collections.Generic;

namespace TravelPlanning.Destinations
{
    public sealed class DestinationHubResult
    {
        public DestinationOption Destination { get; internal set; }
        public IReadOnlyList<PlaceOption> Hotels { get; internal set; }
        public IReadOnlyList<PlaceOption> Restaurants { get; internal set; }
        public IReadOnlyList<PlaceOption> Experiences { get; internal set; }
        public IReadOnlyList<PlaceOption> Hotspots { get; internal set; }
        // For developer validation; the app does not need to show implementation details.
        public long ElapsedMilliseconds { get; internal set; }
        public int WorkerThreadId { get; internal set; }
    }
}
