#!/usr/bin/env node
/**
 * Dry-run of build.yml's `changes` path filter (Slice 44, Decision 69).
 *
 * Reads the inline `filters: |` block out of build.yml, applies dorny/paths-filter's
 * `some-with-excludes` rule (a file matches a group when it matches one of its rules
 * and none of its '!' rules; picomatch with dot:true, as in the action) and then the
 * `changes` job's own `outputs:` expressions (parsed from build.yml, not copied).
 * Run: node .github/scripts/path-filters.test.mjs
 *
 * picomatch is the action's own matcher. It is not a repo dependency: install it
 * anywhere (npm i picomatch) and, if it is not resolvable from here, point
 * PICOMATCH_DIR at the folder that holds node_modules.
 */
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const require = createRequire(join(process.env.PICOMATCH_DIR ?? here, 'noop.js'));
const picomatch = require('picomatch');

const FILTERS_MARKER = /^(\s*)filters: \|\s*$/m;

export function readFilters(workflowText) {
    const marker = FILTERS_MARKER.exec(workflowText);
    assert.ok(marker, 'no `filters: |` block in build.yml');
    const blockIndent = marker[1].length;
    const filters = {};
    let current = null;
    for (const line of workflowText.slice(marker.index + marker[0].length).split('\n')) {
        if (line.trim() === '') continue;
        const indent = line.length - line.trimStart().length;
        if (indent <= blockIndent) break;
        const rule = /^\s*-\s*(?:'([^']+)'|"([^"]+)")\s*$/.exec(line);
        const header = /^\s*([A-Za-z][\w-]*):\s*$/.exec(line);
        if (rule) {
            assert.ok(current, `rule before any group header: ${line}`);
            filters[current].push(rule[1] ?? rule[2]);
        } else if (header) {
            current = header[1];
            filters[current] = [];
        } else {
            throw new Error(`unrecognised line in the filters block: ${line}`);
        }
    }
    return filters;
}

function fileMatchesGroup(file, rules) {
    const include = rules.filter((r) => !r.startsWith('!'));
    const exclude = rules.filter((r) => r.startsWith('!')).map((r) => r.slice(1));
    const hit = (patterns) => patterns.some((p) => picomatch(p, { dot: true })(file));
    return hit(include) && !hit(exclude);
}

/**
 * The `changes` job's `outputs:` expressions, parsed straight out of build.yml so the
 * test cannot drift from them. Each is an `||` chain of terms, and only two term shapes
 * are understood: the workflow_dispatch check and `steps.filter.outputs.<group> == 'true'`.
 */
export function readOutputExpressions(workflowText) {
    const start = workflowText.search(/^  changes:\s*$/m);
    assert.ok(start >= 0, 'no `changes` job in build.yml');
    const section = workflowText.slice(start).split(/\n  \S/)[0];
    const block = /^    outputs:\s*\n((?:      .+\n?)+)/m.exec(section);
    assert.ok(block, 'no `outputs:` block in the changes job');
    const outputs = {};
    for (const line of block[1].split('\n').filter((l) => l.trim())) {
        const m = /^\s+(\w+): \$\{\{ (.+) \}\}\s*$/.exec(line);
        assert.ok(m, `unparseable output line: ${line}`);
        outputs[m[1]] = m[2].split('||').map((t) => t.trim());
    }
    return outputs;
}

function evaluateTerm(term, groups, isDispatch) {
    if (term === "github.event_name == 'workflow_dispatch'") return isDispatch;
    const group = /^steps\.filter\.outputs\.(\w+) == 'true'$/.exec(term);
    assert.ok(group, `unsupported term in an outputs expression: ${term}`);
    return !isDispatch && groups[group[1]] === true;
}

/** The platform outputs of the `changes` job for a list of changed files. */
export function classify(files, filters, outputs, isDispatch = false) {
    const groups = {};
    for (const group of Object.keys(filters)) {
        groups[group] = files.some((f) => fileMatchesGroup(f, filters[group]));
    }
    const result = {};
    for (const [name, terms] of Object.entries(outputs)) {
        result[name] = terms.some((t) => evaluateTerm(t, groups, isDispatch));
    }
    return result;
}

const M = 'src/ReadTheStupidText.Mobile';
const CASES = [
    ['Android platform only', [`${M}/Platforms/Android/AndroidSpeechReader.cs`], { windows: false, android: true, ios: false }],
    ['iOS platform only', [`${M}/Platforms/iOS/AppDelegate.cs`], { windows: false, android: false, ios: true }],
    ['shared Mobile code', [`${M}/ReaderPage.xaml.cs`], { windows: false, android: true, ios: true }],
    ['Mobile csproj', [`${M}/ReadTheStupidText.Mobile.csproj`], { windows: false, android: true, ios: true }],
    ['Mobile resource', [`${M}/Resources/Images/a.png`], { windows: false, android: true, ios: true }],
    ['Domain core', ['src/ReadTheStupidText.Domain/PlaybackRate.cs'], { windows: true, android: true, ios: true }],
    ['pipeline itself', ['.github/workflows/build.yml'], { windows: true, android: true, ios: true }],
    ['Windows app only', ['src/ReadTheStupidText.App/App.xaml.cs'], { windows: true, android: false, ios: false }],
    ['docs only', ['CLAUDE.md', 'docs/plan/decisions.md', 'plan.md'], { windows: false, android: false, ios: false }],
    ['DinoReader only', ['src/DinoReader.Core/Parser.cs'], { windows: false, android: false, ios: false }],
    ['Android + iOS platform files', [`${M}/Platforms/Android/a.cs`, `${M}/Platforms/iOS/b.cs`], { windows: false, android: true, ios: true }],
];

const workflowText = readFileSync(join(here, '..', 'workflows', 'build.yml'), 'utf8').replaceAll('\r\n', '\n');
const filters = readFilters(workflowText);
const outputs = readOutputExpressions(workflowText);
for (const [name, files, expected] of CASES) {
    const got = classify(files, filters, outputs);
    for (const [platform, want] of Object.entries(expected)) {
        assert.equal(got[platform], want, `${name}: ${platform} expected ${want}, got ${got[platform]}`);
    }
    console.log(`ok  ${name}: windows=${got.windows} android=${got.android} ios=${got.ios}`);
}
assert.equal(classify(['src/DinoReader.Core/Parser.cs'], filters, outputs).dinoreader, true);
console.log('ok  DinoReader-only sets dinoreader');
assert.throws(
    () => readFilters('filters: |\n  core:\n    - src/**\n'),
    /unrecognised line/,
    'an unquoted rule must fail the reader, not be reinterpreted as a group',
);
assert.deepEqual(readFilters('filters: |\n  a:\n    - "x/**"\n    - \'y\'\n'), { a: ['x/**', 'y'] });
console.log('ok  filters reader rejects unrecognised lines, accepts double quotes');
const dispatch = classify([], filters, outputs, true);
assert.deepEqual(Object.values(dispatch).every(Boolean), true, 'workflow_dispatch must force every output');
console.log('ok  workflow_dispatch forces every output');
