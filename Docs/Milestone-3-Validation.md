# Milestone 3 validation

## Baseline and scope

Unity 6000.3.24f1, Windows x64 Mono, uGUI 2.0.0, legacy Input Manager, DuyEdit. Existing uncommitted Milestones 1/2 are preserved. The password-policy update passed all 12 account tests and both Windows auth checks. This milestone adds round-trip flight search only; no schema or seed-data changes are required.

Planner supplied the acceptance checklist; grunt inspected actual SQLite data, query plans and scene integration; separate workers implemented backend and UI; the orchestrator reviews and validates. No commit/push is authorized as part of this milestone.

## Required checks

| Criterion | Evidence to record |
|---|---|
| Original login unchanged | Compare source RectTransform geometry with Kevin branch |
| Actual indexed route search | EXPLAIN QUERY PLAN from production SQL builder |
| Correct flights and return direction | JFK/LHR June 15/22 tests |
| Local-time correctness | UTC conversion and morning/afternoon test cases |
| Price, airline and time filters | Individual and combined filter tests |
| Sort orders | Price, local departure and airline name tests |
| Input and empty states | Invalid IDs/dates/filter values, equal airports, missing route |
| No user-data changes | Search fixture persistence/read-only evidence |
| Session and stale-result safety | Close/logout while pending, reopen flow |
| Scene references and scrolling | Unity scene/UI tests |
| Windows behavior and performance | Actual player smoke flow with measured search-to-results timing |
| Auth regression | Existing auth tests and player smoke where appropriate |

## Validation limits

All flight schedules, prices and seat counts remain fictional data. Outbound/return sections show independent offers, not a purchased or paired itinerary. A headless player check exercises actual controls but does not prove rendered appearance, mouse hit-testing, clipping at every window size, or accessibility. Manual resize/visual checks are listed in the guide. Performance results apply to the seeded workload on this computer, not all future catalog sizes.

## Unity results

- Preparation compiled and generated the screen/prefab successfully (`Logs/flight-prepare.log`, `FLIGHT_SEARCH_SCENE_READY`).
- **56/56 tests passed, zero failures** in the full regression suite (`Logs/flight-tests.xml`, `Logs/flight-tests.log`). This includes the existing 36 account/database/UI tests, nine flight-service tests, and 11 flight UI/price-validation cases.
- The production SQL query-plan test confirmed `flights_search` use. Both legs return two expected airlines; sorting, local-time filtering, exact price limits, invalid values and an unseeded route passed.
- Existing user/trip database bytes were unchanged by options/search/query-plan reads. Request snapshot and cancellation tests passed.
- The actual Play-mode UI flow passed login, filters, empty/invalid states, cancellation during a blocked database operation, continued frame updates, re-login, retry, Back and Logout.
- Scene tests verified all serialized references, viewport dimensions, masks and content fitters. All 22 original Kevin-scene RectTransforms retained their anchors, positions, sizes and pivots in a separate source comparison.
- Editor search measurement from the UI flow: **32 ms** through result binding/layout, including **7 ms** database/service work. Worker thread 10 differed from UI thread 1. Windows timing is recorded separately below.

No compile error or unresolved automated failure was introduced. Existing Unity licensing-refresh/non-Windows playback-module log messages remain unrelated environment noise.

## Windows results and final status

- Windows x64 Development build succeeded: `Builds/FlightSearch/TravelPlannerFlights.exe` (`Logs/flight-build.log`, `FLIGHT_SEARCH_BUILD_PASS`). Keep the executable with its adjacent Data/runtime files.
- **Both actual-player runs passed**, first register/search and then reopen/search. Logs: `Logs/FlightSearch/a4467a28f72343d499e60ffed89a2d82/`.
- First run: **33 ms** UI search-to-results, **17 ms** database/service work; worker 7, main 1.
- Reopen run: **33 ms** UI search-to-results, **18 ms** database/service work; worker 9, main 1.
- The timing starts with the Search operation and includes result card binding/layout; authentication and initial options loading are excluded. Both are well within the requested 2–3 seconds on this seeded workload.
- Player scenarios passed: persisted account login, two offers each way, descending price sort, airline and morning filters, maximum-price empty results, clear filters, invalid same-airport input, valid unseeded route, continued frame updates while SQL was deliberately blocked, logout cancellation without stale results, re-login/retry and Back.
- The fixture uses `auth-smoke-a4467a28f72343d499e60ffed89a2d82.db`, separate from normal travel.db. Both scene and player automation enforce isolated fixture paths.

**Ready with visual-validation limitations.** All 56 automated tests and both player scenarios passed. Rendered appearance, physical mouse/keyboard interactions and manual resize checks remain for the guide's manual walkthrough. No commit or push; branch remains DuyEdit. Stop here before Milestone 4 (destination hub).
