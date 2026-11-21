using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TravelApp.FlightMap
{
    /// <summary>Provides searchable, replaceable departure and destination selections.</summary>
    public sealed class FlightLocationSelector : MonoBehaviour
    {
        [SerializeField] TMP_InputField departureSearch;
        [SerializeField] TMP_Dropdown departureDropdown;
        [SerializeField] TMP_InputField destinationSearch;
        [SerializeField] TMP_Dropdown destinationDropdown;
        readonly List<FlightLocation> departureResults = new();
        readonly List<FlightLocation> destinationResults = new();
        public event Action<FlightLocation, FlightLocation> SelectionChanged;
        public FlightLocation Departure => Selected(departureResults, departureDropdown);
        public FlightLocation Destination => Selected(destinationResults, destinationDropdown);

        /// <summary>Selects a destination from a map price bubble while preserving departure.</summary>
        public bool SelectDestinationByCode(string code)
        {
            if (!SelectByCode(destinationSearch, destinationDropdown, destinationResults, code)) return false;
            Notify();
            return true;
        }

        /// <summary>Public selection entry used by pricing refresh tests and future UI integrations.</summary>
        public bool SelectDepartureByCode(string code)
        {
            if (!SelectByCode(departureSearch, departureDropdown, departureResults, code)) return false;
            Notify();
            return true;
        }

        public void Configure(TMP_InputField fromSearch, TMP_Dropdown fromDrop, TMP_InputField toSearch, TMP_Dropdown toDrop)
        { departureSearch = fromSearch; departureDropdown = fromDrop; destinationSearch = toSearch; destinationDropdown = toDrop; }

        void Awake()
        {
            if (!ValidateReferences()) { enabled = false; return; }
            RepairDropdownTemplate(departureDropdown);
            RepairDropdownTemplate(destinationDropdown);
            departureSearch.onValueChanged.AddListener(FilterDeparture);
            destinationSearch.onValueChanged.AddListener(FilterDestination);
            departureDropdown.onValueChanged.AddListener(_ => Notify());
            destinationDropdown.onValueChanged.AddListener(_ => Notify());
            FilterDeparture(string.Empty); FilterDestination(string.Empty);
            SelectCode(departureDropdown, departureResults, "JFK");
            SelectCode(destinationDropdown, destinationResults, "LAX");
            Notify();
        }

        void FilterDeparture(string query) { Populate(departureDropdown, departureResults, query); Notify(); }
        void FilterDestination(string query) { Populate(destinationDropdown, destinationResults, query); Notify(); }

        static void Populate(TMP_Dropdown dropdown, List<FlightLocation> results, string query)
        {
            results.Clear(); dropdown.ClearOptions(); var labels = new List<string>();
            foreach (FlightLocation item in FlightLocationDatabase.Locations)
                if (FlightLocationDatabase.Matches(item, query)) { results.Add(item); labels.Add(item.DisplayName); }
            if (labels.Count == 0) labels.Add("No matching locations");
            dropdown.AddOptions(labels); dropdown.SetValueWithoutNotify(0); dropdown.RefreshShownValue();
        }

        static FlightLocation Selected(List<FlightLocation> values, TMP_Dropdown dropdown)
            => values.Count == 0 ? null : values[Mathf.Clamp(dropdown.value, 0, values.Count - 1)];

        static void RepairDropdownTemplate(TMP_Dropdown dropdown)
        {
            if (dropdown?.template == null) return;
            ScrollRect scroll = dropdown.template.GetComponent<ScrollRect>();
            Transform viewportTransform = dropdown.template.Find("Viewport");
            Transform contentTransform = viewportTransform != null ? viewportTransform.Find("Content") : null;
            if (scroll == null || viewportTransform == null || contentTransform == null) return;
            scroll.viewport = viewportTransform as RectTransform;
            scroll.content = contentTransform as RectTransform;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
        }

        static void SelectCode(TMP_Dropdown dropdown, List<FlightLocation> values, string code)
        { int index = values.FindIndex(x => x.code == code); if (index >= 0) { dropdown.SetValueWithoutNotify(index); dropdown.RefreshShownValue(); } }

        static bool SelectByCode(TMP_InputField search, TMP_Dropdown dropdown, List<FlightLocation> values, string code)
        {
            search.SetTextWithoutNotify(string.Empty);
            Populate(dropdown, values, string.Empty);
            int index = values.FindIndex(location => location.code == code);
            if (index < 0) return false;
            dropdown.SetValueWithoutNotify(index);
            dropdown.RefreshShownValue();
            return true;
        }

        void Notify() { if (Departure != null && Destination != null && Departure != Destination) SelectionChanged?.Invoke(Departure, Destination); }

        bool ValidateReferences()
        {
            bool valid = departureSearch != null && departureDropdown != null && destinationSearch != null && destinationDropdown != null;
            if (!valid) Debug.LogError("FlightLocationSelector is missing UI references.", this);
            return valid;
        }
    }
}
