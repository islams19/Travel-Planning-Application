# Refactor validation — 2026-10-04

Scope: reorganize the existing application into focused classes, readable action sections, shared parent behavior and editable **2D** Unity screens. Branch: **DuyEdit**. No commit or push was made.

## Result

| Check | Result | Evidence |
| --- | --- | --- |
| Unity 6000.3.24f1 import/compilation and scene preparation | Passed | `Logs/Refactor/prepare-final.log` |
| Full automated suite, including scene and Play-mode integration checks | **156/156 passed** | `Logs/Refactor/tests.xml`, `tests.log` |
| Follow-up 2D scene and screen-preview tests | **4/4 passed** | `Logs/Refactor/2d-tests.xml`, `2d-tests.log` |
| Final 2D Windows x64 development build | Passed | `Logs/Refactor/2d-development-build.log` |
| Final 2D Windows x64 release build | Passed | `Logs/Refactor/2d-release-build.log` |
| Development-player first-launch/reopen scenarios for six features | **12/12 passed** | `Logs/Mvp/326613cc9bff4d3f93952b1ffd0d0b1b/` |
| Release argument isolation and first-launch/reopen behavior | **9/9 passed** | `Logs/Release/14fa76b102964a02b408255955a78cc2/` |
| Nine rendered screens at four actual window sizes | **36 screenshots captured and visually inspected** | Same release folder, `Screenshots/` |
| Original Kevin login layout | **22 RectTransforms unchanged** across anchors, position, size and pivot | `Logs/Refactor/preservation.json` |
| Seeded database | SHA256 unchanged | Same preservation report |
| Normal user database and sidecars | Unchanged by both player harnesses | Harness before/after JSON and output |
| Unity Hub desktop opening | Correct **CSIT 435** checkout registered and launched in 6000.3.24f1 | Live desktop verification |
| Editor authoring | Login scene opened; actual Canvas selectable and framed in flat 2D through the new editing window | Live desktop verification |

Captured window sizes: 1000×700, 1280×720, 1920×1080 and 1920×600. Screens include login, registration, flights, destination hub, reviews, saved trips, notifications, login error and signed-in home. No new clipping or layout regression was observed; long lists remain scrollable. The measured flight-search UI time was 40–42 ms in the final development-player checks on this machine; this is not a universal performance guarantee.

## Structure reviewed

- 74 runtime C# files; one top-level model/class/enum per matching file.
- Separate account registration/login actions and generation-safe session.
- Separate trip list/load/create/save/remove actions with repository and validation.
- Separate tracking load/target/track/stop/read actions with repository.
- Shared `DatabaseServiceBase`, `UserDatabaseActionBase`, `ScreenControllerBase` and `EditorUiBuilderBase`; child constructors/methods explicitly call their parents where appropriate.
- Six focused UI view classes and separate authentication QA code.
- Existing MonoBehaviour identities, metadata and serialized reference names retained.
- Named controller actions and grouped Inspector fields; `.editorconfig` added.
- Across 147 source files, lines over 180 characters dropped from 448 to 10; maximum line length dropped from 1,188 to 206. Remaining long lines are readable literals/expressions, rather than many statements packed together.

The intended baseline settings changes are `EditorBuildSettings.asset`, through Unity's Editor API, to include **Login.unity** instead of SampleScene, and `EditorSettings.asset`, to default to **2D** authoring. Unity also rewrote `VersionControlSettings.asset` to Unity Version Control during desktop opening; no cloud workspace was created and no account was connected. Git work remains on DuyEdit. Package versions and legacy input configuration remain unchanged.

The follow-up 2D change sets existing application cameras to orthographic projection with a solid white background and disables any camera Skybox/3D Light components. It keeps the original UI geometry. Screen previews hide the grid and skybox. The four targeted tests run after this change; the 156-test full suite is the refactor checkpoint immediately before it.

## Failures corrected during this run

The first Unity compile found duplicate methods in the extracted Editor helper. The shared helper was corrected and the successful preparation, full suite and both builds ran afterward. The initial failure log remains at `Logs/Refactor/prepare.log`; it is not the final result.

A final Editor-only minimum-window-size adjustment improves readability of the project path and instructions. It does not change player code or serialized assets. It is checked through the desktop Editor rather than repeating the full player suite.

## Practical limits

The refactor preserves the existing MVP design, fictional seeded data and local-account behavior. Clean-machine native dependency testing, multi-monitor/DPI acceptance, full manual keyboard navigation, default-browser opening and teammate usability review remain team acceptance work. Automated flows exercise the application but do not replace those checks.

Use [project structure and editing instructions](Project-Structure-and-Editing.md) for click-by-click authoring. Do not overwrite current scripts with older milestone documentation snapshots.
