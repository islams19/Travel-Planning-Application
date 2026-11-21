using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TravelApp.FlightMap
{
    /// <summary>Draws a smooth great-circle route as a thick antialiased-looking uGUI ribbon.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FlightRouteRenderer : MaskableGraphic
    {
        [SerializeField, Min(2)] int segmentCount = 80;
        [SerializeField, Min(1f)] float thickness = 6f;
        readonly List<Vector2> points = new();
        public IReadOnlyList<Vector2> Points => points;

        protected override void OnEnable()
        {
            // Older serialized versions of FlightMapScene were created before this required
            // component was declared. Repair them before MaskableGraphic registers for clipping.
            if (!TryGetComponent<CanvasRenderer>(out _)) gameObject.AddComponent<CanvasRenderer>();
            base.OnEnable();
        }

        public void SetRoute(FlightLocation from, FlightLocation to, FlightMapViewport map)
        {
            points.Clear();
            for (int i = 0; i <= segmentCount; i++) points.Add(map.RoutePointToLocal(FlightGeoMath.GreatCirclePoint01(from, to, i / (float)segmentCount)));
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (points.Count < 2) return;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 direction = i == points.Count - 1 ? points[i] - points[i - 1] : points[i + 1] - points[i];
                Vector2 normal = new Vector2(-direction.y, direction.x).normalized * thickness * .5f;
                vh.AddVert(points[i] + normal, color, Vector2.zero); vh.AddVert(points[i] - normal, color, Vector2.one);
                if (i > 0) { int n = i * 2; vh.AddTriangle(n - 2, n, n - 1); vh.AddTriangle(n, n + 1, n - 1); }
            }
        }
    }
}
