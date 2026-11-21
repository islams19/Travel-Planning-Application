# Flight Map test record

Date: 2026-11-17  
Target Editor: Unity 6000.3.24f1, URP 17.3.0, uGUI 2.0.0, Input System 1.20.0

## Performed

| Check | Result | Evidence / notes |
|---|---|---|
| Existing project inventory | Pass | Four existing scenes and current package/version files were inspected before changes. |
| Scope isolation | Pass | Flight Map implementation changes are contained under `Assets/FlightMap`. No existing scene or package manifest was edited. `ProjectSettings/ProjectSettings.asset` was already modified outside this feature and was preserved. |
| Reference analysis | Pass | Screenshot features were reviewed: pale compact map, route, plane marker, location labels, and zoom stack. Expedia page itself rejected automated retrieval, so no claim is made that its implementation was inspected. |
| Dependency/license research | Pass | Natural Earth terms, OSM tile policy, Mapbox Unity repository/package metadata, and UnitySlippyMap repository/license/status were inspected. |
| Pure geographic code inspection | Pass | Haversine distance, equirectangular projection, spherical interpolation, endpoint updates, and shared map transform were reviewed. |
| Unity assembly import | Pass | Unity 6000.3.24f1 compiled the module. An initial duplicate Test Runner reference in the test asmdef was found, fixed, and recompiled. An obsolete TMP wrapping warning was also fixed. |
| Scene serialization | Pass | The installed Unity Editor ran `FlightMapSceneBuilder.Build` and serialized the scene, prefabs, sprites, references, and `.meta` files. The scene YAML is 211 KB and contains the expected manager, viewport, markers, airplane, and controls. |
| EditMode test execution | Pass | Unity Test Runner executed `FlightMap.Tests`: 3 passed, 0 failed. Results: `Tests/EditModeResults.xml`. |
| PlayMode visual/interaction checks | Pass | The expanded validator ran in Unity 6000.3.24f1: 22 passed, 0 failed. Evidence: `Tests/RuntimeValidation.txt` and the two resolution-labeled Game-view captures. |

## 2026-11-17 missing-map debugging pass

### Problems discovered

1. Unity's live `Editor.log` repeatedly reported `MissingComponentException` because
   `GreatCircleRoute` had `FlightRouteRenderer` but no `CanvasRenderer`. The exception occurred
   inside `RectMask2D.PerformClipping`, once per UI render, so the route could not render reliably.
2. The first builder version created several top-anchored TMP rectangles with their top/bottom
   offsets reversed. Unity displayed them as red crossed or zero-height rectangles in Scene view.
3. Button TMP labels were created with zero-height top anchors instead of stretch anchors.
4. Both TMP dropdown templates contained a ScrollRect, Viewport, and Content, but the ScrollRect's
   `viewport` and `content` references were not assigned.
5. The supplied screenshot was taken in the 3D Scene tab, where a Screen Space Overlay Canvas is
   shown edge-on/perspective; it was not evidence of what the Game tab rendered.

### Fixes applied

- `FlightRouteRenderer` now declares `CanvasRenderer` as required and self-repairs older serialized
  scene instances in `OnEnable` before `MaskableGraphic` registers for clipping.
- The builder explicitly adds CanvasRenderer and writes valid positive-height TMP rectangles.
- `FlightMapManager.Awake` repairs old button labels and inverted TMP rectangles at runtime, so the
  existing scene works without deleting it.
- `FlightLocationSelector.Awake` repairs existing dropdown ScrollRect references.
- The route color is now a high-contrast cyan similar to the requested highlighted-route reference.
- A one-shot `FlightMapPlayModeValidator` was added to exercise Start, Pause, Resume, Restart,
  zoom, arrival, route samples, map texture, markers, and UI references, and capture the Game view.

### Final verified result

- PASS: project/scene/assets and all serialized manager references inspected.
- PASS: Unity compiled `FlightMap.Runtime`, `FlightMap.Editor`, and `FlightMap.Tests` successfully.
- PASS: the existing scene was rebuilt and saved in place with a serialized `CanvasRenderer`.
- PASS: the Game view renders the focused JFK-LAX map, markers, labels, route, and airplane.
- PASS: Start, Pause, Resume, Restart, Zoom In, arrival state, and arrival message were exercised.
- PASS: runtime references and route samples were validated.
- PASS: the final validation log contained no compiler errors or Flight Map runtime exceptions.
- PASS: all three EditMode geographic tests passed.

The final 1280×720 and 1920×1080-aspect Game-view captures were visually inspected. They show the
header, readable search fields and airport choices, all three flight buttons, information values,
North American city labels, JFK/LAX markers, a cyan great-circle route, enlarged airplane, progress,
arrival confirmation, and right-side zoom controls. Unity's minimized Editor capture surface is
733 pixels wide, but `Screen.SetResolution` exercised both requested aspect/resolution targets and
the Canvas Scaler maintained the same readable layout.

## 2026-11-17 visual clarity pass

### Discovered and fixed

1. The 1920×1080 Canvas reference shrank 14–16 point text excessively in smaller Game views. It is
   now 1280×720 with Match 0.5 and larger 16–22 point functional text.
2. Search/dropdown TMP rectangles were anchored to a thin top strip. They now use inset stretch
   anchors and high-contrast colors.
3. The Standalone map import used a 2048 limit and compression. It now uses the full 4096×2048
   source uncompressed, trilinear filtering, mip bias -0.5, and anisotropic level 8.
4. The original raster's white water looked overexposed. A blue-gray tint improves water/land
   separation without replacing or copying proprietary map tiles.
5. Plane, marker, route, and map-label sizes were increased; real-coordinate city labels were added.
6. Header, Start, and Pause rectangles had reversed or one-pixel heights. Their serialized layout
   was corrected, and the validator now rejects non-visible control rectangles.

### Actual final tests

- PASS: 22/22 Play Mode checks, including search to MIA, distance refresh, restore to LAX, pointer
  drag panning, zoom, Start, Pause, Resume, Restart, arrival, visible controls, 4096×2048 map, and
  readable inset search text.
- PASS: 3/3 EditMode geographic tests.
- PASS: final logs contain no C# compiler errors, `MissingComponentException`,
  `NullReferenceException`, or unhandled Flight Map exception.
- PASS: final captures were manually inspected for visibility, spacing, contrast, labels, markers,
  plane, route, buttons, information, and arrival state.

## Optional manual verification

After running **Tools → Flight Map → Build Flight Map Scene**:

1. Open the generated scene and confirm Console has no errors/warnings from FlightMap.
2. Press Play and verify the initial JFK–LAX markers, labels, curve, facts, and Ready state.
3. Search/select another departure and destination; verify markers, labels, curve, distance, estimate, plane, and progress reset.
4. Start, pause for several seconds, resume, and confirm progress does not advance while paused.
5. Restart during flight and after arrival; confirm plane returns to departure.
6. Let the flight finish; confirm it stops at destination and shows the arrival message.
7. Pan by dragging and zoom with wheel/buttons; confirm all overlays stay aligned and the map remains clipped and readable.
8. Run **Window → General → Test Runner → EditMode → Run All** and record the three `FlightGeoMathTests` results.

## Known limitations

- The airport catalog is a curated 22-location offline demo, not a complete aviation database.
- The base map is offline and sharp at the intended compact scale, but it does not gain street-level detail when zoomed deeply.
- Natural Earth provides terrain and boundaries rather than street-road cartography. The scene adds
  airport/city labels, but true road tiles would require a separately licensed/provider-compliant map source.
- The estimate is not a published airline duration and excludes winds, routing, and airline-specific operations.
- Dateline-crossing route seam splitting is not implemented.
- No live fares, schedules, aircraft tracking, or online geocoding is included.

## 2026-11-17 demo pricing integration

The pricing feature uses clearly labeled offline sample data. No network call or private API key is
present in the Unity project. The first validation exposed an invalid serialized provider reference
because the MonoBehaviour class did not have a matching filename; moving it to
`DemoFlightPriceDataProvider.cs` fixed the reference. Rapid same-frame departure changes also left
old buttons alive until Unity's deferred destruction pass, so old marker objects are now disabled
immediately before destruction. A short-route capture then showed bubbles inheriting map zoom; the
manager now applies inverse scale so bubbles remain readable while their anchors stay geographic.

The final Play Mode report is `Assets/FlightMap/Tests/RuntimeValidation.txt`. It covers provider and
manager initialization, multiple bubbles, demo labeling, destination selection, departure refresh,
missing-price handling, price-to-route/info updates, map-transform alignment, stable bubble size,
all existing flight controls, arrival, and native 1280x720 and 1920x1080 captures.

- PASS: 43/43 final Play Mode assertions; 0 failures.
- PASS: the clean Unity log contains no C# compiler error, `NullReferenceException`,
  `MissingComponentException`, missing pricing reference, or unhandled exception.
- PASS: the final 1280x720 capture was visually inspected; demo bubbles are readable, geographically
  aligned, and remain compact at the closer JFK-MIA zoom.

## 2026-11-17 native-resolution blur correction

The later screenshot showed uniform pixel enlargement across TMP text, buttons, icons, and the map.
That identified a reduced-resolution Game-view preview rather than an individual font or texture
problem. The scene now serializes a pixel-perfect overlay Canvas, the map uses a white (unmodified)
color multiplier, and `FlightMapSharpGameView` selects a fixed full-resolution Game view while
disabling Unity's low-resolution aspect preview whenever this scene opens.

- PASS: 26/26 Play Mode assertions; 0 failures.
- PASS: PNG headers confirm native 1280 x 720 and 1920 x 1080 captures.
- PASS: Start, Pause, Resume, Restart, route updates, pan, zoom, arrival, markers, and information UI.
- PASS: final Unity log has no C# compiler error, missing-component exception, null-reference
  exception, or unhandled exception.
- PASS: native captures were visually inspected for crisp text, buttons, route, markers, labels,
  and map detail.

## 2026-11-17 button and animation repair

The buttons were visible and the EventSystem/Input System actions were valid, but all five saved
buttons had empty persistent `On Click()` lists. The scene builder had used `AddListener` in Edit
Mode; those runtime-only callbacks are not serialized when a scene is saved. The builder now uses
`UnityEventTools.AddPersistentListener` for Start, Pause/Resume, Restart, Zoom In, and Zoom Out.

- PASS: 33/33 Play Mode assertions; 0 failures.
- PASS: all five buttons contain serialized callbacks in `FlightMapScene.unity`.
- PASS: all five button centers receive EventSystem raycasts with no blocking graphic.
- PASS: tests dispatch left-clicks through `IPointerClickHandler`, rather than calling controller
  methods directly.
- PASS: Zoom In enlarges the shared map content and Zoom Out reduces it.
- PASS: Start moves the airplane, Pause freezes its position, Resume continues, and Restart returns
  it to JFK with Ready state and zero progress.
- PASS: repeated Start clicks keep one active animation; the flight reaches LAX and displays arrival.
- PASS: the serialized demonstration duration remains 12 seconds.
- PASS: final Unity log contains no C# compiler error, null-reference exception, missing-component
  exception, or unhandled exception.
