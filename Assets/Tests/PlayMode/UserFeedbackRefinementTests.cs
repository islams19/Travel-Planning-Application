using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using TravelApp;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
using UIButton=UnityEngine.UI.Button;

// Covers the review-round changes: leave-booking prompt, partner airlines, review CTA and payment wording.
public class UserFeedbackRefinementTests
{
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
    static object Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Private).Invoke(o,args);
    static UIButton Button(Transform root,string name)=>root.GetComponentsInChildren<UIButton>().First(b=>b.name==name);
    sealed class Store:ISaveStore {public SaveData Load()=>new SaveData();public void Save(SaveData value){}}
    AppServices original;TravelCatalog catalog;
    SidebarNavigation Open()
    {
        var runtime=TravelAppRuntime.Instance;original=runtime.Services;catalog=ScriptableObject.CreateInstance<TravelCatalog>();
        typeof(TravelAppRuntime).GetProperty("Services").SetValue(runtime,new AppServices(catalog,new Store()));
        var nav=Object.FindFirstObjectByType<SidebarNavigation>();nav.SearchExploreDestination("CUN");var date=new DateTime(2027,7,14);
        Invoke(Field<DateSelectionPage>(nav,"datePage"),"RestoreDates",(DateTime?)date,null,true);Invoke(nav,"ShowFlightResults",date,(DateTime?)null);
        return nav;
    }
    [TearDown] public void Cleanup()
    {
        if(TravelAppRuntime.Instance!=null && original!=null)typeof(TravelAppRuntime).GetProperty("Services").SetValue(TravelAppRuntime.Instance,original);
        if(catalog!=null)Object.DestroyImmediate(catalog);
    }
    [UnityTest,Timeout(45000)] public IEnumerator SidebarAsksBeforeLeavingABookingInProgress()
    {
        yield return SceneManager.LoadSceneAsync("DesktopLayout");yield return null;yield return null;
        var nav=Open();var results=Field<FlightResultsPage>(nav,"flightResults");var dialog=Object.FindFirstObjectByType<AppDialog>();
        var sidebar=Field<UIButton[]>(nav,"buttons");var pages=Field<GameObject[]>(nav,"pages");
        Assert.IsTrue(results.gameObject.activeInHierarchy);
        sidebar[2].onClick.Invoke();
        Assert.IsTrue(dialog.IsOpen,"Leaving flight results must ask first");Assert.IsTrue(results.gameObject.activeInHierarchy);
        Button(dialog.transform,"Stay").onClick.Invoke();Assert.IsFalse(dialog.IsOpen);Assert.IsTrue(results.gameObject.activeInHierarchy);
        sidebar[2].onClick.Invoke();Button(dialog.transform,"Save and leave").onClick.Invoke();
        Assert.IsFalse(dialog.IsOpen);Assert.IsTrue(pages[2].activeInHierarchy);Assert.AreEqual(1,TravelAppRuntime.Instance.Services.DraftTrips.Count);
        // On the search form nothing is lost, so the sidebar never interrupts.
        sidebar[0].onClick.Invoke();Assert.IsFalse(dialog.IsOpen);sidebar[1].onClick.Invoke();Assert.IsFalse(dialog.IsOpen);Assert.IsTrue(pages[1].activeInHierarchy);
        nav.SearchExploreDestination("MIA");var date=new DateTime(2027,7,20);
        Invoke(Field<DateSelectionPage>(nav,"datePage"),"RestoreDates",(DateTime?)date,null,true);Invoke(nav,"ShowFlightResults",date,(DateTime?)null);
        sidebar[1].onClick.Invoke();Assert.IsTrue(dialog.IsOpen);Button(dialog.transform,"Leave without saving").onClick.Invoke();
        Assert.IsTrue(pages[1].activeInHierarchy);Assert.AreEqual(1,TravelAppRuntime.Instance.Services.DraftTrips.Count);
    }
    [UnityTest,Timeout(45000)] public IEnumerator CardsNamePartnerAirlinesAndReviewLeadsToTravelerDetails()
    {
        yield return SceneManager.LoadSceneAsync("DesktopLayout");yield return null;yield return null;
        var nav=Open();var results=Field<FlightResultsPage>(nav,"flightResults");
        foreach(var flight in Field<List<FlightResultsPage.Flight>>(results,"flights"))
        {
            var card=(RectTransform)flight.GetType().GetProperty("CardView",Private).GetValue(flight);
            string label=card.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="Airline").text;
            foreach(var segment in flight.Segments)StringAssert.Contains(segment.Airline,label,"Every operating airline appears on the card");
        }
        StringAssert.DoesNotContain("→",Field<TMP_Text>(results,"legTitle").text,"The results card names the leg; the header already names the route");
        var first=Field<List<FlightResultsPage.Flight>>(results,"flights").First(f=>f.Segments.Count==1);
        Button((RectTransform)first.GetType().GetProperty("CardView",Private).GetValue(first),"Select").onClick.Invoke();
        var seats=Field<SeatSelectionPage>(nav,"seatPage");Button(seats.transform,"Seat 1A").onClick.Invoke();Button(seats.transform,"Confirm Seat").onClick.Invoke();
        var review=Field<TripReviewPage>(nav,"tripReview");Assert.IsTrue(review.gameObject.activeInHierarchy);
        var proceed=Button(review.transform,"Continue to Payment");
        Assert.AreEqual("Add traveler details",proceed.GetComponentInChildren<TMP_Text>().text);
        StringAssert.Contains("Required",Button(review.transform,"Traveler details").GetComponentInChildren<TMP_Text>().text);
        Assert.AreEqual((Color)Color.clear,Button(review.transform.Find("Review itinerary viewport/Viewport/Legs/Departure"),"Edit flight").targetGraphic.color,"Secondary review actions are text links");
        PassengerTestData.Complete(nav);yield return null;
        Assert.AreEqual("Continue to Payment",proceed.GetComponentInChildren<TMP_Text>().text);
        proceed.onClick.Invoke();var payment=Field<PaymentPlaceholderPage>(nav,"paymentPage");Assert.IsTrue(payment.gameObject.activeInHierarchy);
        StringAssert.DoesNotContain("(s)",Field<TMP_Text>(payment,"itinerary").text);
        StringAssert.StartsWith("Pay $",Field<UIButton>(payment,"pay").GetComponentInChildren<TMP_Text>().text);
        Assert.AreEqual("",Field<TMP_InputField>(payment,"expirationInput").text,"Expiry starts empty so its MM / YY hint shows");
    }
}
