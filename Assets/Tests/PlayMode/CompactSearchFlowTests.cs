using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using TravelApp;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UIButton=UnityEngine.UI.Button;
using Object=UnityEngine.Object;

public class CompactSearchFlowTests
{
    sealed class MemoryStore : ISaveStore { public SaveData Load()=>new SaveData(); public void Save(SaveData data) { } }
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
    static UIButton Button(Transform root,string name)=>root.GetComponentsInChildren<UIButton>(true).Single(b=>b.name==name && b.gameObject.activeInHierarchy);
    static IEnumerator Click(UIButton button)
    {
        yield return null;
        Assert.IsTrue(button.interactable,button.name); Assert.IsTrue(button.gameObject.activeInHierarchy,button.name);
        Canvas.ForceUpdateCanvases(); var rect=(RectTransform)button.transform; var canvas=button.GetComponentInParent<Canvas>();
        var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,
            position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
        Assert.IsTrue(hits.Count>0 && (hits[0].gameObject==button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),button.name+" must receive live pointer input");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static IEnumerator Click(Transform root,string name)=>Click(Button(root,name));
    static IEnumerator Suggest(DestinationAutocomplete search,string query,string text)
    {
        Field<TMP_InputField>(search,"input").text=query;
        yield return null;
        var root=Field<RectTransform>(search,"results");
        yield return Click(root.GetComponentsInChildren<UIButton>().Single(b=>b.GetComponentInChildren<TMP_Text>().text.Contains(text)));
    }
    static IEnumerator Day(DateSelectionPage dates,string day)=>Click(Field<UIButton[]>(dates,"dayButtons").Take(42).Single(b=>b.gameObject.activeSelf && b.GetComponentInChildren<TMP_Text>().text==day));
    AppServices original; TravelCatalog catalog;
    [TearDown] public void Cleanup()
    {
        if(original!=null) typeof(TravelAppRuntime).GetProperty("Services").SetValue(TravelAppRuntime.Instance,original);
        if(catalog!=null) Object.DestroyImmediate(catalog);
    }
    [UnityTest,Timeout(45000)] public IEnumerator CompactSetupAirportGroupsSwapDatesOptionsDraftsExploreAndDirectSearch()
    {
        yield return SceneManager.LoadSceneAsync("DesktopLayout"); yield return null; yield return null;
        var runtime=TravelAppRuntime.Instance; original=runtime.Services; catalog=ScriptableObject.CreateInstance<TravelCatalog>();
        typeof(TravelAppRuntime).GetProperty("Services").SetValue(runtime,new AppServices(catalog,new MemoryStore()));
        var nav=Object.FindFirstObjectByType<SidebarNavigation>(); var pages=Field<GameObject[]>(nav,"pages");
        var form=pages[0].GetComponent<CompactFlightSearch>();
        var origin=Field<DestinationAutocomplete>(nav,"originSearch"); var destination=Field<DestinationAutocomplete>(nav,"destinationSearch");
        var options=Field<FlightResultsPage>(nav,"flightResults"); var dates=Field<DateSelectionPage>(nav,"datePage");
        Assert.AreEqual("JFK",origin.SelectedAirportCode);
        Assert.IsFalse(Button(pages[0].transform,"Search Flights").interactable);
        Assert.AreEqual(1,Object.FindObjectsByType<DateSelectionPage>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
        var surface=(RectTransform)pages[0].transform.Find("Trip search"); Assert.Less(surface.rect.height,((RectTransform)pages[0].transform).rect.height*.3f);
        Field<TMP_InputField>(destination,"input").text="New York";
        var labels=Field<RectTransform>(destination,"results").GetComponentsInChildren<TMP_Text>().Select(t=>t.text).ToArray();
        Assert.IsTrue(labels.Any(t=>t.Contains("City default"))); foreach(string code in new[]{"JFK","EWR","LGA"}) Assert.IsTrue(labels.Any(t=>t.Contains(code+" —")));
        yield return Suggest(destination,"New York","EWR —"); Assert.AreEqual("EWR",destination.SelectedAirportCode);
        Assert.IsFalse(Button(pages[0].transform,"Search Flights").interactable,"Different airports in the same city are still invalid");
        Assert.IsFalse(form.Search());
        yield return Suggest(destination,"Japan","Tokyo, Japan · City default (HND)"); Assert.AreEqual("HND",destination.SelectedAirportCode);
        Assert.IsFalse(form.Search()); Assert.IsTrue(form.IsCalendarOpen); Assert.IsTrue(pages[0].activeInHierarchy);
        Assert.IsTrue(dates.transform.IsChildOf(pages[0].transform)); yield return Click(dates.transform,"Close dates");
        yield return Suggest(destination,"Narita","NRT —"); Assert.AreEqual("NRT",destination.SelectedAirportCode);
        yield return Suggest(origin,"EWR","EWR —"); Assert.AreEqual("EWR",origin.SelectedAirportCode);
        yield return Click(pages[0].transform,"Search travelers"); yield return Click(pages[0].transform,"More travelers"); yield return Click(pages[0].transform,"More travelers");
        for(int i=0;i<3;i++) yield return Click(pages[0].transform,"More travelers");
        Assert.AreEqual(6,options.PassengerCount); Assert.IsFalse(Button(pages[0].transform,"More travelers").interactable);
        for(int i=0;i<5;i++) yield return Click(pages[0].transform,"Fewer travelers");
        Assert.AreEqual(1,options.PassengerCount); Assert.IsFalse(Button(pages[0].transform,"Fewer travelers").interactable);
        yield return Click(pages[0].transform,"More travelers"); yield return Click(pages[0].transform,"More travelers");
        yield return Click(pages[0].transform,"Done travelers"); Assert.AreEqual(3,options.PassengerCount);
        yield return Click(pages[0].transform,"Search travelers");
        var trigger=(RectTransform)Button(pages[0].transform,"Search travelers").transform;
        form.GetType().GetMethod("DismissPopupsAt",Private).Invoke(form,new object[]{RectTransformUtility.WorldToScreenPoint(null,trigger.TransformPoint(trigger.rect.center)),null});
        Assert.IsNotNull(Field<GameObject>(form,"openMenu"),"The dropdown trigger must keep its normal toggle behavior");
        form.GetType().GetMethod("DismissPopupsAt",Private).Invoke(form,new object[]{Vector2.zero,null});
        Assert.IsNull(Field<GameObject>(form,"openMenu"),"Outside clicks dismiss a popup without changing booking values");
        Assert.AreEqual(3,options.PassengerCount);
        yield return Click(pages[0].transform,"Search cabin"); yield return Click(pages[0].transform,"Cabin Business"); Assert.AreEqual("Business",options.CabinClass);
        yield return Click(pages[0].transform,"Swap airports"); Assert.AreEqual("NRT",origin.SelectedAirportCode); Assert.AreEqual("EWR",destination.SelectedAirportCode);
        Assert.AreEqual(3,options.PassengerCount); Assert.AreEqual("Business",options.CabinClass);
        yield return Click(pages[0].transform,"Swap airports");
        yield return Click(pages[0].transform,"Departure date"); yield return Day(dates,"10"); yield return Day(dates,"15");
        var departure=Field<DateTime?>(dates,"departureDate"); var returning=Field<DateTime?>(dates,"returnDate");
        yield return Click(dates.transform,"Confirm Dates"); Assert.IsFalse(form.IsCalendarOpen); Assert.IsTrue(pages[0].activeInHierarchy); Assert.IsFalse(options.gameObject.activeInHierarchy);
        yield return Click(pages[0].transform,"Return date"); yield return Day(dates,"17"); yield return Click(dates.transform,"Confirm Dates");
        Assert.AreEqual(departure,Field<DateTime?>(dates,"departureDate")); returning=Field<DateTime?>(dates,"returnDate"); Assert.AreEqual(17,returning.Value.Day);
        yield return Click(pages[0].transform,"Search Flights"); Assert.IsTrue(options.gameObject.activeInHierarchy); Assert.IsFalse(pages[0].activeInHierarchy);
        var flights=Field<List<FlightResultsPage.Flight>>(options,"flights");
        Assert.IsTrue(flights.All(f=>f.Segments[0].OriginCode=="EWR" && f.Segments.Last().DestinationCode=="NRT"));
        Assert.AreEqual(3,options.PassengerCount); Assert.AreEqual("Business",options.CabinClass);
        yield return Click(options.transform,"Modify Search"); yield return Click(options.transform,"Passengers"); yield return Click(options.transform,"Cabin"); yield return Click(options.transform,"Done");
        Field<UIButton[]>(nav,"buttons")[0].onClick.Invoke();
        Assert.AreEqual(4,options.PassengerCount); Assert.AreEqual("First",options.CabinClass);
        StringAssert.Contains("4 travelers",Button(pages[0].transform,"Search travelers").GetComponentInChildren<TMP_Text>().text);
        StringAssert.Contains("First",Button(pages[0].transform,"Search cabin").GetComponentInChildren<TMP_Text>().text);
        Assert.IsTrue(nav.SaveActiveDraft().success); var saved=runtime.Services.DraftTrips.Single();
        Assert.AreEqual("EWR",saved.originAirportCode); Assert.AreEqual("NRT",saved.destinationAirportCode);
        yield return SceneManager.LoadSceneAsync("DesktopLayout"); yield return null; yield return null;
        nav=Object.FindFirstObjectByType<SidebarNavigation>(); nav.ResumeDraft(saved);
        pages=Field<GameObject[]>(nav,"pages"); form=pages[0].GetComponent<CompactFlightSearch>();
        origin=Field<DestinationAutocomplete>(nav,"originSearch"); destination=Field<DestinationAutocomplete>(nav,"destinationSearch");
        options=Field<FlightResultsPage>(nav,"flightResults"); dates=Field<DateSelectionPage>(nav,"datePage");
        Assert.AreEqual("EWR",origin.SelectedAirportCode); Assert.AreEqual("NRT",destination.SelectedAirportCode);
        Assert.AreEqual(4,options.PassengerCount); Assert.AreEqual("First",options.CabinClass);
        Assert.AreEqual(departure,Field<DateTime?>(dates,"departureDate")); Assert.AreEqual(returning,Field<DateTime?>(dates,"returnDate"));
        Field<UIButton[]>(nav,"buttons")[1].onClick.Invoke(); yield return null; yield return null;
        var map=pages[1].GetComponentInChildren<TravelApp.FlightMap.FlightLocationSelector>(); map.SelectDestinationByCode("DXB");
        Assert.AreEqual("NRT",destination.SelectedAirportCode,"Explore cannot mutate booking state without the handoff");
        yield return Click(pages[1].transform,"Search Explore destination"); Assert.IsTrue(pages[0].activeInHierarchy);
        Assert.AreEqual("DXB",destination.SelectedAirportCode); Assert.AreEqual("EWR",origin.SelectedAirportCode);
        Assert.AreEqual(departure,Field<DateTime?>(dates,"departureDate")); Assert.AreEqual(4,options.PassengerCount);
        yield return Click(pages[0].transform,"card-Tokyo"); Assert.AreEqual("HND",destination.SelectedAirportCode); Assert.IsTrue(pages[0].activeInHierarchy);
        Field<TMP_InputField>(destination,"input").text="arbitrary text"; Assert.IsNull(destination.SelectedAirportCode); Assert.IsFalse(form.Search());
        yield return Suggest(destination,"Arab","Dubai, United Arab Emirates · City default (DXB)"); Assert.AreEqual("DXB",destination.SelectedAirportCode);
        yield return Click(pages[0].transform,"Search trip type"); yield return Click(pages[0].transform,"One way");
        Assert.IsNull(Field<DateTime?>(dates,"returnDate")); Assert.IsFalse(pages[0].GetComponentsInChildren<UIButton>().Any(b=>b.name=="Return date"));
        yield return Click(pages[0].transform,"Search Flights"); Assert.IsTrue(options.gameObject.activeInHierarchy); Assert.IsNull(Field<DateTime?>(nav,"returnDate"));
        Assert.AreEqual(4,options.PassengerCount); Assert.AreEqual("First",options.CabinClass);
        runtime.Services.RemoveDraft(saved.id);
    }
}
