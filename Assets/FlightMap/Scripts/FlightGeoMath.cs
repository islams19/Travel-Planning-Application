using System;
using UnityEngine;

namespace TravelApp.FlightMap
{
    /// <summary>Pure geographic calculations shared by the scene and EditMode tests.</summary>
    public static class FlightGeoMath
    {
        const double EarthRadiusKm = 6371.0088;

        public static double GreatCircleDistanceKm(FlightLocation a, FlightLocation b)
        {
            double lat1 = DegreesToRadians(a.latitude), lat2 = DegreesToRadians(b.latitude);
            double dLat = lat2 - lat1, dLon = DegreesToRadians(b.longitude - a.longitude);
            double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return EarthRadiusKm * 2 * Math.Asin(Math.Min(1, Math.Sqrt(h)));
        }

        public static Vector2 Equirectangular01(double latitude, double longitude)
            => new Vector2((float)((longitude + 180d) / 360d), (float)((latitude + 90d) / 180d));

        public static Vector2 GreatCirclePoint01(FlightLocation a, FlightLocation b, float t)
        {
            Vector3 va = ToUnitSphere(a), vb = ToUnitSphere(b);
            Vector3 p = Vector3.Slerp(va, vb, Mathf.Clamp01(t)).normalized;
            double lat = Math.Asin(p.y) * Mathf.Rad2Deg;
            double lon = Math.Atan2(p.z, p.x) * Mathf.Rad2Deg;
            return Equirectangular01(lat, lon);
        }

        static Vector3 ToUnitSphere(FlightLocation l)
        {
            double lat = DegreesToRadians(l.latitude), lon = DegreesToRadians(l.longitude);
            return new Vector3((float)(Math.Cos(lat) * Math.Cos(lon)), (float)Math.Sin(lat), (float)(Math.Cos(lat) * Math.Sin(lon)));
        }

        static double DegreesToRadians(double value) => value * Math.PI / 180d;
    }
}
