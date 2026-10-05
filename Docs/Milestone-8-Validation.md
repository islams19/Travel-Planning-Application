# Milestone 8 — Validation record

Validated locally on **2026-09-29** with Unity **6000.3.24f1**, Windows x64/Mono, uGUI and the legacy Input Manager. Branch: **DuyEdit**. Implementation and automated/rendered checks are complete; physical input, browser opening and clean-machine acceptance remain manual.

## Results

| Check | Result | Local evidence |
| --- | --- | --- |
| Final scene preparation | Passed; no compile errors | `Logs/m8-prepare.log` |
| New keyboard/layout/argument tests | 16/16 passed | `Logs/m8-targeted-tests.xml` |
| Full regression suite | **153/153 passed**, 0 failed | `Logs/m8-tests-final.xml`, `Logs/m8-tests-final.log` |
| Non-development Windows build | Passed, `BuildOptions.None` | `Logs/m8-release-build-final.log` |
| Development Windows QA build | Passed | `Logs/m8-development-build.log` |
| Current development-player regression | **12/12 passed** | `Logs/Mvp/5b58e1cd42f24474bd6df9d4eb604b4b/` |
| Actual release-player checks | **9/9 passed** | `Logs/Release/4e87fea481794d99a3b6761b5d594edc/` |
| Rendered review | **36 PNGs captured and inspected** | Release run's `Screenshots/` directory |

Build outputs:

- Release: `Builds/TravelPlannerRelease/TravelPlanner.exe`.
- Development/demo: `Builds/TravelPlannerDevelopment/TravelPlannerDevelopment.exe`.

Keep each executable with its entire build directory, including the `_Data`, `MonoBleedingEdge`, native libraries and `ThirdPartyNotices` files. Builds and logs are local ignored artifacts, not source-control content.

## What the player checks establish

The development harness runs first-launch and reopening scenarios for authentication, flights, destinations, reviews, saved trips and price tracking against the **same final development artifact**. It exercises actual UI callbacks, persistence, account isolation, cancellation, invalid/empty states, review URL validation with a fake opener, and development price simulation. It does not simulate physical mouse or keyboard hardware.

The release harness rejects missing filenames, the normal database filename, malformed GUIDs, traversal, duplicate filename arguments, combined smoke modes and a development-only smoke flag. These seven processes exit unsuccessfully as expected, before opening a database.

The two valid release processes register/log in, search flights, open destinations/reviews, display saved items and watches, reopen the same isolated fixture and log out. The QA driver provisions its trip/watch/notification fixture through the existing services; the development regression separately exercises the corresponding save/track UI controls. The release driver asserts `Debug.isDebugBuild == false`, checks that demo controls are hidden/disabled, invokes the disabled demo callback directly and verifies that the entire fixture database hash remains unchanged.

Ordinary release launches use `travel.db`. Diagnostic code remains in the binary but only runs with explicit `-releaseSmoke` plus a strictly validated `auth-smoke-<32 hexadecimal GUID characters>.db` filename. It does not grant access to an arbitrary database path or enable the release demo button.

## Performance

| Final player measurement | First launch | Reopen |
| --- | --- | --- |
| Release flight UI completion | 32 ms | 33 ms |
| Release flight database query | 17 ms | 17 ms |
| Development flight UI completion | 36 ms | 36 ms |
| Development flight database query | 18 ms | 19 ms |
| Development destination UI/database | 23 / 3 ms | 24 / 3 ms |
| Development review UI/database | 15 / 2 ms | 15 / 2 ms |
| Development saved-trip database | 1 ms | 1 ms |
| Development tracking database | 2 ms | 2 ms |

Logged worker IDs differ from Unity's main thread ID (1). These local seeded-data observations satisfy the 2–3 second search target on this machine; they are not a guarantee for other hardware or larger catalogs.

## Rendered screen review

The graphics-enabled release player changed its actual window dimensions to **1000×700, 1280×720, 1920×1080 and 1920×600** and captured the rendered backbuffer. Each size includes login, registration, signed-in home, flight results, destination hub, reviews, saved trips, notifications and a login error.

All 36 final images were reviewed in four contact sheets, with the default login and saved-trip views also inspected individually. No missing primary navigation controls, overlapping primary sections or clipped modal headers were observed in these captured states. Long lists continue below their scroll viewport as intended. Small/short windows scale text down; comfortable reading at a preferred Windows display scale still needs a teammate's judgment.

The first capture attempt caught a black startup frame. The QA helper now waits for splash completion and two distinct rendered frames, rejects a black image, and the rebuilt release produced a visible default login. This was a capture-timing fix, not evidence that the application remained black. The initial run is retained at `Logs/Release/403551db141e4aefbafacebf0ab47426/`; use the final run listed above.

Screenshots establish the listed rendered states, not every destination, scrolled list position, arbitrary text length, DPI/monitor combination or physical drag-resize interaction.

## Preservation and packaging

- All **22 original Kevin login RectTransforms** retain their anchors, positions, sizes and pivots compared with commit `0ae085f9889591310dc46e6e14b7bd507c8c0ab3`.
- The canvas scaling policy now fits the reference layout to short windows. LoginPage hands Tab traversal to the desktop helper; its existing Enter submission remains. These are the explicit touches to the existing login integration.
- All **27 existing ProjectSettings/Packages files** match the M8 starting hashes. `activeInputHandler` remains 0.
- The saved Login scene has a blank `developmentDatabaseFile`.
- The normal persistent `travel.db` was absent before and after validation. Both harnesses also compare the presence/hash of its journal/WAL/SHM sidecars. No personal database was reset or deleted.
- Release packaging contains exactly one Windows x64 `e_sqlite3.dll`; the packaged seed matches the source seed.
- No new TravelPlanning asset is missing its `.meta` file.
- Seed database SHA-256: `374D0E97789C66E8D8B37E4B890DB8308545A9FCF7D75D8DD99EEDFEFFE2838C`.
- Editable catalog SHA-256: `5013B7EF204692AEF26F81864843253833886AFBEE79178C2F3A8A9A595366C7`.
- Targeted runtime/fixture ignore rules preserve the intentional `travel_seed.db`; no broad database ignore was added.
- `git diff --check` retains the pre-existing `ProjectSettings/ProjectSettings.asset:537` trailing space on `m_SubKind:`. Unrelated settings were not rewritten to remove it.
- Work remains uncommitted on **DuyEdit**; no push was performed.

## Test corrections and remaining manual acceptance

The initial suite passed 151/153. Both failures were new test-fixture issues: EventSystem registration was attempted outside Play mode, and an EnterPlayMode domain reload discarded a captured lambda closure. The fixtures were corrected, all 16 targeted checks passed, then the full 153-test suite passed. No existing feature regression required a runtime fix. The later screenshot-only helper change was compiled into both final builds and exercised in the final release capture run.

Before the team calls the MVP fully accepted, follow the five-person checklist in [the M8 guide](Milestone-8-Polish.md): physical clicks/scrolling, Tab/Shift+Tab/Enter/Escape, window dragging/resizing, actual default-browser opening, all destination sections, and a Windows 10/11 machine without this Unity installation. These have **not** been represented as passed.

The MVP still uses fictional offline USD data and fixed June 2027 dates. It has no live prices, purchasing, password recovery, cross-device accounts or OS notifications. Google Maps links are search references rather than verified direct review-page links. Price notices are created by the supported local price-change service, not by scanning arbitrary external SQL edits.
