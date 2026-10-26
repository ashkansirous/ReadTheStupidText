# Slice 131 — Mobile.Shared gets its own namespace

*Built 2026-10-26 (PR #371), not yet verified on an emulator or macOS CI.*

**Batch:** 27 (product separation, Decisions 133-137)
**Touches:** `src/ReadTheStupidText.Mobile.Shared/**`, `src/ReadTheStupidText.Mobile/**` (usings), `src/DinoReader.App/**` (usings), `tests/**` (usings), `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md`

## Why

Mobile.Shared's root namespace is `ReadTheStupidText.Mobile`, the same as the RTST phone app's. Dino code
reads `using ReadTheStupidText.Mobile...` 16 times, which looks like Dino depends on the RTST app when it
only depends on the shared library.

## Decisions

- **Decision 134:** no full rename of the shared projects now (cosmetic, about 40k lines, clashes with
  parallel work). Only Mobile.Shared's namespace becomes `ReadTheStupidText.Mobile.Shared`. The project
  and folder names stay.

## Work

- [x] Set `RootNamespace` and rename the namespaces in Mobile.Shared (Platforms included); update usings
      in Mobile, DinoReader.App and the tests. XAML `xmlns` that point at Mobile.Shared types too.
- [x] Note in the Mobile.Shared CLAUDE.md.

Run it **alone, between waves**: it touches usings in nearly every mobile file.

## Verification

- Android builds of both apps, iOS compile (`-p:IncludeIos=true`), Dino Windows publish; both test
  suites green. A quick emulator launch of each app (Shell routes and DI resolve).

*Changed while building:* Mobile.Shared's namespace and `RootNamespace` are now `ReadTheStupidText.Mobile.Shared` (70 files). Dino's 16 usings and 12 RTST Mobile files updated. In the tests, `MainThreadStub` moved to the `.Shared` namespace (linked Shared sources resolve `MainThread` through it) and 3 test files got the using. Review found no XAML, manifest, plist, resource or serialized-name dependency on the namespace.

*Verified at build:* 0 errors on RTST Mobile Android, Dino Android, Dino Windows head (Release build), RTST Mobile iOS compile, Dino iOS compile and RTST app x64; both test suites pass.

#### For CLAUDE.md

- Apps that use Mobile.Shared need `using ReadTheStupidText.Mobile.Shared;`; its namespace is no longer `ReadTheStupidText.Mobile`, which stays the RTST phone app's. Already written in `src/ReadTheStupidText.Mobile.Shared/CLAUDE.md` and `src/ReadTheStupidText.Mobile/CLAUDE.md` (Slice 131: docs/plan/slices/131-mobile-shared-namespace.md).

#### Not yet verified

- Emulator launch of RTST Mobile and Dino (Shell routes, DI resolve).
- macOS CI iOS build after merge.
