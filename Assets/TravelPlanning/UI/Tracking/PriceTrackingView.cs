using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using TravelPlanning.Tracking;
using UnityEngine;

namespace TravelPlanning.UI.Tracking
{
    /// <summary>Displays watches, target prices, and notification rows without changing saved state.</summary>
    internal sealed class PriceTrackingView
    {
        private readonly TMP_Dropdown watchChoice;
        private readonly TMP_InputField demoPrice;
        private readonly TMP_Text targetPreview, unreadSummary, trackLabel;
        private readonly UnityEngine.UI.ScrollRect scroll;
        private readonly PriceNoticeRow rowPrefab;
        private readonly List<PriceNoticeRow> rows = new List<PriceNoticeRow>();
        public PriceTrackingView(TMP_Dropdown watchChoice, TMP_InputField demoPrice, TMP_Text targetPreview,
            TMP_Text unreadSummary, TMP_Text trackLabel, UnityEngine.UI.ScrollRect scroll, PriceNoticeRow rowPrefab)
        {
            this.watchChoice = watchChoice;
            this.demoPrice = demoPrice;
            this.targetPreview = targetPreview;
            this.unreadSummary = unreadSummary;
            this.trackLabel = trackLabel;
            this.scroll = scroll;
            this.rowPrefab = rowPrefab;
        }

        public void Show(TrackingSnapshot snapshot, string targetId, PriceTargetKind targetKind, Action<string> read)
        {
            var choices = new List<string>
            {
                "Choose a tracked item"
            };
            choices.AddRange(snapshot.Watches.Select(watch => watch.Title + (watch.IsActive ? " • Tracking" : " • Stopped")));
            watchChoice.AddOptions(choices);
            int index = snapshot.Watches.ToList().FindIndex(watch => watch.TargetId == targetId && watch.Kind == targetKind);
            watchChoice.SetValueWithoutNotify(index + 1);
            watchChoice.RefreshShownValue();
            foreach (var notice in snapshot.Notifications)
            {
                string id = notice.Id;
                var row = UnityEngine.Object.Instantiate(rowPrefab, scroll.content);
                row.Show(notice, () => read(id));
                row.SetInteractable(false);
                rows.Add(row);
            }

            unreadSummary.text = snapshot.UnreadCount + " unread • " + snapshot.Notifications.Count + " total notifications";
        }

        public void ShowTarget(WatchOption target)
        {
            if (target == null)
            {
                targetPreview.text = "Use Track price on a flight or hotel card to start a watch.";
                return;
            }

            targetPreview.text = target.Title + "\nCurrent sample price: USD " + (target.CurrentPriceCents / 100m).ToString("N2",
                CultureInfo.InvariantCulture) + " " + target.PriceUnit;
            trackLabel.text = target.IsActive ? "Stop tracking" : "Track price";
            demoPrice.SetTextWithoutNotify((target.CurrentPriceCents / 100m).ToString("0.00", CultureInfo.InvariantCulture));
        }

        public void ScrollToTop()
        {
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1;
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
            if (watchChoice)
            {
                watchChoice.ClearOptions();
            }

            if (targetPreview)
            {
                targetPreview.text = "";
            }

            if (unreadSummary)
            {
                unreadSummary.text = "";
            }

            if (trackLabel)
            {
                trackLabel.text = "Track price";
            }

            if (demoPrice)
            {
                demoPrice.SetTextWithoutNotify("");
            }
        }
    }
}
