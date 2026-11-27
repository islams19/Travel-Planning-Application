# Offline flight rules

These are illustrative search results, not published airline schedules or quotes. No API, credentials,
network request, or package is used at runtime. The generic product catalog is separate.

- `MockRouteRules` uses the shared airport coordinates, a 14,500 km leg ceiling, at least 150 km
  between connection points, and a total-path ceiling of 1.45 × direct distance + 250 km. Two-stop
  candidates must advance toward the destination. Short routes without a reasonable hub stay nonstop.
- Each segment gets its own distance-derived block time: taxi/climb/descent allowance, effective
  cruise speed, airway allowance, and a small deterministic directional/time variation. Layovers
  start at 65 minutes domestically or 110 minutes when an international segment is involved.
- `MockFlightGenerator` creates 12 deterministic options in the existing `FlightResultsPage.Flight`
  model. Carrier profiles reflect broad operating regions, not guaranteed routes. Marketing and
  operating carriers can differ; segment details show the operating carrier and flight number.
- Geographic carrier guidance: [JetBlue transatlantic overview](https://s202.q4cdn.com/521076508/files/doc_downloads/factsheets/2025/JB_MediaFactSheets_Transatlantic.pdf),
  [British Airways network](https://www.britishairways.com/content/information/flight-information/our-route-network),
  [Singapore Airlines network](https://www.singaporeair.com/en_UK/gb/plan-travel/destinations/where-we-fly/).
  These rules intentionally do not forecast future aircraft/range changes or claim live availability.
- `MockTravelPricing` owns one shared Economy basis using distance, domestic/regional/intercontinental
  classification and optional travel date. Calendar estimates add the selected date; Explore has no
  selected travel date and displays an indicative Economy estimate. Flight results add deterministic
  itinerary/stops variation. A connection can cost less or more than a nonstop.
- Cabin adjustment occurs once in the booking controller; passenger-category percentages occur once
  in `PassengerParty`. Seat Selection and payment copy the selected itinerary fare. Preliminary
  map/calendar estimates never overwrite a selected booking price.
- New itinerary IDs include route, date, option index, an offer fingerprint and actual segment count. Historical slot IDs
  remain understood for old draft validation. A legacy selected flight that cannot be resolved to an
  exact new ID requires reselection on resume; route, dates, passenger details and contact are retained.
  This avoids silently changing a saved JetBlue selection into another carrier or a new connection.
- Airline logos are local resources with code-badge fallback. All new cards reuse the existing wing
  imagery. Missing logo files do not trigger network loading.
