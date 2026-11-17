# Unity project context
Updated 2026-10-28.

## Confirmed environment
Unity 6000.3.24f1, URP 17.3, Input System 1.20, uGUI/TextMeshPro and Unity Test Framework. No Pipeline/MCP package; direct Unity batch-mode validation is available. No new package dependencies.

## Structure and architecture
Assets/Code contains original global ScriptableObject models and compatible MonoBehaviour adapters; Foundation contains namespaced TravelApp services and presentation. TravelApp.Runtime assembly references UI, TMP and Input System. Editor catalog discovery is isolated in TravelApp.Editor; EditMode and PlayMode tests have separate test assemblies.
Product assets are authored under Assets/Database/Products (Flights, Hotel, Restaurants, Experiences), seed users under Assets/Database/Users. TravelCatalogBuilder maintains stable product IDs, database lists and the build-safe Resources/TravelCatalog.asset; it runs on imports, menu command and before builds.
TravelAppRuntime bootstraps AppServices; AccountService owns sessions, BookingService owns cart/receipts/reviews, JsonFileStore owns versioned atomic JSON persistence. Authored User collections are legacy fields, never runtime storage.

## Scenes and presentation
Build order: LoginScreen, HomeScreen, AccountScreen, BookingScreen. The four scene contents were replaced with serialized AppShell prefab instances and one EventSystem. Scene GUIDs and domain assets are preserved. All nine UI panels exist in edit mode. TravelScreenUI binds saved references; feature presenters under Assets/Code/UI handle auth, catalog, details, cart and history. Dynamic records use three item prefabs under Assets/UI/Prefabs. Legacy managers/prefabs remain unused for reference.

## Validation
Unity EditMode tests cover domain rules, persistence failure/recovery, catalog completeness and serialized scene references. PlayMode test exercises registration, browsing, cart, checkout, cancellation, logout, existing login and booking search/filter/reset. Test saves are temporary or in memory.

## Extension guidance
See Docs/EditableUIGuide.md for editing the UI; Docs/FoundationGuide.md covers data, pricing and saves. Follow the existing simple C# style; preserve stable IDs and metadata. Catalog authored ratings drive filtering; saved user reviews are displayed separately. Pricing is flat per selected product, not per night or traveler. Sessions intentionally reset after restart. Offline classroom app, single-process storage; no real bookings, online services, payments or production authentication.

## Source evidence
ProjectSettings/ProjectVersion.txt, EditorBuildSettings.asset, Packages/manifest.json, Assets/Code, Assets/Database, Assets/Scenes, Assets/Prefabs and Assets/Tests. Original working tree included uncommitted BookingScreen/ProductCard edits and product additions; these were preserved.
