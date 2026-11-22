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
using Object=UnityEngine.Object;
using UIButton=UnityEngine.UI.Button;

public class ExploreMapRefinementTests
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
    static T One<T>()where T:MonoBehaviour=>Object.FindObjectsByType<T>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
    static PointerEventData Pointer(RectTransform rect)
    {
        var canvas=rect.GetComponentInParent<Canvas>();
        return new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,
            position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center))};
    }
    static void Click(Component control)
    {
        Canvas.ForceUpdateCanvases();var pointer=Pointer((RectTransform)control.transform);
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Assert.IsTrue(hits.Count>0 && (hits[0].gameObject==control.gameObject || hits[0].gameObject.transform.IsChildOf(control.transform)),control.name+" is blocked by "+(hits.Count>0?hits[0].gameObject.name:"nothing"));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static UnityEngine.UI.Toggle Option(string code)=>Object.FindObjectsByType<UnityEngine.UI.Toggle>(FindObjectsSortMode.None)
        .Single(toggle=>toggle.GetComponentInChildren<TMP_Text>().text.StartsWith(code+" —"));
    static Vector2 PointUnder(FlightMapViewport map,Vector2 point)=>map.ContentRect.InverseTransformPoint(map.ViewportRect.TransformPoint(point));
    static Rect Bubble(FlightPriceMarker marker,RectTransform viewport)
    {
        var corners=new Vector3[4];((RectTransform)marker.transform).GetWorldCorners(corners);
        Vector2 min=viewport.InverseTransformPoint(corners[0]), max=viewport.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }
    [UnityTest,Timeout(45000)]public IEnumerator BothDropdownsRaycastCatalogFaresSizingDeclutterAndAnchoredZoom()
    {
        yield return SceneManager.LoadSceneAsync("DesktopLayout");yield return null;yield return null;
        var nav=One<SidebarNavigation>();Field<UIButton[]>(nav,"buttons")[1].onClick.Invoke();yield return null;yield return null;
        var selector=One<FlightLocationSelector>();var map=One<FlightMapViewport>();var prices=One<FlightPriceManager>();
        foreach(var name in new[]{"DepartureDropdown","DestinationDropdown"})
        {
            var dropdown=selector.transform.Find(name).GetComponent<TMP_Dropdown>();
            Click(dropdown);yield return null;Assert.IsTrue(dropdown.IsExpanded);
            Click(Option(name=="DepartureDropdown"?"LAX":"MIA"));yield return new WaitForSeconds(.2f);
            if(name=="DepartureDropdown"){Assert.AreEqual("LAX",selector.Departure.code);selector.SelectDepartureByCode("JFK");}
            else Assert.AreEqual("MIA",selector.Destination.code);
        }
        var search=selector.transform.Find("DestinationSearch").GetComponent<TMP_InputField>();search.text="Tokyo";yield return null;
        var destinations=selector.transform.Find("DestinationDropdown").GetComponent<TMP_Dropdown>();
        Assert.IsTrue(destinations.options.Any(option=>option.text.StartsWith("HND")));
        Assert.IsTrue(destinations.options.Any(option=>option.text.StartsWith("NRT")));
        Click(destinations);yield return null;Click(Option("NRT"));yield return new WaitForSeconds(.2f);Assert.AreEqual("NRT",selector.Destination.code);
        search.text="Bali";yield return null;Assert.AreEqual("DPS",selector.Destination.code);
        Assert.IsTrue(prices.Markers.Any(marker=>marker.Offer.Destination.code=="DPS"));
        StringAssert.Contains("DPS",Field<TMP_Text>(prices,"selectedPriceText").text);
        // Coverage is driven by the same data used to populate the booking autocomplete.
        var booking=Field<DestinationAutocomplete>(nav,"destinationSearch");
        CollectionAssert.AreEquivalent(TravelLocationCatalog.BookingDestinations,Field<string[]>(booking,"destinations"));
        selector.SelectDepartureByCode("JFK");selector.SelectDestinationByCode("LAX");yield return null;
        Vector2 center=map.ViewportRect.rect.center;
        Vector2 point=PointUnder(map,center);map.ZoomIn();Assert.Less(Vector2.Distance(point,PointUnder(map,center)),.01f);
        map.ZoomOut();Assert.Less(Vector2.Distance(point,PointUnder(map,center)),.01f);
        Vector2 cursor=center+new Vector2(130,70);
        var scroll=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,map.ViewportRect.TransformPoint(cursor)),scrollDelta=Vector2.up};
        point=PointUnder(map,cursor);map.OnScroll(scroll);Assert.Less(Vector2.Distance(point,PointUnder(map,cursor)),.01f);
        var manager=One<FlightMapManager>();var plane=Field<RectTransform>(One<AirplaneAnimationController>(),"airplane");
        foreach(bool far in new[]{false,true})
        {
            if(far)map.ResetView();else{map.ZoomIn();map.ZoomIn();}
            yield return null;
            foreach(var marker in new[]{Field<RectTransform>(manager,"departureMarker"),Field<RectTransform>(manager,"destinationMarker"),plane})
            {
                Assert.AreEqual(1f,marker.localScale.x*map.Zoom,.001f);
                Assert.LessOrEqual(marker.sizeDelta.x,32);Assert.LessOrEqual(marker.sizeDelta.y,32);
            }
            var visible=prices.Markers.Where(marker=>marker.gameObject.activeInHierarchy).ToArray();Assert.Greater(visible.Length,0);
            for(int i=0;i<visible.Length;i++)for(int j=i+1;j<visible.Length;j++)
                Assert.IsFalse(Bubble(visible[i],map.ViewportRect).Overlaps(Bubble(visible[j],map.ViewportRect)),"Price bubbles must not overlap");
            if(far)Assert.LessOrEqual(visible.Length,12);
        }
        var selected=prices.Markers.Single(marker=>marker.Offer.Destination.code=="LAX");Assert.IsTrue(selected.gameObject.activeInHierarchy);
        Click(selected.Button);Assert.AreEqual("LAX",selector.Destination.code);
        selector.SelectDestinationByCode("HND");yield return null;Canvas.ForceUpdateCanvases();
        // Dateline routes retain samples without a ribbon segment jumping across the entire map.
        var route=One<FlightRouteRenderer>();Assert.Greater(route.Points.Count,2);
        var mesh=route.canvasRenderer.GetMesh();
        for(int i=0;i<mesh.triangles.Length;i+=3)
        {
            float[] x={mesh.vertices[mesh.triangles[i]].x,mesh.vertices[mesh.triangles[i+1]].x,mesh.vertices[mesh.triangles[i+2]].x};
            Assert.Less(x.Max()-x.Min(),map.ContentRect.rect.width*.5f);
        }

        Assert.IsNull(booking.SelectedDestination,"Map refinement must not auto-select a booking destination");
    }
}
