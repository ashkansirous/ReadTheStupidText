# Slice 34 — Android CI: build + signed AAB, internal testing track

*Built (legacy plan.md entry; moved verbatim by archive-docs 2026-10-20).*

## Plan entry (moved from plan.md)

- [x] **Slice 34 — Android CI: build + signed AAB, internal testing track.**
      (Decision 42) New GitHub Actions workflow builds the MAUI Android project
      and packages a signed `.aab` (CI-held upload key; Play App Signing re-signs
      for distribution). Once Play Console credentials exist, upload to the
      **internal testing** track — mirrors the Windows Store pipeline's original
      "testing" stage before any public submission. GitVersion continues to supply
      the SemVer for the Android `versionName`/`versionCode`.
      **Built:** `.github/workflows/android-build.yml`, structured like the
      sibling Lets-Call-Mom project's own `android.yml` (same problem, same
      fix, found by reading that repo's actual workflow rather than
      reinventing it): a `HAS_SIGNING`/`HAS_PLAY_PUBLISHING` pair of env
      booleans (`secrets.X != ''`, since `secrets` isn't usable directly
      inside a step `if:`) gate the signed-build and Play-upload steps
      independently, so the workflow is safe to merge before either secret
      exists — an always-on unsigned Debug build (`dotnet build -f
      net10.0-android`) is the real "does this still compile" check, with no
      secrets required. `version`/`test` jobs mirror `build.yml` exactly
      (same GitVersion config, same unit-test gate); the Android
      `versionCode` is derived as `major*10000 + minor*100 + patch` from
      GitVersion's own numeric outputs (monotonically increasing as long as
      main only bumps forward, which `GitVersion.yml` already guarantees) —
      `versionName` is just `majorMinorPatch` passed straight through.
      Release signing uses `-p:AndroidPackageFormat=aab` +
      `-p:AndroidKeyStore=True` + `-p:AndroidSigningKeyStore/KeyAlias/
      KeyPass/StorePass` (confirmed via context7 that `AndroidSigningKeyPass`'s
      `env:`-prefix indirection is documented as **unsupported once
      `AndroidPackageFormat=aab`** — passed as plain `-p:` values instead,
      still never logged since GitHub Actions masks any output matching a
      registered secret's value); Play upload via `r0adkll/upload-google-play@v1`
      (same action the sibling project already uses), main-only, gated on
      both secrets. **Generated the actual CI-held upload keystore** (RSA
      2048, 10000-day validity, alias `upload`) via `keytool` and stored it
      through the `manage-secrets` skill — durable copy + registry row in the
      private `ashkansirous/secrets` store, then pushed as four GitHub Actions
      repo secrets (`ANDROID_SIGNING_KEYSTORE_BASE64/KEY_ALIAS/KEY_PASSWORD/
      STORE_PASSWORD`) via `gh secret set`. `PLAY_SERVICE_ACCOUNT_JSON` is
      **not** set — that credential can only come from the user's own Google
      Play Console (Setup → API access → service account), the same
      never-fabricate-a-third-party-credential line `store-submit.yml`'s
      Azure AD secrets already draw; `HAS_PLAY_PUBLISHING` stays false and
      the publish step stays a documented no-op until it's added, exactly
      like `AZURE_AD_TENANT_ID` for the Windows Store pipeline. Also note:
      Play Console requires the very first release on any track to be
      uploaded by hand once through its UI regardless — a one-time API
      limitation this workflow can't route around, same caveat the sibling
      project's own workflow documents.
      Verified for real, not just read: ran the exact signing command
      locally against a throwaway 1-day test keystore
      (`-p:AndroidKeyStore=True -p:AndroidSigningKeyStore=... -p:
      AndroidSigningKeyAlias=testkey ...`) — produced
      `uk.sirous.readthestupidtext-Signed.aab`, then `jarsigner -verify`
      confirmed "jar verified" signed by the test cert's own DN, proving the
      MSBuild property wiring is genuinely correct before it ever needed a
      GitHub Actions run to find out. Also verified: `dotnet build`
      (full `.slnx`, 0 errors) and the 108-test suite, unaffected by this
      slice (no application code changed). **Not yet verified: the workflow
      actually running in GitHub Actions** — needs a push/PR to trigger; not
      something triggerable from this environment. **Not yet verified:
      Play Store upload** — blocked on `PLAY_SERVICE_ACCOUNT_JSON`, which only
      the user can create.

## Verification (moved from plan.md)

- **Slice 34:** a CI run on `main` produces a signed `.aab` artifact; once Play
  Console credentials are configured, the same run uploads it to the internal
  testing track and it's installable via the Play Store's internal-testing link.
