using System;
using System.Collections.Generic;
using System.Linq;

// In-memory snapshot passed to the next step. It contains no UI references.
public sealed class PaymentBooking
{
    public string Username { get; }
    public string Origin => Outbound.Origin;
    public string Destination => Outbound.Destination;
    public DateTime DepartureDate => Outbound.Date;
    public DateTime? ReturnDate => Return?.Date;
    public int PassengerCount { get; }
    public PassengerParty Passengers { get; }
    public string CabinClass { get; }
    public Leg Outbound { get; }
    public Leg Return { get; }
    public string Currency => "USD";
    public bool IsSample => true;
    public int EstimatedTotal { get; }

    internal PaymentBooking(SeatSelectionPage.Booking outbound, SeatSelectionPage.Booking inbound)
    {
        Passengers=outbound.Passengers.Clone();
        Username = TravelApp.TravelAppRuntime.Instance?.Services?.Accounts.Username;
        Outbound = new Leg(outbound);
        Return = inbound == null ? null : new Leg(inbound);
        PassengerCount = outbound.PassengerCount;
        CabinClass = outbound.Cabin;
        EstimatedTotal = Passengers.TotalFare(Outbound.FarePerTraveler) + (Return==null?0:Passengers.TotalFare(Return.FarePerTraveler));
    }

    internal bool Matches(SeatSelectionPage.Booking outbound, SeatSelectionPage.Booking inbound)
    {
        return Username == TravelApp.TravelAppRuntime.Instance?.Services?.Accounts.Username &&
            UnityEngine.JsonUtility.ToJson(Passengers)==UnityEngine.JsonUtility.ToJson(outbound.Passengers) && PassengerCount == outbound.PassengerCount && CabinClass == outbound.Cabin &&
            Outbound.Matches(outbound) && (Return == null ? inbound == null : inbound != null && Return.Matches(inbound));
    }

    public sealed class Leg
    {
        public string Origin { get; }
        public string Destination { get; }
        public DateTime Date { get; }
        public string ItineraryId { get; }
        public IReadOnlyList<FlightResultsPage.Segment> Segments { get; }
        public IReadOnlyList<FlightResultsPage.Layover> Layovers { get; }
        public string ConnectionSummary { get; }
        public string SegmentSummary { get; }
        public FlightResultsPage.BaggageAllowance Baggage { get; }
        public string AirlineCode { get; }
        public string Airline { get; }
        public string DepartureTime { get; }
        public string ArrivalTime { get; }
        public int DurationMinutes { get; }
        public int Stops { get; }
        public int FarePerTraveler { get; }
        // Seat order corresponds to traveler order.
        public IReadOnlyList<string> Seats { get; }
        public IReadOnlyList<IReadOnlyList<string>> SegmentSeats { get; }

        internal bool Matches(SeatSelectionPage.Booking booking) =>
            Origin == booking.Origin && Destination == booking.Destination && Date == booking.DepartureDate &&
            ItineraryId == booking.Flight.Id && AirlineCode == booking.Flight.Code && Airline == booking.Flight.Airline &&
            DepartureTime == booking.Flight.Departure && ArrivalTime == booking.Flight.Arrival &&
            DurationMinutes == booking.Flight.DurationMinutes && Stops == booking.Flight.Stops &&
            FarePerTraveler == booking.FarePerTraveler && SegmentSeats.Count==booking.SegmentSeats.Count &&
            SegmentSeats.Select((row,index)=>row.SequenceEqual(booking.SeatsForSegment(index))).All(equal=>equal) &&
            Segments.Select((segment,index)=>segment.DepartureUtc==booking.Flight.Segments[index].DepartureUtc && segment.ArrivalUtc==booking.Flight.Segments[index].ArrivalUtc).All(equal=>equal);

        internal Leg(SeatSelectionPage.Booking booking)
        {
            Origin = booking.Origin;
            Destination = booking.Destination;
            Date = booking.DepartureDate;
            ItineraryId = booking.Flight.Id;
            Segments = Array.AsReadOnly(booking.Flight.Segments.ToArray());
            Layovers = Array.AsReadOnly(booking.Flight.Layovers.ToArray());
            ConnectionSummary = booking.Flight.ConnectionSummary;
            SegmentSummary = booking.Flight.SegmentSummary;
            Baggage = booking.Flight.Baggage;
            AirlineCode = booking.Flight.Code;
            Airline = booking.Flight.Airline;
            DepartureTime = booking.Flight.Departure;
            ArrivalTime = booking.Flight.Arrival;
            DurationMinutes = booking.Flight.DurationMinutes;
            Stops = booking.Flight.Stops;
            FarePerTraveler = booking.FarePerTraveler;
            SegmentSeats = Array.AsReadOnly(booking.SegmentSeats.Select(row=>(IReadOnlyList<string>)Array.AsReadOnly(row.ToArray())).ToArray());
            Seats = SegmentSeats[0];
        }
    }
}
