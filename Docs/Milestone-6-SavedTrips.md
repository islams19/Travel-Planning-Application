# Milestone 6 — Saved trips

## Scope and status

M6 passed **126/126 automated tests**, a Windows x64 build and two Windows player
scenarios. See the [validation record](Milestone-6-Validation.md) for evidence
and remaining manual checks, and [full scripts](Milestone-6-Full-Scripts.md) for
the complete source snapshot. Stop after M6 before price tracking and notifications.

Signed-in users create a trip, save catalog flights and places to it, close the
app and reload their selections later. A single Saved Trips Canvas has a
browse mode and a save-picker mode. A Canvas is the surface that contains one
screen's UI. Keeping it separate lets the app preserve the flight search or
destination screen underneath.

The feature saves flights, hotels, restaurants, experiences and hotspots.
Outbound and return flights are saved independently. Users can remove one
saved item; editing or deleting an entire trip is outside this milestone.

## What a saved trip means

- A trip name is trimmed and must contain 1–60 characters.
- Start/end dropdowns use the current demo catalog, June 1–30, 2027, initially
  June 15–22. The end date cannot precede the start date.
- Trip dates are planning labels. They do not change flight dates, restrict
  saving a flight outside the range, or create hotel check-in/check-out bookings.
- Saved items reference the catalog. Prices shown later are current fictional
  USD catalog prices, not locked prices or reservations.
- Keep price units visible: flight fare, hotel room/night, restaurant
  person/meal, experience adult and hotspot visit. There is no combined total.
- Data belongs to the signed-in local account on this Windows installation.
  Logging out hides it; a different account must not see or change it.

No migration or database reset is needed. Existing accounts and saved data stay
in `Application.persistentDataPath/travel.db`; do not delete it to test this step.
Price watches and notifications remain Milestone 7.

## Existing files touched

**Original login form:** preserve Kevin's original layout. LoginPage only gains
the new isolated development smoke-test flag. The separate **SignedInCard**
gains a Saved trips button and its logout button moves to make room.

**Flight and destination UI:** existing FlightResultRow and PlaceCard prefabs
gain save buttons. PlaceCard retains View reviews. Their controllers pass stable
IDs and typed categories into the picker. Setup upgrades existing prefabs so a
project prepared in earlier milestones receives the buttons too.

## How data access works

The signed-in user ID is captured for an operation; text typed into the UI never
chooses another account. Database reads filter by owner, and writes also check
the selected trip belongs to that owner. A transaction groups related writes
so either the save and timestamp update both happen, or neither does.

Database work runs on a worker thread. `await` lets Unity keep processing
frames while that work completes. The UI then checks that its request and
account are still current before displaying a result. Closing a panel cancels
pending work, but cannot undo a save already committed; reopening shows the
actual database state. Duplicate saves produce a friendly result rather than
another copy of the item.

## Editor step 1 — Prepare

1. Open the project with **Unity 6000.3.24f1** on **DuyEdit**.
2. Stop Play mode and save unrelated scene work before using setup menus.
3. Choose **Travel Planning > Saved Trips > 1 - Prepare Saved Trips**.
   The setup also repairs underlying CanvasGroup references in an already
   prepared Saved Trips scene.
4. Open **Assets > Scenes > Login**. Use the prepared scene and its references;
   do not create another login form or reset the database.
5. Resolve red Console errors before entering Play mode. Preparation alone does
   not prove that the scene or Windows player works.

## Editor step 2 — Inspector references

The prepared scene contains this hierarchy:

```text
SavedTripsController
SavedTripsCanvas
  InputBlocker
  Main
    Header/{Title,CloseButton}
    TargetPreview
    TripSelection/{TripChoice,RetryButton}
    CreateForm
      TripName/Input
      StartDate/Dropdown
      EndDate/Dropdown
      CreateAction/CreateButton
    PlanningNote
    SaveActions/SaveSelectedButton
    Status
    TripInfo
    ScrollView/Viewport/Content
```

SavedTripsCanvas is Screen Space Overlay, sorting order 5, with a 1920 × 1080
reference resolution. Entry buttons are
`Canvas/Right Panel/SignedInCard/SavedTripsButton`,
`FlightCanvas/Main/Header/SavedTripsButton` and
`DestinationCanvas/Main/Header/SavedTripsButton`. Only the signed-in card's
button arrangement changes: its new entry is at y = -460 and logout at y = -540.

Select **SavedTripsController** and check its **Saved Trips Page** component.
The setup script assigns these fields; use this list to diagnose missing
references instead of adding duplicate manual button handlers.

| Field | Component or object |
| --- | --- |
| Account | AccountController / LoginPage |
| Flights | FlightController / FlightSearchPage |
| Hub | DestinationController / DestinationHubPage |
| Reviews | ReviewsController / ReviewsPage |
| Modal Canvas | SavedTripsCanvas |
| Underlying Groups | Three CanvasGroups for the auth, flight and destination Canvases |
| Home Button / Flight Button / Hub Button | Saved trips entry buttons on those three screens |
| Close Button | Main/Header/CloseButton |
| Create Button | Main/CreateForm/CreateAction/CreateButton |
| Save Button | Main/SaveActions/SaveSelectedButton |
| Retry Button | Main/TripSelection/RetryButton |
| Trip Name | Main/CreateForm/TripName/Input |
| Trip Choice | TMP dropdown in TripSelection |
| Start Date / End Date | TMP dropdowns in CreateForm |
| Target Preview / Status / Trip Info | The corresponding TMP labels |
| Scroll | The saved-item list ScrollRect |
| Row Prefab | Assets/TravelPlanning/Prefabs/Trips/SavedTripRow.prefab |

A CanvasGroup controls whether underlying controls accept interaction while
the modal is open. The controller restores their prior interaction states when
closing; it does not reload the underlying screens. Keep SavedTripsController
active independently of its hidden Canvas. Opening Saved trips closes reviews
first, so two overlays do not compete for input.

The row's **Saved Trip Row** component needs Title, Details and Price TMP labels
and its Remove Button. The prefab's children are `Top/{Title,RemoveButton}`,
`Price` and `Details`. Verify FlightResultRow's new Save Button points to
`SaveActions/SaveToTripButton`, and PlaceCard's Save Button points to
`ReviewActions/SaveToTripButton` beside its existing review action.

Runtime files are under `Runtime/Trips` (TripModels,
TripCatalog and TripService) and `UI/Trips` (SavedTripsPage and SavedTripRow).
`UI/Editor/TripSetup.cs` owns generation and build wiring.
TripSmokeRunner is the development-only fixture driver; ordinary users do not
need to configure or run it manually. Closing the modal clears its draft and
account-specific state; a failed load clears stale rows before retrying.

## Editor step 3 — Play mode workflow

1. Press **Play**, log in and choose **Saved trips** from the signed-in screen.
2. Create a trip named **London demo**, start **2027-06-15**, end **2027-06-22**.
   Confirm an empty trip shows a useful empty state.
3. Close Saved trips and open flight search. Save one outbound and one return
   offer into London demo, choosing each independently in the picker.
4. Try saving the same offer again. Confirm the trip contains only one copy.
5. Explore the destination and save one hotel, restaurant, experience and
   hotspot. Verify their names and units when opening Saved trips again.
6. Close Saved trips. Confirm the flight filters/results or hub city/scroll
   position are preserved. Verify View reviews still opens the correct venue.
7. Remove one saved item. Confirm the other entries remain and reopen the trip
   to check the removal persisted.
8. Test a blank name and end before start; confirm friendly validation rather
   than a SQL error or frozen UI. The input field caps names at 60 characters,
   so pasting a longer name truncates it. Service tests separately verify that
   bypassing the input field with more than 60 characters is rejected.
9. Save a flight outside the trip's date range. Its original date should remain
   visible; the trip date does not reschedule it.

## Editor step 4 — Persistence and two accounts

1. Stop Play mode, start again and log in with the same account. The trip and
   remaining items should still be present.
2. Log out and register/log in with a second local account. The first account's
   trips must not appear. Create a second trip and verify it belongs only to
   that account.
3. Return to the first account and confirm its original items remain.
4. Test rapid navigation and logout during loads. A late result must not reopen
   the picker or show the first user's information in the second session.
5. Use isolated automated fixtures to test cancellation during writes and
   ownership attacks; do not manually corrupt your personal database.
6. Resize the Game view and verify dropdowns, wrapped names, buttons and scroll
   content remain usable with physical mouse and keyboard input.

## Editor step 5 — Tests and Windows

1. Choose **Window > General > Test Runner > EditMode > Run All**. Include prior
   authentication, flight, destination and review tests in regression checks.
2. Choose **Travel Planning > Saved Trips > 2 - Build Windows x64**.
3. After a successful build, launch
   `Builds/SavedTrips/TravelPlannerTrips.exe` and repeat save/reopen/remove and
   two-account checks. A real second launch is important for persistence.
4. From the project root in PowerShell, run the isolated two-launch checks:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\tools\Test-Trips.ps1
   ```

   See the [validation record](Milestone-6-Validation.md) for exact logs and results.
5. Automated layout and state tests do not replace visual inspection, physical
   input or desktop resize checks. Record those limits with validation results.

## Common mistakes

| Symptom | What to check |
| --- | --- |
| Setup menu missing | Wait for compilation and fix red Console errors. |
| Save button missing on old cards | Run Saved Trips preparation to upgrade existing prefabs. |
| NullReferenceException | Check assigned Inspector references and regenerate prepared wiring. |
| One item appears twice | Use the service's duplicate handling and retain database unique indexes. |
| Another user's trips appear | Every query needs the captured owner ID; a foreign key does not filter reads. |
| Trip dates seem to change a flight | Show the stored flight dates; trip dates are only planning labels. |
| Price differs after reopening | Saved items read current catalog prices; they do not lock a quote. |
| Closing still leaves a saved item | A completed database commit persists; cancellation is not an undo action. |
| New buttons trigger twice | Avoid manual On Click handlers when scripts already attach listeners. |
| Markup in a name changes styling | Render user/catalog text as literal TMP text, with rich text disabled. |
| A script will not attach | Match its class/file names and resolve compilation errors first. |

## Five-person ownership

- Teammate 1: trip data models, service and ownership rules.
- Teammate 2: saved-item rows and display of prices, dates and units.
- Teammate 3: picker/browser controller and scene setup; owns shared wiring.
- Teammate 4: service tests for two users, duplicates and transaction behavior.
- Teammate 5: UI/persistence checks, Windows validation and documentation.

Coordinate Login.unity and shared prefab edits through the scene owner. Keep
assets with their `.meta` files and generated builds/logs out of commits. Make
project commits only on **DuyEdit**, as requested. Stop after M6 before starting
price tracking and notifications.
