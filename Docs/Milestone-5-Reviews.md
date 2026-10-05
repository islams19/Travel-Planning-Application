# Milestone 5 — Ratings and traveler reviews

## Scope and status

This guide describes the Reviews implementation and manual checks. The full
suite passed **115/115 tests**, and two Windows player scenarios passed. See
the [validation record](Milestone-5-Validation.md) for evidence and remaining
manual limitations, and [full scripts](Milestone-5-Full-Scripts.md) for the
complete source snapshot. Stop after this milestone; Milestone 6 is saved trips.

Signed-in users select **View reviews** on any hotel, restaurant, experience or
hotspot card. A separate Canvas overlays the destination hub without resetting
its selected city or scroll position. Closing reviews returns to that same hub.
Reviews are read from the local SQLite catalog; this milestone does not allow
writing reviews or downloading reviews from Google.

Each row shows the full traveler name, full body, five visual stars, a numeric
rating and its demo label. The heading shows the average and review count.
An empty list says **No reviews yet**. A failed load offers **Retry**.

## Existing files touched

**Existing login page:** only LoginPage's development smoke-test isolation guard
is extended. Kevin's original form geometry, fonts and colors remain the
baseline. Authentication and the 10–128 character password policy are unchanged.

**Existing destination UI:** PlaceCard gains a View reviews button; PlaceSection
passes its callback; DestinationHubPage publishes `ReviewRequested` and
`ContentCleared`. Setup upgrades the existing place-card prefab, so its height
can accommodate the button. Other hub controls keep their existing purpose.
No SQLite schema migration or runtime database deletion is required.

## Files and responsibilities

| File under Assets/TravelPlanning | One job |
| --- | --- |
| Runtime/Reviews/ReviewModels.cs | Store one review and a loaded result. |
| Runtime/Reviews/ReviewService.cs | Read the selected place and its reviews. |
| Runtime/Reviews/GoogleMapsReference.cs | Validate and normalize a Maps search reference. |
| UI/Reviews/ReviewsPage.cs | Load, close, retry and navigate the overlay. |
| UI/Reviews/ReviewRow.cs | Fill one review row. |
| UI/Reviews/StarGraphic.cs | Draw one star without a font glyph or image dependency. |
| UI/Reviews/ReviewSmokeRunner.cs | Drive isolated development-player checks. |
| UI/Editor/ReviewSetup.cs | Prepare scene/prefabs and build the Windows player. |

`await` means the UI lets the database worker finish before using its result;
Unity keeps processing frames while it waits. `TravelDatabase.ExecuteAsync`
queues work off the main thread. The service chooses a SQL column from the four
known categories and sends the place ID as a parameter, rather than joining
user text into SQL. Closing, logging out or changing hub content cancels the
request and invalidates old results.

## What the Google button means

**Google Maps search** opens the default browser only when clicked. It searches
for the venue; it does not promise a verified direct Google reviews page.
The local sample reviews are not imported from Google. Rows marked `is_demo`
explicitly say **Fictional demo review — not a Google review**.

The button is enabled only for a validated HTTPS Google Maps search reference.
The validator permits exact Google hosts, the search path, `api=1` and one
nonempty `query`; it rejects credentials, extra/duplicate parameters, unexpected
ports, fragments, malformed encoding and non-web links. It creates a canonical
URL and checks again on click. An unavailable link disables the button; a browser
launch failure leaves the user on the review screen with a friendly message.
See Unity 6.3's [Application.OpenURL documentation](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application.OpenURL.html).

## Editor step 1 — Prepare the screen

1. Open this project in **Unity 6000.3.24f1** and remain on Git branch **DuyEdit**.
2. Stop Play mode and save any unrelated scene work before running a setup menu.
3. Choose **Travel Planning > Reviews > 1 - Prepare Reviews**. Let compilation
   finish first if this menu is not yet visible.
4. Open **Assets > Scenes > Login** if it is not already open. Setup uses that
   existing scene and wires its references; do not build a second login form.
5. Check the Console for errors. Preparation is not evidence that Play mode or
   a Windows build has passed.

## Editor step 2 — Check Inspector references

The generated hierarchy is:

```text
ReviewsController (ReviewsPage, ReviewSmokeRunner)
ReviewsCanvas
  InputBlocker
  Main
    Header
      PlaceName
      CloseButton
    Summary
    DemoNote
    Status
    Actions
      GoogleMapsButton
      RetryButton
    ScrollView
      Viewport
        Content
```

ReviewsCanvas uses Screen Space Overlay, sorting order 4 and a Canvas Scaler
with Scale With Screen Size, reference resolution 1920 × 1080 and Match 0.
InputBlocker catches clicks so they do not reach hub controls underneath.
The overlay uses an opaque white surface, LiberationSans and the #3157FF
accent. Keyboard focus starts on Close reviews; explicit button navigation
keeps navigation within the overlay.
Do not deactivate the independent ReviewsController when hiding the Canvas.

Select **ReviewsController**. Its **Reviews Page** component must have all of
these assigned by setup:

| Inspector field | Required object/component |
| --- | --- |
| Account | AccountController's LoginPage |
| Hub | DestinationController's DestinationHubPage |
| Overlay Canvas | ReviewsCanvas |
| Place Name | ReviewsCanvas/Main/Header/PlaceName |
| Summary / Status | ReviewsCanvas/Main/Summary and Status |
| Close Button | ReviewsCanvas/Main/Header/CloseButton |
| Retry Button / Maps Button | ReviewsCanvas/Main/Actions/RetryButton and GoogleMapsButton |
| Scroll | ReviewsCanvas/Main/ScrollView's ScrollRect |
| Row Prefab | Assets/TravelPlanning/Prefabs/Reviews/ReviewRow.prefab |

The ReviewRow component needs traveler name, numeric rating, demo label, body
and an array of exactly five StarGraphic components. Each StarGraphic draws a
five-point star using ten alternating outer/inner vertices. Filled stars use
the app's blue accent; the remainder use gray. The numeric rating remains
readable without relying on color. The prefab hierarchy is TravelerName,
Rating/{Star1, Star2, Star3, Star4, Star5, RatingText}, DemoLabel and Body.
Body wraps and the row grows to its preferred height instead of cutting off
long reviews.

Select **Assets/TravelPlanning/Prefabs/Destinations/PlaceCard.prefab** and verify
**View Reviews Button** on its PlaceCard component points to
**ReviewActions/ViewReviewsButton**. Its preferred height becomes 292.
Do not add manual button On Click handlers on
top of the script wiring, which would invoke an action twice.

## Editor step 3 — Test in Play mode

1. Press **Play**, register or log in, then open **Explore destination**.
2. Choose a city and scroll to a hotel. Click **View reviews**; verify the venue
   name, average/count, full review bodies, five stars and numeric ratings.
3. Close reviews. Confirm the same city and hub scroll position remain.
4. Repeat for a restaurant, experience and hotspot; verify reviews belong to
   the selected venue, rather than the last one opened.
5. Click **Google Maps search** once. Confirm the system browser searches for
   that venue and the app still shows its local demo-review disclaimer.
6. Navigate back to flights and confirm the prior search controls/results are
   retained. Log out and log in again; an old review panel must not reappear.
7. Resize the Game window through wide and smaller desktop proportions. Check
   review wrapping, vertical scrolling, buttons and the underlying login form.
8. Stop Play mode. Empty/error/cancellation and rejected-link cases should use
   isolated automated fixtures, not destructive edits to your personal database.

## Editor step 4 — Build and regression checks

1. Choose **Window > General > Test Runner > EditMode > Run All**. Review failures
   before continuing; retain the earlier authentication, flight and hub checks.
2. Choose **Travel Planning > Reviews > 2 - Build Windows x64**.
3. After a successful build, run
   `Builds/Reviews/TravelPlannerReviews.exe` and repeat the manual workflow.
4. From the project root in PowerShell, run the two isolated player checks:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\tools\Test-Reviews.ps1
   ```

   These checks use a fake browser opener and headless players. See the current
   [validation record](Milestone-5-Validation.md) for exact results and log paths.
5. A successful automated test does not prove rendering, physical mouse input,
   window resizing or an actual browser launch. Keep those manual checks visible
   in the validation record.

## Common mistakes

| Symptom | Check |
| --- | --- |
| Review menu missing | Resolve red Console compile errors and wait for compilation. |
| View reviews missing on existing cards | Run Reviews preparation so it upgrades the existing prefab. |
| Missing-reference error / NullReferenceException | Check every Reviews Page and Review Row field; rerun setup for generated assets. |
| Square boxes instead of stars | Use StarGraphic components; do not paste a star character into the existing font. |
| Wrong venue's reviews after navigation | Pass category plus ID and retain cancellation/revision checks. |
| Tags in a review change formatting | Disable TMP rich text for database-sourced labels/bodies. |
| Google button disabled | The reference is absent or fails strict search-URL validation; do not bypass the validator. |
| Empty results confused with a failure | Empty reviews are normal; show No reviews yet. A load failure offers Retry. |
| New scripts fail to attach | Match the C# class name to its file name and fix compile errors first. |

## Five-person ownership

- Teammate 1: review models/service and typed query tests.
- Teammate 2: ReviewRow prefab and StarGraphic presentation.
- Teammate 3: ReviewsPage behavior and scene setup; owns scene/prefab wiring.
- Teammate 4: Google reference validation and rejected-link tests.
- Teammate 5: isolated smoke tests, Windows checks and documentation.

Coordinate changes to Login.unity and the shared PlaceCard prefab through the
scene owner. Keep `.meta` files with assets, keep generated builds/logs out of
commits, and make project commits only on **DuyEdit** as requested.
