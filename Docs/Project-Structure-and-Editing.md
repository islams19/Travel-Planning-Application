# Project structure and editing

This is an editable **2D desktop application**, using Unity **6000.3.24f1**, uGUI and the legacy Input Manager. Open the repository root containing `Assets`, `Packages` and `ProjectSettings`. Work on **DuyEdit**.

**Current scene layout:** each page now has its own `.unity` file. See [separate page scenes](Separate-Page-Scenes.md) for the full list, paths and authoring steps. Login no longer contains the feature pages.

## Open the correct project in Unity Hub

1. Open **Unity Hub > Projects > Add > Add project from disk**.
2. Select `C:\Users\playm\OneDrive\Documents\ChatGPT\CSIT 435`.
3. Open the **CSIT 435** entry with Editor **6000.3.24f1**. It has been registered in Hub. Check the path; the separate `Documents\GitHub\Travel-Planning-Application` copy is a different checkout.
4. Wait for importing and compilation. In the Project window, open **Assets > TravelPlanning > Scenes > Login**.
5. Open **Travel Planning > Open Editable Project**. The window shows the actual project path.

If Unity offers to enable the installed new Input System, choose **Don't Enable**. This application uses the legacy Input Manager.

## 2D configuration

The application's scene cameras use orthographic projection and a solid white background. Its screens use flat **Screen Space - Overlay** Canvases; there are no 3D models or scene lighting in the application flow. The project defaults to **2D** for authoring. The screen-editing window uses 2D Scene view with skybox and perspective grid hidden.

To check it yourself, open **Edit > Project Settings > Editor** and find **Default Behavior Mode: 2D**. Select **Camera** in the Login scene: **Projection: Orthographic**, **Background Type: Solid Color** (or **Clear Flags: Solid Color**), and a white background. These settings are prepared by `Assets/TravelPlanning/UI/Editor/Desktop2DSetup.cs`. Orthographic projection is the flat camera mode described in the [Unity 6.3 camera reference](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Camera-orthographic.html).

## Edit one screen

The application build starts with **Login**, followed by seven separate page scenes: **Registration**, **Home**, **Flights**, **Destinations**, **Reviews**, **SavedTrips**, and **Notifications**. The database demonstration scenes and template scene remain additional supporting entries. To see the list, open **File > Build Profiles > Scene List**. **Travel Planning > Release > Configure Application Build Scenes** restores this list; release/development builds use it too.

1. In **Travel Planner Screens**, choose **Flights**, **Destinations**, **Reviews**, **Saved trips**, or **Price tracking** in the **Screen** dropdown.
2. Click **Show Screen in Scene view**. Unity opens that page's separate scene and makes its Canvas visible for editing.
3. Select a label, button or layout object in the **Hierarchy**. Edit its text, colors, anchors and layout in the **Inspector**.
4. Click **Select screen controller** to see that screen's grouped references. Click **Open controller C# script** to edit its behavior.
5. For generated result rows/cards, click **Show editable prefabs folder** and open the matching prefab. Changes to the prefab affect newly displayed rows.
6. Save with **Ctrl+S**. Screen-preview visibility restores before Save, Play, script reload and closing the window. Your actual Inspector edits remain. Click **Show Screen** again to continue previewing it.
7. Press **Play**, register or log in, then navigate to the edited screen to test it.

**Existing login page:** its original design remains the baseline. Login, Registration and Signed-in home are now distinct scenes. Their account logic is shared through AccountController in Login. Changing the Login scene's original objects is an intentional edit to the existing login design.

Do not rerun the preparation menus simply to edit a screen. Those menus generate/wire missing objects and can reset generated layout values. Use the scene, Inspector and prefabs for normal authoring.

## Where code belongs

| Folder | Responsibility | Examples |
| --- | --- | --- |
| `Assets/TravelPlanning/Runtime/Data` | SQLite initialization and shared database execution | `TravelDatabase`, `DatabaseServiceBase` |
| `Runtime/Accounts` | Account validation, hashing, session and account operations | `AccountService`, `AccountSession`, `Actions/LoginAccountAction` |
| `Runtime/Flights`, `Destinations`, `Reviews` | Catalog queries and validation | `FlightSearchService`, `ReviewService` |
| `Runtime/Trips` | Saved-trip operations and SQL | `TripService`, `Actions/CreateTripAction`, `TripRepository` |
| `Runtime/Tracking` | Price watches, notifications and simulation | `PriceTrackingService`, `Actions/TrackPriceAction`, `TrackingRepository` |
| `Runtime/<feature>/Models` | One data class or enum per matching file | `FlightOption`, `TripSummary`, `PriceNotice` |
| `Assets/TravelPlanning/UI/<feature>` | Screen control and presentation | `FlightSearchPage`, `FlightSearchView`, `FlightResultRow` |
| `UI/Shared` | Shared screen-request lifetime | `ScreenControllerBase` |
| `UI/Editor` | Scene-building and editing tools, excluded from players | `EditorUiBuilderBase`, `EditableProjectWindow` |
| `Assets/TravelPlanning/Tests` | Behavior and scene-wiring checks | Edit-mode and Play-mode checks |

A **model** holds data. A **repository** contains database queries. An **action** performs one operation, such as saving an item. A **service** gives the screen a small set of methods and passes work to those actions. A **controller** responds to user input; a **view** fills dropdowns and creates displayed rows. Plain classes are constructed by their owners; only `MonoBehaviour` components belong on GameObjects.

## Parent classes and action sections

Inheritance means a child reuses a parent's behavior. The database actions derive from `UserDatabaseActionBase`, which derives from `DatabaseServiceBase`. Constructors use `: base(...)`; actions use `base.ExecuteAsync(...)` to pass database work through the shared queue. Feature screen controllers derive from `ScreenControllerBase` and call `base.OnDestroy()` for shared cleanup.

Parent classes contain shared behavior. Login, save, remove and track retain separate action classes. Named action comments group controller operations; `#region` sections group service and builder operations. Inspector `[Header]` sections group references by purpose.

Follow `.editorconfig`: four-space indentation, braces and statements on readable lines. Keep class names matched to filenames. Keep Unity `.meta` files with their assets so scene references stay connected.

## Five-person task split

1. Accounts: account actions and login/registration behavior.
2. Flights: flight search service, page, view and row prefab.
3. Destinations/reviews: place and review views, services and prefabs.
4. Trips: trip actions, repository, page, view and saved-item prefab.
5. Tracking: watches, notifications, price simulation and their views/prefabs.

Assign one integrator to merge scene changes. Prefer feature-prefab edits and feature-owned scripts; avoid two teammates editing `Login.unity` simultaneously. Keep `.gitignore`, `.meta` files and branch policy. This refactor does not commit or push work.

## Recognize common mistakes

- **Missing Inspector reference:** the Console names a page and asks for references. Select its controller; fill every required field using objects from this scene.
- **Missing script component:** a file/class rename or missing `.meta` can break a reference. Keep existing component class/file names and metadata together.
- **Screen small in Edit mode:** select its Canvas and press **F** in Scene view. Application page Canvases are saved visible for direct editing.
- **Preview restored on Save:** expected. Choose **Show Screen** again; do not permanently activate all Canvases.
- **Wrong checkout opens:** check the project path in the editing window against the path above.
- **Runtime rows missing before Play:** edit their prefabs. SQLite fills lists after login/search.
- **Database changes appear unchanged:** the seeded database copies only on first launch; existing local user data is preserved. Do not delete a teammate's database as a cleanup step.

See the current `.cs` files for complete source. Older milestone full-script documents are historical snapshots, not files to paste over the refactored code.
