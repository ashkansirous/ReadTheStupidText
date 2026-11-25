import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { CAPTION_KEYS, captionStyle, faceFullNames, faceHasGlyph, fontFaces, linesProblem, loadPreviewCaptions, previewLanguages, previewRows, previewVariables } from './preview-locales.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const data = loadPreviewCaptions();
const screenshots = JSON.parse(readFileSync(join(HERE, '..', '..', '..', '..', 'docs', 'store-listings', 'screenshots', 'captions.json'), 'utf8')).dino;
const windowsFonts = join(process.env.WINDIR ?? 'C:\\Windows', 'Fonts');
const hasWindowsFonts = existsSync(join(windowsFonts, 'YuGothB.ttc')) && existsSync(join(windowsFonts, 'msyh.ttc'));

test('every App Store locale has a language with all seven lines; all 16 locales map to 14 languages', () => {
    assert.equal(Object.keys(data.appleLocales).length, 16);
    const langs = previewLanguages(data);
    assert.equal(langs.length, 14);
    for (const lang of langs) assert.equal(linesProblem(data.lines[lang]), '', lang);
});

test('English keeps the accepted 2026-11-25 captions', () => {
    assert.deepEqual(data.lines.en, {
        cap1: 'Stories read aloud', cap2: 'A voice for every character', cap3: 'Record Grandma as the wolf', cap4: 'Stories in 15 languages',
        cap5: 'New books on the shelf', cap6: 'Some books need a subscription', endSub: 'Kids Books & Stories',
    });
});

test('the reused lines are the screenshot captions word for word', () => {
    const reused = { cap1: '1-picture', cap2: '2-voices', cap4: '4-language', cap5: '5-shelf' };
    for (const lang of previewLanguages(data).filter((l) => l !== 'en')) {
        // zh cap1 is its own line: the screenshot's adds "bedtime stories", which "Stories read aloud" does not say (review pass 2).
        for (const [key, shot] of Object.entries(reused)) if (!(lang === 'zh' && key === 'cap1')) assert.equal(data.lines[lang][key], screenshots[shot][lang], `${lang} ${key}`);
    }
});

test('a missing, multi-line or over-long line is refused', () => {
    const good = Object.fromEntries(CAPTION_KEYS.map((key) => [key, 'x']));
    assert.equal(linesProblem(good), '');
    assert.match(linesProblem({ ...good, cap3: '' }), /cap3 is missing/);
    assert.match(linesProblem({ ...good, cap6: 'a\nb' }), /line break/);
    assert.match(linesProblem({ ...good, endSub: 'x'.repeat(46) }), /longer than 45/);
    assert.throws(() => previewVariables({ lines: { de: { ...good, cap1: undefined } } }, 'de'), /cap1 is missing/);
});

test('the caption font and direction follow store-captions.ps1: Andika for Russian, Vazirmatn right to left for Arabic, Windows UI fonts for CJK', () => {
    assert.deepEqual(captionStyle('de'), { fontKey: 'fredoka', family: 'Fredoka', dir: 'ltr' });
    assert.deepEqual(captionStyle('ru'), { fontKey: 'andika', family: 'Andika', dir: 'ltr' });
    assert.deepEqual(captionStyle('ar'), { fontKey: 'vazirmatn', family: 'Vazirmatn', dir: 'rtl' });
    assert.equal(captionStyle('ja').family, 'CapJa');
    assert.equal(captionStyle('zh').family, 'CapZh');
    const ar = previewVariables(data, 'ar');
    assert.equal(ar.dir, 'rtl');
    assert.equal(ar.capFont, 'Vazirmatn');
    assert.equal(ar.cap4, data.lines.ar.cap4);
});

test('the glyph check finds what a font lacks: Fredoka has Latin but no Cyrillic or Arabic', () => {
    const b = readFileSync(join(HERE, 'assets', 'fonts', 'Fredoka.ttf'));
    const face = fontFaces(b)[0];
    for (const ch of 'Aøåäéß') assert.ok(faceHasGlyph(b, face, ch.codePointAt(0)), ch);
    assert.equal(faceHasGlyph(b, face, 'Ж'.codePointAt(0)), false);
    assert.equal(faceHasGlyph(b, face, 'ك'.codePointAt(0)), false);
});

test('every language passes its glyph check (Windows fonts needed for Japanese and Chinese)', { skip: !hasWindowsFonts && 'no Windows fonts on this machine' }, () => {
    const b = readFileSync(join(windowsFonts, 'YuGothB.ttc'));
    assert.ok(fontFaces(b).some((face) => faceFullNames(b, face).includes('Yu Gothic UI Semibold')));
    const rows = previewRows(data);
    assert.equal(rows.length, 14);
    assert.deepEqual(rows.map((row) => row.lang).sort(), previewLanguages(data).sort());
});

const unescapeHtml = (s) => s.replace(/&quot;/g, '"').replace(/&#39;/g, "'").replace(/&amp;/g, '&');

function compositionVariables(file) {
    const html = readFileSync(join(HERE, file), 'utf8');
    const attr = html.match(/data-composition-variables='([^']*)'/) ?? html.match(/data-composition-variables="([^"]*)"/);
    assert.ok(attr, `${file} has data-composition-variables`);
    return JSON.parse(unescapeHtml(attr[1]));
}

test('both compositions declare exactly the variables previewVariables sends, with the English defaults', () => {
    for (const file of ['index.html', join('variants', 'ipad.html')]) {
        const vars = compositionVariables(file);
        assert.deepEqual(vars.map((v) => v.id).sort(), ['lang', 'dir', 'capFont', ...CAPTION_KEYS].sort(), file);
        const defaults = Object.fromEntries(vars.map((v) => [v.id, v.default]));
        assert.equal(defaults.lang, 'en', file);
        assert.equal(defaults.dir, 'ltr', file);
        assert.equal(defaults.capFont, 'Fredoka', file);
        for (const key of CAPTION_KEYS) assert.equal(defaults[key], data.lines.en[key], `${file} ${key}`);
    }
});

test('a caption with a glyph its font lacks fails previewRows (Cyrillic or Arabic in a Fredoka language)', () => {
    for (const bad of ['Привет', 'مرحبا']) {
        const broken = { appleLocales: { 'de-DE': 'de' }, lines: { de: { ...data.lines.de, cap1: bad } } };
        assert.throws(() => previewRows(broken), /caption glyphs missing/, bad);
    }
    assert.equal(previewRows({ appleLocales: { 'de-DE': 'de' }, lines: { de: data.lines.de } }).length, 1);
});

test('the cmap lookup reads Andika (Cyrillic) and Vazirmatn (Arabic) and rejects letters they lack', () => {
    const fonts = join(HERE, 'assets', 'fonts');
    const cyrillic = readFileSync(join(fonts, 'Andika-Regular.ttf'));
    const arabic = readFileSync(join(fonts, 'Vazirmatn.ttf'));
    const has = (b, ch) => faceHasGlyph(b, fontFaces(b)[0], ch.codePointAt(0));
    for (const ch of 'ЖяЁ') assert.ok(has(cyrillic, ch), ch);
    for (const ch of 'كمس') assert.ok(has(arabic, ch), ch);
    assert.equal(has(cyrillic, 'ك'), false);
    assert.equal(has(cyrillic, '猫'), false);
    assert.equal(has(arabic, 'Ж'), false);
    assert.equal(has(arabic, '猫'), false);
});

const LISTING_FILE = { nb: 'nb' };

test('the end frame subtitle of every non-English language is the listing title after "Dino: "', () => {
    const listings = join(HERE, '..', '..', '..', '..', 'docs', 'store-listings', 'dino');
    for (const lang of previewLanguages(data).filter((l) => l !== 'en')) {
        const text = readFileSync(join(listings, `${LISTING_FILE[lang] ?? lang}.md`), 'utf8');
        const title = text.match(/^(?:Title|Name) \(<= 30\): Dino: (.+?)\s*$/m);
        assert.ok(title, `${lang} listing has a title line`);
        assert.equal(data.lines[lang].endSub, title[1], lang);
    }
});
