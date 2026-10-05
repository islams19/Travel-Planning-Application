using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using TravelPlanning.Flights;
using UnityEngine;

namespace TravelPlanning.UI.Flights
{
    /// <summary>Populates flight controls and owns result rows. It never searches the database.</summary>
    internal sealed class FlightSearchView
    {
        private readonly TMP_Dropdown origin, destination, departureDate, returnDate, airline, timeBand, sort;
        private readonly TMP_InputField maximumPrice;
        private readonly TMP_Text outboundHeading, returnHeading, status;
        private readonly RectTransform outboundContent, returnContent;
        private readonly UnityEngine.UI.ScrollRect outboundScroll, returnScroll;
        private readonly FlightResultRow rowPrefab;
        private readonly List<GameObject> rows = new List<GameObject>();
        public FlightSearchView(TMP_Dropdown origin, TMP_Dropdown destination, TMP_Dropdown departureDate,
            TMP_Dropdown returnDate, TMP_Dropdown airline, TMP_Dropdown timeBand, TMP_Dropdown sort, TMP_InputField maximumPrice,
            TMP_Text outboundHeading, TMP_Text returnHeading, TMP_Text status, RectTransform outboundContent, RectTransform returnContent,
            UnityEngine.UI.ScrollRect outboundScroll, UnityEngine.UI.ScrollRect returnScroll, FlightResultRow rowPrefab)
        {
            this.origin = origin;
            this.destination = destination;
            this.departureDate = departureDate;
            this.returnDate = returnDate;
            this.airline = airline;
            this.timeBand = timeBand;
            this.sort = sort;
            this.maximumPrice = maximumPrice;
            this.outboundHeading = outboundHeading;
            this.returnHeading = returnHeading;
            this.status = status;
            this.outboundContent = outboundContent;
            this.returnContent = returnContent;
            this.outboundScroll = outboundScroll;
            this.returnScroll = returnScroll;
            this.rowPrefab = rowPrefab;
        }

        public void PopulateOptions(FlightSearchOptions options)
        {
            SetOptions(origin, options.Airports.Select(x => x.City + " (" + x.Id + ")").ToList());
            SetOptions(destination, options.Airports.Select(x => x.City + " (" + x.Id + ")").ToList());
            var dates = Shared.CatalogDateChoices.Create(options.StartDate, options.EndDate);
            SetOptions(departureDate, dates);
            SetOptions(returnDate, dates);
            origin.value = Math.Max(0, options.Airports.ToList().FindIndex(x => x.Id == "JFK"));
            destination.value = Math.Max(0, options.Airports.ToList().FindIndex(x => x.Id == "LHR"));
            departureDate.value = Math.Max(0, dates.IndexOf("2027-06-15"));
            returnDate.value = Math.Max(0, dates.IndexOf("2027-06-22"));
            var airlines = new List<string>
            {
                "All airlines"
            };
            airlines.AddRange(options.Airlines.Select(x => x.Name));
            SetOptions(airline, airlines);
            SetOptions(timeBand, new List<string> { "Any departure time", "Night (00:00-05:59)", "Morning (06:00-11:59)",
                "Afternoon (12:00-17:59)", "Evening (18:00-23:59)" });
            SetOptions(sort, new List<string> { "Price: low to high", "Price: high to low", "Departure: earliest", "Departure: latest", "Airline: A-Z" });
            maximumPrice.text = "";
        }

        public void Show(FlightSearchResult result, FlightSearchRequest request, Action<FlightOption> save, Action<FlightOption> track)
        {
            Fill(result.Outbound, outboundContent, save, track);
            Fill(result.Return, returnContent, save, track);
            outboundHeading.text = "Outbound • " + request.OriginAirportId + " → " + request.DestinationAirportId +
                " • " + request.DepartureDate + " (" + result.Outbound.Count + ")";
            returnHeading.text = "Return • " + request.DestinationAirportId + " → " + request.OriginAirportId +
                " • " + request.ReturnDate + " (" + result.Return.Count + ")";
            status.text = result.Outbound.Count == 0 && result.Return.Count == 0 ? "No flights match. Try another route or date, or clear the filters." : result.Outbound.Count == 0 ||
                result.Return.Count == 0 ? "One direction has no matching flights. Try another date or clear the filters." : "Choose flights independently for each direction. Prices are per flight in USD.";
            Canvas.ForceUpdateCanvases();
            outboundScroll.verticalNormalizedPosition = returnScroll.verticalNormalizedPosition = 1;
        }

        private void Fill(IReadOnlyList<FlightOption> flights, Transform parent, Action<FlightOption> save, Action<FlightOption> track)
        {
            foreach (var flight in flights)
            {
                var row = UnityEngine.Object.Instantiate(rowPrefab, parent);
                row.gameObject.SetActive(true);
                row.Show(flight, save, track);
                rows.Add(row.gameObject);
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

                row.SetActive(false);
                UnityEngine.Object.Destroy(row);
            }

            rows.Clear();
            if (outboundHeading)
            {
                outboundHeading.text = "Outbound";
            }

            if (returnHeading)
            {
                returnHeading.text = "Return";
            }
        }

        private static void SetOptions(TMP_Dropdown dropdown, List<string> values)
        {
            dropdown.ClearOptions();
            dropdown.AddOptions(values);
            dropdown.value = 0;
            dropdown.RefreshShownValue();
        }
    }
}
