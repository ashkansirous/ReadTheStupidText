# CLAUDE.md — Dino Reader Debug test hooks

Loaded when you work under `src/DinoReader.App/Testing`. App-wide rules: `../CLAUDE.md`.

- Debug hooks `import-test-book id=<stem|all>` and `test-store mode=ok|slow|stall|offline|off` (`TestStore*.cs`; `StoreClient` reads the fixture list and `testbook:` addresses) give a flow books and a picker without the live store; `skip-first-run` puts the four test books on the shelf; `shelf-audio lang=sv` starts a run against the test store's one pack. (Slice 261: docs/plan/slices/261-dino-no-starters-first-run-book-picker.md)
- Maestro flow expect-log markers must be written with `TestHookLog.Write` (RTST-TEST tag), not `Debug.WriteLine`. (Slice 228: docs/plan/slices/228-dino-grownups-corner-clarity.md)
