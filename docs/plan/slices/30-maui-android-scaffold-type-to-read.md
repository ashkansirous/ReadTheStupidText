# Slice 30 — MAUI Android scaffold + type-to-read (smallest E2E)

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 30 — MAUI Android scaffold + type-to-read (smallest E2E).**
      (Decision 37, 38, 43 — Screen 1) Scaffold `src/ReadTheStupidText.Mobile`
      (MAUI, `net10.0-android`), referencing the existing
      `ReadTheStupidText.Domain` and `ReadTheStupidText.Application` projects
      unchanged. Builds Screen 1 ("Type or paste"): brand app-bar, editor card,
      and a transport card (play/pause, skip ±10s, progress bar + elapsed/total
      timer, six speed presets — no hidden slider on mobile), plus the bottom nav
      shell (Type · Camera · File tabs, Camera/File as stubs until Slices 32/33).
      TTS engine for this slice is Android's built-in `TextToSpeech` (the neural
      voice is ported in Slice 31) via an `ISpeechReader` implementation in
      `Platforms/Android`. Add the project to `ReadTheStupidText.slnx`. This
      proves the whole DI/Domain/Application reuse story end-to-end before any
      mobile-specific complexity (OCR, file parsing, neural voice) is added — a
      plain/native-default visual treatment is an acceptable stand-in for this
      first pass if that's faster to prove the wiring; the branded Screen 1 look
      (Decision 43) lands whenever this slice's UI is actually built.
      **Built:** Android-only for now (`TargetFrameworks` trimmed to
      `net10.0-android`; iOS/MacCatalyst/Windows heads removed from the
      template scaffold — Decision 37). `ApplicationId` `uk.sirous.readthestupidtext`,
      display name "Read The Stupid Text" per the naming convention. No
      network permissions in the manifest (on-device TTS only, matching the
      "we collect nothing" stance). Rather than reusing `ReadAloudService`
      wholesale (it's wired for Windows-only triggers — hotkey, UIA selection,
      clipboard — none of which exist on Android per Decision 38, and the
      plan text itself only asked to reuse `PlaybackRate`/`SpeedPresets`),
      `TypePage` wires `ISpeechReader` directly as its own thin use case — the
      simpler, correctly-scoped design once `ReadAloudService`'s dependency
      list was inspected. `AndroidSpeechReader` (`Platforms/Android`, in
      namespace `ReadTheStupidText.Mobile` — deliberately *not*
      `...Platforms.Android`, since a namespace segment literally named
      `Android` shadows the top-level `Android.*` binding namespace for
      unqualified lookups; the same reason `App : Application` had to be
      qualified as `Microsoft.Maui.Controls.Application`, since the project
      also references a library literally named `ReadTheStupidText.Application`)
      wraps `Android.Speech.Tts.TextToSpeech`. That engine has no native
      pause/resume/seek, so `Pause`/`Resume`/`SkipForward`/`SkipBackward` all
      stop and re-`Speak()` from a character offset tracked via
      `UtteranceProgressListener.OnRangeStart` (word-boundary granularity),
      snapped to the nearest space — best-effort, matching the "not
      sample-accurate" contract the Windows chunked engines already promise,
      just via a different mechanism (word boundaries instead of chunk
      boundaries). `ReadTimingFormatter` (Application, Slice 25) is reused
      as-is for the `mm:ss/--:--` timer label. `AppShell` is a `TabBar` with
      `Type`/`Camera`/`File` tabs (`FlyoutBehavior="Disabled"` — bottom nav is
      the only navigation, Decision 43); `CameraPage`/`FilePage` are stub
      pages pointing at Slices 32/33. `MauiProgram` registers `ISpeechReader`
      behind an `#if ANDROID` factory and `TypePage` for constructor
      injection through Shell's `ContentTemplate`. Colors.xaml gained the
      brand tokens (`BrandStart`/`BrandEnd`/`PageBackground`/`CardBorder`/
      `TextSecondaryLight`) and `Primary` now matches the brand purple, so
      default control theming (buttons, progress bars, the tab bar) tracks
      the brand color for free. Verified: `dotnet build` (both the Mobile
      project alone and the full `.slnx`) and the existing 102-test suite all
      pass. **Not yet verified: running on a device/emulator** — none was
      available in the dev environment this slice was built in (no AVD
      configured, no physical device attached); do that manually before
      relying on this slice.

## Verification (moved from plan.md)

- **Slice 30:** install the Android app on a device/emulator, type or paste text,
  tap Play → it reads aloud at the selected speed via Android's built-in TTS;
  Pause/Play and the speed control work exactly as their Windows counterparts.
