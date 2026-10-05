# Milestone 3 — Flight search

## What this adds

Signed-in users can search seeded flight offers by origin, destination, departure date and return date. Outbound and return offers appear separately. Filters cover maximum price per flight, airline and local departure time; sorting covers price, departure time and airline name. No live API, ticket purchase, seat reservation or combined itinerary is created.

The fixed demonstration calendar covers June 1–30, 2027. Defaults are JFK → LHR, June 15 outbound and June 22 return. Other seeded dates can be selected from the dropdowns. All prices are fictional USD values from the database. There are two offers per seeded route/day. A valid route without seed data returns an empty result, not an error or fabricated price.

## Existing login integration

Kevin's original login geometry remains the source of truth. The signed-in card gains **Search flights**. The new flight screen has its own Canvas/controller and uses the existing LiberationSans font, white background and blue accent. **Back** returns to the signed-in card; **Log out** returns to the login form. The account controller remains alive outside hidden panels, so opening search does not lose the session.

`LoginPage.Database` exposes the already initialized database to the flight screen. The public logout operation and `LoggedOut` event let the flight controller cancel and close when a session ends. The password requirement remains **10–128 characters**. No saved database is reset and no schema migration is needed.

## Seeded routes

| Airport | Seeded direct connections (both directions) |
|---|---|
| JFK — New York | LHR London, CDG Paris, YVR Vancouver, CUN Cancun, GIG Rio |
| LHR — London | FCO Rome, CPT Cape Town, DXB Dubai, plus JFK |
| DXB — Dubai | SIN Singapore, plus LHR |
| HND — Tokyo | SIN Singapore, SYD Sydney |
| SIN — Singapore | SYD Sydney, plus DXB/HND |

This is 12 reciprocal pairs / 24 directed routes. Selecting another valid pair intentionally demonstrates the empty-results state.

## How the query works

A request contains only selected IDs, dates and filter values. The service validates the IDs and date range, then queries through `TravelDatabase.ExecuteAsync`. The existing one-at-a-time database gate opens and disposes each connection on a worker thread. SQL values are parameters, never concatenated user input.

Required origin/destination/local-date comparisons use the existing `flights_search` index. An index is a database lookup structure that avoids reading unrelated flight rows. Optional price/airline filters narrow those rows. Sorting and departure-time filtering happen on the worker before returning plain result data. Unity creates result cards on the main thread only after awaiting the results.

UTC timestamps are converted using each airport's Windows time zone. The displayed departure is local to the origin airport; arrival is local to the arrival airport. The time filter uses each leg's origin-local departure time, not UTC. Overnight arrival dates remain visible. Prices are stored as integer cents and displayed as USD.

Outbound and return lists are independent offers. The screen does not pair selections, guarantee a workable connection between particular offers, calculate a total trip price, or purchase anything.

## Editor step 1 — Prepare the screen

1. Open the project using Unity **6000.3.24f1** and wait for scripts to compile.
2. Choose **Travel Planning > Flight Search > 1 - Prepare Flight Search**.
3. The command opens the integrated `Assets/Scenes/Login.unity`, preserves the prepared auth UI, and adds the flight screen.
4. In Hierarchy, locate **AccountController**, **FlightController**, and **FlightCanvas**. FlightCanvas starts inactive.
5. Keep the single **EventSystem** with **Standalone Input Module**. In **Edit > Project Settings > Player > Other Settings**, Active Input Handling remains **Input Manager (Old)** or **Both**.
6. Do not add duplicate Inspector On Click events; the controllers own their button listeners.

## Editor step 2 — Test the user flow

1. Enter Play mode; register a test account if needed, then sign in.
2. Click **Search flights** on the signed-in card.
3. Use origin JFK, destination LHR, depart June 15, return June 22. Run Search.
4. Expect two outbound offers and two return offers before filters. For June 15, JFK → LHR includes BA100 at USD 527 and AA101 at USD 562 in the initial fixture.
5. Change sort order and confirm the displayed ordering changes.
6. Limit the maximum price or choose an airline. Confirm the list/count narrows. Clear filters to restore the unfiltered result.
7. Use a local departure-time filter. For JFK → LHR June 15, the sample departures are 08:00 and 17:00 New York time, not their UTC values.
8. Search JFK → HND to demonstrate a valid route with no seeded offers.
9. Choose identical origin/destination or a return before departure: expect a friendly validation message.
10. Click Back and reopen search; then Log out. Search results should not reappear after logout or a cancelled request.
11. Resize the window and inspect dropdowns, labels and scrolling manually. Headless tests cannot establish rendered appearance or mouse hit-testing.

## Editor step 3 — Windows build

1. Stop Play mode.
2. Choose **Travel Planning > Flight Search > 2 - Build Windows x64**.
3. Run `Builds/FlightSearch/TravelPlannerFlights.exe` with its adjacent Data/runtime files.
4. Repeat login, flight search, filtering, Back and Logout in the player.
5. To repeat the automated player checks from PowerShell in the project root:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Test-FlightSearch.ps1
```

The harness uses a separate development database and artificial account. It must never use normal `travel.db`. Logs include search timing against the 2–3-second target; registration's intentionally slow password hash is not part of flight-search timing.

## Common mistakes

| Symptom | Check |
|---|---|
| Search button unavailable | Wait for options/database initialization and ensure the user is signed in |
| Empty list | Route exists in the seed table above; filter may exclude both offers |
| Price rejected | Use a nonnegative USD number with at most two decimal places |
| Unexpected time filter result | Times are local to the origin airport of each leg |
| Buttons ignore clicks | Single EventSystem, legacy module, Canvas raycaster and assigned references |
| Results not scrolling | ScrollRect Content/Viewport references and layout/fitter on Content |
| Duplicate results on repeat search | Controller must clear the old result cards before binding new data |
| NullReferenceException | Assign every required FlightSearchPage/row prefab field; check the first Console error |
| UI freezes | Do not run SQL, timezone conversion loops, or task .Wait()/.Result on the Unity main thread |
| Password rejected | Current auth rule is 10–128 characters, exact confirmation and no whitespace-only password |

## Team ownership

The flight teammate owns the flight runtime/UI scripts and result prefab. The database owner reviews query/index changes. The integration owner maintains scene wiring; teammates should not simultaneously edit Login.unity. Destination/review screens can be developed as separate prefabs next. Keep all local user databases, Logs and Builds out of Git; this work remains on DuyEdit.

Complete scripts and test/build evidence are delivered in the adjacent Milestone 3 documents. Stop before destination-hub implementation.

## Inspector map and script ownership

Preparation assigns these references automatically. If a teammate removes one, select **FlightController > Flight Search Page** and use this map. Paths under Main begin at `FlightCanvas/Main`.

| Inspector field | Target |
|---|---|
| Account | AccountController / LoginPage |
| Auth Canvas / Flight Canvas | Canvas / FlightCanvas GameObjects |
| Origin / Destination | Main/RouteFields/Origin/Dropdown and Destination/Dropdown |
| Departure Date / Return Date | Main/RouteFields/DepartureDate/Dropdown and ReturnDate/Dropdown |
| Maximum Price | Main/FilterFields/MaximumPrice/Input (TMP_InputField) |
| Airline / Time Band / Sort | Main/FilterFields/Airline/Dropdown, TimeBand/Dropdown, Sort/Dropdown |
| Status | Main/Status (TMP_Text) |
| Open Button | Canvas/Right Panel/SignedInCard/SearchFlightsButton |
| Search Button / Clear Button | Main/Actions/SearchButton and ClearFiltersButton |
| Back Button / Logout Button | Main/Header/BackButton and LogoutButton |
| Outbound Heading / Return Heading | Main/Results/Outbound/Heading and Return/Heading |
| Outbound Content / Return Content | Each Results column's ScrollView/Viewport/Content |
| Outbound Scroll / Return Scroll | Each Results column's ScrollView (ScrollRect) |
| Row Prefab | Assets/TravelPlanning/Prefabs/Flights/FlightResultRow.prefab |

The row prefab's **Flight Result Row** component connects Airline Text to `Top/Airline`, Price Text to `Top/Price`, and Route Text to `Route`. Its layout groups handle row spacing. Keep ScrollRect Content and Viewport assigned, and retain the viewport mask.

| Source file | Responsibility |
|---|---|
| Runtime/Flights/FlightModels.cs | Plain search options, request, result and flight data |
| Runtime/Flights/FlightSearchQuery.cs | Parameterized SQL and airport-local time conversion |
| Runtime/Flights/FlightSearchService.cs | Validation, worker queries, filtering and sorting |
| UI/Flights/FlightSearchPage.cs | Form state, cancellation and result cards |
| UI/Flights/FlightResultRow.cs | Format one flight's airline, local times and USD price |
| UI/Flights/FlightSmokeRunner.cs | Development-only automated UI verification |
| UI/Editor/FlightSearchSetup.cs | Build/wire the screen and Windows player |
| Tests/EditMode/FlightSearchTests.cs | Database behavior and query-plan tests |
| Tests/EditMode/FlightSearchSceneTests.cs | Scene and interaction validation |

Source paths in this table begin at `Assets/TravelPlanning/`. `FlightSearchService` receives the existing initialized `TravelDatabase`; it exposes `LoadOptionsAsync`, `SearchAsync`, and a developer-only query-plan diagnostic. No new database package is required.
