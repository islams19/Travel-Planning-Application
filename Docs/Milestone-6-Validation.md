# Milestone 6 — Validation record

## Confirmed preparation and tests

Validation used Unity **6000.3.24f1** on branch **DuyEdit**.

| Check | Result | Evidence |
| --- | --- | --- |
| Repair preparation | PASS, exit 0 | `Logs/trips-prepare-repair.log` |
| Final full EditMode suite | **126 passed / 126 total; 0 failures** | `Logs/trips-tests-final.xml`, `Logs/trips-tests-final.log` |
| Original login layout | All 22 original RectTransforms unchanged after first preparation | Anchors, anchored position, size and pivot compared with Kevin reference commit `0ae085f9889591310dc46e6e14b7bd507c8c0ab3` |
| Saved development database override | Empty | Saved scene's `developmentDatabaseFile` field |

The first suite passed 124/126 tests and exposed missing underlying CanvasGroup
references. TripSetup was corrected to create and assign the native components
using Unity's null handling, including repair of an already-prepared scene.
Preparation was rerun, followed by the final full suite above. The initial
result is not the final validation result.

## Observed Editor timings

| Workflow | UI elapsed | Database elapsed | Worker / main thread |
| --- | --- | --- | --- |
| Saved trips | Not reported here | 2 ms | 52 / 1 |
| Flight regression | 23 ms | 8 ms | Not reported here |
| Destination regression | 26 ms | 4 ms | Not reported here |
| Review regression | 9 ms | 3 ms | Not reported here |

These fixture measurements are not a performance guarantee. The saved-trip
worker ID differs from the main thread ID, demonstrating background database
execution for the measured operation.

## Windows build and player

The Windows x64 build passed with exit 0 and `SAVED_TRIPS_BUILD_PASS` in
`Logs/trips-build.log`. The executable is
`Builds/SavedTrips/TravelPlannerTrips.exe`.

`tools/Test-Trips.ps1` exited 0; both actual Windows player scenarios passed:

| Scenario | Database elapsed | Worker / main thread | Log |
| --- | --- | --- | --- |
| Register and save | 1 ms | 12 / 1 | `Logs/SavedTrips/1000d88b2e15459e9ca669ab537e9e2b/register-save.log` |
| Reopen and reload saved data | 1 ms | 9 / 1 | `Logs/SavedTrips/1000d88b2e15459e9ca669ab537e9e2b/reopen-save.log` |

The players run headlessly and exercise UI callbacks. They do not establish
physical-input or rendered-appearance behavior. To reproduce after building,
run from the project root in PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Test-Trips.ps1
```

## Manual limitations

Automated state/layout checks do not establish correct rendered appearance,
physical mouse or keyboard behavior, or desktop window resizing. Follow the
[Saved trips guide](Milestone-6-SavedTrips.md) for manual save/reopen/remove,
two-account and layout checks. Preserve the distinction between trip planning
dates and actual flight dates, and check current-price unit labels visually.

No browser action is required for this milestone. The earlier Reviews feature
still needs its separately documented real-browser and visual checks.

## Working-tree boundaries

Existing work remains uncommitted on **DuyEdit**. No commit, push or personal
database reset was performed for this validation. The original login layout
comparison concerns Kevin's form; the separate SignedInCard intentionally gains
a Saved trips entry and moves its logout button. Stop after this milestone
before implementing Milestone 7 price tracking and notifications.

Seed database and catalog SHA hashes were unchanged before and after testing.
The normal persistent `travel.db` was absent both before and after the checks;
isolated fixtures did not create a personal runtime database. The only remaining
`git diff --check` finding is pre-existing trailing whitespace in
`ProjectSettings/ProjectSettings.asset` at line 537, left unchanged.
