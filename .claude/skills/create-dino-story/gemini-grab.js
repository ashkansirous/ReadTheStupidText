// Gemini picture grabber for create-dino-story step 6. Inject this whole file with javascript_tool once per page load
// (again after a reload or a new chat). Then `await window.__grab(1)` (1 = newest picture, 2 = the one before), one real
// `computer` click on empty page space (the clipboard write needs a user gesture), then read `window.__clipResult`
// ("896x1200" once the picture is on the clipboard) and run `pwsh -STA gemini-clipboard.ps1 Save <file>`.
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
  window.__grab = async (indexFromEnd) => {
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
    window.__wantClip = window.__blobs.length > 0;
    return window.__blobs.length ? window.__blobs.map(b => b.type + ' ' + b.size).join(',') : 'no blob';
  };
}
'ready'
