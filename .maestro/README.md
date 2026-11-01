# Scripted device flows (Slice 162)

Maestro flows for the two MAUI apps: `rtst/` (Read The Stupid Text, `uk.sirous.readthestupidtext`) and `dino/` (Dino Reader, the dev package `uk.sirous.dinoreader.dbg`; the store id `uk.sirous.dinoreader` is never a flow target). One flow is one acceptance check; a failing flow prints the step that failed.

```powershell
tools\emulator\install-debug.ps1 -App rtst     # Debug x86_64 build (see the note on adb install below)
tools\emulator\run-flows.ps1 -App rtst         # every flow of the app, pass/fail only
tools\emulator\run-flows.ps1 -App dino -Flow open-book
```

`run-flows.ps1 -App auto` runs only the apps the change touches (`tools\test-targets.ps1`); `-Instance 1|2` picks the emulator (Slice 180, `tools/emulator/README.md`).

Maestro (2.12) lives in `<worktrees folder>\.tools\maestro` or `$env:MAESTRO_HOME`; `run-flows.ps1` keeps its state under `<worktrees folder>\.tools\maestro-home` and logs under `maestro-out`. Java 21 is the only other need.

## Rules for a flow

- Find elements by `AutomationId` (`id:`), never by text or position. A new screen gets `AutomationId`s on the controls a flow touches. On Android the id is the resource-id `<package>:id/<AutomationId>`.
- Reach the screen under test with a **test hook**, not by tapping through first run. Put the hook in the `launchApp` `arguments` (`cmd: skip-first-run`, `lang: en`); a hook sent mid-flow to the running app is `launchApp: {stopApp: false, arguments: {cmd: state}}` (without `stopApp: false` Maestro restarts the app and the restart can swallow the hook); a deliberate restart needs a `# launchApp-restart: <why>` comment above it. `lint-flows.ps1` enforces this for every launchApp after the first.
- Assert on logcat markers with header comments: `# expect-log: READ_START`. `run-flows.ps1` waits for each text in the `RTST-TEST` log (`adb logcat -s RTST-TEST`) after the flow.
- `tapOn` gets `waitToSettleTimeoutMs: 300`: the voice-pack download animates the screen, and Maestro otherwise waits seconds for it to settle.
- Wait for a page with `extendedWaitUntil` before tapping on it.
- The voice-pack status label has `AutomationId="PackStatusLabel"` (Slice 215): match `id: PackStatusLabel` plus the state's text where the state matters (the fa regex for Downloading or Stopped), never the text alone. A wait for "Downloading" is only safe after a `clearState` (the pack is gone, so the download always starts); say so in a comment above it, and in a flow that keeps the pack wait for Downloading OR the ready state.
- The Slice 183 "keep downloading when locked?" sheet: dismiss it by `android:id/button2`. Assert it (not `optional`) in a flow where it surely shows (the first pack download on a fresh install: `cancelled-voice-pack-stays-cancelled`), optional elsewhere.
- A flow whose UI is not English (a `lang:` hook argument, or a language it picks) asserts by `id:`, or by the strings of that language from its `AppResources.<lang>.resx` (`"Start reading|Begynn å lese"`); an English text wait never matches a Farsi or Norwegian screen. (Slices 212, norsk flow: three flows failed this way.)
- A flow that needs a network state says so in its header (above `---`) with `# needs: cellular-only` (Wi-Fi off, mobile data on); `run-flows.ps1` sets it before that flow and turns Wi-Fi back on after it, even when it fails. A comment alone does nothing. `lint-flows.ps1` rejects unknown `needs` values.
- Every `clearState` has a comment line right above it that starts `# clearState:` and says what it wipes (settings, downloaded voice/OCR packs, own books) and why that is fine for this flow; it also wipes the voice pack. `tools/emulator/lint-flows.ps1` fails the run on a missing comment, on a command name that is not a Maestro command, on a bad escape such as `\+` in a double-quoted string, and on an `appId` that is not a dev package.

## Hooks (Debug builds only)

Commands: `skip-first-run [lang=en]`, `stop-read`, `state` (logs `STATE language=.. reader=.. speed=..`), `delete-translation-model from=.. [to=..]` (Slice 187; waits for the voice pack, then removes only those ML Kit models), `read text=.. [speed=..]` (Slice 165) and, Dino only, `open-book id=.. [page=n] [play=1|page] [speed=..]` (Slice 175; refuses with `failed no book|grown-up|intro|reading open`). They run through `TestHookDispatcher` in `Mobile.Shared/Testing`. Entry points: launch arguments (Android intent extras, iOS user defaults), an Android broadcast, one action per app (`adb shell am broadcast -a uk.sirous.rtst-test.TEST_HOOK --es cmd state`; Dino: `uk.sirous.dino-test.TEST_HOOK`) and the iOS URL scheme (`rtst-test://state`, `dino-test://state`).

Markers: `PAGE n` (Dino page swiped), `READ_START`, `ENGINE_READY ms`, `CHUNK_SYNTH n start|end ms`, `CHUNK n`, `READ_END`, `PACK_READY`, `HOOK <name> ok|unknown`, `STATE ...`. None of this is compiled into Release.

## Slice 185 flows

- `dino/first-run-pack-start`: real first run, Continue starts the voice pack, the app stays on the download panel 12 s later.
- `rtst/language-continue-two-step`: Continue relabels to "Start reading", the second tap opens the reader.
- `rtst/needs-file/open-huge-text-file` (tag `needs-file`): a 36 MB text file from the system file picker must not crash and shows the "text was cut" note. **Precondition:** `adb -s emulator-5554 push big36.txt /sdcard/Download/big36.txt` (36 MB text). It sits in a subfolder because `run-flows.ps1` runs only the `*.yaml` files directly in an app folder, so default runs skip it; run it with `maestro --device emulator-5554 test .maestro/rtst/needs-file/open-huge-text-file.yaml`. It drives the picker by English labels, so it is Android-only and en-US only.

## Not covered yet

Scan's photo fallback (camera), a fake pack-ready hook, Dino's intro wizard and store list. iOS flows are written platform-neutral but run only on a Mac simulator (later step, Actions quota).
