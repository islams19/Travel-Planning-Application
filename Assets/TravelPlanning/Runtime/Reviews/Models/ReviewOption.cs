using System.Collections.Generic;
using TravelPlanning.Destinations;

namespace TravelPlanning.Reviews
{
    /// <summary>A review stored locally; IsDemo tells the screen whether to label it as sample data.</summary>
    public sealed class ReviewOption
    {
        public string Id { get; set; }
        public string TravelerName { get; set; }
        public int Rating { get; set; }
        public string Body { get; set; }
        public bool IsDemo { get; set; }
    }
}
