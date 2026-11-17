# Offline travel foundation
User authorized implementation using our judgment. Existing Unity/uGUI and folder-based ScriptableObjects remain. No network, payments, new packages or visual polish.
1. Add testable services: catalog search, accounts/session, cart/bookings/reviews, versioned JSON storage with backup and transactional updates. Cover invalid inputs, isolation, repeated checkout, restart and corrupt saves.
2. Add an editor catalog synchronizer: discover assets from existing folders, persist stable IDs and configure a Resources catalog for builds. Preserve existing GUIDs.
3. Replace manager internals while preserving serialized UI fields and entry points. Add async navigation, pooled search cards and visible validation messages.
4. Supply functional supplemental uGUI account/cart/history/registration/reviews panels; use existing scenes and keep presentation separate from services.
5. Run Unity compilation and EditMode tests in an isolated validation copy, inspect changes, then apply only intentional files to the original project. Document extension points and demo steps.
