using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class PassengerPartyTests
{
    [Test] public void CategoriesEnforceAdultsLapRatioAndExistingTravelerLimit()
    {
        var p=PassengerParty.Adults(1);
        Assert.IsFalse(p.SetCounts(0,1,0,0));Assert.IsFalse(p.SetCounts(1,0,0,2));Assert.IsFalse(p.SetCounts(2,3,1,1));Assert.IsFalse(p.SetCounts(1,-1,0,0));
        Assert.AreEqual(1,p.Count);Assert.IsTrue(p.SetCounts(2,1,1,2));Assert.AreEqual(6,p.Count);Assert.AreEqual(4,p.SeatCount);Assert.IsTrue(p.IsValid);
        p.travelers[0].firstName="Ana";Assert.IsTrue(p.SetCounts(2,0,1,1));Assert.AreEqual("Ana",p.travelers[0].firstName);
        Assert.IsFalse(p.SetCounts(1,0,0,2));Assert.AreEqual(4,p.Count);
    }
    [Test] public void ConfigurableCategoryRatesApplyAfterCabinAndRoundPerTravelerDeterministically()
    {
        var p=PassengerParty.Adults(1);p.SetCounts(1,1,1,1);
        Assert.AreEqual(100,p.FareFor(100,PassengerParty.Category.Adult));Assert.AreEqual(75,p.FareFor(100,PassengerParty.Category.Child));Assert.AreEqual(75,p.FareFor(100,PassengerParty.Category.InfantInSeat));Assert.AreEqual(10,p.FareFor(100,PassengerParty.Category.InfantOnLap));Assert.AreEqual(260,p.TotalFare(100));
        int businessAdult=220;Assert.AreEqual(572,p.TotalFare(businessAdult));Assert.AreEqual(19,p.FareFor(189,PassengerParty.Category.InfantOnLap));
        p.rates.child=50;Assert.AreEqual(50,p.FareFor(100,PassengerParty.Category.Child));Assert.AreEqual(235,p.TotalFare(100));
        Assert.AreEqual(p.TotalFare(415),p.Clone().TotalFare(415));
    }
    [Test] public void LegacyDraftsMigrateAsAdultsAndNewDraftsKeepPartialDetailsWithoutRequiringPayment()
    {
        var legacy=JsonUtility.FromJson<TripDraft>("{\"passengers\":3}");Assert.AreEqual(3,legacy.PassengerDetails.Count);Assert.AreEqual(3,legacy.PassengerDetails.SeatCount);
        var p=PassengerParty.Adults(1);p.SetCounts(1,1,0,1);p.travelers[0].firstName="José";p.email="name@example.com";
        var draft=JsonUtility.FromJson<TripDraft>(JsonUtility.ToJson(new TripDraft{passengers=3,passengerDetails=p,passengerDetailsVersion=1}));
        Assert.AreEqual(2,draft.PassengerDetails.SeatCount);Assert.AreEqual("José",draft.PassengerDetails.travelers[0].firstName);Assert.AreEqual("name@example.com",draft.PassengerDetails.email);Assert.IsFalse(draft.PassengerDetails.HasValidDetails);
    }
    [Test] public void LapInfantsDoNotBlockNeedsPaymentCategoryWhenEveryRequiredSeatIsAssigned()
    {
        var p=PassengerParty.Adults(1);p.SetCounts(1,0,0,1);
        var draft=new TripDraft{origin="New York, New York (JFK)",destination="Miami, Florida",departureTicks=new DateTime(2027,7,14).Ticks,oneWay=true,passengers=2,passengerDetails=p,passengerDetailsVersion=1,
            outbound=new TripDraft.Leg{airlineCode="B6",itineraryId="B6-100",farePerTraveler=189,confirmed=true,seats=new[]{"1A"}}};
        Assert.IsTrue(draft.NeedsPayment);draft.outbound.seats=new[]{"1A","1B"};Assert.IsFalse(draft.NeedsPayment);
    }
    [TestCase("Zoë",true)] [TestCase("Anne-Marie",true)] [TestCase("O’Neill",true)] [TestCase("",false)] [TestCase("123",false)] [TestCase("<b>A</b>",false)]
    public void NamesAllowHumanNamesAndRejectMarkupOrMissingValues(string value,bool valid)=>Assert.AreEqual(valid,PassengerParty.NameError(value)==null);
    [Test] public void ContactValidationRequiresEmailAndPhoneButNeverInventsTravelerNames()
    {
        var p=PassengerParty.Adults(1);Assert.IsFalse(p.HasValidDetails);p.travelers[0].firstName="Jane";p.travelers[0].lastName="Doe";p.email="jane@example.com";p.phone="+1 (212) 555-0100";Assert.IsTrue(p.HasValidDetails);
        p.phone="123";Assert.IsFalse(p.HasValidDetails);p.phone="2125550100";p.email="bad";Assert.IsFalse(p.HasValidDetails);
    }
}
