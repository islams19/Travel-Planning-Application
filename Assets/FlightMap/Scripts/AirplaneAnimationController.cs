using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TravelApp.FlightMap
{
    /// <summary>Moves and rotates the airplane along sampled great-circle route points.</summary>
    public sealed class AirplaneAnimationController : MonoBehaviour
    {
        public enum FlightState { Ready, Flying, Paused, Arrived }
        [SerializeField] RectTransform airplane;
        [SerializeField] Slider progressSlider;
        [SerializeField, Min(1f)] float demonstrationDurationSeconds = 12f;
        IReadOnlyList<Vector2> route;
        float progress;
        FlightState state = FlightState.Ready;
        public event Action<FlightState, float> StateChanged;
        public FlightState State => state;
        public float DemonstrationDurationSeconds => demonstrationDurationSeconds;

        public void Configure(RectTransform plane, Slider slider) { airplane = plane; progressSlider = slider; }
        public void SetRoute(IReadOnlyList<Vector2> points) { route = points; RestartFlight(); }
        public void StartFlight() { if (route == null || route.Count < 2) return; if (state == FlightState.Arrived) progress = 0; SetState(FlightState.Flying); }
        public void TogglePause() { if (state == FlightState.Flying) SetState(FlightState.Paused); else if (state == FlightState.Paused) SetState(FlightState.Flying); }
        public void RestartFlight() { progress = 0; SetState(FlightState.Ready); UpdatePose(); }

        void Update()
        {
            if (state != FlightState.Flying) return;
            progress = Mathf.Clamp01(progress + Time.deltaTime / demonstrationDurationSeconds);
            UpdatePose();
            if (progress >= 1f) SetState(FlightState.Arrived); else StateChanged?.Invoke(state, progress);
        }

        void UpdatePose()
        {
            if (route == null || route.Count < 2 || airplane == null) return;
            float scaled = progress * (route.Count - 1); int index = Mathf.Min(Mathf.FloorToInt(scaled), route.Count - 2); float local = scaled - index;
            Vector2 a = route[index], b = route[index + 1]; airplane.anchoredPosition = Vector2.Lerp(a, b, local);
            Vector2 d = b - a; airplane.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f);
            if (progressSlider != null) progressSlider.value = progress;
        }

        void SetState(FlightState value) { state = value; StateChanged?.Invoke(state, progress); }
    }
}
