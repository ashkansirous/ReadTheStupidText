# Slice 425 — Dino: the "story unreadable" fallback says which part of the app sent it, why, and which store book
**Product:** Dino

*Planned 2026-11-20. User, 2026-11-20, asked whether to add a slice that makes the "story unreadable" event say which part of the app sent it and which book: "yes".*

*In progress (wave 2026-11-28), draft PR.*

**Touches:** `src/Shared/Sirous.Reading.Application/Usage/FallbackKind.cs`, `src/Dino/DinoReader.Core/FileStoryLibrary.cs`, `src/Dino/DinoReader.Core/StoryJson.cs`, `src/Dino/DinoReader.App/GalleryPage.xaml.cs`, `src/Dino/DinoReader.App/OwnBooksCleanupRun.cs`, `src/Dino/DinoReader.App/BackupPage.xaml.cs`, `src/Dino/DinoReader.App/DinoFallbackSink.cs`, `worker/events.ts`, `worker/events.test.mjs`, `worker/CLAUDE.md` (the reason list it documents), `tests/Dino/DinoReader.Tests/`, `tests/Shared/Shared.Tests/` (fallback list tests), `.maestro/dino/usage-fallback-swallowed.yaml`, this slice file.

## Design (coordinator, wave 2026-11-28, from the premise check of origin/main 618b339b)

The premise check found three gaps; this design closes them and the builder builds it.

- **Line numbers moved:** site 1 and 2 are now `FileStoryLibrary.cs:582` and `:587`; the others hold (GalleryPage `:363`, OwnBooksCleanupRun `:41` and `:115`, BackupPage `:498`).
- **A seventh site:** `FileStoryLibrary.cs:611` (Slice 464's optional listing cleanup) also reports `story_unreadable`. It gets its own reason, `story_listing_cleanup_failed`. (plan-not-needed: 464)
- **Site 3's kind goes into the reason, not a new field:** the Worker's `fallbackDetail` (`worker/events.ts:294`) takes exactly `area:reason`. So instead of one `shelf_rebuild_failed` plus a kind, site 3 sends one of six reasons: `shelf_rebuild_timeout`, `shelf_rebuild_cancelled`, `shelf_rebuild_io`, `shelf_rebuild_layout`, `shelf_rebuild_library`, `shelf_rebuild_other`. One pure, unit-tested function maps the caught exception to that reason.
- **The store book id rides as an optional third part of the detail:** `library:<reason>:<catalogue id>`. Fallback events have no book field (their `sources` are `shelf`/`own`/`none`, `worker/events.ts:26-28`), so the Worker's `fallbackDetail` accepts two parts (every reason, as today) or three parts only for the reasons that name a store book (sites 1, 2 and 6), with the third part checked against the catalogue id format the Worker already uses for store book ids (find it in `worker/`; if none exists, a strict lower-case id pattern written once as a named constant). Anything else is refused. Own books never send a third part. Old builds' two-part `library:story_unreadable` stays accepted.
- **The Worker's own speaker fault** (`worker/speaker-usage.ts:23`, `:80`, Slice 436) keeps sending `library:story_unreadable`; "no longer sent" in the Decisions means the app only. (plan-not-needed: 436)
- **Once per book per launch:** one process-wide set of reported (reason, book) pairs, owned by the reporting site in `FileStoryLibrary`, cleared only by a new launch.

## Why

`library:story_unreadable` is one reason shared by six report sites (read from `origin/main` 2026-11-20), and the event carries only `area:reason` (`worker/events.ts:288` accepts exactly two parts), so a count cannot tell a damaged book from a failed shelf rebuild:

1. `FileStoryLibrary.cs:545`: the stored id differs from the folder name. The book is left off the shelf.
2. `FileStoryLibrary.cs:550`: `StoryFormatException` (`StoryJson.cs:54` empty, `:62` invalid, `:102` newer version or no valid id, `:142` back cover but no pages, `:429` missing fields). The book is left off the shelf, re-reported on every shelf listing.
3. `GalleryPage.xaml.cs:362`: a catch-all around the shelf rebuild (store timeouts, cancels, layout, library errors). The shelf keeps what it showed.
4. `OwnBooksCleanupRun.cs:41`: listing own-book folders or the cleanup pass threw.
5. `OwnBooksCleanupRun.cs:115`: deleting one own (recorded) book threw.
6. `BackupPage.xaml.cs:498`: one store book from a backup could not be applied.

Production data: 6 events since retention began (2026-10-27): 5 on 2026-11-11 (Windows 1.123.3.0, Mexico, 5 sessions) and 1 on 2026-11-20 (Windows 1.136.3.0, Denmark, first shelf right after onboarding, followed 1 s later by `ui:layout_failed`). Which site sent them is unknown; the 2026-11-20 one is *likely* site 3.

## Decisions (decided by this session, 2026-11-20)

- One reason per site and per format error, from a closed list (no free text, no exception messages): `story_id_mismatch` (1); `story_empty`, `story_invalid`, `story_newer_version`, `story_no_pages`, `story_missing_fields` (2, carried by a closed enum on `StoryFormatException`); `shelf_rebuild_failed` (3); `own_books_cleanup_failed` (4); `own_book_delete_failed` (5); `backup_book_unapplied` (6). `story_unreadable` stays accepted by the Worker for old builds and is no longer sent.
- Site 3 also says what failed, as a closed kind: `timeout`, `cancelled`, `io`, `layout`, `library`, `other`.
- **Which book:** only a **store** book's catalogue id (the public id the store and `/book/<id>` links already use), validated by the Worker against the catalogue id format. Own (recorded) books never send an id; they send the reason only. No device or user id is added.
- **Once per book per launch:** sites 1 and 2 report a given book once per app launch, not on every shelf listing (today one bad book counts once per refresh).
- Shared code: the same change covers Android, iOS and Windows; nothing is platform-specific.

## Work

- [ ] 1. The new reasons and the site-3 kinds in `FallbackKind.cs` and the Worker's allow-lists (`worker/events.ts`), with Worker tests: old `story_unreadable` still accepted, unknown values refused, a book id outside the catalogue format refused.
- [ ] 2. `StoryFormatException` carries a closed `StoryFormatError` enum set at each throw in `StoryJson.cs`; `FileStoryLibrary` reports the matching reason with the store book id, once per book per launch.
- [ ] 3. Sites 3-6 report their own reasons (site 3 with its kind, site 6 with the store book id).
- [ ] 4. Pure tests in `tests/Dino/DinoReader.Tests` (each bad file gives its reason; a second listing in the same launch reports nothing; an own book sends no id) and the fallback list tests in `Shared.Tests`.
- [ ] 5. Update `.maestro/dino/usage-fallback-swallowed.yaml` to expect the new reason, and run it on the emulator; run the Dino Windows launch on the VM (both of the real cases were on Windows).

## Verification

- Worker and Dino tests pass; the emulator flow logs `[Usage] fallback library:<new reason>`; the Windows VM launch logs the same for a planted bad book.
- After release: `dino_usage` shows the new reasons (`blob9 LIKE 'library:%'`) and no new `story_unreadable` from builds after this slice.

## Not yet verified

_(none yet)_
