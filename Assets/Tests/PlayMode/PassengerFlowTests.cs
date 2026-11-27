using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using TravelApp;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;
using UIButton=UnityEngine.UI.Button;

public sealed class PassengerFlowTests
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Private).GetValue(o);
    static object Invoke(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Private).Invoke(o,args);
    static UIButton Button(Transform root,string n)=>root.GetComponentsInChildren<UIButton>().First(b=>b.name==n);
    static string Text(Transform root)=>string.Join("\n",root.GetComponentsInChildren<TMP_Text>(true).Select(t=>t.text));
    static void Select(FlightResultsPage results,string id)
    {
        var f=Field<List<FlightResultsPage.Flight>>(results,"flights").Single(x=>MockFlightTestData.Matches(x,id));
        Button((RectTransform)f.GetType().GetProperty("CardView",Private).GetValue(f),"Select").onClick.Invoke();
    }
    IEnumerator Click(UIButton b)
    {
        yield return null; // Let newly resumed pages register their graphics before the pointer event.
        Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Assert.IsNotEmpty(hits);Assert.AreEqual(b.gameObject,ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),b.name+" must receive pointer clicks; top hit="+hits[0].gameObject.name+" point="+e.position);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,e,ExecuteEvents.pointerClickHandler);yield return null;
    }
    Keyboard keyboard;AppServices original;TravelCatalog catalog;CompletedDemoBooking receipt;
    sealed class Store:ISaveStore {string json;public SaveData Load()=>json==null?new SaveData():JsonUtility.FromJson<SaveData>(json);public void Save(SaveData data)=>json=JsonUtility.ToJson(data);}
    void Isolate()
    {
        keyboard=InputSystem.AddDevice<Keyboard>();var r=TravelAppRuntime.Instance;original=r.Services;catalog=ScriptableObject.CreateInstance<TravelCatalog>();
        var service=new AppServices(catalog,new Store());service.Accounts.Register("PassengerFlowUser","family@example.com","test-pass");typeof(TravelAppRuntime).GetProperty("Services").SetValue(r,service);
    }
    [TearDown] public void Cleanup()
    {
        if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
        if(TravelAppRuntime.Instance!=null)
        {if(receipt!=null)Field<List<CompletedDemoBooking>>(TravelAppRuntime.Instance,"completedFlights").Remove(receipt);if(original!=null)typeof(TravelAppRuntime).GetProperty("Services").SetValue(TravelAppRuntime.Instance,original);}
        if(catalog!=null)Object.DestroyImmediate(catalog);
    }
    IEnumerator Press(params Key[] keys)
    {InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;}
    [UnityTest,Timeout(60000)] public IEnumerator MixedFamilyRoundTripConnectionsDetailsDraftPaymentAndHistoryKeepOneStateAndPrice()
    {
        yield return SceneManager.LoadSceneAsync("DesktopLayout");yield return null;yield return null;Isolate();
        var nav=Object.FindFirstObjectByType<SidebarNavigation>();var search=Field<GameObject[]>(nav,"pages")[0];var results=Field<FlightResultsPage>(nav,"flightResults");
        yield return Click(Button(search.transform,"Search travelers"));
        Assert.IsFalse(Button(search.transform,"Fewer travelers").interactable);
        yield return Click(Button(search.transform,"More Child"));yield return Click(Button(search.transform,"More InfantInSeat"));yield return Click(Button(search.transform,"More InfantOnLap"));
        Assert.IsFalse(Button(search.transform,"More InfantOnLap").interactable);Assert.AreEqual(4,results.PassengerCount);Assert.AreEqual(3,results.Passengers.SeatCount);
        yield return Click(Button(search.transform,"Done travelers"));StringAssert.Contains("4 travelers",Button(search.transform,"Search travelers").GetComponentInChildren<TMP_Text>().text);
        Invoke(results,"SetBookingOptions",4,"Business");Assert.AreEqual(1,results.Passengers.CountFor(PassengerParty.Category.Child),"Cabin changes must preserve categories");
        nav.SearchExploreDestination("CUN");var dep=new DateTime(2027,7,14);var ret=dep.AddDays(7);Invoke(Field<DateSelectionPage>(nav,"datePage"),"RestoreDates",(DateTime?)dep,(DateTime?)ret,false);Invoke(nav,"ShowFlightResults",dep,(DateTime?)ret);
        var connecting=Field<List<FlightResultsPage.Flight>>(results,"flights").Single(f=>MockFlightTestData.Matches(f,"UA-107"));int outboundTotal=results.Passengers.TotalFare((int)Invoke(results,"AdjustFare",connecting.Price));
        StringAssert.Contains(outboundTotal.ToString("N0"),Text((RectTransform)connecting.GetType().GetProperty("CardView",Private).GetValue(connecting)));
        Select(results,"UA-107");var seats=Field<SeatSelectionPage>(nav,"seatPage");Assert.AreEqual(3,nav.CurrentSeatBooking.Seats.Count);Assert.AreEqual(4,nav.CurrentSeatBooking.PassengerCount);
        for(int segment=0;segment<2;segment++)
        {
            seats.SelectSegment(segment);Button(seats.transform,"Seat 1A").onClick.Invoke();Button(seats.transform,"Seat 1B").onClick.Invoke();
            Assert.IsFalse(Button(seats.transform,"Confirm Seat").interactable);
            var booking=nav.CurrentSeatBooking;Invoke(booking,"Assign",1,"1A",segment);Assert.AreEqual("1B",booking.SeatsForSegment(segment)[1],"Duplicate assignments cannot overwrite another traveler");
            Button(seats.transform,"Seat 1D").onClick.Invoke();
        }
        Assert.AreEqual(outboundTotal,nav.CurrentSeatBooking.EstimatedTotal);StringAssert.Contains("3/3 seats selected",Text(seats.transform));
        Button(seats.transform,"Confirm Seat").onClick.Invoke();var returns=Field<FlightResultsPage>(nav,"returnResults");Assert.AreSame(results.Passengers,returns.Passengers);
        Select(returns,"B6-100");var returnSeats=Field<SeatSelectionPage>(nav,"returnSeats");foreach(string code in new[]{"1A","1B","1D"})Button(returnSeats.transform,"Seat "+code).onClick.Invoke();Button(returnSeats.transform,"Confirm Seat").onClick.Invoke();
        var review=Field<TripReviewPage>(nav,"tripReview");var form=review.GetComponentInChildren<TravelerDetailsForm>();Assert.AreEqual("family@example.com",results.Passengers.email);
        var first=form.GetComponentsInChildren<TMP_InputField>(true).First(f=>f.name=="Traveler 1 first name");Canvas.ForceUpdateCanvases();
        Assert.AreEqual(((RectTransform)first.transform).rect.width-28,first.textViewport.rect.width,1,"Name entry must use the full padded field width");first.text="Ana";
        Assert.IsTrue(nav.SaveActiveDraft().success);var saved=TravelAppRuntime.Instance.Services.DraftTrips.Single();Assert.AreEqual("Ana",saved.PassengerDetails.travelers[0].firstName);Assert.AreEqual(3,saved.PassengerDetails.SeatCount);Assert.IsTrue(saved.NeedsPayment,"Lap infants must not make a fully seated draft look incomplete");
        nav.ResumeDraft(saved);review=Field<TripReviewPage>(nav,"tripReview");form=review.GetComponentInChildren<TravelerDetailsForm>();
        Assert.IsFalse((bool)Invoke(nav,"ContinueToPayment"));
        yield return Click(Button(review.transform,"Continue to Payment"));Assert.IsTrue(review.gameObject.activeInHierarchy);
        Assert.AreEqual("Traveler 1 last name",EventSystem.current.currentSelectedGameObject.name);
        first=form.GetComponentsInChildren<TMP_InputField>().First(f=>f.name=="Traveler 1 first name");var last=form.GetComponentsInChildren<TMP_InputField>().First(f=>f.name=="Traveler 1 last name");
        KeyboardFocusNavigation.Instance.Focus(first);yield return null;yield return Press(Key.Tab);Assert.AreEqual(last.gameObject,EventSystem.current.currentSelectedGameObject);yield return Press(Key.LeftShift,Key.Tab);Assert.AreEqual(first.gameObject,EventSystem.current.currentSelectedGameObject);
        foreach(var f in form.GetComponentsInChildren<TMP_InputField>(true))
        {f.text=f.name=="Booking email"?"family@example.com":f.name=="Booking phone"?"+1 212 555 0100":f.name.EndsWith("first name")?"Ana":"Traveler";}
        var email=form.GetComponentsInChildren<TMP_InputField>().First(f=>f.name=="Booking email");KeyboardFocusNavigation.Instance.Focus(email);yield return null;yield return Press(Key.Tab);Assert.AreEqual("Booking phone",EventSystem.current.currentSelectedGameObject.name);
        int total=nav.CurrentSeatBooking.EstimatedTotal+nav.CurrentReturnSeatBooking.EstimatedTotal;Assert.AreEqual(total,review.GrandTotal);
        yield return Click(Button(review.transform,"Continue to Payment"));var payment=Field<PaymentPlaceholderPage>(nav,"paymentPage");Assert.IsTrue(payment.gameObject.activeInHierarchy);Assert.AreEqual(total,payment.Booking.EstimatedTotal);Assert.AreEqual(4,payment.Booking.Passengers.Count);Assert.AreEqual(3,payment.Booking.Outbound.SegmentSeats[1].Count);
        foreach(var pair in new[]{("nameInput","Ana Traveler"),("numberInput","4242424242424242"),("expirationInput","12/30"),("cvvInput","123"),("postalInput","10001")})Field<TMP_InputField>(payment,pair.Item1).text=pair.Item2;
        Invoke(payment,"Submit");receipt=TravelAppRuntime.Instance.CompletedFlights.Single();Assert.AreEqual(total,receipt.Booking.EstimatedTotal);Assert.AreEqual("Ana",receipt.Booking.Passengers.travelers[0].firstName);StringAssert.Contains("Infant on lap",CompletedDemoBooking.Itinerary(receipt.Booking));
        nav.OpenSavedTrips();var trips=Object.FindFirstObjectByType<SavedTripsPage>();Button(trips.transform,"Filter Paid").onClick.Invoke();Button(trips.transform,"View details").onClick.Invoke();StringAssert.Contains("family@example.com",Text(Field<DemoBookingPages>(nav,"confirmationPage").transform));
    }
    [UnityTest,Timeout(30000)] public IEnumerator OneAdultAndLapInfantNeedOneSeatAndKeepNamesWhenReturningToSearch()
    {
        yield return SceneManager.LoadSceneAsync("DesktopLayout");yield return null;yield return null;Isolate();var nav=Object.FindFirstObjectByType<SidebarNavigation>();var results=Field<FlightResultsPage>(nav,"flightResults");
        Assert.IsTrue(results.SetPassengerCategories(1,0,0,1));results.Passengers.travelers[0].firstName="Alex";
        nav.SearchExploreDestination("MIA");var dep=new DateTime(2027,8,2);Invoke(Field<DateSelectionPage>(nav,"datePage"),"RestoreDates",(DateTime?)dep,null,true);Invoke(nav,"ShowFlightResults",dep,(DateTime?)null);Select(results,"B6-100");
        var seats=Field<SeatSelectionPage>(nav,"seatPage");Assert.AreEqual(1,nav.CurrentSeatBooking.SeatCount);Assert.AreEqual(2,nav.CurrentSeatBooking.PassengerCount);Button(seats.transform,"Seat 1A").onClick.Invoke();Assert.IsTrue(Button(seats.transform,"Confirm Seat").interactable);
        Assert.AreEqual(nav.CurrentSeatBooking.FarePerTraveler+(int)Math.Round(nav.CurrentSeatBooking.FarePerTraveler*.1m,MidpointRounding.AwayFromZero),nav.CurrentSeatBooking.EstimatedTotal);
        Button(seats.transform,"Confirm Seat").onClick.Invoke();Assert.IsTrue(Field<TripReviewPage>(nav,"tripReview").gameObject.activeInHierarchy);Field<UIButton[]>(nav,"buttons")[0].onClick.Invoke();Assert.AreEqual("Alex",results.Passengers.travelers[0].firstName);Assert.AreEqual(1,results.Passengers.CountFor(PassengerParty.Category.InfantOnLap));
    }
}
