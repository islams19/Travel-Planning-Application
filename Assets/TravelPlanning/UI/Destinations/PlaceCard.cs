using System;
using System.Globalization;
using TMPro;
using TravelPlanning.Destinations;
using UnityEngine;

namespace TravelPlanning.UI.Destinations
{
    /// <summary>Displays a seeded place, including clear demo price and review labels.</summary>
    public sealed class PlaceCard : MonoBehaviour
    {
        [Header("Results and presentation")]
        [SerializeField]
        private TMP_Text placeName, price, rating, description, address;
        [Header("Buttons")]
        [SerializeField]
        private UnityEngine.UI.Button viewReviewsButton;
        [SerializeField]
        private UnityEngine.UI.Button saveButton;
        [SerializeField]
        private UnityEngine.UI.Button trackButton;
        private PlaceOption shownPlace;
        private Action<PlaceOption> reviewRequested;
        private Action<PlaceOption> saveRequested;
        private Action<PlaceOption> trackRequested;
        private void Awake()
        {
            if (viewReviewsButton)
            {
                viewReviewsButton.onClick.AddListener(OpenReviews);
            }

            if (saveButton)
            {
                saveButton.onClick.AddListener(Save);
            }

            if (trackButton)
            {
                trackButton.onClick.AddListener(Track);
            }
        }

        public void Show(PlaceOption place, Action<PlaceOption> onReviews = null, Action<PlaceOption> onSave = null, Action<PlaceOption> onTrack = null)
        {
            shownPlace = place;
            reviewRequested = onReviews;
            saveRequested = onSave;
            if (saveButton)
            {
                saveButton.interactable = onSave != null;
            }

            trackRequested = onTrack;
            if (trackButton)
            {
                trackButton.gameObject.SetActive(place.Category == PlaceCategory.Hotel);
                trackButton.interactable = onTrack != null;
            }

            if (viewReviewsButton)
            {
                viewReviewsButton.interactable = onReviews != null;
            }

            placeName.text = place.Name;
            price.text = FormatPrice(place.PriceCents, place.Category);
            rating.text = place.AverageRating.HasValue && place.ReviewCount > 0 ? place.AverageRating.Value.ToString("0.0",
                CultureInfo.InvariantCulture) + " / 5 • " + place.ReviewCount + " demo reviews" : "No reviews yet";
            description.text = place.Description;
            address.text = place.Address;
        }

        private void OpenReviews() => reviewRequested?.Invoke(shownPlace);
        private void Save() => saveRequested?.Invoke(shownPlace);
        private void Track()
        {
            if (shownPlace?.Category == PlaceCategory.Hotel)
            {
                trackRequested?.Invoke(shownPlace);
            }
        }

        private void OnDestroy()
        {
            if (viewReviewsButton)
            {
                viewReviewsButton.onClick.RemoveListener(OpenReviews);
            }

            if (saveButton)
            {
                saveButton.onClick.RemoveListener(Save);
            }

            if (trackButton)
            {
                trackButton.onClick.RemoveListener(Track);
            }
        }

        public static string FormatPrice(int? cents, PlaceCategory category)
        {
            if (!cents.HasValue)
            {
                return "Price unavailable";
            }

            if (cents.Value == 0)
            {
                return "Free";
            }

            string unit = category == PlaceCategory.Hotel ? " / room / night" : category == PlaceCategory.Restaurant ? " / person (meal)" : category == PlaceCategory.Experience ? " / adult" : " / visit";
            return "USD " + (cents.Value / 100m).ToString("N2", CultureInfo.InvariantCulture) + unit;
        }
    }
}
