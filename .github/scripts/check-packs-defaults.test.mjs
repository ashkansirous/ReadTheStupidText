import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

import { findLowerDefaults, readCatalogUrl, readDefaults, INPUT_PACKS } from './check-packs-defaults.mjs';

const CATALOG = [
    { id: 'supertonic-3', version: '4' },
    { id: 'piper-fa', version: '2' },
    { id: 'piper-nb', version: '2' },
    { id: 'melo-zh-en', version: '1' },
    { id: 'kokoro-en', version: '3' },
    { id: 'ocr-latin', version: '1' },
    { id: 'translation-en-de', version: '1' },
];

const workflow = (norwegian) => `on:
  workflow_dispatch:
    inputs:
      pack_version:
        description: 'x'
        required: true
        default: '4'
        type: string
      piper_pack_version:
        description: 'x'
        required: true
        default: '2'
        type: string
      norwegian_pack_version:
        description: 'x'
        required: true
        default: '${norwegian}'
        type: string
      melo_pack_version:
        default: '1'
      kokoro_pack_version:
        default: '3'
      ocr_pack_version:
        default: '1'
      translation_pack_version:
        default: '1'
`;

test('defaults equal to the catalog pass', () => {
    const defaults = readDefaults(workflow('2'));
    assert.equal(Object.keys(defaults).length, 7);
    assert.deepEqual(findLowerDefaults(defaults, CATALOG), []);
});

test('a default above the catalog passes (a planned bump)', () => {
    assert.deepEqual(findLowerDefaults(readDefaults(workflow('3')), CATALOG), []);
});

test('a lower default fails and names the input and pack', () => {
    const problems = findLowerDefaults(readDefaults(workflow('1')), CATALOG);
    assert.equal(problems.length, 1);
    assert.match(problems[0], /norwegian_pack_version defaults to 1 but piper-nb is version 2/);
});

test('an unmapped input and a pack-less input are reported', () => {
    assert.equal(findLowerDefaults({ new_pack_version: 1 }, CATALOG).length, 1);
    assert.equal(findLowerDefaults({ melo_pack_version: 1 }, []).length, 1);
});

test('a catalog pack matched by no input and a non-numeric version fail', () => {
    const defaults = readDefaults(workflow('2'));
    const extra = findLowerDefaults(defaults, [...CATALOG, { id: 'piper-xx', version: '1' }]);
    assert.match(extra.join('\n'), /piper-xx: catalog pack matches no packs.yml version input/);
    const bad = findLowerDefaults(defaults, [{ id: 'piper-nb', version: '2a' }]);
    assert.match(bad.join('\n'), /piper-nb: catalog version "2a" is not a whole number/);
});

test('a re-indented inputs block reads zero defaults', () => {
    assert.deepEqual(readDefaults(workflow('2').replace(/^ {6}/gm, '    ')), {});
});

test('an input without a numeric default throws', () => {
    assert.throws(() => readDefaults("      pack_version:\n        description: 'x'\n"));
});

test('the catalog url is read from PackConstants', () => {
    const cs = 'public const string CatalogUrl =\n  "https://github.com/o/r/releases/download/packs-11/packs.json";';
    assert.equal(readCatalogUrl(cs), 'https://github.com/o/r/releases/download/packs-11/packs.json');
    assert.throws(() => readCatalogUrl('nothing'));
});

test('every version input in the real packs.yml is mapped and parsed', () => {
    const defaults = readDefaults(readFileSync('.github/workflows/packs.yml', 'utf8'));
    assert.deepEqual(Object.keys(defaults).sort(), Object.keys(INPUT_PACKS).sort());
});
