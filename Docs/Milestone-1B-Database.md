# Milestone 1B — Seeded travel database

## Result and scope

The complete version-1 schema and first-launch copy are implemented. This is a database diagnostic, not the application navigation UI. **Existing login page: untouched.** Authentication integration belongs to Milestone 2. Old LiteDB account code is still present and must not be used as secure production authentication.

12 destinations: New York City, Vancouver, Cancun, Rio de Janeiro, London, Paris, Rome, Cape Town, Dubai, Tokyo, Singapore, Sydney. The JSON contains 1,440 flight offers, 36 hotels, 36 restaurants, 36 experiences, 36 hotspots, and 288 explicitly fictional reviews. USD only; fixed June 1–30, 2027 departure dates. Example: JFK → LHR June 15, return June 22. Only seeded routes have results.

## Versions

Unity 6000.3.24f1; uGUI 2.0.0; legacy Input Manager; Windows x64 Mono/.NET Standard 2.1. SQLite-net-pcl 1.11.285, SQLitePCLRaw core/provider.e_sqlite3 3.0.3, SourceGear.sqlite3 3.53.3. Native Windows x64 binary and managed assemblies were installed in Milestone 1A. See `Assets/Plugins/SQLite/README.md` and `tools/Import-Sqlite.ps1` for pinned install details and hashes. No additional packages are required.

## Editor step 1 — Generate the seed

1. Open this project with Unity Hub using 6000.3.24f1; wait for compilation.
2. Choose **Travel Planning > Database > Build Seed Database**.
3. Open **Window > General > Console**. Resolve any red error before continuing.
4. In Project, inspect `Assets/StreamingAssets/Database/travel_seed.db`. This generated file and its `.meta` belong in Git alongside the JSON source.

The importer reads `Assets/TravelPlanning/SeedData/catalog.json`. It validates references, dates, prices and duplicate IDs before building a temporary database, then replaces only the packaged seed. The packaged seed contains no accounts or saved trips.

## Editor step 2 — Run the diagnostic

1. Choose **Travel Planning > Travel Database > 2 - Create Diagnostic Scene**.
2. Save prompted scene changes if needed. The generated scene is `Assets/TravelPlanning/Scenes/TravelDatabase.unity`.
3. Click **Play**. Expect `PASS - seed copied and validated` on a fresh installation, or `PASS - existing database preserved` on later runs.
4. Confirm 12 destinations, 1,440 flights, 36 of each place type and 288 reviews. The worker and UI thread numbers should differ.
5. Click **Validate again (preserves your database)**. Expect existing database preserved.
6. Stop Play. The diagnostic prints the full writable database path. Do not delete an existing database just to make a test pass.

The scene generator wires the four Inspector references automatically. If building this diagnostic manually, select the object holding **Travel Database Page** and drag the `Status`, `Details`, `Heartbeat` Text components and `RunAgain` Button into their corresponding fields. This diagnostic uses legacy uGUI Text; future application screens will use Kevin's TMP styling.

## Editor step 3 — Windows build

1. Choose **Travel Planning > Travel Database > 3 - Build Windows x64**.
2. Wait for `TRAVEL_DATABASE_BUILD_PASS` in Console.
3. Run `Builds/TravelDatabase/TravelPlannerDatabase.exe` from Explorer.
4. Confirm PASS, close, reopen, and confirm existing database preserved.
5. Resize the window and inspect text/buttons manually; automated headless checks cannot judge visual clipping.

The build command includes only the diagnostic scene and copies license notices. It does not replace the project's shared scene list. To repeat automated player checks, open PowerShell in the repository and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Test-TravelDatabase.ps1
```

This tests a separate UUID-named database, inserts a clearly artificial account/trip fixture, edits the trip and verifies it after restart. The fixture is not an authentication implementation. The script also checks corruption preservation and missing-seed behavior, restoring the built seed in `finally`.

## How database access works

`TravelDatabase` captures paths provided on the Unity main thread. `InitializeAsync` copies the packaged seed through a unique temporary file to `Application.persistentDataPath/travel.db` only when no writable database exists. The temporary copy must pass version, integrity, foreign-key and structural checks before it is moved into place. Existing files are validated read-only, never replaced, including corrupt or incompatible files.

`Task.Run` moves blocking database work to a worker thread. A semaphore is a one-at-a-time gate: it keeps this application's database operations from overlapping. `ExecuteAsync` opens a short-lived connection, enables foreign keys, runs the supplied SQL operation and disposes the connection. Awaiting it from a Unity UI method resumes on the UI thread. Never access GameObjects, UI or `Application` properties inside the database delegate. Use parameters for every input value; dynamic table names are restricted to code-owned constants.

An existing database is not refreshed when JSON changes. Schema migration and catalog-update behavior are intentionally deferred. Back up user data before any future migration. Schema validation is not protection against someone deliberately editing their own local database.

## Schema and relationships

| Table | Purpose / references |
|---|---|
| metadata | Catalog version and fixed demo date range |
| users | Unique normalized email, hash/salt/iterations/algorithm; no plaintext password column |
| destinations | City, country, region and description |
| airports | IATA ID, destination and Windows timezone |
| airlines | Stable airline ID and name |
| flights | Airline and origin/destination airports, UTC times, origin-local date, seats, cents |
| hotels | Destination, nightly cents, description/address/Maps reference |
| restaurants | Destination, sample meal cents, description/address/Maps reference |
| experiences | Destination, sample activity cents, description/address/Maps reference |
| hotspots | Destination, optional cents, description/address/Maps reference |
| reviews | Exactly one place, traveler label, integer stars 1–5, text, demo flag |
| trips | Owner, name, travel dates and timestamps |
| saved_items | Owner's trip and exactly one flight/place |
| price_watches | Owner and exactly one flight/hotel; last price and active flag |
| price_history | Exactly one flight/hotel, old/new cents and time |
| notifications | Owner's watch, matching price-history record, message and read flag |

All IDs are TEXT (readable catalog IDs; future user-generated IDs can be GUID strings). Prices are integer USD cents, avoiding floating-point rounding. Ratings are calculated from reviews rather than storing a second conflicting aggregate. Nullable target columns plus a CHECK enforce exactly one target; real foreign keys prevent dangling references. Composite foreign keys enforce trip/watch ownership. Triggers reject notifications about a different watched item. Deleting a user cascades to their trips, saved items, watches and notifications; catalog data remains.

The flight route/date/price index supports later search. Additional indexes cover destinations, review targets, saves, watches, unread notifications and price history. Unique partial indexes prevent duplicate saves/watches. Integrity tests do not yet constitute the Milestone 3 search performance benchmark.

## Add a destination

Follow `Assets/TravelPlanning/SeedData/README.md`: add JSON destination, airport, flights, places and reviews with unique IDs and valid references; increment catalogVersion and rebuild the seed. No city list is hard-coded in C#. The optional Python generator recreates the initial fixture and overwrites manual edits; normal content editing needs no Python.

All prices, schedules and reviews are fictional. Addresses currently identify cities, not verified street addresses. Maps URLs are search references, not verified direct Google review pages. Future screens must label these accurately; direct review links remain a catalog enrichment task.

## Common mistakes

| Symptom | Check |
|---|---|
| Missing bundled seed | Exact `StreamingAssets/Database/travel_seed.db` path; run seed menu, rebuild player |
| Red compiler error / menus absent | Console first error, file/class name and assembly references |
| Missing Inspector references | Four fields on Travel Database Page; regenerate diagnostic if absent |
| Unsupported database | Schema/application ID mismatch; preserve file and inspect, never auto-reset |
| Edited JSON has no effect in running app | Existing persistent database is intentionally preserved |
| Import rejected | Error names the malformed field/reference; fix JSON and rebuild |
| Native DLL load error | Windows x64 plugin settings from Milestone 1A; rebuild player |
| Frozen UI in future code | SQL must run through ExecuteAsync; no .Wait()/.Result on main thread |

## Team handoff

Database/schema owner maintains Runtime/Data and seed importer; content owner edits catalog JSON; auth owner integrates Kevin's login next; flight owner owns a separate flight prefab/controller; destination owner owns separate destination/review prefabs/controllers. Coordinate shared schema changes with the database owner. Integrate completed prefabs through one scene owner. Do not have multiple teammates edit the same scene. Our work stays on DuyEdit; inspect `git status` and only stage intended files with `.meta` companions. Never commit Library, Logs, Temp, Builds or personal persistent databases.

Complete C# files and scripts are reproduced in `Milestone-1B-Full-Scripts.md`. Validation evidence is recorded in `Milestone-1B-Validation.md`. Stop here before Milestone 2.
