using System;

namespace TravelPlanning.Data
{
    [Serializable]
    public sealed class SeedPlace
    {
        public string id, kind, destinationId, name, description, address, googleMapsUrl;
        public int priceCents;
    }
}
