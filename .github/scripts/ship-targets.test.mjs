import test from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { CONTENT_MARKDOWN, GROUPS, IGNORED_PATTERNS, PRODUCTS, bumpKind, changedFilesSince, localChangedFiles, matchesPattern, newestAppTag, parseCommits, parseLocalArgs, planFor, plansFromEnv, productBump, releaseWorkflows, shipTargets, shipTargetsPerProduct, testSuites } from './ship-targets.mjs';

// Slice 265: every book is store-only content (Slice 299: dino-starters.json is gone); the book stems are read from the real content folders, so a renamed book cannot leave these tests empty.
const CONTENT_DIR = fileURLToPath(new URL('../../content/dino/', import.meta.url));
const BOOK_STEMS = readdirSync(CONTENT_DIR).filter((stem) => existsSync(join(CONTENT_DIR, stem, 'book.json')));
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
    ['Application core (also selects the store job: LineReflow, Slice 302)', ['src/ReadTheStupidText.Application/X.cs'], { ...ALL3, dinostore: true }],
    ['Documents core', ['src/ReadTheStupidText.Documents/X.cs'], ALL3],
    ['unit tests build and ship nothing', ['tests/ReadTheStupidText.Tests/X.cs'], {}],
    ['shared unit tests build and ship nothing', ['tests/Shared.Tests/X.cs'], {}],
    // Slice 316 item 8: no build or test job goes through the solution file (each builds its own csproj), so editing it, e.g. a Dino-only line, ships nothing.
    ['solution file builds and ships nothing', ['ReadTheStupidText.slnx'], {}],
    ['global.json', ['global.json'], ALL3],
    ['Directory.Build.props', ['Directory.Build.props'], ALL3],
    ['sherpa-onnx version props (Slice 245)', ['src/SherpaOnnxVersion.props'], ALL3],
    ['GitVersion.yml', ['GitVersion.yml'], ALL3],
    ['GitVersion.dino.yml', ['GitVersion.dino.yml'], { dinoreader: true, dinoapp: true, dinoios: true, dinowin: true }],
    ['.gitattributes', ['.gitattributes'], ALL3],
    ['shared release workflow (the pipeline itself, both products)', ['.github/workflows/release-shared.yml'], ALL3],
    ['RTST release workflow rebuilds RTST only (Slice 369)', ['.github/workflows/release-rtst.yml'], { windows: true, android: true, ios: true }],
    ['Dino release workflow rebuilds Dino Reader only (Slice 369)', ['.github/workflows/release-dino.yml'], { dinoapp: true, dinoios: true, dinowin: true }],
    ['find-release script', ['.github/scripts/find-release.sh'], ALL3],
    ['Windows app only', ['src/ReadTheStupidText.App/App.xaml.cs'], { windows: true, windowsstore: true }],
    ['Windows tray library only (no Dino target)', ['src/ReadTheStupidText.Windows.Tray/Input/X.cs'], { windows: true, windowsstore: true }],
    ['Infrastructure only', ['src/ReadTheStupidText.Infrastructure/X.cs'], { windows: true, windowsstore: true, dinowin: true }],
    ['store-submit workflow', ['.github/workflows/store-submit.yml'], { windows: true, windowsstore: true, dinowin: true }],
    ['Dino Reader Windows workflow', ['.github/workflows/dinoreader-windows.yml'], { dinowin: true }],
    ['docs only', ['CLAUDE.md', 'docs/plan/decisions.md', 'plan.md'], {}],
    ['DinoReader only', ['src/DinoReader.Core/Parser.cs'], { dinoreader: true, dinoapp: true, dinoios: true, dinowin: true, dinostore: true }],
    // Slice 239: a store book (any folder that is not a starter) runs only publish-store: no suite, no app, no PR check.
    ['store book text', ['content/dino/x-marks-the-treasure/story.md'], { dinostore: true }],
    ['store book data', ['content/dino/x-marks-the-treasure/book.json'], { dinostore: true }],
    ['store book titles', ['content/dino/x-marks-the-treasure/titles.json'], { dinostore: true }],
    ['store book picture', ['content/dino/x-marks-the-treasure/pictures/1.jpg'], { dinostore: true }],
    ['a new store book folder', ['content/dino/brand-new-book/book.json', 'content/dino/brand-new-book/story.md', 'content/dino/brand-new-book/translations/fa.json'], { dinostore: true }],
    ['a deleted store book folder', ['content/dino/freddy-and-the-slipper/book.json', 'content/dino/freddy-and-the-slipper/pictures/3.jpg', 'content/dino/freddy-and-the-slipper/story.md'], { dinostore: true }],
    // Slice 265: the four former starters are store-only content too; an audio clip, a manifest or a cast change starts no app build and no test workflow.
    ['former starter translations', ['content/dino/three-little-pigs/translations/fa.json'], { dinostore: true }],
    ['former starter text', ['content/dino/three-little-pigs/story.md'], { dinostore: true }],
    ['an audio clip only', ['content/dino/three-little-pigs/audio/fa/0a1b2c3d4e5f6071-Hali-gemini-tts-6de2d2f6.m4a'], { dinostore: true }],
    ['an audio manifest only', ['content/dino/x-marks-the-treasure/audio/ja/manifest.json'], { dinostore: true }],
    ['audio clips of several books and languages', ['content/dino/good-enough/audio/fa/a.m4a', 'content/dino/tortoise-and-hare/audio/de/b.m4a', 'content/dino/tortoise-and-hare/audio-cast.json'], { dinostore: true }],
    ['a former starter change with its rebuilt package ships the apps', ['content/dino/three-little-pigs/story.md', 'src/DinoReader.App/Resources/Raw/starters/three-little-pigs.dino'], { dinostore: true, dinoapp: true, dinoios: true, dinowin: true }],
    ['a high-res print picture ships nothing (Slice 350)', ['content/dino/lion-and-mouse/high-res/pictures/5.png', 'content/dino/lion-and-mouse/high-res/cover.png'], {}],
    ['a high-res picture next to a page picture still ships the store book', ['content/dino/lion-and-mouse/high-res/pictures/5.png', 'content/dino/lion-and-mouse/pictures/5.jpg'], { dinostore: true }],
    ['a book folder that merely contains the words high-res elsewhere still ships', ['content/dino/lion-and-mouse/pictures/high-res.jpg'], { dinostore: true }],
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

// Slice 368: tests.yml runs the shared suite (tests/Shared.Tests) and RTST's own (tests/ReadTheStupidText.Tests); testSuites picks which.
const SUITE_CASES = [
    ['a shared engine file (Application) selects both suites', ['src/ReadTheStupidText.Application/Reading/X.cs'], { shared: true, rtst: true }],
    ['Domain selects both', ['src/ReadTheStupidText.Domain/X.cs'], { shared: true, rtst: true }],
    ['Documents selects both', ['src/ReadTheStupidText.Documents/X.cs'], { shared: true, rtst: true }],
    ['Infrastructure selects both', ['src/ReadTheStupidText.Infrastructure/Reading/X.cs'], { shared: true, rtst: true }],
    ['Mobile.Shared root code selects both', [`${S}/PackDownloadHost.cs`], { shared: true, rtst: true }],
    // The repo-wide guards (FallbackGuardTests, SherpaVersionPinTests) live in Shared.Tests and scan all of src/.
    ['a tray change selects both suites (the guards scan it)', ['src/ReadTheStupidText.Windows.Tray/Input/X.cs'], { shared: true, rtst: true }],
    ['an App change selects both suites', ['src/ReadTheStupidText.App/X.xaml.cs'], { shared: true, rtst: true }],
    ['the RTST phone app selects both suites', [`${M}/ReaderPage.xaml.cs`], { shared: true, rtst: true }],
    ['the Android packaging props select both suites (SherpaVersionPinTests reads them)', ['src/AndroidSherpaPackaging.props'], { shared: true, rtst: true }],
    ['the pin checker selects the shared suite only (SherpaVersionPinTests runs it)', ['.github/scripts/check-sherpa-pins.mjs'], { shared: true, rtst: false }],
    ['packs.yml (holds a sherpa-onnx pin) selects the shared suite only', ['.github/workflows/packs.yml'], { shared: true, rtst: false }],
    ['the sound-words requirements (a sherpa-onnx pin) select the shared suite only', ['tools/sound-words-test/requirements.txt'], { shared: true, rtst: false }],
    ['Dino app code selects the shared suite only (the guards scan it)', ['src/DinoReader.App/X.cs'], { shared: true, rtst: false }],
    ['TestFakes.cs (linked into both test projects) selects both', ['tests/ReadTheStupidText.Tests/TestFakes.cs'], { shared: true, rtst: true }],
    ['TranslationFakes.cs (linked) selects both', ['tests/ReadTheStupidText.Tests/TranslationFakes.cs'], { shared: true, rtst: true }],
    ['MainThreadStub.cs (linked) selects both', ['tests/Shared.Tests/MainThreadStub.cs'], { shared: true, rtst: true }],
    ['a change to RTST tests only selects only the RTST suite', ['tests/ReadTheStupidText.Tests/X.cs'], { shared: false, rtst: true }],
    ['a change to the shared tests only selects only the shared suite', ['tests/Shared.Tests/X.cs'], { shared: true, rtst: false }],
    ['Dino Reader tests alone select neither .NET suite here (dinoreader.yml owns them)', ['tests/DinoReader.Tests/ParserTests.cs'], { shared: false, rtst: false }],
    ['Markdown selects nothing', ['src/ReadTheStupidText.Application/CLAUDE.md'], { shared: false, rtst: false }],
    ['an unknown range selects both', null, { shared: true, rtst: true }],
];

for (const [name, files, expected] of SUITE_CASES) {
    test(`testSuites: ${name}`, () => {
        assert.deepEqual(testSuites(files), expected);
    });
}

test('--tests prints shared and rtst for a file list on standard input', () => {
    const out = execFileSync('node', [fileURLToPath(new URL('./ship-targets.mjs', import.meta.url)), '--local', '--tests', '--stdin'], { input: 'tests/ReadTheStupidText.Tests/X.cs\n', encoding: 'utf8' });
    assert.equal(out.trim(), 'shared=false\nrtst=true');
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

test('an iOS-only change reports ios and nothing else (release-shared.yml declines to ship it unsigned)', () => {
    assert.deepEqual(targetsOf([`${M}/Platforms/iOS/Info.plist`]), { ...NONE, ios: true });
});

test('DinoReader.Core builds the Dino Reader app (Slice 60) but no RTST platform', () => {
    const t = targetsOf(['src/DinoReader.Core/a.cs']);
    assert.deepEqual([t.windows, t.android, t.ios, t.dinoapp], [false, false, false, true]);
});

test('Slice 239: a store-only change starts no suite, no app and no RTST target', () => {
    const t = targetsOf(['content/dino/new-book/book.json', 'content/dino/new-book/story.md', 'content/dino/old-book/pictures/1.jpg']);
    assert.deepEqual(t, { ...NONE, dinostore: true });
});

test('Slice 302: an Application-only change selects the store job (LineReflow)', () => {
    assert.equal(targetsOf(['src/ReadTheStupidText.Application/LineReflow.cs']).dinostore, true);
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

// Slice 369: rtstrelease / dinorelease are release-rtst.yml / release-dino.yml; the shared half, release-shared.yml, is in `core`.
const PR_TARGETS = {
    'windows.yml': ['core', 'windows', 'rtstrelease'],
    'android.yml': ['core', 'android', 'mobileshared', 'sharedroot', 'rtstrelease'],
    'ios.yml': ['core', 'ios', 'mobileshared', 'sharedroot', 'rtstrelease'],
    'dinoreader-android.yml': ['core', 'dinoapp', 'sharedroot', 'dinorelease'],
    'dinoreader-ios.yml': ['core', 'dinoios', 'sharedroot', 'dinorelease'],
    'dinoreader-windows.yml': ['core', 'dinowin', 'sharedroot', 'dinorelease'],
    // tests.yml holds RTST's suite and the shared suite (Slice 368; Slice 128 before it): the shared suite gates the Dino builds too, and testSuites() selects it for every Dino group, so a Dino-only PR must start this workflow.
    'tests.yml': ['core', 'unittests', 'sharedtests', 'sherpapins', 'windows', 'android', 'ios', 'mobileshared', 'sharedroot', 'dinoapp', 'dinoios', 'dinowin', 'rtstrelease', 'dinorelease'],
    // dinoreader.yml is Dino's suite: it runs for every Dino target (core is in each of them).
    'dinoreader.yml': ['core', 'dinoreader', 'dinoapp', 'dinoios', 'dinowin', 'sharedroot', 'dinorelease'],
};

for (const [file, groups] of Object.entries(PR_TARGETS)) {
    test(`${file}: pull_request.paths equals the paths derived from GROUPS (${groups.join(' + ')})`, () => {
        const actual = readPullRequestPaths(file);
        const own = `.github/workflows/${file}`;
        const expected = derivedPaths(groups);
        expected.add(own); // a workflow always re-runs on its own edits
        IGNORED_PATTERNS.forEach((pattern) => expected.add(`!${pattern}`));
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
        const paths = readPullRequestPaths(file);
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
    assert.deepEqual(plain, { local: true, files: false, stdin: false, tests: false, base: 'origin/main' });
    const { repoRoot: explicit, ...withFlags } = parseLocalArgs(['--local', '--base', 'main', '--files', '--repo-root', 'X']);
    assert.equal(explicit, 'X');
    assert.deepEqual(withFlags, { local: true, files: true, stdin: false, tests: false, base: 'main' });
    assert.equal(parseLocalArgs(['--local', '--tests']).tests, true);
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
        assert.deepEqual([flags.dinoios, flags.dinostore], [true, true]); // an Application change selects the store job too (Slice 302)
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

test('Slice 222: a Dino-only run with a pending Store change builds and submits the MSIX (the rtst version is always computed by GitVersion)', () => {
    const dinoOnly = plan([], ['src/DinoReader.Core/Parser.cs']);
    dinoOnly.rtst.storeChangedFiles = ['src/ReadTheStupidText.App/App.xaml.cs'];
    const flags = shipTargetsPerProduct({ plans: dinoOnly });
    assert.deepEqual([flags.windows, flags.windowsstore, flags.dinoapp], [true, true, true]);
});

test('Slice 222: the newest Store marker wins whatever order the tags come in, so a marker for an older release never moves the base back', () => {
    const prefix = PRODUCTS.rtst.store.prefix;
    assert.equal(newestAppTag(['rtst-store-v1.77.0', 'rtst-store-v1.79.0', 'rtst-store-v1.78.3'], prefix), 'rtst-store-v1.79.0');
});

test('Slice 222: the marker job never moves or adds an older marker', () => {
    const job = readFileSync(`${WORKFLOWS_DIR}release-rtst.yml`, 'utf8').split('store-submit-mark:')[1].split('play-submit:')[0];
    assert.match(job, /existing" = "\$sha"/);
    assert.match(job, /\[ -n "\$existing" \].*exit 0/);
    assert.match(job, /not adding an older marker/);
    assert.doesNotMatch(job, /--force|-X PATCH/);
});

// ---- "Changed after merge" (Slice 239): the store upload is its own workflow, the release workflows ignore store-book folders ----
/** GitHub glob: `**` crosses folders (also a lone `**`), `*` stays inside one. (matchesPattern's `**` is for the narrower patterns of GROUPS.) */
function globMatches(file, pattern) {
    const body = pattern.replace(/[.+?^${}()|[\]\\]/g, '\\$&').replace(/\*\*/g, '@@').replace(/\*/g, '[^/]*').replace(/@@/g, '.*');
    return new RegExp(`^${body}$`).test(file);
}

/** GitHub `paths` semantics for one file: the last matching pattern decides (a `!` pattern excludes), no match = no run. */
function pushRuns(paths, file) {
    let runs = false;
    for (const pattern of paths) {
        const negated = pattern.startsWith('!');
        if (globMatches(file, negated ? pattern.slice(1) : pattern)) runs = !negated;
    }
    return runs;
}

function pushPathsOf(workflow) {
    const lines = readFileSync(`${WORKFLOWS_DIR}${workflow}`, 'utf8').split(/\r?\n/);
    const start = lines.findIndex((line) => line === '    paths:');
    assert.ok(start >= 0, `${workflow}: no push paths`);
    const out = [];
    for (const line of lines.slice(start + 1)) {
        const item = /^      - '(.+)'$/.exec(line);
        if (!item) break;
        out.push(item[1]);
    }
    return out;
}

for (const workflow of ['release-rtst.yml', 'release-dino.yml']) {
    test(`${workflow} ignores every book folder, audio clips included (Slice 265)`, () => {
        const paths = pushPathsOf(workflow);
        assert.equal(pushRuns(paths, 'content/dino/x-marks-the-treasure/story.md'), false);
        assert.equal(pushRuns(paths, 'content/dino/x-marks-the-treasure/pictures/1.jpg'), false);
        assert.ok(BOOK_STEMS.length > 0, 'content/dino has no book folders');
        for (const stem of BOOK_STEMS) assert.equal(pushRuns(paths, `content/dino/${stem}/book.json`), false, stem);
        assert.equal(pushRuns(paths, 'content/dino/three-little-pigs/audio/fa/manifest.json'), false);
        assert.equal(pushRuns(paths, 'content/dino/three-little-pigs/audio/fa/0a1b2c3d4e5f6071-Hali-gemini-tts-6de2d2f6.m4a'), false);
    });
}

test('a mixed push (a book plus Dino code) still starts release-dino.yml; shared code starts both release workflows', () => {
    const mixed = ['content/dino/x-marks-the-treasure/story.md', 'src/DinoReader.Core/Parser.cs'];
    assert.equal(mixed.some((file) => pushRuns(pushPathsOf('release-dino.yml'), file)), true);
    for (const workflow of ['release-rtst.yml', 'release-dino.yml']) {
        for (const file of ['src/ReadTheStupidText.Domain/X.cs', '.github/workflows/release-shared.yml', 'GitVersion.yml']) assert.equal(pushRuns(pushPathsOf(workflow), file), true, `${workflow}: ${file}`);
    }
});

// Slice 369 (review): the release workflows' push paths are written by hand, so they are checked against GROUPS here.
// (release-dino.yml's list is already compared with GROUPS by the "release-dino.yml push paths equal ..." test above.)
// dino-store.yml is not in any GROUPS entry (it ships books, not an app) but only the store workflow reads it.
const RTST_EXTRA_NEGATIONS = ['!content/dino/**', '!.github/workflows/dino-store.yml'];
const RTST_RELEASE_GROUPS = ['core', 'unittests', 'sharedtests', 'sherpapins', 'infrastructure', 'windows', 'android', 'ios', 'mobileshared', 'sharedroot', 'rtstrelease'];
const sampleFileOf = (pattern) => globOf(pattern).replace('**', 'sample.cs').replace('*', 'a');
const includeSamples = (groups) => groups.flatMap((name) => GROUPS[name].include.map(sampleFileOf));

test('release-rtst.yml push paths run for every RTST group and skip every path only Dino Reader reads', () => {
    const paths = pushPathsOf('release-rtst.yml');
    const rtstSamples = includeSamples(RTST_RELEASE_GROUPS);
    const dinoOnly = [...new Set(includeSamples(['dinoapp', 'dinoios', 'dinowin', 'dinoreader', 'dinostore', 'dinorelease']))].filter((file) => !rtstSamples.includes(file));
    assert.ok(dinoOnly.length > 5, 'no Dino-only paths derived from GROUPS');
    const stillRuns = dinoOnly.filter((file) => pushRuns(paths, file) && !RTST_RELEASE_GROUPS.some((name) => GROUPS[name].include.some((p) => matchesPattern(file, p))));
    assert.deepEqual(stillRuns, [], 'Dino-only paths that still start release-rtst.yml: add a ! line');
    const skipped = rtstSamples.filter((file) => !pushRuns(paths, file));
    assert.deepEqual(skipped, [], 'RTST paths that release-rtst.yml would not run for');
    const negations = paths.filter((p) => p.startsWith('!'));
    const orphans = negations.filter((p) => !dinoOnly.some((file) => globMatches(file, p.slice(1))) && !RTST_EXTRA_NEGATIONS.includes(p));
    assert.deepEqual(orphans, [], 'release-rtst.yml negations that no Dino-only GROUPS path needs');
});

test('release-rtst.yml keeps the old release.yml exclusion (content/dino/**) and nothing a docs-only merge could use', () => {
    const paths = pushPathsOf('release-rtst.yml');
    assert.equal(paths[0], '**');
    assert.ok(paths.includes('!content/dino/**'));
    assert.deepEqual(targetsOf(['docs/plan/slices/369-release-workflow-per-product.md', 'plan.md']), NONE);
});

test('dino-store.yml runs for every book folder and workflow_dispatch, and has its own concurrency group', () => {
    const paths = pushPathsOf('dino-store.yml');
    assert.equal(pushRuns(paths, 'content/dino/x-marks-the-treasure/story.md'), true);
    for (const stem of BOOK_STEMS) assert.equal(pushRuns(paths, `content/dino/${stem}/book.json`), true, stem);
    assert.equal(pushRuns(paths, 'content/dino/three-little-pigs/audio/fa/0a1b2c3d4e5f6071-Hali-gemini-tts-6de2d2f6.m4a'), true);
    const text = readFileSync(`${WORKFLOWS_DIR}dino-store.yml`, 'utf8');
    assert.match(text, /group: dino-store\r?\n\s+cancel-in-progress: false/);
    assert.match(text, /workflow_dispatch:/);
    for (const workflow of ['release-rtst.yml', 'release-dino.yml', 'release-shared.yml']) assert.doesNotMatch(readFileSync(`${WORKFLOWS_DIR}${workflow}`, 'utf8'), /publish-store-books\.mjs --/);
    assert.match(text, /schedule:\r?\n\s+- cron: /);
    assert.match(text, /publish-store-books\.mjs --delist --bucket-state "\$RUNNER_TEMP\/live-store\.json" --audio-verdict "\$RUNNER_TEMP\/audio-verdict\.json"\r?\n/);
    assert.doesNotMatch(text, /content-changed/);
});

test('dino-store.yml push paths cover every path of the dinostore group (one source of truth) plus its own extras', () => {
    const paths = pushPathsOf('dino-store.yml');
    const probe = (entry) => (entry.endsWith('/') ? `${entry}sub/file.x` : entry);
    for (const entry of GROUPS.dinostore.include) assert.equal(pushRuns(paths, probe(entry)), true, `dinostore path not covered by dino-store.yml: ${entry}`);
    for (const extra of ['src/DinoReader.App/ShippedTitles.cs', '.github/workflows/dino-store.yml']) assert.equal(pushRuns(paths, extra), true, extra);
    assert.equal(pushRuns(paths, 'content/dino/x-marks-the-treasure/high-res/cover.png'), false);
    assert.equal(pushRuns(paths, 'content/dino/x-marks-the-treasure/pictures/1.jpg'), true);
});

test('tools-selftests.yml still runs on a high-res-only pull request (a PR must always have a check to wait on)', () => {
    const paths = readPullRequestPaths('tools-selftests.yml');
    assert.equal(pushRuns(paths, 'content/dino/x-marks-the-treasure/high-res/cover.png'), true);
});

// ---- Slice 369: one release workflow per product ----
const BOTH = { rtst: true, dino: true };
const RELEASE_CASES = [
    ['a Dino-only change starts only the Dino workflow', ['src/DinoReader.App/MainPage.xaml.cs'], { rtst: false, dino: true }],
    ['a Dino Core change starts only the Dino workflow', ['src/DinoReader.Core/Parser.cs'], { rtst: false, dino: true }],
    ['a Dino test change starts only the Dino workflow (its suite)', ['tests/DinoReader.Tests/ParserTests.cs'], { rtst: false, dino: true }],
    ['a tray-only change starts only the RTST workflow', ['src/ReadTheStupidText.Windows.Tray/Input/X.cs'], { rtst: true, dino: false }],
    ['the RTST phone app starts only the RTST workflow', [`${M}/ReaderPage.xaml.cs`], { rtst: true, dino: false }],
    ['a shared engine change starts both', ['src/ReadTheStupidText.Application/Reading/X.cs'], BOTH],
    ['Infrastructure starts both (Dino reads with its Windows engines)', ['src/ReadTheStupidText.Infrastructure/X.cs'], BOTH],
    ['the shared release workflow starts both', ['.github/workflows/release-shared.yml'], BOTH],
    ['each product release workflow starts only its own product', ['.github/workflows/release-rtst.yml'], { rtst: true, dino: false }],
    ['docs only start neither', ['CLAUDE.md', 'plan.md', 'docs/plan/slices/1-x.md'], { rtst: false, dino: false }],
    ['a store book starts neither (dino-store.yml owns it)', ['content/dino/x-marks-the-treasure/story.md'], { rtst: false, dino: false }],
    ['a change in both products starts both', ['src/DinoReader.App/MainPage.xaml.cs', 'src/ReadTheStupidText.Windows.Tray/Input/X.cs'], BOTH],
    ['an unknown range starts both', null, BOTH],
];

for (const [name, files, expected] of RELEASE_CASES) {
    test(`releaseWorkflows: ${name}`, () => {
        assert.deepEqual(releaseWorkflows(files), expected);
    });
}

/** Whether a workflow's push `paths` start it for at least one file. */
const startsFor = (workflow, files) => files.some((file) => pushRuns(pushPathsOf(workflow), file));

test('push paths: every CASES change set starts the release workflow(s) releaseWorkflows names, and never the other product when it is not named', () => {
    for (const [name, files] of CASES) {
        const wanted = releaseWorkflows(files);
        if (wanted.rtst) assert.equal(startsFor('release-rtst.yml', files), true, `${name}: release-rtst.yml must start`);
        if (wanted.dino) assert.equal(startsFor('release-dino.yml', files), true, `${name}: release-dino.yml must start`);
        // A Dino-only change must not start the RTST run, and an RTST-only one not the Dino run (docs/other files may start RTST: ship-targets then plans nothing).
        if (wanted.dino && !wanted.rtst) assert.equal(startsFor('release-rtst.yml', files), false, `${name}: release-rtst.yml must not start`);
        if (wanted.rtst && !wanted.dino) assert.equal(startsFor('release-dino.yml', files), false, `${name}: release-dino.yml must not start`);
    }
});

test('push paths: every path of every group starts the workflow(s) of the product(s) it ships (one source of truth, GROUPS)', () => {
    const probe = (entry) => (entry.includes('*') ? entry.replace(/\*/g, 'x') : entry.endsWith('/') ? `${entry}sub/file.x` : entry);
    for (const [group, { include, exclude = [] }] of Object.entries(GROUPS)) {
        for (const entry of include) {
            const file = probe(entry);
            if (exclude.some((pattern) => matchesPattern(file, pattern))) continue;
            const wanted = releaseWorkflows([file]);
            if (wanted.rtst) assert.equal(startsFor('release-rtst.yml', [file]), true, `${group}: ${file} must start release-rtst.yml`);
            if (wanted.dino) assert.equal(startsFor('release-dino.yml', [file]), true, `${group}: ${file} must start release-dino.yml`);
            if (wanted.dino && !wanted.rtst) assert.equal(startsFor('release-rtst.yml', [file]), false, `${group}: ${file} is Dino-only and must not start release-rtst.yml`);
            if (wanted.rtst && !wanted.dino) assert.equal(startsFor('release-dino.yml', [file]), false, `${group}: ${file} is RTST-only and must not start release-dino.yml`);
        }
    }
});

test('release-dino.yml push paths equal the paths derived from GROUPS (core + every Dino group + sharedroot + dinorelease), like a Dino build workflow', () => {
    const actual = pushPathsOf('release-dino.yml');
    const expected = derivedPaths(['core', 'dinoapp', 'dinoios', 'dinowin', 'dinoreader', 'sharedroot', 'dinorelease']);
    IGNORED_PATTERNS.forEach((pattern) => expected.add(`!${pattern}`));
    assert.equal(new Set(actual).size, actual.length, 'duplicate path entries');
    assert.deepEqual([...new Set(actual)].sort(), [...expected].sort());
    const excluded = actual.indexOf('!src/ReadTheStupidText.Mobile.Shared/Platforms/**');
    assert.ok(excluded > actual.indexOf('src/ReadTheStupidText.Mobile.Shared/**') && actual.indexOf('src/ReadTheStupidText.Mobile.Shared/**') >= 0, '! must follow the Mobile.Shared glob');
    assert.ok(actual.filter((p) => p.startsWith('src/ReadTheStupidText.Mobile.Shared/Platforms/')).every((p) => actual.indexOf(p) > excluded), 'Platforms re-includes must follow the !');
});

test('a Dino-only PR starts only release-dino.yml and an RTST-only one only release-rtst.yml (the first-merge check of Slice 369)', () => {
    assert.equal(startsFor('release-dino.yml', ['src/DinoReader.App/MainPage.xaml.cs']), true);
    assert.equal(startsFor('release-rtst.yml', ['src/DinoReader.App/MainPage.xaml.cs']), false);
    assert.equal(startsFor('release-rtst.yml', ['src/ReadTheStupidText.Windows.Tray/Input/X.cs']), true);
    assert.equal(startsFor('release-dino.yml', ['src/ReadTheStupidText.Windows.Tray/Input/X.cs']), false);
});

test('per product plan: PRODUCT=dino plans only Dino (the RTST tag is never read), PRODUCT=rtst only RTST, no PRODUCT both', () => {
    const asked = [];
    const plan = (name, versionEnv) => { asked.push([name, versionEnv]); return { changedFiles: [], bump: 'other' }; };
    assert.deepEqual(Object.keys(plansFromEnv({ PRODUCT: 'dino' }, plan)), ['dino']);
    assert.deepEqual(Object.keys(plansFromEnv({ PRODUCT: 'rtst' }, plan)), ['rtst']);
    assert.deepEqual(Object.keys(plansFromEnv({}, plan)).sort(), ['dino', 'rtst']);
    assert.deepEqual(asked, [['dino', 'VERSION'], ['rtst', 'VERSION'], ['rtst', 'RTST_VERSION'], ['dino', 'DINO_VERSION']]);
    assert.throws(() => plansFromEnv({ PRODUCT: 'other' }, plan), /unknown PRODUCT/);
});

test('per product plan: a Dino plan sets only the Dino targets, force_all and a major bump stay inside the product', () => {
    const dinoOnly = shipTargetsPerProduct({ plans: { dino: { changedFiles: ['src/DinoReader.App/X.cs'], bump: 'other' } } });
    assert.deepEqual(Object.keys(dinoOnly).sort(), [...PRODUCTS.dino.targets].sort());
    assert.deepEqual([dinoOnly.dinoapp, dinoOnly.dinoios, dinoOnly.dinowin], [true, true, true]);
    const forced = shipTargetsPerProduct({ plans: { rtst: { changedFiles: [], bump: 'other' } }, forceAll: true });
    assert.deepEqual(Object.keys(forced).sort(), [...PRODUCTS.rtst.targets].sort());
    assert.deepEqual([forced.windows, forced.android, forced.ios, forced.windowsstore], [true, true, true, true]);
});

test('the release workflows: own concurrency group per product, a dispatch trigger each, no workflow_call mixed into a caller', () => {
    const groups = [];
    for (const [workflow, product] of [['release-rtst.yml', 'rtst'], ['release-dino.yml', 'dino']]) {
        const text = readFileSync(`${WORKFLOWS_DIR}${workflow}`, 'utf8');
        assert.match(text, /workflow_dispatch:/, workflow);
        assert.doesNotMatch(text, /workflow_call:/, workflow);
        assert.match(text, new RegExp(`product: ${product}\\r?\\n\\s+workflow_file: ${workflow.replace('.', '\\.')}`), workflow);
        const group = /^concurrency:\r?\n\s+group: (.+)\r?\n\s+cancel-in-progress: false/m.exec(text);
        assert.ok(group, `${workflow}: concurrency group with cancel-in-progress: false`);
        for (const prefix of [`release-${product}-dispatch-`, `release-${product}-fix-`, `release-${product}-{0}`]) assert.ok(group[1].includes(prefix), `${workflow}: group names ${prefix}`);
        groups.push(group[1]);
    }
    assert.ok(!groups[0].includes('release-dino') && !groups[1].includes('release-rtst'), 'a product group never names the other product');
    const shared = readFileSync(`${WORKFLOWS_DIR}release-shared.yml`, 'utf8');
    assert.doesNotMatch(shared, /^concurrency:/m, 'release-shared.yml has no group of its own (a group shared with its caller would cancel the caller)');
    assert.match(shared, /^on:\r?\n  workflow_call:/m);
    assert.doesNotMatch(shared, /^  (push|workflow_dispatch):/m);
});

test('the retired release.yml is gone and no workflow or script still points at it (a path, a `uses`, a `gh run list --workflow`)', () => {
    assert.equal(existsSync(`${WORKFLOWS_DIR}release.yml`), false);
    const scriptsDir = fileURLToPath(new URL('./', import.meta.url));
    const files = [...readdirSync(WORKFLOWS_DIR).map((f) => `${WORKFLOWS_DIR}${f}`), ...readdirSync(scriptsDir).filter((f) => /\.(mjs|sh)$/.test(f) && !f.endsWith('.test.mjs')).map((f) => `${scriptsDir}${f}`)];
    for (const file of files) assert.doesNotMatch(readFileSync(file, 'utf8'), /workflows\/release\.yml|--workflow "?release\.yml/, file);
});
