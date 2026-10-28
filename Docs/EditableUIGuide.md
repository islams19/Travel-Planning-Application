# Editing the Waypoint UI in Unity

## Everything is saved before Play
The four scenes now contain a **WaypointApp** prefab instance and one EventSystem. The old scene UI was replaced, so there is no second interface behind it. App services still start automatically, but they do not create any Canvas, page, input, navigation button or layout at runtime.

Open one of these scenes:
- Assets/Scenes/LoginScreen.unity
- Assets/Scenes/HomeScreen.unity
- Assets/Scenes/BookingScreen.unity
- Assets/Scenes/AccountScreen.unity

In the Hierarchy, expand **WaypointApp > Screens - select a panel to edit**. The nine authored panels are Login, Register, Home, Catalog, Details, Cart, Checkout, Account and Confirmation. One is visible at a time.

## Preview any screen without running
1. Select **WaypointApp** in the Hierarchy.
2. In the **Travel Screen UI** Inspector, choose a **Preview screen**.
3. Click **Show selected screen in Scene / Game view**.
4. Expand that screen's objects to select and edit them normally.

Each panel includes its own **EditablePageHeading** with ordinary TextMeshPro heading and subtitle objects. These texts are authored in the prefab and are not replaced by hardcoded strings when the panel opens.

Use 2D mode in the Scene view, select the Canvas and press F to frame it. The Game view also shows the selected screen outside Play Mode. The **Initial Page** field determines which panel opens when running; previewing a different panel does not change that field.

## Change the shared look
Open **Assets/UI/Prefabs/AppShell.prefab**, or click **Open shared AppShell prefab** in the root's Inspector.

Edit its normal Unity components:
- TextMeshPro labels: wording, font, size, alignment and color.
- Images: background colors and sprites.
- RectTransforms: anchors, padding and placement.
- Layout Groups: spacing and sizing of the children they control.
- Buttons: colors, focus/hover transitions and On Click callbacks.
- Input fields and dropdowns: styling and available options.

Save the prefab. All four scenes share it. Editing a scene instance instead creates a local prefab override; use Unity's Overrides menu if you want to apply that change to every scene.

The layout uses a cream background, navy sidebar, teal actions and soft travel-category cards. Replace the **WelcomeArtwork / ReplaceableArtwork** Image with your own sprite, or adjust the nested artwork objects. Assign product sprites in the existing Product assets to populate catalog and details images.

## Reusable item prefabs
- **Assets/UI/Prefabs/ProductCard.prefab** — catalog results.
- **Assets/UI/Prefabs/CartItem.prefab** — cart and checkout entries.
- **Assets/UI/Prefabs/BookingCard.prefab** — booking history.

These are the actual templates used during Play Mode. Each exposes its image, labels and buttons through **Travel Item View**. Open and edit the prefab to update all runtime entries.

Lists contain a saved **EditorPreview - sample card** so their appearance is visible before running. This sample is hidden when real records are displayed. Select a list and click **Open reusable item prefab** in its Inspector to edit the real template. Dynamic records still instantiate from these prefabs at runtime, which is normal for a data-driven list.

## Modular code responsibilities
- TravelScreenUI: selects authored panels and handles shared navigation/feedback.
- TravelAuthView: login and registration fields.
- TravelCatalogView: search, filters and the product list.
- TravelDetailsView: selected product, dates and reviews.
- TravelCartView: cart totals, checkout and item removal.
- TravelAccountView: receipts and cancellation.
- TravelItemList / TravelItemView: reusable list templates and data binding.

All visual references are serialized in the Inspector. AccountService, BookingService, ProductSearch and JsonFileStore remain separate, so changing the layout does not require changing booking or save rules.

The original scripts and prefab assets under Assets/Prefabs remain as unused legacy assets for reference; the replacement scenes use **Assets/UI/Prefabs**. They do not appear behind the new UI.

## Normal editing does not regenerate the UI
Do not use **Travel App > Create Editable UI** after customizing the interface unless you intentionally want to rebuild the stock layout. That command asks for confirmation because it replaces AppShell and the four scenes. It is a setup/reset tool, not an editing requirement.

## What stays the same
Your folder-based products and users, stable product IDs, saved local accounts, carts, receipt history, cancellations and reviews still use the existing foundation. Pricing is a flat amount per selected product. Checkout is simulated; no real booking or payment occurs.

Run EditMode and PlayMode tests from Unity's Test Runner after major changes. They check serialized scene content, panel previews, layout dimensions, one Canvas/EventSystem per scene, catalog references and the full user flow.
