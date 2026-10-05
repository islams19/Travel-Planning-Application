# Editable application screens

All eight application scenes are in `Assets/TravelPlanning/Scenes`. Login is the team's original design. The seven other scenes were visually updated in Unity and saved as ordinary uGUI GameObjects. Their Canvases and account cards are visible in Edit mode. A one-time Editor authoring tool applied the changes and was removed; the application does not run that tool or rebuild these layouts.

## Edit a page directly

1. In Unity Hub, open **CSIT 435** with **6000.3.24f1**.
2. Stop Play mode, then open **Assets > TravelPlanning > Scenes** in the Project window.
3. Double-click **Home**, **Flights**, **Destinations**, **Reviews**, **SavedTrips**, **Notifications**, or **Registration**.
4. Select the page's Canvas in the Hierarchy. Hover the Scene view and press **F**. Keep **2D** enabled.
5. Expand the Canvas. Feature pages have **Main > Header**, **Main > TravelBanner**, their existing form/actions, and a results ScrollView. Change labels, colors, spacing, photos and anchors in the Inspector.
6. To replace a banner photograph, expand **TravelBanner > InspirationPhoto > Image** and change its **Image > Source Image**. Update **Aspect Ratio Fitter > Aspect Ratio** to the new image's width divided by height.
7. Home's photograph is **HomeCanvas > Left Panel > Travel Image**. Change **Raw Image > Texture**, then its Aspect Ratio Fitter if needed.
8. Save with **Ctrl+S**. Open **Login** and press **Play** to test the complete account and navigation flow.

## Result cards

Search results come from SQLite, so the number of rows changes during Play mode. Each row uses a saved, editable prefab under `Assets/TravelPlanning/Prefabs`: FlightResultRow, PlaceCard, ReviewRow, SavedTripRow and PriceNoticeRow. Double-click a prefab to edit its layout. The scripts fill those existing fields with data and respond to actions; they do not define the screen layout at runtime.

Do not rename the controller's referenced controls or move them to another scene without updating the shared bindings. Decorative objects such as TravelBanner can be edited freely. Controllers remain disabled in the saved feature scenes until the startup component connects the shared account and database. This does not prevent editing their Inspector fields.

## Appearance and data

The new scenes use the existing blue accent and fonts, soft backgrounds, clearer heading hierarchy, consistent primary/secondary buttons and local travel photography. Banner captions identify photographs as travel inspiration; they are not pictures of the fictional catalog businesses. Photo sources and CC0 licenses are in [photography credits](../Assets/TravelPlanning/Art/Photography/CREDITS.md). The app works offline.

`Art/UI/RoundedPanel.png` is an original neutral UI shape. Its sprite uses nine-slice borders: Unity keeps the corners small while stretching the middle of a button or panel. The Inspector controls its color. Shadows are ordinary Unity UI Shadow components, also editable in the Inspector.

The bootstrap hides the visible feature Canvases immediately as scenes load in Play mode, then navigation shows the requested page. Editing visibility therefore does not change startup behavior. Login scene content and the seeded database were preserved.

Work remains on **DuyEdit**. Coordinate scene ownership among teammates and include `.meta` files with assets.

See [visual refresh validation](Visual-Refresh-Validation.md) for final builds, tests and screenshot evidence.
