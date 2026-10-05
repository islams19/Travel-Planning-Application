# Milestone 6 — Saved trips inspection

These are the starting-point findings on **DuyEdit**. Existing uncommitted
milestone work is preserved. This inspection does not claim successful M6
compilation, tests or Windows validation.

## Schema already supports the feature

`Assets/TravelPlanning/Runtime/Data/TravelDatabaseSchema.cs` defines:

```text
trips:
  id, user_id, name, start_date, end_date, created_utc, updated_utc
saved_items:
  id, user_id, trip_id, flight_id, hotel_id, restaurant_id,
  experience_id, hotspot_id, created_utc
```

IDs are TEXT; application-generated user/trip/item IDs can be GUIDs. Catalog
IDs remain stable seeded references. `saved_items` requires exactly one typed
target. Its `(trip_id,user_id)` foreign key references the same pair in trips,
so an item cannot claim another user's trip. Each target also has a foreign key.

`trips_user` and `saved_items_owner` support account-specific lists. Five
partial unique indexes named `saved_flight`, `saved_hotel`, `saved_restaurant`,
`saved_experience` and `saved_hotspot` prevent duplicate targets within one trip.
The same item may still belong to another trip. The existing schema is enough;
no migration, seed replacement or personal database reset is required.

SQL enforces non-null trip dates and lexicographic end >= start, but not valid
calendar dates or a useful name. The application must validate those before
inserting. There is no stored price snapshot, quantity, stay duration or booking.
Saved prices therefore come from the current catalog and must retain their
units; adding them into one total would be misleading.

## Current UI and lifecycle boundaries

`UI/Flights/FlightResultRow.cs` receives a FlightOption containing a stable ID
and original flight dates. `FlightSearchPage.Fill` creates outbound and return
rows separately. An optional save callback can preserve the existing Show API
and prior tests; each direction remains an independent choice.

`UI/Destinations/PlaceCard.cs` and `PlaceSection.cs` already pass a review
callback and retain the PlaceOption. Add a separate optional save callback with
both category and ID. Preserve the existing review button and all four categories.
`DestinationHubPage.ContentCleared` provides a cancellation boundary when hub
data changes or the user navigates away.

`UI/Reviews/ReviewsPage.cs` uses a separate overlay, an input blocker, explicit
button navigation and cancellation/revision checks. A saved-trips overlay must
avoid competing modal screens and restore the underlying page without calling
an Open method that resets its filters or city. Logout must close all overlays.

`LoginPage.SignedInUserId` is the account identity; email/display text is not an
ownership key. Capture that ID for each operation and reject stale completions
after logout or a different login. New saves must never read a replacement
session's ID halfway through an awaited operation.

## Write and ownership rules

`TravelDatabase.ExecuteAsync` serializes background work and enables foreign
keys. It does not automatically wrap operations in a transaction. Saving should
check trip ownership, duplicate state and target existence, insert the item,
and update the trip timestamp in one transaction. All values remain parameters;
typed SQL column names come only from a fixed supported-category mapping.

Every trip lookup includes its owner. Every item lookup/removal includes owner
and trip. Foreign keys help protect writes but do not filter reads automatically.
A broad `INSERT OR IGNORE` risks hiding invalid-input errors; duplicate saves
should receive an explicit friendly result instead.

Check cancellation before committing. Closing a screen cannot undo a save
already committed to SQLite, so the UI must not promise that it can. Disable
repeated mutation clicks while busy and reload actual saved state on reopening.

## Setup and regression risks

Existing FlightSearchSetup and DestinationHubSetup reuse their prefabs and
return early when their controllers already exist. TripSetup must upgrade both
FlightResultRow.prefab and PlaceCard.prefab idempotently, not only modify code
that creates new assets. Keep asset metadata and existing review references.

The signed-in account card gains a Saved trips entry and moves its logout
button. This is distinct from Kevin's original login form: preserve the original
login RectTransforms. LoginPage's intended code change is only its isolated
development smoke-test guard.

Tests already cover duplicate saved targets, mismatched ownership and database
reopening. M6 needs service and UI coverage for two users, all five target types,
create/empty/list/remove flows, duplicate requests, invalid input, queued
cancellation, committed-save recovery and unchanged flight/hub/review navigation.
No price watches, notifications or simulated purchasing belongs to this milestone.
