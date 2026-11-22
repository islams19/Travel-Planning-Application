# Shared travel locations

Search Flights and Explore World consume `TravelLocationCatalog`. The `BookingDestination` field is
the existing canonical booking UI label. Multiple airports resolve to the same city label; no airport,
price or animation state is introduced into the live booking model. The five legacy map-only cities
(Atlanta, Seattle, Dallas, Washington and Frankfurt) remain map-only and fail the explicit booking
handoff gracefully. All 62 previously supported booking destinations have airport coordinates.

Airport names, IATA identifiers and WGS84 coordinates were extracted from the public-domain
[OurAirports airport dataset](https://ourairports.com/data/) on 2026-11-21, using
[airports.csv](https://davidmegginson.github.io/ourairports-data/airports.csv).
The city assignment is the app's supported city/metro label, which may differ from the airport's
municipality (for example Newark serving New York). This is bundled offline demo data, not live
route availability or airport operating advice. No network, SDK or API key is needed at runtime.


## Airport-local flight times

Each airport now carries an explicit IANA `TimeZoneId`. Airport identifiers were checked
against [OpenFlights airport data](https://github.com/jpatokal/openflights/blob/master/data/airports.dat);
current IANA city identifiers are used for Berlin, Doha, Istanbul, Beijing Daxing, and Melbourne.
Legacy Calcutta and Buenos Aires zone aliases use Kolkata and Argentina/Buenos_Aires.

`AirportClock` uses the operating system's date-aware `TimeZoneInfo` rules, never fixed
UTC offsets. Its Windows/Mono fallback names are from the Unicode CLDR
[windowsZones mapping](https://github.com/unicode-org/cldr/blob/main/common/supplemental/windowsZones.xml).
No network calls or additional package are required at runtime.

Flight segments store UTC departure/arrival timestamps and the selected origin-local
travel date. Clock labels convert at each airport; day offsets compare the airport-local
calendar date with the origin travel date. Flight and layover durations use UTC differences.
Autumn ambiguous schedule times choose the earlier occurrence; nonexistent spring times
are rejected. Existing schedules avoid the missing clock hour. See
[TimeZoneInfo documentation](https://learn.microsoft.com/en-us/dotnet/api/system.timezoneinfo).

Validation covers every configured zone, winter/summer offsets at JFK/ATL/CUN, both DST
transitions, UTC-midnight versus local-date arrival offsets, and absolute elapsed time
across a daylight-saving clock change.
