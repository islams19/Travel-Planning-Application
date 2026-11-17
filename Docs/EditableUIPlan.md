# Editable travel UI
User authorizes replacing the original visual layout. Preserve services, catalog, saves and stable asset identities.

Use a serialized uGUI AppShell prefab in each of the four existing scenes. Panels, labels, inputs, navigation and templates exist in edit mode. Runtime changes data and visibility only; no layout construction. Separate auth, catalog, details, cart and history presenters consume existing services. Reusable list-item prefabs expose text/image/button references in the Inspector.

Use a warm light background, navy sidebar, teal actions, restrained coral accents, clear headings and scrollable content at desktop sizes. Keep a single Canvas and EventSystem in each scene. Preserve all offline behavior.

Build assets through Unity Editor APIs in an isolated validation copy; back up and replace the four original scenes only after checks pass. Validate serialized bindings, edit-mode visibility, no runtime duplicate Canvas, UI modification persistence, full PlayMode flow, two desktop resolutions, and screenshot renders. Document prefab editing and screen switching.
