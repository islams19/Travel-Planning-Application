using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace TravelApp.FlightMap
{
    /// <summary>Pans and zooms one shared map content RectTransform so all overlays stay aligned.</summary>
    public sealed class FlightMapViewport : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
    {
        [SerializeField] RectTransform viewport;
        [SerializeField] RectTransform mapContent;
        [SerializeField, Range(1f, 8f)] float maximumZoom = 5f;
        [SerializeField] float zoomStep = 0.35f;
        float zoom = 1f;
        Vector2 dragStart;
        Vector2 contentStart;

        public float Zoom => zoom;

        public void Configure(RectTransform viewportRect, RectTransform contentRect) { viewport = viewportRect; mapContent = contentRect; ClampPosition(); }
        public void OnBeginDrag(PointerEventData eventData) { dragStart = eventData.position; contentStart = mapContent.anchoredPosition; }
        public void OnDrag(PointerEventData eventData) { if (mapContent == null) return; mapContent.anchoredPosition = contentStart + (eventData.position - dragStart); ClampPosition(); }
        public void OnScroll(PointerEventData eventData) => SetZoom(zoom + Mathf.Sign(eventData.scrollDelta.y) * zoomStep);
        public void ZoomIn() => SetZoom(zoom + zoomStep);
        public void ZoomOut() => SetZoom(zoom - zoomStep);
        public void ResetView() { zoom = 1f; mapContent.localScale = Vector3.one; mapContent.anchoredPosition = Vector2.zero; }

        /// <summary>Frames a geographic route with padding while keeping every overlay aligned.</summary>
        public void FocusOnPoints(IReadOnlyList<Vector2> points, float padding = 180f)
        {
            if (viewport == null || mapContent == null || points == null || points.Count == 0) return;
            Canvas.ForceUpdateCanvases();

            Vector2 minimum = points[0], maximum = points[0];
            for (int i = 1; i < points.Count; i++)
            {
                minimum = Vector2.Min(minimum, points[i]);
                maximum = Vector2.Max(maximum, points[i]);
            }

            Vector2 bounds = maximum - minimum;
            float fitX = viewport.rect.width / Mathf.Max(1f, bounds.x + padding);
            float fitY = viewport.rect.height / Mathf.Max(1f, bounds.y + padding);
            zoom = Mathf.Clamp(Mathf.Min(fitX, fitY), 1f, maximumZoom);
            mapContent.localScale = Vector3.one * zoom;
            mapContent.anchoredPosition = -(minimum + maximum) * .5f * zoom;
            ClampPosition();
        }

        public Vector2 CoordinateToLocal(double latitude, double longitude)
        {
            Vector2 uv = FlightGeoMath.Equirectangular01(latitude, longitude);
            Rect rect = mapContent.rect;
            return new Vector2((uv.x - .5f) * rect.width, (uv.y - .5f) * rect.height);
        }

        public Vector2 RoutePointToLocal(Vector2 uv)
        {
            Rect rect = mapContent.rect;
            return new Vector2((uv.x - .5f) * rect.width, (uv.y - .5f) * rect.height);
        }

        void SetZoom(float value)
        {
            if (mapContent == null) return;
            zoom = Mathf.Clamp(value, 1f, maximumZoom);
            mapContent.localScale = Vector3.one * zoom;
            ClampPosition();
        }

        void ClampPosition()
        {
            if (viewport == null || mapContent == null) return;
            Vector2 excess = Vector2.Max(Vector2.zero, (mapContent.rect.size * zoom - viewport.rect.size) * .5f);
            Vector2 p = mapContent.anchoredPosition;
            p.x = Mathf.Clamp(p.x, -excess.x, excess.x); p.y = Mathf.Clamp(p.y, -excess.y, excess.y);
            mapContent.anchoredPosition = p;
        }
    }
}
