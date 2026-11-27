using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Existing flow scenarios refer to stable sample slots instead of assuming the same carrier on every route.
static class MockFlightTestData
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public static bool Matches(FlightResultsPage.Flight flight,string id)=>flight.Id==id || MockFlightGenerator.LegacyOption(id)==flight.MockOptionIndex;
    public static Transform Card(FlightResultsPage page,int slot)
    {
        var flight=((List<FlightResultsPage.Flight>)page.GetType().GetField("flights",Private).GetValue(page)).First(f=>f.MockOptionIndex==slot);
        return (RectTransform)flight.GetType().GetProperty("CardView",Private).GetValue(flight);
    }
}
