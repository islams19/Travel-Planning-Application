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
        public RectTransform ViewportRect => viewport;
        public RectTransform ContentRect => mapContent;

        public void Configure(RectTransform viewportRect, RectTransform contentRect) { viewport = viewportRect; mapContent = contentRect; ClampPosition(); }
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (mapContent == null || viewport == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position, eventData.pressEventCamera, out dragStart);
            contentStart = mapContent.anchoredPosition;
        }
        public void OnDrag(PointerEventData eventData)
        {
            if (mapContent == null || viewport == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position, eventData.pressEventCamera, out Vector2 position);
            mapContent.anchoredPosition = contentStart + position - dragStart;
            ClampPosition();
        }
        public void OnScroll(PointerEventData eventData)
        {
            if (viewport == null || Mathf.Approximately(eventData.scrollDelta.y, 0)) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, eventData.position, eventData.enterEventCamera, out Vector2 pivot);
            if (!viewport.rect.Contains(pivot)) pivot = viewport.rect.center;
            SetZoom(zoom + Mathf.Sign(eventData.scrollDelta.y) * zoomStep, pivot);
        }
        public void ZoomIn() => SetZoom(zoom + zoomStep, viewport.rect.center);
        public void ZoomOut() => SetZoom(zoom - zoomStep, viewport.rect.center);
        private float MinimumZoom => viewport != null && mapContent != null
            ? Mathf.Min(1f, Mathf.Min(viewport.rect.width / mapContent.rect.width, viewport.rect.height / mapContent.rect.height)) : 1f;
        public void ResetView() { zoom = MinimumZoom; mapContent.localScale = Vector3.one * zoom; mapContent.anchoredPosition = Vector2.zero; }

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
            zoom = Mathf.Clamp(Mathf.Min(fitX, fitY), MinimumZoom, maximumZoom);
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

        void SetZoom(float value, Vector2 pivot)
        {
            if (mapContent == null || viewport == null) return;
            float next = Mathf.Clamp(value, MinimumZoom, maximumZoom);
            if (Mathf.Approximately(next, zoom)) return;
            Vector3 geographicPoint = mapContent.InverseTransformPoint(viewport.TransformPoint(pivot));
            zoom = next;
            mapContent.localScale = Vector3.one * zoom;
            Vector2 after = viewport.InverseTransformPoint(mapContent.TransformPoint(geographicPoint));
            mapContent.anchoredPosition += pivot - after;
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
