# Milestone 4 — Destination hub inspection

This is the read-only starting-point inspection. It does not claim that hub
implementation, tests, or a player build have passed. Branch: `DuyEdit`. Pending
changes from earlier milestones were preserved.

## Existing catalog

The packaged SQLite seed contains 12 destinations. Each has three hotels, three
restaurants, three experiences and three hotspots: 36 records per table, 144
places overall. Each place currently has two fictional demonstration reviews.

The destination cities are New York City, Vancouver, Cancun, Rio de Janeiro,
London, Paris, Rome, Cape Town, Dubai, Tokyo, Singapore and Sydney. The catalog
version is `demo-2027-06-v1`.

All four place tables share these columns:

```text
id, destination_id, name, description, address,
price_cents, currency, google_maps_url
```

Prices use integer USD cents. Hotels require a value through a database CHECK;
the other tables permit NULL. Zero and NULL have different meanings: zero can
display as Free; NULL must display as Price unavailable. Hotel budgets are per
room/night, restaurant budgets per person/meal, and experience budgets per
adult/activity. Seed hotspots represent free public-area visits.

Names reference real places, but prices, schedules, ratings and review text are
fictional. Addresses explicitly identify only the city, not a verified street
address. `google_maps_url` holds an external search reference, not a verified
Google review URL. Link actions and detailed review browsing belong to M5.

## Queries and ratings

Existing indexes cover each category's `destination_id`. A read-only query plan
for `SELECT * FROM hotels WHERE destination_id=?` used `hotels_destination`.
No table/index migration is necessary for a destination hub.

`reviews` has `hotel_id`, `restaurant_id`, `experience_id`, and `hotspot_id`, with
exactly one target required; `rating` is 1–5 and `is_demo` marks sample content.
Each review-target column has an index. Aggregate preview can use AVG(rating) and
COUNT(*), keeping an absent average nullable. Show No reviews for zero matches.
Do not accidentally count an unmatched LEFT JOIN row as one review.

Use the existing `TravelDatabase.ExecuteAsync` worker path, pass IDs as SQL
parameters, and select category table/target names from a fixed internal list.
Never accept arbitrary table names from a dropdown or user-entered text.

## Navigation starting point

`LoginPage` now exposes its initialized `Database`, `SignedInUserId`, `LoggedIn`,
`LoggedOut`, and public `Logout()`. AccountController stays alive outside hidden
Canvases. Existing authentication policy remains 10–128 password characters.

`FlightSearchPage.Open()` currently loads defaults. `Close()` cancels requests,
clears rows and returns to the account Canvas. These are unsuitable for a hub
round trip that must preserve flight filters/results. Explicit suspend/resume
is needed; a canceled search should retry its preserved inputs when appropriate.

At inspection, `AirportOption` lacks a destination ID. Add the existing database
`airports.destination_id` to its options projection to open a hub for the selected
flight destination. Load the hub's own city dropdown directly from destinations,
so a future city without an airport remains discoverable.

Recommended boundary: DestinationCanvas and a root controller in Login.unity,
with four category sections in a scrollable content area. Logout must cancel
hub loading, clear stale content and hide the hub regardless of listener order.
Preserve Kevin's login geometry and reuse LiberationSans/#3157FF/white styling.

## Existing validation conventions

Runtime services/models are separate from UI controllers and card prefabs.
Editor setup helpers create/wire UI; tests check their serialized references and
scroll hierarchy. NUnit service tests and Play-mode UI flows live under
`Assets/TravelPlanning/Tests/EditMode`. Windows smoke scripts use hidden players,
UUID-named isolated databases and explicit result markers. Never substitute the
normal travel.db for an automated fixture.

The M3 report records 56 passing regression tests and two actual-player runs.
Those are prior milestone results, not M4 results. Preserve that regression
coverage and add hub-specific loading, navigation, empty-state, cancellation,
price/rating and data-preservation checks. Rendered appearance and manual resize
behavior need their own checks; headless tests do not establish them.
