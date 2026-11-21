using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TravelApp.FlightMap
{
    /// <summary>Clickable map bubble for one demo price offer.</summary>
    public sealed class FlightPriceMarker : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] TMP_Text priceText;
        [SerializeField] TMP_Text destinationText;
        FlightPriceOffer offer;

        public FlightPriceOffer Offer => offer;
        public Button Button => button;

        public void ConfigureVisual(Button targetButton, TMP_Text price, TMP_Text destination)
        { button = targetButton; priceText = price; destinationText = destination; }

        public void Bind(FlightPriceOffer value, Action<FlightPriceOffer> selected)
        {
            offer = value;
            priceText.text = value.PriceLabel;
            destinationText.text = value.Destination.code;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => selected?.Invoke(offer));
            gameObject.name = "DemoPrice_" + value.Destination.code;
        }
    }
}
