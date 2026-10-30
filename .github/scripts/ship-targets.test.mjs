import test from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { CONTENT_MARKDOWN, GROUPS, IGNORED_PATTERNS, PRODUCTS, bumpKind, changedFilesSince, localChangedFiles, matchesPattern, newestAppTag, parseCommits, parseLocalArgs, planFor, productBump, shipTargets, shipTargetsPerProduct } from './ship-targets.mjs';

const M = 'src/ReadTheStupidText.Mobile';
const S = 'src/ReadTheStupidText.Mobile.Shared';
const NONE = { windows: false, windowsstore: false, android: false, ios: false, dinoreader: false, dinoapp: false, dinoios: false, dinowin: false, dinostore: false };
// Core changes rebuild every app platform, Dino Reader's app included (its tests are separate).
const ALL3 = { windows: true, android: true, ios: true, dinoapp: true, dinoios: true, dinowin: true };
const EVERYTHING = { ...ALL3, dinoreader: true, windowsstore: true, dinostore: true };

function targetsOf(changedFiles, extra = {}) {
    return shipTargets({ changedFiles, bump: 'other', ...extra });
}

// Ported from path-filters.test.mjs (the build.yml `changes` job's cases).
const CASES = [
    ['Android platform only', [`${M}/Platforms/Android/AndroidSpeechReader.cs`], { android: true }],
    ['iOS platform only', [`${M}/Platforms/iOS/AppDelegate.cs`], { ios: true }],
    ['iOS signing script', ['.github/scripts/ios-signing.sh'], { ios: true, dinoios: true }],
    ['TestFlight upload script', ['.github/scripts/testflight-upload.sh'], { ios: true, dinoios: true }],
    ['iOS bundle check script', ['.github/scripts/check-ios-bundle.sh'], { dinoios: true }],
    ['Android stage script', ['.github/scripts/stage-android-package.sh'], { android: true, dinoapp: true }],
    ['Android natives check script', ['.github/scripts/check-android-natives.sh'], { android: true, dinoapp: true }],
    ['shared Mobile code', [`${M}/ReaderPage.xaml.cs`], { android: true, ios: true }],
    ['Mobile csproj', [`${M}/ReadTheStupidText.Mobile.csproj`], { android: true, ios: true }],
    ['Mobile resource', [`${M}/Resources/Images/a.png`], { android: true, ios: true }],
    ['Domain core', ['src/ReadTheStupidText.Domain/PlaybackRate.cs'], ALL3],
    ['Application core', ['src/ReadTheStupidText.Application/X.cs'], ALL3],
    ['Documents core', ['src/ReadTheStupidText.Documents/X.cs'], ALL3],
    ['unit tests build and ship nothing', ['tests/ReadTheStupidText.Tests/X.cs'], {}],
    ['solution file', ['ReadTheStupidText.slnx'], ALL3],
    ['global.json', ['global.json'], ALL3],
    ['Directory.Build.props', ['Directory.Build.props'], ALL3],
    ['GitVersion.yml', ['GitVersion.yml'], ALL3],
    ['GitVersion.dino.yml', ['GitVersion.dino.yml'], { dinoreader: true, dinoapp: true, dinoios: true, dinowin: true }],
    ['.gitattributes', ['.gitattributes'], ALL3],
    ['release workflow (the pipeline itself)', ['.github/workflows/release.yml'], ALL3],
    ['find-release script', ['.github/scripts/find-release.sh'], ALL3],
    ['Windows app only', ['src/ReadTheStupidText.App/App.xaml.cs'], { windows: true, windowsstore: true }],
    ['Windows tray library only (no Dino target)', ['src/ReadTheStupidText.Windows.Tray/Input/X.cs'], { windows: true, windowsstore: true }],
    ['Infrastructure only', ['src/ReadTheStupidText.Infrastructure/X.cs'], { windows: true, windowsstore: true, dinowin: true }],
    ['store-submit workflow', ['.github/workflows/store-submit.yml'], { windows: true, windowsstore: true, dinowin: true }],
    ['Dino Reader Windows workflow', ['.github/workflows/dinoreader-windows.yml'], { dinowin: true }],
    ['docs only', ['CLAUDE.md', 'docs/plan/decisions.md', 'plan.md'], {}],
    ['DinoReader only', ['src/DinoReader.Core/Parser.cs'], { dinoreader: true, dinoapp: true, dinoios: true, dinowin: true, dinostore: true }],
    ['store book text', ['content/dino/x-marks-the-treasure/story.md'], { dinoreader: true, dinostore: true }],
    ['store book data', ['content/dino/x-marks-the-treasure/book.json'], { dinoreader: true, dinostore: true }],
    ['store book picture', ['content/dino/x-marks-the-treasure/pictures/1.jpg'], { dinoreader: true, dinostore: true }],
    ['starter translations', ['content/dino/three-little-pigs/translations/fa.json'], { dinoreader: true, dinostore: true }],
    ['book art record is documentation', ['content/dino/x-marks-the-treasure/art.md'], {}],
    ['book builder', ['tools/DinoReader.BookBuilder/Program.cs'], { dinoreader: true, dinostore: true }],
    ['store upload script', ['tools/upload-store-story.ps1'], { dinostore: true }],
    ['store publish script', ['.github/scripts/publish-store-books.mjs'], { dinostore: true }],
    ['R2 wrangler helper', ['.github/scripts/r2-wrangler.mjs'], { dinostore: true }],
    ['DinoReader tests only', ['tests/DinoReader.Tests/ParserTests.cs'], { dinoreader: true }],
    ['Android + iOS platform files', [`${M}/Platforms/Android/a.cs`, `${M}/Platforms/iOS/b.cs`], { android: true, ios: true }],
    ['Windows + DinoReader', ['src/ReadTheStupidText.App/a.cs', 'src/DinoReader.Core/b.cs'], { windows: true, windowsstore: true, dinoreader: true, dinoapp: true, dinoios: true, dinowin: true, dinostore: true }],
    // Slice 58: the shared MAUI library and the Dino Reader app.
    ['Dino Reader app only', ['src/DinoReader.App/MainPage.xaml.cs'], { dinoapp: true, dinoios: true, dinowin: true }],
    ['Dino Reader app csproj', ['src/DinoReader.App/DinoReader.App.csproj'], { dinoapp: true, dinoios: true, dinowin: true }],
    ['Dino Reader iOS workflow', ['.github/workflows/dinoreader-ios.yml'], { dinoios: true }],
    ['iOS sherpa packaging props', [`${S}/IosSherpaPackaging.props`], { android: true, ios: true, dinoapp: true, dinoios: true, dinowin: true }],
    ['Dino Reader Android workflow', ['.github/workflows/dinoreader-android.yml'], { dinoapp: true }],
    ['Mobile.Shared root', [`${S}/CompositeSpeechReader.cs`], { android: true, ios: true, dinoapp: true, dinoios: true, dinowin: true }],
    ['Mobile.Shared csproj', [`${S}/ReadTheStupidText.Mobile.Shared.csproj`], { android: true, ios: true, dinoapp: true, dinoios: true, dinowin: true }],
    ['Mobile.Shared Android platform', [`${S}/Platforms/Android/AndroidSpeechReader.cs`], { android: true, dinoapp: true }],
    ['Mobile.Shared iOS platform', [`${S}/Platforms/iOS/AvSpeechReader.cs`], { ios: true, dinoios: true }],
    ['Mobile.Shared Windows platform', [`${S}/Platforms/Windows/WindowsPlatformServices.cs`], { dinowin: true }],
    ['Mobile.Shared unknown platform folder', [`${S}/Platforms/Tizen/x.cs`], {}],
    ['Android sherpa packaging props', ['src/AndroidSherpaPackaging.props'], { android: true, dinoapp: true }],
    ['Android release props', ['src/AndroidRelease.props'], { android: true, dinoapp: true }],
    ['Android proguard keep rules', ['src/AndroidProguard.cfg'], { android: true, dinoapp: true }],
    ['RTST Mobile Android platform does not rebuild Dino Reader', [`${M}/Platforms/Android/MainActivity.cs`], { android: true }],
    ['RTST Mobile root does not rebuild Dino Reader', [`${M}/MauiProgram.cs`], { android: true, ios: true }],
    ['RTST Mobile iOS platform does not rebuild Dino Reader', [`${M}/Platforms/iOS/AppDelegate.cs`], { ios: true }],
    ['site and worker files', ['worker/downloads.ts', 'site/index.html'], {}],
];

for (const [name, files, expected] of CASES) {
    test(`maps: ${name}`, () => {
        assert.deepEqual(targetsOf(files), { ...NONE, ...expected });
    });
}

test('a core-only change rebuilds the MSIX but does not submit to the Store', () => {
    const t = targetsOf(['src/ReadTheStupidText.Application/X.cs']);
    assert.deepEqual([t.windows, t.windowsstore], [true, false]);
});

test('a Windows App change builds and submits to the Store', () => {
    const t = targetsOf(['src/ReadTheStupidText.App/X.cs', 'src/ReadTheStupidText.Application/X.cs']);
    assert.deepEqual([t.windows, t.windowsstore], [true, true]);
});

test('a tests-only change builds and ships nothing', () => {
    assert.deepEqual(targetsOf(['tests/ReadTheStupidText.Tests/X.cs']), NONE);
});

test('force_all, a major bump and a missing base tag submit to the Store', () => {
    assert.equal(targetsOf([], { forceAll: true }).windowsstore, true);
    assert.equal(targetsOf([], { bump: 'major' }).windowsstore, true);
    assert.equal(targetsOf(null).windowsstore, true);
});

test('a major bump builds every target', () => {
    assert.deepEqual(targetsOf(['docs/x.md'], { bump: 'major' }), EVERYTHING);
});

test('an unknown range (no base tag) builds every target', () => {
    assert.deepEqual(targetsOf(null), EVERYTHING);
});

test('forceAll (manual run) builds every target', () => {
    assert.deepEqual(targetsOf([], { forceAll: true }), EVERYTHING);
});

test('an iOS-only change reports ios and nothing else (release.yml declines to ship it unsigned)', () => {
    assert.deepEqual(targetsOf([`${M}/Platforms/iOS/Info.plist`]), { ...NONE, ios: true });
});

test('DinoReader.Core builds the Dino Reader app (Slice 60) but no RTST platform', () => {
    const t = targetsOf(['src/DinoReader.Core/a.cs']);
    assert.deepEqual([t.windows, t.android, t.ios, t.dinoapp], [false, false, false, true]);
});

test('a store-book change publishes books but ships no app (Slice 158)', () => {
    const t = targetsOf(['content/dino/x-marks-the-treasure/story.md']);
    assert.deepEqual([t.dinostore, t.dinoapp, t.dinoios, t.dinowin, t.windows, t.android, t.ios], [true, false, false, false, false, false, false]);
});

test('a store-book change cannot make a major bump rebuild the product (dinostore is content-only)', () => {
    const commits = [{ message: ['x', '+', 'semver: major'].join(''), files: ['tools/upload-store-story.ps1'] }];
    assert.equal(productBump({ baseTag: 'dino-v1.0.0', version: '2.0.0', prefix: 'dino-v', commits, targets: PRODUCTS.dino.targets.filter((t) => t !== 'dinostore') }), 'other');
});

test('a Dino Reader app change builds nothing else (Windows, RTST Android, iOS)', () => {
    const t = targetsOf(['src/DinoReader.App/App.xaml']);
    assert.deepEqual([t.windows, t.android, t.ios], [false, false, false]);
});

test('a Dino Reader app change builds Dino Reader on every platform (Slice 67)', () => {
    const t = targetsOf(['src/DinoReader.App/App.xaml']);
    assert.deepEqual([t.dinoapp, t.dinoios, t.dinowin], [true, true, true]);
});

test('Mobile/Platforms/** is excluded from the shared group', () => {
    assert.equal(targetsOf([`${M}/Platforms/Windows/x.cs`]).android, false);
    assert.equal(targetsOf([`${M}/MauiProgram.cs`]).ios, true);
});

test('matchesPattern: folder, single-segment glob and exact file', () => {
    assert.equal(matchesPattern('a/b/c.cs', 'a/b/'), true);
    assert.equal(matchesPattern('x.slnx', '*.slnx'), true);
    assert.equal(matchesPattern('sub/x.slnx', '*.slnx'), false);
    assert.equal(matchesPattern('global.json', 'global.json'), true);
    assert.equal(matchesPattern('global.json.bak', 'global.json'), false);
});

test('newestAppTag sorts numerically and ignores non-app tags', () => {
    assert.equal(newestAppTag(['v1.0.9', 'v1.0.10', 'v1.0.2', 'packs-3', 'v1.0.11-rc', ''], 'v'), 'v1.0.10');
    assert.equal(newestAppTag(['packs-1'], 'v'), null);
    assert.equal(newestAppTag(['v2.0.0', 'v10.0.0', 'v9.9.9'], 'v'), 'v10.0.0');
});

test('bumpKind: higher major is major, same major is other, no tag is major', () => {
    assert.equal(bumpKind('rtst-v1.4.2', '2.0.0', PRODUCTS.rtst.prefix), 'major');
    assert.equal(bumpKind('rtst-v1.4.2', '1.4.3', PRODUCTS.rtst.prefix), 'other');
    assert.equal(bumpKind('rtst-v1.4.2', '1.5.0', PRODUCTS.rtst.prefix), 'other');
    assert.equal(bumpKind(null, '1.0.0', PRODUCTS.rtst.prefix), 'major');
});

test('newestAppTag and bumpKind only see their own product prefix (Slice 130)', () => {
    const tags = ['v1.52.0', 'rtst-v1.53.0', 'dino-v1.70.1', 'rtst-v1.9.0', 'dino-v1.9.0', 'packs-7'];
    assert.equal(newestAppTag(tags, PRODUCTS.rtst.prefix), 'rtst-v1.53.0');
    assert.equal(newestAppTag(tags, PRODUCTS.dino.prefix), 'dino-v1.70.1');
    assert.equal(newestAppTag(['v1.52.0', 'dino-v1.1.0'], PRODUCTS.rtst.prefix), null);
    assert.equal(bumpKind('dino-v1.70.1', '2.0.0', PRODUCTS.dino.prefix), 'major');
    assert.equal(bumpKind('v1.70.1', '1.70.2', PRODUCTS.dino.prefix), 'major', 'a legacy tag is not a dino base');
});

const NO_RTST = { windows: false, windowsstore: false, android: false, ios: false };
const NO_DINO = { dinoreader: false, dinoapp: false, dinoios: false, dinowin: false, dinostore: false };
const plan = (rtstFiles, dinoFiles) => ({ rtst: { changedFiles: rtstFiles, bump: 'other' }, dino: { changedFiles: dinoFiles, bump: 'other' } });

test('per product: each product reads only the changes since its own tag', () => {
    const dinoOnly = ['src/DinoReader.App/App.xaml'];
    const rtstOnly = ['src/ReadTheStupidText.App/App.xaml.cs'];
    // RTST was tagged after the Dino change, Dino after the RTST one: each sees only its own.
    assert.deepEqual(shipTargetsPerProduct({ plans: plan([], dinoOnly) }), { ...NO_RTST, ...NO_DINO, dinoapp: true, dinoios: true, dinowin: true });
    assert.deepEqual(shipTargetsPerProduct({ plans: plan(rtstOnly, []) }), { ...NO_RTST, ...NO_DINO, windows: true, windowsstore: true });
});

test('per product: a core change cuts both, a Dino-only change leaves RTST untouched', () => {
    const core = ['src/ReadTheStupidText.Application/X.cs'];
    const both = shipTargetsPerProduct({ plans: plan(core, core) });
    assert.deepEqual([both.windows, both.android, both.ios, both.dinoapp, both.dinoios, both.dinowin], [true, true, true, true, true, true]);
    const dino = shipTargetsPerProduct({ plans: plan([], ['src/DinoReader.Core/Parser.cs']) });
    assert.deepEqual([dino.windows, dino.android, dino.ios, dino.windowsstore], [false, false, false, false]);
});

test('per product: a major bump or force_all builds that product (or both) only', () => {
    const plans = plan([], []);
    plans.dino.bump = 'major';
    const majorDino = shipTargetsPerProduct({ plans });
    assert.deepEqual([majorDino.windows, majorDino.dinoapp, majorDino.dinoreader], [false, true, true]);
    assert.equal(shipTargetsPerProduct({ plans: plan([], []), forceAll: true }).windowsstore, true);
    assert.equal(Object.keys(shipTargetsPerProduct({ plans: plan([], []) })).length, 9);
});

// ---- pull_request.paths parity: each workflow mirrors its GROUPS rows ----

const WORKFLOWS_DIR = fileURLToPath(new URL('../workflows/', import.meta.url));

/**
 * Reads `on.pull_request.paths` from a workflow with a deliberately tiny
 * parser. It accepts exactly the shape these workflows use (2-space indent,
 * `branches: [main]`, a `paths:` list of single-quoted items) and throws on
 * anything else, so a reshaped workflow can't silently pass.
 */
function readPullRequestPaths(file) {
    const lines = readFileSync(`${WORKFLOWS_DIR}${file}`, 'utf8').split(/\r?\n/);
    const onAt = lines.findIndex((line) => line === 'on:');
    assert.notEqual(onAt, -1, `${file}: no top-level "on:" block`);
    const onBlock = [];
    for (const line of lines.slice(onAt + 1)) {
        if (/^\S/.test(line)) break;
        onBlock.push(line);
    }
    const prAt = onBlock.findIndex((line) => line === '  pull_request:');
    assert.notEqual(prAt, -1, `${file}: no "  pull_request:" trigger`);
    const paths = [];
    let sawPaths = false;
    for (const line of onBlock.slice(prAt + 1)) {
        if (/^ {0,2}\S/.test(line)) break;
        if (/^\s*(#.*)?$/.test(line)) continue;
        if (line === '    branches: [main]') continue;
        if (line === '    paths:') { sawPaths = true; continue; }
        const item = /^ {6}- '([^']+)'\s*(#.*)?$/.exec(line);
        assert.ok(item && sawPaths, `${file}: unrecognised line in pull_request: ${JSON.stringify(line)}`);
        paths.push(item[1]);
    }
    assert.ok(sawPaths && paths.length > 0, `${file}: pull_request has no paths list`);
    return paths;
}

const globOf = (pattern) => (pattern.endsWith('/') ? `${pattern}**` : pattern);

/** The PR paths implied by a set of GROUPS: includes as globs, excludes negated. */
function derivedPaths(groupNames) {
    const wanted = new Set();
    for (const name of groupNames) {
        const { include, exclude = [] } = GROUPS[name];
        include.forEach((pattern) => wanted.add(globOf(pattern)));
        exclude.forEach((pattern) => wanted.add(`!${globOf(pattern)}`));
    }
    return wanted;
}

const PR_TARGETS = {
    'windows.yml': ['core', 'windows'],
    'android.yml': ['core', 'android', 'mobileshared', 'sharedroot'],
    'ios.yml': ['core', 'ios', 'mobileshared', 'sharedroot'],
    'dinoreader-android.yml': ['core', 'dinoapp', 'sharedroot'],
    'dinoreader-ios.yml': ['core', 'dinoios', 'sharedroot'],
    'dinoreader-windows.yml': ['core', 'dinowin', 'sharedroot'],
    // tests.yml is RTST's suite (Slice 128): it gates the Windows, Android and iOS builds, never Dino alone.
    'tests.yml': ['core', 'unittests', 'windows', 'android', 'ios', 'mobileshared', 'sharedroot'],
    // dinoreader.yml is Dino's suite: it runs for every Dino target (core is in each of them).
    'dinoreader.yml': ['core', 'dinoreader', 'dinoapp', 'dinoios', 'dinowin', 'sharedroot'],
};

for (const [file, groups] of Object.entries(PR_TARGETS)) {
    test(`${file}: pull_request.paths equals the paths derived from GROUPS (${groups.join(' + ')})`, () => {
        const actual = readPullRequestPaths(file);
        const own = `.github/workflows/${file}`;
        const expected = derivedPaths(groups);
        expected.add(own); // a workflow always re-runs on its own edits
        IGNORED_PATTERNS.forEach((pattern) => expected.add(`!${pattern}`));
        if (file === 'dinoreader.yml') CONTENT_MARKDOWN.forEach((pattern) => expected.add(pattern)); // a book's text is Markdown that must still run its suite
        assert.equal(new Set(actual).size, actual.length, `${file}: duplicate path entries`);
        assert.deepEqual([...new Set(actual)].sort(), [...expected].sort());
    });
}

const MOBILE_ROOTS = ['src/ReadTheStupidText.Mobile', 'src/ReadTheStupidText.Mobile.Shared'];

test('a negated path follows its root glob and precedes the Platforms re-includes', () => {
    const roots = { 'android.yml': MOBILE_ROOTS, 'ios.yml': MOBILE_ROOTS, 'tests.yml': MOBILE_ROOTS, 'dinoreader-android.yml': [MOBILE_ROOTS[1]], 'dinoreader-ios.yml': [MOBILE_ROOTS[1]], 'dinoreader-windows.yml': [MOBILE_ROOTS[1]] };
    for (const [file, folders] of Object.entries(roots)) {
        const paths = readPullRequestPaths(file);
        for (const root of folders) {
            const excluded = paths.indexOf(`!${root}/Platforms/**`);
            assert.ok(excluded > paths.indexOf(`${root}/**`) && paths.indexOf(`${root}/**`) >= 0, `${file}: ! must follow the ${root} glob`);
            const reincludes = paths.filter((p) => p.startsWith(`${root}/Platforms/`) && !p.startsWith('!'));
            assert.ok(reincludes.every((p) => paths.indexOf(p) > excluded), `${file}: ${root} Platforms re-includes must follow the !`);
        }
    }
});

test('every pull_request.paths list ends with the Markdown negation', () => {
    for (const file of [...Object.keys(PR_TARGETS), 'release-scripts.yml']) {
        const paths = readPullRequestPaths(file).filter((path) => !CONTENT_MARKDOWN.includes(path)); // re-includes of content Markdown follow the negation
        const tail = paths.slice(-IGNORED_PATTERNS.length);
        assert.deepEqual(tail, IGNORED_PATTERNS.map((pattern) => `!${pattern}`), `${file}: ignore negations must be last`);
    }
});

test('a Markdown-only change set builds nothing', () => {
    const files = [
        'src/ReadTheStupidText.App/a.md',
        'src/ReadTheStupidText.Infrastructure/a.md',
        'src/DinoReader.App/a.md',
        'src/DinoReader.Core/a.md',
        'tests/DinoReader.Tests/a.md',
        `${M}/CLAUDE.md`,
        `${M}/Platforms/Android/a.md`,
        `${M}/Platforms/iOS/a.md`,
        `${S}/CLAUDE.md`,
        'src/ReadTheStupidText.Application/a.md',
    ];
    assert.deepEqual(targetsOf(files), NONE);
});

test('the Markdown ignore is case-sensitive, like GitHub paths globs', () => {
    assert.deepEqual(targetsOf(['src/ReadTheStupidText.Domain/README.MD']), { ...NONE, ...ALL3 });
});

test('Markdown mixed with code still builds the code targets', () => {
    assert.deepEqual(targetsOf([`${M}/CLAUDE.md`, `${M}/Platforms/iOS/AppDelegate.cs`]), { ...NONE, ios: true });
});

// ---- Slice 130: a major-bump token only bumps the product whose paths its commit touches ----

const MAJOR_MESSAGE = ['feat: x', '', '+' + 'semver: major'].join('\n');
const dinoCommit = { message: MAJOR_MESSAGE, files: ['src/DinoReader.App/App.xaml'] };
const coreCommit = { message: MAJOR_MESSAGE, files: ['src/ReadTheStupidText.Domain/X.cs'] };
const bumpOf = (name, version, commits) => productBump({
    baseTag: `${PRODUCTS[name].prefix}1.0.0`, version, prefix: PRODUCTS[name].prefix, commits, targets: PRODUCTS[name].targets,
});

test('productBump: a major token on a Dino-only commit bumps Dino but not RTST', () => {
    assert.equal(bumpOf('dino', '2.0.0', [dinoCommit]), 'major');
    assert.equal(bumpOf('rtst', '2.0.0', [dinoCommit]), 'other');
});

test('productBump: a major token on a core commit bumps both products', () => {
    assert.equal(bumpOf('dino', '2.0.0', [coreCommit]), 'major');
    assert.equal(bumpOf('rtst', '2.0.0', [coreCommit]), 'major');
});

test('productBump: no major version move, or no token, is not a major', () => {
    assert.equal(bumpOf('dino', '1.0.1', [dinoCommit]), 'other');
    assert.equal(bumpOf('dino', '2.0.0', [{ message: 'fix: y', files: dinoCommit.files }]), 'other');
    assert.equal(productBump({ baseTag: null, version: '1.0.0', prefix: 'dino-v', commits: [], targets: PRODUCTS.dino.targets }), 'major');
});

test('parseCommits splits messages from their changed files', () => {
    const raw = '\u0001fix: a\n\u0002\n\nsrc/a.cs\nsrc/b.cs\n\u0001fix: b\n\u0002\n\nsrc/c.cs\n';
    const commits = parseCommits(raw);
    assert.deepEqual(commits.map((c) => c.files), [['src/a.cs', 'src/b.cs'], ['src/c.cs']]);
});

// Local mode (Slice 180).
test('parseLocalArgs reads --local, --base and --files, defaulting the base to origin/main', () => {
    const { repoRoot, ...plain } = parseLocalArgs(['--local']);
    assert.ok(repoRoot);
    assert.deepEqual(plain, { local: true, files: false, stdin: false, base: 'origin/main' });
    const { repoRoot: explicit, ...withFlags } = parseLocalArgs(['--local', '--base', 'main', '--files', '--repo-root', 'X']);
    assert.equal(explicit, 'X');
    assert.deepEqual(withFlags, { local: true, files: true, stdin: false, base: 'main' });
    assert.equal(parseLocalArgs([]).local, false);
});

test('localChangedFiles lists committed, uncommitted and untracked files since the merge base; an unknown base is null', () => {
    const dir = mkdtempSync(join(tmpdir(), 'ship-local-'));
    const run = (...args) => execFileSync('git', args, { cwd: dir, stdio: 'ignore' });
    try {
        run('init', '-b', 'main');
        run('config', 'user.email', 't@example.com');
        run('config', 'user.name', 't');
        writeFileSync(join(dir, 'a.txt'), 'a');
        run('add', '.');
        run('commit', '-m', 'base');
        run('checkout', '-b', 'topic');
        writeFileSync(join(dir, 'b.txt'), 'b');
        run('add', '.');
        run('commit', '-m', 'topic');
        writeFileSync(join(dir, 'a.txt'), 'a2');
        writeFileSync(join(dir, 'c.txt'), 'c');
        mkdirSync(join(dir, 'sub'));
        writeFileSync(join(dir, 'sub', 'd.txt'), 'd');
        // The repo is named explicitly, the caller's own directory (this checkout) is never read; a subfolder gives repo-root paths.
        assert.deepEqual(localChangedFiles('main', dir).sort(), ['a.txt', 'b.txt', 'c.txt', 'sub/d.txt']);
        assert.deepEqual(localChangedFiles('main', join(dir, 'sub')).sort(), ['a.txt', 'b.txt', 'c.txt', 'sub/d.txt']);
        assert.equal(localChangedFiles('no-such-branch', dir), null);
    } finally { rmSync(dir, { recursive: true, force: true }); }
});

test('localChangedFiles returns non-ASCII paths as they are, not quoted and octal-escaped', () => {
    const dir = mkdtempSync(join(tmpdir(), 'ship-local-'));
    const run = (...args) => execFileSync('git', args, { cwd: dir, stdio: 'ignore' });
    try {
        run('init', '-b', 'main');
        run('config', 'user.email', 't@example.com');
        run('config', 'user.name', 't');
        run('config', 'core.quotePath', 'true');
        writeFileSync(join(dir, 'a.txt'), 'a');
        run('add', '.');
        run('commit', '-m', 'base');
        mkdirSync(join(dir, 'content', 'dino', 'caf\u00e9'), { recursive: true });
        writeFileSync(join(dir, 'content', 'dino', 'caf\u00e9', 'story.md'), 'x');
        writeFileSync(join(dir, 'caf\u00e9.txt'), 'x');
        run('add', 'caf\u00e9.txt');
        assert.deepEqual(localChangedFiles('main', dir).sort(), ['caf\u00e9.txt', 'content/dino/caf\u00e9/story.md']);
    } finally { rmSync(dir, { recursive: true, force: true }); }
});

test('localChangedFiles returns an empty list for a clean tree', () => {
    const dir = mkdtempSync(join(tmpdir(), 'ship-local-'));
    const run = (...args) => execFileSync('git', args, { cwd: dir, stdio: 'ignore' });
    try {
        run('init', '-b', 'main');
        run('config', 'user.email', 't@example.com');
        run('config', 'user.name', 't');
        writeFileSync(join(dir, 'a.txt'), 'a');
        run('add', '.');
        run('commit', '-m', 'base');
        assert.deepEqual(localChangedFiles('main', dir), []);
    } finally { rmSync(dir, { recursive: true, force: true }); }
});

test('local mode: a Dino-only UI change touches the Dino targets and not the RTST ones; unknown changes build everything', () => {
    const dino = targetsOf(['src/DinoReader.App/Views/ReaderPage.xaml']);
    assert.deepEqual([dino.dinoapp, dino.dinowin, dino.android, dino.windows], [true, true, false, false]);
    assert.ok(Object.values(targetsOf(null)).every(Boolean));
});

test('Slice 218: a Windows change in a release the Store never got is submitted by the next core-only release', () => {
    const infra = 'src/ReadTheStupidText.Infrastructure/Reading/SherpaEngineFactory.cs';
    const core = 'src/ReadTheStupidText.Application/X.cs';
    // Last Store submission was before the Infrastructure change; the last release (the base of `changedFiles`) is a core-only one after it.
    const plans = plan([core], []);
    plans.rtst.storeChangedFiles = [infra, core];
    const next = shipTargetsPerProduct({ plans });
    assert.deepEqual([next.windows, next.windowsstore], [true, true]);
    // Without the Store base (the old behaviour) the same release is skipped.
    assert.equal(shipTargetsPerProduct({ plans: plan([core], []) }).windowsstore, false);
});

test('Slice 218: nothing Windows-specific since the last Store submission submits nothing; a Store-only backlog still builds the MSIX', () => {
    const core = 'src/ReadTheStupidText.Application/X.cs';
    const plans = plan([core], []);
    plans.rtst.storeChangedFiles = [core];
    assert.equal(shipTargetsPerProduct({ plans }).windowsstore, false);
    // The release diff is empty (e.g. docs only) but a Windows change is still unsubmitted: the release must build and carry the MSIX.
    const backlog = plan([], []);
    backlog.rtst.storeChangedFiles = ['src/ReadTheStupidText.App/App.xaml.cs'];
    const flags = shipTargetsPerProduct({ plans: backlog });
    assert.deepEqual([flags.windows, flags.windowsstore, flags.android], [true, true, false]);
});

test('Slice 218: the Store marker tag is not an rtst-v release tag', () => {
    assert.equal(newestAppTag(['rtst-store-v1.80.0', 'rtst-v1.76.1'], PRODUCTS.rtst.prefix), 'rtst-v1.76.1');
    assert.equal(newestAppTag(['rtst-store-v1.80.0', 'rtst-v1.76.1'], PRODUCTS.rtst.store.prefix), 'rtst-store-v1.80.0');
});

const INFRA = 'src/ReadTheStupidText.Infrastructure/Reading/SherpaEngineFactory.cs';
const CORE = 'src/ReadTheStupidText.Application/X.cs';

/** A throwaway repo; `commit(file, tag...)` adds a change under that path and tags it. */
function withRepo(body) {
    const dir = mkdtempSync(join(tmpdir(), 'ship-store-'));
    const run = (...args) => execFileSync('git', args, { cwd: dir, stdio: 'ignore' });
    let n = 0;
    const commit = (file, ...tags) => {
        mkdirSync(join(dir, file, '..'), { recursive: true });
        writeFileSync(join(dir, file), String(n++));
        run('add', '.');
        run('commit', '-m', `c${n}`);
        tags.forEach((tag) => run('tag', tag));
    };
    try {
        run('init', '-b', 'main');
        run('config', 'user.email', 't@example.com');
        run('config', 'user.name', 't');
        run('config', 'commit.gpgsign', 'false');
        run('config', 'tag.gpgsign', 'false');
        body(dir, commit);
    } finally {
        rmSync(dir, { recursive: true, force: true });
    }
}

function flagsFor(dir) {
    const plans = { rtst: planFor('rtst', 'RTST_VERSION_UNSET', dir), dino: planFor('dino', 'DINO_VERSION_UNSET', dir) };
    return { plans, flags: shipTargetsPerProduct({ plans }) };
}

test('Slice 218 (git): core, then Windows with a failed Store submission, then core, submits the Windows change', () => {
    withRepo((dir, commit) => {
        commit('README.txt', 'rtst-v1.0.0', 'rtst-store-v1.0.0', 'dino-v1.0.0');
        commit(CORE, 'rtst-v1.1.0');
        commit(INFRA, 'rtst-v1.2.0'); // released, but no rtst-store-v1.2.0: the submission was skipped or failed
        commit(CORE);
        const { plans, flags } = flagsFor(dir);
        assert.deepEqual(plans.rtst.changedFiles, [CORE]);
        assert.deepEqual(plans.rtst.storeChangedFiles.sort(), [CORE, INFRA]);
        assert.deepEqual([flags.windows, flags.windowsstore], [true, true]);
    });
});

test('Slice 218 (git): with the marker at the newest release, a core-only release submits nothing', () => {
    withRepo((dir, commit) => {
        commit(INFRA, 'rtst-v1.0.0', 'rtst-store-v1.0.0', 'dino-v1.0.0');
        commit(CORE);
        const { flags } = flagsFor(dir);
        assert.deepEqual([flags.windows, flags.windowsstore], [true, false]);
    });
});

test('Slice 218 (git): no marker tag falls back to the release diff, never to shipping everything', () => {
    withRepo((dir, commit) => {
        commit(INFRA, 'rtst-v1.0.0', 'dino-v1.0.0');
        commit(CORE);
        const { plans, flags } = flagsFor(dir);
        assert.equal(plans.rtst.storeChangedFiles, undefined);
        assert.deepEqual([flags.windows, flags.windowsstore, flags.android], [true, false, true]);
        assert.deepEqual([flags.dinoios, flags.dinostore], [true, false]);
    });
});

test('Slice 218 (git): an unreadable store diff is null, which leaves the plan on the release diff', () => {
    withRepo((dir, commit) => {
        commit(CORE, 'rtst-v1.0.0', 'dino-v1.0.0');
        assert.equal(changedFilesSince('rtst-store-v9.9.9', dir), null);
        const plans = plan([CORE], []);
        plans.rtst.storeChangedFiles = changedFilesSince('rtst-store-v9.9.9', dir) ?? undefined;
        assert.equal(shipTargetsPerProduct({ plans }).windowsstore, false);
    });
});
