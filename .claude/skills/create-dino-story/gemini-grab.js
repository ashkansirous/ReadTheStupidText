// Gemini picture grabber for create-dino-story step 6. Inject this whole file with javascript_tool once per page load
// (again after a reload or a new chat). `await window.__grab(1)` clicks the newest picture's download button (1 = newest,
// 2 = the one before); then save it with gemini-take-download.ps1. The clipboard is NOT touched by default. Fallback only:
// call `await window.__grab(1, { clipboard: true })`, then one real `computer` click on the picture copies it to the clipboard;
// poll `window.__clipResult` for "WxH" and run `pwsh -STA gemini-clipboard.ps1 Save <file>`.
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
    return { box, area: area || box?.parentElement };
  };
  const inChat = img => img.closest('user-query, model-response, message-content, infinite-scroller');
  // Counts distinct picture sources (one thumbnail drawn twice counts once). The page-wide fallback is used only when
  // a picture is expected, so a stray page image can never block a text-only patch.
  const distinct = imgs => new Set(imgs.map(img => img.src)).size;
  const attachedPictures = (area, expected) => {
    const own = [...(area?.querySelectorAll('img') || [])]
      .filter(img => !inChat(img) && (/^(blob:|data:)/.test(img.src) || img.naturalWidth > 48));
    if (own.length || expected === 0) return distinct(own);
    return distinct([...document.querySelectorAll('img')].filter(img => /^(blob:|data:)/.test(img.src) && !inChat(img)));
  };
  const firstDifference = (a, b) => {
    let i = 0;
    while (i < a.length && a[i] === b[i]) i++;
    return i;
  };
  window.__sendChecked = async (expected, pictures) => {
    if (!Number.isInteger(pictures) || pictures < 0) return 'REFUSED: say how many pictures are attached (0 or more)';
    const { box, area } = composer();
    if (!box) return 'REFUSED: no prompt box';
    const typed = squash(box.innerText);
    const want = squash(expected);
    if (!want) return 'REFUSED: empty expected prompt';
    if (typed !== want) {
      const at = firstDifference(typed, want);
      return `REFUSED: prompt box differs at char ${at} of ${want.length} (box has ${typed.length}): "${typed.slice(at, at + 30)}" vs "${want.slice(at, at + 30)}"`;
    }
    const attached = attachedPictures(area, pictures);
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
