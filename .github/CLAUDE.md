# CLAUDE.md — CI and release pipeline (.github)

Loaded when you edit workflows or scripts. Complements the root `CLAUDE.md`.

> **This repo is public, so its Actions minutes are free.** October 2026 used about 12,500 macOS, 8,700 Windows
> and 7,800 Linux minutes; private, that would count as roughly 150,000 against the account's 2,000 (about $907 a
> month at GitHub's prices). Before it is ever made private, its builds need another home (a self-hosted runner).
> (Billing read 2026-11-01; [note](../docs/plan/notes/2026-11-01-0859-feedback-loop-w1.md).)

> **CI (Batch 8, Slice 70):** zero `upload-artifact` (the account's storage is 500 MB
> shared across repos, billed as monthly GB-hours; one run's MSIX + AAB/APK is ~1 GB, and
> deleting artifacts doesn't refund it): build jobs upload straight to a **draft GitHub
> Release** (Release assets don't count toward the quota), which `release-publish`
> publishes (creating the tag) once every affected build succeeds; repo artifact
> retention stays at 1 day as a safety net (Slice 42). `release.yml` is the only
> workflow on push to `main` (plus a main-only `workflow_dispatch` with `force_all`):
> version (GitVersion + versionCode + `.github/scripts/ship-targets.mjs`) -> test
> (`tests.yml`) -> release-draft -> `windows.yml` / `android.yml` / `ios.yml` (reusable;
> they also run on their own PR paths, never signing or uploading on PRs) ->
> release-publish -> store-submit, play-submit, testflight-submit.
> **One version and release per product (Slice 130, Decision 137):** Read The Stupid Text is `rtst-v<x.y.z>`
> (`GitVersion.yml`), Dino Reader `dino-v<x.y.z>` (`GitVersion.dino.yml`; keep the two files identical except
> `tag-prefix`). `release.yml` runs two GitVersion steps and has a draft, publish and submit chain per product
> (`release-draft-rtst`/`-dino`, `release-publish-rtst`/`-dino`). `ship-targets.mjs` measures each product's
> changes from its own newest tag (`PRODUCTS`), so a Dino-only change cuts no RTST release and the reverse; a
> core change cuts both; a major bump or `force_all` applies per product. A release carries only its own
> product's files; carry-forward stays inside the product (`find-release.sh` takes a tag regex; the first
> `rtst-v`/`dino-v` release also falls back to the old shared `v*` releases). RTST tests gate only the RTST
> chain, `dinoreader.yml` only the Dino one. Only the RTST release is marked latest (README, `releases/latest`).
> The version job bootstraps a missing `rtst-v*`/`dino-v*` baseline itself (`bootstrap-baseline-tags.sh`
> tags the newest legacy `v*` release commit and pushes it, so versions continue and never restart at
> 0.1.0; it fails only if no legacy tag exists). `GitVersion.dino.yml` is a path of every Dino target and
> must stay identical to `GitVersion.yml` except `tag-prefix`.
> The Worker is unaffected: it resolves "latest" per product from the R2 manifest, not from tags.
> (Slice 130: docs/plan/slices/130-per-product-versions-and-releases.md)

> `ship-targets.mjs` holds the path -> platform mapping (`Mobile/Platforms/Android` ->
> Android, `Mobile/Platforms/iOS` -> iOS, rest of `Mobile` -> both; `App`/`Infrastructure`
> -> Windows; core paths -> all; a major bump -> all). `ship-targets.test.mjs` checks
> every platform workflow's `pull_request.paths` matches it, so change them together.
> `release-scripts.yml` runs all script tests and actionlint (`.github/actionlint.yaml`)
> on PRs touching `.github/**`. An untouched platform is neither built nor submitted;
> its assets are carried forward. The iOS PR check builds Debug (simulator); only main does the signed Release (PR #206). When adding a project or shared build file, add its
> path to `ship-targets.mjs`. Never add `actions/upload-artifact` for build outputs.
> (Decisions 52-58, 69; Slice 44: docs/plan/slices/44-pipeline-ios-split.md;
> Slice 70: docs/plan/slices/70-split-build-pipeline.md)

> **Store submission is Windows-only.** `store-submit` is gated on the `windowsstore` output
> (App/Infrastructure/`store-submit.yml`/`windows.yml` changes), not `windows`: a core-only change
> rebuilds the MSIX for the release but is not submitted. `tests/ReadTheStupidText.Tests` is not a
> core path (tests.yml still runs it on PRs). Force a submission with `release.yml` `force_all`.
> `force_all` turns on **every** target of **both** products (`ship-targets.mjs`): it also submits Dino Reader
> to the Microsoft Store, Play and TestFlight. When only one product's store submission is approved, say so
> to the user before dispatching it (2026-10-30: an RTST-only approval ran `force_all`).
> The Store flag is measured from the newest `rtst-store-v<x.y.z>` tag (pushed by `release.yml`'s `store-submit-mark` job after a successful submission), not from the last release, so a Windows change in a release whose submission was skipped, failed or cancelled goes out with the next release. No marker tag yet: it follows the release diff (the first successful submission creates it). Unsubmitted Windows changes also switch `windows` on, so that release builds the MSIX (also on a core-only, Android-only or Dino-only release); docs-only merges cut no release, so they never carry the backlog. A marker is never moved or re-seeded by hand, and a release older than the newest marker never adds one (a rerun of an old release leaves the store base where it is; `store-submit-mark` also treats an already-existing marker at the same commit as success). (Slice 222: docs/plan/slices/222-store-marker-hardening.md; Slice 218: docs/plan/slices/218-microsoft-store-submission-skipped.md)

> **Merging a change to a core path builds every app.** The core paths in
> `ship-targets.mjs` include `.github/workflows/release.yml`, `tests.yml`,
> `.github/scripts/find-release.sh`, `ship-targets.mjs`, `*.slnx`, `GitVersion.yml`,
> `global.json` and `Directory.Build.props`, so a PR that edits any of them (even a Dino
> Reader-only PR adding a project to the solution) builds all platforms, cuts a version and submits to Google Play and
> TestFlight on merge (the Store only if a Windows path also changed, see above). Expect that, or batch such
> edits. (Slice 70: docs/plan/slices/70-split-build-pipeline.md)

> **Dino CI rule:** the `dinoreader` paths run only the `test-dinoreader`
> job (ubuntu), never the version, release or store jobs. Keep Dino paths out of
> the core paths in `ship-targets.mjs`. (Slice 59: docs/plan/slices/59-dialogue-parser-voice-caster.md)

> **Tests per product (Slice 128):** `tests.yml` (RTST's suite) runs on `windows|android|ios` and gates those builds;
> `dinoreader.yml` (Dino's suite) runs for `dinoreader|dinoapp|dinoios|dinowin` and is standalone (gates nothing). A Dino-only
> change skips RTST's tests, an RTST-only change skips Dino's. `src/ReadTheStupidText.Windows.Tray/**` is in the `windows` group
> only (no `dinowin`). (Slice 128: docs/plan/slices/128-tray-code-out-of-shared-infrastructure.md)

> **iOS toolchain pin:** `ios.yml` pins dotnet 10.0.401 + workload set 10.0.401
> (Microsoft.iOS.Sdk 26.5.10318) + Xcode 26.6, because `macos-26` has no Xcode 27.
> Bump all three together. (Slice 48: docs/plan/slices/48-ios-target-testflight.md)

> **Android toolchain pin (Slice 72):** `android.yml` and `dinoreader-android.yml` pin dotnet 10.0.402 + workload set
> 10.0.402 (Android SDK 36.1.118, the first with R8 obfuscation). `src/AndroidRelease.props` (an `AndroidR8ObfuscationMode`
> setting plus an Error target) fails any Release Android build on an older SDK, so these two files and the props'
> `36.1.118` check move together; no other workflow builds Android Release. `src/AndroidProguard.cfg` holds the R8 keep rules
> (MAUI Essentials reaches `ActivityResultContracts` through JNI, and R8 stripped `PickVisualMedia$ImageOnly.INSTANCE`).
> `AndroidRelease.props` and `AndroidProguard.cfg` are in the android and
> dinoapp path groups, so editing either ships both Android apps.
> After editing `AndroidProguard.cfg`, delete `obj/Release` and `bin/Release` before rebuilding: stale generated manifest keep rules otherwise survive.

> **Dino Windows toolchain pin (Slice 240):** `dinoreader-windows.yml` pins `SDK_VERSION` and `WORKLOAD_SET` (job `env`, both 10.0.402) and caches the whole job-local dotnet root (SDK + file-based maui-windows workload, installed by `setup-dotnet` into `DOTNET_INSTALL_DIR`; no MSI marker, so no `userlocal` or `DOTNET_CLI_HOME`) plus NuGet packages per matrix arch. The two pins must match each other and both are in the cache key; never use `10.0.x` there. The key includes `ImageOS` but not `ImageVersion` (jobs of one run can get different image versions). A restore is validated (version, `maui-windows`, a Microsoft.Maui folder in `packs\`); a bad exact hit is reinstalled, its entry deleted (`gh cache delete`, job needs `actions: write`) and saved again under the same key. A called workflow can only downgrade its caller's token permissions, so every `permissions:` scope a reusable workflow's job asks for must also be granted on the caller job in `release.yml` (`build-dinowin`), or the whole release run ends `startup_failure` with 0 jobs (2026-11-01, run 41351722978). (Slice 240: docs/plan/slices/240-dino-windows-ci-setup-cache.md)

> **iOS signing (Slice 48, lifted 2026-10-21):** `ios.yml` compiles unsigned on PRs; on main with the six iOS secrets it signs (`.github/scripts/ios-signing.sh`, identity by SHA-1), attaches the `.ipa` to the draft and `testflight-submit` uploads it after `release-publish`. Signing scripts must run on macOS bash 3.2. (Slice 48: docs/plan/slices/48-ios-target-testflight.md)

> **Tool self-tests (Slice 212):** `tools-selftests.yml` runs the device-tooling self-tests (`tools/emulator`, `tools/phone`, `tools/test-test-targets.ps1`, flow lint) on `windows-latest`, `pull_request` only, for `tools/**`, `.maestro/**` and itself. It is in no `ship-targets.mjs` group (and must stay out: editing `tests.yml` ships every app); no `upload-artifact`.

## Windows packaging and Store

`.github/workflows/windows.yml` (called by `release.yml`, Slice 5, Slice 70) packages the single-project
MSIX (x64 + ARM64, unsigned — the Store re-signs; uploaded to the Release, never
as an Actions artifact — Batch 8); it has no LFS checkout (Slice 56). See `STORE.md` for capability justification, licenses, and the
Store/deployment details. The app is **live in the Store** (`9NGT1BN1H92V`);
`store-submit.yml` pushes updates (bundles both arches → one `.msixbundle` →
msstore CLI) once the Partner Center secrets are set.

## Packs releases

`packs-<n>` releases hold packs + `packs.json`, published by the manual `packs.yml` with `--latest=false`. Every app-release tag scan (`release.yml` release-publish-*, `.github/scripts/find-release.sh`) must filter to the product's `^rtst-v`/`^dino-v` (or legacy `^v`) tags, never to `packs-<n>`. (Slice 50: docs/plan/slices/50-voice-packs-first-run-language.md)

A new translation pair in `packs.yml` needs its `TRANSLATION_PAIRS` entry, an 11-column table row (layout and per-pair licence) and a matching `TranslationPacks` leg; a test ties the three together, and `maxLength` must resolve (`max_position_embeddings` fallback) or the job fails. New packs reach users only after a `packs.yml` run and the `PackConstants` pin bump. (Slice 106: docs/plan/slices/106-windows-nordic-dutch-translation-packs.md)

A same-version pack rebuild is safe for installed phones (only `PackVersions.IsNewer` triggers a download), but only Kokoro can be carried over; the rest are rebuilt. Merge a `PackConstants.CatalogUrl` bump only after that packs release exists. (Slice 220: docs/plan/slices/220-kokoro-silent-short-lines-arm64.md)

`packs.yml` can carry an unchanged pack over from an earlier release (`kokoro_carry_over_from` input) when an upstream archive is re-uploaded and its pin breaks. Never re-pin a changed upstream file under the same pack version. (Slice 168: docs/plan/slices/168-german-in-both-apps.md)

## Dino Reader pipeline

Dino Reader has its own path-filter target (`dinoapp` in `ship-targets`, with
`dinoreader-android.yml`; assets `DinoReader-<v>.aab`/`.apk`; `DINOREADER_*` secrets,
never the RTST ones; without all five a Dino-only change cuts no release). Its releases are
`dino-v<x.y.z>` (Slice 130).

**Own workflow again (2026-11-01, Slice 239 "Changed after merge"):** the upload is `.github/workflows/dino-store.yml` (push to `main` for everything in ship-targets' `dinostore` group plus `ShippedTitles.cs` and `dino-starters.json` (a node test checks the paths cover the group), a daily `schedule` at 03:30 UTC that retries a failed or cancelled upload, and `workflow_dispatch`; every run passes `--delist`; group `dino-store`, no cancel), because in `release.yml`'s group a story merge waited up to an hour behind an iOS build. `release.yml`'s push `paths` are `**`, `!content/dino/**` and then the four starter folders (later patterns win), so a store-only push starts no `release.yml` run, a starter push runs both, a mixed push runs both. Keep those four paths equal to `dino-starters.json` (a node test checks release.yml's and dino-store.yml's path lists). The text below that says `release.yml`'s `publish-store` job describes the same steps, now in `dino-store.yml`; the Dino suite no longer gates it, and `--check-content` plus `build --live` validation do the checking.
Store books publish from the `publish-store` job (Slice 158, Decision 163; Decision 162's separate `dino-store.yml` came back): every run of `dino-store.yml` starts it (store-path push, daily schedule or manual; not tag-relative, so a failed or skipped publish can never be lost behind a later `dino-v` tag; the live comparison makes a no-op run upload nothing; 30-minute timeout). It builds `artifacts/store` from `content/dino/` (`dotnet run --project tools/DinoReader.BookBuilder -- build`; Slice 157), then `publish-store-books.mjs` compares with the LIVE `/api/stories` (never the
diff) and uploads each book whose built version is higher, cover and package first, `meta.json` last, never overwriting an existing
`story-v<n>.dino`, never uploading a lower version; `--verify` then fails the job if any built book is still behind. It reuses
`CLOUDFLARE_API_TOKEN`/`CLOUDFLARE_ACCOUNT_ID` (a missing token on `main` fails the job), has its own `concurrency` group and `dinostore` alone never
cuts an app release (`CONTENT_ONLY_TARGETS` keeps it out of the major-bump check). One failing book no longer blocks the rest: the job fails at the end listing every failed book.
A book is the folder `content/dino/<stem>/` (Slice 157). **Slice 239: a store book's folder (new, changed or deleted) runs only `publish-store`**: `ship-targets.mjs` maps it to `dinostore` alone, and only a STARTER's folder (listed in `.github/scripts/dino-starters.json`, which a node test and a C# test keep equal to every `book.json` `"kind": "Starter"`; path filters cannot read book.json) also maps to `dinoreader` (`dinoreader.yml` runs `BookBuilder check` after the tests) and watches the app's rebuilt `.dino`. `dinoreader.yml`'s PR paths therefore list the four starter folders, not `content/dino/**`; `site-checks.yml` has `paths-ignore: content/dino/**`; on a push, `release.yml`'s `site-checks` job runs only when `site-checks-needed.mjs` finds a changed file outside `content/dino/`. A new starter must be added to `dino-starters.json` AND to `dinoreader.yml`'s paths (the parity test fails otherwise). A book's `story.md` is Markdown, which the path filters ignore: `CONTENT_MARKDOWN` in `ship-targets.mjs` exempts every book's (so `dinostore` fires), and `dinoreader.yml` re-includes only the starters' (`STARTER_CONTENT_MARKDOWN`) after `!**/*.md`.
No version stamp for a store book: the job first writes the live state (`publish-store-books.mjs --live-state`: each live story's version and the `contentHash` in its `meta.json`), then `BookBuilder build --live` builds a changed book (hash differs, or the live one has no hash) as max(`book.json` version, live + 1) and an unchanged one as the live version (nothing uploaded). Right before an upload the script checks that `story-v<n>.dino` does not exist in the bucket and moves a hashed book up to n+1 until it is free (a package version is never reused or overwritten; `meta.json` goes up last). A validation problem skips only that book (named in an `::error::` line, build exit 2): the valid books still publish and the job fails at the end; `build --live` without a value is an error. A story is delisted (its `meta.json` deleted; packages stay for installed copies; ids logged first; more than 3 ids or half the live list is refused) on every run (`--delist`; the guards below make that safe, and a delisting is never lost when a pending run is replaced), its live `meta.json` carries a `contentHash` (hand-uploaded or older stories are left alone) and no content folder has its id. (Slice 239: docs/plan/slices/239-dino-store-book-without-app-build.md)
A book already live at the same version still gets only its `meta.json` re-uploaded when the built one differs from the bucket's, so listing-only data (credit lines) ships without a version bump. (Slice 192: docs/plan/slices/192-dino-store-credits-in-every-language.md)
(Slice 158: docs/plan/slices/158-dino-publish-store-books.md)

Dino Reader on iOS (Slice 67) is the `dinoios` target: `dinoreader-ios.yml` (a twin of `ios.yml`
for `src/DinoReader.App`, asset `DinoReader-<v>.ipa`) and `testflight-submit-dino` in `release.yml`.
It reuses the team-wide `ASC_API_*` and `IOS_DIST_CERT_P12_*` secrets and has its own
`DINOREADER_IOS_PROVISIONING_PROFILE_BASE64` (profile "Dino Reader App Store"); `release-draft`
reports all six as `dino_ios_signed`, and without them it only compiles unsigned and ships nothing.
`Mobile.Shared/Platforms/iOS/**` and the Shared root rebuild both `ios` and `dinoios`. The `.ipa`
carry-forward patterns are per app (`^ReadTheStupidText-...ipa$`, `^DinoReader-...ipa$`). (Decisions 72-82, `docs/plan/slices/58-67`)

Dino Reader on Windows (Slice 67) is the `dinowin` target: `dinoreader-windows.yml` (a twin of `windows.yml`
for `src/DinoReader.App`; unsigned x64 + ARM64 MSIX from `dotnet publish -f net10.0-windows10.0.19041.0
-p:IncludeAndroid=false -p:Platform=<arch>`, assets `DinoReader.App_<v>.0_<arch>.msix`) and `store-submit-dino` in
`release.yml`, which calls the shared `store-submit.yml` (inputs `package`, `product_id`) with the same
`AZURE_AD_*`/`SELLER_ID` secrets and runs only when the repository variable `DINOREADER_STORE_APP_ID` is set.
It needs no secrets to build, so any change to `src/DinoReader.App/**`, `DinoReader.Core`, `Mobile.Shared` (root and
`Platforms/Windows/`) or `Infrastructure` now cuts a release (a Dino-only change did not before). Never pass a global
`-p:RuntimeIdentifier` to the Dino build: the csproj derives it from `-p:Platform` (a global one makes the package list
Mobile.Shared's `.pdb` twice, APPX1101). The RTST Store submit matches only `ReadTheStupidText.App_*.msix`.
`store-submit.yml` has no default Store id: `product_id` is required (RTST passes `vars.STORE_PRODUCT_ID`, Dino `vars.DINOREADER_STORE_APP_ID`) and a guard step fails when the package and id belong to different apps or `STORE_PRODUCT_ID` is unset. (Slice 127: docs/plan/slices/127-release-safety-fixes.md)
Never pipe a producer into `grep -q` under `pipefail` (`release.yml`, `.github/scripts`): capture the output first, or here-string it into grep. (Slice 127)

## iOS review cadence

`ios-review-cadence.mjs` is the only code that sends a build to Apple's reviews: `testflight-external.yml` (daily 05:17 UTC, backup 07:17, + manual) adds the newest processed build to the external group "Rofagha" and submits it for Beta App Review even while an older build still waits (batching: the newest build each morning; never cancel or expire the older one), turns `autoNotifyEnabled` on for each build it submits or finds waiting, and sends "Notify Testers" for the newest approved build (`buildBetaNotifications`; otherwise approved builds sit at Approved, 2026-10-28 note `docs/plan/notes/2026-10-28-testflight-auto-notify.md`); `appstore-submit.yml` (manual, `app` = rtst|dino|both) does the App Store and still skips while an App Store review is in progress. Never add a step that cancels a waiting review or expires a build (starvation: each cancel goes to the back of Apple's queue). A new iOS app needs its row in the script's `APPS` table. (Decision 154, Slice 147: docs/plan/slices/147-ios-review-cadence.md)

## Android downloads

APKs are not served from the site or R2: `/download` and everything under it (`/download/*`, `/dino/download/*`) on both sites is a 302 to `#platforms` (`worker/downloads.ts`); APKs live on the GitHub Release. Old APKs and manifests still sit in the `DOWNLOADS` bucket (`readthestupidtext-downloads`) but nothing serves them. Windows stays Store-only. Don't re-add APK serving. (Slice 69: docs/plan/slices/69-apk-downloads-on-r2.md)
- The `piper-nb` pack pins rhasspy/piper-voices revision 5041a7a in `packs.yml`; bump the `PackConstants` pin after each packs run. The espeak guard checks `no_dict` (Norwegian `lang/gmq/nb` uses `dictionary no`; there is no `nb_dict`): don't "fix" it back. `packs.yml` pins Python 3.12. (Slice 104: docs/plan/slices/104-piper-norwegian-pack.md)

- `kokoro-en` is built by `packs.yml` from the sha256-pinned sherpa-onnx `kokoro-int8-multi-lang-v1_0` archive, catalog kind `storyteller`, with its own 150 MB cap (`KOKORO_MAX_BYTES`; the zip is about 126 MB) and a `<id>-<version>-sample.wav` beside the zip (the "Hear one" clip). It carries GPL espeak-ng data inside the zip only (Decision 119, same scope as `piper-fa`/`piper-nb`). The card shows only while the pinned catalog lists it (packs-6 onward). The v2 pack also ships the zh lexicon, dict and rule fsts, and the `packs.yml` self-check synthesizes every sid. The current packs release is packs-11 (`PackConstants.CatalogUrl`). The pin is the float32-STFT re-upload of the archive (kokoro-en 3 onward; `packs.yml` holds the sha), and fp32 is over the cap (Slice 220: docs/plan/slices/220-kokoro-silent-short-lines-arm64.md). (Slice 114: docs/plan/slices/114-dino-kokoro-english-storyteller-pack.md; Slice 118: docs/plan/slices/118-dino-storyteller-more-languages.md)
- Local `actionlint` runs shellcheck only when shellcheck is installed, and this PC has none, so shell findings in workflow `run:` blocks (SC2010, SC2129) show up only in CI. Install shellcheck before editing workflow shell. Its download was blocked by the auto-mode permission check ("Code from External", 2026-11-01); when that happens, tell the user it was blocked and ask them to install shellcheck themselves, and until then treat CI's actionlint as the shell check and read its findings before pushing again. (Slice 240: docs/plan/slices/240-dino-windows-ci-setup-cache.md; it cost two extra CI rounds, 2026-11-01)
- Dispatch and push release runs have separate concurrency groups (`release-dispatch-<ref>`, `release-<ref>`), ordered by the `version` job's wait step; do not drop that step. (Slice 221: docs/plan/slices/221-device-test-tooling-leftovers-night-1.md)
