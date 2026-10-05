# Milestone 4 — Validation record

## Current status

**Unity preparation, all 69 automated tests, the Windows x64 build, and both
actual-player scenarios passed.** Milestone 4 is ready with the manual visual
validation limitations below.

## Confirmed Unity results

- Scene preparation passed: `Logs/destination-prepare.log`.
- Full regression suite: **69/69 passed, zero failures**:
  `Logs/destination-tests.xml` and `Logs/destination-tests.log`.
- Editor destination load: **23 ms** through UI loading/binding, including
  **4 ms** database/service work; worker thread 16, main thread 1.
- Flight-search regression measurement: **21 ms** through UI search/binding,
  including **7 ms** database/service work; worker thread 11, main thread 1.
- After scene preparation, all **22 original Kevin-login RectTransforms** retained
  their anchors, size, position and pivot when compared with source commit
  `0ae085f9889591310dc46e6e14b7bd507c8c0ab3`.

Timings describe this computer and the seeded demonstration workload. They are
not a guarantee for arbitrary larger catalogs or other hardware. The fixture's
prices/ratings/reviews remain fictional, addresses are city-level references,
and no external review-link actions are included in this milestone.

## Windows build and player

- Build: **passed**, exit code 0; log `Logs/destination-build.log` contains
  `DESTINATION_HUB_BUILD_PASS`.
- `tools/Test-DestinationHub.ps1`: **passed**, exit code 0; both actual-player
  runs emitted `DESTINATION_UI_SMOKE_PASS`.
- First register/explore run: **19 ms** UI load, **3 ms** database/service work;
  worker thread 9, main thread 1.
- Reopen/explore run: **20 ms** UI load, **3 ms** database/service work;
  worker thread 11, main thread 1.
- Logs: `Logs/DestinationHub/7e5884892cbb43649fbffdfc04afe580/`, files
  `register-explore.log` and `reopen-explore.log`.
- Player scenarios cover registration/persisted login, hub navigation, preserving
  flight filters/results, rapid city switching, interrupted flight search,
  continued frames during a blocked database operation, logout cancellation and
  signing in again. Automated fixture databases are isolated from normal travel.db.
- Output: `Builds/DestinationHub/TravelPlannerDestinations.exe` with its
  adjacent Data/runtime files. Do not distribute the executable alone.

## Practical limits and workspace state

These automated checks are headless. They do not establish rendered appearance,
physical mouse/keyboard interaction, or manual desktop-window resizing. Complete
the walkthrough in [the destination hub guide](Milestone-4-DestinationHub.md) for
those checks.

Work remains uncommitted on **DuyEdit**; no commit, push, database reset or
discarding of prior work occurred. Parent review found `git diff --check` reports
only the pre-existing trailing whitespace at ProjectSettings.asset line 537;
that unrelated setting-file whitespace was left unchanged.
