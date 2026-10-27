# Warm headless emulator (Slice 161)

One `RTST_Test` x86_64 emulator, no window, fixed port (`emulator-5554`), reused across a wave.

- `start-warm.ps1` boots it (`-no-window -no-boot-anim -no-audio -gpu swiftshader_indirect -memory 4096`), or returns at once if it is already up. Every start is a cold boot (~45 s) with `-no-snapshot-load -no-snapshot-save`: snapshots are off because a resumed one carried stale state and broke Maestro flows (Slice 162). Boot once per session and keep it running between runs; a second `start-warm.ps1` reuses it. RAM and GPU are flags, so the AVD's `config.ini` is never edited.
- `install-debug.ps1 -App rtst|dino` builds an `android-x64` Debug APK (embedded assemblies), installs and launches it. The x86_64 sherpa-onnx natives are fetched by `src/AndroidSherpaPackaging.props` on first build.
- `stop.ps1` stops only the emulator `start-warm.ps1` started (pid in `<worktrees folder>/.emulator/state.json`, shared by all worktrees; a machine-wide mutex serialises start and stop).

Always address it with `adb -s emulator-5554`; a real phone may also be connected.
