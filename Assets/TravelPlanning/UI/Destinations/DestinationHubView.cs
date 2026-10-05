using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TravelPlanning.Destinations;
using UnityEngine;

namespace TravelPlanning.UI.Destinations
{
    /// <summary>Renders destination choices and cards; it never starts database requests.</summary>
    internal sealed class DestinationHubView
    {
        private readonly TMP_Dropdown destination;
        private readonly TMP_Text description;
        private readonly PlaceSection hotels, restaurants, experiences, hotspots;
        private readonly PlaceCard cardPrefab;
        private readonly UnityEngine.UI.ScrollRect scroll;
        public DestinationHubView(TMP_Dropdown destination, TMP_Text description, PlaceSection hotels, PlaceSection restaurants,
            PlaceSection experiences, PlaceSection hotspots, PlaceCard cardPrefab, UnityEngine.UI.ScrollRect scroll)
        {
            this.destination = destination;
            this.description = description;
            this.hotels = hotels;
            this.restaurants = restaurants;
            this.experiences = experiences;
            this.hotspots = hotspots;
            this.cardPrefab = cardPrefab;
            this.scroll = scroll;
        }

        public void ShowOptions(IReadOnlyList<DestinationOption> options, string selectedId)
        {
            destination.ClearOptions();
            destination.AddOptions(options.Select(option => option.Name + ", " + option.Country).ToList());
            destination.SetValueWithoutNotify(Math.Max(0, options.ToList().FindIndex(option => option.Id == selectedId)));
            destination.RefreshShownValue();
        }

        public void Show(DestinationHubResult result, Action<PlaceOption> review, Action<PlaceOption> save, Action<PlaceOption> track)
        {
            description.text = result.Destination.Description;
            hotels.Show(result.Hotels, cardPrefab, review, save, track);
            restaurants.Show(result.Restaurants, cardPrefab, review, save);
            experiences.Show(result.Experiences, cardPrefab, review, save);
            hotspots.Show(result.Hotspots, cardPrefab, review, save);
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1;
        }

        public void Clear()
        {
            if (description)
            {
                description.text = "";
            }

            if (hotels)
            {
                hotels.Clear();
            }

            if (restaurants)
            {
                restaurants.Clear();
            }

            if (experiences)
            {
                experiences.Clear();
            }

            if (hotspots)
            {
                hotspots.Clear();
            }
        }
    }
}
