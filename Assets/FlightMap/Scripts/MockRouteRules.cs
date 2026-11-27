using System;
using System.Collections.Generic;
using System.Linq;

namespace TravelApp.FlightMap
{
    // Illustrative geography/range rules, not a claim of published airline route availability.
    public static class MockRouteRules
    {
        public const double MaximumLegKm=14500;
        static readonly string[] Hubs={"ATL","ORD","DFW","LAX","SFO","SEA","YYZ","YVR","LHR","CDG","AMS","FRA","IST","DXB","DOH","SIN","HND","ICN","GRU","AKL","JNB","DEL"};
        public static string Region(FlightLocation a)
        {
            if(new[]{"United States","Canada","Mexico"}.Contains(a.country))return "North America";
            if(new[]{"Brazil","Argentina","Peru","Colombia","Chile"}.Contains(a.country))return "South America";
            if(new[]{"Australia","New Zealand"}.Contains(a.country))return "Oceania";
            if(new[]{"United Arab Emirates","Qatar","Türkiye","Israel","Saudi Arabia"}.Contains(a.country))return "Middle East";
            if(new[]{"South Africa","Kenya","Egypt","Morocco","Nigeria"}.Contains(a.country))return "Africa";
            if(new[]{"United Kingdom","France","Germany","Spain","Italy","Greece","Netherlands","Portugal","Switzerland","Austria","Czechia","Denmark","Sweden","Norway","Finland","Ireland","Hungary","Iceland","Poland"}.Contains(a.country))return "Europe";
            return "Asia";
        }
        public static bool SameCity(FlightLocation a,FlightLocation b)=>a.city==b.city && a.country==b.country;
        // Conservative illustrative limits for restricted/regional airports. No published schedules are implied.
        public static bool CanOperateLeg(FlightLocation from,FlightLocation to)
        {
            foreach(var pair in new[]{new[]{from,to},new[]{to,from}})
            {
                var airport=pair[0];var other=pair[1];double distance=Km(airport,other);
                if(airport.code=="LGA" && (distance>2500 || !new[]{"United States","Canada"}.Contains(other.country)))return false;
                if(airport.code=="DCA" && (distance>2200 || !new[]{"United States","Canada"}.Contains(other.country)))return false;
                if(airport.code=="LCY" && (distance>2500 || Region(other)!="Europe"))return false;
                if(new[]{"JTR","JMK","HKT","USM"}.Contains(airport.code) && distance>5000)return false;
            }
            return Km(from,to)<=MaximumLegKm;
        }
        public static double Km(FlightLocation a,FlightLocation b)=>FlightGeoMath.GreatCircleDistanceKm(a,b);
        public static IReadOnlyList<FlightLocation> Connections(FlightLocation origin,FlightLocation to,int desiredStops,int variant)
        {
            double direct=Km(origin,to);
            var hubs=Hubs.Select(TravelLocationCatalog.Find).Where(h=>h!=null && !SameCity(h,origin) && !SameCity(h,to)).ToArray();
            bool Valid(FlightLocation[] points)
            {
                double distance=0;
                for(int i=0;i<points.Length-1;i++)
                {double leg=Km(points[i],points[i+1]);if(leg<150 || !CanOperateLeg(points[i],points[i+1]) || SameCity(points[i],points[i+1]))return false;distance+=leg;}
                if(distance>direct*1.45+250)return false;
                for(int i=1;i<points.Length-2;i++)if(Km(origin,points[i])>Km(origin,points[i+1])+400 || Km(points[i+1],to)>Km(points[i],to)+400)return false;
                return true;
            }
            if(desiredStops==0 && CanOperateLeg(origin,to))return Array.Empty<FlightLocation>();
            if(desiredStops>=2)
            {
                var pairs=(from a in hubs from b in hubs where a.code!=b.code && !SameCity(a,b)
                    let points=new[]{origin,a,b,to} where Valid(points)
                    select new {Airports=new[]{a,b},Distance=Km(origin,a)+Km(a,b)+Km(b,to)})
                    .OrderBy(p=>p.Distance).ThenBy(p=>p.Airports[0].code).ThenBy(p=>p.Airports[1].code).Take(3).ToArray();
                if(pairs.Length>0)return pairs[variant%pairs.Length].Airports;
            }
            var singles=hubs.Where(h=>Valid(new[]{origin,h,to})).OrderBy(h=>Km(origin,h)+Km(h,to)).ThenBy(h=>h.code).Take(4).ToArray();
            if(singles.Length>0)return new[]{singles[variant%singles.Length]};
            if(CanOperateLeg(origin,to))return Array.Empty<FlightLocation>();
            // A second connection can be necessary for an ultra-long route even when one was requested.
            if(desiredStops<2)return Connections(origin,to,2,variant);
            return Array.Empty<FlightLocation>();
        }
        public static int BlockMinutes(FlightLocation origin,FlightLocation to,int variant)
        {
            double km=Km(origin,to),speed=km<1200?700:850;
            double delta=to.longitude-origin.longitude;if(delta>180)delta-=360;if(delta< -180)delta+=360;
            double wind=Math.Abs(origin.latitude)>20 && Math.Abs(to.latitude)>20?(delta>0?.97:1.04):1;
            double variation=.99+(variant%3)*.01;
            return Math.Max(45,(int)Math.Round((40+km*1.035/speed*60*wind)*variation/5)*5);
        }
        public static int LayoverMinutes(FlightLocation previous,FlightLocation hub,FlightLocation next,int variant)
        {
            bool international=previous.country!=hub.country || next.country!=hub.country;
            return (international?110:65)+(variant%4)*15;
        }
    }
}
