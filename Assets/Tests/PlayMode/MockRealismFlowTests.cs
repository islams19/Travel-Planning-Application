using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TravelApp;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;
using UIButton=UnityEngine.UI.Button;

public sealed class MockRealismFlowTests
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Private).GetValue(o);
    static object Invoke(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Private).Invoke(o,args);
    static UIButton Button(Transform root,string n)=>root.GetComponentsInChildren<UIButton>().First(b=>b.name==n);
    sealed class Store:ISaveStore {string json;public SaveData Load()=>json==null?new SaveData():JsonUtility.FromJson<SaveData>(json);public void Save(SaveData d)=>json=JsonUtility.ToJson(d);}
    AppServices original;TravelCatalog catalog;
    void Isolate(){var r=TravelAppRuntime.Instance;original=r.Services;catalog=ScriptableObject.CreateInstance<TravelCatalog>();typeof(TravelAppRuntime).GetProperty("Services").SetValue(r,new AppServices(catalog,new Store()));}
    [TearDown] public void Cleanup(){if(original!=null&&TravelAppRuntime.Instance!=null)typeof(TravelAppRuntime).GetProperty("Services").SetValue(TravelAppRuntime.Instance,original);if(catalog!=null)Object.DestroyImmediate(catalog);}
    [UnityTest,Timeout(60000)] public IEnumerator LongHaulFamilyPricesAndSeatsPersistAcrossEveryCabinAndExactDraftResume()
    {
        yield return SceneManager.LoadSceneAsync("DesktopLayout");yield return null;yield return null;Isolate();
        var nav=Object.FindFirstObjectByType<SidebarNavigation>();var results=Field<FlightResultsPage>(nav,"flightResults");nav.SearchExploreDestination("SYD");
        var date=new DateTime(2027,7,14);Invoke(Field<DateSelectionPage>(nav,"datePage"),"RestoreDates",(DateTime?)date,null,true);
        Assert.IsTrue(results.SetPassengerCategories(1,1,1,1));PassengerTestData.Complete(nav);
        foreach(string cabin in new[]{"Economy","Premium Economy","Business","First"})
        {
            Invoke(results,"SetBookingOptions",4,cabin);Invoke(nav,"ShowFlightResults",date,(DateTime?)null);yield return null;
            var all=Field<List<FlightResultsPage.Flight>>(results,"flights");Assert.IsTrue(all.All(f=>f.Stops>0&&f.Code!="B6"));
            Assert.GreaterOrEqual(Field<Slider>(results,"priceSlider").maxValue,all.Max(f=>(int)Invoke(results,"AdjustFare",f.Price)));
            var flight=all.First();Button((RectTransform)flight.GetType().GetProperty("CardView",Private).GetValue(flight),"Select").onClick.Invoke();
            var seats=Field<SeatSelectionPage>(nav,"seatPage");int adult=(int)Invoke(results,"AdjustFare",flight.Price);int expected=results.Passengers.TotalFare(adult);
            Assert.AreEqual(adult,nav.CurrentSeatBooking.FarePerTraveler);Assert.AreEqual(expected,nav.CurrentSeatBooking.EstimatedTotal);Assert.AreEqual(3,nav.CurrentSeatBooking.SeatCount);
            for(int segment=0;segment<flight.Segments.Count;segment++)
            {seats.SelectSegment(segment);for(int n=0;n<3;n++)seats.GetComponentsInChildren<UIButton>().First(b=>b.name.StartsWith("Seat ")&&b.interactable).onClick.Invoke();}
            Button(seats.transform,"Confirm Seat").onClick.Invoke();var review=Field<TripReviewPage>(nav,"tripReview");Assert.AreEqual(expected,review.GrandTotal);
            Assert.IsTrue(nav.SaveActiveDraft().success);var saved=TravelAppRuntime.Instance.Services.DraftTrips.Single();Assert.IsTrue(saved.NeedsPayment);
            nav.ResumeDraft(saved);Assert.AreEqual(flight.Id,results.SelectedDeparture.Id);Assert.AreEqual(expected,nav.CurrentSeatBooking.EstimatedTotal);Assert.IsTrue(nav.CurrentSeatBooking.Confirmed);
            Assert.IsTrue((bool)Invoke(nav,"ContinueToPayment"));var payment=Field<PaymentPlaceholderPage>(nav,"paymentPage");Assert.AreEqual(expected,payment.Booking.EstimatedTotal);Assert.AreEqual(adult,payment.Booking.Outbound.FarePerTraveler);Assert.AreEqual(4,payment.Booking.Passengers.Count);Assert.AreEqual(flight.Segments.Count,payment.Booking.Outbound.SegmentSeats.Count);
            Invoke(nav,"ShowPage",0);
        }
    }
    [UnityTest,Timeout(30000)] public IEnumerator OldMockFlightIdsRequireReselectionWithoutDiscardingRouteOrTravelers()
    {
        yield return SceneManager.LoadSceneAsync("DesktopLayout");yield return null;yield return null;Isolate();var service=TravelAppRuntime.Instance.Services;var date=new DateTime(2027,7,14);
        var party=PassengerParty.Adults(1);party.travelers[0].firstName="Alex";party.travelers[0].lastName="Morgan";party.email="alex@example.com";party.phone="2125550100";
        var draft=new TripDraft{origin="New York, New York (JFK)",originAirportCode="JFK",destination="Sydney, Australia",destinationAirportCode="SYD",departureTicks=date.Ticks,oneWay=true,passengerDetails=party,passengerDetailsVersion=1,step=TripDraft.Step.Review,outbound=new TripDraft.Leg{airlineCode="B6",itineraryId="B6-100",farePerTraveler=189,confirmed=true,seats=new[]{"1A"}}};
        Assert.IsTrue(service.SaveDraft(draft).success);var nav=Object.FindFirstObjectByType<SidebarNavigation>();nav.ResumeDraft(service.DraftTrips.Single());var results=Field<FlightResultsPage>(nav,"flightResults");
        Assert.IsTrue(results.gameObject.activeInHierarchy);Assert.IsNull(results.SelectedDeparture);Assert.AreEqual("Alex",results.Passengers.travelers[0].firstName);Assert.AreEqual("alex@example.com",results.Passengers.email);Assert.AreEqual(date,Field<DateTime?>(Field<DateSelectionPage>(nav,"datePage"),"departureDate"));
        Assert.IsFalse((bool)Invoke(nav,"ContinueToPayment"));Assert.AreEqual(1,service.DraftTrips.Count);Assert.IsTrue(Object.FindFirstObjectByType<AppNotification>().IsVisible);
    }
}
