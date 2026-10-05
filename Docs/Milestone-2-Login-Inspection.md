# Milestone 2 — Kevin login integration inspection

Source: `origin/kevin-frontend-login`, commit
`0ae085f9889591310dc46e6e14b7bd507c8c0ab3`, fetched and inspected without switching
from `DuyEdit`. This describes the original saved scene, before authentication
wiring. Runtime success and build validation are reported separately.

## Imported asset scope

- `Assets/Scenes/Login.unity` and its `.meta`
- `Assets/Images/` and `Assets/Images.meta`
- `Assets/TextMesh Pro/` and `Assets/TextMesh Pro.meta`

These paths were absent at the initial inspection, with no collisions for their
critical GUIDs. Importing original metadata preserves image/font references. The
whole TMP resource tree is used so settings, fallbacks, shaders, line-breaking,
styles, and sprite dependencies stay together. Do not import Kevin's project
settings, HomeScreen, other scene shells, Button scripts, or button prefabs.

| Dependency | GUID |
|---|---|
| Login.unity | `7d913960ecf5841f3815d39fe9b77bd5` |
| Images/rounded_input_bg.png | `c83fa5a52717f435087681c036b57d21` |
| Images/travel_login_left_image.png | `1bfa4fb27fe3446119cf9205e788cb5d` |
| LiberationSans SDF.asset | `8f586378b4e144a9851e7b34d9b748ee` |
| LiberationSans SDF - Fallback.asset | `2e498d1c8094910479dc3e1b768306a4` |
| Fonts/LiberationSans.ttf | `e3265ab4bf004d28a9537516768c1c75` |
| Shaders/TMP_SDF-Mobile.shader | `fe393ace9b354375a9cb14cdbbc28be4` |

The main font references its fallback and shader; the fallback references the
source font. The shader includes `TMPro_Properties.cginc`. TMP Settings also
references default styles, line-breaking assets and EmojiOne. Package component
GUIDs resolve through installed uGUI/TMP, URP and (before replacement) Input System.

## Original hierarchy

```text
Canvas
  Left Panel
    Travel Image
  Right Panel
    LoginCard
      Welcome back
      Subtitle
      EmailLabel
      EmailInput / Text Area / {Text, Placeholder}
      PasswordLabel
      PasswordInput / Text Area / {Text, Placeholder}
      Forgot?
      LoginButton / Text (TMP)
      First Time?
      Create an account
Camera
EventSystem
```

Canvas uses Scale With Screen Size, 1920×1080, Match Width Or Height **0**.
Its render mode is Screen Space Overlay, with no render-camera reference.
Left/right panels stretch, with width offset -960 and anchored x positions
-480/+480. LoginCard is centered in the right panel, 580×700. Original controls
have top-left anchors and pivot (0,0.5); positions below are relative to that card.

| Control | Position | Size |
|---|---|---|
| Welcome back | 0,-65 | 400×80 |
| Subtitle | 0,-120 | 500×50 |
| EmailLabel | 0,-200 | 200×50 |
| EmailInput | 0,-230 | 580×58 |
| PasswordLabel | 0,-325 | 200×50 |
| PasswordInput | 0,-355 | 580×58 |
| Forgot? | 500,-320 | 200×50 |
| LoginButton | 0,-460 | 580×60 |
| First Time? | 95,-535 | 200×50 |
| Create an account | 240,-535 | 250×50 |

Font is LiberationSans SDF. Heading is black, 48; labels are #333333, 20;
subtitle/secondary text is #666666, 22. Accent/button/link color is #3157FF.
The white right panel and original travel photo on the left are preserved.
Rounded input sprite borders are (30,0,30,0). These fixed positions are the
baseline, not a claim that every window aspect ratio has already been tested.

## Wiring and input findings

EmailInput is TMP_InputField component fileID 1481659382; PasswordInput is
43873812; LoginButton is UnityEngine.UI.Button 286056326. Password masking is
already configured, but both fields originally have characterLimit=0. The login
button's onClick list is empty. Forgot/Create-account are text, not working
buttons. No account controller or custom Button/manager scripts are attached.

Kevin's EventSystem uses InputSystemUIInputModule, component fileID 1359567766.
It must be replaced with **StandaloneInputModule** to match current DuyEdit's
Input Manager (Old) setting. Keep exactly one EventSystem and the existing
GraphicRaycaster. This changes event handling, not the visual design.

## Authentication integration boundaries

Keep Kevin's original login controls and appearance; add behavior, a separately
styled registration panel, feedback, and a small signed-in/logout panel. Place
the lifetime/controller object outside panels that are hidden during navigation.
Use the existing style to make added controls consistent; do not run the old
generated-login builder over Kevin's scene.

At inspection start, AccountDatabase/AccountService still used plaintext LiteDB
`accounts.db`. The Milestone 2 implementation replaces that path with SQLite and
salted PBKDF2 authentication; consult the authentication guide and final validation
report for implemented/verified behavior. Old local LiteDB accounts are not
migrated, opened, or deleted. The dated Duy explanation is historical only.

The original ButtonManager merely logs names; even its Quit case does not exit.
Its global `Button` class name can conflict with Unity's Button type. Excluding
these unused scripts avoids bringing that incomplete routing into the app.
