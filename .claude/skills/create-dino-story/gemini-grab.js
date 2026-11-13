// Gemini picture grabber for create-dino-story step 6. Inject this whole file with javascript_tool once per page load
// (again after a reload or a new chat). `await window.__grab(1)` clicks the newest picture's download button (1 = newest,
// 2 = the one before); then save it with gemini-take-download.ps1. The clipboard is NOT touched by default. Fallback only:
// call `await window.__grab(1, { clipboard: true })`, then one real `computer` click on the picture copies it to the clipboard;
// poll `window.__clipResult` for "WxH" and run `pwsh -STA gemini-clipboard.ps1 Save <file>`.
// Slice 356 adds `__attachmentCount()` (confirms the line-up paste) and `await __setupPicture(prompt, pictures[, {dryRun}])`
// (Create image, 3:4, the prompt and Send in one call).
if (!window.__grabReady) {
  window.__grabReady = true;
  window.__blobs = [];
  const pictureUrl = /googleusercontent\.com\/rd-gg|lh3\.google\.com\/rd-gg/;
  const originalFetch = window.fetch;
  window.fetch = async function (...args) {
    const response = await originalFetch.apply(this, args);
    try {
      const url = ((args[0] && args[0].url) || args[0]).toString();
      if (pictureUrl.test(url)) {
        response.clone().blob().then(blob => { if (blob.type.startsWith('image/')) window.__blobs.push(blob); });
      }
    } catch (e) { }
    return response;
  };
  window.__toClipboard = async () => {
    const largest = window.__blobs.reduce((best, blob) => (blob.size > best.size ? blob : best));
    const bitmap = await createImageBitmap(largest);
    const canvas = document.createElement('canvas');
    canvas.width = bitmap.width;
    canvas.height = bitmap.height;
    canvas.getContext('2d').drawImage(bitmap, 0, 0);
    const png = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
    await navigator.clipboard.write([new ClipboardItem({ 'image/png': png })]);
    return bitmap.width + 'x' + bitmap.height;
  };
  document.addEventListener('click', async () => {
    if (!window.__wantClip) return;
    window.__wantClip = false;
    try { window.__clipResult = await window.__toClipboard(); } catch (e) { window.__clipResult = 'ERR ' + e.message; }
  }, true);
  // Page health: a Gemini tab can hang silently (the sidebar chat list never loads, no error shown) and turn out to be
  // Google's CAPTCHA (google.com/sorry) on reload. Returns 'OK', 'CAPTCHA' or 'HUNG: <what is missing>'.
  window.__geminiHealth = () => {
    if (/google\.com\/sorry/.test(location.href)) return 'CAPTCHA';
    const missing = [];
    if (document.readyState !== 'complete') missing.push('page still loading');
    if (!document.querySelector('rich-textarea, div[contenteditable="true"]')) missing.push('no prompt box');
    if (!document.querySelector('a[href*="/app/"]')) missing.push('no chat list in the sidebar');
    return missing.length ? 'HUNG: ' + missing.join(', ') : 'OK';
  };
  const pictureCount = () => [...document.querySelectorAll('button')]
    .filter(b => (b.getAttribute('aria-label') || '') === 'Download full size image').length;
  // Call before sending a prompt: remembers how many pictures the chat already has.
  window.__markPictures = () => { window.__picturesBefore = pictureCount(); return window.__picturesBefore; };
  // Call right after sending: returns 'READY <n>' the moment a new picture exists, or 'WAITING <n>' after
  // maxMs (call it again). Never use fixed sleeps while Gemini draws; they waste up to a minute per picture.
  window.__waitForPicture = async (maxMs = 40000) => {
    const before = window.__picturesBefore ?? 0;
    const start = Date.now();
    let count = pictureCount();
    while (count <= before && Date.now() - start < maxMs) {
      await new Promise(resolve => setTimeout(resolve, 1500));
      count = pictureCount();
    }
    return (count > before ? 'READY ' : 'WAITING ') + count;
  };
  window.__grab = async (indexFromEnd, options) => {
    window.__blobs = [];
    window.__wantClip = false;
    window.__clipResult = null;
    const buttons = [...document.querySelectorAll('button')]
      .filter(b => (b.getAttribute('aria-label') || '') === 'Download full size image');
    if (buttons.length < indexFromEnd) return 'no such picture: ' + buttons.length + ' on the page';
    buttons[buttons.length - indexFromEnd].click();
    for (let i = 0; i < 40 && !window.__blobs.length; i++) await new Promise(resolve => setTimeout(resolve, 500));
    if (window.__blobs.length) await new Promise(resolve => setTimeout(resolve, 1500));
    window.__clipResult = null;
    window.__wantClip = !!(options && options.clipboard) && window.__blobs.length > 0;
    return window.__blobs.length ? window.__blobs.map(b => b.type + ' ' + b.size).join(',') : 'no blob';
  };
}
// The only way to send a picture prompt (2026-11-12: a chat got the line-up with an empty prompt box and drew a
// picnic). Defined outside the guard above, so a tab injected before this existed gets it on the next injection.
// `await window.__sendChecked(prompt, pictures)`: `pictures` is required (1 after pasting the line-up, 0 for a
// text-only patch). Refuses unless the prompt box holds exactly `prompt` and `pictures` pictures are attached, then
// clicks Send and reads the sent message back. Returns:
//   'SENT'                     the sent message shows the whole prompt;
//   'REFUSED: <why>'           nothing was sent; fix what it names and call again;
//   'SENT WITHOUT TEXT: <why>' a message went out without the prompt: the chat is spoiled, start a new chat;
//   'UNCONFIRMED: <why>'       Send was clicked but no new message showed within 30 s: read the chat before
//                              doing anything else, and never send the prompt again into the same chat.
(() => {
  const squash = s => (s || '').replace(/[\s​-‍﻿]+/g, ' ').trim();
  const sendButton = root => root?.querySelector('button[aria-label="Send message"]');
  const plain = s => squash(s).replace(/["'*_`“”‘’]/g, '');
  const lastQuery = () => {
    const queries = [...document.querySelectorAll('user-query')];
    const last = queries[queries.length - 1];
    return { count: queries.length, text: last ? squash((last.querySelector('.query-text') ?? last).innerText) : '' };
  };
  const composer = () => {
    const box = document.querySelector('rich-textarea');
    let area = box;
    for (let i = 0; i < 8 && area && !sendButton(area); i++) area = area.parentElement;
    return { box, area: area && sendButton(area) ? area : null };
  };
  const inChat = img => img.closest('user-query, model-response, message-content, infinite-scroller');
  // Counts distinct picture sources (one thumbnail drawn twice counts once), only inside the prompt/composer area, so a
  // stray page image can never count as an attachment. Returns null (UNKNOWN) when the composer area can't be found:
  // never treat that as 0 or as the expected count (a page-wide count once gave a false pass).
  const distinct = imgs => new Set(imgs.map(img => img.src)).size;
  const attachedPictures = area => (area ? distinct([...area.querySelectorAll('img')]
    .filter(img => !inChat(img) && (/^(blob:|data:)/.test(img.src) || img.naturalWidth > 48))) : null);
  const UNKNOWN_AREA = 'composer area';
  const firstDifference = (a, b) => {
    let i = 0;
    while (i < a.length && a[i] === b[i]) i++;
    return i;
  };
  // Slice 356. How many pictures are attached in the prompt box right now (the line-up paste is confirmed by this, never by
  // a screenshot). Same counting as __sendChecked, so a thumbnail drawn twice counts once. Returns the string
  // 'UNKNOWN' (not a number) when the composer area can't be found: screenshot once and carry on click by click.
  window.__attachmentCount = () => { const n = attachedPictures(composer().area); return n === null ? 'UNKNOWN' : n; };
  const pause = ms => new Promise(r => setTimeout(r, ms));
  const waitFor = async (test, maxMs = 5000) => {
    for (let waited = 0; waited < maxMs; waited += 250) {
      const found = test();
      if (found) return found;
      await pause(250);
    }
    return null;
  };
  // On a cold page the Send button (which marks the composer area) can appear only after the prompt fill: wait for it
  // before reporting UNKNOWN (live check 2026-11-13: the first dry run on a cold page returned MISSING: composer area).
  const composerAreaReady = () => waitFor(() => composer().area, 3000);
  const composerArea = () => composer().area || document;
  const createImageChip = () => composerArea().querySelector('button[aria-label="Deselect Images"]');
  const aspectButton = () => [...composerArea().querySelectorAll('button')]
    .find(b => /^Aspect ratio/.test(b.getAttribute('aria-label') || b.innerText.trim()));
  const ratioIs34 = () => /3:4/.test(aspectButton()?.getAttribute('aria-label') || '');
  // A chip already showing may be stale (kept from an earlier chat or another tool), so it is never trusted: it is deselected
  // first, then Create image is picked through the menu in this call and counts as on only once the chip shows after that click.
  const chooseCreateImage = async () => {
    if (createImageChip()) {
      createImageChip().click();
      if (!(await waitFor(() => !createImageChip(), 2000))) return false;
    }
    composerArea().querySelector('button[aria-label="Upload & tools"]')?.click();
    const item = await waitFor(() => [...document.querySelectorAll('toolbox-drawer-item')]
      .find(e => squash(e.innerText) === 'Create image'));
    if (!item) return false;
    (item.querySelector('button') || item).click();
    return !!(await waitFor(createImageChip));
  };
  // Right before sending: both modes still on (a composer re-render after the prompt fill can reset them).
  const modesStillOn = () => !!createImageChip() && ratioIs34();
  const chooseRatio34 = async () => {
    if (ratioIs34()) return true;
    const opener = aspectButton();
    if (!opener) return false;
    opener.click();
    const option = await waitFor(() => document.querySelector('[role="menuitemradio"][aria-label="3:4"]'));
    if (!option) return false;
    option.click();
    return !!(await waitFor(ratioIs34));
  };
  const fillPrompt = async prompt => {
    const { box } = composer();
    const editor = box?.querySelector('[contenteditable="true"]') || box;
    if (!editor) return false;
    const typeOnce = async () => {
      editor.focus();
      if (!editor.contains(document.activeElement) && document.activeElement !== editor) return false;
      document.execCommand('selectAll');
      document.execCommand('delete');
      document.execCommand('insertText', false, prompt);
      return !!(await waitFor(() => squash(editor.innerText) === squash(prompt), 3000));
    };
    return (await typeOnce()) || (await typeOnce());
  };
  // One call per picture set-up (Slice 356): Create image, 3:4, the prompt, then Send through __sendChecked. Items are found
  // by label, never by position. Returns one line, e.g. 'ok: create-image, 3:4, 1 attachment, prompt 812 chars, sent'.
  // `MISSING: <item>` means that step's control was not found (do that picture click by click); anything else after
  // `REFUSED`/`SENT WITHOUT TEXT`/`UNCONFIRMED` is __sendChecked's own answer, so follow its rules. Paste the line-up first
  // and wait for its thumbnail; `pictures` is how many are attached (0 for none). options.dryRun: do everything except
  // Send and mark nothing, returning 'dry-run ok: ...' (a check without drawing a picture); it leaves the chat in Create image + 3:4
  // with the prompt filled (clear the box with selectAll + delete and click the Create image chip to deselect it before any
  // text-only message).
  window.__setupPicture = async (prompt, pictures, options) => {
    const dryRun = !!(options && options.dryRun);
    if (!Number.isInteger(pictures) || pictures < 0) return 'REFUSED: say how many pictures are attached (0 or more)';
    if (!(await chooseCreateImage())) return 'MISSING: Create image (Upload & tools menu or its item)';
    if (!(await chooseRatio34())) return 'MISSING: Aspect ratio 3:4';
    if (!(await fillPrompt(prompt))) return 'MISSING: prompt box did not take the whole prompt';
    const attached = attachedPictures(await composerAreaReady());
    if (attached === null) return `MISSING: ${UNKNOWN_AREA} (could not count attached pictures; screenshot and go click by click)`;
    if (pictures > 0 ? attached < pictures : attached !== 0) return `REFUSED: ${attached} picture(s) attached, expected ${pictures}`;
    if (!modesStillOn()) return 'MISSING: Create image or 3:4 was reset after the prompt was filled (set them again, then call again)';
    const line = `create-image, 3:4, ${attached} attachment, prompt ${squash(prompt).length} chars`;
    if (dryRun) return `dry-run ok: ${line}, not sent`;
    const previousMark = window.__picturesBefore;
    window.__markPictures(); // only now: every check above passed
    const sent = await window.__sendChecked(prompt, pictures);
    if (/^(REFUSED|MISSING)/.test(sent)) window.__picturesBefore = previousMark; // nothing was sent: keep the old mark
    return sent === 'SENT' ? `ok: ${line}, sent` : sent;
  };
  window.__sendChecked = async (expected, pictures) => {
    if (!Number.isInteger(pictures) || pictures < 0) return 'REFUSED: say how many pictures are attached (0 or more)';
    const { box } = composer();
    if (!box) return 'REFUSED: no prompt box';
    const area = await composerAreaReady();
    const typed = squash(box.innerText);
    const want = squash(expected);
    if (!want) return 'REFUSED: empty expected prompt';
    if (typed !== want) {
      const at = firstDifference(typed, want);
      return `REFUSED: prompt box differs at char ${at} of ${want.length} (box has ${typed.length}): "${typed.slice(at, at + 30)}" vs "${want.slice(at, at + 30)}"`;
    }
    const attached = attachedPictures(area);
    if (attached === null) return `MISSING: ${UNKNOWN_AREA} (could not count attached pictures; screenshot and go click by click)`;
    if (pictures === 0 ? attached !== 0 : attached < pictures) return `REFUSED: ${attached} picture(s) attached, expected ${pictures}`;
    const send = sendButton(area);
    if (!send || send.disabled || send.getAttribute('aria-disabled') === 'true') return 'REFUSED: no enabled Send button';
    const before = lastQuery();
    send.click();
    const isNew = now => now.count > before.count || (now.text && now.text !== before.text);
    const head = plain(want).slice(0, 60);
    let now = lastQuery();
    for (let i = 0; i < 60 && !(isNew(now) && plain(now.text).includes(head)); i++) {
      await new Promise(r => setTimeout(r, 500));
      now = lastQuery();
    }
    if (!isNew(now)) return 'UNCONFIRMED: no new message within 30 s';
    if (plain(now.text).includes(head)) return 'SENT';
    if (plain(now.text).length < 20) return `SENT WITHOUT TEXT: the sent message shows "${now.text.slice(0, 60)}"`;
    return `UNCONFIRMED: the sent message starts "${now.text.slice(0, 60)}"`;
  };
})();
'ready'
