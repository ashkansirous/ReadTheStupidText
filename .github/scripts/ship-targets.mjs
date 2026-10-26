#!/usr/bin/env node
/**
 * Decides which targets a push to main builds (Slice 70, replaces build.yml's
 * `changes` job and its dorny/paths-filter block).
 *
 * The mapping is exactly the one Slices 44/48 settled (Decision 69):
 *   - core (Domain/Application/Documents/solution/pipeline)       -> windows + android + ios
 *   - tests/ReadTheStupidText.Tests                               -> nothing (tests.yml runs it on PRs)
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
 *   - DinoReader.Core / tests/DinoReader.Tests                    -> dinoreader (its tests; Core also rebuilds dinoapp)
 *   - Markdown anywhere (IGNORED_PATTERNS, case-sensitive like GitHub's `paths`) -> nothing (ignored before matching)
 *   - docs and anything else                                      -> nothing
 * `windowsstore` is true only for the Windows group's own paths (a core-only change sets `windows`,
 * so the MSIX is rebuilt for the GitHub release, but is not submitted to the Store).
 * A major version bump builds every target (windowsstore included), and so does a missing base tag
 * or a force_all run.
 *
 * Changes are measured per product (Slice 130, Decision 137) from the newest published tag of that
 * product merged into HEAD (rtst-v<x.y.z> for the RTST targets, dino-v<x.y.z> for the Dino ones;
 * the old shared v<x.y.z> tags are history and ignored), not from the push's own `before`: a failed
 * run, or a pending run dropped by the concurrency queue, is still built next time. Failing safe
 * means building more, never less. A product with no tag builds everything (release.yml's version
 * job creates the baseline tags first, so this only happens if that is skipped).
 *
 * Known gap: changes to publish-downloads.mjs or the download-keys don't map to any target, so they trigger no re-publish. Run release.yml with force_all if
 * the downloads copy needs refreshing.
 *
 * Each platform workflow's own `pull_request.paths` mirrors its rows below (plus a
 * trailing "not Markdown" negation, the Markdown rule above);
 * ship-targets.test.mjs parses the workflows and fails on any drift.
 */

import { execFileSync } from 'node:child_process';
import { appendFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const MOBILE = 'src/ReadTheStupidText.Mobile/';
const SHARED = 'src/ReadTheStupidText.Mobile.Shared/';
const MAJOR_BUMP = 'major';
/** Dino Reader's GitVersion config (the twin of the core GitVersion.yml): a path of every Dino target. */
const DINO_VERSION_CONFIG = 'GitVersion.dino.yml';

/** Each product's tag prefix (also its GitVersion tag-prefix) and the targets its release carries. */
export const PRODUCTS = {
    rtst: { prefix: 'rtst-v', targets: ['windows', 'windowsstore', 'android', 'ios'] },
    dino: { prefix: 'dino-v', targets: ['dinoreader', 'dinoapp', 'dinoios', 'dinowin'] },
};

/** Changed files matching any of these never trigger a build (mirrored as trailing `!` entries in each workflow's `paths`). */
export const IGNORED_PATTERNS = ['**/*.md'];

/** Changes that rebuild every app platform (Windows, Android, iOS and Dino Reader). */
export const CORE_PATHS = [
    'src/ReadTheStupidText.Domain/',
    'src/ReadTheStupidText.Application/',
    'src/ReadTheStupidText.Documents/',
    'global.json',
    'Directory.Build.props',
    'GitVersion.yml',
    '*.slnx',
    '.gitattributes',
    '.github/workflows/release.yml',
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
    // RTST's unit tests: they build and ship nothing, so no target maps here. The group only exists so
    // tests.yml's pull_request.paths (which runs them on PRs) stays derived from GROUPS.
    unittests: { include: ['tests/ReadTheStupidText.Tests/'] },
    windows: {
        include: [
            'src/ReadTheStupidText.App/',
            'src/ReadTheStupidText.Windows.Tray/',
            'src/ReadTheStupidText.Infrastructure/',
            '.github/workflows/store-submit.yml',
            '.github/workflows/windows.yml',
        ],
    },
    android: {
        include: [
            `${MOBILE}Platforms/Android/`,
            `${SHARED}Platforms/Android/`,
            'src/AndroidSherpaPackaging.props',
            'src/AndroidRelease.props',
            'src/AndroidProguard.cfg',
            '.github/scripts/stage-android-package.sh',
            '.github/scripts/check-android-natives.sh',
            '.github/workflows/android.yml',
        ],
    },
    dinoapp: {
        include: [
            DINO_VERSION_CONFIG,
            'src/DinoReader.App/',
            'src/DinoReader.Core/',
            `${SHARED}Platforms/Android/`,
            'src/AndroidSherpaPackaging.props',
            'src/AndroidRelease.props',
            'src/AndroidProguard.cfg',
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
            'src/DinoReader.App/',
            'src/DinoReader.Core/',
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
            'src/DinoReader.App/',
            'src/DinoReader.Core/',
            `${SHARED}Platforms/Windows/`,
            'src/ReadTheStupidText.Infrastructure/',
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
    dinoreader: {
        include: [DINO_VERSION_CONFIG, 'src/DinoReader.Core/', 'tests/DinoReader.Tests/', '.github/workflows/dinoreader.yml'],
    },
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

function touchedGroups(changedFiles) {
    const relevant = changedFiles.filter((file) => !IGNORED_PATTERNS.some((pattern) => matchesPattern(file, pattern)));
    return Object.fromEntries(Object.entries(GROUPS).map(([name, group]) => [name, groupTouched(relevant, group)]));
}

/**
 * `changedFiles` is everything since the last release tag (`null`: unknown,
 * which builds everything). `bump` is the pending SemVer bump kind.
 * Returns one boolean per target. Whether anything ships is release.yml's call
 * (windows || android || dinowin || signed ios || signed dinoapp || signed dinoios); dinoreader alone never ships.
 */
export function shipTargets({ changedFiles, bump, forceAll = false }) {
    const everything = forceAll || bump === MAJOR_BUMP || changedFiles === null;
    const g = everything ? null : touchedGroups(changedFiles);
    const windows = everything || g.core || g.windows;
    const android = everything || g.core || g.android || g.mobileshared || g.sharedroot;
    const ios = everything || g.core || g.ios || g.mobileshared || g.sharedroot;
    const dinoreader = everything || g.dinoreader;
    const dinoapp = everything || g.core || g.dinoapp || g.sharedroot;
    const dinoios = everything || g.core || g.dinoios || g.sharedroot;
    const dinowin = everything || g.core || g.dinowin || g.sharedroot;
    // The Microsoft Store only gets a submission for Windows-specific changes: a core-only change
    // rebuilds the MSIX (windows) for the GitHub release but is not worth a Store review.
    const windowsstore = everything || g.windows;
    return { windows, windowsstore, android, ios, dinoreader, dinoapp, dinoios, dinowin };
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

function git(args) {
    return execFileSync('git', args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
}

/** The newest published tag of a product merged into HEAD (HEAD's own tag included). */
export function previousReleaseTag(prefix) {
    try {
        return newestAppTag(git(['tag', '--merged', 'HEAD', '--list', `${prefix}*`]).split('\n'), prefix);
    } catch {
        return null;
    }
}

/** Files changed since `base`; `--no-renames` lists both sides of a move. */
export function changedFilesSince(base) {
    if (!base) return null;
    try {
        const output = git(['diff', '--no-renames', '--name-only', base, 'HEAD']);
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
export function commitsSince(base) {
    if (!base) return [];
    try {
        const raw = git(['log', '--no-renames', '--name-only', `--format=${COMMIT_START}%B${MESSAGE_END}`, `${base}..HEAD`]);
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
 * Combines each product's own plan (`plans.rtst` / `plans.dino`, each `{ changedFiles, bump }`) into one
 * flag set: a product's flags come only from the changes since its own last tag.
 */
export function shipTargetsPerProduct({ plans, forceAll = false }) {
    const merged = {};
    for (const [name, product] of Object.entries(PRODUCTS)) {
        const all = shipTargets({ ...plans[name], forceAll });
        product.targets.forEach((target) => { merged[target] = all[target]; });
    }
    return merged;
}

function planFor(name, versionEnv) {
    const { prefix, targets } = PRODUCTS[name];
    const base = previousReleaseTag(prefix);
    console.log(base ? `ship-targets (${name}): changes since ${base}` : `ship-targets (${name}): no earlier ${prefix}* tag, building everything.`);
    const bump = productBump({ baseTag: base, version: process.env[versionEnv], prefix, commits: commitsSince(base), targets });
    return { changedFiles: changedFilesSince(base), bump };
}

function main() {
    const plans = { rtst: planFor('rtst', 'RTST_VERSION'), dino: planFor('dino', 'DINO_VERSION') };
    writeOutputs(shipTargetsPerProduct({ plans, forceAll: process.env.FORCE_ALL === 'true' }));
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    main();
}
