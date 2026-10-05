using System;
using System.Globalization;
using TMPro;
using TravelPlanning.Tracking;
using UnityEngine;

namespace TravelPlanning.UI.Tracking
{
    /// <summary>Displays one persisted price notice and its read state.</summary>
    public sealed class PriceNoticeRow : MonoBehaviour
    {
        [Header("Results and presentation")]
        [SerializeField]
        private TMP_Text title, body, timestamp, actionLabel;
        [Header("Buttons")]
        [SerializeField]
        private UnityEngine.UI.Button readButton;
        private Action markRead;
        private bool alreadyRead;
        private void Awake()
        {
            if (readButton)
            {
                readButton.onClick.AddListener(Read);
            }
        }

        public void Show(PriceNotice notice, Action onRead)
        {
            title.text = notice.Title;
            body.text = notice.Body;
            timestamp.text = DateTime.TryParse(notice.CreatedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                out DateTime date) ? date.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture) : notice.CreatedUtc;
            alreadyRead = notice.IsRead;
            markRead = onRead;
            actionLabel.text = alreadyRead ? "Read" : "Mark read";
            SetInteractable(true);
        }

        public void SetInteractable(bool value)
        {
            if (readButton)
            {
                readButton.interactable = value && !alreadyRead;
            }
        }

        private void Read() => markRead?.Invoke();
        private void OnDestroy()
        {
            if (readButton)
            {
                readButton.onClick.RemoveListener(Read);
            }
        }
    }
}
