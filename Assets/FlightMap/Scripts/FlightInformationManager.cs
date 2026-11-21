using TMPro;
using UnityEngine;

namespace TravelApp.FlightMap
{
    /// <summary>Formats route facts and keeps estimated real duration distinct from demo animation time.</summary>
    public sealed class FlightInformationManager : MonoBehaviour
    {
        [SerializeField] TMP_Text departureText, destinationText, distanceText, durationText, animationTimeText, statusText, arrivalText;

        public void Configure(TMP_Text departure, TMP_Text destination, TMP_Text distance, TMP_Text duration, TMP_Text animation, TMP_Text status, TMP_Text arrival)
        { departureText = departure; destinationText = destination; distanceText = distance; durationText = duration; animationTimeText = animation; statusText = status; arrivalText = arrival; }

        public void SetRoute(FlightLocation from, FlightLocation to, float demoSeconds)
        {
            double km = FlightGeoMath.GreatCircleDistanceKm(from, to);
            // Compact airport/city labels stay readable in the two-column information grid.
            departureText.text = $"Departure\n{from.code} · {from.city}"; destinationText.text = $"Destination\n{to.code} · {to.city}";
            distanceText.text = $"Great-circle distance\n{km:N0} km / {km * .621371:N0} mi";
            // Planning estimate only: 45 minutes ground allowance + cruise at 850 km/h.
            double hours = .75 + km / 850d;
            durationText.text = $"Estimated travel time*\n{Mathf.FloorToInt((float)hours)}h {Mathf.RoundToInt((float)(hours % 1 * 60)):00}m";
            animationTimeText.text = $"Demo animation\n{demoSeconds:0} seconds";
            arrivalText.gameObject.SetActive(false);
        }

        public void SetStatus(AirplaneAnimationController.FlightState state, float progress)
        {
            statusText.text = $"Status\n{state} · {progress:P0}";
            arrivalText.gameObject.SetActive(state == AirplaneAnimationController.FlightState.Arrived);
            if (state == AirplaneAnimationController.FlightState.Arrived) arrivalText.text = "Arrived at destination";
        }
    }
}
