# Milestone 5 — Reviews inspection

Read-only starting-point findings and integration boundaries on `DuyEdit`.
This document does not record successful compilation, tests, or a Windows build.
Existing uncommitted work from earlier milestones remains in place.

## Data already available

The seed has 144 places across hotels, restaurants, experiences and hotspots,
with two fictional reviews per place: 288 review rows. Existing schema and seed
files already support this milestone; no migration or runtime database reset
is needed. Existing user accounts and trips must remain intact.

`Runtime/Data/TravelDatabaseSchema.cs` defines `reviews` with:

```text
id, hotel_id, restaurant_id, experience_id, hotspot_id,
traveler_name, rating, body, is_demo
```

Exactly one target ID must be present. Each target has a foreign key and a
partial index (`reviews_hotel`, `reviews_restaurant`, `reviews_experience`,
`reviews_hotspot`). Ratings are integer values from 1 to 5. There is no review
timestamp, so this milestone must not offer a misleading newest-first sort.
Stable ID order is sufficient for the small preset catalog.

Every place table has `google_maps_url`. Seed links are Google Maps searches,
not verified direct review-page links. Names refer to established venues;
review text, traveler identities, ratings and prices are demonstration data.
The existing seed URL check alone is not an adequate external-launch boundary:
the runtime database can be edited separately from seed generation.

## Existing UI integration

`DestinationModels.cs` already carries `PlaceOption.Id`, `Category`,
`DestinationId` and `GoogleMapsUrl`. A review request must pass both category
and ID: an ID alone does not identify which typed review column to query.
`PlaceCard.Show` and `PlaceSection.Show` are the points where a callback can
travel from a rendered card to `DestinationHubPage`.

The hub owns city selection, loaded cards and scroll position. A separate
Reviews overlay can preserve them while blocking clicks on the underlying
screen. `ReviewRequested` carries the selected place; `ContentCleared` closes
and cancels reviews when hub data or navigation changes. Logout and object
destruction must also invalidate pending work so a late result cannot reopen
the overlay for a different session.

`DestinationHubSetup.Prepare` and its card creation helper reuse existing scene
objects and prefabs. Consequently, the Reviews setup must upgrade the existing
place-card prefab as well as create its own overlay. Editing only the original
creation path would leave an already-prepared project without review buttons.

## Query and browser boundaries

`ReviewService` uses a fixed category-to-table/column mapping and parameterized
place IDs. It reads through `TravelDatabase.ExecuteAsync`, which serializes
SQLite work on a background worker. UI changes happen after the awaited result
returns to Unity. Cancellation and request revision checks prevent stale UI.
The existing typed indexes support the place-specific review lookup.

`GoogleMapsReference` validates again before launch and returns a canonical
HTTPS Google Maps search URL. It rejects unexpected hosts, paths, ports,
credentials, fragments, duplicate or extra query keys, malformed encoding and
empty search text. Only `api=1` and `query` are accepted. The browser opens only
after an explicit user click; automated tests should replace the opener with a
recording callback and never launch a real browser.

Unity 6.3 supports [Application.OpenURL](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application.OpenURL.html).
Because this API can handle more than web pages, passing unchecked database
text directly to it would be unsafe.

## Regression boundaries

Preserve the M4 navigation, city selection, scroll state, price labels and
flight filters/results. Keep Kevin's login layout and legacy Input Manager.
The only planned LoginPage change is the isolated development smoke-test flag.
Retain the M4 test fixture APIs while adding optional card callbacks. Show full
review text as plain text; disable TMP rich-text interpretation for stored
traveler names and bodies. Draw stars with uGUI geometry because the existing
LiberationSans asset lacks a suitable star glyph. No Google data import,
review submission, saved-trip changes or price tracking belongs to this step.
