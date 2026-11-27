using System;

namespace TravelApp.FlightMap
{
    // Shared offline Economy basis. Cabin and traveler rates are deliberately applied by booking only.
    public static class MockTravelPricing
    {
        public static uint StableHash(string value)
        {uint hash=2166136261;foreach(char c in value??"")hash=unchecked((hash^c)*16777619);return hash;}
        public static int EconomyBasis(FlightLocation from,FlightLocation to,DateTime? date=null)
        {
            if(from==null || to==null || from.code==to.code)return 0;
            double km=FlightGeoMath.GreatCircleDistanceKm(from,to);
            bool domestic=from.country==to.country,regional=MockRouteRules.Region(from)==MockRouteRules.Region(to);
            double basis=domestic?60+km*.062:regional?85+km*.065:180+km*.062;
            string pair=string.CompareOrdinal(from.code,to.code)<0?from.code+to.code:to.code+from.code;
            if(date.HasValue)
            {
                var d=date.Value.Date;
                double season=d.Month==12?1.18:d.Month>=6 && d.Month<=8?1.12:d.Month==1 || d.Month==2?.94:1;
                double weekday=d.DayOfWeek==DayOfWeek.Friday || d.DayOfWeek==DayOfWeek.Sunday?1.08:d.DayOfWeek==DayOfWeek.Tuesday || d.DayOfWeek==DayOfWeek.Wednesday?.96:1;
                double variation=.94+(StableHash(pair+d.ToString("yyyyMMdd",System.Globalization.CultureInfo.InvariantCulture))%13)/100d;
                basis*=season*weekday*variation;
            }
            return Round(basis);
        }
        public static int ItineraryEconomy(FlightLocation from,FlightLocation to,DateTime date,int stops,int option,double actualKm)
        {
            double direct=FlightGeoMath.GreatCircleDistanceKm(from,to);
            double schedule=new[]{1.00,1.12,1.22,1.30,.92,1.06,.90,1.10,1.18,1.25,.94,1.24}[option%12];
            double connection=stops==0?1:stops==1?(option%2==0?.93:1.02):.88;
            double detour=1+Math.Min(.12,Math.Max(0,actualKm/Math.Max(1,direct)-1)*.18);
            double fare=EconomyBasis(from,to,date)*schedule*connection*detour;
            double floor=direct>=8500?450+direct*.02:direct>=5000?260:55;
            return Round(Math.Max(floor,fare));
        }
        public static int CalendarEstimate(FlightLocation from,FlightLocation to,DateTime date)=>Round(EconomyBasis(from,to,date)*.90);
        public static int PlanningEstimate(FlightLocation from,FlightLocation to)=>Round(EconomyBasis(from,to)*.90);
        static int Round(double value)=>Math.Max(55,(int)Math.Round(value/5,MidpointRounding.AwayFromZero)*5);
    }
}
