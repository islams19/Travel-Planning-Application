using System;
using TravelApp.FlightMap;

// Calendar-only planning estimates. Never used to price or select a flight-result itinerary.
public static class DateFareEstimates
{
    public static int? EconomyFare(DateTime date,string originCode,string destinationCode)
    {
        var from=TravelLocationCatalog.Find(originCode); var to=TravelLocationCatalog.Find(destinationCode);
        if(from==null || to==null || from.code==to.code || from.BookingDestination==to.BookingDestination) return null;
        return MockTravelPricing.CalendarEstimate(from,to,date);
    }
}
