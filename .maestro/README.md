# Scripted device flows (Slice 162)

Maestro flows for the two MAUI apps: `rtst/` (Read The Stupid Text, `uk.sirous.readthestupidtext`) and `dino/` (Dino Reader, `uk.sirous.dinoreader`). One flow is one acceptance check; a failing flow prints the step that failed.

```powershell
tools\emulator\install-debug.ps1 -App rtst     # Debug x86_64 build (see the note on adb install below)
tools\emulator\run-flows.ps1 -App rtst         # every flow of the app, pass/fail only
tools\emulator\run-flows.ps1 -App dino -Flow open-book
```

Maestro (2.12) lives in `<worktrees folder>\.tools\maestro` or `$env:MAESTRO_HOME`; `run-flows.ps1` keeps its state under `<worktrees folder>\.tools\maestro-home` and logs under `maestro-out`. Java 21 is the only other need.

## Rules for a flow

- Find elements by `AutomationId` (`id:`), never by text or position. A new screen gets `AutomationId`s on the controls a flow touches. On Android the id is the resource-id `<package>:id/<AutomationId>`.
- Reach the screen under test with a **test hook**, not by tapping through first run. Put the hook in the `launchApp` `arguments` (`cmd: skip-first-run`, `lang: en`); a relaunch without `clearState` keeps the data, so `launchApp: {arguments: {cmd: state}}` reports state mid-flow.
- Assert on logcat markers with header comments: `# expect-log: READ_START`. `run-flows.ps1` waits for each text in the `RTST-TEST` log (`adb logcat -s RTST-TEST`) after the flow.
- `tapOn` gets `waitToSettleTimeoutMs: 300`: the voice-pack download animates the screen, and Maestro otherwise waits seconds for it to settle.
- Wait for a page with `extendedWaitUntil` before tapping on it.

## Hooks (Debug builds only)

Commands: `skip-first-run [lang=en]`, `stop-read`, `state` (logs `STATE language=.. reader=.. speed=..`), `read text=.. [speed=..]` (Slice 165) and, Dino only, `open-book id=.. [page=n] [play=1|page] [speed=..]` (Slice 175; refuses with `failed no book|grown-up|intro|reading open`). They run through `TestHookDispatcher` in `Mobile.Shared/Testing`. Entry points: launch arguments (Android intent extras, iOS user defaults), an Android broadcast, one action per app (`adb shell am broadcast -a uk.sirous.rtst-test.TEST_HOOK --es cmd state`; Dino: `uk.sirous.dino-test.TEST_HOOK`) and the iOS URL scheme (`rtst-test://state`, `dino-test://state`).

Markers: `PAGE n` (Dino page swiped), `READ_START`, `ENGINE_READY ms`, `CHUNK_SYNTH n start|end ms`, `CHUNK n`, `READ_END`, `PACK_READY`, `HOOK <name> ok|unknown`, `STATE ...`. None of this is compiled into Release.

## Not covered yet

A document open (system file picker), Scan's photo fallback (camera), a fake pack-ready hook, Dino's intro wizard and store list. iOS flows are written platform-neutral but run only on a Mac simulator (later step, Actions quota).
