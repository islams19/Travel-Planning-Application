using System;
using System.Collections.Generic;

namespace TravelApp.FlightMap
{
    /// <summary>One map price result. Demo offers are never represented as live inventory.</summary>
    [Serializable]
    public sealed class FlightPriceOffer
    {
        public FlightLocation Destination { get; }
        public int Amount { get; }
        public string Currency { get; }
        public bool IsDemo { get; }

        public FlightPriceOffer(FlightLocation destination, int amount, string currency, bool isDemo)
        { Destination = destination; Amount = amount; Currency = currency; IsDemo = isDemo; }

        public string PriceLabel => Currency == "USD" ? $"${Amount}" : $"{Amount} {Currency}";
    }

    public interface IFlightPriceDataProvider
    {
        IReadOnlyList<FlightPriceOffer> GetOffers(FlightLocation departure);
    }

}
