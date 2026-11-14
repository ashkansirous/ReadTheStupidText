// Slice 354 item 7: the send check in gemini-grab.js, run against a hand-made page (no browser).
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const source = readFileSync(join(dirname(fileURLToPath(import.meta.url)), 'gemini-grab.js'), 'utf8');
const PROMPT = 'Draw the pigs by the brick house';

const picture = src => ({ src, naturalWidth: 200, closest: () => null });

// A page with a prompt box, `thumbnails` attached pictures and a Send button; `onSend(queries)` shows what a click does.
// `stopButton`: the only button.send-button is Gemini's Stop (no "Send message" label, class marked stop).
function page({ thumbnails, onSend, stopButton = false }) {
  const queries = [{ text: 'earlier message' }];
  const asQuery = q => ({ querySelector: () => null, innerText: q.text });
  const area = { parentElement: null };
  const send = {
    disabled: false, className: stopButton ? 'send-button stop' : 'send-button', classList: { contains: c => stopButton && c === 'stop' },
    getAttribute: () => null, click: () => onSend(queries),
  };
  area.querySelector = sel => (!stopButton && /Send message/.test(sel) ? send : null);
  area.querySelectorAll = sel => (sel === 'img' ? thumbnails.map(picture) : (/send-button/.test(sel) ? [send] : []));
  const box = { innerText: PROMPT, parentElement: area, querySelector: () => null, querySelectorAll: () => [] };
  const document = {
    addEventListener: () => { },
    querySelector: sel => (sel === 'rich-textarea' ? box : null),
    querySelectorAll: sel => (sel === 'user-query' ? queries.map(asQuery) : []),
  };
  return document;
}

async function sendChecked(document, pictures) {
  const window = {};
  const context = { window, document, setTimeout: fn => { fn(); return 0; }, Date, Promise, Set, console };
  vm.runInNewContext(source, context);
  return window.__sendChecked(PROMPT, pictures);
}

test('an extra thumbnail is refused, nothing is sent', async () => {
  let clicked = false;
  const result = await sendChecked(page({ thumbnails: ['a.png', 'b.png'], onSend: () => { clicked = true; } }), 1);
  assert.match(result, /^REFUSED: 2 picture\(s\) attached, expected exactly 1/);
  assert.equal(clicked, false);
});

test('exactly the expected picture and a new message with the prompt is SENT', async () => {
  const result = await sendChecked(page({ thumbnails: ['a.png'], onSend: q => q.push({ text: PROMPT }) }), 1);
  assert.equal(result, 'SENT');
});

test('a changed last text with the same message count is UNCONFIRMED, not SENT', async () => {
  const result = await sendChecked(page({ thumbnails: ['a.png'], onSend: q => { q[q.length - 1].text = PROMPT; } }), 1);
  assert.match(result, /^UNCONFIRMED: no new message/);
});

test('a text-only send with a thumbnail still attached is refused', async () => {
  const result = await sendChecked(page({ thumbnails: ['a.png'], onSend: () => { } }), 0);
  assert.match(result, /^REFUSED: 1 picture\(s\) attached, expected exactly 0/);
});

test('a Stop button wearing the send-button class is never clicked, and the answer is UNCONFIRMED', async () => {
  let clicked = false;
  const result = await sendChecked(page({ thumbnails: ['a.png'], stopButton: true, onSend: () => { clicked = true; } }), 1);
  assert.match(result, /^UNCONFIRMED: no Send button/);
  assert.equal(clicked, false);
});
