# Slice 131 — Mobile.Shared gets its own namespace

*In progress (wave 2026-10-26 D), draft PR.*

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

- [ ] Set `RootNamespace` and rename the namespaces in Mobile.Shared (Platforms included); update usings
      in Mobile, DinoReader.App and the tests. XAML `xmlns` that point at Mobile.Shared types too.
- [ ] Note in the Mobile.Shared CLAUDE.md.

Run it **alone, between waves**: it touches usings in nearly every mobile file.

## Verification

- Android builds of both apps, iOS compile (`-p:IncludeIos=true`), Dino Windows publish; both test
  suites green. A quick emulator launch of each app (Shell routes and DI resolve).
