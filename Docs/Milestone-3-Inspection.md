# Milestone 3 — Flight search inspection

Read-only findings from the packaged seed database and existing login code. This
document describes the starting point, not completed flight-search functionality
or player validation. Work remains on `DuyEdit`.

## Catalog available for search

`Assets/StreamingAssets/Database/travel_seed.db` contains 1,440 flight offers,
12 airlines, and 12 airports. There are 24 directed routes, 60 offers per route:
two airline/time options on each departure date from **June 1–30, 2027**.

Reciprocal route pairs:

- JFK ↔ LHR, CDG, YVR, CUN, GIG
- LHR ↔ FCO, CPT, DXB
- DXB ↔ SIN
- HND ↔ SIN, SYD
- SIN ↔ SYD

These are fictional demonstration schedules, availability and prices. Missing
route combinations are expected empty results. Prices are integer USD cents;
the current fixture ranges from 18,000 to 89,700 cents.

Example: JFK → LHR on June 15, 2027 returns BA100 at $527 and AA101 at $562.
They depart at 12:00Z and 21:00Z, corresponding to 08:00 and 17:00 in New York.
Their arrivals are 19:00Z June 15 and 04:00Z June 16. June 22 includes return
offers, making June 15/22 useful repeatable demonstration dates.

## Data contract and time handling

`flights` columns:

```text
id, airline_id, flight_number, origin_airport_id, destination_airport_id,
departure_utc, arrival_utc, departure_local_date, price_cents,
available_seats, currency
```

IDs and timestamps are TEXT; price/seats are INTEGER. UTC timestamps use
`yyyy-MM-ddTHH:mm:ssZ`; local departure dates use `yyyy-MM-dd`. Airports expose
`id`, `destination_id`, `name`, and `time_zone`; time_zone contains Windows time
zone identifiers. Join airports to destinations for city labels and airlines for
company names. Display each endpoint in its own local zone, retaining its date
when arrival crosses midnight. A UTC hour is not a local departure-time filter.

The database is a fixed-date fixture. Valid demo dates must continue to work when
the calendar advances; do not silently reject June 2027 as a past date.

## Query-plan evidence

The seed has `flights_search` on
`(origin_airport_id,destination_airport_id,departure_local_date,price_cents)`.
Read-only SQLite `EXPLAIN QUERY PLAN` confirmed:

- Required exact origin, destination and local-date comparisons with price order:
  `SEARCH flights USING INDEX flights_search`.
- The same comparisons with UTC departure order: index search, followed by a
  temporary sort of the matching rows.
- Optional-OR wrappers around required fields, such as
  `(? IS NULL OR origin_airport_id=?)`: `SCAN flights` and a temporary sort.

Keep required route/date comparisons direct. Parameterize input values and
choose sorting expressions from fixed known options. Additional filters must
not replace indexed airport/date values with formatted display strings. Query
plans establish index use, not the 2–3 second end-to-end performance requirement;
the implementation needs its own timed tests and Windows player validation.

## Existing application integration

At inspection, `LoginPage` exposes `SignedInEmail`, `SignedInUserId`, `LoggedIn`
(email argument), `IsReady`, and `IsBusy`. Its initialized `TravelDatabase` is
local to `Start`; logout is private and has no event. Destroying AccountController
logs out the service. Therefore flight search needs access to the initialized
database and an explicit logout/session boundary while retaining the controller.

`TravelDatabase.ExecuteAsync<T>(Func<SQLiteConnection,T>, CancellationToken)` is
the existing serialized worker-thread entry point for queries. Use it rather
than a second connection owner on the UI thread. UI updates remain on Unity's
main thread and must ignore canceled/stale searches after logout.

Scene roots include Canvas, Camera, EventSystem and AccountController. Under
`Canvas/Right Panel`, LoginCard and RegistrationCard provide authentication;
SignedInCard currently contains the next-milestone placeholder. Preserve these
authentication visuals and match LiberationSans, white surfaces and #3157FF
accent in new screens. Existing password policy is **10–128 characters**.

## Kevin branch scope

Inspected `origin/kevin-frontend-login` reference
`0ae085f9889591310dc46e6e14b7bd507c8c0ab3` without importing:

- `Assets/Scenes/FlightScreen.unity` contains only camera/light objects.
- `Assets/Scenes/HomeScreen.unity` contains an earlier “Travel R Us!” login form.

Neither supplies a working flight-search interface, so neither is required for
this milestone. No AGENTS.md was found in the project or checked immediate
ancestor directories. This inspection changed no runtime code, assets, or data.
