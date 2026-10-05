# Milestone 4 — Destination hub

## Scope and status

This guide covers the destination hub implementation and its manual test steps.
See the [validation record](Milestone-4-Validation.md) for automated test and
Windows player results, and the [full scripts](Milestone-4-Full-Scripts.md) for
the source snapshot. Manual visual-check limitations are recorded separately.

Signed-in users explore a city and browse hotels, restaurants, experiences and
hotspots from the existing local SQLite catalog. The hub has its own Canvas in
`Assets/Scenes/Login.unity`. A Canvas is the surface containing one screen's UI;
hiding it does not destroy the separate account controller or end the session.

The current fixture has 12 cities and three places in each category per city.
The dropdown reads database destinations, so adding catalog data does not require
changing the list in C#. Opening from flight search selects its destination city.
Back returns to flight search with its prior selections and results preserved.
Logout closes the hub and flight screen and returns to the login form.

## Existing files and visual boundaries

**Touches existing integration:** FlightSearchPage gains the destination entry
point and suspend/resume behavior; AirportOption includes its destination ID.
LoginPage extends its development smoke-test isolation guard to `-destinationSmoke`.
Its existing initialized Database and login/logout events provide the session.
Kevin's original login form positions, anchors, fonts and colors remain the visual
baseline. The password policy remains 10–128 characters.

New hub scripts belong under `UI/Destinations`, its setup helper under
`UI/Editor`, and its independent place-card prefab under `Prefabs/Destinations`.
`DestinationHubPage` coordinates loading/navigation, `PlaceSection` owns one
category's card list, and `PlaceCard` fills the visible labels for one place.
Cards use the existing LiberationSans font, white surfaces and #3157FF accent.
Detailed review browsing, external review links, saved trips, price watches and
purchases are later milestones; an aggregate rating preview is included here.

## What the numbers mean

- Prices are **fictional USD demo budgets**, read from SQLite, not current quotes.
- Hotel labels use **/ room / night**, restaurants **/ person (meal)**,
  experiences **/ adult**, and priced hotspots **/ visit**. Read the card's unit
  before comparing amounts; seed hotspots are free public-area visits.
- Zero can display as **Free**; a missing price displays **Price unavailable**.
- Rating/count previews summarize stored demonstration reviews. They are not
  ratings downloaded from Google. Zero reviews displays **No reviews yet**.
- Addresses are city-level references; they are not verified street addresses.
- The database's Maps URLs are search references. M4 does not open them; external
  link actions and review details wait for M5.

## How loading stays responsive

The controller reads the selected destination ID and asks the service for data.
The service uses `TravelDatabase.ExecuteAsync`: it performs database work on a
worker thread, allowing Unity's main thread to keep drawing and receiving clicks.
`await` means “continue when the data is ready” without freezing that main thread.
Only after awaiting does the controller update labels and create card objects.

Queries pass IDs as parameters, keeping user values separate from SQL commands.
Existing destination/review indexes help SQLite find the relevant rows. Category
table names come from a fixed internal list. The hub only reads data: it does not
rebuild the seed, migrate the schema or reset the user's database.

Changing city, pressing Back or logging out cancels or invalidates earlier loads.
A late result must not reopen a screen or show the previous user's content.

## Editor step 1 — Prepare the hub

1. Open the project in **Unity 6000.3.24f1** and wait for compilation to finish.
2. Stop Play mode before changing or preparing scene objects.
3. Choose **Travel Planning > Destination Hub > 1 - Prepare Destination Hub**.
4. The helper opens the integrated Login scene and adds/wires the hub. Save any
   work when Unity asks; do not create a second copy of the original login form.
5. In Hierarchy, confirm the existing AccountController and FlightController
   remain outside hidden screen Canvases. Locate **DestinationController** and
   **DestinationCanvas**. The hub starts hidden until navigation opens it.
6. Keep one EventSystem with **Standalone Input Module**. Under **Edit > Project
   Settings > Player > Other Settings**, Active Input Handling stays **Input
   Manager (Old)** or **Both**.
7. Do not add Inspector On Click listeners to buttons already wired by the
   controller; that can run the same action twice.

## Editor step 2 — Check Inspector wiring

1. Select **DestinationController**. Its **Account** reference must point to
   AccountController's LoginPage, and its flight reference to FlightController.
2. In its DestinationHubPage component, verify **Flights**, **Hub Canvas**,
   **Destination**, **Description**, **Status**, **Open Button**, **Back Button**,
   **Logout Button**, **Retry Button**, **Scroll**, **Hotels**, **Restaurants**,
   **Experiences**, **Hotspots**, and **Card Prefab** are assigned. The four
   category fields refer to PlaceSection components, not bare RectTransforms.
   A field showing **None** is a missing reference, not an optional blank.
3. Expand DestinationCanvas. Verify the main Scroll View has a Viewport with a
   mask and Content assigned to its ScrollRect. The four category sections must
   be children of that Content, so they move together when scrolling.
4. Let the vertical layout control card heights/spacing. Do not manually drag
   card RectTransforms that a Layout Group controls; the next layout pass will
   replace those positions.
5. Use the setup helper to create the initial wiring rather than guessing object
   names or copying another controller's references.

The entry button is `FlightCanvas/Main/Header/ExploreDestinationButton`.
Hub controls are under `DestinationCanvas/Main`: `Header/BackButton`,
`Header/LogoutButton`, `DestinationSelection/Dropdown`, and
`DestinationSelection/RetryButton`. Its `ScrollView/Viewport/Content` contains
`Hotels`, `Restaurants`, `Experiences`, and `Hotspots`, each with `Heading`,
`EmptyMessage`, and `Items`. **Card Prefab** points to
`Assets/TravelPlanning/Prefabs/Destinations/PlaceCard.prefab`.

## Editor step 3 — Test in Play mode

1. Enter Play mode and sign in with a local test account.
2. Open flight search. Use JFK → LHR, June 15/22, 2027, and run Search.
3. Change an airline/price/sort filter and note the selected values and results.
4. Choose **Explore destination**. The hub should select London from the flight
   destination, with three cards in each of the four categories in this fixture.
5. Select Tokyo, then Sydney. Confirm heading/cards update to that city and old
   cards disappear rather than accumulating.
6. Check price units, the fictional-data label, rating/count previews, city-level
   address wording, and a Free hotspot. Missing-price/no-review states are covered
   by test fixtures; do not edit your normal database just to force those cases.
7. Click Back. Flight fields and completed results should remain as you left them.
   Reopen the hub and confirm navigation still works.
   If loading fails, use **Retry**; it should try again without requiring logout.
8. Switch city quickly, then press Back or Log out while loading. No late cards
   should appear after leaving, and logout must restore the login form.
9. Sign in again; ensure you can load the hub afresh without another user's state.
10. Resize the Game view and the built desktop window. Check headings, labels,
    card heights, four sections and scrolling for clipping or overlap. Test mouse
    wheel, buttons and dropdown selection manually as well as automated checks.

## Editor step 4 — Build and check Windows

1. Stop Play mode.
2. Choose **Travel Planning > Destination Hub > 2 - Build Windows x64**.
3. After a successful build, run
   `Builds/DestinationHub/TravelPlannerDestinations.exe` with its adjacent Data and
   runtime files; copying the executable alone is insufficient.
4. Repeat the Play-mode navigation checks in the Windows player, then close and
   reopen it to confirm the existing account still works.
5. From PowerShell in the project root, run
   `powershell -ExecutionPolicy Bypass -File .\tools\Test-DestinationHub.ps1`
   to repeat the isolated player scenarios. See the
   [validation report](Milestone-4-Validation.md) for evidence.
   Automated fixture databases must stay separate from
   normal `travel.db`; do not delete that file as a troubleshooting shortcut.

To reproduce the Unity regression suite, stop Play mode and choose **Window >
General > Test Runner > EditMode > Run All**. The scene-flow tests enter and exit
Play mode themselves. Wait for the complete result before editing the scene.

## Common mistakes and recognition

| Symptom | Likely cause and first check |
|---|---|
| Red compiler errors after adding a script | File/class name mismatch, duplicate class, or missing assembly reference; read the first Console error |
| NullReferenceException or missing-reference message | Controller Inspector field is None; inspect the named field and rerun setup only if instructed |
| Hub buttons do nothing | Missing EventSystem/GraphicRaycaster, wrong input module, or an overlay intercepting clicks |
| Nothing scrolls or lower sections disappear | ScrollRect Content/Viewport points to the wrong RectTransform, or Content height does not follow layout |
| Old city's cards appear after a new choice | Previous request completed late; check cancellation/revision guards rather than adding a delay |
| Back resets flight filters | Navigation used flight Open/Close instead of its dedicated suspend/resume path |
| Missing price looks like Free | Nullable price was converted to zero; preserve the difference in model and label |
| Ratings look like Google data | Missing demo label; averages must be labeled as stored sample-review summaries |
| New seed edits do not appear in an existing installation | First launch copies the seed only once; this is deliberate user-data preservation |

## Parallel team ownership

Assign one owner to each area so five people can contribute without editing the
same scene or script:

1. Destination service/models and database queries.
2. DestinationHubPage navigation, loading and cancellation.
3. PlaceCard prefab, typography and layout.
4. JSON catalog content and data-quality checks.
5. Editor setup, scene integration and tests/documentation.

Only the integrator should save the shared Login scene. Other teammates work in
their owned scripts/prefabs. Keep `.meta` files with their assets and review the
diff before committing. This session remains on **DuyEdit**; do not discard the
pending work from earlier milestones to make the tree clean.
