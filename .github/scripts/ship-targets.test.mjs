import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { GROUPS, bumpKind, matchesPattern, newestAppTag, shipTargets } from './ship-targets.mjs';

const M = 'src/ReadTheStupidText.Mobile';
const NONE = { windows: false, android: false, ios: false, dinoreader: false };
const ALL3 = { windows: true, android: true, ios: true };

function targetsOf(changedFiles, extra = {}) {
    return shipTargets({ changedFiles, bump: 'other', ...extra });
}

// Ported from path-filters.test.mjs (the build.yml `changes` job's cases).
const CASES = [
    ['Android platform only', [`${M}/Platforms/Android/AndroidSpeechReader.cs`], { android: true }],
    ['iOS platform only', [`${M}/Platforms/iOS/AppDelegate.cs`], { ios: true }],
    ['iOS signing script', ['.github/scripts/ios-signing.sh'], { ios: true }],
    ['TestFlight upload script', ['.github/scripts/testflight-upload.sh'], { ios: true }],
    ['Android stage script', ['.github/scripts/stage-android-package.sh'], { android: true }],
    ['shared Mobile code', [`${M}/ReaderPage.xaml.cs`], { android: true, ios: true }],
    ['Mobile csproj', [`${M}/ReadTheStupidText.Mobile.csproj`], { android: true, ios: true }],
    ['Mobile resource', [`${M}/Resources/Images/a.png`], { android: true, ios: true }],
    ['Domain core', ['src/ReadTheStupidText.Domain/PlaybackRate.cs'], ALL3],
    ['Application core', ['src/ReadTheStupidText.Application/X.cs'], ALL3],
    ['Documents core', ['src/ReadTheStupidText.Documents/X.cs'], ALL3],
    ['unit tests', ['tests/ReadTheStupidText.Tests/X.cs'], ALL3],
    ['solution file', ['ReadTheStupidText.slnx'], ALL3],
    ['global.json', ['global.json'], ALL3],
    ['GitVersion.yml', ['GitVersion.yml'], ALL3],
    ['.gitattributes', ['.gitattributes'], ALL3],
    ['release workflow (the pipeline itself)', ['.github/workflows/release.yml'], ALL3],
    ['find-release script', ['.github/scripts/find-release.sh'], ALL3],
    ['Windows app only', ['src/ReadTheStupidText.App/App.xaml.cs'], { windows: true }],
    ['Infrastructure only', ['src/ReadTheStupidText.Infrastructure/X.cs'], { windows: true }],
    ['store-submit workflow', ['.github/workflows/store-submit.yml'], { windows: true }],
    ['docs only', ['CLAUDE.md', 'docs/plan/decisions.md', 'plan.md'], {}],
    ['DinoReader only', ['src/DinoReader.Core/Parser.cs'], { dinoreader: true }],
    ['DinoReader tests only', ['tests/DinoReader.Tests/ParserTests.cs'], { dinoreader: true }],
    ['Android + iOS platform files', [`${M}/Platforms/Android/a.cs`, `${M}/Platforms/iOS/b.cs`], { android: true, ios: true }],
    ['Windows + DinoReader', ['src/ReadTheStupidText.App/a.cs', 'src/DinoReader.Core/b.cs'], { windows: true, dinoreader: true }],
    ['site and worker files', ['worker/downloads.ts', 'site/index.html'], {}],
];

for (const [name, files, expected] of CASES) {
    test(`maps: ${name}`, () => {
        assert.deepEqual(targetsOf(files), { ...NONE, ...expected });
    });
}

test('a major bump builds every target', () => {
    assert.deepEqual(targetsOf(['docs/x.md'], { bump: 'major' }), { ...ALL3, dinoreader: true });
});

test('an unknown range (no base tag) builds every target', () => {
    assert.deepEqual(targetsOf(null), { ...ALL3, dinoreader: true });
});

test('forceAll (manual run) builds every target', () => {
    assert.deepEqual(targetsOf([], { forceAll: true }), { ...ALL3, dinoreader: true });
});

test('an iOS-only change reports ios and nothing else (release.yml declines to ship it unsigned)', () => {
    assert.deepEqual(targetsOf([`${M}/Platforms/iOS/Info.plist`]), { ...NONE, ios: true });
});

test('DinoReader alone builds no app platform', () => {
    const t = targetsOf(['src/DinoReader.Core/a.cs']);
    assert.deepEqual([t.windows, t.android, t.ios], [false, false, false]);
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
    assert.equal(newestAppTag(['v1.0.9', 'v1.0.10', 'v1.0.2', 'packs-3', 'v1.0.11-rc', '']), 'v1.0.10');
    assert.equal(newestAppTag(['packs-1']), null);
    assert.equal(newestAppTag(['v2.0.0', 'v10.0.0', 'v9.9.9']), 'v10.0.0');
});

test('bumpKind: higher major is major, same major is other, no tag is major', () => {
    assert.equal(bumpKind('v1.4.2', '2.0.0'), 'major');
    assert.equal(bumpKind('v1.4.2', '1.4.3'), 'other');
    assert.equal(bumpKind('v1.4.2', '1.5.0'), 'other');
    assert.equal(bumpKind(null, '1.0.0'), 'major');
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
    'android.yml': ['core', 'android', 'mobileshared'],
    'ios.yml': ['core', 'ios', 'mobileshared'],
    // tests.yml gates both the Windows and Android builds.
    'tests.yml': ['core', 'windows', 'android', 'mobileshared'],
    'dinoreader.yml': ['dinoreader'],
};

for (const [file, groups] of Object.entries(PR_TARGETS)) {
    test(`${file}: pull_request.paths equals the paths derived from GROUPS (${groups.join(' + ')})`, () => {
        const actual = readPullRequestPaths(file);
        const own = `.github/workflows/${file}`;
        const expected = derivedPaths(groups);
        expected.add(own); // a workflow always re-runs on its own edits
        assert.equal(new Set(actual).size, actual.length, `${file}: duplicate path entries`);
        assert.deepEqual([...new Set(actual)].sort(), [...expected].sort());
    });
}

test('a negated path follows the Mobile glob and precedes the Platforms re-includes', () => {
    for (const file of ['android.yml', 'ios.yml', 'tests.yml']) {
        const paths = readPullRequestPaths(file);
        const excluded = paths.indexOf('!src/ReadTheStupidText.Mobile/Platforms/**');
        assert.ok(excluded > paths.indexOf('src/ReadTheStupidText.Mobile/**'), `${file}: ! must follow the Mobile glob`);
        const reincludes = paths.filter((p) => p.startsWith('src/ReadTheStupidText.Mobile/Platforms/') && !p.startsWith('!'));
        assert.ok(reincludes.every((p) => paths.indexOf(p) > excluded), `${file}: Platforms re-includes must follow the !`);
    }
});
