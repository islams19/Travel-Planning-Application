using NUnit.Framework;

namespace TravelApp.FlightMap.Tests
{
    public sealed class FlightGeoMathTests
    {
        [Test] public void JfkToLaxDistance_IsWithinKnownTolerance()
        {
            var jfk = FlightLocationDatabase.Locations[0]; var lax = FlightLocationDatabase.Locations[1];
            Assert.That(FlightGeoMath.GreatCircleDistanceKm(jfk, lax), Is.InRange(3960d, 3995d));
        }

        [Test] public void Projection_MapsWorldBoundsToUnitSquare()
        {
            Assert.That(FlightGeoMath.Equirectangular01(-90, -180), Is.EqualTo(UnityEngine.Vector2.zero));
            Assert.That(FlightGeoMath.Equirectangular01(90, 180), Is.EqualTo(UnityEngine.Vector2.one));
        }

        [Test] public void GreatCircleRoute_StartsAndEndsAtSelectedCoordinates()
        {
            var a = FlightLocationDatabase.Locations[0]; var b = FlightLocationDatabase.Locations[12];
            Assert.That(UnityEngine.Vector2.Distance(FlightGeoMath.GreatCirclePoint01(a,b,0), FlightGeoMath.Equirectangular01(a.latitude,a.longitude)), Is.LessThan(.0001f));
            Assert.That(UnityEngine.Vector2.Distance(FlightGeoMath.GreatCirclePoint01(a,b,1), FlightGeoMath.Equirectangular01(b.latitude,b.longitude)), Is.LessThan(.0001f));
        }
    }
}
