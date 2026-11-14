import test from 'node:test';
import assert from 'node:assert/strict';

import { RETRY_WAIT_SECONDS, isTransientStoreError } from './store-submit-retry.mjs';

test('a 500 InternalServerError from the Ingestion API is retried', () => {
    assert.equal(isTransientStoreError('Retrieving existing Submission...\nError: Response status code does not indicate success: 500 (InternalServerError).'), true);
});

const STAGE = 'Retrieving existing Submission...';

test('other 5xx answers are retried at the same stage', () => {
    assert.equal(isTransientStoreError(STAGE + '\nServiceUnavailable'), true);
    assert.equal(isTransientStoreError(STAGE + '\nThe server returned HTTP status 502'), true);
    assert.equal(isTransientStoreError(STAGE + '\nGatewayTimeout'), true);
});

test('a 5xx at any other stage (upload, commit) is not retried: a draft may already exist', () => {
    assert.equal(isTransientStoreError('Uploading package...\nGatewayTimeout while uploading'), false);
    assert.equal(isTransientStoreError('ServiceUnavailable'), false);
    assert.equal(isTransientStoreError('HTTP 500 InternalServerError'), false);
});

test('a 4xx, a credentials problem or empty output is never retried', () => {
    assert.equal(isTransientStoreError('Response status code does not indicate success: 401 (Unauthorized).'), false);
    assert.equal(isTransientStoreError('HTTP 400 BadRequest: the package is invalid'), false);
    assert.equal(isTransientStoreError('Error while uploading the application package.'), false);
    assert.equal(isTransientStoreError(''), false);
    assert.equal(isTransientStoreError(undefined), false);
});

test('a number that merely starts with 5 in a path or size is not a status', () => {
    assert.equal(isTransientStoreError('uploaded 512 MB of out\\DinoReader.App.msixbundle'), false);
});

test('HTTP/1.1 and HTTP/2 5xx status lines are retried', () => {
    assert.equal(isTransientStoreError(STAGE + '\nHTTP/1.1 503 Service Unavailable'), true);
    assert.equal(isTransientStoreError(STAGE + '\n< HTTP/2 502'), true);
    assert.equal(isTransientStoreError(STAGE + '\nHTTP/2 503'), true);
});

test('a 5xx earlier in a verbose log is ignored when the command then failed with a 4xx', () => {
    const early = ['Status: 503 (Service Unavailable)', 'HTTP/2 503'];
    const filler = Array.from({ length: 20 }, (_, i) => `request ${i} ok`);
    const verbose = [...early, ...filler, 'Error: Response status code does not indicate success: 400 (BadRequest).'].join('\n');
    assert.equal(isTransientStoreError(verbose), false);
    assert.equal(isTransientStoreError(verbose + '\n' + STAGE + '\nError: Response status code does not indicate success: 500 (InternalServerError).'), true);
});

test('the marker far above the error (verbose output or a stack trace after it) is still found', () => {
    const trace = Array.from({ length: 40 }, (_, i) => `   at Microsoft.Store.Frame${i}()`);
    const log = [STAGE, 'Error: Response status code does not indicate success: 500 (InternalServerError).', ...trace].join('\n');
    assert.equal(isTransientStoreError(log), true);
});

test('a 5xx after a later stage (an upload) is not the lookup\'s failure', () => {
    assert.equal(isTransientStoreError([STAGE, 'Uploading package...', 'GatewayTimeout'].join('\n')), false);
});

test('the wait is one minute', () => {
    assert.equal(RETRY_WAIT_SECONDS, 60);
});
