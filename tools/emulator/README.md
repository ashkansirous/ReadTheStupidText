# Warm headless emulator (Slice 161)

One `RTST_Test` x86_64 emulator, no window, fixed port (`emulator-5554`), reused across a wave.

- `start-warm.ps1` boots it (`-no-window -no-boot-anim -no-audio -gpu swiftshader_indirect -memory 4096`), or returns at once if it is already up. Every start is a cold boot (~45 s) with `-no-snapshot-load -no-snapshot-save`: snapshots are off because a resumed one carried stale state and broke Maestro flows (Slice 162). Boot once per session and keep it running between runs; a second `start-warm.ps1` reuses it. RAM and GPU are flags, so the AVD's `config.ini` is never edited.
- `install-debug.ps1 -App rtst|dino` builds an `android-x64` Debug APK (embedded assemblies), installs and launches it. The x86_64 sherpa-onnx natives are fetched by `src/AndroidSherpaPackaging.props` on first build.
- `stop.ps1` stops only the emulator `start-warm.ps1` started (pid in `<worktrees folder>/.emulator/state.json`, shared by all worktrees; a machine-wide mutex serialises start and stop).

## Slice 180: one build per commit, two emulators, only the touched apps

- `..\test-targets.ps1` prints which of `rtst-android`, `rtst-windows`, `dino-android`, `dino-windows` a change needs (CI's own file mapping, `ship-targets.mjs --local`, plus the test tooling's files; a tooling file it cannot place runs everything). `run-flows.ps1 -App auto` runs only those and lists the skipped ones.
- `install-debug.ps1` builds the APK once per **build key** (hash of `src/` and the root build files as on disk) into `<worktrees folder>/_builds/<app>-android-<key>/` and reuses it (any agent, any worktree); `-Rebuild` forces a build; the emulator boots while the build runs. The newest 3 builds per app and worktree are kept. `build-cache.ps1` holds the key logic.
- Two emulators: `new-avd.ps1` creates `RTST_Test2` by copying only `config.ini` (it does not copy RTST_Test's 16 GB disk; the emulator makes small disk files on first boot, which took 101 s under load); every script takes `-Instance 1|2` (`emulator-5554` / `emulator-5556`, own state file and lock); `start-warm.ps1 -Instance any` takes a free one and prints which. A running instance counts as taken: stop it (`stop.ps1 -Instance n`) when done. Maestro output is per instance.
- `activity.ps1`: builds, flows runs and VM runs register while they run; `tools/phone/audio-timing.ps1` takes the quiet-machine lock first, waits for them to end, and holds new ones off until it finishes.

Always address it with `adb -s emulator-5554`; a real phone may also be connected.
- **Load limit** (measured 2026-10-28, i9-11950H, 32 GB): one emulator + the VM is fine; two emulators with flows and no VM is fine (2/2 each); **two emulators + the VM is too much** (CPU 100%, RAM 30.7 of 32 GB: one flows run and all three VM checks failed). `activity.ps1` enforces it: a VM run waits (and prints why) while two emulators run flows, and a second emulator's flows wait while the VM runs. Never start a flows run while the other emulator is still booting (Maestro: "Device emulator-5554 was requested, but it is not connected").
- **Builds run at BelowNormal priority** (`Set-BuildPriority`; dotnet and msbuild children inherit it), because flows failed 0/2 while an MSIX build held the CPU at 100%.
- **System UI "isn't responding"**: `run-flows.ps1` looks for an ANR dialog before and after the flows, taps *Wait* (or *Close app*) and prints that it did; if it cannot clear it, it fails naming the dialog. Before this, one ANR failed every later run until the emulator restarted.
- `-Instance 2` works in every script (the chosen number is `$script:EmuInstance`; it used to overwrite the caller's `$Instance`). Each flows run gets its own Maestro `user.home` per instance.
