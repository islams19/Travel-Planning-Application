using System;

public sealed class CompletedDemoBooking
{
    public bool PaymentCompleted => true;
    public PaymentBooking Booking { get; }
    public string Reference { get; }
    public DateTime CreatedAtUtc { get; }

    internal CompletedDemoBooking(PaymentBooking booking)
    {
        Booking = booking;
        Reference = "VOY-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant();
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static string Itinerary(PaymentBooking booking)
    {
        string result = DescribeLeg("OUTBOUND", booking.Outbound, booking.CabinClass);
        if (booking.Return != null)
            result += "\n\n" + DescribeLeg("RETURN", booking.Return, booking.CabinClass);
        return result;
    }

    private static string DescribeLeg(string heading, PaymentBooking.Leg leg,string cabin)
    {
        return $"{heading} · {leg.Date:MMM d, yyyy}\n{leg.Origin} → {leg.Destination}\n" +
            BookingItineraryText.Describe(leg.Airline,leg.Segments,leg.Layovers,leg.SegmentSeats,cabin,leg.FarePerTraveler,leg.Seats.Count,leg.Baggage);
    }
}
