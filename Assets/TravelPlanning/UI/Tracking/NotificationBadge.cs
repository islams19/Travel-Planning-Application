using System;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using TravelPlanning.Tracking;
using UnityEngine;

namespace TravelPlanning.UI.Tracking
{
    /// <summary>Refreshes unread badges on explicit events, never by polling.</summary>
    public sealed class NotificationBadge : Shared.ScreenControllerBase
    {
        [Header("Screen references")]
        [SerializeField]
        private LoginPage account;
        [Header("Results and presentation")]
        [SerializeField]
        private TMP_Text[] labels;
        public int? UnreadCount { get; private set; }

        public void Configure(Shared.TravelSceneBindings scene)
        {
            account = scene.Account;
            labels = new[] {
                scene.HomeCard.Find("NotificationsButton/UnreadBadge").GetComponent<TMP_Text>(),
                scene.FlightCanvas.transform.Find("Main/Header/NotificationsButton/UnreadBadge").GetComponent<TMP_Text>(),
                scene.HubCanvas.transform.Find("Main/Header/NotificationsButton/UnreadBadge").GetComponent<TMP_Text>()
            };
        }

        private void Start()
        {
            if (!account || labels == null || labels.Length != 3 || Array.Exists(labels, label => !label))
            {
                Debug.LogError("NotificationBadge: assign the account and three badge labels.");
                enabled = false;
                return;
            }

            account.LoggedIn += OnLogin;
            account.LoggedOut += ClearOnLogout;
            if (string.IsNullOrEmpty(account.SignedInUserId))
            {
                ClearOnLogout();
            }
            else
            {
                Refresh();
            }
        }

        private void OnLogin(string _) => Refresh();
        public async void Refresh() => await RefreshAsync();
        public async Task RefreshAsync()
        {
            Cancel();
            string owner = account.SignedInUserId;
            if (string.IsNullOrEmpty(owner))
            {
                Display(0);
                return;
            }

            int current = RequestRevision;
            var token = base.BeginScreenRequest();
            Display(null);
            try
            {
                int count = await new PriceTrackingService(account.Database, owner).GetUnreadCountAsync(token);
                if (base.IsRequestCurrent(current) && owner == account.SignedInUserId)
                {
                    Display(count);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                if (base.IsRequestCurrent(current) && owner == account.SignedInUserId)
                {
                    Display(null);
                }
            }
        }

        private void Display(int? count)
        {
            UnreadCount = count;
            foreach (var label in labels)
            {
                if (label)
                {
                    label.text = count.HasValue ? count.Value > 99 ? "99+" : count.Value.ToString() : "?";
                    label.gameObject.SetActive(!count.HasValue || count.Value > 0);
                }
            }
        }

        private void ClearOnLogout()
        {
            Cancel();
            Display(0);
        }

        private void Cancel()
        {
            base.CancelScreenRequest();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (account)
            {
                account.LoggedIn -= OnLogin;
                account.LoggedOut -= ClearOnLogout;
            }
        }
    }
}
