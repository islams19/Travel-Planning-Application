# Milestone 7 — Price tracking and notifications

## Scope and status

M7 passed **137/137 automated tests**, a Windows x64 development build and two
Windows player checks. See the [validation record](Milestone-7-Validation.md)
for evidence and remaining manual/release-build limitations, and
[full scripts](Milestone-7-Full-Scripts.md) for the complete source snapshot.
Stop after M7 before M8 polish and testing.

Signed-in users track flights and hotels, view an unread notification badge,
open their notification list and mark notifications read. Price tracking is
independent of Saved trips: saving an item does not automatically track it.
Restaurants, experiences and hotspots are not tracked in this milestone.

All prices and notifications belong to the local preset-data demonstration.
There are no live travel APIs, operating-system alerts, emails or purchases.
No database migration or reset is required; existing accounts and trips remain.

## How tracking behaves

- Starting a watch records the current catalog price as its baseline.
- Stopping sets the watch inactive and keeps its old notifications.
- Tracking again reuses the watch and starts from the current price. It does
  not create alerts for price changes that happened while tracking was stopped.
- An actual increase or decrease creates a notification for every active local
  account watching that target. Only the signed-in user's notices are shown.
- Logged-out users receive their persisted notices when they next log in.
- Marking a notice read changes its unread state, not the catalog price.
- Badges refresh on login, opening the panel and explicit actions/refresh.
  There is no background polling or automatic detection of external SQL edits.

## Demo-only price changes

The price-change control is explicitly **DEMO ONLY**, available in the Editor
and development builds. It changes the fictional shared catalog on **this
Windows installation**, not a private quote for the signed-in user. Use it only
when you intend to change that local sample price. Ordinary release builds must
not expose the simulator.

DemoPriceService writes the new price, one history entry, notices for all active
watchers and their new baselines in one transaction. A transaction means those
related writes succeed together or are rolled back together. Applying the same
price does nothing. Use the app's simulator for demonstrations: arbitrary edits
with an external SQLite tool do not generate notifications.

Prices are integer USD cents in SQLite. Display units still matter: a flight
fare differs from a hotel room/night. A watch is not a price lock, reservation
or booking. Closing a panel cannot undo a price change already committed.

## When displayed prices refresh

The tracking panel loads the selected target's current price. After a demo
change, existing flight/hub cards may still display their earlier cached price.
Rerun the flight search or reload the destination to refresh them. Saved trips
shows current prices when reopened/reloaded. This preserves the user's existing
search filters, city and scroll state rather than resetting screens silently.

## Existing screen boundaries

**Original login form:** LoginPage only gains its isolated development smoke
flag. Kevin's original login positions/anchors remain the baseline. The separate
signed-in card gains a tracking/notifications entry.

**Flight and hotel cards:** add Track price alongside existing actions, keeping
Save to trip and View reviews. The setup upgrades existing prefabs. Other place
categories keep their current actions without a tracking button.

PriceTrackingCanvas is a separate modal at overlay order 6. It closes Reviews
and Saved trips first, then temporarily disables underlying screen interaction.
Closing restores prior interaction and screen state. The controller captures
the signed-in owner ID and ignores results from canceled requests or an old
session. Badge/list/selection state must clear on logout.

## Editor step 1 — Prepare

1. Open the project in **Unity 6000.3.24f1** on **DuyEdit**.
2. Stop Play mode and save unrelated scene work before using setup menus.
3. Choose **Travel Planning > Price Tracking > 1 - Prepare Price Tracking**.
4. Open **Assets > Scenes > Login** and resolve red Console errors. Do not
   replace the login form, rebuild the seed or delete your personal database.
5. Check the Inspector wiring below. Compilation/preparation alone is not a
   successful test or build.

## Editor step 2 — Inspector references

TrackingSetup creates this hierarchy in Login.unity:

```text
PriceTrackingController (PriceTrackingPage, NotificationBadge, TrackingSmokeRunner)
PriceTrackingCanvas
  InputBlocker
  Main
    Header/{Title,CloseButton}
    TargetPreview
    WatchSelection/{Label,Dropdown,RefreshButton}
    TargetActions/{TrackButton,ReadAllButton}
    DemoPanel
      Title
      Controls/{PriceInput,ApplyButton}
      Feedback
    Status
    UnreadSummary
    ScrollView/Viewport/Content
```

Entry buttons are **NotificationsButton** under
`Canvas/Right Panel/SignedInCard`, `FlightCanvas/Main/Header` and
`DestinationCanvas/Main/Header`. Each has a child **UnreadBadge**. On the
signed-in card, Notifications is at y = -540 and Logout moves to y = -620;
the original login form is unaffected. The Canvas uses Scale With Screen Size,
reference 1920 × 1080, Match 0 and overlay order 6.

Select **PriceTrackingController**. Its **Price Tracking Page** component needs:

| Inspector field | Assignment |
| --- | --- |
| Account / Flights / Hub | Existing LoginPage, FlightSearchPage and DestinationHubPage controllers |
| Reviews / Trips | Existing ReviewsPage and SavedTripsPage controllers |
| Badge | NotificationBadge component |
| Modal Canvas / Demo Panel | PriceTrackingCanvas and its demo controls panel |
| Underlying Groups | Three CanvasGroups on the auth, flight and destination Canvases |
| Home Button / Flight Button / Hub Button | Three tracking entry buttons |
| Close Button / Refresh Button / Track Button | The panel's navigation and tracking controls |
| Read All Button / Apply Button | Mark-all-read and demo-price application controls |
| Watch Choice | TMP dropdown selecting an existing watch |
| Demo Price | TMP input field for a fictional USD price |
| Target Preview / Status / Unread Summary | Corresponding panel TMP labels |
| Track Label / Demo Feedback | Tracking button text and demo validation/status label |
| Scroll | PriceTrackingCanvas/Main/ScrollView's ScrollRect |
| Row Prefab | Assets/TravelPlanning/Prefabs/Tracking/PriceNoticeRow.prefab |

**Notification Badge** needs Account plus exactly three TMP labels, one per
entry. A zero count hides the badge. `?` means the count is loading or could not
be read; it must not be interpreted as zero. Use explicit Refresh to try again.
Counts above 99 display as **99+** so the small entry badge stays readable.

Each **Price Notice Row** needs Title, Body, Timestamp, Action Label and Read
Button. A read notice displays **Read** with its action disabled; an unread one
offers **Mark read**. Preserve script-installed button listeners rather than
adding duplicate On Click entries in the Inspector.
The row prefab contains `Top/{Title,ReadButton}`, `Body` and `Timestamp`;
Action Label points to `Top/ReadButton/Label`.

Inspect `Assets/TravelPlanning/Prefabs/Flights/FlightResultRow.prefab`: its
Track Button must reference `SaveActions/TrackPriceButton`. In
`Assets/TravelPlanning/Prefabs/Destinations/PlaceCard.prefab`, the field points
to `ReviewActions/TrackPriceButton`; runtime hides it for non-hotels. Setup
also repairs existing underlying CanvasGroup references when rerun.

Runtime files are `Runtime/Tracking/{TrackingModels,TrackingCatalog,
PriceTrackingService,DemoPriceService}.cs`. UI files include NotificationBadge,
PriceTrackingPage, PriceNoticeRow and the isolated TrackingSmokeRunner under
`UI/Tracking`. `UI/Editor/TrackingSetup.cs` owns preparation/build wiring.

## Editor step 3 — Basic Play mode demonstration

1. Press **Play**, log in and run the default JFK–London search for June 15, 2027.
2. Choose **Track price** on one flight. Verify the panel loads that flight's
   current price, then start tracking it.
3. Open the demo control, enter a different valid USD price and apply it once.
   Verify the price change, a new notification and the unread badge.
4. Apply exactly that same price again. Confirm no additional notification.
5. Mark the notice read and confirm the badge updates. Close and reopen the
   panel to check the read state is saved.
6. Explore London and repeat with a hotel. Confirm the unit is room/night and
   that restaurant, experience and hotspot cards have no tracking action.
7. Stop tracking, make another demo change, and check no new notice is created
   for the stopped watch. Track again and confirm the current baseline; earlier
   changes must not suddenly appear as new alerts.
8. Rerun flight search/reload the hub to refresh old card prices. Open a saved
   trip containing the item to confirm its displayed price follows the catalog.

## Editor step 4 — Persistence and account isolation

1. Track the same flight or hotel with two different local accounts.
2. With one account logged in, apply a demo change. Verify only its notices are
   visible. Log into the other account and verify it has its own persisted notice.
3. Mark a notice read in one account. The other account's unread count must not
   change.
4. Stop and restart Play mode, then log in again. Watches, notices and read state
   should persist. Repeat in two actual launches of the Windows executable.
5. Log out during a load and reopen with another account; old rows, badge counts
   or target selections must not reappear.
6. Test invalid prices and repeat clicks. Expect friendly validation, no duplicate
   events and responsive UI. Use isolated automated fixtures for direct ownership
   and rollback tests, not manual corruption of your personal database.
7. Check keyboard/mouse interaction, long labels and resizing. Confirm closing
   tracking leaves the previous screen usable, including after Saved trips or
   Reviews was open.

## Editor step 5 — Tests and Windows

1. Choose **Window > General > Test Runner > EditMode > Run All** and retain all
   earlier milestone regression tests.
2. Choose **Travel Planning > Price Tracking > 2 - Build Windows x64**.
3. After a successful build, run
   `Builds/PriceTracking/TravelPlannerTracking.exe` and repeat the manual checks.
4. From the project root in PowerShell, run the isolated harness after building:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\tools\Test-Tracking.ps1
   ```

5. Automated callback/layout checks do not establish rendered appearance,
   physical input or desktop resizing. Record those manual limits separately.

## Common mistakes

| Symptom | Check |
| --- | --- |
| Missing menu or script cannot attach | Wait for compilation; fix Console errors and matching class/file names. |
| Old cards have no Track price button | Run the setup so it upgrades existing prefabs. |
| NullReferenceException or frozen underlying controls | Check generated Inspector fields and CanvasGroups; rerun preparation to repair wiring. |
| A stopped watch loses old notices | Soft-stop with active=0; deleting the watch cascades its notices. |
| Retracking causes a unique-key error | Reuse the existing watch; inactive rows still participate in uniqueness. |
| Price changes but old card text does not | Refresh the flight search/hub; those cards retain cached values. |
| Editing SQLite creates no alert | Only the central demo-change transaction creates notifications; there is no external-edit scanner. |
| Logged-out account sees nothing immediately | Its notices persist and appear after login; there are no OS alerts. |
| One account's read action affects another | Filter every read-state mutation by captured user ID. |
| Same-price apply creates an event | Treat it as a no-op; history requires different old/new prices. |
| Demo control absent in a release build | Expected: the simulator is Editor/development-only. |

## Five-person ownership

- Teammate 1: owner-bound tracking/notification service and models.
- Teammate 2: demo price transaction and multi-account event tests.
- Teammate 3: tracking modal, badge and shared scene/prefab wiring.
- Teammate 4: UI cancellation, interaction restoration and persistence tests.
- Teammate 5: Windows validation, manual checks and documentation.

Coordinate edits to Login.unity and shared prefabs through the scene owner.
Keep `.meta` files with assets and builds/logs out of commits. Make project
commits only on **DuyEdit**. Stop after this milestone before M8 polish/testing.
