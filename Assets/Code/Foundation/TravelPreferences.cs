using System;
using System.Linq;
using TravelApp.FlightMap;
namespace TravelApp
{
    // Stored in the existing account/save document; these are defaults, never live booking values.
    [Serializable] public sealed class TravelPreferences
    {
        public string homeAirportCode="JFK", cabinClass="Economy", seatType="Any", distanceUnits="Kilometers";
        public static readonly string[] Cabins={"Economy","Premium Economy","Business","First"};
        public static readonly string[] SeatTypes={"Any","Window","Aisle"};
        public static readonly string[] Distances={"Kilometers","Miles"};
        public static TravelPreferences Normalize(TravelPreferences value)
        {
            value=value??new TravelPreferences();
            return new TravelPreferences {
                homeAirportCode=TravelLocationCatalog.Find(value.homeAirportCode)?.BookingDestination!=null?value.homeAirportCode:"JFK",
                cabinClass=Cabins.Contains(value.cabinClass)?value.cabinClass:"Economy",
                seatType=SeatTypes.Contains(value.seatType)?value.seatType:"Any",
                distanceUnits=Distances.Contains(value.distanceUnits)?value.distanceUnits:"Kilometers"};
        }
    }
}
