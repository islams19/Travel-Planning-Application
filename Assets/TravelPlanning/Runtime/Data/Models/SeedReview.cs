using System;

namespace TravelPlanning.Data
{
    [Serializable]
    public sealed class SeedReview
    {
        public string id, placeId, kind, travelerName, body;
        public int rating;
    }
}
