using System.Collections.Generic;
using System.Globalization;
using TMPro;
using TravelPlanning.Reviews;
using UnityEngine;

namespace TravelPlanning.UI.Reviews
{
    /// <summary>Owns review row objects and summary text, without account or request state.</summary>
    internal sealed class ReviewsView
    {
        private readonly TMP_Text placeName, summary, status;
        private readonly UnityEngine.UI.ScrollRect scroll;
        private readonly ReviewRow rowPrefab;
        private readonly List<GameObject> rows = new List<GameObject>();
        public ReviewsView(TMP_Text placeName, TMP_Text summary, TMP_Text status, UnityEngine.UI.ScrollRect scroll, ReviewRow rowPrefab)
        {
            this.placeName = placeName;
            this.summary = summary;
            this.status = status;
            this.scroll = scroll;
            this.rowPrefab = rowPrefab;
        }

        public void Show(ReviewDetails result)
        {
            placeName.text = result.Place.Name;
            summary.text = result.Place.AverageRating.HasValue ? result.Place.AverageRating.Value.ToString("0.0",
                CultureInfo.InvariantCulture) + " / 5 • " + result.Place.ReviewCount + " stored reviews" : "No reviews yet";
            foreach (var review in result.Reviews)
            {
                var row = Object.Instantiate(rowPrefab, scroll.content);
                row.Show(review);
                rows.Add(row.gameObject);
            }

            status.text = result.Reviews.Count == 0 ? "No traveler reviews have been added for this place yet." : "These sample reviews are stored locally. They are not imported from Google.";
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1;
        }

        public void Clear()
        {
            foreach (var row in rows)
            {
                if (!row)
                {
                    continue;
                }

                row.SetActive(false);
                Object.Destroy(row);
            }

            rows.Clear();
        }
    }
}
