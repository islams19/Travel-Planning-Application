using System;
using System.Linq;

// A serializable snapshot of the existing booking controllers, not a second live booking model.
[Serializable]
public sealed class TripDraft
{
    public enum Step { Search, Dates, DepartureFlights, DepartureSeats, ReturnFlights, ReturnSeats, Review, Payment }
    public string id, username, updatedUtc, title;
    public string origin, destination, originText, destinationText;
    public string originAirportCode,destinationAirportCode;
    public long departureTicks, returnTicks;
    public bool oneWay;
    public int passengers = 1;
    public PassengerParty passengerDetails;
    public int passengerDetailsVersion;
    public PassengerParty PassengerDetails => passengerDetailsVersion>0?PassengerParty.Restore(passengerDetails,passengers):PassengerParty.Adults(passengers);
    public string cabin = "Economy";
    public Step step;
    public Leg outbound = new Leg(), inbound = new Leg();
    public DateTime? Departure => departureTicks > 0 && departureTicks <= DateTime.MaxValue.Ticks ? new DateTime(departureTicks) : (DateTime?)null;
    public DateTime? Return => returnTicks > 0 && returnTicks <= DateTime.MaxValue.Ticks ? new DateTime(returnTicks) : (DateTime?)null;
    public bool NeedsPayment => !string.IsNullOrWhiteSpace(origin) && !string.IsNullOrWhiteSpace(destination) &&
        !string.Equals(origin.Split(',')[0], destination.Split(',')[0], StringComparison.OrdinalIgnoreCase) &&
        Departure.HasValue && (oneWay || Return.HasValue && Return.Value > Departure.Value) &&
        PassengerDetails.IsValid && new[] { "Economy", "Premium Economy", "Business", "First" }.Contains(cabin) &&
        Complete(outbound) && (oneWay || Complete(inbound));
    private bool Complete(Leg leg)
    {
        if(leg==null || !leg.confirmed || string.IsNullOrEmpty(leg.airlineCode) || leg.farePerTraveler<=0) return false;
        bool Valid(string[] seats)=>seats!=null && seats.Length==PassengerDetails.SeatCount && seats.All(s=>!string.IsNullOrWhiteSpace(s)) && seats.Distinct().Count()==PassengerDetails.SeatCount;
        int expected=FlightResultsPage.ExpectedSegments(leg.itineraryId);
        if(leg.segments==null || leg.segments.Length==0) return expected==1 && Valid(leg.seats);
        return leg.segments.Length==expected && leg.segments.All(s=>s!=null && Valid(s.seats) &&
            !string.IsNullOrEmpty(s.originCode) && !string.IsNullOrEmpty(s.destinationCode) && s.arrivalUtcTicks>s.departureUtcTicks && s.departureUtcTicks>0);
    }
    [Serializable] public sealed class Segment
    {
        public string originCode,destinationCode,flightNumber;
        public long departureUtcTicks,arrivalUtcTicks;
        public string[] seats=Array.Empty<string>();
    }
    [Serializable] public sealed class Leg
    {
        public string airlineCode;
        public string itineraryId;
        public int farePerTraveler;
        public string[] seats = Array.Empty<string>();
        public Segment[] segments=Array.Empty<Segment>();
        public bool confirmed;
    }
}
