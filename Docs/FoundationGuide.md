# Offline Travel Planner — foundation guide

## Start here
Open this project in Unity **6000.3.24f1**. Wait for imports to finish, then open **Assets/Scenes/LoginScreen.unity** and press Play. Existing authored users retain their existing classroom passwords. Use **Create account** in the bottom bar to make a new local account.

The left sidebar provides Home, Explore, Your cart, Your bookings and Log out. All screens are saved as editable objects in the shared Assets/UI/Prefabs/AppShell.prefab, with an instance in each scene. Feedback appears along the bottom. See Docs/EditableUIGuide.md for previewing and editing any screen outside Play Mode.

## Demo walkthrough
1. Create an account or log in.
2. Browse the catalog or open Search and try the type, price, rating and date filters. Dates use YYYY-MM-DD. Invalid input gives feedback instead of silently dropping the filter.
3. Open a product's details, choose dates within its authored availability, and add it to the cart.
4. Open Cart, review the items, then confirm the simulated booking. No real payment, ticket reservation, stock deduction or internet access occurs.
5. Open Account to view receipt snapshots, cancel a booking or open a purchased product to write/update a review.
6. Log out and log back in, or restart the application. Each account's cart and booking history is saved separately.

Each product has a **flat price per booking**. Dates select availability, with no per-night, per-traveler or quantity multiplication. A cart cannot contain the same product and date range twice. Cancellation preserves the receipt. Reviews require an uncancelled booking; one review per user/product can be updated. Submitted reviews are displayed separately from the catalog's authored rating, which remains the search rating.

## Folder-based catalog
Create Product assets using **Create > Travel App > Product**, under:
- Assets/Database/Products/Flights
- Assets/Database/Products/Hotel
- Assets/Database/Products/Restaurants
- Assets/Database/Products/Experiences

Set product type, name, description, location, price, availability, dates and optional sprite. Folder names organize assets; the productType field determines filtering. Create User assets under Assets/Database/Users for classroom seed accounts. Keep usernames unique ignoring case and surrounding spaces.

**Travel App > Synchronize Catalog** discovers these assets, fills the existing database lists, assigns missing stable product IDs from their asset GUIDs and updates Assets/Resources/TravelCatalog.asset. It runs on imports and before builds as well. The Resources catalog includes the referenced assets in offline desktop builds. It does not rely on AssetDatabase at runtime.

Keep existing Product IDs when renaming or moving assets. After duplicating a Product asset, clear the duplicate's ID and synchronize so it receives its own ID. Duplicate IDs and usernames block catalog synchronization/builds. Deleted products no longer appear in Browse; old receipts remain intact, and stale cart items can be removed.

## Modules and extension points
- **Product, User, ProductDatabase, UserDatabase, TravelDate:** original authored data types. Public serialized fields and existing script GUIDs are retained. Legacy User cart/history/review lists are not mutated by the new services.
- **TravelCatalog / TravelCatalogBuilder:** folder discovery, stable identity, build inclusion and editor-only automation.
- **ProductQuery / ProductSearch:** search validation, matching, availability filtering and sorting, independent of screen controls.
- **AccountService:** registration, authentication and a scene-independent session. New account passwords are salted and derived before saving. Authored classroom credentials remain in their original assets; this is an offline teaching demo, not a production identity system.
- **BookingService:** cart validation, checkout, immutable-in-practice receipt snapshots, cancellation and product reviews. Catalog changes cannot change an old receipt's name, location, dates or price.
- **ISaveStore / JsonFileStore:** versioned JSON persistence with atomic replacement, backup recovery and preservation of unreadable files. Failed saves roll back changes and report an error. Replace ISaveStore to change storage without rewriting booking rules.
- **AppServices:** composes the services around one save document. Changed events notify UI after successful changes.
- **TravelAppRuntime:** small persistent bootstrap and asynchronous scene navigation. Owns services, never references a destroyed login input.
- **LoginManager / BookingManager / ProductCardUI / ButtonManager:** legacy adapters retained for reference. The new scenes use TravelAuthView, TravelCatalogView, TravelDetailsView, TravelCartView and TravelAccountView. Search reuses authored card prefabs and waits 0.2 seconds after input before refreshing.
- **TravelScreenUI and Assets/Code/UI presenters:** bind services to saved panels and reusable item prefabs. Visual customization is done in Unity, not in layout-building runtime code. Legacy ButtonManager adapters remain unused in the new scenes.

Runtime, editor and test code have separate assembly definitions. No additional packages were installed.

## Local saves
The file is **Application.persistentDataPath/travel-data-v1.json**, with a .bak backup after replacements. Sessions are not persisted; restart requires login. Carts, local registrations, receipt snapshots, cancellation and reviews are persisted.

Corrupt or unsupported saves are recovered from backup when possible. Unreadable originals are preserved with a .recovered suffix before a new save is written. This is a single-process classroom app; running multiple instances against the same save is not supported. To reset demo data, close the app and move the save file and its .bak out of that folder. Authored catalog assets are independent of this save.

## Tests
In Unity Test Runner, run EditMode and PlayMode tests under Assets/Tests. EditMode covers search/date constraints, persistence/recovery, save failures, account isolation, receipt snapshots, checkout and cancellation, and scene/catalog references. PlayMode exercises the supplemental UI's registration-to-checkout flow with temporary in-memory services, leaving real save data unchanged.

Start builds with LoginScreen first in Build Settings; the existing four scene paths remain in Build Settings. The added UI uses Input System, uGUI and TMP already in this project. Visual assets can be attached to products and your existing prefabs without changing domain logic.
