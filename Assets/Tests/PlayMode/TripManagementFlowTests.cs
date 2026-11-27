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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;
using UIButton=UnityEngine.UI.Button;

public class TripManagementFlowTests
{
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
    static object Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Private).Invoke(o,args);
    static UIButton Button(Transform root,string name)=>root.GetComponentsInChildren<UIButton>().First(b=>b.name==name);
    static IEnumerator Click(UIButton button)
    {
        yield return null;Canvas.ForceUpdateCanvases();var rect=(RectTransform)button.transform;
        var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Assert.IsNotEmpty(hits);
        Assert.AreEqual(button.gameObject,ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),button.name+" must receive pointer clicks");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,e,ExecuteEvents.pointerClickHandler);yield return null;
    }
    sealed class Store:ISaveStore {public bool Fail;public SaveData Load()=>new SaveData();public void Save(SaveData value){if(Fail)throw new System.IO.IOException("Test failure");}}
    AppServices original;TravelCatalog catalog;Store store;CompletedDemoBooking receipt;
    void Isolate()
    {
        var runtime=TravelAppRuntime.Instance;original=runtime.Services;catalog=ScriptableObject.CreateInstance<TravelCatalog>();store=new Store();
        typeof(TravelAppRuntime).GetProperty("Services").SetValue(runtime,new AppServices(catalog,store));
    }
    [TearDown] public void Cleanup()
    {
        if(TravelAppRuntime.Instance!=null)
        {
            if(receipt!=null)Field<List<CompletedDemoBooking>>(TravelAppRuntime.Instance,"completedFlights").Remove(receipt);
            if(original!=null)typeof(TravelAppRuntime).GetProperty("Services").SetValue(TravelAppRuntime.Instance,original);
        }
        if(catalog!=null)Object.DestroyImmediate(catalog);
    }
    [UnityTest,Timeout(45000)] public IEnumerator DraftRenameDeleteSortingNotificationsAndResumeUseExistingStore()
    {
        yield return SceneManager.LoadSceneAsync("DesktopLayout");yield return null;yield return null;Isolate();
        var nav=Object.FindFirstObjectByType<SidebarNavigation>();nav.SearchExploreDestination("CUN");
        var services=TravelAppRuntime.Instance.Services;var pages=Field<GameObject[]>(nav,"pages");
        var focus=Button(pages[0].transform,"Search travelers");KeyboardFocusNavigation.Instance.Focus(focus);
        Assert.IsTrue(nav.SaveActiveDraft().success);string id=services.DraftTrips.Single().id;
        Assert.AreEqual(focus.gameObject,EventSystem.current.currentSelectedGameObject,"Saving must not steal focus");
        var toast=Object.FindFirstObjectByType<AppNotification>();Assert.IsTrue(toast.IsVisible);Assert.IsTrue(pages[0].activeSelf);
        nav.SaveActiveDraft();Assert.AreEqual(1,services.DraftTrips.Count);Assert.IsFalse(nav.HasUnsavedBooking);
        yield return Click(Button(toast.transform,"View My Trips"));var trips=pages[2].GetComponent<SavedTripsPage>();
        var dialog=Object.FindFirstObjectByType<AppDialog>();
        yield return Click(Button(trips.transform,"Rename trip"));
        var name=dialog.transform.Find("Application dialog/Dialog panel/Surface/Trip name").GetComponent<TMP_InputField>();name.text="Beach escape";
        yield return Click(Button(dialog.transform,"Save name"));Assert.AreEqual("Beach escape",services.DraftTrips.Single().title);Assert.IsFalse(dialog.IsOpen);
        Assert.IsTrue(trips.GetComponentsInChildren<TMP_Text>().Any(t=>t.name=="Last saved"&&t.text.Contains("Last saved:")));
        nav.SaveActiveDraft();Assert.AreEqual("Beach escape",services.DraftTrips.Single().title);toast.Hide();
        var second=new TripDraft{title="Later trip",destination="Tokyo, Japan",departureTicks=new DateTime(2028,7,14).Ticks};services.SaveDraft(second);
        var data=(SaveData)typeof(AppServices).GetProperty("Data",Private).GetValue(services);
        data.draftTrips.First(d=>d.id==id).updatedUtc="2026-11-25T12:00:00Z";
        data.draftTrips.First(d=>d.id==second.id).updatedUtc="2025-11-25T12:00:00Z";
        data.draftTrips.First(d=>d.id==id).departureTicks=new DateTime(2027,7,14).Ticks;
        trips.Refresh();
        yield return Click(Button(trips.transform,"Sort trips"));yield return Click(Button(trips.transform,"Oldest saved"));
        Assert.AreEqual(second.id,Field<RectTransform>(trips,"content").GetChild(0).name);
        yield return Click(Button(trips.transform,"Sort trips"));yield return Click(Button(trips.transform,"Upcoming travel date"));
        Assert.AreEqual(id,Field<RectTransform>(trips,"content").GetChild(0).name);
        yield return Click(Button(trips.transform,"Sort trips"));yield return Click(Button(trips.transform,"Newest saved"));
        var first=Field<RectTransform>(trips,"content").Find(id);
        yield return Click(Button(first,"Delete trip"));yield return Click(Button(dialog.transform,"Cancel"));Assert.AreEqual(2,services.DraftTrips.Count);
        yield return Click(Button(first,"Delete trip"));yield return Click(Button(dialog.transform,"Delete saved trip"));Assert.AreEqual(1,services.DraftTrips.Count);
        Assert.AreEqual(second.id,services.DraftTrips.Single().id);toast.Hide();
        yield return Click(Button(trips.transform,"Resume draft"));Assert.AreEqual("Tokyo, Japan",Field<DestinationAutocomplete>(nav,"destinationSearch").SelectedDestination);
        toast.Show("Trip saved",seconds:.05f);yield return new WaitForSecondsRealtime(.1f);Assert.IsFalse(toast.IsVisible);
    }
    [UnityTest,Timeout(45000)] public IEnumerator NewTripGuardProtectsUnsavedChangesWithoutInterruptingSidebar()
    {
        yield return SceneManager.LoadSceneAsync("DesktopLayout");yield return null;yield return null;Isolate();
        var nav=Object.FindFirstObjectByType<SidebarNavigation>();var dialog=Object.FindFirstObjectByType<AppDialog>();var services=TravelAppRuntime.Instance.Services;
        Assert.IsFalse(nav.HasUnsavedBooking);nav.StartNewTrip();Assert.IsFalse(dialog.IsOpen);
        nav.SearchExploreDestination("CUN");Assert.IsTrue(nav.HasUnsavedBooking);
        Field<UIButton[]>(nav,"buttons")[1].onClick.Invoke();nav.OpenSavedTrips();Assert.IsFalse(dialog.IsOpen);
        nav.StartNewTrip();Assert.IsTrue(dialog.IsOpen);yield return Click(Button(dialog.transform,"Cancel"));
        Assert.AreEqual("CUN",Field<DestinationAutocomplete>(nav,"destinationSearch").SelectedAirportCode);
        nav.StartNewTrip();store.Fail=true;yield return Click(Button(dialog.transform,"Save and start new trip"));
        Assert.IsTrue(dialog.IsOpen);Assert.AreEqual(0,services.DraftTrips.Count);Assert.AreEqual("CUN",Field<DestinationAutocomplete>(nav,"destinationSearch").SelectedAirportCode);
        store.Fail=false;yield return Click(Button(dialog.transform,"Save and start new trip"));
        Assert.AreEqual(1,services.DraftTrips.Count);Assert.IsNull(Field<DestinationAutocomplete>(nav,"destinationSearch").SelectedDestination);Assert.IsFalse(nav.HasUnsavedBooking);
        Object.FindFirstObjectByType<AppNotification>().Hide();var saved=services.DraftTrips.Single();nav.ResumeDraft(saved);Assert.IsFalse(nav.HasUnsavedBooking);
        nav.SearchExploreDestination("HND");Assert.IsTrue(nav.HasUnsavedBooking);nav.StartNewTrip();
        yield return Click(Button(dialog.transform,"Discard and start new trip"));Assert.IsFalse(nav.HasUnsavedBooking);
        Assert.AreEqual("CUN",services.DraftTrips.Single().destinationAirportCode);Assert.AreEqual(saved.id,services.DraftTrips.Single().id);
    }
    [UnityTest,Timeout(60000)] public IEnumerator ValidationPreservesInputsFocusesErrorsAndPaidTripsStayReadOnly()
    {
        yield return SceneManager.LoadSceneAsync("Login");yield return null;yield return null;Isolate();
        var login=Object.FindFirstObjectByType<DesktopLoginUI>();var username=Field<TMP_InputField>(login,"username");var password=Field<TMP_InputField>(login,"password");var email=Field<TMP_InputField>(login,"email");
        username.text="";password.text="";Invoke(login,"Submit");Assert.AreEqual(username.gameObject,EventSystem.current.currentSelectedGameObject);
        Assert.IsTrue(username.GetComponent<FormFieldFeedback>().HasError);username.text="FeedbackUser";Assert.IsFalse(username.GetComponent<FormFieldFeedback>().HasError);
        Invoke(login,"SwitchMode");username.text="ab";email.text="bad";password.text="pw";Invoke(login,"Submit");
        Assert.IsTrue(new[]{username,email,password}.All(f=>f.GetComponent<FormFieldFeedback>().HasError));Assert.AreEqual("pw",password.text);
        username.text="FeedbackUser";email.text="person@randomtext.randommmmm";password.text="test-pass";Invoke(login,"Submit");Assert.AreEqual(email.gameObject,EventSystem.current.currentSelectedGameObject);
        email.text="person@example.com";Invoke(login,"Submit");Assert.IsFalse(Field<bool>(login,"registering"));Assert.AreEqual("test-pass",password.text);
        Invoke(login,"Submit");while(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="DesktopLayout")yield return null;yield return null;yield return null;
        var nav=Object.FindFirstObjectByType<SidebarNavigation>();nav.SearchExploreDestination("CUN");var date=new DateTime(2027,7,14);
        Invoke(Field<DateSelectionPage>(nav,"datePage"),"RestoreDates",(DateTime?)date,null,true);Invoke(nav,"ShowFlightResults",date,(DateTime?)null);
        var results=Field<FlightResultsPage>(nav,"flightResults");var flight=Field<List<FlightResultsPage.Flight>>(results,"flights").First(f=>MockFlightTestData.Matches(f,"B6-100"));
        var card=(RectTransform)flight.GetType().GetProperty("CardView",Private).GetValue(flight);Button(card,"Select").onClick.Invoke();
        var seats=Field<SeatSelectionPage>(nav,"seatPage");Button(seats.transform,"Seat 1A").onClick.Invoke();Button(seats.transform,"Confirm Seat").onClick.Invoke();PassengerTestData.Complete(nav);Invoke(nav,"ContinueToPayment");
        var payment=Field<PaymentPlaceholderPage>(nav,"paymentPage");var inputs=new[]{"nameInput","numberInput","expirationInput","cvvInput","postalInput"}.Select(n=>Field<TMP_InputField>(payment,n)).ToArray();
        inputs[0].text="";inputs[1].text="123";inputs[2].text="13/20";inputs[3].text="1";inputs[4].text="!";Invoke(payment,"Submit");
        Assert.IsTrue(inputs.All(f=>f.GetComponent<FormFieldFeedback>().HasError));Assert.AreEqual(inputs[0].gameObject,EventSystem.current.currentSelectedGameObject);Assert.AreEqual("123",inputs[1].text);
        foreach(var field in inputs)
        {
            var border=field.transform.Find("Invalid field border");
            Assert.IsTrue(border.gameObject.activeInHierarchy);
            Assert.IsNotNull(border.GetComponent<CanvasRenderer>(),"Invalid-field borders must render, not only store an error state");
        }
        inputs[0].text="Jane Example";Assert.IsFalse(inputs[0].GetComponent<FormFieldFeedback>().HasError);Invoke(payment,"Submit");Assert.AreEqual(inputs[1].gameObject,EventSystem.current.currentSelectedGameObject);
        inputs[1].text="4242424242424242";inputs[2].text="12/30";inputs[3].text="123";inputs[4].text="10001";
        Assert.IsTrue(inputs.All(f=>!f.GetComponent<FormFieldFeedback>().HasError));Invoke(payment,"Submit");
        receipt=TravelAppRuntime.Instance.CompletedFlights.Single();nav.OpenSavedTrips();var trips=Field<GameObject[]>(nav,"pages")[2].GetComponent<SavedTripsPage>();
        yield return Click(Button(trips.transform,"Filter Paid"));Assert.IsFalse(trips.GetComponentsInChildren<UIButton>().Any(b=>b.name=="Rename trip"||b.name=="Delete trip"));
        yield return Click(Button(trips.transform,"View details"));Assert.IsTrue(Field<DemoBookingPages>(nav,"confirmationPage").gameObject.activeInHierarchy);
        Assert.IsFalse(nav.HasUnsavedBooking,"A paid receipt must not trigger a new-plan warning");
    }
}
