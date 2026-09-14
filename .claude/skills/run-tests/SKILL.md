---
name: run-tests
description: How Till Winter tests are run and read (EditMode suite and play-mode smoke test) without flooding the context.
---

# Running tests

- Always through the `test-runner` subagent: "run the tests" or "run the tests and the smoke test".
- The subagent uses `run-tests-summary.bat` / `smoke-test-summary.bat`. A PreToolUse hook rewrites
  the raw `run-tests.bat` / `smoke-test.bat` to the summary versions, so calling them directly is harmless but pointless.
- Both need the project closed in the Unity editor. "Unity could not start" in the summary means it is open.
- Summary format: `ERROR file:line CSxxxx message` lines (compile errors), `Tests: P passed, F failed, T total`,
  then `FAIL <test name>` with up to 5 message lines each, capped at 100 lines. Exit code 0 pass, 2 failures, 3 Unity error.
- Fix compile errors first; they hide every test result.
- Smoke summary: `Smoke: P checks passed, F failed, C console errors`, the FAIL/CONSOLE lines, the `Done.` line.
  Open a screenshot (`TestResults/smoke/NN-name.png`) only for a named failed check, and only that one.
- The raw scripts and full logs (`TestResults/EditMode.log`, `TestResults/smoke/smoke.log`) are for the developer in a terminal, not for the session.
- Every numbered rule in `docs/GDD.md` has a test in `Assets/TillWinter/Tests`; a rule change means a test change and a GDD edit.
