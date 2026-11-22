# Flight Map module

## Overview

`FlightMap` is an isolated Unity 6 uGUI module for planning and visually simulating a flight. It does not provide live tracking, schedules, fares, or navigation advice. The initial route is JFK–LAX.

Implemented features:

- sharp 4096×2048 offline Natural Earth world map;
- drag panning, mouse-wheel zoom, and `+` / `−` controls;
- real WGS84 coordinates and offline search for 83 airports covering all 62 booking destinations and legacy map locations;
- departure/destination markers and labels;
- automatic route framing whenever either endpoint changes;
- sampled great-circle route (not a straight screen-space tween);
- rotating airplane with Start, Pause/Resume, Restart, progress, and arrival state;
- great-circle distance, transparent estimated travel time, and separate demo duration;
- responsive 1280×720 reference canvas, verified at 1280×720 and 1920×1080 aspect targets, using TextMesh Pro;
- EditMode tests for projection, route endpoints, and JFK–LAX distance.

## Design and dependencies

The module uses only packages already in this project: Unity uGUI, TextMesh Pro, and Input System. No package manifest or existing scene is changed.

The base image is derived from [Natural Earth I 1:50m](https://www.naturalearthdata.com/downloads/50m-raster-data/50m-natural-earth-1/). [Natural Earth's terms](https://www.naturalearthdata.com/about/terms-of-use/) place its raster and vector data in the public domain. The source is 4096×2048 and is imported uncompressed for Standalone, with trilinear filtering, mipmaps, negative mip bias, and 8× anisotropic filtering. A subtle blue-gray RawImage tint reduces the original white-water glare.

Options evaluated:

- [Mapbox Unity SDK](https://github.com/mapbox/mapbox-unity-sdk): its documentation lists Unity 6 as best-effort, requires a Mapbox token for API calls, and adds Burst, Newtonsoft JSON, glTFast, meshopt, and other dependencies. It was researched but not installed or tested.
- [UnitySlippyMap](https://github.com/jondewoo/UnitySlippyMap): MIT licensed and targets Unity 2021.3+, but the repository was archived in 2024 and its own TODO still lists orthographic-camera and movement work. It was researched but not installed or tested.
- [OpenStreetMap tile policy](https://operations.osmfoundation.org/policies/tiles/): OSM data is open, but the community tile servers are capacity-limited and are not a free production CDN. Runtime tile calls were intentionally avoided.

## Folder structure

```text
Assets/FlightMap/
├── Documentation/       README and test record
├── Editor/              repeatable scene/prefab/sprite builder
├── MapAssets/           offline public-domain map image
├── Prefabs/             generated airplane and marker prefabs
├── Scenes/              generated FlightMapScene.unity
├── Scripts/             runtime modules and assembly definition
├── Sprites/             generated original icons
└── Tests/               EditMode tests and assembly definition
```

## Build/open the scene

1. Open the existing `Travel-Planning-Application` folder in Unity `6000.3.24f1` or a compatible Unity 6 release.
2. Wait for script compilation to finish with no Console errors.
3. Choose **Tools → Flight Map → Build Flight Map Scene**. This creates/refreshes only files under `Assets/FlightMap`.
4. In Project, open **Assets → FlightMap → Scenes → FlightMapScene**.
5. Press Play. Search by airport code, airport name, or city; select both endpoints; then use **Start Flight**.

For a recorded smoke test, choose **Tools → Flight Map → Run Play Mode Validation**. The test
enters Play Mode, validates the map and references, exercises the flight states and zoom, captures
`Tests/FlightMapGameView1280x720.png` and `Tests/FlightMapGameView1920x1080.png`, and writes `Tests/RuntimeValidation.txt`.

The final recorded validation completed with 22 Play Mode checks passed and 3 EditMode tests
passed. Evidence is stored in `Tests/RuntimeValidation.txt`, `Tests/EditModeResults.xml`, and
the two resolution-labeled Game-view captures; detailed notes are in `Documentation/TESTING.md`.

The builder is intentionally repeatable. It regenerates the FlightMap scene and its two prefabs, but does not touch other scenes, Build Settings, packages, or project settings.

## Scene hierarchy and references

The builder creates `FlightMapCanvas` with a header, left controls/information panel, and clipped map viewport. `MapContent` is the single panned/scaled transform containing the map image, route graphic, markers, labels, and airplane, so overlays remain aligned.

`FlightMapManager` coordinates the module. `FlightLocationSelector` publishes endpoint changes. `FlightRouteRenderer` samples a great-circle arc. `AirplaneAnimationController` consumes the same samples, so its position and heading match the visible route. `FlightInformationManager` formats facts and status. `FlightMapViewport` owns all coordinate conversion, panning, and zooming.

Button listeners are serialized by the builder:

- Start Flight → `AirplaneAnimationController.StartFlight`
- Pause / Resume → `AirplaneAnimationController.TogglePause`
- Restart Flight → `AirplaneAnimationController.RestartFlight`
- `+` / `−` → `FlightMapViewport.ZoomIn` / `ZoomOut`

## Geographic calculations

Airport coordinates are WGS84 decimal degrees. The base image uses an equirectangular world extent, so longitude and latitude map consistently to normalized X/Y. Distance uses the haversine formula with mean Earth radius 6371.0088 km. Route samples use spherical interpolation between unit vectors; the plane traverses those samples.

The displayed duration is a planning estimate: 45 minutes ground allowance plus distance at 850 km/h. It is labeled as an estimate and is not airline schedule data. The 12-second demo duration is separately labeled and configurable in `AirplaneAnimationController`.

## Rebuild manually from scratch

The recommended exact rebuild is the menu command above. To reproduce it manually:

1. Create a new scene via **File → New Scene → Empty**, then save it as `Assets/FlightMap/Scenes/FlightMapScene.unity`.
2. Create **GameObject → UI → Canvas**. Set Canvas Scaler to **Scale With Screen Size**, 1280×720, Match 0.5.
3. Create one EventSystem and use `InputSystemUIInputModule`.
4. Under the Canvas, add a 64-pixel header, a 360-pixel left panel, and a stretch map panel.
5. Under the map panel create `MapViewport`, add Image and Rect Mask 2D, then add a centered 4096×2048 `MapContent`.
6. Add Raw Image to `MapContent`; assign `NaturalEarthWorld.jpg` and stretch it fully.
7. Add full-stretch `GreatCircleRoute` with `FlightRouteRenderer`; add marker Images, labels, and an airplane Image as siblings.
8. Add search Input Fields and Dropdowns for both endpoints; add the three flight buttons, two zoom buttons, Slider, and information labels.
9. Add `FlightMapViewport` to viewport, selector/information components to the left panel, airplane controller to map panel, and `FlightMapManager` to a root object.
10. Assign all Inspector references and button events exactly as listed above. Ensure noninteractive graphics have Raycast Target off.

## Extending and troubleshooting

### Visual clarity settings

The blurry first version combined a 1920×1080 Canvas reference with 14–16 point text, so a smaller
Game view reduced important labels to roughly 8–10 physical pixels. Input/dropdown labels also used
top-edge fixed anchors instead of inset stretch anchors, the map's Standalone import was limited to
2048 pixels with compression, and two button rectangles had one-pixel heights because of an
overloaded builder helper. The current builder fixes all four causes.

- Adjust general typography in `FlightMapSceneBuilder.Text`, `Input`, `Dropdown`, and `Info`.
- Keep TMP Auto Size disabled; the included Liberation Sans SDF asset uses point size 86 and scale 1.
- Adjust airplane and marker sizes where their RectTransforms are created in the builder.
- Adjust route visibility with `FlightRouteRenderer.thickness` (currently 6).
- Keep `NaturalEarthWorld.jpg` at 4096×2048 and uncompressed for Standalone for the sharpest result.
- Re-run **Tools → Flight Map → Build Flight Map Scene** after builder-level visual changes.

- Add airports and booking labels in `TravelLocationCatalog.cs` with verified coordinates. `FlightLocationDatabase` is a compatibility adapter; both Search Flights and Explore read this shared catalog. See `LocationCatalog.md` for provenance and city/airport semantics.
- Change `demonstrationDurationSeconds` on the airplane controller.
- Increase route samples for very large displays; 80 is sufficient for the included compact view.
- If controls do not click, confirm exactly one EventSystem, an Input System UI Input Module, and a Graphic Raycaster.
- If text is missing, import TMP Essential Resources; this project already contains the Liberation Sans SDF asset.
- If the map is missing, confirm `MapAssets/NaturalEarthWorld.jpg` exists and rerun the builder.
- If Scene view shows thin red lines, switch to the **Game** tab first. Screen Space Overlay UI is
  not intended to be judged from the 3D perspective Scene camera. The current scripts also repair
  negative-height TMP rectangles from the first scene version at runtime.
- If Console reports that `GreatCircleRoute` has no CanvasRenderer, let Unity recompile the current
  scripts, reopen the scene, and save it. `FlightRouteRenderer` now installs the missing component
  before clipping begins; rebuilding the same scene also serializes it permanently.
- Routes crossing the ±180° seam may require seam-splitting before drawing; the included demonstration routes avoid that edge case.

## Installation elsewhere

Copy the entire `Assets/FlightMap` folder into another Unity 6 project's `Assets` folder. That project needs uGUI, TextMesh Pro Essentials, and Input System. Then run the builder menu command. Do not copy individual scripts without their assembly definitions and assets.

## Sharp Game view preview

The editor-only `FlightMapSharpGameView` utility selects a fixed 1280 x 720 preview when this scene
opens and disables Unity's low-resolution aspect-ratio mode. Use **Tools > Flight Map > Sharp Game
View > 1920 x 1080** for a Full HD preview. If the Game tab is manually changed to Free Aspect and
enlarged above its internal render size, Unity can make the entire frame look pixelated even though
the TMP font and 4096 x 2048 map texture are sharp.

## Flight and zoom button wiring

The scene stores persistent UnityEvent callbacks so reopening the project does not lose button
behavior:

- `StartFlightButton` calls `AirplaneAnimationController.StartFlight`.
- `PauseResumeButton` calls `AirplaneAnimationController.TogglePause`.
- `RestartFlightButton` calls `AirplaneAnimationController.RestartFlight`.
- `ZoomInButton` calls `FlightMapViewport.ZoomIn`.
- `ZoomOutButton` calls `FlightMapViewport.ZoomOut`.

The EventSystem uses `InputSystemUIInputModule` with its point, click, navigation, and scroll actions
assigned. The Canvas has a `GraphicRaycaster`; decorative map, route, marker, airplane, and TMP
graphics do not block button raycasts. If rebuilding the scene, always use the included builder,
which records these connections with persistent listeners rather than runtime-only `AddListener`.

## Demo flight prices

The map now displays rounded, clickable price bubbles at real destination coordinates. They are
explicitly labeled **DEMO PRICES - NOT LIVE**; the generated USD amounts are deterministic sample
data and must not be presented as current tickets. Clicking a bubble keeps the current departure,
selects that destination, redraws the great-circle route, refreshes distance and estimated travel
time, and shows the selected demo amount. Bubbles move with map pan/zoom while compensating for map
scale so their text remains a consistent screen size.

- `FlightPriceDataProvider.cs` defines the provider contract and offer model.
- `DemoFlightPriceDataProvider.cs` generates offline demonstration offers.
- `FlightPriceManager.cs` refreshes offers, positions markers, and coordinates selection.
- `FlightPriceMarker.cs` owns one clickable bubble and its price/airport labels.

For live fares, replace the demo provider with a server-backed implementation of
`IFlightPriceDataProvider`. Keep credentials on that server; never embed an airline/aggregator API
secret in a Unity client. Amadeus Self-Service provides test and production environments, Duffel's
test mode uses non-realistic sandbox data, and Skyscanner access requires an approved partnership.
Use each provider's current terms and attribution requirements before shipping.

Verified provider documentation:

- Amadeus FAQ: https://admin.developers.amadeus.com/self-service/apis-docs/guides/developer-guides/faq/
- Amadeus test data: https://amadeus4dev.github.io/developer-guides/test-data/
- Duffel test mode: https://duffel.com/docs/api/overview/test-mode
- Skyscanner authentication: https://developers.skyscanner.net/docs/getting-started/authentication

## DesktopLayout / Explore World integration

`DesktopLayout` embeds `Prefabs/ExploreWorldMap.prefab` below the existing `exploreworld_page`.
It contains the original map Content and manager, without the standalone Canvas, header, camera,
or EventSystem. The application provides those shared objects and its existing sidebar navigation.
`Tools > Flight Map > Integrate Explore World` rebuilds this prefab from the original standalone
scene and replaces only its instance under Explore World. The original map scene is retained.

Map airport selections and demo prices remain independent of booking/search state. Hiding and
reopening Explore World retains its existing map objects and selections; JFK–LAX is only an initial
default. Invalid or identical airports clear the route, markers, distance, and airplane and disable
the flight controls until valid endpoints are selected. Layout repairs are restricted to map Content,
and preserve the separate fare/code labels inside price bubbles. Dragging converts pointer movement
to the shared Canvas coordinates; zoom can frame the entire offline map in the embedded viewport.

`Assets/Tests/PlayMode/ExploreMapFlowTests.cs` exercises map selection, dropdown pointer events,
prices, dragging, zoom, flight states, invalid endpoints, sidebar persistence, one shared Canvas and
EventSystem, unchanged non-map text layouts, and unchanged booking selections.

### Explicit Search Flights handoff

The desktop host adds **Search flights to this destination** below Explore's controls. Only clicking
this button passes the selected airport code to the existing booking autocomplete catalog. The booking
UI resolves its canonical city label through `DestinationAutocomplete.TrySelectAirportDestination`.
It preserves the booking origin and does not transfer map fares, progress, animation state, dates,
or selected flights. Changing Explore selections without clicking the button remains independent.
Unsupported catalog destinations stay on Explore with a message and leave the booking untouched.
The adapter lives in `Assets/Code/ExploreBookingBridge.cs`; FlightMap.Runtime has no booking dependency.


### Explore refinement validation

The shared catalog includes 83 airports, all 62 original booking destination labels, and
alternative airports for supported cities. Each distinct endpoint pair has a deterministic,
explicitly demo fare; these offers never become booking fares.

Dropdown rows have full-width raycast targets and top-anchored scroll content. Regression
checks use actual EventSystem raycast hits before clicking both popups. Pins and the new
top-down airplane retain small screen sizes across zoom levels. Price bubbles are prioritized,
spaced, and culled; the selected demo fare remains visible in its fixed banner. Mouse-wheel
zoom preserves the cursor location, while buttons zoom around the viewport center.

Validation: 9 Play Mode tests and 56 editor tests passed (65 total), including existing booking
flows, Explore isolation/handoff, dropdown clicks/search, centered and cursor zoom, marker
sizes, bubble overlap, dateline routes, catalog coverage, and deterministic demo offers.


### Compact Explore route preview

Explore hides the legacy Start/Pause/Restart controls, progress bar, demo animation duration,
and status labels while retaining their wiring for the standalone map. A new valid endpoint
pair automatically plays a three-second, non-looping preview through the existing airplane
controller and leaves the plane at its destination. Repeated selection notifications for the
same pair preserve playback and map pan/zoom; invalid pairs still clear stale route visuals.
Route facts and the Search Flights handoff move up into the freed space.

Validation after this change: all 9 Play Mode regression tests passed, including automatic
play/restart/completion without looping, invalid endpoints, map interactions, demo offers,
explicit handoff, and the existing booking flow.
