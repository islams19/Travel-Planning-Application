# Milestone 7 — Validation record

## Confirmed preparation and targeted tests

Validation uses Unity **6000.3.24f1** on **DuyEdit**.

| Check | Result | Evidence |
| --- | --- | --- |
| Final scene preparation | PASS, exit 0; `PRICE_TRACKING_SCENE_READY`, no exception | `Logs/tracking-prepare-final.log` |
| Targeted tracking tests | **11 passed / 11 total; 0 failures** | `Logs/tracking-targeted-tests.xml`, `Logs/tracking-targeted-tests.log` |

The first preparation exposed a Unity lifecycle naming issue: AddComponent
invoked NotificationBadge.Reset before its Inspector labels were assigned.
Renaming the logout handler to ClearOnLogout removed that unintended lifecycle
callback. The clean preparation above was run after the fix.

The targeted run measured database work at **2 ms**, on worker thread **65**
versus main thread **1**. This confirms background execution for the measured
operation; it is not a performance guarantee.

## Full regression suite

The full regression suite passed **137/137 tests with 0 failures**. Evidence:
`Logs/tracking-tests.xml` and `Logs/tracking-tests.log`.

| Workflow | UI elapsed | Database elapsed | Worker / main thread |
| --- | --- | --- | --- |
| Price tracking | Not reported here | 2 ms | 60 / 1 |
| Saved trips regression | Not reported here | 2 ms | Worker 11; main ID not reported here |
| Flight regression | 23 ms | 7 ms | Not reported here |
| Destination regression | 27 ms | 3 ms | Not reported here |
| Reviews regression | 10 ms | 3 ms | Not reported here |

These are observations from this machine and fixture, not performance guarantees.

## Windows and release-build checks

The Windows x64 development build passed with exit 0 and
`PRICE_TRACKING_BUILD_PASS` in `Logs/tracking-build.log`. Executable:
`Builds/PriceTracking/TravelPlannerTracking.exe`.

`tools/Test-Tracking.ps1` exited 0. Both actual Windows player scenarios passed:

| Scenario | Database elapsed | Worker / main thread | Log |
| --- | --- | --- | --- |
| Register and track | 1 ms | 13 / 1 | `Logs/PriceTracking/5cbfefc44d294ea2b4ff3e2e58e4aa2c/register-track.log` |
| Reopen and reload tracking | 2 ms | 11 / 1 | `Logs/PriceTracking/5cbfefc44d294ea2b4ff3e2e58e4aa2c/reopen-track.log` |

These players run headlessly through UI callbacks. Reproduce after building
from the project root in PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Test-Tracking.ps1
```

The simulator is intended only for the Editor and development builds. An actual
non-development release build has not been tested here; do not treat source
guards alone as release-player validation.

## Manual limitations

Automated callback and layout checks do not establish correct rendered
appearance, physical mouse/keyboard behavior or desktop window resizing.
Follow the [Price tracking guide](Milestone-7-PriceTracking.md) for manual checks,
including modal interaction restoration, badge readability and current-price
labels after reopening/reloading catalog views.

This milestone has no operating-system alerts or live price API. The simulator
changes the shared fictional catalog only through the central price-change
transaction. Arbitrary external SQL edits are not automatically detected.

## Working boundaries

Work remains uncommitted on **DuyEdit**, preserving earlier pending changes.
No commit, push, personal database reset or M8 work was performed.

Final checks confirmed:

- All 22 original Kevin login RectTransforms retain their anchors, position,
  size and pivot after building, compared with reference commit
  `0ae085f9889591310dc46e6e14b7bd507c8c0ab3`.
- The saved `developmentDatabaseFile` is blank; legacy input remains 0.
- Every new asset has its `.meta` file.
- Seed database and catalog hashes are unchanged from the M6 baseline.
- Normal persistent `travel.db` was absent before and after validation; isolated
  fixtures did not create or reset personal data.
- All 27 files under ProjectSettings and Packages retain their baseline hashes.
- The remaining `git diff --check` finding is the pre-existing trailing whitespace
  in `ProjectSettings/ProjectSettings.asset` at line 537, left unchanged.

Stop after M7 before beginning M8 polish and testing.
