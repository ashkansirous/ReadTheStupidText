// Run: node --test ".claude/skills/create-dino-story/*.test.mjs"
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { isGrabResult, isSendResult } from './illustrator-usage.mjs';

const SCRIPT = path.join(path.dirname(fileURLToPath(import.meta.url)), 'illustrator-usage.mjs');

test('a grab result is the blob list __grab returns', () => {
  assert.equal(isGrabResult('image/jpeg 2654604'), true);
  assert.equal(isGrabResult('image/png 10,image/jpeg 20'), true);
  assert.equal(isGrabResult('no blob'), false);
  assert.equal(isGrabResult('no such picture: 0 on the page'), false);
  assert.equal(isGrabResult('1792x2400'), false);
  assert.equal(isGrabResult('ready'), false);
});

test('only a real send counts as the first send', () => {
  assert.equal(isSendResult('ok: create-image, 3:4, 1 attachment, prompt 81 chars, sent'), true);
  assert.equal(isSendResult('SENT'), true);
  assert.equal(isSendResult('dry-run ok: create-image, 3:4, 0 attachment, prompt 31 chars, not sent'), false);
  assert.equal(isSendResult('SENT WITHOUT TEXT: the sent message shows ""'), false);
  assert.equal(isSendResult('REFUSED: no prompt box'), false);
  assert.equal(isSendResult('MISSING: Aspect ratio 3:4'), false);
  assert.equal(isSendResult('UNCONFIRMED: no new message within 30 s'), false);
});

function transcript(steps) {
  const lines = [];
  let t = Date.parse('2026-11-13T10:00:00Z');
  steps.forEach(([script, result], i) => {
    const id = `tu${i}`;
    t += 10000;
    lines.push({ type: 'assistant', timestamp: new Date(t).toISOString(), message: { id: `m${i}`, usage: { input_tokens: 1, cache_read_input_tokens: 100 },
      content: [{ type: 'tool_use', id, name: 'mcp__claude-in-chrome__javascript_tool', input: { text: script } }] } });
    t += 10000;
    lines.push({ type: 'user', timestamp: new Date(t).toISOString(), message: { content: [{ type: 'tool_result', tool_use_id: id,
      content: [{ type: 'text', text: result }, { type: 'text', text: '\n\nTab Context:\n- Executed on tabId: 1' }] }] } });
  });
  return lines.map(l => JSON.stringify(l)).join('\n');
}

function measure(steps) {
  const file = path.join(fs.mkdtempSync(path.join(os.tmpdir(), 'usage-')), 'run.jsonl');
  fs.writeFileSync(file, transcript(steps));
  return execFileSync(process.execPath, [SCRIPT, file], { encoding: 'utf8' });
}

test('counts saved pictures and the first send from results, not from script text', () => {
  const injected = '// `await window.__grab(1)`\nconst re = /["\'*_`]/g; window.__grab = async () => 1; window.__sendChecked = async () => 1; window.__setupPicture(p, 1); "ready"';
  const out = measure([
    [injected, 'ready'],
    ['await window.__setupPicture(p, 1, {dryRun:true})', 'dry-run ok: create-image, 3:4, 1 attachment, prompt 5 chars, not sent'],
    ['await window.__setupPicture(p, 1)', 'ok: create-image, 3:4, 1 attachment, prompt 5 chars, sent'],
    ['await window.__grab(1)', 'image/jpeg 2654604'],
    ['await window.__grab(1, {clipboard:true})', 'image/jpeg 2654604'],
    ['window.__clipResult', '1792x2400'],
    ['await window.__grab(1)', 'no blob'],
  ]);
  assert.match(out, /pictures saved 2\b/);
  assert.match(out, /set-up before the first picture: \d+ s, 2 turns/);
});

test('a run with only a dry run and an injected script saves no picture', () => {
  const out = measure([
    ['window.__grab = async () => 1; await window.__grab(1)', 'ready'],
    ['await window.__setupPicture(p, 0, {dryRun:true})', 'dry-run ok: x'],
  ]);
  assert.match(out, /pictures saved 0\b/);
  assert.match(out, /set-up before the first picture: \d+ s, 0 turns/);
});
