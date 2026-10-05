# Travel Planning Application — Current Guide

Unity **6000.3.24f1**, **2D** desktop screens, Windows x64, uGUI, legacy Input Manager, and a local SQLite database. Work in **DuyEdit** unless the team explicitly chooses another branch.

## Current structure

The code has been reorganized into separate models, actions, repositories, services, screen controllers and views. Shared parent classes handle database execution and screen-request cleanup. Start with [project structure and click-by-click editing instructions](Project-Structure-and-Editing.md). Unity Hub now lists this checkout as **CSIT 435**; use its `Documents\ChatGPT\CSIT 435` path.

Each application page has its own `.unity` file under `Assets/TravelPlanning/Scenes`, including **Login.unity**, the startup scene. Start with [separate page scenes and exact paths](Separate-Page-Scenes.md). **Travel Planning > Open Editable Project** opens the selected page scene for editing. The runtime connects the page scenes while retaining the account session. See [refactor validation](Refactor-Validation.md) for the earlier single-scene checkpoint; the separate-scene validation is recorded independently. Current `.cs` files are authoritative; older full-script documents below are historical snapshots.

## Milestone checkpoint before the refactor

[Milestone 8: desktop polish and release checks](Milestone-8-Polish.md) is implemented with **153/153 automated tests, 12/12 development-player scenarios and 9/9 release-player checks passed** on 2026-09-29. All 36 screenshots were inspected across four actual window sizes. See [validation evidence and remaining manual acceptance](Milestone-8-Validation.md), [complete scripts](Milestone-8-Full-Scripts.md), and [starting inspection](Milestone-8-Inspection.md).

Run `Builds/TravelPlannerRelease/TravelPlanner.exe` for the normal Windows app; keep its entire build folder together. Use `Builds/TravelPlannerDevelopment/TravelPlannerDevelopment.exe` for the price-change demonstration. These local build artifacts are ignored by Git. Physical mouse/keyboard use, default-browser opening, drag-resizing/DPI and clean-machine testing remain manual. Work remains uncommitted on **DuyEdit**. The planned eight milestones stop here for team acceptance.

[Milestone 7: price tracking and notifications](Milestone-7-PriceTracking.md) passed 137/137 automated tests and two Windows player scenarios at its checkpoint. See [validation evidence and limitations](Milestone-7-Validation.md), [full scripts](Milestone-7-Full-Scripts.md), and [inspection](Milestone-7-Inspection.md). M8 adds actual release-build and rendered validation.

[Milestone 6: saved trips](Milestone-6-SavedTrips.md) passed 126/126 automated tests and two Windows player scenarios at its checkpoint. See [validation evidence and manual-check limitations](Milestone-6-Validation.md), [full scripts](Milestone-6-Full-Scripts.md), and [inspection](Milestone-6-Inspection.md).

[Milestone 5: ratings and traveler reviews](Milestone-5-Reviews.md) passed 115/115 automated tests and two Windows player scenarios at its checkpoint. See [validation evidence and manual-check limitations](Milestone-5-Validation.md), [full scripts](Milestone-5-Full-Scripts.md), and [inspection](Milestone-5-Inspection.md). Actual browser launch remains a manual check.

[Milestone 4: destination hub](Milestone-4-DestinationHub.md) passed 69/69 automated tests and two Windows player scenarios at its checkpoint. See [validation evidence and visual-check limitations](Milestone-4-Validation.md), [full scripts](Milestone-4-Full-Scripts.md), and [inspection](Milestone-4-Inspection.md).

[Milestone 3: flight search](Milestone-3-FlightSearch.md) is implemented and validated with 56 passing tests and two Windows player runs. See [complete scripts](Milestone-3-Full-Scripts.md), [validation and limitations](Milestone-3-Validation.md), and [inspection](Milestone-3-Inspection.md).

[Milestone 2: authentication](Milestone-2-Authentication.md) describes the SQLite registration, login, and logout work. [Kevin login inspection](Milestone-2-Login-Inspection.md) records the original scene, asset references, and visual preservation boundaries. Follow the milestone's validation report for what was actually tested; this index does not claim that validation has finished.

The app uses the `travel.db` schema introduced in Milestone 1B. Authentication work replaces the earlier plaintext LiteDB implementation. Existing `accounts.db` files are historical local data: they are not migrated, opened, or deleted by the new login flow. Register a fresh SQLite account.

## Guides and evidence

- [Editable application screens and visual refresh](Visual-Refresh.md)
- [Visual refresh: final validation](Visual-Refresh-Validation.md)
- [Separate page scenes: paths and editing instructions](Separate-Page-Scenes.md)
- [Separate page scenes: current validation](Separate-Scenes-Validation.md)
- [Milestone 8: desktop polish and team QA guide](Milestone-8-Polish.md)
- [Milestone 8: full scripts snapshot](Milestone-8-Full-Scripts.md)
- [Milestone 8: final validation and limitations](Milestone-8-Validation.md)
- [Milestone 8: starting-point inspection](Milestone-8-Inspection.md)
- [Milestone 7: price tracking guide](Milestone-7-PriceTracking.md)
- [Milestone 7: full scripts snapshot](Milestone-7-Full-Scripts.md)
- [Milestone 7: validation record](Milestone-7-Validation.md)
- [Milestone 7: starting-point inspection](Milestone-7-Inspection.md)
- [Milestone 6: saved trips guide](Milestone-6-SavedTrips.md)
- [Milestone 6: full scripts snapshot](Milestone-6-Full-Scripts.md)
- [Milestone 6: validation record](Milestone-6-Validation.md)
- [Milestone 6: starting-point inspection](Milestone-6-Inspection.md)
- [Milestone 5: reviews guide](Milestone-5-Reviews.md)
- [Milestone 5: full scripts snapshot](Milestone-5-Full-Scripts.md)
- [Milestone 5: validation record](Milestone-5-Validation.md)
- [Milestone 5: starting-point inspection](Milestone-5-Inspection.md)
- [Milestone 4: destination hub guide](Milestone-4-DestinationHub.md)
- [Milestone 4: full scripts snapshot](Milestone-4-Full-Scripts.md)
- [Milestone 4: validation record](Milestone-4-Validation.md)
- [Milestone 4: starting-point inspection](Milestone-4-Inspection.md)
- [Milestone 3: flight search guide](Milestone-3-FlightSearch.md)
- [Milestone 3: starting-point inspection](Milestone-3-Inspection.md)
- [Milestone 2: authentication guide](Milestone-2-Authentication.md)
- [Milestone 1A: SQLite proof of concept](Milestone-1A-SQLite.md)
- [Milestone 1A: validation record](Milestone-1A-Validation.md)
- [Milestone 1A: full scripts snapshot](Milestone-1A-Full-Scripts.md)
- [Milestone 1B: schema and seeded catalog](Milestone-1B-Database.md)
- [Milestone 1B: validation record](Milestone-1B-Validation.md)
- [Milestone 1B: full scripts snapshot](Milestone-1B-Full-Scripts.md)
- [Editable catalog and data limitations](../Assets/TravelPlanning/SeedData/README.md)
- [SQLite dependencies, versions, and import instructions](../Assets/Plugins/SQLite/README.md)

Milestone guides and full-script documents describe their own point in time. Current `.cs` files are authoritative if later milestones have changed them. Flight search and later screens follow authentication; the diagnostic scenes are not completed application screens.

## Historical material

[Duy Explanation 9.17.26](Duy%20Explanation%209.17.26.md) describes the superseded LiteDB prototype, including insecure plaintext passwords. It is retained as project history, not current implementation or security guidance.
