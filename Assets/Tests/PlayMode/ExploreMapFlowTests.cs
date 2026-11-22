using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using TravelApp.FlightMap;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UIButton=UnityEngine.UI.Button;
using Object=UnityEngine.Object;

public class ExploreMapFlowTests
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
    static T Page<T>() where T:MonoBehaviour=>Object.FindObjectsByType<T>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
    static UIButton Button(Transform root,string name)=>root.GetComponentsInChildren<UIButton>(true).Single(button=>button.name==name);
    static void Click(UIButton button)
    {
        Assert.IsTrue(button.gameObject.activeInHierarchy);Assert.IsTrue(button.interactable);
        Canvas.ForceUpdateCanvases();
        var canvas=button.GetComponentInParent<Canvas>();
        var rect=(RectTransform)button.transform;
        var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,
            position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Assert.IsTrue(hits.Count>0 && (hits[0].gameObject==button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)),button.name+" must receive pointer clicks");
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static void ClickToggle(UnityEngine.UI.Toggle toggle)
    {
        Canvas.ForceUpdateCanvases();var rect=(RectTransform)toggle.transform;
        var canvas=toggle.GetComponentInParent<Canvas>();
        var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,
            position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Assert.IsTrue(hits.Count>0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Toggle>()==toggle,"Airport row must be the actual raycast target");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static Vector2[] Layout(TMP_Text label)=>new[]{label.rectTransform.anchorMin,label.rectTransform.anchorMax,label.rectTransform.offsetMin,label.rectTransform.offsetMax};
    [UnityTest,Timeout(45000)] public IEnumerator ExploreUsesSharedShellPreservesStateAndExercisesMapWithoutChangingBooking()
    {
        yield return SceneManager.LoadSceneAsync("DesktopLayout");yield return null;yield return null;
        var nav=Page<SidebarNavigation>();var pages=Field<GameObject[]>(nav,"pages");var sidebar=Field<UIButton[]>(nav,"buttons");
        var search=pages[0].GetComponentInChildren<DestinationAutocomplete>();
        Click(Button(pages[0].transform,"card-Tokyo"));
        string selected=search.SelectedDestination;
        var labels=nav.GetComponentInParent<Canvas>().GetComponentsInChildren<TMP_Text>(true)
            .Where(text=>!text.transform.IsChildOf(pages[1].transform) && text.name!="headerSubtitle" && text.name!="headerTitle").ToDictionary(text=>text,Layout);
        Click(sidebar[1]);yield return null;yield return null;
        foreach(var pair in labels)CollectionAssert.AreEqual(pair.Value,Layout(pair.Key),"Map must not rewrite "+pair.Key.name);
        Assert.AreEqual(1,Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length);
        Assert.AreEqual(1,Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length);
        var selector=Page<FlightLocationSelector>();var map=Page<FlightMapViewport>();var route=Page<FlightRouteRenderer>();
        var airplane=Page<AirplaneAnimationController>();var prices=Page<FlightPriceManager>();
        Assert.IsTrue(selector.transform.IsChildOf(pages[1].transform));
        Assert.IsTrue(Page<FlightMapManager>().transform.IsChildOf(pages[1].transform));
        Assert.AreEqual("JFK",selector.Departure.code);Assert.AreEqual("LAX",selector.Destination.code);
        Assert.Greater(route.Points.Count,2);Assert.Greater(prices.OfferCount,2);
        var canvasRoot=nav.GetComponentInParent<Canvas>();
        Click(GameObject.Find("header_avatar").GetComponent<UIButton>());Page<SharedProfileMenu>().Close();
        var dropdown=selector.transform.Find("DepartureDropdown").GetComponent<TMP_Dropdown>();
        var dropdownRect=(RectTransform)dropdown.transform;
        var dropdownPointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,
            position=RectTransformUtility.WorldToScreenPoint(null,dropdownRect.TransformPoint(dropdownRect.rect.center))};
        ExecuteEvents.Execute(dropdown.gameObject,dropdownPointer,ExecuteEvents.pointerClickHandler);
        yield return null;Assert.IsTrue(dropdown.IsExpanded);
        var laxOption=Object.FindObjectsByType<UnityEngine.UI.Toggle>(FindObjectsSortMode.None)
            .First(toggle=>toggle.GetComponentInChildren<TMP_Text>().text.Contains("LAX"));
        ClickToggle(laxOption);
        yield return new WaitForSeconds(.2f);
        Assert.AreEqual("LAX",selector.Departure.code);Assert.AreEqual(0,route.Points.Count);
        selector.SelectDepartureByCode("JFK");
        Assert.AreEqual(1,Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length,"Temporary dropdown canvases must close cleanly");
        var departureInput=selector.transform.Find("DepartureSearch").GetComponent<TMP_InputField>();
        departureInput.text="MIA";Assert.AreEqual("MIA",selector.Departure.code);
        Assert.AreEqual("MIA",prices.LoadedDepartureCode);
        Assert.IsTrue(selector.SelectDepartureByCode("JFK"));Assert.IsTrue(selector.SelectDestinationByCode("MIA"));
        var offerMarker=prices.Markers.First(marker=>marker.Offer.Destination.code=="LAX");
        // Bring the target into the clipped viewport, just as a user must pan/zoom to a bubble.
        map.FocusOnPoints(new[]{map.CoordinateToLocal(selector.Departure.latitude,selector.Departure.longitude),
            map.CoordinateToLocal(offerMarker.Offer.Destination.latitude,offerMarker.Offer.Destination.longitude)});
        yield return null;
        Click(offerMarker.Button);
        Assert.AreEqual("LAX",selector.Destination.code);
        Assert.IsTrue(selector.SelectDepartureByCode("LHR"));Assert.IsTrue(selector.SelectDestinationByCode("CDG"));
        Assert.IsTrue(canvasRoot.GetComponentsInChildren<TMP_Text>().Any(text=>text.text == "Flight prices"));
        float zoom=map.Zoom;Click(Button(pages[1].transform,"ZoomInButton"));Assert.Greater(map.Zoom,zoom);
        Click(Button(pages[1].transform,"ZoomOutButton"));Assert.AreEqual(zoom,map.Zoom,.001f);
        map.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,1)});Assert.Greater(map.Zoom,zoom);
        var content=Field<RectTransform>(map,"mapContent");Vector2 before=content.anchoredPosition;
        var drag=new PointerEventData(EventSystem.current){position=new Vector2(1200,500)};
        map.OnBeginDrag(drag);drag.position+=new Vector2(-34,24);map.OnDrag(drag);
        Assert.Greater((content.anchoredPosition-before).magnitude,1);
        yield return null;
        Assert.AreEqual(1f,prices.Markers[0].transform.localScale.x*map.Zoom,.001f);
        foreach (string name in new[]{"StartFlightButton","PauseResumeButton","RestartFlightButton"})
            Assert.IsFalse(Button(pages[1].transform,name).gameObject.activeInHierarchy);
        foreach (string name in new[]{"FlightProgress","AnimationInfo","StatusInfo"})
            Assert.IsFalse(selector.transform.Find(name).gameObject.activeInHierarchy);
        var plane=Field<RectTransform>(airplane,"airplane");
        Assert.AreEqual(AirplaneAnimationController.FlightState.Flying,airplane.State);
        Vector2 start=plane.anchoredPosition;
        yield return new WaitForSeconds(.15f);
        Assert.Greater((plane.anchoredPosition-start).magnitude,0);
        selector.SelectDestinationByCode("CDG"); // Same endpoint must not reset an in-flight preview.
        Assert.AreNotEqual(route.Points[0],plane.anchoredPosition);
        selector.SelectDestinationByCode("MIA");
        Assert.AreEqual(AirplaneAnimationController.FlightState.Flying,airplane.State);
        Assert.AreEqual(route.Points[0],plane.anchoredPosition);
        selector.SelectDestinationByCode("CDG");
        yield return new WaitForSeconds(airplane.DemonstrationDurationSeconds+.15f);
        Assert.AreEqual(AirplaneAnimationController.FlightState.Arrived,airplane.State);
        Vector2 arrived=plane.anchoredPosition;
        yield return new WaitForSeconds(.15f);
        Assert.AreEqual(arrived,plane.anchoredPosition,"Preview must not loop");
        Assert.Less(Vector2.Distance(route.Points[route.Points.Count-1],arrived),.01f);
        // A zero-distance route and an invalid code must clear every stale visual and control.
        selector.SelectDestinationByCode("LHR");Assert.AreEqual(0,route.Points.Count);Assert.IsFalse(plane.gameObject.activeSelf);
        Assert.IsFalse(Button(pages[1].transform,"StartFlightButton").interactable);
        Assert.IsTrue(canvasRoot.GetComponentsInChildren<TMP_Text>().Any(text=>text.text.Contains("Choose different")));
        Assert.IsFalse(selector.SelectDestinationByCode("INVALID"));Assert.IsNull(selector.Destination);Assert.AreEqual(0,route.Points.Count);
        selector.SelectDestinationByCode("CDG");Assert.Greater(route.Points.Count,2);Assert.IsTrue(plane.gameObject.activeSelf);
        var retainedSelector=selector;
        for(int i=0;i<3;i++)
        {
            Click(sidebar[0]);yield return null;Assert.AreEqual(selected,search.SelectedDestination);
            Click(sidebar[1]);yield return null;
            Assert.AreSame(retainedSelector,Page<FlightLocationSelector>());Assert.AreEqual("LHR",selector.Departure.code);Assert.AreEqual("CDG",selector.Destination.code);
        }
        Assert.IsNull(nav.CurrentSeatBooking);Assert.IsNull(nav.CurrentReturnSeatBooking);
        Assert.AreEqual(selected,search.SelectedDestination);
        Assert.IsNull(Field<FlightResultsPage>(nav,"flightResults").SelectedDeparture);
        Click(sidebar[0]);Assert.IsTrue(pages[0].activeSelf);Assert.IsFalse(pages[1].activeSelf);
    }
    [UnityTest,Timeout(45000)] public IEnumerator ExplicitExploreHandoffUsesBookingCatalogAndPreservesOriginAndFares()
    {
        yield return SceneManager.LoadSceneAsync("DesktopLayout");yield return null;yield return null;
        var nav=Page<SidebarNavigation>();var pages=Field<GameObject[]>(nav,"pages");var sidebar=Field<UIButton[]>(nav,"buttons");
        var destination=Field<DestinationAutocomplete>(nav,"destinationSearch");
        var origin=Field<DestinationAutocomplete>(nav,"originSearch");
        Field<TMP_InputField>(origin,"input").text="Boston";
        yield return null;
        Click(Field<RectTransform>(origin,"results").GetComponentsInChildren<UIButton>().First());
        string originalOrigin=origin.SelectedDestination;
        Assert.AreEqual("Boston, Massachusetts (BOS)",originalOrigin);
        Click(Button(pages[0].transform,"card-Tokyo"));
        var dates=Page<DateSelectionPage>();
        var flights=Field<FlightResultsPage>(nav,"flightResults");
        var baseFares=Field<List<FlightResultsPage.Flight>>(flights,"flights").Select(flight=>flight.Price).ToArray();
        Assert.IsNull(Field<DateTime?>(dates,"departureDate"));Assert.IsNull(Field<DateTime?>(dates,"returnDate"));
        Click(sidebar[1]);yield return null;yield return null;
        var selector=Page<FlightLocationSelector>();var prices=Page<FlightPriceManager>();
        var airplane=Page<AirplaneAnimationController>();
        var handoff=Button(pages[1].transform,"Search Explore destination");
        selector.SelectDepartureByCode("CDG");selector.SelectDestinationByCode("MIA");
        Assert.IsTrue(handoff.interactable);
        Assert.AreEqual("Tokyo, Japan",destination.SelectedDestination,"Map selection alone cannot change the booking");
        Assert.AreEqual(originalOrigin,origin.SelectedDestination);
        Assert.Greater(prices.Markers.First(marker=>marker.Offer.Destination.code=="MIA").Offer.Amount,0);
        var mapState=airplane.State;
        Click(handoff);
        Assert.IsTrue(pages[0].activeInHierarchy);Assert.IsFalse(pages[1].activeInHierarchy);
        Assert.AreEqual("Miami, Florida",destination.SelectedDestination);
        Assert.AreEqual("Miami, Florida",Field<TMP_InputField>(destination,"input").text);
        Assert.AreEqual(originalOrigin,origin.SelectedDestination,"Explore departure must never replace booking origin");
        Assert.IsTrue(Button(pages[0].transform,"Search Flights").interactable);
        Assert.AreEqual("CDG",selector.Departure.code);Assert.AreEqual("MIA",selector.Destination.code);
        Assert.AreEqual(mapState,airplane.State);
        Assert.IsNull(flights.SelectedDeparture);Assert.AreEqual(0,flights.SelectedFarePerTraveler);
        CollectionAssert.AreEqual(baseFares,Field<List<FlightResultsPage.Flight>>(flights,"flights").Select(flight=>flight.Price));
        Assert.IsNull(Field<DateTime?>(dates,"departureDate"));Assert.IsNull(Field<DateTime?>(dates,"returnDate"));
        Assert.IsNull(nav.CurrentSeatBooking);Assert.IsNull(nav.CurrentReturnSeatBooking);
        Click(sidebar[1]);yield return null;
        selector.SelectDestinationByCode("INVALID");Assert.IsFalse(handoff.interactable);
        handoff.onClick.Invoke();Assert.IsTrue(pages[1].activeInHierarchy);Assert.AreEqual("Miami, Florida",destination.SelectedDestination);
        foreach(string unsupported in new[]{"ATL","SEA","DFW","IAD","FRA"})
        {
            Assert.IsTrue(selector.SelectDestinationByCode(unsupported));
            Click(handoff);
            Assert.IsTrue(pages[1].activeInHierarchy,"Unsupported airport must keep Explore visible");
            Assert.AreEqual("Miami, Florida",destination.SelectedDestination);
            Assert.AreEqual("Miami, Florida",Field<TMP_InputField>(destination,"input").text);
            Assert.AreEqual(originalOrigin,origin.SelectedDestination);
            StringAssert.Contains("isn’t available",pages[1].GetComponentsInChildren<TMP_Text>().Single(text=>text.name=="Explore booking message").text);
        }
        // Resolve a city with a canonical catalog label that has no comma/country suffix.
        selector.SelectDestinationByCode("SIN");Click(handoff);
        Assert.AreEqual("Singapore",destination.SelectedDestination);Assert.AreEqual(originalOrigin,origin.SelectedDestination);
        Click(sidebar[1]);yield return null;
        Assert.AreEqual("SIN",selector.Destination.code);
        selector.SelectDestinationByCode("HND");
        Assert.AreEqual("Singapore",destination.SelectedDestination,"Later Explore changes remain independent until another explicit handoff");
        Click(sidebar[0]);Assert.AreEqual("Singapore",destination.SelectedDestination);
        Assert.IsNull(flights.SelectedDeparture);
        CollectionAssert.AreEqual(baseFares,Field<List<FlightResultsPage.Flight>>(flights,"flights").Select(flight=>flight.Price));
    }

}
