import test from 'node:test';
import assert from 'node:assert/strict';

import { BUCKET_NAME, publishedStateFrom, wranglerArgs, wranglerCommand } from './r2-wrangler.mjs';

test('builds the wrangler arguments for the remote bucket', () => {
    const args = wranglerArgs({ key: 'android/1.4.0/a.apk', path: 'out/a.apk', contentType: 'application/vnd.android.package-archive' });
    assert.deepEqual(args, [
        'r2', 'object', 'put', `${BUCKET_NAME}/android/1.4.0/a.apk`,
        '--file', 'out/a.apk', '--remote', '--content-type', 'application/vnd.android.package-archive',
    ]);
});

test('runs wrangler through node with the content type as one argument, no shell', () => {
    const upload = { key: 'manifests/android/1.4.0.json', path: 'm.json', contentType: 'application/json; charset=utf-8' };
    const { command, args } = wranglerCommand(wranglerArgs(upload), 'node', 'wrangler.js');
    assert.equal(command, 'node');
    assert.equal(args[0], 'wrangler.js');
    assert.equal(args[args.indexOf('--content-type') + 1], 'application/json; charset=utf-8');
});

test('reads wrangler get results: found, missing key in any wording, or an error that must not pass as missing', () => {
    assert.equal(publishedStateFrom({ status: 0, output: '' }), true);
    for (const output of [
        'ERROR The specified key does not exist.',
        'X [ERROR] NoSuchKey: The specified key does not exist. [code: 10007]',
        'the object does not exist',
        'Error: 404 Not Found',
        'R2 error (nosuchkey)',
    ]) {
        assert.equal(publishedStateFrom({ status: 1, output }), false, output);
    }
    for (const output of [
        'Authentication error [code: 10000]',
        'ENOTFOUND api.cloudflare.com',
        'The specified bucket does not exist. [code: 10006]',
        'NoSuchBucket',
        '',
    ]) {
        assert.throws(() => publishedStateFrom({ status: 1, output }), /Could not tell/, output);
    }
});
