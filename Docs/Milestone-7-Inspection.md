# Milestone 7 — Price tracking inspection

Starting-point inspection on **DuyEdit**, preserving earlier uncommitted work.
No successful M7 tests or build are claimed in this document.

## Existing data and constraints

`Runtime/Data/TravelDatabaseSchema.cs` already defines all required tables:

```text
price_watches:
  id, user_id, flight_id, hotel_id, last_price_cents, active, created_utc
price_history:
  id, flight_id, hotel_id, old_price_cents, new_price_cents, changed_utc
notifications:
  id, user_id, watch_id, price_history_id, title, body, created_utc, is_read
```

Watches and history require exactly one typed target: flight or hotel. Prices
are nonnegative integer cents. History requires both old/new prices and they
must differ; updates to a history row are forbidden. Watch ownership and target
cannot change after creation. Notifications reference a watch belonging to the
same user, and triggers verify that its history describes that watched target.
A watch/history pair can produce only one notification.

Unique `watches_flight` and `watches_hotel` indexes include inactive rows. Stop
tracking should set `active=0`; reactivation must reuse that row with the current
baseline. Deleting a watch would cascade-delete its notifications. Soft stopping
preserves them, which matches the intended behavior.

`notifications_unread(user_id,is_read,created_utc)` supports the unread badge.
Target indexes support finding all watchers of a changed item. A chronological
list covering both read/unread rows may use an extra sort; the small local
catalog does not require a schema change for that.

The inspected seed has 1,440 flights, 36 hotels, and zero watches, history or
notifications. Example stable IDs/prices at this inspection:

| Target | Name | Seed price |
| --- | --- | --- |
| JFK-LHR-20270615-BA | BA100, JFK to LHR, June 15 | USD 527.00 / flight |
| JFK-LHR-20270615-AA | AA101, JFK to LHR, June 15 | USD 562.00 / flight |
| london-hotel-1 | The Savoy | USD 160.00 / room / night |
| london-hotel-2 | The Ritz London | USD 245.00 / room / night |

No migration, seed rebuild or personal database reset is needed.

## Approved change path

PriceTrackingService binds to an immutable signed-in owner. It owns that user's
watch and notification queries/actions. DemoPriceService changes the shared
fictional catalog through one transaction: update the price, add history, notify
all active watchers of the target, and advance their last observed prices.
An unchanged price is a no-op. Other local users' notifications persist while
they are logged out and appear at their next login.

This deliberately does not detect arbitrary direct SQL edits. There is no
polling or catch-up scanner. Refresh the badge on login, panel opening and
explicit actions/refresh. New/reactivated watches use current prices and do not
receive alerts for earlier changes. Every user-visible query and read-state
mutation needs an owner filter; foreign keys alone do not protect reads.

## UI integration risks

FlightResultRow and PlaceCard already have save callbacks; the latter also has
reviews. Add optional tracking callbacks without breaking existing signatures.
Only flight/hotel cards offer tracking. Existing prefabs must be upgraded rather
than relying on creation-only setup helpers.

SavedTripsPage locks three underlying CanvasGroups and restores their prior
values when closing. Close Saved trips and Reviews before the tracking modal
locks those groups, or the wrong disabled state can be restored. Retain
Unity-aware null checks when creating/repairing native CanvasGroup components.
PriceTrackingCanvas uses overlay order 6, above the earlier screens.

The selected target must be freshly loaded in the tracking panel. Existing
flight/hub cards retain cached prices until the user reruns the search or reloads
the hub. Saved trips resolves fresh prices when reopened. Do not silently reset
filters or city/scroll state to refresh these screens after a simulation.

LoginPage's intended code change is the isolated smoke-test guard. The signed-in
card gains an entry, while Kevin's original 22 login RectTransforms remain the
preservation baseline. A separate controller must clear badges, lists, target
selection and pending requests on logout and reject stale results from an old
session. No OS notifications, live-price API, purchasing or M8 polish is in scope.

## Validation needed

Exercise duplicate tracking, stop/retrack baselines, both price directions,
same-price no-op, invalid prices, two users watching one item, exactly-once
notifications, owner-filtered mark-read, canceled/rolled-back operations and
relaunch persistence. Test the modal's interaction restoration and preserve
M3–M6 navigation. The simulator must be unavailable in ordinary release builds;
its writes still need explicit user action in Editor/development builds.
