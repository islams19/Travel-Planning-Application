using System;
using System.Linq;
using NUnit.Framework;
using TravelApp.FlightMap;

public sealed class MockFlightRealismTests
{
    static readonly DateTime Date=new DateTime(2027,5,12);
    [TestCase("JFK","BOS")] [TestCase("JFK","LAX")] [TestCase("JFK","LHR")]
    [TestCase("JFK","HND")] [TestCase("JFK","SYD")] [TestCase("LAX","SYD")]
    [TestCase("CDG","FCO")] [TestCase("SYD","MEL")] [TestCase("HND","SIN")]
    public void GeneratedItinerariesAreDeterministicGeographicallyPlausibleAndUseLocalTimes(string origin,string destination)
    {
        var from=TravelLocationCatalog.Find(origin);var to=TravelLocationCatalog.Find(destination);
        var flights=MockFlightGenerator.Generate(from,to,Date);var again=MockFlightGenerator.Generate(from,to,Date);
        Assert.That(flights.Count,Is.InRange(10,15));Assert.AreEqual(flights.Count,flights.Select(f=>f.Id).Distinct().Count());
        CollectionAssert.AreEqual(flights.Select(f=>f.Id+f.Price+f.SegmentSummary),again.Select(f=>f.Id+f.Price+f.SegmentSummary));
        Assert.LessOrEqual(flights.Select(f=>f.Code).Distinct().Count(),4,"Existing filter layout accommodates four route-specific carriers");
        foreach(var flight in flights)
        {
            Assert.AreEqual(origin,flight.Segments.First().OriginCode);Assert.AreEqual(destination,flight.Segments.Last().DestinationCode);
            Assert.AreEqual(flight.Segments.Count,MockFlightGenerator.ExpectedSegments(flight.Id));
            Assert.AreEqual(flight.DurationMinutes,flight.Segments.Sum(s=>s.DurationMinutes)+flight.Layovers.Sum(l=>l.DurationMinutes));
            double flown=0;
            foreach(var segment in flight.Segments)
            {
                var a=TravelLocationCatalog.Find(segment.OriginCode);var b=TravelLocationCatalog.Find(segment.DestinationCode);double km=MockRouteRules.Km(a,b);flown+=km;
                Assert.LessOrEqual(km,MockRouteRules.MaximumLegKm);Assert.GreaterOrEqual(segment.DurationMinutes,45);
                var carrier=MockFlightGenerator.Carriers.Single(c=>c.Code==segment.FlightNumber.Split(' ')[0]);Assert.IsTrue(MockFlightGenerator.Supports(carrier,a,b),segment.Description);
                Assert.Greater(segment.ArrivalUtc,segment.DepartureUtc);Assert.AreEqual(AirportClock.Local(segment.DepartureUtc,a.code),segment.DepartureLocal);
            }
            Assert.LessOrEqual(flown,MockRouteRules.Km(from,to)*1.45+250);
            for(int i=0;i<flight.Layovers.Count;i++)Assert.AreEqual(flight.Layovers[i].DurationMinutes,(flight.Segments[i+1].DepartureUtc-flight.Segments[i].ArrivalUtc).TotalMinutes);
            Assert.IsTrue(flight.Layovers.All(l=>l.DurationMinutes>=65));
        }
    }
    [Test] public void SydneyFromNewYorkRequiresConnectionsAndNeverUsesJetBlue()
    {
        var flights=MockFlightGenerator.Generate(TravelLocationCatalog.Find("JFK"),TravelLocationCatalog.Find("SYD"),Date);
        Assert.IsTrue(flights.All(f=>f.Stops>=1 && f.Code!="B6"));Assert.IsFalse(flights.Any(f=>f.Segments.Any(s=>s.Airline=="Delta Air Lines" && (s.OriginCode=="SFO"&&s.DestinationCode=="SYD"||s.OriginCode=="SYD"&&s.DestinationCode=="SFO"))));Assert.IsTrue(flights.Any(f=>f.Code=="QF"));
        Assert.That(flights.Min(f=>f.DurationMinutes),Is.InRange(20*60,28*60));Assert.GreaterOrEqual(flights.Min(f=>f.Price),750);
    }
    [Test] public void DistanceDateAndStopsAffectFaresWithoutMakingEveryConnectionCheaper()
    {
        var jfk=TravelLocationCatalog.Find("JFK");var bos=TravelLocationCatalog.Find("BOS");var lax=TravelLocationCatalog.Find("LAX");var hnd=TravelLocationCatalog.Find("HND");
        Assert.Less(MockTravelPricing.EconomyBasis(jfk,bos,Date),MockTravelPricing.EconomyBasis(jfk,lax,Date));Assert.Less(MockTravelPricing.EconomyBasis(jfk,lax,Date),MockTravelPricing.EconomyBasis(jfk,hnd,Date));
        var flights=MockFlightGenerator.Generate(jfk,lax,Date);Assert.IsTrue(flights.Any(f=>f.Stops==0));Assert.IsTrue(flights.Any(f=>f.Stops==1));Assert.IsTrue(flights.Any(f=>f.Stops==2));
        Assert.Less(flights.Where(f=>f.Stops>0).Min(f=>f.Price),flights.Where(f=>f.Stops==0).Min(f=>f.Price));Assert.Greater(flights.Where(f=>f.Stops>0).Max(f=>f.Price),flights.Where(f=>f.Stops==0).Min(f=>f.Price));
        Assert.Greater(Enumerable.Range(0,60).Select(i=>MockTravelPricing.CalendarEstimate(jfk,lax,Date.AddDays(i))).Distinct().Count(),8);
    }
    [Test] public void PlanningAndCalendarUseTheSameEconomyBasisAndCabinTravelerRatesApplyOnce()
    {
        var a=TravelLocationCatalog.Find("JFK");var b=TravelLocationCatalog.Find("HND");
        Assert.AreEqual(MockTravelPricing.PlanningEstimate(a,b),DemoFlightPriceDataProvider.CreateOffer(a,b).Amount);
        Assert.AreEqual(MockTravelPricing.CalendarEstimate(a,b,Date),DateFareEstimates.EconomyFare(Date,a.code,b.code));
        var flight=MockFlightGenerator.Generate(a,b,Date)[0];var party=PassengerParty.Adults(1);party.SetCounts(1,1,1,1);
        foreach(float multiplier in new[]{1f,1.35f,2.2f,3.25f})
        {int adult=UnityEngine.Mathf.RoundToInt(flight.Price*multiplier);Assert.AreEqual(party.travelers.Sum(t=>party.FareFor(adult,t.category)),party.TotalFare(adult));Assert.Greater(party.TotalFare(adult),adult);}
    }
    [Test] public void InvalidEndpointsDoNotInventFlightResults()
    {
        Assert.IsEmpty(MockFlightGenerator.Generate(null,TravelLocationCatalog.Find("LAX"),Date));Assert.IsEmpty(MockFlightGenerator.Generate(TravelLocationCatalog.Find("JFK"),TravelLocationCatalog.Find("EWR"),Date));
        Assert.AreEqual(2,MockFlightGenerator.ExpectedSegments("UA-107"));Assert.AreEqual(3,MockFlightGenerator.ExpectedSegments("B6-110"));
    }
}
