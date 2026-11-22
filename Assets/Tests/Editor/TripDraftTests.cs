using System;
using System.IO;
using NUnit.Framework;
using TravelApp;
using UnityEngine;

public class TripDraftTests
{
    TravelCatalog catalog;
    User user;
    string path;
    [SetUp] public void Setup()
    {
        catalog=ScriptableObject.CreateInstance<TravelCatalog>();user=ScriptableObject.CreateInstance<User>();
        user.username="DraftUser";user.password="test";user.email="draft@example.com";catalog.users.Add(user);
        path=Path.Combine(Path.GetTempPath(),"travel-draft-test-"+Guid.NewGuid().ToString("N"),"save.json");
    }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(user);UnityEngine.Object.DestroyImmediate(catalog);
        if(Directory.Exists(Path.GetDirectoryName(path)))Directory.Delete(Path.GetDirectoryName(path),true);
    }
    [Test] public void DraftsPersistAcrossRestartUpdateInPlaceAndRemainAccountScoped()
    {
        var app=new AppServices(catalog,new JsonFileStore(path));Assert.IsTrue(app.Accounts.Login("DraftUser","test").success);
        var draft=new TripDraft{origin="JFK",destination="Tokyo, Japan",passengers=2,cabin="Business",step=TripDraft.Step.DepartureSeats,
            departureTicks=new DateTime(2027,1,12).Ticks,returnTicks=new DateTime(2027,1,19).Ticks};
        draft.outbound.airlineCode="B6";draft.outbound.seats=new[]{"1A",null};
        Assert.IsTrue(app.SaveDraft(draft).success);draft.passengers=3;Assert.IsTrue(app.SaveDraft(draft).success);
        Assert.AreEqual(1,app.DraftTrips.Count);Assert.AreEqual(3,app.DraftTrips[0].passengers);
        var restored=new AppServices(catalog,new JsonFileStore(path));Assert.IsTrue(restored.Accounts.Login("DraftUser","test").success);
        Assert.AreEqual(1,restored.DraftTrips.Count);Assert.AreEqual(draft.id,restored.DraftTrips[0].id);
        Assert.AreEqual(draft.Departure,restored.DraftTrips[0].Departure);Assert.AreEqual(draft.Return,restored.DraftTrips[0].Return);
        Assert.AreEqual("1A",restored.DraftTrips[0].outbound.seats[0]);
        var detached=restored.DraftTrips[0];detached.destination="Changed outside service";
        Assert.AreEqual("Tokyo, Japan",restored.DraftTrips[0].destination,"Snapshots must not mutate stored drafts");
        restored.Accounts.Logout();Assert.AreEqual(0,restored.DraftTrips.Count);
        Assert.IsFalse(restored.SaveDraft(draft).success,"Another owner cannot overwrite a draft ID");
        restored.Accounts.Login("DraftUser","test");Assert.IsTrue(restored.RemoveDraft(draft.id).success);
        Assert.AreEqual(0,restored.DraftTrips.Count);
    }
    [Test] public void NeedsPaymentRequiresCompleteFlightsAndSeatsRatherThanJustAPaymentStep()
    {
        var draft=new TripDraft{origin="New York, New York (JFK)",destination="Tokyo, Japan",departureTicks=new DateTime(2027,1,12).Ticks,
            returnTicks=new DateTime(2027,1,19).Ticks,passengers=2,cabin="Business",step=TripDraft.Step.Payment};
        Assert.IsFalse(draft.NeedsPayment);
        draft.outbound=new TripDraft.Leg{airlineCode="B6",farePerTraveler=416,confirmed=true,seats=new[]{"1A","1B"}};
        draft.inbound=new TripDraft.Leg{airlineCode="DL",farePerTraveler=548,confirmed=true,seats=new[]{"2A","2C"}};
        Assert.IsTrue(draft.NeedsPayment);
        draft.inbound.seats=new[]{"2A","2A"};Assert.IsFalse(draft.NeedsPayment,"Duplicate seats cannot be ready for payment");
        draft.oneWay=true;Assert.IsTrue(draft.NeedsPayment,"One-way trips need only the outbound leg");
        draft.outbound.confirmed=false;Assert.IsFalse(draft.NeedsPayment);
    }
    [Test] public void ConnectingDraftNeedsEveryAircraftSeatAndDoesNotBroadcastLegacySeats()
    {
        var date=new DateTime(2027,7,14);
        var draft=new TripDraft{origin="New York, New York (JFK)",destination="Cancún, Mexico",departureTicks=date.Ticks,
            passengers=2,cabin="Economy",oneWay=true};
        draft.outbound=new TripDraft.Leg{airlineCode="UA",itineraryId="UA-107",farePerTraveler=238,confirmed=true,seats=new[]{"1A","1B"}};
        Assert.IsFalse(draft.NeedsPayment,"A legacy first-aircraft seat list cannot complete a connecting itinerary");
        draft.outbound.segments=new[]{
            new TripDraft.Segment{originCode="JFK",destinationCode="ATL",departureUtcTicks=date.Ticks,arrivalUtcTicks=date.AddHours(2).Ticks,seats=new[]{"1A","1B"}},
            new TripDraft.Segment{originCode="ATL",destinationCode="CUN",departureUtcTicks=date.AddHours(3).Ticks,arrivalUtcTicks=date.AddHours(5).Ticks,seats=new[]{"1A","1B"}}
        };
        Assert.IsTrue(draft.NeedsPayment,"Seat numbers can repeat on different aircraft");
        draft.outbound.segments[1].seats=new[]{"1A",(string)null}; Assert.IsFalse(draft.NeedsPayment);
        draft.outbound.segments[1].seats=new[]{"1A","1A"}; Assert.IsFalse(draft.NeedsPayment);
    }
    [Test] public void GuestDraftsRemainAvailableOnThisDeviceAfterRestart()
    {
        var app=new AppServices(catalog,new JsonFileStore(path));
        var draft=new TripDraft{destination="Tokyo, Japan"};Assert.IsTrue(app.SaveDraft(draft).success);
        var restored=new AppServices(catalog,new JsonFileStore(path));Assert.AreEqual(1,restored.DraftTrips.Count);
        var updated=restored.DraftTrips[0];updated.passengers=2;Assert.IsTrue(restored.SaveDraft(updated).success);
        Assert.AreEqual(1,restored.DraftTrips.Count);Assert.AreEqual(2,restored.DraftTrips[0].passengers);
        restored.Accounts.Login("DraftUser","test");Assert.AreEqual(0,restored.DraftTrips.Count);
        restored.Accounts.Logout();Assert.IsTrue(restored.RemoveDraft(draft.id).success);Assert.AreEqual(0,restored.DraftTrips.Count);
    }
    [Test] public void OlderSaveWithoutDraftFieldLoadsWithoutLosingAccounts()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path,"{\"version\":1,\"accounts\":[],\"reviews\":[]}");
        var app=new AppServices(catalog,new JsonFileStore(path));Assert.AreEqual(0,app.DraftTrips.Count);
        Assert.IsTrue(app.Accounts.Login("DraftUser","test").success);
        Assert.IsTrue(app.SaveDraft(new TripDraft{destination="Tokyo, Japan"}).success);
        Assert.AreEqual(1,app.DraftTrips.Count);
    }
}
