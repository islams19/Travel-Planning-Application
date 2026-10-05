# Milestone 8 — Polish and release inspection

Read-only starting-point review on **DuyEdit**. M7's recorded checkpoint was
137 passing tests and two development Windows player runs. Those are prior
results, not evidence that M8 has passed. Existing uncommitted work is preserved.

## Gaps selected for M8

| Evidence | Practical implication |
| --- | --- |
| TrackingSetup.cs builds with BuildOptions.Development | A real non-development player needs its own build and validation. |
| Existing Test-*.ps1 player harnesses use -nographics | They verify behavior but do not establish rendered appearance. |
| LoginPage only honors development database overrides; old smoke flags reject release execution | Release QA needs an explicit validated fixture path before account work, avoiding the personal database. |
| New Canvases use 1920 × 1080 scaling with Match Width = 0 | Short/wide windows reduce available logical height; small windows also shrink text. |
| Tracking and trip panels place fixed-height controls before their list | Short-window checks must ensure controls and a useful list remain reachable. |
| Destination descriptions/place rows include fixed heights | Long names and descriptions need actual wrapping/overlap checks. |
| Login has custom Tab/Shift-Tab; later forms do not | Keyboard traversal should consistently skip unavailable controls and respect modal/dropdown state. |
| Modal close handlers clear focus | Returning focus to a useful entry control improves keyboard continuity. |

The AuthSetup source contains an old next-milestone message, but
FlightSearchSetup replaces it in the prepared scene. It is not a confirmed
current-screen placeholder defect. Loading/error/empty states already exist;
test their appearance rather than replacing working data flows.

## Preservation boundaries

Keep the original 22 Kevin login RectTransforms unchanged. New-screen layout
or keyboard helpers should not redesign his form. Preserve uGUI, legacy Input
Manager, the offline SQLite schema/data and existing session/ownership rules.
No unrelated package upgrade, seed replacement or personal database reset is
part of polish. Price changes remain demo-only in Editor/development builds.

## Planned validation coverage

Use 1000 × 700 (current default), 1280 × 720, 1920 × 1080 and 1920 × 600 (short
boundary) as the requested size matrix. These are targets, not yet verified
supported results. Inspect login/registration, signed-in home, flight search,
destination hub, reviews, saved trips and tracking, including long and empty
content. Record actual player dimensions and screenshots, not just requested
resolution values.

A graphics-enabled Windows player can capture the real screen after rendering;
inspect the resulting PNG files. Screen Space Overlay UI is not captured by
simply rendering a camera to a texture. Unity 6.3 documents that screenshot
capture should wait for frame completion and that WaitForEndOfFrame does not
run in Editor batch mode. See [CaptureScreenshotAsTexture](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ScreenCapture.CaptureScreenshotAsTexture.html)
and [WaitForEndOfFrame](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/WaitForEndOfFrame.html).

The release-QA entry is `-releaseSmoke` with an explicit strictly validated
`auth-smoke-<GUID>.db` fixture. Old development smoke flags must remain rejected
by a release player. Validate both successful isolation and malformed/missing
fixture rejection before claiming release safety.

## Evidence pending at the starting checkpoint

At inspection time, M8 compilation, regression tests, release player runs and
screenshot inspection were pending. The [final validation record](Milestone-8-Validation.md)
now records their results. Physical mouse/keyboard interaction and actual
default-browser launch remain manual checks; invoking UI callbacks or viewing
screenshots does not prove them.
