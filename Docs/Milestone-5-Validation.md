# Milestone 5 — Validation record

## Confirmed preparation and tests

Validation used Unity **6000.3.24f1** on branch **DuyEdit**.

| Check | Result | Evidence |
| --- | --- | --- |
| Reviews scene preparation | PASS, process exit 0; `REVIEWS_SCENE_READY` | `Logs/reviews-prepare.log` |
| Full EditMode suite, including earlier milestones | **115 passed / 115 total; 0 failures** | `Logs/reviews-tests.xml`, `Logs/reviews-tests.log` |
| Original login layout | All 22 original RectTransforms unchanged | Compared anchors, anchored position, size and pivot with Kevin reference commit `0ae085f9889591310dc46e6e14b7bd507c8c0ab3` after preparation |
| Saved development database override | Empty | The saved scene's `developmentDatabaseFile` field |

The suite covers all four place categories; review requests and navigation
state; cancellation; empty reviews; unavailable or invalid links; all 144 seed
Maps references; rejected malformed URLs; and a complete 3,290-character body
rendered as literal text with layout checks. Earlier authentication, flight
search and destination tests ran in the same full suite.

## Observed Editor timings

| Workflow | UI elapsed | Database elapsed | Database worker thread | Main thread |
| --- | --- | --- | --- | --- |
| Reviews (`REVIEWS_LOAD_TIMING`) | 10 ms | 3 ms | 10 | 1 |
| Destination regression | 27 ms | 4 ms | 16 | 1 |
| Flight regression | 21 ms | 7 ms | 59 | 1 |

These are measurements from this fixture and machine, not a performance
guarantee. Worker IDs differing from the main thread confirm that the measured
database work ran off Unity's main thread.

## Windows build and player

The Windows x64 build passed with process exit 0 and `REVIEWS_BUILD_PASS` in
`Logs/reviews-build.log`. The executable is
`Builds/Reviews/TravelPlannerReviews.exe`.

`tools/Test-Reviews.ps1` exited 0. Both actual Windows player runs emitted
`REVIEWS_UI_SMOKE_PASS`:

| Scenario | UI elapsed | Database elapsed | Worker / main thread | Log |
| --- | --- | --- | --- | --- |
| Register and browse reviews | 14 ms | 2 ms | 12 / 1 | `Logs/Reviews/ccfff0d0c46649688638eeff98002d2e/register-reviews.log` |
| Reopen and browse reviews | 14 ms | 3 ms | 9 / 1 | `Logs/Reviews/ccfff0d0c46649688638eeff98002d2e/reopen-reviews.log` |

The player checks exercise review UI, stars, the fake browser boundary,
empty/invalid-link cases, cancellation and recovery. Both runs use a recording
URL opener; neither launches an actual browser. They run headlessly, so the
manual limitations below still apply.

To reproduce after building, run this from the project root in PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\Test-Reviews.ps1
```

## Manual verification still required

Automated tests replace the browser opener with a recording callback. They
verify the validated URL and click boundary without launching a real browser.
An actual default-browser launch remains a manual check.

The runs do not establish correct rendered appearance, physical mouse or
keyboard behavior, or desktop window resizing. The long-body layout assertion
is useful structural evidence, but is not a visual inspection. Follow the
[Reviews guide](Milestone-5-Reviews.md) for those Play mode and Windows checks.

## Working-tree boundaries

Work remains uncommitted on **DuyEdit**. No commit, push or runtime database reset
was performed for this validation. Existing pending work was preserved. The
remaining `git diff --check` finding is the pre-existing trailing whitespace in
`ProjectSettings/ProjectSettings.asset` at line 537; it was not changed as part
of this milestone.
