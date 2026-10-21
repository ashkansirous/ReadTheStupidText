import test from 'node:test';
import assert from 'node:assert/strict';

import {
    BUCKET_NAME,
    apkKey,
    assertAssets,
    assetPathsFromEnv,
    buildManifest,
    manifestKey,
    markerCheckArgs,
    planUploads,
    publishUploads,
    publishedMarkerKey,
    publishedStateFrom,
    wranglerArgs,
    wranglerCommand,
} from './publish-downloads.mjs';

const APK = 'ReadTheStupidText-1.4.0.apk';
const BUILD = { platform: 'android', version: '1.4.0', sizeBytes: 123, publishedAt: '2026-10-20T00:00:00.000Z' };

test('plans the APK first and the manifest last', () => {
    const plan = planUploads({ ...BUILD, paths: [`dist/${APK}`] });
    assert.deepEqual(plan.map((upload) => upload.key), [`android/1.4.0/${APK}`, 'manifests/android/1.4.0.json']);
    assert.equal(plan[0].contentType, 'application/vnd.android.package-archive');
    assert.equal(plan[0].path, `dist/${APK}`);
    assert.equal(plan[1].contentType, 'application/json; charset=utf-8');
});

test('the manifest names the version, file, size and time', () => {
    assert.deepEqual(JSON.parse(buildManifest({ version: '1.4.0', sizeBytes: 123, publishedAt: 't' })), {
        version: '1.4.0',
        fileName: APK,
        sizeBytes: 123,
        publishedAt: 't',
    });
});

test('key helpers match the layout the Worker reads', () => {
    assert.equal(apkKey('1.4.0'), `android/1.4.0/${APK}`);
    assert.equal(manifestKey('1.4.0'), 'manifests/android/1.4.0.json');
});

test('refuses the Play bundle, another version, a stray file, two files and none', () => {
    for (const path of ['ReadTheStupidText-1.4.0.aab', 'ReadTheStupidText-1.3.0.apk', 'notes.txt']) {
        assert.throws(() => planUploads({ ...BUILD, paths: [path] }), /is not a android 1\.4\.0 download/);
    }
    assert.throws(() => planUploads({ ...BUILD, paths: [APK, `x/${APK}`] }), /exactly one/);
    assert.throws(() => planUploads({ ...BUILD, paths: [] }), /exactly one/);
});

test('refuses an unknown platform and a malformed version', () => {
    assert.throws(() => planUploads({ ...BUILD, platform: 'windows', paths: [] }), /Unknown PLATFORM/);
    assert.throws(() => planUploads({ ...BUILD, version: '1.4', paths: [] }), /not x\.y\.z/);
});

test('builds the wrangler arguments for the remote bucket', () => {
    const args = wranglerArgs({ key: 'android/1.4.0/a.apk', path: 'out/a.apk', contentType: 'application/vnd.android.package-archive' });
    assert.deepEqual(args, [
        'r2', 'object', 'put', `${BUCKET_NAME}/android/1.4.0/a.apk`,
        '--file', 'out/a.apk', '--remote', '--content-type', 'application/vnd.android.package-archive',
    ]);
});

test('uploads in plan order and stops at the first failure, leaving the manifest unsent', () => {
    const plan = planUploads({ ...BUILD, paths: [APK] }).map((upload) => ({ ...upload, path: upload.path ?? 'm.json' }));
    const sent = [];
    assert.throws(
        () =>
            publishUploads(plan, (args) => {
                sent.push(args[3]);
                throw new Error('boom');
            }),
        /boom/,
    );
    assert.deepEqual(sent, [`${BUCKET_NAME}/android/1.4.0/${APK}`]);
});

test('reads one asset path per line', () => {
    assert.deepEqual(assetPathsFromEnv({ ASSET_PATHS: ' a \r\n\nb\n' }), ['a', 'b']);
    assert.deepEqual(assetPathsFromEnv({}), []);
});

test('runs wrangler through node with the content type as one argument, no shell', () => {
    const upload = { key: 'manifests/android/1.4.0.json', path: 'm.json', contentType: 'application/json; charset=utf-8' };
    const { command, args } = wranglerCommand(wranglerArgs(upload), 'node', 'wrangler.js');
    assert.equal(command, 'node');
    assert.equal(args[0], 'wrangler.js');
    assert.equal(args[args.indexOf('--content-type') + 1], 'application/json; charset=utf-8');
});

test('a version counts as published once its manifest (the last upload) exists', () => {
    assert.equal(publishedMarkerKey(planUploads({ ...BUILD, paths: [APK] })), 'manifests/android/1.4.0.json');
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

test('the already-published check reads the manifest from the remote bucket to stdout', () => {
    const args = markerCheckArgs(planUploads({ ...BUILD, paths: [APK] }));
    assert.deepEqual(args, ['r2', 'object', 'get', `${BUCKET_NAME}/manifests/android/1.4.0.json`, '--remote', '--pipe']);
});

test('checks the assets before anything is read from disk, with a clear message', () => {
    assert.throws(() => assertAssets({ ...BUILD, paths: [] }), /exactly one android APK among the assets \(ASSET_PATHS\), found 0/);
    assert.throws(() => assertAssets({ ...BUILD, paths: [APK, `x/${APK}`] }), /found 2/);
    assert.throws(() => assertAssets({ ...BUILD, paths: ['notes.txt'] }), /is not a android 1\.4\.0 download/);
    assert.doesNotThrow(() => assertAssets({ ...BUILD, paths: [`dist/${APK}`] }));
});

test('Dino Reader uploads go under the dinoreader/ prefix with its own file name', () => {
    const plan = planUploads({ ...BUILD, product: 'dinoreader', paths: ['dist/DinoReader-1.4.0.apk'] });
    assert.deepEqual(plan.map((upload) => upload.key), ['dinoreader/android/1.4.0/DinoReader-1.4.0.apk', 'dinoreader/manifests/android/1.4.0.json']);
    assert.equal(JSON.parse(plan[1].body).fileName, 'DinoReader-1.4.0.apk');
    assert.equal(publishedMarkerKey(plan), 'dinoreader/manifests/android/1.4.0.json');
});

test('each product refuses the other products APK and an unknown product', () => {
    assert.throws(() => planUploads({ ...BUILD, product: 'dinoreader', paths: [APK] }), /is not a android 1\.4\.0 download/);
    assert.throws(() => planUploads({ ...BUILD, paths: ['DinoReader-1.4.0.apk'] }), /is not a android 1\.4\.0 download/);
    assert.throws(() => planUploads({ ...BUILD, product: 'nope', paths: [APK] }), /Unknown PRODUCT/);
});
