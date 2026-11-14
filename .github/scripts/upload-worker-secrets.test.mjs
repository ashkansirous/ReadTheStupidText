import assert from 'node:assert/strict';
import { test } from 'node:test';

import { WANTED, applyAll, describeFailure, expandPemNewlines, isInsideRoot, namesMissingFrom, parseEnv, parseMode, putInOrder, resolveSecrets } from './upload-worker-secrets.mjs';

const ROOT = '/store';
const PLAIN = { DINOREADER_PLAY_SERVICE_ACCOUNT_JSON: '{"a":1}', APPLE_IAP_KEY_ID: 'K', APPLE_IAP_ISSUER_ID: 'I', MS_STORE_TENANT_ID: 'T', MS_STORE_CLIENT_ID: 'C', MS_STORE_CLIENT_SECRET: 'S' };

test('Slice 346 E5: parseEnv reads KEY=value lines and skips comments', () => {
    assert.deepEqual(parseEnv('# --- A ---\r\n# Purpose: x\r\nA=1\r\n\r\nB_FILE=dino/x.p8\r\n'), { A: '1', B_FILE: 'dino/x.p8' });
});

test('Slice 346 review (check2 #7): unquoted values are trimmed and lose a trailing " # comment"; quoted ones keep their inside', () => {
    assert.deepEqual(parseEnv('A=ABC123 \nB=xyz  # note\nC="a # b"\nD=x#y'), { A: 'ABC123', B: 'xyz', C: 'a # b', D: 'x#y' });
});

test('Slice 346 E5: the Apple key is read from its *_FILE entry, and the Play key from the DINOREADER_ name', () => {
    const reads = [];
    const { values, missing } = resolveSecrets({ ...PLAIN, APPLE_IAP_KEY_P8_FILE: 'dinoreader/key.p8' }, ROOT, (path) => { reads.push(path); return '-----BEGIN-----\n'; }, () => true);
    assert.deepEqual(missing, []);
    assert.equal(values.get('APPLE_IAP_KEY_P8'), '-----BEGIN-----');
    assert.equal(values.get('PLAY_SERVICE_ACCOUNT_JSON'), '{"a":1}');
    assert.equal(values.size, WANTED.length);
    assert.equal(reads.length, 1);
});

test('Slice 346 E5: a missing entry, a missing file and a path out of the store are each named as missing', () => {
    assert.deepEqual(resolveSecrets(PLAIN, ROOT, () => '', () => true).missing.map((m) => m.name), ['APPLE_IAP_KEY_P8']);
    const noFile = resolveSecrets({ ...PLAIN, APPLE_IAP_KEY_P8_FILE: 'dinoreader/gone.p8' }, ROOT, () => 'x', () => false).missing;
    assert.match(noFile[0].why, /does not exist/);
    const outside = resolveSecrets({ ...PLAIN, APPLE_IAP_KEY_P8_FILE: '../etc/passwd' }, ROOT, () => 'x', () => true).missing;
    assert.match(outside[0].why, /outside the secrets store/);
    const empty = resolveSecrets({ ...PLAIN, APPLE_IAP_KEY_P8_FILE: 'dinoreader/e.p8' }, ROOT, () => '  \n', () => true).missing;
    assert.match(empty[0].why, /empty file/);
});

test('Slice 346 E5: isInsideRoot refuses a path that climbs out or is absolute', () => {
    assert.equal(isInsideRoot(ROOT, 'dinoreader/key.p8'), true);
    assert.equal(isInsideRoot(ROOT, '../key.p8'), false);
    assert.equal(isInsideRoot(ROOT, '/etc/key.p8'), false);
});

test('Slice 346 E5: the Worker list is checked by name, and one missing name is reported', () => {
    const all = JSON.stringify(WANTED.map(({ name }) => ({ name, type: 'secret_text' })));
    assert.deepEqual(namesMissingFrom(all), []);
    const withoutApple = JSON.stringify(WANTED.filter(({ name }) => name !== 'APPLE_IAP_KEY_P8').map(({ name }) => ({ name, type: 'secret_text' })));
    assert.deepEqual(namesMissingFrom(withoutApple), ['APPLE_IAP_KEY_P8']);
});

test('Slice 346 E5: a list that is not JSON, or not a list, fails loudly', () => {
    assert.throws(() => namesMissingFrom('<html>'), /did not answer JSON/);
    assert.throws(() => namesMissingFrom('{"errors":[]}'), /did not answer a list/);
});

test('Slice 346 E5: no flag is a dry run, and a typo is an error, never a real run', () => {
    assert.equal(parseMode([]), 'dry');
    assert.equal(parseMode(['--apply']), 'apply');
    assert.equal(parseMode(['--verify']), 'verify');
    assert.throws(() => parseMode(['--aply']), /Unknown arguments/);
});

test('Slice 346 review #5: a banner around the secret list does not hide it, and quoted env values lose their quotes', () => {
    const banner = ' wrangler 4.0\n-------\n[{"name":"APPLE_IAP_KEY_ID","type":"secret_text"}]\nUpdate available\n';
    assert.equal(namesMissingFrom(banner).includes('APPLE_IAP_KEY_ID'), false);
    assert.equal(namesMissingFrom(banner).length, WANTED.length - 1);
    assert.deepEqual(parseEnv('A="x y"\nB=\'z\'\nC=plain\nD="odd\nE={"k":1}'), { A: 'x y', B: 'z', C: 'plain', D: '"odd', E: '{"k":1}' });
});

test('Slice 346 review #6: one missing source means no secret is put at all, and a file named ..foo is inside the root', () => {
    const puts = [];
    assert.equal(applyAll(new Map([['A', '1']]), [{ name: 'B', why: 'x' }], (values) => { puts.push(values); return false; }, () => false), true);
    assert.equal(puts.length, 0);
    assert.equal(applyAll(new Map([['A', '1']]), [], (values) => { puts.push(values); return false; }, () => false), false);
    assert.equal(puts.length, 1);
    let verified = false;
    assert.equal(applyAll(new Map([['A', '1']]), [], () => true, () => { verified = true; return false; }), true);
    assert.equal(verified, false); // check2 #5: a stopped put is not followed by an "all names present" line
    assert.equal(isInsideRoot(ROOT, 'dinoreader/..foo.p8'), true);
});

test('Slice 346 review #8: only the PEM secret gets real newlines from literal backslash-n; the Play JSON is uploaded byte for byte', () => {
    const oneLine = '-----BEGIN PRIVATE KEY-----\\nABC\\n-----END PRIVATE KEY-----';
    const playJson = '{"private_key":"-----BEGIN PRIVATE KEY-----\\nABC\\n-----END PRIVATE KEY-----\\n"}';
    assert.equal(expandPemNewlines('APPLE_IAP_KEY_P8', oneLine), '-----BEGIN PRIVATE KEY-----\nABC\n-----END PRIVATE KEY-----');
    const { values } = resolveSecrets({ ...PLAIN, APPLE_IAP_KEY_P8: oneLine, DINOREADER_PLAY_SERVICE_ACCOUNT_JSON: playJson }, ROOT, () => '', () => false);
    assert.equal(values.get('APPLE_IAP_KEY_P8').split('\n').length, 3);
    assert.equal(values.get('PLAY_SERVICE_ACCOUNT_JSON'), playJson);
    assert.doesNotThrow(() => JSON.parse(values.get('PLAY_SERVICE_ACCOUNT_JSON')));
});

test('Slice 346 review #7: a failed put stops the run and names what was set and what was not', () => {
    const tried = [];
    const values = new Map([['A', '1'], ['B', '2'], ['C', '3']]);
    const result = putInOrder(values, (name) => { tried.push(name); return { ok: name !== 'B', err: 'exit code 1' }; });
    assert.deepEqual(tried, ['A', 'B']);
    assert.deepEqual({ failed: result.failed, set: result.set, notSet: result.notSet }, { failed: true, set: ['A'], notSet: ['B', 'C'] });
    assert.equal(putInOrder(values, () => ({ ok: true, err: '' })).failed, false);
});

test('Slice 346 review #9: a failed wrangler run prints its exit code and one redacted line, never the stderr verbatim', () => {
    const leaked = 'Invalid value MIIEvQIBADANBgkqhkiG9w0BAQEFAASC for secret\nsecond line with more MIIEvQIBADANBgkqhkiG9w0BAQEFAASC';
    const text = describeFailure(1, leaked);
    assert.match(text, /^exit code 1: /);
    assert.ok(!text.includes('MIIEvQ') && !text.includes('second line'));
    assert.equal(describeFailure(7, ''), 'exit code 7');
});
