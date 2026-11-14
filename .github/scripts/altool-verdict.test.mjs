import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

// Slice 354 item 1: testflight-upload.sh's altool_verdict, sourced (the upload only runs when the file is executed).
const script = join(dirname(fileURLToPath(import.meta.url)), 'testflight-upload.sh').replace(/\\/g, '/');

function verdict(logText) {
    const file = join(mkdtempSync(join(tmpdir(), 'altool-')), 'altool.log').replace(/\\/g, '/');
    writeFileSync(file, logText);
    return spawnSync('bash', ['-c', `source "${script}"; altool_verdict "${file}"`], { encoding: 'utf8' }).status;
}

test('a 500 on the state change followed by UPLOAD SUCCEEDED is a success (run of 2026-11-12)', () => {
    assert.equal(verdict('ERROR: CHANGE UPLOAD STATE TO COMPLETE failed (500)\nUPLOAD SUCCEEDED with no errors\n'), 0);
});

test('a plain success passes', () => {
    assert.equal(verdict('No errors uploading \'x.ipa\'\n'), 0);
});

test('UPLOAD FAILED as the last decisive line fails', () => {
    assert.equal(verdict('UPLOAD SUCCEEDED with no errors\n*** Error: Upload failed\nUPLOAD FAILED\n'), 1);
});

test('an informational line that merely contains "error:" after UPLOAD SUCCEEDED does not fail the upload', () => {
    assert.equal(verdict('UPLOAD SUCCEEDED with no errors\nDelivery note: if an error: occurs, retry\n'), 0);
});

test('an error with no success line fails', () => {
    assert.equal(verdict('ERROR: 500\n'), 1);
});

test('a log with no decisive line fails (exit 0 after a failed upload)', () => {
    assert.equal(verdict('Uploading...\n'), 1);
});
