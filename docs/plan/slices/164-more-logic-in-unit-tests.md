# Slice 164 — Move more logic out of UI code into unit tests

*In progress (wave 2026-10-27), draft PR.* Batch 36 (fast device tests), RTST (Windows and Mobile) and Dino Reader. Decision 165.

**Touches:** decided by the audit (first task), expected: `src/ReadTheStupidText.App/**/*.xaml.cs`,
`src/ReadTheStupidText.Windows.Tray/**`, `src/ReadTheStupidText.Mobile/**/*.xaml.cs`,
`src/DinoReader.App/**/*.xaml.cs`, `src/ReadTheStupidText.Application/**`, `tests/ReadTheStupidText.Tests/**`,
`tests/DinoReader.Tests/**`. Rewrite the Touches line once the audit lists the files.

## Why

Every decision that lives in a code-behind or a platform class can only be checked on a device or the desktop,
which is the slow path. Pure logic in Application/Domain/Core is checked by `dotnet test` in seconds
(the root CLAUDE.md, *Tests*).

## Work

- [ ] **Audit first** (a `ground-in-code` pass): list the branching logic in code-behinds, `Windows.Tray`, and the
  Android/iOS platform classes that has no test. For example: which read path to use, when a control is enabled,
  pack state to UI state, hotkey and selection rules. Rank the items by how often a device test has caught a bug
  there (slice files' review notes). Write the list into this file and narrow **Touches**.
- [ ] For each item on the list, top first, in its own sub-PR (so it can run beside other work): move the decision
  into a pure class in Application (or Core for Dino) and keep the UI code as a thin caller. Then add xUnit tests,
  and delete the device-test step it replaces from the slice checklists that named it.
- [ ] Stop when what's left needs a real device (audio, OS integration, layout).

## Verification

- `dotnet test` for both suites passes, with the new tests named in this file.
- The device checklists in Slices 161-163 shrink by the steps moved into unit tests (listed here).
- No behaviour change: the Slice 162 flows and the Slice 163 checks still pass.
