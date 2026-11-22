using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using TravelApp;
using TravelApp.FlightMap;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UIButton=UnityEngine.UI.Button;
using Object=UnityEngine.Object;

public class SegmentSeatFlowTests
{
    sealed class MemoryStore : ISaveStore
    {
        string json;
        public SaveData Load()=>json==null ? new SaveData() : JsonUtility.FromJson<SaveData>(json);
        public void Save(SaveData data)=>json=JsonUtility.ToJson(data);
    }
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
    static void Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Private).Invoke(target,args);
    static UIButton Button(Transform root,string name)=>root.GetComponentsInChildren<UIButton>(true).First(b=>b.name==name && b.gameObject.activeInHierarchy);
    static void Click(Transform root,string name)=>Button(root,name).onClick.Invoke();
    static void Select(FlightResultsPage page,string id)
    {
        var flight=Field<List<FlightResultsPage.Flight>>(page,"flights").Single(f=>f.Id==id);
        var card=(RectTransform)typeof(FlightResultsPage.Flight).GetProperty("CardView",Private).GetValue(flight);
        Click(card,"Select");
    }
    static string Text(Transform root)=>string.Join("\n",root.GetComponentsInChildren<TMP_Text>(true).Select(t=>t.text));
    AppServices original; TravelCatalog catalog; User user;
    [TearDown] public void Cleanup()
    {
        if(TravelAppRuntime.Instance!=null && original!=null) typeof(TravelAppRuntime).GetProperty("Services").SetValue(TravelAppRuntime.Instance,original);
        if(user!=null) Object.DestroyImmediate(user); if(catalog!=null) Object.DestroyImmediate(catalog);
    }
    [UnityTest,Timeout(45000)] public IEnumerator SegmentSeatsPersistAcrossSwitchesDraftReloadPaymentConfirmationAndHistory()
    {
        yield return SceneManager.LoadSceneAsync("DesktopLayout"); yield return null; yield return null;
        var runtime=TravelAppRuntime.Instance; original=runtime.Services;
        var store=new MemoryStore(); catalog=ScriptableObject.CreateInstance<TravelCatalog>(); user=ScriptableObject.CreateInstance<User>();
        user.username="SegmentFlowUser"; user.password="test-pass"; user.email="segments@example.com"; catalog.users.Add(user);
        var services=new AppServices(catalog,store); services.Accounts.Login(user.username,user.password);
        typeof(TravelAppRuntime).GetProperty("Services").SetValue(runtime,services);
        var nav=Object.FindFirstObjectByType<SidebarNavigation>();
        Assert.IsTrue(nav.SearchExploreDestination("CUN").success);
        var departure=new DateTime(2027,7,14); var returning=departure.AddDays(7);
        Invoke(Field<DateSelectionPage>(nav,"datePage"),"RestoreDates",(DateTime?)departure,(DateTime?)returning,false);
        Invoke(nav,"ShowFlightResults",departure,(DateTime?)returning);
        var results=Field<FlightResultsPage>(nav,"flightResults");
        Click(results.transform,"Modify Search"); Click(results.transform,"Passengers"); Click(results.transform,"Done");
        Select(results,"UA-107"); var seats=Field<SeatSelectionPage>(nav,"seatPage");
        Assert.AreEqual(2,nav.CurrentSeatBooking.SegmentSeats.Count); Assert.AreEqual(2,nav.CurrentSeatBooking.PassengerCount);
        Assert.AreEqual("JFK",nav.CurrentSeatBooking.Flight.Segments[0].OriginCode);
        Assert.AreEqual("ATL",nav.CurrentSeatBooking.Flight.Segments[0].DestinationCode);
        Assert.AreEqual("CUN",nav.CurrentSeatBooking.Flight.Segments[1].DestinationCode);
        foreach(var segment in nav.CurrentSeatBooking.Flight.Segments)
        {
            Assert.AreEqual(AirportClock.Local(segment.DepartureUtc,segment.OriginCode),segment.DepartureLocal);
            Assert.AreEqual(AirportClock.Local(segment.ArrivalUtc,segment.DestinationCode),segment.ArrivalLocal);
            Assert.AreEqual((int)(segment.ArrivalUtc-segment.DepartureUtc).TotalMinutes,segment.DurationMinutes);
        }
        seats.SelectSegment(0); Click(seats.transform,"Seat 1A"); Click(seats.transform,"Seat 1B");
        Assert.IsFalse(Button(seats.transform,"Confirm Seat").interactable,"First aircraft seats alone cannot complete a connection");
        Assert.IsTrue(nav.SaveActiveDraft().success);
        var partial=services.DraftTrips.Single(); Assert.IsFalse(partial.NeedsPayment);
        Assert.AreEqual(2,partial.outbound.segments.Length);
        nav.ResumeDraft(partial); seats=Field<SeatSelectionPage>(nav,"seatPage");
        CollectionAssert.AreEqual(new[]{"1A","1B"},nav.CurrentSeatBooking.SeatsForSegment(0));
        Assert.IsTrue(nav.CurrentSeatBooking.SeatsForSegment(1).All(s=>s==null));
        Click(seats.transform,"Segment 2"); Click(seats.transform,"Seat 1A");
        Click(seats.transform,"Seat 1A"); Assert.IsNull(nav.CurrentSeatBooking.SeatsForSegment(1)[1],"Duplicates on one aircraft must be rejected");
        Click(seats.transform,"Seat 1B"); // Identical numbers on a different aircraft are allowed.
        CollectionAssert.AreEqual(new[]{"1A","1B"},nav.CurrentSeatBooking.SeatsForSegment(1));
        Click(seats.transform,"Traveler 1"); Click(seats.transform,"Seat 2A");
        Click(seats.transform,"Traveler 2"); Click(seats.transform,"Seat 2B");
        Click(seats.transform,"Segment 1"); CollectionAssert.AreEqual(new[]{"1A","1B"},nav.CurrentSeatBooking.SeatsForSegment(0));
        Click(seats.transform,"Segment 2"); CollectionAssert.AreEqual(new[]{"2A","2B"},nav.CurrentSeatBooking.SeatsForSegment(1));
        Assert.IsTrue(Button(seats.transform,"Confirm Seat").interactable); Click(seats.transform,"Confirm Seat");
        var returns=Field<FlightResultsPage>(nav,"returnResults"); Assert.AreEqual(2,returns.PassengerCount);
        Select(returns,"UA-107"); var returnSeats=Field<SeatSelectionPage>(nav,"returnSeats");
        for(int i=0;i<nav.CurrentReturnSeatBooking.SegmentSeats.Count;i++)
        { returnSeats.SelectSegment(i); Click(returnSeats.transform,"Seat 3A"); Click(returnSeats.transform,"Seat 3B"); }
        Click(returnSeats.transform,"Confirm Seat");
        var review=Field<TripReviewPage>(nav,"tripReview"); Assert.IsTrue(review.gameObject.activeInHierarchy);
        foreach(var leg in new[]{nav.CurrentSeatBooking,nav.CurrentReturnSeatBooking}) foreach(var segment in leg.Flight.Segments)
            StringAssert.Contains(segment.OriginCode+" → "+segment.DestinationCode,Text(review.transform));
        foreach(string seat in new[]{"1A","1B","2A","2B","3A","3B"}) StringAssert.Contains("Seat "+seat,Text(review.transform));
        Assert.IsFalse(Text(review.transform).Contains("UTC"));
        Canvas.ForceUpdateCanvases();
        foreach(var body in review.GetComponentsInChildren<TMP_Text>().Where(t=>t.name=="Details"))
        {
            body.ForceMeshUpdate();
            Assert.GreaterOrEqual(body.rectTransform.rect.height,body.preferredHeight-1,"Review panels must fit every segment and seat row");
        }
        Assert.IsTrue(nav.SaveActiveDraft().success); var ready=services.DraftTrips.Single(); Assert.IsTrue(ready.NeedsPayment);
        var reloaded=new AppServices(catalog,store); reloaded.Accounts.Login(user.username,user.password);
        typeof(TravelAppRuntime).GetProperty("Services").SetValue(runtime,reloaded);
        ready=reloaded.DraftTrips.Single(); nav.ResumeDraft(ready);
        CollectionAssert.AreEqual(new[]{"1A","1B"},nav.CurrentSeatBooking.SeatsForSegment(0));
        CollectionAssert.AreEqual(new[]{"2A","2B"},nav.CurrentSeatBooking.SeatsForSegment(1));
        Assert.IsTrue(nav.CurrentSeatBooking.Confirmed); Assert.IsTrue(nav.CurrentReturnSeatBooking.Confirmed);
        foreach(var pair in nav.CurrentSeatBooking.Flight.Segments.Select((s,i)=>new{s,i}))
            Assert.AreEqual(pair.s.DepartureUtc.Ticks,ready.outbound.segments[pair.i].departureUtcTicks);
        Invoke(nav,"ContinueToPayment"); var payment=Field<PaymentPlaceholderPage>(nav,"paymentPage");
        Assert.IsTrue(payment.gameObject.activeInHierarchy); var snapshot=payment.Booking;
        Assert.AreEqual(2,snapshot.Outbound.SegmentSeats.Count); Assert.AreEqual(2,snapshot.Return.SegmentSeats.Count);
        CollectionAssert.AreEqual(new[]{"2A","2B"},snapshot.Outbound.SegmentSeats[1]);
        Assert.Throws<NotSupportedException>(()=>((IList<string>)snapshot.Outbound.SegmentSeats[1])[0]="9A");
        StringAssert.Contains(snapshot.Outbound.Baggage.Summary,Text(payment.transform));
        StringAssert.Contains(snapshot.Return.Baggage.Summary,Text(payment.transform));
        StringAssert.Contains("Seat 2B",Text(payment.transform)); Assert.IsFalse(Text(payment.transform).Contains("UTC"));
        var itineraryText=Field<TMP_Text>(payment,"itinerary"); itineraryText.ForceMeshUpdate();
        Assert.GreaterOrEqual(itineraryText.rectTransform.rect.height,itineraryText.preferredHeight-1,"Payment scroll content must fit the complete itinerary");
        var inputs=payment.GetComponentsInChildren<TMP_InputField>();
        inputs.First(i=>i.name=="Cardholder name").text="Segment Traveler";
        inputs.First(i=>i.name=="Card number").text="4242424242424242";
        inputs.First(i=>i.name=="Expiration date").text="12/39";
        inputs.First(i=>i.name=="Security code").text="123";
        inputs.First(i=>i.name=="ZIP / postal code").text="10001";
        Click(payment.transform,"Submit mock payment");
        var confirmation=Field<DemoBookingPages>(nav,"confirmationPage"); Assert.IsTrue(confirmation.gameObject.activeInHierarchy);
        StringAssert.Contains(snapshot.Outbound.Baggage.Summary,Text(confirmation.transform));
        StringAssert.Contains(snapshot.Return.Baggage.Summary,Text(confirmation.transform));
        StringAssert.Contains("Seat 2B",Text(confirmation.transform));
        var receipt=runtime.CompletedFlights.Single(); Assert.AreSame(snapshot,receipt.Booking);
        Click(confirmation.transform,"View Booked Trips");
        var trips=Object.FindFirstObjectByType<SavedTripsPage>(); Click(trips.transform,"View details");
        Assert.IsTrue(confirmation.gameObject.activeInHierarchy); StringAssert.Contains("Seat 2B",Text(confirmation.transform));
        StringAssert.Contains(snapshot.Outbound.Baggage.Summary,Text(confirmation.transform));
        StringAssert.Contains(snapshot.Return.Baggage.Summary,Text(confirmation.transform));
        Assert.AreEqual(snapshot.EstimatedTotal,receipt.Booking.EstimatedTotal); Assert.IsTrue(receipt.PaymentCompleted);
        var itinerary=CompletedDemoBooking.Itinerary(receipt.Booking);
        StringAssert.Contains("JFK → ATL",itinerary); StringAssert.Contains("ATL → CUN",itinerary);
        StringAssert.Contains("Seat 2A",itinerary); StringAssert.Contains("Seat 3A",itinerary);
        Invoke(nav.CurrentSeatBooking,"Assign",0,"5A",1);
        Assert.AreEqual("2A",receipt.Booking.Outbound.SegmentSeats[1][0],"A paid receipt must not share mutable seat rows with an editable booking");
    }
}
