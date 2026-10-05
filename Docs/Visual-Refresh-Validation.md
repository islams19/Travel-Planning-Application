# Editable scene visual refresh validation

Validated on 2026-10-05 using Unity 6000.3.24f1, Windows x64, on DuyEdit. Changes remain uncommitted.

## Delivered assets

Seven application pages were updated as saved uGUI scene objects: Registration, Home, Flights, Destinations, Reviews, SavedTrips and Notifications. Their Canvases are visible in Edit mode. Five saved result-card prefabs were restyled. Runtime scripts bind data and actions to these assets. The one-time Editor authoring tools were removed.

Login.unity remained byte-for-byte unchanged (SHA256 `22C5117FC932F9F4598972B9D7B5FCEB9D5099CE579B365A7B2FFADDD71D938F`). The seeded database remained unchanged (SHA256 `374D0E97789C66E8D8B37E4B890DB8308545A9FCF7D75D8DD99EEDFEFFE2838C`).

## Automated and player checks

- **165/165 automated tests passed**, zero failures or skips. Evidence: `Logs/VisualRefresh/tests-delivery.xml` and `tests-delivery.log`.
- Both final Windows builds succeeded: `Builds/TravelPlannerRelease/TravelPlanner.exe` and `Builds/TravelPlannerDevelopment/TravelPlannerDevelopment.exe`. Build logs: `Logs/VisualRefresh/release-delivery-build.log` and `development-delivery-build.log`.
- **12/12 development player scenarios passed**, exercising first launch and reopen for authentication, flights, destinations, reviews, trips and tracking. Evidence: `Logs/Mvp/5067dc17f58f4aefa14ab3bd9f30c3ca/`. Flight UI search took 35–36 ms in these runs; database calls ran on worker threads.
- **9/9 release checks passed**, including seven rejected harness arguments and first launch/reopen. Evidence: `Logs/Release/ad6f5dae4c0843f98536ee021abf1bbe/`. Both harnesses confirmed the personal database was unchanged.
- **36 final rendered screenshots reviewed**, covering nine states at 1000×700, 1280×720, 1920×1080 and 1920×600. Evidence: the release evidence folder's `Screenshots/`. Four contact sheets are in `Logs/VisualRefresh/contact-*.jpg`.

The initial rendered review caught stretched background borders. Neutral nine-slice artwork corrected them before these final builds and screenshots. Five old scene assertions were updated to require the newly requested Edit-mode Canvas visibility; reference and runtime checks were retained.

## Editing and remaining manual acceptance

Follow [Editable application screens](Visual-Refresh.md). Open a page, select its Canvas, enable 2D and press F in Scene view. Edit objects and prefabs through the Inspector; start Play mode from Login for the complete flow.

Physical mouse/keyboard acceptance, drag resizing across monitors/DPI settings, default-browser opening and installation on a clean Windows machine remain manual acceptance checks. Automated screenshots and player scenarios do not replace those checks.

Photography is stored locally with [source and license credits](../Assets/TravelPlanning/Art/Photography/CREDITS.md). Photo credit files are also included in both delivered build folders under `ThirdPartyNotices/`. No live data API was introduced.
