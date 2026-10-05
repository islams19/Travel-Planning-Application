# Milestone 8 — Desktop polish and release checks

## Status and purpose

M8 implementation passed 153 automated tests, 12 development-player scenarios
and 9 actual release-player checks on 2026-09-29. All 36 rendered screenshots
were inspected. See the [validation record](Milestone-8-Validation.md) and
[complete scripts](Milestone-8-Full-Scripts.md). Physical input, browser opening
and clean-machine acceptance remain the team's manual checks below.

The app remains an offline Windows travel-planning demonstration. Polish should
make its existing screens easier to navigate and resize, preserve Kevin's login
design and test an actual non-development player. It does not add purchases,
live APIs, cloud synchronization or operating-system notifications.

## Keep the agreed environment

| Dependency | Project pin/setting |
| --- | --- |
| Unity | 6000.3.24f1, Windows x64 |
| UI/input | uGUI 2.0.0; legacy Input Manager, activeInputHandler = 0 |
| Test Framework / URP | 1.6.0 / 17.3.0 |
| sqlite-net-pcl | 1.11.285 |
| SQLitePCLRaw core/provider.e_sqlite3 | 3.0.3 / 3.0.3 |
| Native SQLite package | SourceGear.sqlite3 3.53.3, Windows x64 |

The existing Input System package is present in the manifest but is not used
for this app's controls; do not switch input modes or migrate controls during
polish. Use the checked-in SQLite bundle and retain its notices. See the
[dependency instructions](../Assets/Plugins/SQLite/README.md) for native plugin
settings. Do not install another SQLite DLL to fix a missing-reference symptom.

## Product limits to explain during the demo

- The catalog covers 12 seeded destinations with fixed June 2027 flight dates.
- Prices use fictional USD values, with per-flight, room/night, meal/person,
  adult or visit units. There are no combined totals or guaranteed quotes.
- Reviews and traveler names are fictional sample data stored locally.
- Google Maps buttons open venue search references, not verified direct Google
  review pages. The app does not import Google reviews.
- Accounts/trips persist on this Windows installation. Password recovery and
  cross-device account synchronization are not implemented.
- Price simulation is only for Editor/development builds. A release player may
  browse persisted notices but must not expose the demo-price controls.
- Existing catalog cards can show cached prices until search/hub reload;
  tracking and reopened saved trips read current local prices.

## Editor step 1 — Prepare and check the project

1. Open Unity **6000.3.24f1** and confirm the project root contains Assets,
   Packages and ProjectSettings. Keep branch **DuyEdit**.
2. Stop Play mode and save unrelated work before invoking setup. Do not delete
   `travel.db`, replace the seed, reset Git or discard earlier uncommitted work.
3. Choose **Travel Planning > Release > 1 - Prepare Final Scene**. This includes
   **Travel Planning > Polish > 1 - Prepare Desktop Polish** and earlier setup.
4. Open **Assets > Scenes > Login**. Resolve Console compile errors first.
5. Verify **Edit > Project Settings > Player > Other Settings > Active Input
   Handling** remains **Input Manager (Old)**. Check the single EventSystem uses
   StandaloneInputModule. Do not add a second EventSystem to a modal.
6. Check the native SQLite plugin and retain asset `.meta` files. A missing
   Inspector reference should be repaired through the prepared setup/wiring,
   not by replacing existing scene objects with a fresh design.

The six Canvases (Canvas, FlightCanvas, DestinationCanvas, ReviewsCanvas,
SavedTripsCanvas and PriceTrackingCanvas) each gain **Desktop Canvas Fit**.
It uses Canvas Scaler **Scale With Screen Size > Expand** while retaining the
reference resolution and original RectTransforms. Expand keeps the reference
layout available as aspect ratio changes; it does not guarantee large physical
text in a very small window, so inspect readability at each target size.

Select **DesktopNavigationController**. Its **Desktop Keyboard Navigation**
component has Account = AccountController/LoginPage and six Screens bindings:

| Canvas | Back Button path under Canvas | Default Focus path under Canvas |
| --- | --- | --- |
| Canvas | Right Panel/RegistrationCard/Create an account | Right Panel/LoginCard/EmailInput |
| FlightCanvas | Main/Header/BackButton | Main/RouteFields/Origin/Dropdown |
| DestinationCanvas | Main/Header/BackButton | Main/DestinationSelection/Dropdown |
| ReviewsCanvas | Main/Header/CloseButton | Main/Header/CloseButton |
| SavedTripsCanvas | Main/Header/CloseButton | Main/TripSelection/TripChoice |
| PriceTrackingCanvas | Main/Header/CloseButton | Main/WatchSelection/Dropdown |

**ReleaseQaController** contains ReleaseSmokeRunner. Setup assigns Account,
Flights, Hub, Tracking, Reviews, Trips and the Auth/Flight/Hub/Tracking Canvas
references. It stays dormant without its explicit QA arguments. Do not manually
assign fixture accounts or personal database filenames in the saved scene.

## Editor step 2 — Window sizes and readable content

The target matrix is **1000 × 700**, **1280 × 720**, **1920 × 1080** and
**1920 × 600**. The captured states fit all four actual player sizes; physical
drag-resizing, display scaling and comfortable reading still require manual review.

1. In the Game view, choose each size/aspect; in the Windows player, test real
   window resizing as well. Check the measured dimensions rather than assuming
   the requested window size was applied.
2. Visit login, registration, home, flights, destinations, reviews, trips and
   tracking. Ensure every required control remains reachable.
3. Check long venue/trip names, full review bodies, notification text and price
   units. Text must wrap or remain scrollable without covering nearby controls.
4. Confirm lists scroll to their last item and that dropdown menus stay usable.
5. Check loading, no-result/empty and error/retry messages. A spinner/status must
   not hide Close/Back or leave controls permanently disabled after a failure.
6. Compare the original login design with its baseline; do not move its 22
   preserved RectTransforms as a shortcut to fixing new screens.

## Editor step 3 — Keyboard, mouse and modal checks

DesktopKeyboardNavigation uses the legacy Input Manager and the highest active
Canvas. **Tab** moves forward in visual order and **Shift-Tab** moves backward,
skipping hidden/disabled controls. It scrolls focused list actions into view.
**Escape** closes an expanded dropdown first; otherwise it invokes the screen's
Back/Close action. Registration can return to login; login/home are not dismissed.
Focus is remembered when returning to a previous screen. Login's old Tab handler
defers to this helper to avoid moving twice; Enter submission remains available.

The acceptance checklist is:

- Tab/Shift-Tab reach appropriate visible, enabled controls in a sensible order.
- Editing text does not accidentally activate a different form or modal.
- Dropdown interaction takes precedence over closing an entire overlay.
- Closing an overlay leaves a useful focus target and restores underlying
  controls, including after opening Reviews, Saved trips and Tracking in turn.
- Buttons respond once per activation; no duplicate manual On Click handlers.
- Logout during a load cannot show another account's rows or stale badge.

Run these with a physical keyboard/mouse. Automated callbacks and screenshots
provide different evidence and do not replace this check. A real Google Maps
click should also be checked manually in the default browser.

## Editor step 4 — Release and fixture isolation

A development build contains diagnostic behavior. A non-development release
must be tested separately; simply renaming an executable does not change it.
Choose **Travel Planning > Release > 2 - Build Windows Release** for
`Builds/TravelPlannerRelease/TravelPlanner.exe`. For the current development
regression artifact, choose **Travel Planning > Release > 3 - Build Windows
Development QA**, producing
`Builds/TravelPlannerDevelopment/TravelPlannerDevelopment.exe`.
Each build includes ThirdPartyNotices and `release-validation.json` describing
its company/product identity and development flag. Distribute the complete
player folder, not the `.exe` alone.

Release QA diagnostic code remains present in the binary but is inactive in
ordinary launches. It is not claimed to be excluded from the release executable.

Release QA uses the explicit `-releaseSmoke` flag with a strict
`-authFile auth-smoke-<GUID>.db` fixture. The runner must reject missing or
malformed fixture names. Use the supplied harness once available instead of
typing a filename that could point at real user data:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Test-Release.ps1
powershell -ExecutionPolicy Bypass -File .\tools\Test-Release.ps1 -Screenshots
```

The first command runs rejection checks plus first-launch/reopen workflows.
The screenshot option renders the first workflow with graphics enabled and
expects 36 PNGs (eight screens, including signed-in home, plus a login-error
state at four sizes), under the run's `Logs/Release`
folder. Actual screenshots must still be inspected; file existence alone is not
visual approval. The final M8 runs passed and their 36 screenshots were inspected;
see the validation record for exact artifacts and remaining manual checks.

Existing development-only
smoke flags are not a substitute: release must continue rejecting them.

Verify the fixture path and that normal `Application.persistentDataPath/travel.db`
is untouched. Test creation/login, save/reopen/remove, ownership and notices
across actual launches. Verify demo-price controls are absent and their action
is blocked in release. Keep seed/catalog hashes unchanged during isolated QA.
Do not claim release isolation based solely on reading source guards.

## Regression commands already available

The M8 current-build harness runs all six feature checks against one current
development executable, twice per feature (12 processes total), instead of
requiring six separate historical builds:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Test-Mvp.ps1
```

It defaults to `Builds/TravelPlannerDevelopment/TravelPlannerDevelopment.exe`.
Its `release-validation.json` must identify a development build and the actual
company/product name. The harness creates one unique fixture per feature,
collects logs/timings under `Logs/Mvp`, and verifies normal `travel.db` plus
journal/WAL/SHM sidecars have unchanged presence and SHA-256 hashes before and
after execution. The script has passed syntax parsing; runtime results remain
pending. Do not run another app process writing personal data during this guard.

For the full current suite, choose **Window > General > Test Runner > EditMode >
Run All**. Earlier development harnesses below remain historical regression
tools and require their corresponding milestone build output. Do not run them
against a release executable or rebuild old scenes casually during final QA.

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Test-Authentication.ps1
powershell -ExecutionPolicy Bypass -File .\tools\Test-FlightSearch.ps1
powershell -ExecutionPolicy Bypass -File .\tools\Test-DestinationHub.ps1
powershell -ExecutionPolicy Bypass -File .\tools\Test-Reviews.ps1
powershell -ExecutionPolicy Bypass -File .\tools\Test-Trips.ps1
powershell -ExecutionPolicy Bypass -File .\tools\Test-Tracking.ps1
```

Each earlier milestone's validation document records the build, logs and
limitations for that checkpoint. Their passing history does not establish M8
release or visual results. [Final M8 evidence](Milestone-8-Validation.md) records
the current builds, consolidated regression run and rendered review separately.

## Five-person manual QA split

| Teammate | Primary check |
| --- | --- |
| 1 | Login/register/logout, keyboard flow, original design preservation. |
| 2 | Flight search/filter/reset, all size targets, empty results and units. |
| 3 | Destination/review scrolling, long text, real Google Maps browser action. |
| 4 | Trips, two-account ownership, reopen persistence and modal focus recovery. |
| 5 | Tracking/read states, release demo guard, clean-machine build packaging and logs. |

Record build/version, actual resolution, action, expected/actual result and a
screenshot for each failure. Fix one cause and rerun the affected case plus
appropriate regressions; do not mark a manual case passed from a callback test.

## Git handoff on DuyEdit

1. Run `git status --short` and `git branch --show-current` before staging.
   Preserve unrelated pending work; stop if the branch is not DuyEdit.
2. Coordinate scene and shared-prefab edits through one teammate. Keep `.meta`
   files alongside added/moved assets so GUID references remain stable.
3. Stage only reviewed task files by path. Inspect `git diff --cached` and run
   `git diff --cached --check`; do not use a hard reset or blanket cleanup.
4. Keep Library, Temp, Builds, Logs and UserSettings out of commits using the
   existing Unity `.gitignore`. Commit the intentional seed database and its
   metadata, not personal runtime databases or fixture data.
5. When the team is ready to commit, commit on **DuyEdit**. Coordinate who pushes
   so five people do not overwrite each other's scene changes. Do not pull/rebase
   over unreviewed local work; first preserve and review it with the team.

No commit or push is performed by this documentation step.

The M8 `.gitignore` cleanup specifically excludes `travel.db`,
`auth-smoke-*.db` and their `-journal`, `-wal` and `-shm` sidecars. A
`git check-ignore --no-index` check confirmed all eight representative paths
are ignored. `Assets/StreamingAssets/Database/travel_seed.db` is not ignored
and remains available to add with the pending project assets (it is currently
untracked). These patterns protect accidental local copies; they do
not delete existing files or untrack a file that was already committed.
