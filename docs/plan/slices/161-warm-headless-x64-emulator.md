# Slice 161 — One warm headless x86_64 emulator, with an x86_64 Debug build

*In progress (wave 2026-10-27), draft PR.* Batch 36 (fast device tests), RTST Mobile and Dino Reader, Android. Decision 165.

**Touches:** `tools/emulator/**` (new: `start-warm.ps1`, `install-debug.ps1`, `stop.ps1`, `README.md`),
`src/AndroidSherpaPackaging.props`, `src/ReadTheStupidText.Mobile/ReadTheStupidText.Mobile.csproj`,
`src/DinoReader.App/DinoReader.App.csproj`, `.github/scripts/check-android-natives.sh` (only if the Debug ABI list
reaches it), `src/ReadTheStupidText.Mobile/CLAUDE.md`, `docs/plan/decisions.md`

## Why

Device tests are too slow (user, 2026-10-26; memory *Device tests too slow*: 10x faster wanted). Grounding on
2026-10-27:

- The AVD `RTST_Test` is **already** `x86_64` (`system-images/android-36/google_apis/x86_64`) and WHPX is installed
  and usable. The slow part is the **app**: Debug builds use `-p:RuntimeIdentifier=android-arm64`
  (`src/ReadTheStupidText.Mobile/CLAUDE.md:124`), so all managed and native code runs under ARM translation.
- The reason is sherpa-onnx: the NuGet package has only an arm64-v8a Android build
  (`src/AndroidSherpaPackaging.props:49`), so an x86_64 app has no neural engine.
- The AVD has `hw.gpu.enabled = no` and `hw.ramSize = 2G` (the host has 32 GB and an i9-11950H).
- Each agent cold-boots its own emulator with a window on the user's screen.

Decided with the user: **try the x86_64 natives from sherpa-onnx's own GitHub release, Debug builds only. If they
don't load, the neural/audio checks go to the real phone (Slice 165)** and the emulator tests everything else on
the phone-voice fallback. Release stays arm64-v8a only (Slice 72).

## Work

- [ ] **Smallest end-to-end first:** `tools/emulator/start-warm.ps1` starts `RTST_Test` with `-no-window
  -no-boot-anim -gpu swiftshader_indirect` (or `host` if it proves stable headless), waits for
  `sys.boot_completed`, and **reuses** a running emulator instead of starting a second one (one per wave). It uses
  quick-boot snapshots, so a later start is a warm resume, never a cold boot. `install-debug.ps1 -App rtst|dino`
  builds Debug with `-p:RuntimeIdentifier=android-x64 -p:EmbedAssembliesIntoApk=true -p:IncludeWindows=false`,
  installs it and launches it. Done when RTST opens on x86_64 and logcat shows its start line with no window shown.
- [ ] AVD tuning in the script (idempotent edit of `config.ini`): RAM 4 GB, GPU on, a fixed `-port` so agents
  address it as `emulator-5554`.
- [ ] Neural natives for x86_64 Debug: check sherpa-onnx 1.13.3's Android release assets for an x86_64
  `libsherpa-onnx-c-api.so` and a matching `libonnxruntime.so` (must stay 1.24.3, see Mobile CLAUDE.md). If they
  exist, download them at build time (Debug and `RuntimeIdentifier=android-x64` only; pinned URL and SHA-256; cached
  under `obj/`, never committed) into `AndroidNativeLibrary Abi="x86_64"`. Check PaddleOCR/onnxruntime OCR the same
  way. If they don't exist or don't load, write it down here and in the rule; the reader falls back to the phone
  voice, as it already does without a pack.
- [ ] `stop.ps1` stops only the emulator this script started (records its pid/port), for the *Release what you
  hold* rule.
- [ ] Both apps: the same scripts install either RTST or Dino Reader.
- [ ] Rule in `src/ReadTheStupidText.Mobile/CLAUDE.md`: emulator testing goes through `tools/emulator/` with the
  x64 Debug build; arm64 is only for the Release smoke test. Point the global `device-test` agent at the scripts
  (ClaudeSkills PR, separate repo).

iOS: no task. This slice is test tooling with no app behaviour, and there is no local Mac; the iOS simulator stays
on CI (Slice 48).

## Verification

- With no emulator running: `start-warm.ps1` boots it with no window; a second call returns at once and reuses it.
- After `stop.ps1` and a new `start-warm.ps1`, boot to `sys.boot_completed` takes under 20 s (warm resume).
- `install-debug.ps1 -App rtst` and `-App dino`: each app launches; `adb shell getprop ro.product.cpu.abi` is
  `x86_64` and the APK has `lib/x86_64/`.
- If the x86_64 natives were found: a neural read plays on the emulator. Otherwise the phone voice plays and this
  file says why.
- Time the old arm64 path against the new one for the same launch-and-read, and record both times here.
