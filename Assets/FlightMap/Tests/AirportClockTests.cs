using System;
using NUnit.Framework;

namespace TravelApp.FlightMap.Tests
{
    public class AirportClockTests
    {
        [Test] public void EveryAirportHasResolvableIanaTimeZone()
        {
            foreach(var airport in TravelLocationCatalog.Airports)
            {
                StringAssert.Contains("/",airport.TimeZoneId);
                Assert.IsNotNull(AirportClock.Zone(airport.code),airport.code);
            }
        }
        [TestCase("JFK",1,13)] [TestCase("JFK",7,12)]
        [TestCase("ATL",1,13)] [TestCase("ATL",7,12)]
        [TestCase("CUN",1,13)] [TestCase("CUN",7,13)]
        public void AirportLocalDepartureUsesDateAwareDst(string airport,int month,int utcHour)
        {
            var local=new DateTime(2027,month,15,8,0,0);
            DateTime utc=AirportClock.ToUtc(local,airport);
            Assert.AreEqual(new DateTime(2027,month,15,utcHour,0,0,DateTimeKind.Utc),utc);
            Assert.AreEqual(local,AirportClock.Local(utc,airport));
        }
        [Test] public void SpringGapAndAutumnOverlapFollowZoneRules()
        {
            Assert.Throws<ArgumentException>(()=>AirportClock.ToUtc(new DateTime(2027,3,14,2,30,0),"JFK"));
            Assert.AreEqual(new DateTime(2027,11,7,5,30,0,DateTimeKind.Utc),AirportClock.ToUtc(new DateTime(2027,11,7,1,30,0),"JFK"));
            Assert.AreEqual(new DateTime(2027,11,8,6,30,0,DateTimeKind.Utc),AirportClock.ToUtc(new DateTime(2027,11,8,1,30,0),"JFK"));
        }
        [Test] public void DurationAcrossSpringClockChangeUsesAbsoluteTime()
        {
            var arrival=AirportClock.ToUtc(new DateTime(2027,3,14,1,30,0),"JFK");
            var onward=AirportClock.ToUtc(new DateTime(2027,3,14,3,30,0),"JFK");
            Assert.AreEqual(60,(onward-arrival).TotalMinutes);
            Assert.AreEqual(120,(AirportClock.Local(onward,"JFK")-AirportClock.Local(arrival,"JFK")).TotalMinutes);
        }
        [Test] public void ArrivalDayIsBasedOnDestinationLocalDateRatherThanUtcMidnight()
        {
            var utc=new DateTime(2027,7,15,2,0,0,DateTimeKind.Utc);
            var departureDate=new DateTime(2027,7,14);
            Assert.AreEqual("7:00 PM",AirportClock.Format(utc,"LAX",departureDate));
            Assert.AreEqual("10:00 PM",AirportClock.Format(utc,"JFK",departureDate));
            Assert.AreEqual("11:00 AM (+1 day)",AirportClock.Format(utc,"HND",departureDate));
            Assert.AreEqual("7:00 PM (-1 day)",AirportClock.Format(utc,"LAX",departureDate.AddDays(1)));
        }
    }
}
