using System;

namespace TravelPlanning.Data
{
    [Serializable]
    public sealed class SeedFlight
    {
        public string id, airlineId, flightNumber, originAirportId, destinationAirportId;
        public string departureUtc, arrivalUtc, departureLocalDate;
        public int priceCents, availableSeats;
    }
}
