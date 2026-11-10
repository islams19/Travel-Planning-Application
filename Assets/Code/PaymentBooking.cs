using System;
using System.Collections.Generic;
using System.Linq;

// In-memory snapshot passed to the next step. It contains no UI references.
public sealed class PaymentBooking
{
    public string Origin => Outbound.Origin;
    public string Destination => Outbound.Destination;
    public DateTime DepartureDate => Outbound.Date;
    public DateTime? ReturnDate => Return?.Date;
    public int PassengerCount { get; }
    public string CabinClass { get; }
    public Leg Outbound { get; }
    public Leg Return { get; }
    public string Currency => "USD";
    public bool IsSample => true;
    public int EstimatedTotal { get; }

    internal PaymentBooking(SeatSelectionPage.Booking outbound, SeatSelectionPage.Booking inbound)
    {
        Outbound = new Leg(outbound);
        Return = inbound == null ? null : new Leg(inbound);
        PassengerCount = outbound.PassengerCount;
        CabinClass = outbound.Cabin;
        EstimatedTotal = (Outbound.FarePerTraveler + (Return?.FarePerTraveler ?? 0)) * PassengerCount;
    }

    public sealed class Leg
    {
        public string Origin { get; }
        public string Destination { get; }
        public DateTime Date { get; }
        public string AirlineCode { get; }
        public string Airline { get; }
        public string DepartureTime { get; }
        public string ArrivalTime { get; }
        public int DurationMinutes { get; }
        public int Stops { get; }
        public int FarePerTraveler { get; }
        // Seat order corresponds to traveler order.
        public IReadOnlyList<string> Seats { get; }

        internal Leg(SeatSelectionPage.Booking booking)
        {
            Origin = booking.Origin;
            Destination = booking.Destination;
            Date = booking.DepartureDate;
            AirlineCode = booking.Flight.Code;
            Airline = booking.Flight.Airline;
            DepartureTime = booking.Flight.Departure;
            ArrivalTime = booking.Flight.Arrival;
            DurationMinutes = booking.Flight.DurationMinutes;
            Stops = booking.Flight.Stops;
            FarePerTraveler = booking.FarePerTraveler;
            Seats = Array.AsReadOnly(booking.Seats.ToArray());
        }
    }
}
