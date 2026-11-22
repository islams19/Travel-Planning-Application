using System;
using TravelApp.FlightMap;

// Calendar-only planning estimates. Never used to price or select a flight-result itinerary.
public static class DateFareEstimates
{
    public static int? EconomyFare(DateTime date,string originCode,string destinationCode)
    {
        var from=TravelLocationCatalog.Find(originCode); var to=TravelLocationCatalog.Find(destinationCode);
        if(from==null || to==null || from.code==to.code || from.BookingDestination==to.BookingDestination) return null;
        // Stable FNV-1a rather than string.GetHashCode, which can vary between processes.
        string pair=string.CompareOrdinal(from.code,to.code)<0 ? from.code+to.code : to.code+from.code;
        uint hash=2166136261;
        foreach(char letter in pair) hash=unchecked((hash^letter)*16777619);
        hash=unchecked((hash^(uint)(date.Date.Ticks/TimeSpan.TicksPerDay))*16777619);
        int weekday=date.DayOfWeek==DayOfWeek.Friday || date.DayOfWeek==DayOfWeek.Sunday ? 65 :
            date.DayOfWeek==DayOfWeek.Saturday ? 35 : date.DayOfWeek==DayOfWeek.Monday ? 15 : 0;
        int seasonal=date.Month>=6 && date.Month<=8 || date.Month==12 ? 40 : 0;
        int baseFare=90+(int)(FlightGeoMath.GreatCircleDistanceKm(from,to)*.018);
        return ((baseFare+weekday+seasonal+(int)(hash%65)+2)/5)*5;
    }
}
