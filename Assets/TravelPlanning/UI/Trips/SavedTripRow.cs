using System;
using System.Globalization;
using TMPro;
using TravelPlanning.Trips;
using UnityEngine;

namespace TravelPlanning.UI.Trips
{
    /// <summary>Displays one saved reference using its current catalog information.</summary>
    public sealed class SavedTripRow : MonoBehaviour
    {
        [Header("Results and presentation")]
        [SerializeField]
        private TMP_Text title, details, price;
        [Header("Buttons")]
        [SerializeField]
        private UnityEngine.UI.Button removeButton;
        private Action remove;
        private void Awake() => removeButton.onClick.AddListener(Remove);
        public void Show(SavedTripItem item, Action onRemove)
        {
            title.text = item.Title;
            details.text = item.Details;
            remove = onRemove;
            price.text = !item.PriceCents.HasValue ? "Price unavailable" : item.PriceCents.Value == 0 ? "Free" : "USD " +
                (item.PriceCents.Value / 100m).ToString("N2", CultureInfo.InvariantCulture) + " " + item.PriceUnit;
        }

        public void SetInteractable(bool value)
        {
            if (removeButton)
            {
                removeButton.interactable = value;
            }
        }

        private void Remove() => remove?.Invoke();
        private void OnDestroy()
        {
            if (removeButton)
            {
                removeButton.onClick.RemoveListener(Remove);
            }
        }
    }
}
