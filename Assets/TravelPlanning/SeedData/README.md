# Editable offline demonstration catalog

`catalog.json` is the input for **Travel Planning > Database > Build Seed Database**.
`TravelSeedBuilder.BuildFromJson(jsonPath, outputPath)` is the same importer with
explicit paths for automated tests. It validates inputs, builds a separate staging
database, checks SQLite integrity/foreign keys, then replaces only the packaged
seed. It never opens or deletes `Application.persistentDataPath` user databases.

## What is real and what is simulated

There are 12 destination cities: New York City, Vancouver, Cancun, Rio de Janeiro,
London, Paris, Rome, Cape Town, Dubai, Tokyo, Singapore, and Sydney. Airport codes,
airline names, and venue names identify real places/companies. Their inclusion is
not a claim of current operation, service availability, endorsement, or booking.
Hotel addresses are explicitly **city-level references**, not verified street
addresses. Open Maps to inspect a full address before actual travel.

All flight schedules/numbers, seat counts, USD prices, star ratings, and review
text are **fictional demo data**. Reviews carry `is_demo=1` and explicitly identify
themselves as fictional. They were not copied from Google or attributed to real
travelers. Future screens must retain the demo label. `google_maps_url` opens a
Google Maps **search reference**, not a verified review page; label it accordingly.

Coverage: 12 airports, 12 airlines, 1,440 flight offers across 12 route pairs in
both directions, 36 hotels, 36 restaurants, 36 experiences, 36 hotspots, and 288
reviews (two per place). The selected route pairs are a teaching fixture; they do
not claim every airline flies every shown route. No live API is called.

## Demo dates, units, and IDs

- Flights depart on every local date **June 1–30, 2027**, including June 15/22.
- Search example: JFK to LHR, June 15; return LHR to JFK, June 22.
- There are two airline/time/price options per route/day. Other routes return no
  results naturally; do not treat this as an error.
- Monetary values are integer **USD cents** (12345 means $123.45). Hotels are
  room/night, restaurants sample per-person meal budgets, experiences per-adult
  activity budgets, hotspots free public-area visits (optional services excluded).
- UTC timestamps use `yyyy-MM-ddTHH:mm:ssZ`. Local departure date is separate and
  validated against the origin airport's Windows time zone identifier.
- IDs are stable strings. Catalog IDs are readable; future account/trip/item IDs
  will be GUID strings. This deliberately uses TEXT IDs consistently, including
  user IDs. Do not rename IDs once saved user data refers to them.

## Add data without changing C#

1. Copy an object in the `destinations` array; give it a unique ID and fill fields.
2. Add airports with uppercase three-letter codes and a valid Windows time zone ID.
3. Add airline records if necessary; add flight offers referencing known codes/IDs.
4. Add `places` with kind `hotel`, `restaurant`, `experience`, or `hotspot`.
5. Add reviews referencing a known place ID and matching kind, rating 1–5.
6. Increment `catalogVersion`; run Build Seed Database and inspect the Console.

The importer has no hard-coded city list or required destination count. It rejects
duplicate IDs, missing/padded required strings, unknown references, negative
prices/seats, invalid UTC/local dates, reversed flight times, invalid ratings,
mismatched review kinds, and non-Google HTTPS search URLs. An invalid input leaves
the previous seed unchanged. Editing this seed does not automatically replace an
existing user's database; reset/import decisions belong to a later explicit flow.

`GenerateDemoCatalog.py` is an optional Python 3.9+ maintainer script that recreates
the checked-in initial JSON fixture. **Running it replaces manual JSON edits.**
Teammates do not need Python to edit JSON or build the SQLite seed. Its fixed June
2027 offsets are independently checked against Windows time zone rules by C#.

## Schema boundaries

The packaged seed has application ID `0x5452504C`, schema version 1, and metadata
keys `catalog_version`, `is_demo`, `seed_start_date`, `seed_end_date`. No users,
trips, saved items, watches, notifications, or price history are seeded. Price
history begins with actual simulated changes, with non-null old/new prices.

SQLite foreign keys connect records to real targets. Saved items, reviews, price
watches, and price history each require exactly one typed target. A composite
foreign key means a saved trip must belong to the same user; a notification must
belong to its watch's user. Notification triggers check that the historical price
change matches the watched item. Watch ownership/target and price history are
immutable. Deleting a watch removes its notifications; deactivating it preserves
them. Partial unique indexes prevent duplicate saves and duplicate watches.

`TravelDatabaseSchema.Validate` checks the header, integrity, foreign keys,
required tables/columns, key indexes, and consistency triggers. It allows existing
accounts and edited prices. It is a corruption/schema check, not tamper-proof
authentication for an editable local file.

## Representative primary references

Official venue sites checked while assembling this fixture (names only; no
prices, ratings, or reviews were imported):

- Metropolitan Museum of Art: https://www.metmuseum.org/
- Louvre Museum: https://www.louvre.fr/en
- Gardens by the Bay: https://www.gardensbythebay.com.sg/
- Sydney Opera House: https://www.sydneyoperahouse.com/

The remaining names are recognizable planning examples rather than a fully
verified commercial directory. Use their Maps search reference to resolve the
exact property or attraction. Cuisine and street-address verification can be
expanded as catalog data without changing the importer.
