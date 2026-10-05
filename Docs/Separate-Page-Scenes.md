# Separate page scenes

The application pages now have their own Unity scene files. The project root is:

```text
C:\Users\playm\OneDrive\Documents\ChatGPT\CSIT 435
```

See [validation results and remaining manual acceptance](Separate-Scenes-Validation.md).

## Page files

| Page | Path relative to the project root |
| --- | --- |
| Login | `Assets/TravelPlanning/Scenes/Login.unity` |
| Registration | `Assets/TravelPlanning/Scenes/Registration.unity` |
| Welcome | `Assets/TravelPlanning/Scenes/Home.unity` |
| Flights | `Assets/TravelPlanning/Scenes/Flights.unity` |
| Destinations | `Assets/TravelPlanning/Scenes/Destinations.unity` |
| Reviews | `Assets/TravelPlanning/Scenes/Reviews.unity` |
| Saved trips | `Assets/TravelPlanning/Scenes/SavedTrips.unity` |
| Notifications and price tracking | `Assets/TravelPlanning/Scenes/Notifications.unity` |

## Open and edit a page

1. Open **Unity Hub > Projects > CSIT 435** with Unity **6000.3.24f1**.
2. Stop Play mode before editing.
3. In the **Project** panel, open **Assets > TravelPlanning > Scenes**.
4. Double-click the page's scene file, such as **Flights**.
5. Its Canvas is visible immediately in Edit mode. Select the Canvas in the Hierarchy and press **F** in Scene view to frame it. Keep the Scene view's **2D** button enabled. You can also use **Travel Planning > Open Editable Project > Show Screen in Scene view**.
6. Select the desired button, text or layout object and edit it in the **Inspector**.
7. Save with **Ctrl+S**.

You can also use **Travel Planning > Open Editable Project** to select and open the page directly. Result rows remain reusable prefabs under **Assets > TravelPlanning > Prefabs**.

**Existing login page:** the migration changes the scene structure and connects its account controls across scenes. Its original visual design is retained.

## Run the application

Open **Assets > TravelPlanning > Scenes > Login**, then click **Play** and select the **Game** tab. Register or sign in, then use the Welcome page's buttons. Start from Login to exercise the complete account flow.

An **additive scene** is a scene loaded alongside another scene. The application keeps Login loaded for the shared account/database and loads the page scenes alongside it. Each page owns its UI objects in its own file. Navigation chooses which page is visible; loading another page does not log the user out or discard the current flight results. The shared EventSystem lives in Login, so there is only one input handler for the loaded pages.

Cross-scene references are connected by the application's scene bootstrap before page controllers start. Do not drag objects from another open scene into a controller field and expect Unity to save that reference. Local UI references and reusable prefab assets are saved in each page scene normally.

## Team work

Assign different scene files to teammates: accounts (Login/Registration/Home), Flights, Destinations, Reviews/SavedTrips, and Notifications. Agree on ownership before editing a scene or shared bootstrap script. Keep every `.meta` file with its asset and work on **DuyEdit**.

## Common mistakes

- **The page looks small in Scene view:** select its Canvas and press **F** to frame it. All application Canvases are saved visible for editing; the bootstrap hides feature pages as it loads them during Play mode.
- **Play mode asks for login:** start from Login; account-protected pages need a signed-in account.
- **Missing local Inspector reference:** check that page's controller and assign the matching object from that scene. Shared cross-scene dependencies are supplied by the bootstrap. Controllers are saved disabled and enabled after that connection happens in Play mode; do not enable them just to edit their fields.
- **Two inputs respond to one click:** do not add another EventSystem to a page scene. Login provides the shared one.
- **Edits disappeared:** stop Play mode before changing scene objects and save the scene afterward.
- **Old setup menus recreate a combined page:** use the current scene migration/editor tools. Existing migrated scenes are the editable source; avoid replacing them with old milestone code snapshots.

## Build

Open **File > Build Profiles > Scene List** to inspect the enabled scenes. Login must remain first. Use **Travel Planning > Release > Build Windows Release** to produce the current executable. The database demonstration scenes and SampleScene remain separate supporting assets; they do not replace the user-facing page scenes.
