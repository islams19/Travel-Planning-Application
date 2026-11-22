using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace TravelApp.FlightMap.Tests
{
    public sealed class SharedLocationCatalogTests
    {
        [Test] public void EveryBookingCityHasUniqueValidAirportMetadataAndCanonicalLabels()
        {
            var all=TravelLocationCatalog.Airports;
            Assert.AreEqual(62,TravelLocationCatalog.BookingDestinations.Count,"Preserve the existing Search Flights city set");
            Assert.AreEqual(all.Count,all.Select(location=>location.code).Distinct().Count());
            foreach(var city in TravelLocationCatalog.BookingDestinations)
                Assert.IsTrue(all.Any(location=>location.BookingDestination==city));
            foreach(var location in all)
            {
                Assert.AreEqual(3,location.code.Length);Assert.IsNotEmpty(location.airport);Assert.IsNotEmpty(location.city);Assert.IsNotEmpty(location.country);
                Assert.That(location.latitude,Is.InRange(-90d,90d));Assert.That(location.longitude,Is.InRange(-180d,180d));
            }
            Assert.AreEqual("Bali, Indonesia",TravelLocationCatalog.Find("DPS").BookingDestination);
            Assert.AreEqual("Santorini, Greece",TravelLocationCatalog.Find("JTR").BookingDestination);
            Assert.AreEqual(TravelLocationCatalog.Find("HND").BookingDestination,TravelLocationCatalog.Find("NRT").BookingDestination);
            Assert.AreEqual(TravelLocationCatalog.Find("JFK").BookingDestination,TravelLocationCatalog.Find("EWR").BookingDestination);
        }
        [Test] public void EveryValidAirportPairHasAStableExplicitlyDemoFare()
        {
            var go=new GameObject("Demo provider test");
            try
            {
                var provider=go.AddComponent<DemoFlightPriceDataProvider>();
                foreach(var origin in TravelLocationCatalog.Airports)
                {
                    var offers=provider.GetOffers(origin);
                    Assert.AreEqual(TravelLocationCatalog.Airports.Count-1,offers.Count);
                    Assert.AreEqual(offers.Count,offers.Select(offer=>offer.Destination.code).Distinct().Count());
                    foreach(var offer in offers)
                    {
                        Assert.AreNotEqual(origin.code,offer.Destination.code);Assert.IsTrue(offer.IsDemo);
                        Assert.Greater(offer.Amount,0);Assert.AreEqual("USD",offer.Currency);
                        Assert.AreEqual(offer.Amount,DemoFlightPriceDataProvider.CreateOffer(origin,offer.Destination).Amount);
                    }
                }
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}
