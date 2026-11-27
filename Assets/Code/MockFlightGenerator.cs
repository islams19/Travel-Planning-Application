using System;
using System.Collections.Generic;
using System.Linq;
using TravelApp.FlightMap;

// Builds the existing itinerary model. No second booking state or inventory model is introduced.
public static class MockFlightGenerator
{
    public sealed class Carrier
    {
        public readonly string Code,Name,Country;
        public Carrier(string code,string name,string country){Code=code;Name=name;Country=country;}
    }
    public static readonly IReadOnlyList<Carrier> Carriers=Array.AsReadOnly(new[]{
        new Carrier("B6","JetBlue","United States"),new Carrier("DL","Delta Air Lines","United States"),new Carrier("AA","American Airlines","United States"),new Carrier("UA","United Airlines","United States"),
        new Carrier("AS","Alaska Airlines","United States"),new Carrier("AC","Air Canada","Canada"),new Carrier("QF","Qantas","Australia"),new Carrier("NZ","Air New Zealand","New Zealand"),
        new Carrier("BA","British Airways","United Kingdom"),new Carrier("LH","Lufthansa","Germany"),new Carrier("AF","Air France","France"),new Carrier("KL","KLM","Netherlands"),
        new Carrier("NH","ANA","Japan"),new Carrier("JL","Japan Airlines","Japan"),new Carrier("SQ","Singapore Airlines","Singapore"),new Carrier("EK","Emirates","United Arab Emirates"),new Carrier("QR","Qatar Airways","Qatar"),
        new Carrier("TK","Turkish Airlines","Türkiye"),new Carrier("AI","Air India","India"),new Carrier("CA","Air China","China"),new Carrier("KE","Korean Air","South Korea"),
        new Carrier("LA","LATAM Airlines","Brazil"),new Carrier("AV","Avianca","Colombia"),new Carrier("SA","South African Airways","South Africa"),new Carrier("KQ","Kenya Airways","Kenya"),new Carrier("MS","Egyptair","Egypt"),new Carrier("AT","Royal Air Maroc","Morocco"),
        new Carrier("AM","Aeromexico","Mexico"),new Carrier("TG","Thai Airways","Thailand"),new Carrier("GA","Garuda Indonesia","Indonesia"),new Carrier("MH","Malaysia Airlines","Malaysia"),new Carrier("PR","Philippine Airlines","Philippines"),new Carrier("CX","Cathay Pacific","Hong Kong"),new Carrier("CI","China Airlines","Taiwan"),
        new Carrier("TP","TAP Air Portugal","Portugal"),new Carrier("OS","Austrian Airlines","Austria"),new Carrier("SK","SAS","Denmark"),new Carrier("LO","LOT Polish Airlines","Poland"),new Carrier("EI","Aer Lingus","Ireland"),new Carrier("QS","Smartwings","Czechia"),new Carrier("P4","Air Peace","Nigeria"),new Carrier("AR","Aerolíneas Argentinas","Argentina"),new Carrier("IB","Iberia","Spain"),new Carrier("AZ","ITA Airways","Italy"),new Carrier("A3","Aegean Airlines","Greece"),new Carrier("LX","SWISS","Switzerland"),new Carrier("AY","Finnair","Finland"),new Carrier("FR","Ryanair","Ireland")});
    static readonly int[] RequestedStops={0,0,0,0,1,0,1,1,1,0,2,1};
    static readonly int[] DepartureMinutes={20*60+30,6*60,14*60+15,11*60,7*60+10,9*60+20,5*60+40,17*60+35,22*60+25,19*60+45,12*60+10,8*60};
    public static readonly DateTime DefaultTravelDate=new DateTime(2027,1,15);
    public static bool Supports(Carrier c,FlightLocation from,FlightLocation to)
    {
        double km=MockRouteRules.Km(from,to);if(!MockRouteRules.CanOperateLeg(from,to))return false;
        if(c.Code=="UA" && (from.code=="JFK" || to.code=="JFK"))return false;
        if(c.Country=="United States" && MockRouteRules.Km(from,to)>7500 && (from.country=="United States" || to.country=="United States"))
        {
            var us=from.country=="United States"?from:to;var overseas=from.country=="United States"?to:from;
            if(MockRouteRules.Region(overseas)=="Oceania")
            {if(c.Code=="DL" && us.code!="LAX" || c.Code=="AA" && !new[]{"LAX","DFW"}.Contains(us.code) || c.Code=="UA" && !new[]{"LAX","SFO","IAH"}.Contains(us.code))return false;}
            if(MockRouteRules.Region(overseas)=="Asia")
            {if(c.Code=="DL" && !new[]{"LAX","SEA","ATL","DTW"}.Contains(us.code) || c.Code=="UA" && !new[]{"LAX","SFO","EWR","ORD","IAD"}.Contains(us.code) || c.Code=="AA" && !new[]{"JFK","LAX","DFW"}.Contains(us.code))return false;}
        }
        if(c.Code=="B6")return km<=7000 && (from.country=="United States" || to.country=="United States") &&
            (MockRouteRules.Region(from)=="North America" && MockRouteRules.Region(to)=="North America" ||
             new[]{"JFK","BOS"}.Contains(from.code) && new[]{"LHR","CDG","AMS","DUB","MAD"}.Contains(to.code) ||
             new[]{"JFK","BOS"}.Contains(to.code) && new[]{"LHR","CDG","AMS","DUB","MAD"}.Contains(from.code));
        if(new[]{"A3","QS"}.Contains(c.Code) && km>4500)return false;
        if(c.Code=="AS")return from.country=="United States" && to.country=="United States" && km<6000 && new[]{from.code,to.code}.Any(code=>new[]{"SEA","SFO","LAX"}.Contains(code));
        if(c.Code=="SK")return new[]{"Denmark","Sweden","Norway"}.Contains(from.country) || new[]{"Denmark","Sweden","Norway"}.Contains(to.country);
        if(c.Code=="FR")return MockRouteRules.Region(from)=="Europe" && MockRouteRules.Region(to)=="Europe" && km<4500;
        if(c.Code=="LA")return new[]{"Brazil","Argentina","Chile","Peru","Colombia"}.Contains(from.country) || new[]{"Brazil","Argentina","Chile","Peru","Colombia"}.Contains(to.country);
        return from.country==c.Country || to.country==c.Country;
    }
    static List<Carrier> Pool(FlightLocation from,FlightLocation to)
    {
        if((MockRouteRules.Region(from)=="North America" && MockRouteRules.Region(to)=="Oceania") || (MockRouteRules.Region(to)=="North America" && MockRouteRules.Region(from)=="Oceania"))
        {
            string oceaniaCarrier=(from.country=="New Zealand" || to.country=="New Zealand")?"NZ":"QF";
            return new[]{oceaniaCarrier,oceaniaCarrier=="QF"?"NZ":"QF",from.country=="Canada"||to.country=="Canada"?"AC":"UA","AA"}.Select(Find).ToList();
        }
        if(from.country=="United States" && to.country=="United States")return new[]{from.code,to.code}.Contains("SEA")?
            new[]{"AS","DL","AA","UA"}.Select(Find).ToList():new[]{"B6","DL","AA","UA"}.Select(Find).ToList();
        if(new[]{from.country,to.country}.Contains("United States") && new[]{from.country,to.country}.Contains("Mexico"))return new[]{"B6","DL","AA","UA"}.Select(Find).ToList();
        var home=Carriers.Where(c=>c.Country!="United States" && c.Code!="FR" && (c.Country==from.country || c.Country==to.country)).ToList();
        if(from.country=="United States" || to.country=="United States")home.AddRange(new[]{"UA","DL","AA"}.Select(Find));
        if(MockRouteRules.Region(from)=="Europe" && MockRouteRules.Region(to)=="Europe")home.Add(Find("FR"));
        foreach(var country in new[]{from.country,to.country})home.AddRange(Carriers.Where(c=>c.Country==country));
        if(home.Count==0)home.Add(Find(MockRouteRules.Region(from)=="Asia"?"SQ":MockRouteRules.Region(from)=="Africa"?"KQ":"LH"));
        return home.GroupBy(c=>c.Code).Select(g=>g.First()).Take(4).ToList();
    }
    static Carrier Find(string code)=>Carriers.First(c=>c.Code==code);
    public static IReadOnlyList<FlightResultsPage.Flight> Generate(FlightLocation from,FlightLocation to,DateTime date)
    {
        if(from==null || to==null || MockRouteRules.SameCity(from,to))return Array.Empty<FlightResultsPage.Flight>();
        var pool=Pool(from,to);var result=new List<FlightResultsPage.Flight>();
        for(int i=0;i<12;i++)
        {
            var via=MockRouteRules.Connections(from,to,RequestedStops[i],i%4);var points=new[]{from}.Concat(via).Concat(new[]{to}).ToArray();
            if(points.Zip(points.Skip(1),(a,b)=>MockRouteRules.CanOperateLeg(a,b)).Any(valid=>!valid))continue;
            int longest=Enumerable.Range(0,points.Length-1).OrderByDescending(n=>MockRouteRules.Km(points[n],points[n+1])).First();
            var preferred=pool[i%pool.Count];
            if(!Supports(preferred,points[longest],points[longest+1]))preferred=pool.FirstOrDefault(c=>Supports(c,points[longest],points[longest+1]))??preferred;
            var segments=new List<FlightResultsPage.Segment>();var layovers=new List<FlightResultsPage.Layover>();
            DateTime start=AirportClock.ToUtc(date.Date.AddMinutes(DepartureMinutes[i]),from.code);double totalKm=0;
            for(int leg=0;leg<points.Length-1;leg++)
            {
                var a=points[leg];var b=points[leg+1];totalKm+=MockRouteRules.Km(a,b);
                var operating=Supports(preferred,a,b)?preferred:Carriers.FirstOrDefault(c=>Supports(c,a,b))??preferred;
                int block=MockRouteRules.BlockMinutes(a,b,i);
                segments.Add(new FlightResultsPage.Segment(a.code,b.code,operating.Name,operating.Code+" "+(1000+i*10+leg),start,start.AddMinutes(block),date));start=start.AddMinutes(block);
                if(leg<points.Length-2){int wait=MockRouteRules.LayoverMinutes(a,b,points[leg+2],i);layovers.Add(new FlightResultsPage.Layover(b.code,b.city,wait));start=start.AddMinutes(wait);}
            }
            int fare=MockTravelPricing.ItineraryEconomy(from,to,date,layovers.Count,i,totalKm);
            string fingerprint=preferred.Code+":"+fare+":"+string.Join(";",segments.Select(segment=>segment.FlightNumber+segment.OriginCode+segment.DestinationCode+segment.DepartureUtc.Ticks+":"+segment.ArrivalUtc.Ticks));
            string id=$"mock2:{from.code}:{to.code}:{date.ToString("yyyyMMdd",System.Globalization.CultureInfo.InvariantCulture)}:{i}:{MockTravelPricing.StableHash(fingerprint):x8}:s{segments.Count}";
            result.Add(new FlightResultsPage.Flight(id,preferred.Code,preferred.Name,DepartureMinutes[i],fare,i,segments,layovers));
        }
        return result.AsReadOnly();
    }
    // Legacy template IDs remain readable for existing drafts; all new IDs include actual segment count.
    public static int LegacyOption(string id)
    {
        if(string.IsNullOrEmpty(id))return -1;
        int start=id.LastIndexOf('-');return start>=0 && int.TryParse(id.Substring(start+1),out int value) && value>=100 && value<=111?value-100:-1;
    }
    public static int ExpectedSegments(string id)
    {
        if(!string.IsNullOrEmpty(id) && id.StartsWith("mock2:",StringComparison.Ordinal))
        {int pos=id.LastIndexOf(":s",StringComparison.Ordinal);if(pos>=0 && int.TryParse(id.Substring(pos+2),out int count) && count>=1 && count<=3)return count;}
        int option=LegacyOption(id);return option>=0?RequestedStops[option]+1:1;
    }
}
