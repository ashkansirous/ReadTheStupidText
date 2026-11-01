# Emulator: one-off Mono SIGSEGV in Dino's dev build (2026-11-01 21:46)

An excerpt (the header and the named frames) copied by the final feedback loop of night 2026-11-01-0859 from the Maestro run folder
`D:\ReadTheStupidText-worktrees\.tools\maestro-out\i1\.maestro\tests\2026-11-01_214608\Record page shows the words\logs\crash-report.txt`
(a rolling folder, so it would have been lost). Tracked in [Slice 247](../slices/247-final-loop-leftovers-night-2026-11-01-0859.md) item c.
The flow was "Record page shows the words", the last flow of a Slice 241 emulator run; the rerun passed.
The process was 6 s old, after a `clearState` cold start. The crash was on a thread-pool thread, during
assembly loading (`mono_assembly_request_byname` from `mono_class_from_typeref_checked`). Cause not known.

```
11-01 21:46:26.238 F/libc    ( 4593): Fatal signal 11 (SIGSEGV), code 1 (SEGV_MAPERR), fault addr 0x100000005 in tid 4612 (.NET TP Worker), pid 4593 (.dinoreader.dbg)
Build fingerprint: 'google/sdk_gphone64_x86_64/emu64xa:16/BE2A.250530.026.F3/13894323:userdebug/dev-keys'
ABI: 'x86_64'
Process uptime: 6s
Cmdline: uk.sirous.dinoreader.dbg
pid: 4593, tid: 4612, name: .NET TP Worker  >>> uk.sirous.dinoreader.dbg <<<
signal 11 (SIGSEGV), code 1 (SEGV_MAPERR), fault addr 0x0000000100000005
backtrace (25 frames; libmonosgen-2.0.so BuildId 5da5c145fc7fa7ce8afbbdad2d3fa1638294a919):
  #00 libc.so (strcasecmp+26)
  #01-#03 libmonosgen-2.0.so (no symbol)
  #04 libmonosgen-2.0.so (mono_assembly_request_byname+493)
  #05 libmonosgen-2.0.so (mono_assembly_load_reference+555)
  #06 libmonosgen-2.0.so (mono_class_from_typeref_checked+506)
  #07 libmonosgen-2.0.so (mono_class_get_checked+183)
  #09 libmonosgen-2.0.so (mono_class_get_checked+76)
  #11 libmonosgen-2.0.so (mono_class_from_typeref_checked+401)
  #21 libmonosgen-2.0.so (mono_runtime_invoke_checked+134)
```
