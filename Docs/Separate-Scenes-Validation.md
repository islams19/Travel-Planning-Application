# Separate page scenes: validation

Validated on 2026-10-04 (local time), Unity 6000.3.24f1, Windows x64, branch DuyEdit. See [page paths and Editor instructions](Separate-Page-Scenes.md).

## Result

Login, Registration, Home, Flights, Destinations, Reviews, SavedTrips and Notifications are eight actual scene files. Login remains the startup scene. Runtime loads the seven other scenes additively, connects shared dependencies before enabling their controllers, and retains the account session. Every page transition selects the corresponding active scene. There is one EventSystem across all eight loaded scenes.

The build list includes these eight pages first, followed by the existing SqliteProof, TravelDatabase and SampleScene support scenes. Both Windows players were rebuilt from this list.

## Checks passed

| Check | Result | Local evidence (ignored by Git) |
| --- | --- | --- |
| Actual scene migration | Passed | `Logs/SceneSplit/migration-final.log` |
| Automated Editor/integration tests | 165 passed, 0 failed, 0 skipped | `Logs/SceneSplit/tests.xml` |
| Development Windows build | Passed | `Logs/SceneSplit/development-build.log` |
| Release Windows build | Passed | `Logs/SceneSplit/release-build.log` |
| Development player scenarios | 12/12 passed: first launch and reopen for authentication, flights, destinations, reviews, trips and tracking | `Logs/Mvp/908847ce5b534445930576c64e0b08ba/` |
| Release player checks | 9/9 passed: seven invalid command-line cases, first launch and reopen | `Logs/Release/ce120438cefc4f02a232d992c35566c4/` |
| Page transitions | All nine release checkpoints selected the expected active scene; eight loaded pages and one EventSystem verified | Release `first-launch.log` and `reopen.log` |
| Rendered layout | 36 screenshots reviewed across 1000x700, 1280x720, 1920x1080 and 1920x600 windows | Release `Screenshots/`; contact sheets in `Logs/SceneSplit/` |

The full 165-test run preceded the final QA-only scene-transition assertions. Those assertions then compiled in both final builds and passed in the release player checks.

## Preservation

All 398 original RectTransform identifiers were retained, with no changes to their anchors, pivots, sizes, positions, scales or rotations. The original account cards were moved into their owning scenes. The original login design remains intact; its scene structure and shared reference wiring changed.

The seeded database SHA-256 remains `374d0e97789c66e8d8b37e4b890db8308545a9fcf7d75d8dd99eedfeffe2838c`. Both player harnesses confirmed the normal user's database and sidecars were unchanged. Evidence: `Logs/SceneSplit/preservation.json` and each harness's before/after database records.

The first migration attempt stopped because Unity requires a new additive scene to be saved before another is created. The tool was corrected, rerun successfully, and the original backup retained under `Logs/SceneSplit/Before/`.

## Remaining manual acceptance

On 2026-10-05 Login was relocated to `Assets/TravelPlanning/Scenes/Login.unity` through Unity's AssetDatabase. The scene and metadata hashes are unchanged, preserving GUID `7d913960ecf5841f3815d39fe9b77bd5`. The shared Editor path constant, build scene list and current editing guides were updated. Unity imported and executed the relocation successfully. The automated test and player results above are the previous checkpoint, not a new run after this path-only move. Evidence: `Logs/SceneSplit/login-move-result.txt` and `login-move-before.json`.

Clean-machine native DLL loading, desktop DPI/multiple monitors, all physical keyboard/mouse interactions, default-browser review links and teammate acceptance remain manual. No commit or push was made; changes remain in DuyEdit.
