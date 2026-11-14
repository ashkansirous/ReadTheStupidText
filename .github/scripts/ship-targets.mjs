#!/usr/bin/env node
/**
 * Decides which targets a push to main builds (Slice 70, replaces build.yml's
 * `changes` job and its dorny/paths-filter block).
 *
 * The mapping is exactly the one Slices 44/48 settled (Decision 69):
 *   - core (Domain/Application/Documents/solution/pipeline)       -> windows + android + ios
 *   - tests/Rtst/ReadTheStupidText.Tests, tests/Shared/Shared.Tests           -> nothing (tests.yml runs them on PRs; `--local --tests` / testSuites() says which suite a change needs, Slice 368)
 *   - App / Windows.Tray / Infrastructure / store-submit.yml / windows.yml -> windows + windowsstore (the Store submission)
 *   - Windows.Tray (Slice 128, tray-only code) maps to no dino* target: only App references it
 *   - Mobile/Platforms/Android/**                                 -> android
 *   - Mobile/Platforms/iOS/**                                     -> ios
 *   - the rest of Mobile/** (the RTST app's MAUI root)            -> android + ios
 *   - Mobile.Shared/Platforms/Android/**                          -> android + dinoapp
 *   - Mobile.Shared/Platforms/iOS/**                              -> ios + dinoios
 *   - Mobile.Shared/Platforms/Windows/**                          -> dinowin (Slice 67)
 *   - (Infrastructure/** also -> dinowin: Dino Reader on Windows reads with its engines)
 *   - DinoReader.App/**, DinoReader.Core/**                       -> also dinoios (Slice 67, ships only with its own iOS secrets) and dinowin (unsigned MSIX, needs no secrets)
 *   - the rest of Mobile.Shared/** (platform code both apps use)  -> android + ios + dinoapp
 *   - DinoReader.App/**, DinoReader.Core/**                       -> dinoapp (Slice 58/60; ships only with its own signing secrets)
 *   - DinoReader.Core / tests/Dino/DinoReader.Tests                    -> dinoreader (its tests; Core also rebuilds dinoapp)
 *   - store books (Slice 158, Slice 157): Core, content/dino/** (every book's text, voices, pictures and translations), the book builder, the upload scripts -> dinostore (dino-store.yml only; never ships an app)
 *   - Slice 239, 265: a change under content/dino/<book>/** (a new, changed or deleted folder; its text, pictures, translations and its Git LFS audio clips) maps to dinostore ONLY: no Dino suite,
 *     no app, no PR check workflow. There is no starter exception (Slice 261 moves the starters out of the app); a book's rebuilt .dino in the app is under DinoReader.App/** and ships the apps
 *   - Slice 375 B: content/dino/<book>/high-res/** (print-size pictures, Git LFS) -> dinostore only (packed into the store book's high-res package; never an app, never a Dino suite)
 *   - Markdown anywhere (IGNORED_PATTERNS, case-sensitive like GitHub's `paths`) -> nothing (ignored before matching), except a book's text (CONTENT_MARKDOWN: content/dino/<stem>/story.md)
 *   - docs and anything else                                      -> nothing
 * `windowsstore` is true only for the Windows group's own paths (a core-only change sets `windows`,
 * so the MSIX is rebuilt for the GitHub release, but is not submitted to the Store). Slice 218: that flag
 * is measured from the newest marker tag `rtst-store-v<x.y.z>` that release-rtst.yml pushes after a successful submission, not from the last release; so an unsubmitted
 * Windows change is submitted by the next release even when that release is core-only, docs-only or
 * Android-only, and a pending submission also turns `windows` on (the MSIX must be built). With no marker
 * tag, or when the diff against it fails, the flag follows the release diff.
 * A major version bump builds every target (windowsstore included), and so does a missing base tag
 * or a force_all run.
 *
 * Changes are measured per product (Slice 130, Decision 137) from the newest published tag of that
 * product merged into HEAD (rtst-v<x.y.z> for the RTST targets, dino-v<x.y.z> for the Dino ones;
 * the old shared v<x.y.z> tags are history and ignored), not from the push's own `before`: a failed
 * run, or a pending run dropped by the concurrency queue, is still built next time. Failing safe
 * means building more, never less. A product with no tag builds everything (release-shared.yml's version
 * job creates the baseline tags first, so this only happens if that is skipped).
 *
 * Local mode (Slice 180): `node .github/scripts/ship-targets.mjs --local [--base origin/main] [--files]` prints the same flags for
 * the working tree (committed and uncommitted changes, untracked files included) against the merge base with `--base`, so a
 * device test run can skip the apps a change does not touch. `--files` prints the changed files instead; `--stdin` takes the file list from standard input (one per line, `*` = unknown). An unknown base
 * means every target (fail safe: test more, never less).
 *
 * Known gap: changes to worker/download-keys.mjs don't map to any target, so they trigger no re-publish.
 *
 * Each platform workflow's own `pull_request.paths` mirrors its rows below (plus a
 * trailing "not Markdown" negation, the Markdown rule above);
 * ship-targets.test.mjs parses the workflows and fails on any drift.
 */

import { execFileSync } from 'node:child_process';
import { appendFileSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const MOBILE = 'src/Rtst/ReadTheStupidText.Mobile/';
const SHARED = 'src/Shared/Sirous.Reading.Mobile.Shared/';
const MAJOR_BUMP = 'major';
/** The branch a local run compares against (Slice 180). */
const DEFAULT_LOCAL_BASE = 'origin/main';
/** Node's default 1 MB would fail an 18k-path diff with ENOBUFS (ClaudeSkills #166 raised it the same way; Slice 269). */
const GIT_MAX_BUFFER = 256 * 1024 * 1024;
const LINE_BREAK = String.fromCharCode(10);
const NUL = String.fromCharCode(0);
/** The repo this script lives in (.github/scripts/..): local mode reads this tree, not the caller's current directory. */
const DEFAULT_REPO_ROOT = fileURLToPath(new URL('../..', import.meta.url));
/** Dino Reader's GitVersion config (the twin of the core GitVersion.yml): a path of every Dino target. */
const DINO_VERSION_CONFIG = 'GitVersion.dino.yml';

/**
 * Marker tag release-rtst.yml pushes after a Microsoft Store submission succeeds (Slice 218); the literal in release-rtst.yml's
 * `store-submit-mark` job must match. Not an `rtst-v` tag (so GitVersion and the release scan ignore
 * it); the store flag is measured from the newest one.
 */
export const STORE_TAG_PREFIX = 'rtst-store-v';

/**
 * Each product's tag prefix (also its GitVersion tag-prefix) and the targets its release carries.
 * `store` (RTST only): the marker-tag prefix, the flag measured from it and the flag a pending submission switches on.
 */
export const PRODUCTS = {
    rtst: { prefix: 'rtst-v', targets: ['windows', 'windowsstore', 'android', 'ios'], store: { prefix: STORE_TAG_PREFIX, flag: 'windowsstore', buildFlag: 'windows' } },
    dino: { prefix: 'dino-v', targets: ['dinoreader', 'dinoapp', 'dinoios', 'dinowin', 'dinostore'] },
};

/** Targets that publish content and never cut a release, so a major-bump token on their files must not rebuild the product (Slice 158). */
const CONTENT_ONLY_TARGETS = ['dinostore'];

/** Changed files matching any of these never trigger a build (mirrored as trailing `!` entries in each workflow's `paths`). */
export const IGNORED_PATTERNS = ['**/*.md'];
/** Markdown that is content, not documentation (Slice 157): a book's text. Not ignored; the workflows re-include it after the Markdown negation. */
export const CONTENT_MARKDOWN = ['content/dino/*/story.md'];

/** Changes that rebuild every app platform (Windows, Android, iOS and Dino Reader). */
export const CORE_PATHS = [
    'src/Shared/Sirous.Reading.Domain/',
    'src/Shared/Sirous.Reading.Application/',
    'src/Shared/Sirous.Reading.Documents/',
    'global.json',
    'Directory.Build.props',
    'src/Shared/SherpaOnnxVersion.props',
    'GitVersion.yml',
    '.gitattributes',
    // Slice 369: the shared half of both release workflows; each product's own release-<product>.yml is in its own group below.
    '.github/workflows/release-shared.yml',
    '.github/workflows/tests.yml',
    '.github/scripts/find-release.sh',
    '.github/scripts/ship-targets.mjs',
];

/**
 * A pattern ending in `/` matches everything under that folder; one with `*`
 * matches within a single path segment; anything else is an exact file.
 */
export const GROUPS = {
    core: { include: CORE_PATHS },
    // The unit tests build and ship nothing, so no target maps here. The groups only exist so tests.yml's
    // pull_request.paths (which runs them on PRs) stays derived from GROUPS, and so testSuites() can pick the suites.
    // unittests: RTST's own (tray, control panel, phone app); sharedtests: the shared engine's (Slice 368).
    unittests: { include: ['tests/Rtst/ReadTheStupidText.Tests/'] },
    sharedtests: { include: ['tests/Shared/Shared.Tests/'] },
    // The pin checker SherpaVersionPinTests runs, and the pin sites outside every other group (Slice 368): they select the shared suite only.
    sherpapins: { include: ['.github/scripts/check-sherpa-pins.mjs', '.github/workflows/packs.yml', 'tools/dino/sound-words-test/requirements.txt'] },
    // Infrastructure is in `windows` too (RTST's suite builds on it); on its own it is shared code (Dino Reader on Windows reads with it).
    infrastructure: { include: ['src/Shared/Sirous.Reading.Infrastructure/'] },
    windows: {
        include: [
            'src/Rtst/ReadTheStupidText.App/',
            'src/Rtst/ReadTheStupidText.Windows.Tray/',
            'src/Shared/Sirous.Reading.Infrastructure/',
            '.github/workflows/store-submit.yml',
            '.github/workflows/windows.yml',
        ],
    },
    android: {
        include: [
            `${MOBILE}Platforms/Android/`,
            `${SHARED}Platforms/Android/`,
            'src/Shared/AndroidSherpaPackaging.props',
            'src/Shared/AndroidRelease.props',
            'src/Shared/AndroidProguard.cfg',
            '.github/scripts/stage-android-package.sh',
            '.github/scripts/check-android-natives.sh',
            '.github/workflows/android.yml',
        ],
    },
    dinoapp: {
        include: [
            DINO_VERSION_CONFIG,
            'src/Dino/DinoReader.App/',
            'src/Dino/DinoReader.Core/',
            `${SHARED}Platforms/Android/`,
            'src/Shared/AndroidSherpaPackaging.props',
            'src/Shared/AndroidRelease.props',
            'src/Shared/AndroidProguard.cfg',
            '.github/scripts/stage-android-package.sh',
            '.github/scripts/check-android-natives.sh',
            '.github/workflows/dinoreader-android.yml',
        ],
    },
    // Dino Reader on iOS (Slice 67): its app and Core, the iOS platform code it shares with RTST,
    // the iOS native packaging props and the iOS signing/upload scripts. Like dinoapp it ships only
    // with its own signing secrets.
    dinoios: {
        include: [
            DINO_VERSION_CONFIG,
            'src/Dino/DinoReader.App/',
            'src/Dino/DinoReader.Core/',
            `${SHARED}Platforms/iOS/`,
            `${SHARED}IosSherpaPackaging.props`,
            '.github/scripts/ios-signing.sh',
            '.github/scripts/testflight-upload.sh',
            '.github/scripts/check-ios-bundle.sh',
            '.github/workflows/dinoreader-ios.yml',
        ],
    },
    // Dino Reader on Windows (Slice 67): its app and Core, its Windows platform code, and
    // Infrastructure, whose Windows speech engines it reads with. The MSIX is unsigned, so unlike
    // dinoapp/dinoios it needs no secrets; the Store submission is gated on DINOREADER_STORE_APP_ID.
    dinowin: {
        include: [
            DINO_VERSION_CONFIG,
            'src/Dino/DinoReader.App/',
            'src/Dino/DinoReader.Core/',
            `${SHARED}Platforms/Windows/`,
            'src/Shared/Sirous.Reading.Infrastructure/',
            '.github/workflows/store-submit.yml',
            '.github/workflows/dinoreader-windows.yml',
        ],
    },
    ios: {
        include: [
            `${MOBILE}Platforms/iOS/`,
            `${SHARED}Platforms/iOS/`,
            '.github/scripts/ios-signing.sh',
            '.github/scripts/testflight-upload.sh',
            '.github/workflows/ios.yml',
        ],
    },
    mobileshared: { include: [MOBILE], exclude: [`${MOBILE}Platforms/`] },
    // Mobile.Shared's platform-neutral root: used by RTST on both OSes and by Dino Reader.
    sharedroot: { include: [SHARED], exclude: [`${SHARED}Platforms/`] },
    // Free store books (Slice 158, Decision 163): the texts, voices, pictures, translations, builders, the .dino format
    // and the upload scripts. dino-store.yml's push paths must cover this list (a node test checks it); never cuts an app release by itself.
    dinostore: {
        include: [
            'src/Dino/DinoReader.Core/',
            'src/Shared/Sirous.Reading.Application/', // LineReflow, used by the book builder (Slice 302)
            'content/dino/',
            'tools/dino/DinoReader.BookBuilder/',
            'tools/dino/upload-store-story.ps1',
            '.github/scripts/publish-store-books.mjs',
            '.github/scripts/r2-wrangler.mjs',
        ],
        // Slice 375 B: the print-size pictures (Git LFS) never enter an app build, but they are packed into the high-res package of a store book, so a change there selects the store upload group.
    },
    dinoreader: {
        include: [DINO_VERSION_CONFIG, 'src/Dino/DinoReader.Core/', 'tests/Dino/DinoReader.Tests/', 'tools/dino/DinoReader.BookBuilder/', '.github/workflows/dinoreader.yml'],
    },
    // Slice 369: each product's own release workflow. Editing one rebuilds that product's targets only (the shared half,
    // release-shared.yml, is in `core` and rebuilds both). release-<product>.yml's push paths and the PR paths of the
    // product's build workflows are derived from these groups.
    rtstrelease: { include: ['.github/workflows/release-rtst.yml'] },
    dinorelease: { include: ['.github/workflows/release-dino.yml'] },
};

function patternToRegExp(pattern) {
    const escaped = pattern.replace(/[.+?^${}()|[\]\\]/g, '\\$&');
    const body = escaped.split('**/').map((part) => part.replace(/\*/g, '[^/]*')).join('(?:.*/)?');
    return new RegExp(`^${body}$`);
}

export function matchesPattern(file, pattern) {
    if (pattern.endsWith('/')) return file.startsWith(pattern);
    if (pattern.includes('*')) return patternToRegExp(pattern).test(file);
    return file === pattern;
}

function groupTouched(changedFiles, { include, exclude = [] }) {
    const hit = (file, patterns) => patterns.some((pattern) => matchesPattern(file, pattern));
    return changedFiles.some((file) => hit(file, include) && !hit(file, exclude));
}

function isIgnored(file) {
    return IGNORED_PATTERNS.some((pattern) => matchesPattern(file, pattern)) && !CONTENT_MARKDOWN.some((pattern) => matchesPattern(file, pattern));
}

function touchedGroups(changedFiles) {
    const relevant = changedFiles.filter((file) => !isIgnored(file));
    return Object.fromEntries(Object.entries(GROUPS).map(([name, group]) => [name, groupTouched(relevant, group)]));
}

/**
 * `changedFiles` is everything since the last release tag (`null`: unknown,
 * which builds everything). `bump` is the pending SemVer bump kind.
 * Returns one boolean per target. Whether anything ships is release-shared.yml's call
 * (windows || android || dinowin || signed ios || signed dinoapp || signed dinoios); dinoreader alone never ships.
 */
export function shipTargets({ changedFiles, bump, forceAll = false }) {
    const everything = forceAll || bump === MAJOR_BUMP || changedFiles === null;
    const g = everything ? null : touchedGroups(changedFiles);
    const windows = everything || g.core || g.windows || g.rtstrelease;
    const android = everything || g.core || g.android || g.mobileshared || g.sharedroot || g.rtstrelease;
    const ios = everything || g.core || g.ios || g.mobileshared || g.sharedroot || g.rtstrelease;
    const dinoreader = everything || g.dinoreader;
    const dinoapp = everything || g.core || g.dinoapp || g.sharedroot || g.dinorelease;
    const dinoios = everything || g.core || g.dinoios || g.sharedroot || g.dinorelease;
    const dinowin = everything || g.core || g.dinowin || g.sharedroot || g.dinorelease;
    const dinostore = everything || g.dinostore;
    // The Microsoft Store only gets a submission for Windows-specific changes: a core-only change
    // rebuilds the MSIX (windows) for the GitHub release but is not worth a Store review.
    const windowsstore = everything || g.windows;
    return { windows, windowsstore, android, ios, dinoreader, dinoapp, dinoios, dinowin, dinostore };
}

/** Test files one project compiles from the other (see the two csproj files): editing one can break the other project. */
const LINKED_TEST_FILES = [
    'tests/Rtst/ReadTheStupidText.Tests/TestFakes.cs',
    'tests/Rtst/ReadTheStupidText.Tests/TranslationFakes.cs',
    'tests/Shared/Shared.Tests/MainThreadStub.cs',
];

/**
 * Slice 368: which of RTST's two .NET suites a change set needs. `shared` (tests/Shared/Shared.Tests) covers Domain, Application,
 * Documents, Infrastructure and Mobile.Shared's pure logic, so any of those selects it; `rtst` (tests/Rtst/ReadTheStupidText.Tests)
 * covers the tray, control panel and the RTST phone app on top of the same engine, so it runs for those and for core changes.
 * The two repo-wide guards (FallbackGuardTests, SherpaVersionPinTests) live in Shared.Tests and scan all of src/, so any code
 * change selects `shared`. Only a change to RTST's own tests (not the files the two projects link) selects `rtst` alone, and a change
 * to the shared tests alone only `shared`. Unknown (`null`) selects both.
 */
export function testSuites(changedFiles) {
    if (changedFiles === null) return { shared: true, rtst: true };
    const g = touchedGroups(changedFiles);
    // FallbackGuardTests and SherpaVersionPinTests (in Shared.Tests) scan all of src/, so every code group selects the shared suite too.
    const code = g.core || g.infrastructure || g.sharedroot || g.windows || g.android || g.ios || g.mobileshared || g.dinoapp || g.dinoios || g.dinowin || g.rtstrelease || g.dinorelease;
    const linked = changedFiles.some((file) => LINKED_TEST_FILES.includes(file));
    const shared = code || g.sharedtests || g.sherpapins || linked;
    const rtst = g.core || g.unittests || g.windows || g.android || g.ios || g.mobileshared || g.sharedroot || g.rtstrelease || linked;
    return { shared, rtst };
}

const appTagPattern = (prefix) => new RegExp(`^${prefix}(\\d+)\\.(\\d+)\\.(\\d+)$`);

/** The newest strict <prefix><x.y.z> tag in a list (null when none). */
export function newestAppTag(tags, prefix) {
    const pattern = appTagPattern(prefix);
    const parse = (tag) => pattern.exec(tag)?.slice(1).map(Number);
    const valid = tags.filter((tag) => parse(tag));
    valid.sort((a, b) => {
        const [pa, pb] = [parse(a), parse(b)];
        return pa[0] - pb[0] || pa[1] - pb[1] || pa[2] - pb[2];
    });
    return valid.at(-1) ?? null;
}

/**
 * 'major' when the version GitVersion computed has a higher major than the
 * base tag, else 'other'. No base tag (null) is treated as major: the first
 * release builds everything.
 */
export function bumpKind(baseTag, newVersion, prefix) {
    const base = appTagPattern(prefix).exec(baseTag ?? '');
    if (!base) return MAJOR_BUMP;
    const next = /^(\d+)\./.exec(newVersion ?? '');
    return next && Number(next[1]) > Number(base[1]) ? MAJOR_BUMP : 'other';
}

function git(args, cwd) {
    return execFileSync('git', args, { encoding: 'utf8', cwd, stdio: ['ignore', 'pipe', 'ignore'], maxBuffer: GIT_MAX_BUFFER }).trim();
}

/** Like `git`, but the output is returned as it is (no trimming), for NUL-separated lists. */
function gitRaw(args, cwd) {
    return execFileSync('git', args, { encoding: 'utf8', cwd, stdio: ['ignore', 'pipe', 'ignore'], maxBuffer: GIT_MAX_BUFFER });
}

/** The newest published tag of a product merged into HEAD (HEAD's own tag included). */
export function previousReleaseTag(prefix, cwd) {
    try {
        return newestAppTag(git(['tag', '--merged', 'HEAD', '--list', `${prefix}*`], cwd).split('\n'), prefix);
    } catch {
        return null;
    }
}

/** Files changed since `base`; `--no-renames` lists both sides of a move. */
export function changedFilesSince(base, cwd) {
    if (!base) return null;
    try {
        const output = git(['diff', '--no-renames', '--name-only', base, 'HEAD'], cwd);
        return output ? output.split('\n') : [];
    } catch {
        return null;
    }
}

/** GitVersion's major-bump commit token, assembled so this file never contains it literally. */
const MAJOR_TOKEN = new RegExp(String.raw`\+` + String.raw`semver:\s?(breaking|major)`, 'i');
const COMMIT_START = '\u0001';
const MESSAGE_END = '\u0002';

/** Commits in `base..HEAD` as `{ message, files }`. */
export function commitsSince(base, cwd) {
    if (!base) return [];
    try {
        const raw = git(['log', '--no-renames', '--name-only', `--format=${COMMIT_START}%B${MESSAGE_END}`, `${base}..HEAD`], cwd);
        return parseCommits(raw);
    } catch {
        return [];
    }
}

export function parseCommits(raw) {
    return raw.split(COMMIT_START).filter((chunk) => chunk.includes(MESSAGE_END)).map((chunk) => {
        const [message, rest] = chunk.split(MESSAGE_END);
        return { message, files: rest.split('\n').map((f) => f.trim()).filter(Boolean) };
    });
}

/**
 * 'major' for a product only when the version moved up a major AND a commit carrying the major token
 * touched one of that product's own targets; a token on the other product's commit does not count.
 * No base tag is always major (first release builds everything).
 */
export function productBump({ baseTag, version, prefix, commits, targets }) {
    if (bumpKind(baseTag, version, prefix) !== MAJOR_BUMP) return 'other';
    if (!baseTag) return MAJOR_BUMP;
    const ownsToken = commits.some((commit) => {
        if (!MAJOR_TOKEN.test(commit.message)) return false;
        const hit = shipTargets({ changedFiles: commit.files, bump: 'other' });
        return targets.some((target) => hit[target]);
    });
    return ownsToken ? MAJOR_BUMP : 'other';
}

function writeOutputs(targets) {
    const lines = Object.entries(targets).map(([key, value]) => `${key}=${value}`);
    console.log(lines.join('\n'));
    if (process.env.GITHUB_OUTPUT) appendFileSync(process.env.GITHUB_OUTPUT, `${lines.join('\n')}\n`);
}

/**
 * Slice 218: the store flag is measured from the last tag a store submission succeeded for
 * (`plan.storeChangedFiles`), not from the last release, so a change in a release whose
 * submission was skipped, failed or cancelled is submitted by the next release. A pending RTST Store
 * submission also needs the MSIX built and released, so it turns `windows` on.
 */
function applySinceLastStore(merged, plan, forceAll, { flag, buildFlag }) {
    const sinceStore = shipTargets({ changedFiles: plan.storeChangedFiles, bump: plan.bump, forceAll });
    merged[flag] = merged[flag] || sinceStore[flag];
    if (buildFlag) merged[buildFlag] = merged[buildFlag] || merged[flag];
}

/**
 * Combines each product's own plan (`plans.rtst` / `plans.dino`, each `{ changedFiles, bump }`) into one
 * flag set: a product's flags come only from the changes since its own last tag.
 */
export function shipTargetsPerProduct({ plans, forceAll = false }) {
    const merged = {};
    // Slice 369: a release workflow plans only its own product, so a product with no plan is left out (its targets stay unset).
    for (const [name, product] of Object.entries(PRODUCTS).filter(([name]) => plans[name])) {
        const all = shipTargets({ ...plans[name], forceAll });
        product.targets.forEach((target) => { merged[target] = all[target]; });
        if (product.store && plans[name].storeChangedFiles) applySinceLastStore(merged, plans[name], forceAll, product.store);
    }
    return merged;
}

/**
 * Slice 369: which release workflows a change set starts work in: `rtst` (release-rtst.yml) when an RTST platform target
 * builds, `dino` (release-dino.yml) when a Dino target does (`dinoreader` alone runs Dino's suite and never ships, but
 * the workflow still starts). `dinostore` is not a release: dino-store.yml publishes books on its own. Unknown (`null`) is both.
 * A docs-only change is neither. (The workflows' own `push.paths` are tested against this, ship-targets.test.mjs.)
 */
export function releaseWorkflows(changedFiles) {
    const hit = shipTargets({ changedFiles, bump: 'other' });
    const started = (name) => PRODUCTS[name].targets.filter((target) => !CONTENT_ONLY_TARGETS.includes(target)).some((target) => hit[target]);
    return { rtst: started('rtst'), dino: started('dino') };
}

/** The files changed since the product's last store marker tag, or undefined (no marker, or the diff failed: the flag then follows the release diff). */
function storeChangesFor(name, cwd) {
    if (!PRODUCTS[name].store) return undefined;
    const { prefix } = PRODUCTS[name].store;
    const storeBase = previousReleaseTag(prefix, cwd);
    if (!storeBase) { console.log(`ship-targets (${name}): no ${prefix}* tag yet, the store flag follows the release diff.`); return undefined; }
    const files = changedFilesSince(storeBase, cwd);
    if (files === null) { console.log(`ship-targets (${name}): cannot diff against ${storeBase}, the store flag follows the release diff.`); return undefined; }
    console.log(`ship-targets (${name}): store changes since ${storeBase}`);
    return files;
}

export function planFor(name, versionEnv, cwd) {
    const { prefix } = PRODUCTS[name];
    const targets = PRODUCTS[name].targets.filter((target) => !CONTENT_ONLY_TARGETS.includes(target));
    const base = previousReleaseTag(prefix, cwd);
    console.log(base ? `ship-targets (${name}): changes since ${base}` : `ship-targets (${name}): no earlier ${prefix}* tag, building everything.`);
    const bump = productBump({ baseTag: base, version: process.env[versionEnv], prefix, commits: commitsSince(base, cwd), targets });
    return { changedFiles: changedFilesSince(base, cwd), bump, storeChangedFiles: storeChangesFor(name, cwd) };
}

/**
 * Files changed in the working tree since the merge base with `base` (committed, uncommitted and untracked), as repo-root paths;
 * null when unknown. Git runs in the top level of the repo that contains `repoRoot` (default: the repo this script lives in),
 * never in the caller's current directory, so a run from another checkout or a subfolder still reads the right tree.
 */
export function localChangedFiles(base, repoRoot = DEFAULT_REPO_ROOT) {
    try {
        const top = git(['rev-parse', '--show-toplevel'], repoRoot);
        const mergeBase = git(['merge-base', 'HEAD', base], top);
        // -z: paths come NUL-separated and unquoted, so a non-ASCII name is not octal-escaped (which would match no pattern).
        const tracked = gitRaw(['diff', '--no-renames', '--name-only', '-z', mergeBase], top);
        const untracked = gitRaw(['ls-files', '--others', '--exclude-standard', '--full-name', '-z'], top);
        const files = [...tracked.split(NUL), ...untracked.split(NUL)].filter(Boolean);
        return [...new Set(files)];
    } catch {
        return null;
    }
}

/** The changed files given on standard input, one per line (`--stdin`); `*` or nothing means unknown. */
function readStdinFiles() {
    const lines = readFileSync(0, 'utf8').split(LINE_BREAK).map((line) => line.trim()).filter(Boolean);
    return lines.length === 0 || lines.includes('*') ? null : lines;
}

/** Reads `--local`, `--base <ref>`, `--repo-root <dir>`, `--files` and `--stdin` from an argument list. */
export function parseLocalArgs(argv) {
    const baseIndex = argv.indexOf('--base');
    const rootIndex = argv.indexOf('--repo-root');
    return {
        repoRoot: rootIndex >= 0 && argv[rootIndex + 1] ? argv[rootIndex + 1] : DEFAULT_REPO_ROOT,
        local: argv.includes('--local'),
        files: argv.includes('--files'),
        stdin: argv.includes('--stdin'),
        tests: argv.includes('--tests'),
        base: baseIndex >= 0 && argv[baseIndex + 1] ? argv[baseIndex + 1] : DEFAULT_LOCAL_BASE,
    };
}

function mainLocal({ base, files, stdin, repoRoot, tests }) {
    const changedFiles = stdin ? readStdinFiles() : localChangedFiles(base, repoRoot);
    // Slice 368: `--tests` prints shared=<bool> and rtst=<bool> (and appends them to $GITHUB_OUTPUT), tests.yml's suite selection.
    if (tests) return writeOutputs(testSuites(changedFiles));
    if (files) {
        // `*` means unknown (git failed); an empty list prints nothing, so a clean tree is not mistaken for a failure.
        if (changedFiles === null) console.log('*');
        else if (changedFiles.length > 0) console.log(changedFiles.join(LINE_BREAK));
        return;
    }
    console.log(Object.entries(shipTargets({ changedFiles, bump: 'other' })).map(([key, value]) => `${key}=${value}`).join(LINE_BREAK));
}

function main() {
    const local = parseLocalArgs(process.argv.slice(2));
    if (local.local) return mainLocal(local);
    writeOutputs(shipTargetsPerProduct({ plans: plansFromEnv(process.env), forceAll: process.env.FORCE_ALL === 'true' }));
}

/**
 * Slice 369: each release workflow plans its own product only (`PRODUCT=rtst|dino`, version in `VERSION`), so the other
 * product's targets stay unset and its tag is never read. Without PRODUCT both are planned (RTST_VERSION / DINO_VERSION).
 */
export function plansFromEnv(env, plan = planFor) {
    if (!env.PRODUCT) return { rtst: plan('rtst', 'RTST_VERSION'), dino: plan('dino', 'DINO_VERSION') };
    if (!PRODUCTS[env.PRODUCT]) throw new Error(`ship-targets: unknown PRODUCT '${env.PRODUCT}' (expected ${Object.keys(PRODUCTS).join(' or ')})`);
    return { [env.PRODUCT]: plan(env.PRODUCT, 'VERSION') };
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    main();
}
