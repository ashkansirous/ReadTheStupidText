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
function page({ thumbnails, onSend, stopButton = false, earlier = true }) {
  const queries = earlier ? [{ text: 'earlier message' }] : [];
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

test('the file stores itself in localStorage, and SKILL.md names the same version in its loader line', () => {
  const stored = {};
  const window = {};
  const localStorage = { setItem: (k, v) => { stored[k] = v; } };
  const document = { addEventListener: () => { }, querySelector: () => null, querySelectorAll: () => [] };
  const result = vm.runInNewContext(source, { window, document, localStorage, setTimeout, Date, Promise, Set, console });
  const version = stored.grabver;
  assert.match(version, /^[0-9a-f]{8}$/);
  assert.equal(result, 'ready ' + version);
  assert.match(stored.grabsrc, /^\(function grabMain\(\)/);
  const skill = readFileSync(join(dirname(fileURLToPath(import.meta.url)), 'SKILL.md'), 'utf8');
  assert.ok(skill.includes(`localStorage.grabver==='${version}'`), `SKILL.md loader line must name the hash of gemini-grab.js: ${version} (and its "ready ${version}" text)`);
  assert.ok(skill.includes(`\`ready ${version}\``), `SKILL.md must say the injection answers \`ready ${version}\``);
});

test('the version changes whenever the source changes', () => {
  const versionOf = text => {
    const stored = {};
    vm.runInNewContext(text, { window: {}, document: { addEventListener: () => { }, querySelector: () => null, querySelectorAll: () => [] }, localStorage: { setItem: (k, v) => { stored[k] = v; } }, setTimeout, Date, Promise, Set, console });
    return stored.grabver;
  };
  assert.notEqual(versionOf(source), versionOf(source.replace("'Create image'", "'Create images'")));
  assert.equal(versionOf(source), versionOf(source.replace(/\r?\n/g, '\r\n')));
});

function drawPage(extra = {}) {
  const window = {};
  const delays = [];
  const doc = page({ thumbnails: ['a.png'], onSend: () => { }, ...extra });
  const buttons = extra.downloadButtons ?? [];
  const original = doc.querySelectorAll;
  doc.querySelectorAll = sel => (sel === 'button' ? buttons : original(sel));
  vm.runInNewContext(source, { window, document: doc, setTimeout: (fn, ms) => { delays.push(ms); fn(); return 0; }, Date, Promise, Set, console });
  return { window, delays };
}

const button = { getAttribute: () => 'Download full size image' };

test('__drawPicture refuses when the chat already has a sent message or a picture', async () => {
  const { window } = drawPage();
  assert.match(await window.__drawPicture(PROMPT, 1), /^REFUSED: this chat already has a sent message/);
  const noMessages = drawPage({ earlier: false, downloadButtons: [button] });
  assert.match(await noMessages.window.__drawPicture(PROMPT, 1), /^REFUSED: this chat already has/);
});

test('__drawPicture returns an error answer from __grab as an error, never as bytes', async () => {
  const { window } = drawPage({ earlier: false });
  window.__waitAttached = async () => 1;
  window.__setupPicture = async () => 'ok: create-image';
  window.__waitForPicture = async () => 'READY 1';
  window.__grab = async () => 'no such picture: 0 on the page';
  const result = await window.__drawPicture(PROMPT, 1);
  assert.doesNotMatch(result, /bytes=/);
  assert.match(result, /grabbed: no such picture: 0/);
  window.__drawSent = false;
  window.__grab = async () => 'image/jpeg 1234,image/png 99';
  assert.match(await window.__drawPicture(PROMPT, 1), /bytes=1234,99$/);
});

test('__drawPicture sends only once per page load even when called again', async () => {
  const { window } = drawPage({ earlier: false });
  let setups = 0;
  window.__waitAttached = async () => 1;
  window.__setupPicture = async () => { setups++; return 'ok: create-image'; };
  window.__waitForPicture = async () => 'READY 1';
  window.__grab = async () => 'image/jpeg 5';
  await window.__drawPicture(PROMPT, 1);
  assert.match(await window.__drawPicture(PROMPT, 1), /^REFUSED/);
  assert.equal(setups, 1);
});

test('__drawPicture refuses a second send in the same chat but not in a new chat (the mark is keyed to the chat path)', async () => {
  const { window } = drawPage({ earlier: false });
  window.location = { pathname: '/app/abc' };
  window.__waitAttached = async () => 1;
  window.__setupPicture = async () => 'ok: create-image';
  window.__waitForPicture = async () => 'READY 1';
  window.__grab = async () => 'image/jpeg 5';
  await window.__drawPicture(PROMPT, 1);
  assert.match(await window.__drawPicture(PROMPT, 1), /^REFUSED/);
  window.location = { pathname: '/app' };
  assert.match(await window.__drawPicture(PROMPT, 1), /bytes=5$/);
});

test('__recoverChat never waits less than 20 s between downloads and answers one size per picture', async () => {
  const { window, delays } = drawPage({ downloadButtons: [button, button] });
  window.__grab = async n => (n === 2 ? 'image/jpeg 10,image/jpeg 700' : 'no blob');
  const result = await window.__recoverChat({ gapMs: 1000 });
  assert.equal(result, 'RECOVER 2 pictures | 1: 700 | 2: 0');
  assert.ok(delays.includes(20000));
  assert.ok(!delays.includes(1000));
  const slower = drawPage({ downloadButtons: [button, button] });
  slower.window.__grab = async () => 'no blob';
  await slower.window.__recoverChat({ gapMs: 45000 });
  assert.ok(slower.delays.includes(45000));
});

test('__modelLabel reads the mode picker button', () => {
  const { window } = drawPage({ downloadButtons: [{ getAttribute: () => 'Open mode picker, currently Pro' }] });
  assert.equal(window.__modelLabel(), 'Pro');
  assert.equal(drawPage().window.__modelLabel(), 'NO PICKER');
});

test('__drawPicture refuses, sending nothing, when the attachment count is wrong', async () => {
  let clicked = false;
  const doc = page({ thumbnails: [], earlier: false, onSend: () => { clicked = true; } });
  const window = {};
  vm.runInNewContext(source, { window, document: doc, setTimeout: fn => { fn(); return 0; }, Date, Promise, Set, console });
  const result = await window.__drawPicture(PROMPT, 1, { maxMs: 10 });
  assert.match(result, /^REFUSED: FAILED: 0 attached/);
  assert.equal(clicked, false);
});

test('a Stop button wearing the send-button class is never clicked, and the answer is UNCONFIRMED', async () => {
  let clicked = false;
  const result = await sendChecked(page({ thumbnails: ['a.png'], stopButton: true, onSend: () => { clicked = true; } }), 1);
  assert.match(result, /^UNCONFIRMED: no Send button/);
  assert.equal(clicked, false);
});
