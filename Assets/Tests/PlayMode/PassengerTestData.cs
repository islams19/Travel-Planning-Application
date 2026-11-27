using System.Linq;
using System.Reflection;
using UnityEngine.UI;

// Existing checkout regressions now supply required traveler/contact data before payment.
static class PassengerTestData
{
    public static void Complete(SidebarNavigation nav)
    {
        var results=(FlightResultsPage)nav.GetType().GetField("flightResults",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(nav);
        foreach(var traveler in results.Passengers.travelers){traveler.firstName="Test";traveler.lastName="Traveler";}
        results.Passengers.email="traveler@example.com";results.Passengers.phone="2125550100";
    }
    public static void AddAdult(FlightResultsPage results)
    {
        void Click(string name)=>results.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name==name).onClick.Invoke();
        Click("Passengers");Click("More travelers");Click("Done travelers");Click("Modify Search");
    }
}
