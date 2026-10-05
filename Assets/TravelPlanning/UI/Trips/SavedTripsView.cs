using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TravelPlanning.Trips;
using UnityEngine;

namespace TravelPlanning.UI.Trips
{
    /// <summary>Owns trip dropdown choices and item rows; writes stay in the controller/service.</summary>
    internal sealed class SavedTripsView
    {
        private readonly TMP_Dropdown tripChoice, startDate, endDate;
        private readonly TMP_Text tripInfo;
        private readonly UnityEngine.UI.ScrollRect scroll;
        private readonly SavedTripRow rowPrefab;
        private readonly List<SavedTripRow> rows = new List<SavedTripRow>();
        public SavedTripsView(TMP_Dropdown tripChoice, TMP_Dropdown startDate, TMP_Dropdown endDate, TMP_Text tripInfo,
            UnityEngine.UI.ScrollRect scroll, SavedTripRow rowPrefab)
        {
            this.tripChoice = tripChoice;
            this.startDate = startDate;
            this.endDate = endDate;
            this.tripInfo = tripInfo;
            this.scroll = scroll;
            this.rowPrefab = rowPrefab;
        }

        public void Show(TripDetails details, Action<string, string> remove)
        {
            tripInfo.text = details.Trip.Name + " • Planning dates: " + details.Trip.StartDate + " to " +
                details.Trip.EndDate + " • " + details.Items.Count + " saved items";
            foreach (var item in details.Items)
            {
                string itemId = item.Id;
                var row = UnityEngine.Object.Instantiate(rowPrefab, scroll.content);
                row.Show(item, () => remove(details.Trip.Id, itemId));
                row.SetInteractable(false);
                rows.Add(row);
            }

            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1;
        }

        public void ShowChoices(TripListResult list, string selectedId)
        {
            tripChoice.ClearOptions();
            tripChoice.AddOptions(list.Trips.Select(trip => trip.Name + " (" + trip.ItemCount + ")").ToList());
            tripChoice.SetValueWithoutNotify(Math.Max(0, list.Trips.ToList().FindIndex(trip => trip.Id == selectedId)));
            tripChoice.RefreshShownValue();
        }

        public void PopulateDates(string start, string end)
        {
            var values = Shared.CatalogDateChoices.Create(start, end);
            startDate.ClearOptions();
            startDate.AddOptions(values);
            endDate.ClearOptions();
            endDate.AddOptions(values);
            startDate.SetValueWithoutNotify(Math.Max(0, values.IndexOf("2027-06-15")));
            endDate.SetValueWithoutNotify(Math.Max(0, values.IndexOf("2027-06-22")));
            startDate.RefreshShownValue();
            endDate.RefreshShownValue();
        }

        public void SetRowsInteractable(bool interactable)
        {
            foreach (var row in rows)
            {
                if (row)
                {
                    row.SetInteractable(interactable);
                }
            }
        }

        public void Clear()
        {
            foreach (var row in rows)
            {
                if (!row)
                {
                    continue;
                }

                row.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(row.gameObject);
            }

            rows.Clear();
            if (tripInfo)
            {
                tripInfo.text = "";
            }
        }
    }
}
