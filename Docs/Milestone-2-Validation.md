# Milestone 2 validation — 2026-09-29

## Scope and baseline

Unity 6000.3.24f1, Windows x64 Mono, uGUI, legacy Input Manager (`activeInputHandler: 0`). Working branch DuyEdit; no commit/push. Milestone 1A/1B pending changes are preserved. Previous suite: 34 passed, including 13 legacy plaintext-account tests. Those obsolete account expectations are replaced by 11 secure SQLite account integration tests; three login-scene tests are added.

Kevin scene import reference: `origin/kevin-frontend-login` at `0ae085f9889591310dc46e6e14b7bd507c8c0ab3`. No branch switch or full merge; only login/image/TMP dependencies imported with original metadata. All 22 original RectTransforms retain their anchorMin, anchorMax, anchoredPosition, sizeDelta and pivot values after preparation. New registration/signed-in objects are separate. Package versions and global input settings were not changed in this milestone; unused InputSystem assembly references were removed, while its package remains installed.

## Evidence

- Preparation compiled and exited successfully: `Logs/auth-prepare.log`, marker `AUTH_SCENE_READY`.
- Unity Test Runner and Windows build results recorded below after execution.

## Review decisions

PBKDF2-SHA256 at 600,000 iterations, 16-byte random salt, 32-byte hash, fixed-time comparison. Per-account algorithm/iterations stored; unsupported/malformed values fail closed. Passwords are 10–128 characters (updated on request after initial milestone validation), case/space-sensitive, no plaintext writes. Email normalization is consistent across registration/login. Parameterized SQL and a unique normalized-email constraint prevent duplicate replacement. Unknown-account login runs a dummy KDF. No remote API or email recovery. AccountService owns a guarded in-memory identity; logout invalidates pending logins. Destroying LoginPage removes listeners and clears its session.

## Limits

Automated UI calls exercise the real controller and buttons but do not establish mouse hit-testing, font appearance or visual layout quality. Rendered appearance and extreme window sizes need the manual guide checks. Original fixed-width login geometry is intentionally retained. Local-account hashing does not encrypt trip data or prevent a computer owner from editing SQLite. Legacy accounts.db remains untouched and is not migrated. Later travel screens are out of scope.

Unity logs contain pre-existing licensing-token refresh and unavailable non-Windows playback-module messages. They are distinguished from compiler/test/build results rather than treated as feature failures.

## Automated test results

- Initial complete run: 35 tests, 34 passed, one UI test cleanup failure (`Logs/auth-tests.xml`). All 11 account security/persistence tests and all other database tests passed. The failure was a temporary filename local being cleared by Unity's Play-mode domain reload after all form assertions passed.
- Corrected the test fixture cleanup to recover its serialized isolated filename and delete the fixture before leaving Play mode. No runtime behavior was changed for this fix.
- Reran the complete affected scene-test class: **3/3 passed** (`Logs/auth-scene-tests.xml`, `Logs/auth-scene-tests.log`).
- Combined current evidence: **35 distinct tests passing** (32 unaffected tests from the full run plus all 3 scene tests from the rerun). This is not represented as a second full-suite execution.
- Scene tests cover original login geometry, all required references, the legacy input module, switching forms, empty-form validation, and removal of button listeners when the controller is destroyed.

## Windows player results

**Build succeeded**, `AUTH_BUILD_PASS` in `Logs/auth-build.log`. Artifact: `Builds/Authentication/TravelPlannerAuthentication.exe` (keep its adjacent Data and native-runtime files when sharing).

The two-launch smoke harness passed using an isolated development fixture:

- First launch: actual UI registration, failed-password feedback, successful login, cleared password field, and logout.
- Second launch: reopened the same database, rejected wrong credentials, authenticated the persisted account, and logged out.
- Logs: `Logs/Authentication/da7051aad6bb4a52905376513e7e1117/register.log` and `reopen.log`.
- Fixture filename: `auth-smoke-da7051aad6bb4a52905376513e7e1117.db` under the app's persistent data directory; normal `travel.db` is not used by this harness.
- Player executable SHA256: `30D4B41A85303ABF83C686F06B75AB1681471C79B9DC7FE530EAD09099FDF63D`.

Overall: **ready with visual-validation limitations** described above. No new compiler errors or unresolved automated test failures. No commit/push; DuyEdit remains active. Stop before Milestone 3.

Measured Windows smoke-flow time on this computer: 5.085 seconds for registration + wrong-password attempt + successful login + logout; 3.486 seconds for reopen + wrong-password attempt + successful login + logout (timers start after database initialization). These are multi-operation totals, not a flight-search benchmark. The slow KDF stays off the UI thread and the form shows a busy message.


## Password-length update

At the user's request, registration and login now allow **10–128 characters**. The prepared scene and scene builder show the same range. Hashing, confirmation and whitespace-only rejection remain unchanged.

Validation: **12/12 account tests passed**, including exact 10/128 registration and login and 9/129 rejection (`Logs/auth-password-tests.xml`). Updated Windows build succeeded (`Logs/auth-password-build.log`). Both actual-player UI checks passed (`Logs/Authentication/1531660465f04400a1a0744c38f3007a/`). Guide and complete-source appendix updated. Branch remains DuyEdit; no commit/push.
